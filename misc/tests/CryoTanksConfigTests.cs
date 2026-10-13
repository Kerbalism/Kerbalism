using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using ModuleManager;
using ModuleManager.Collections;
using ModuleManager.Logging;
using ModuleManager.Progress;

// Runs against the installed KSP ConfigNode and ModuleManager implementations.
// No Unity scene, vessel simulation or live GameData writes are involved.
internal static class CryoTanksTestBootstrap
{
    public static int Main(string[] args)
    {
        string game = args[1];
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs e)
        {
            string name = new AssemblyName(e.Name).Name + ".dll";
            string path = Path.Combine(game, "KSP_x64_Data", "Managed", name);
            if (!File.Exists(path)) path = Path.Combine(game, "GameData", name);
            if (!File.Exists(path) && name == "ModuleManager.dll")
                path = Directory.GetFiles(Path.Combine(game, "GameData"), "ModuleManager.*.dll").Single();
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        // Resolve KSP/MM before the CLR initializes fields containing their types.
        return (int)Assembly.GetExecutingAssembly().GetType("CryoTanksConfigTests")
            .GetMethod("RunTests").Invoke(null, new object[] { args });
    }
}

internal static class CryoTanksConfigTests
{
    private static string repo;
    private static string game;
    private static int assertions;
    private static readonly Dictionary<string, double> densities = new Dictionary<string, double>();
    private static readonly Dictionary<string, ConfigNode> cryo = new Dictionary<string, ConfigNode>();
    private static readonly Dictionary<string, ConfigNode> ftt = new Dictionary<string, ConfigNode>();
    private static ConfigNode defaults;
    private static ConfigNode cryoFile;
    private static ConfigNode fttFile;
    private static ConfigNode felineFile;
    private static UrlDir emptyGameData;

    public static int RunTests(string[] args)
    {
        repo = args[0];
        game = args[1];
        try
        {
            Run();
            Console.WriteLine("PASS: " + assertions + " assertions");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }
    }

    private static void Run()
    {
        Console.WriteLine("KSP ConfigNode / ModuleManager " + typeof(MMPatchLoader).Assembly.GetName().Version);
        emptyGameData = new UrlDir(new UrlDir.ConfigDirectory[0], new UrlDir.ConfigFileType[0]);
        foreach (ConfigNode resource in ConfigNode.Load(Path.Combine(game, "GameData", "CommunityResourcePack", "CommonResources.cfg")).GetNodes("RESOURCE_DEFINITION"))
            densities[resource.GetValue("name")] = Number(resource.GetValue("density"));
        densities["ElectricCharge"] = 0;
        defaults = Load("Profiles/Default.cfg").GetNode("Profile");
        cryoFile = Load("Support/CryoTanks.cfg");
        fttFile = Load("Support/USI/USI_FTT.cfg");
        felineFile = Load("Support/FelineUtilityRover.cfg");
        foreach (ConfigNode process in cryoFile.GetNode("Profile").nodes) cryo[process.GetValue("name") ?? "Supply"] = process;
        foreach (ConfigNode process in fttFile.GetNode("Profile").nodes) ftt[process.GetValue("name") ?? "Supply"] = process;

        foreach (ConfigNode process in cryo.Values.Where(p => p.HasValue("modifier"))) MassConservation(process);
        foreach (ConfigNode process in ftt.Values.Where(p => p.HasValue("modifier"))) MassConservation(process);

        PhaseRoundTrip("h2 to lh2 converter", "lh2 to h2 converter", "Hydrogen", "LqdHydrogen");
        PhaseRoundTrip("ch4 to lch4 converter", "lch4 to ch4 converter", "Methane", "LqdMethane");
        SameRecipe(cryo["h2 to lh2 converter"], ftt["hydrogen liquefaction"]);
        SameRecipe(cryo["lh2 to h2 converter"], ftt["lqd hydrogen evaporator"]);
        SameRecipe(cryo["lqd hydrogen fuel cell"], ftt["lqd hydrogen fuel cell"]);

        ConfigNode gasCell = Process("fuel cell");
        ConfigNode liquidCell = cryo["lqd hydrogen fuel cell"];
        Near(Rate(gasCell, "input", "Hydrogen") * densities["Hydrogen"], Rate(liquidCell, "input", "LqdHydrogen") * densities["LqdHydrogen"], "fuel-cell hydrogen mass");
        foreach (string resource in new[] { "Oxygen", "Water", "ElectricCharge" })
            Near(Rate(gasCell, resource == "Oxygen" ? "input" : "output", resource), Rate(liquidCell, resource == "Oxygen" ? "input" : "output", resource), "same fuel-cell output/input: " + resource);
        Check(!liquidCell.HasValue("specialist_bonus"), "fuel cells cannot gain engineer output");
        ConfigNode electrolysis = Process("water electrolysis");
        Near(Rate(electrolysis, "input", "ElectricCharge"), Rate(liquidCell, "output", "ElectricCharge"), "electrolysis/cell baseline energy");
        foreach (double capacity in new[] { 2.0, 36.0, 90.0 })
        foreach (double engineer in new[] { 1.0, 1.5 })
        {
            double coldEnergy = Rate(cryo["h2 to lh2 converter"], "input", "ElectricCharge");
            double returned = Rate(liquidCell, "output", "ElectricCharge");
            double spent = Rate(electrolysis, "input", "ElectricCharge") + coldEnergy;
            Check((spent - returned) * capacity * engineer > 0, "electrolysis/liquefaction/cell loses energy");
            Near(Rate(electrolysis, "output", "Hydrogen") * capacity * engineer, Rate(cryo["h2 to lh2 converter"], "input", "Hydrogen") * capacity * engineer, "electrolyzer/liquefier throughput");
        }

        ConfigNode sabatier = Process("sabatier process");
        foreach (string name in new[] { "CO2 methanation", "SE-RWGS" })
        {
            foreach (string resource in new[] { "CarbonDioxide", "Hydrogen" })
                Near(Rate(sabatier, "input", resource), Rate(cryo[name], "input", resource), "legacy methane throughput");
            Near(Rate(sabatier, "output", "Water"), Rate(cryo[name], "output", "Water"), "legacy methane water yield");
            Near(Rate(cryo[name], "output", "LqdMethane") * densities["LqdMethane"], 3.46196019 * densities["Methane"], "Sabatier methane mass");
            Check(Rate(cryo[name], "input", "ElectricCharge") >= Rate(cryo["ch4 to lch4 converter"], "input", "ElectricCharge"), "integrated liquefaction is charged");
            Check(cryo[name].GetValue("specialist_bonus") == "true", "methane engineer bonus");
        }
        ConfigNode pyrolysis = cryo["methane pyrolosis"];
        double methaneMoles = Rate(pyrolysis, "input", "LqdMethane") * densities["LqdMethane"] * 1000000 / 16.04246;
        Near(Rate(pyrolysis, "output", "Hydrogen") * densities["Hydrogen"] * 1000000 / 2.01588, 2 * methaneMoles, "pyrolysis 2 H2 per CH4");
        Near(Rate(pyrolysis, "output", "Carbon") * densities["Carbon"] * 1000000 / 12.0107, methaneMoles, "pyrolysis 1 C per CH4");
        ConfigNode eating = defaults.GetNodes("Rule").Single(p => p.GetValue("name") == "eating");
        Near(Rate(cryo["algae bioreactor"], "output", "Food"), Number(eating.GetValue("rate")) / Number(eating.GetValue("interval")), "algae one crew food baseline");

        foreach (bool hasCryo in new[] { false, true })
        foreach (bool hasFtt in new[] { false, true })
            Installation(hasCryo, hasFtt);
        foreach (string name in new[] { "Lynx_ISRU" })
        {
            var phaseFile = new ConfigNode();
            foreach (ConfigNode patch in felineFile.nodes)
                if (patch.GetNodes("MODULE").Any(m => new[] { "_H2Converter", "_LH2Converter" }.Contains(m.GetValue("resource"))))
                    phaseFile.AddNode(patch);
            ConfigNode part = Fixture(name, "ChemicalPlant");
            part = Apply(part, phaseFile, new[] { "ProfileDefault", "CryoTanks", "FelineUtilityRover" });
            Check(Controllers(part).Count() == 2, "Lynx phase controllers");
            foreach (ConfigNode module in Controllers(part)) Near(Number(module.GetValue("capacity")), 36, "Lynx capacity");
            Check(Equivalent(part, Apply(part, phaseFile, new[] { "ProfileDefault", "CryoTanks", "FelineUtilityRover" })), "Lynx patch idempotence");
        }
    }

    private static void Installation(bool hasCryo, bool hasFtt)
    {
        var mods = new List<string> { "ProfileDefault" };
        if (hasCryo) mods.Add("CryoTanks");
        if (hasFtt) mods.Add("FTT");
        using (var logger = new StreamLogger(new MemoryStream()))
        {
            var progress = new PatchProgress(logger);
            var needs = new NeedsChecker(mods, emptyGameData, progress, logger);
            var patchUrl = PatchUrl();
            var processes = new List<ConfigNode>();
            var resources = new List<ConfigNode>();
            foreach (string path in new[] { "Support/CryoTanks.cfg", "Support/USI/USI_FTT.cfg" })
            {
                if (path.Contains("USI") ? !hasFtt : !hasCryo) continue;
                ConfigNode prepared = Load(path);
                needs.CheckNeedsRecursive(prepared, patchUrl);
                processes.AddRange(prepared.GetNode("Profile").GetNodes("Process"));
                resources.AddRange(prepared.GetNodes("RESOURCE_DEFINITION"));
            }
            int cellDefinitions = processes.Count(p => p.GetValue("modifier") == "_LH2FuelCell");
            Check(cellDefinitions == (hasCryo || hasFtt ? 1 : 0), "one fuel-cell recipe per installation");
            Check(resources.Select(r => r.GetValue("name")).Distinct().Count() == resources.Count, "unique pseudo-resource definitions");
            foreach (ConfigNode process in processes)
                Check(resources.Any(r => r.GetValue("name") == process.GetValue("modifier")), "process modifier is defined");
            Check(progress.Counter.errors.Value == 0 && progress.Counter.exceptions.Value == 0, "NEEDS processing has no errors");
        }
        foreach (string name in new[] { "kerbalism-chemicalplant", "MiniISRU", "ISRU", "FuelCell", "FuelCellArray", "kerbalism-container-radial-small", "kerbalism-container-radial-box-small" })
        {
            bool container = name.StartsWith("kerbalism-container");
            bool cell = name.StartsWith("FuelCell");
            string configureId = container ? name.Contains("box") ? "SupplyContainer" : "PressurizedTank" : cell ? "FuelCell" : "ChemicalPlant";
            ConfigNode part = Fixture(name, configureId);
            // Apply in real pass order: UmbraSpaceIndustries, then zzz_CryoTanks.
            part = Apply(part, fttFile, mods);
            part = Apply(part, cryoFile, mods);
            int expected = !hasCryo && !hasFtt ? 0 : container ? 0 : cell ? 1 : hasCryo ? 8 : 2;
            Check(Controllers(part).Count() == expected, "controller count " + name + "/Cryo=" + hasCryo + "/FTT=" + hasFtt);
            Check(Configure(part, "Unrelated").GetNodes("SETUP").Length == 0, "unrelated Configure untouched");
            foreach (ConfigNode module in Controllers(part))
                Near(Number(module.GetValue("capacity")), cell ? name == "FuelCellArray" ? 6 : 1 : name == "ISRU" ? 90 : name == "MiniISRU" ? 36 : 2, "part process capacity");
            if (container)
            {
                int setups = Configure(part, configureId).GetNodes("SETUP").Count(s => s.GetNodes("RESOURCE").Any(r => r.GetValue("name") == "LqdHydrogen"));
                Check(setups == (hasCryo || hasFtt ? 1 : 0), "one LH2 container setup");
                Check(part.GetNodes("MODULE").Count(m => m.GetValue("name") == "ModuleCryoTank") == (hasCryo ? 1 : 0), "LH2 cooling attached once");
            }
            if (hasCryo && !cell && !container)
            {
                string[] legacyNames = { "Hydrogen_liquefaction", "LH2_evaporator", "CO2_Methanation", "Methane_Pyrolosis", "SE_RWGS", "Algae_Bioreactor" };
                foreach (string setup in legacyNames)
                    Check(Configure(part, "ChemicalPlant").GetNodes("SETUP").Any(s => s.GetValue("name") == "#KERBALISM_BranchLoc_" + setup), "saved setup retained: " + setup);
                foreach (ConfigNode setup in Configure(part, "ChemicalPlant").GetNodes("SETUP"))
                {
                    string id = setup.GetNode("MODULE").GetValue("id_value");
                    if (new[] { "_H2Converter", "_LH2Converter", "_CH4Converter", "_LCH4Converter" }.Contains(id))
                    {
                        ConfigNode buffer = setup.GetNode("RESOURCE");
                        Check(buffer != null && Number(buffer.GetValue("maxAmount")) > 0 && Number(buffer.GetValue("amount")) == 0, "empty phase-change buffer");
                    }
                }
            }
            ConfigNode twice = Apply(Apply(part, fttFile, mods), cryoFile, mods);
            Check(Equivalent(part, twice), "patch idempotence: " + name);
            // SystemHeat renames controllers after these patches. Stable guards
            // must still recognize them if a later compatibility pass reuses one.
            foreach (ConfigNode module in Controllers(part)) module.SetValue("name", "ProcessControllerSystemHeat");
            ConfigNode renamed = Apply(Apply(part, fttFile, mods), cryoFile, mods);
            Check(Controllers(renamed).Count() == expected, "SystemHeat controller deduplication");
        }
    }

    private static ConfigNode Apply(ConfigNode part, ConfigNode file, IEnumerable<string> mods)
    {
        using (var logger = new StreamLogger(new MemoryStream()))
        {
            var progress = new PatchProgress(logger);
            var needs = new NeedsChecker(mods, emptyGameData, progress, logger);
            var patchUrl = PatchUrl();
            var context = new PatchContext(patchUrl, new IProtoUrlConfig[0], logger, progress);
            foreach (ConfigNode patch in file.nodes)
            {
                if (!patch.name.StartsWith("@PART[")) continue;
                string need = Argument(patch.name, ":NEEDS[");
                if (need != null && !needs.CheckNeedsExpression(need)) continue;
                string names = Argument(patch.name, "@PART[");
                if (!names.Split(',', '|').Any(mask => Regex.IsMatch(part.GetValue("name"), "^" + Regex.Escape(mask).Replace("\\*", ".*").Replace("\\?", ".") + "$"))) continue;
                string constraints = Argument(patch.name, ":HAS[");
                if (constraints != null && !MMPatchLoader.CheckConstraints(part, constraints)) continue;
                part = MMPatchLoader.ModifyNode(new ImmutableStack<ConfigNode>(part), patch, context);
            }
            Check(progress.Counter.errors.Value == 0 && progress.Counter.exceptions.Value == 0 && progress.Counter.warnings.Value == 0, "ModuleManager patch applies cleanly");
            return part;
        }
    }

    private static string Argument(string text, string prefix)
    {
        int start = text.IndexOf(prefix, StringComparison.Ordinal);
        if (start < 0) return null;
        start += prefix.Length;
        int depth = 1;
        for (int end = start; end < text.Length; ++end)
        {
            if (text[end] == '[') ++depth;
            if (text[end] == ']' && --depth == 0) return text.Substring(start, end - start);
        }
        throw new Exception("Unbalanced selector: " + text);
    }

    private static ConfigNode Fixture(string name, string configureId)
    {
        var part = new ConfigNode("PART");
        part.AddValue("name", name);
        part.AddValue("ContainerVolume", "10");
        foreach (string id in new[] { "Unrelated", configureId })
        {
            ConfigNode configure = part.AddNode("MODULE");
            configure.AddValue("name", "Configure");
            configure.AddValue("configureId", id);
        }
        return part;
    }

    private static UrlDir.UrlConfig PatchUrl()
    {
        return new UrlDir.UrlConfig(new UrlDir.UrlFile(emptyGameData, new FileInfo(Path.Combine(repo, "GameData", "KerbalismConfig", "Support", "CryoTanks.cfg"))), new ConfigNode("TEST"));
    }

    private static IEnumerable<ConfigNode> Controllers(ConfigNode part)
    {
        return part.GetNodes("MODULE").Where(m => (m.GetValue("name") ?? "").StartsWith("ProcessController"));
    }

    private static ConfigNode Configure(ConfigNode part, string id)
    {
        return part.GetNodes("MODULE").Single(m => m.GetValue("configureId") == id);
    }

    private static ConfigNode Load(string path)
    {
        return ConfigNode.Load(Path.Combine(repo, "GameData", "KerbalismConfig", path.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static ConfigNode Process(string name)
    {
        return defaults.GetNodes("Process").Single(p => p.GetValue("name") == name);
    }

    private static Dictionary<string, double> Rates(ConfigNode process, string direction)
    {
        return process.GetValues(direction).Select(v => v.Split('@')).ToDictionary(v => v[0], v => Number(v[1]));
    }

    private static double Rate(ConfigNode process, string direction, string resource)
    {
        return Rates(process, direction)[resource];
    }

    private static void MassConservation(ConfigNode process)
    {
        double input = Rates(process, "input").Sum(r => r.Value * densities[r.Key]);
        double output = Rates(process, "output").Sum(r => r.Value * densities[r.Key]);
        Near(input, output, "mass: " + process.GetValue("name"), 1e-6);
    }

    private static void SameRecipe(ConfigNode a, ConfigNode b)
    {
        foreach (string direction in new[] { "input", "output" })
        {
            Dictionary<string, double> ar = Rates(a, direction), br = Rates(b, direction);
            Check(ar.Count == br.Count, "alias resource count");
            foreach (var r in ar) Near(r.Value, br[r.Key], "alias flow: " + r.Key);
        }
        Check(a.GetValue("specialist_bonus") == b.GetValue("specialist_bonus"), "alias engineer multiplier");
    }

    private static void PhaseRoundTrip(string forward, string reverse, string gasResource, string liquidResource)
    {
        ConfigNode cold = cryo[forward], warm = cryo[reverse];
        Near(Rate(cold, "input", gasResource), Rate(warm, "output", gasResource), "phase gas round trip");
        Near(Rate(cold, "output", liquidResource), Rate(warm, "input", liquidResource), "phase liquid round trip");
        Check(Rate(cold, "input", "ElectricCharge") > 0 && Rate(warm, "input", "ElectricCharge") > 0, "both phase directions cost EC");
        Check(cold.GetValue("specialist_bonus") == "true" && warm.GetValue("specialist_bonus") == "true", "phase engineer multiplier");
    }

    private static bool Equivalent(ConfigNode a, ConfigNode b) { return a.ToString() == b.ToString(); }
    private static double Number(string value) { return double.Parse(value, CultureInfo.InvariantCulture); }
    private static void Near(double a, double b, string message, double relative = 1e-9)
    {
        Check(Math.Abs(a - b) <= Math.Max(Math.Abs(a), Math.Abs(b)) * relative + 1e-18, message + ": " + a + " != " + b);
    }
    private static void Check(bool value, string message)
    {
        ++assertions;
        if (!value) throw new Exception("FAILED: " + message);
    }
}
