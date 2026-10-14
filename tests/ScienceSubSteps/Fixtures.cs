using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace KERBALISM
{
    // Only state and external side effects are faked. run.ps1 extracts the
    // production methods and interval types afresh for every invocation.
    public struct Vector3d { }
    public sealed class CelestialBody { public bool atmosphere; public double atmosphereDepth; }
    public sealed class Vessel { public Guid id = Guid.NewGuid(); public CelestialBody mainBody = new CelestialBody(); }
    public sealed class Situation { public int Id; }
    public sealed class ExperimentInfo
    {
        public bool IsSample;
        public double DataSize = 100, SampleMass;
        public string ExperimentId = "regression";
    }
    public sealed class SubjectData
    {
        public string Id;
        public Situation Situation;
        public ExperimentInfo ExpInfo;
        public double ScienceRemainingToCollect = 1000, SciencePerMB = 1;
    }
    public static class ScienceDB
    {
        public static Dictionary<int, SubjectData> Subjects;
        public static SubjectData GetSubjectData(ExperimentInfo experiment, Situation situation) => Subjects[situation.Id];
    }
    public sealed class VesselSituations
    {
        public Situation GetExperimentSituationAt(ExperimentInfo experiment, double altitude, double latitude, double longitude)
            => new Situation { Id = (int)longitude };
    }
    public sealed class PartData { public Drive Drive; }
    public sealed class VesselData
    {
        public VesselSituations VesselSituations = new VesselSituations();
        public List<Drive> Drives = new List<Drive>();
        public Drive TransmitBufferDrive = new Drive();
        public PartData GetPartData(uint id) => new PartData { Drive = Drives[(int)id - 1] };
    }
    public sealed class File { public double size; }
    public sealed class Sample { public double size; }
    public sealed class Drive
    {
        public double Capacity = 10000;
        public bool Send;
        public Dictionary<SubjectData, File> files = new Dictionary<SubjectData, File>();
        public Dictionary<SubjectData, Sample> samples = new Dictionary<SubjectData, Sample>();
        public double FileCapacityAvailable() => Math.Max(0, Capacity - files.Values.Sum(file => file.size));
        public bool GetFileSend(string id) => Send;
        public void Record_file(SubjectData subject, double size, bool allowImmediateTransmission)
        {
            if (size < 0 || size > FileCapacityAvailable() + 1e-10) throw new Exception("invalid file write");
            if (!files.TryGetValue(subject, out File file)) files[subject] = file = new File();
            file.size += size;
        }
        public void Record_sample(SubjectData subject, double size, double mass)
        {
            if (!samples.TryGetValue(subject, out Sample sample)) samples[subject] = sample = new Sample();
            sample.size += size;
        }
        public static IEnumerable<Drive> GetDrives(VesselData vessel, bool includePrivate) => vessel.Drives;
        public static Drive FileDrive(VesselData vessel, double size) => vessel.Drives[0];
        public static Drive SampleDrive(VesselData vessel, double size, SubjectData subject) => vessel.Drives[0];
    }
    public sealed class ObjectPair<TKey, TValue> { public TKey Key; public TValue Value; }
    public enum ResourceBroker { Experiment }
    public sealed class ResourceInfo
    {
        public double Amount = 10000, Consumed;
        public void Consume(double amount, ResourceBroker broker)
        {
            if (amount < 0 || amount > Amount + 1e-10) throw new Exception("invalid resource consumption");
            Amount -= amount;
            Consumed += amount;
        }
    }
    public sealed class VesselResources
    {
        public Dictionary<string, ResourceInfo> Values = new Dictionary<string, ResourceInfo>();
        public ResourceInfo GetResource(Vessel vessel, string name) => Values[name];
        public void Consume(Vessel vessel, string name, double amount, ResourceBroker broker) => Values[name].Consume(amount, broker);
    }
    public static class Profiler { public static void BeginSample(string name) { } public static void EndSample() { } }
    public static class Settings { public static bool SubStepSimulationLogging; }
    public static class Lib
    {
        public enum LogLevel { Message }
        public static double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(max, value));
        public static void Log(string message, LogLevel level, params object[] values) { }
    }
    public static class Local
    {
        public const string Module_Experiment_issue1 = "situation", Module_Experiment_issue3 = "reset",
            Module_Experiment_issue9 = "requirements", Module_Experiment_issue10 = "resource", Module_Experiment_issue11 = "storage";
    }
    public partial class ExperimentRequirements { public RequireDef[] Requires = Array.Empty<RequireDef>(); }
    public partial class Experiment
    {
        public enum RunningState { Running, Forced }
        public double data_rate = 1, ec_rate = 2;
        public string crew_reset = "";
        public ExperimentRequirements Requirements = new ExperimentRequirements();
        private static int checks;

        private sealed class Fixture
        {
            public Vessel Vessel = new Vessel();
            public VesselData Data = new VesselData();
            public Experiment Prefab = new Experiment();
            public ExperimentInfo Info = new ExperimentInfo();
            public ResourceInfo EC = new ResourceInfo();
            public VesselResources Resources = new VesselResources();
            public List<ObjectPair<string, double>> ResourceDefs = new List<ObjectPair<string, double>>();
            public SubjectData A, B;
            public Fixture()
            {
                A = new SubjectData { Id = "A", Situation = new Situation { Id = 0 }, ExpInfo = Info };
                B = new SubjectData { Id = "B", Situation = new Situation { Id = 1 }, ExpInfo = Info };
                ScienceDB.Subjects = new Dictionary<int, SubjectData> { { 0, A }, { 1, B } };
                Data.Drives.Add(new Drive());
            }
            public double Run(SubStepIntervalResult interval, RunningState state = RunningState.Running, double scalar = 1, int startSituation = -1)
            {
                return RunningUpdateBodyBiomes(Vessel, Data, Prefab, 1, EC, Resources, ResourceDefs,
                    Info, state, interval.ElapsedSeconds, interval.ElapsedSeconds * Prefab.data_rate,
                    scalar, interval, startSituation, out Issue);
            }
            public string Issue;
            public double Size(SubjectData subject) => Data.Drives[0].files.TryGetValue(subject, out File file) ? file.size : 0;
        }

        private static SubStepIntervalResult Interval(Fixture fixture, int[] biomes, double[] altitudes = null,
            bool[] lit = null, bool[] secondStarLit = null)
        {
            var geometry = new SubStepGeometrySample[biomes.Length];
            var first = new SubStepSunSample[biomes.Length];
            var second = new SubStepSunSample[biomes.Length];
            for (int i = 0; i < biomes.Length; i++)
            {
                geometry[i] = new SubStepGeometrySample(i * 10 + 5, 10, new Vector3d(), 0, biomes[i], altitudes == null ? 200 : altitudes[i]);
                first[i] = new SubStepSunSample(i * 10 + 5, 10, new Vector3d(), 1, lit == null || lit[i], 0.25, 100);
                second[i] = new SubStepSunSample(i * 10 + 5, 10, new Vector3d(), 1, secondStarLit != null && secondStarLit[i], 0.5, 300);
            }
            var suns = new Dictionary<int, SubStepSunResult> { { 0, new SubStepSunResult { Samples = Array.AsReadOnly(first) } } };
            if (secondStarLit != null) suns.Add(1, new SubStepSunResult { Samples = Array.AsReadOnly(second) });
            return new SubStepIntervalResult(fixture.Vessel.id, 0, biomes.Length * 10, 1, geometry, suns, null);
        }

        public static string RunTests()
        {
            checks = 0;
            var f = new Fixture();
            f.Prefab.Requirements.Requires = new[] {
                new ExperimentRequirements.RequireDef(ExperimentRequirements.Require.AltitudeMin, 150.0),
                new ExperimentRequirements.RequireDef(ExperimentRequirements.Require.AltitudeMax, 350.0),
                new ExperimentRequirements.RequireDef(ExperimentRequirements.Require.Sunlight, null) };
            var interval = Interval(f, new[] { 0, 1, 0, 1 }, new[] { 100.0, 200, 300, 400 }, new[] { true, true, false, true });
            Near("cross-biome altitude and sunlight production", f.Run(interval, scalar: 0.5), 0.125);
            Near("ineligible biome produces no data", f.Size(f.A), 0);
            Near("eligible biome only", f.Size(f.B), 5);
            Near("only recorded data consumes EC", f.EC.Consumed, 10);

            f = new Fixture();
            f.Prefab.Requirements.Requires = new[] { new ExperimentRequirements.RequireDef(ExperimentRequirements.Require.Sunlight, null) };
            interval = Interval(f, new[] { 0, 1 }, lit: new[] { true, false }, secondStarLit: new[] { false, true });
            Near("first star sunlight weight", interval.SunlightFactorAt(0), 0.25);
            Near("second star sunlight weight", interval.SunlightFactorAt(1), 0.75);
            Near("mixed-star production", f.Run(interval), 0.5);
            Near("mixed-star biome A", f.Size(f.A), 2.5);
            Near("mixed-star biome B", f.Size(f.B), 7.5);
            f.Prefab.Requirements.Requires = new[] { new ExperimentRequirements.RequireDef(ExperimentRequirements.Require.Shadow, null) };
            Near("shadow complements multi-star sunlight", f.Prefab.Requirements.TestSampleRequirements(200, 0, 0.25), 0.75);

            f = new Fixture();
            f.Info.DataSize = 20;
            f.Data.Drives[0].Record_file(f.A, 5, true);
            var otherDrive = new Drive(); otherDrive.Record_file(f.A, 4, true); f.Data.Drives.Add(otherDrive);
            f.Data.TransmitBufferDrive.Record_file(f.A, 6, true);
            Near("forced cap includes every drive and transmit buffer", ForcedRemainingData(f.Data, f.Info, f.A), 5);
            Near("forced repeat-biome cap", f.Run(Interval(f, new[] { 0, 1, 0 }), RunningState.Forced), 0.5);
            Near("forced A cannot be collected twice", f.Size(f.A), 10);
            Near("forced B remains independent", f.Size(f.B), 10);
            Near("forced cap conserves EC", f.EC.Consumed, 30);

            f = new Fixture(); f.A.ScienceRemainingToCollect = 7;
            Near("normal repeated subject science cap", f.Run(Interval(f, new[] { 0, 1, 0 })), 17.0 / 30);
            Near("normal repeated subject total", f.Size(f.A), 7);

            f = new Fixture();
            f.EC.Amount = 30;
            var chemical = new ResourceInfo { Amount = 10 };
            f.Resources.Values["Chemical"] = chemical;
            f.ResourceDefs.Add(new ObjectPair<string, double> { Key = "Chemical", Value = 1 });
            Near("joint EC and material limiting", f.Run(Interval(f, new[] { 0, 1, 0, 1 })), 0.25);
            Near("resource-limited biome A", f.Size(f.A), 5);
            Near("resource-limited biome B", f.Size(f.B), 5);
            Near("material exactly conserved", chemical.Consumed, 10);
            Near("EC follows actual production", f.EC.Consumed, 20);

            f = new Fixture(); f.Data.Drives[0].Capacity = 7;
            Near("disk truncates chronological output", f.Run(Interval(f, new[] { 0, 1 })), 0.35);
            Near("disk writes only available amount", f.Size(f.A), 7);
            Near("full disk stops later biome", f.Size(f.B), 0);
            Near("disk limit conserves EC", f.EC.Consumed, 14);

            f = new Fixture(); f.Data.Drives[0].Send = true; f.Data.Drives[0].Capacity = 4; f.Data.TransmitBufferDrive.Capacity = 3;
            Near("disk and transmit buffer aggregate capacity", f.Run(Interval(f, new[] { 0, 1 })), 0.35);
            Near("buffer is filled first", f.Data.TransmitBufferDrive.files[f.A].size, 3);
            Near("remaining output reaches disk", f.Size(f.A), 4);

            f = new Fixture(); f.Prefab.crew_reset = "Scientist";
            Near("crew reset stops at first subject boundary", f.Run(Interval(f, new[] { 0, 1, 0 }), startSituation: 0), 1.0 / 3);
            if (f.Issue != Local.Module_Experiment_issue3) throw new Exception("crew reset reason missing");
            Near("crew reset excludes later biome", f.Size(f.B), 0);

            var requirements = new ExperimentRequirements { Requires = new[] {
                new ExperimentRequirements.RequireDef(ExperimentRequirements.Require.AtmosphereAltMin, 0.2),
                new ExperimentRequirements.RequireDef(ExperimentRequirements.Require.AtmosphereAltMax, 0.4) } };
            Near("atmosphere altitude fraction accepted", requirements.TestSampleRequirements(30, 100, 1), 1);
            Near("atmosphere requirement rejects airless body", requirements.TestSampleRequirements(30, 0, 1), 0);
            Near("atmosphere altitude fraction rejected", requirements.TestSampleRequirements(50, 100, 1), 0);

            return "PASS: " + checks + " production science checks (biomes, sample requirements, forced caps, resources, storage, reset and multiple stars)";
        }

        private static void Near(string name, double actual, double expected)
        {
            checks++;
            if (double.IsNaN(actual) || Math.Abs(actual - expected) > 1e-10)
                throw new Exception(name + ": expected " + expected + ", got " + actual);
        }
    }
}
