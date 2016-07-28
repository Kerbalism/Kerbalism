// ===================================================================================================================
// implement the malfunction mechanic
// ===================================================================================================================


using System;
using System.Collections.Generic;
using HighlightingSystem;
using UnityEngine;


namespace KERBALISM {


public sealed class Malfunction : PartModule
{
  // manufacturing quality technologies
  public class ManufacturingQuality
  {
    public ManufacturingQuality()
    {
      var cfg = Lib.ParseConfig("Kerbalism/Patches/System/ManufacturingQuality");
      this.techs[0] = Lib.ConfigValue(cfg, "tech0", "advConstruction");
      this.techs[1] = Lib.ConfigValue(cfg, "tech1", "specializedConstruction");
      this.techs[2] = Lib.ConfigValue(cfg, "tech2", "composites");
      this.techs[3] = Lib.ConfigValue(cfg, "tech2", "metaMaterials");
    }
    public string[] techs = {"", "", "", ""};
  }
  public static ManufacturingQuality manufacturing_quality = new ManufacturingQuality();


  // cfg
  [KSPField] public double min_lifetime;             // no-malfunctions guaranteed lifetime in seconds
  [KSPField] public double max_lifetime;             // malfunctions guaranteed lifetime in seconds
  [KSPField] public string malfunction_msg;          // malfunction message content
  [KSPField] public string repair_msg;               // repair message content
  [KSPField] public string status_msg;               // rmb status message content

  // data
  [KSPField(isPersistant = true)] public uint malfunctions;               // level of malfunctions
  [KSPField(isPersistant = true)] public double age;                      // age since last malfunction
  [KSPField(isPersistant = true)] public double lifetime;                 // current lifetime
  [KSPField(isPersistant = true)] public double quality = 1.0;            // tech dependent quality factor

  // rmb status
  [KSPField(guiActive = false, guiName = "Malfunction")] public string Status;


  public override string GetInfo()
  {
    return "This part can malfunction";
  }


  public static string PrepareMsg(string s, Vessel v, uint malfunctions)
  {
    return s
      .Replace("$VESSEL", v.vesselName)
      .Replace("$PERC", Lib.HumanReadablePerc(Math.Pow(0.5, (double)malfunctions)))
      .Replace("$OVERHEATING", Math.Pow(2.0, (double)malfunctions).ToString("F0") + "x")
      .Replace("$RANGE", Lib.HumanReadablePerc(Math.Pow(0.7071, (double)malfunctions)))
      .Replace("$NEWLINE", "\n");
  }


  public override void OnLoad(ConfigNode node)
  {
    // do nothing in the editors and when compiling parts
    if (!HighLogic.LoadedSceneIsFlight) return;

    // apply serialized malfunction level
    if (malfunctions > 0) Apply(Math.Pow(0.5, malfunctions));
  }


  // implement malfunction mechanics
  public void FixedUpdate()
  {
    // do nothing in the editor
    if (HighLogic.LoadedSceneIsEditor) return;

    // deduce quality from technological level if necessary
    // note: done at prelaunch to avoid problems with start()/load() and the tech tree being not consistent
    if (vessel.situation == Vessel.Situations.PRELAUNCH) quality = DeduceQuality();

    // if for some reason quality wasn't set, default to 1.0
    // note: for example, resque vessels never get to prelaunch
    if (quality <= double.Epsilon) quality = 1.0;

    // generate lifetime the first time
    if (lifetime <= double.Epsilon)
    {
      lifetime = min_lifetime + (max_lifetime - min_lifetime) * Lib.RandomDouble();
    }

    // update rmb ui
    var av = FlightGlobals.ActiveVessel;
    Events["Repair"].active = malfunctions > 0 && av != null && av.isEVA;
    Events["Inspect"].active = malfunctions == 0 && av != null && av.isEVA;
    Fields["Status"].guiActive = malfunctions > 0;
    Status = malfunctions == 0 ? "" : PrepareMsg(status_msg, vessel, malfunctions);

    // get vessel info from the cache
    vessel_info vi = Cache.VesselInfo(vessel);

    // get elapsed time
    double elapsed_s = Kerbalism.elapsed_s * vi.time_dilation;

    // accumulate age
    age += elapsed_s / quality;

    // check age and malfunction if needed
    if (age > lifetime) Break();

    // set highlighting
    if (DB.Ready() && DB.VesselData(this.vessel.id).cfg_highlights > 0)
    {
      switch(malfunctions)
      {
        case 0:
          part.SetHighlightDefault();
          break;

        case 1:
          part.SetHighlightType(Part.HighlightType.AlwaysOn);
          part.SetHighlightColor(Color.yellow);
          part.SetHighlight(true, false);
          break;

        default:
          part.SetHighlightType(Part.HighlightType.AlwaysOn);
          part.SetHighlightColor(Color.red);
          part.SetHighlight(true, false);
          break;
      }
    }
    else
    {
      part.SetHighlightDefault();
    }
  }


  // implement malfunction mechanics for unloaded vessels
  public static void BackgroundUpdate(Vessel vessel, ProtoPartModuleSnapshot m, Malfunction malfunction, double elapsed_s)
  {
    // get data
    double lifetime = Lib.Proto.GetDouble(m, "lifetime");
    double age = Lib.Proto.GetDouble(m, "age");
    double quality = Lib.Proto.GetDouble(m, "quality");

    // if for some reason quality wasn't set, default to 1.0
    // note: for example, this may happen with vessels launched before kerbalism is installed
    if (quality <= double.Epsilon)
    {
      quality = 1.0;
      Lib.Proto.Set(m, "quality", quality);
    }

    // generate lifetime if necessary
    // note: for example, this may happen with vessels launched before kerbalism is installed
    if (lifetime <= double.Epsilon)
    {
      lifetime = malfunction.min_lifetime + (malfunction.max_lifetime - malfunction.min_lifetime) * Lib.RandomDouble();
      Lib.Proto.Set(m, "lifetime", lifetime);
    }

    // accumulate age
    age += elapsed_s / quality;
    Lib.Proto.Set(m, "age", age);

    // check age and malfunction if needed
    if (age > lifetime) Break(vessel, m, malfunction);
  }


  // trigger malfunction
  public void Break()
  {
    // limit number of malfunctions per-component
    if (malfunctions >= 2u) return;

    // apply malfunction penalty immediately
    Apply(0.5);

    // increase malfunctions
    ++malfunctions;

    // reset age
    age = 0.0;

    // generate new lifetime
    lifetime = min_lifetime + (max_lifetime - min_lifetime) * Lib.RandomDouble();

    // show message
    if (DB.Ready() && DB.VesselData(vessel.id).cfg_malfunction == 1)
    {
      Message.Post(Severity.warning, PrepareMsg(malfunction_msg, vessel, malfunctions));
    }

    // record first malfunction
    if (DB.Ready()) DB.NotificationData().first_malfunction = 1;
  }


  // trigger malfunction for unloaded module
  public static void Break(Vessel v, ProtoPartModuleSnapshot m, Malfunction malfunction)
  {
    // get data
    uint malfunctions = Lib.Proto.GetUInt(m, "malfunctions");

    // limit number of malfunctions per-component
    if (malfunctions >= 2u) return;

    // increase malfunction
    ++malfunctions;
    Lib.Proto.Set(m, "malfunctions", malfunctions);

    // reset age
    double age = 0.0;
    Lib.Proto.Set(m, "age", age);

    // generate new lifetime
    double lifetime = malfunction.min_lifetime + (malfunction.max_lifetime - malfunction.min_lifetime) * Lib.RandomDouble();
    Lib.Proto.Set(m, "lifetime", lifetime);

    // show message
    if (DB.Ready() && DB.VesselData(v.id).cfg_malfunction == 1)
    {
      Message.Post(Severity.warning, PrepareMsg(malfunction.malfunction_msg, v, malfunctions));
    }

    // record first malfunction
    if (DB.Ready()) DB.NotificationData().first_malfunction = 1;
  }


  public void Apply(double k)
  {
    foreach(PartModule m in part.Modules)
    {
      switch(m.moduleName)
      {
        case "ModuleEngines":               Apply((ModuleEngines)m, k);              break;
        case "ModuleEnginesFX":             Apply((ModuleEnginesFX)m, k);            break;
        case "ModuleDeployableSolarPanel":  Apply((ModuleDeployableSolarPanel)m, k); break;
        case "ModuleGenerator":             Apply((ModuleGenerator)m, k);            break;
        case "ModuleResourceConverter":
        case "ModuleKPBSConverter":                                                         //< support PlanetaryBaseSystem converter
        case "FissionReactor":              Apply((ModuleResourceConverter)m, k);    break; //< support NearFuture reactor
        case "ModuleResourceHarvester":     Apply((ModuleResourceHarvester)m, k);    break;
        case "ModuleReactionWheel":         Apply((ModuleReactionWheel)m, k);        break;
        case "Antenna":                     Apply((Antenna)m, k);                    break;
      }
    }
  }


  // repair event
  [KSPEvent(guiActiveUnfocused = true, guiName = "Repair", active = false)]
  public void Repair()
  {
    // do nothing if something is wrong, or the eva kerbal is dead
    Vessel v = FlightGlobals.ActiveVessel;
    if (v == null || !v.isEVA || Cache.VesselInfo(v).is_eva_dead) return;

    // if the kerbal isn't an engineer, show a message and do nothing
    if (v.GetVesselCrew()[0].trait != "Engineer")
    {
      Message.Post("Only <b>Engineers</b> can repair parts");
      return;
    }

    // restore full functionality
    Apply(Math.Pow(2.0, (double)malfunctions));

    // reset malfunctions counter
    malfunctions = 0;

    // reset age and lifetime
    age = 0.0;
    lifetime = 0.0;

    // show a message
    Message.Post(Severity.relax, PrepareMsg(repair_msg, v, malfunctions));
  }


  // inspect event
  [KSPEvent(guiActiveUnfocused = true, guiName = "Inspect", active = false)]
  public void Inspect()
  {
    // do nothing if something is wrong, or the eva kerbal is dead
    Vessel v = FlightGlobals.ActiveVessel;
    if (v == null || !v.isEVA || Cache.VesselInfo(v).is_eva_dead) return;

    // if the kerbal isn't an engineer, show a message and do nothing
    if (v.GetVesselCrew()[0].trait != "Engineer")
    {
      Message.Post("Only <b>Engineers</b> can inspect parts");
      return;
    }

    // evaluate at what point we are in the lifetime
    double time_k = Lib.Clamp((age - min_lifetime) / max_lifetime, 0.0, 1.0);
    if (time_k > 0.75) Message.Post("This is going to fail");
    else if (time_k > 0.5) Message.Post("This is reaching its limit");
    else if (time_k > 0.25) Message.Post("This is still okay");
    else Message.Post("This is in good shape");
  }


  void Apply(ModuleEngines m, double k)
  {
    m.heatProduction /= (float)k;
  }


  void Apply(ModuleDeployableSolarPanel m, double k)
  {
    m.chargeRate *= (float)k;
  }


  void Apply(ModuleGenerator m, double k)
  {
    foreach(var r in m.outputList) r.rate *= (float)k;
  }


  void Apply(ModuleResourceConverter m, double k)
  {
    foreach(var r in m.outputList) r.Ratio *= k;
  }


  void Apply(ModuleResourceHarvester m, double k)
  {
    m.Efficiency *= (float)k;
  }


  void Apply(ModuleReactionWheel m, double k)
  {
    m.PitchTorque *= (float)k;
    m.YawTorque *= (float)k;
    m.RollTorque *= (float)k;
  }


  void Apply(Antenna m, double k)
  {
    m.penalty *= Math.Sqrt(k);
  }


  // deduce quality from technological level
  public static double DeduceQuality()
  {
    double[] value = {1.0, 2.0, 4.0, 6.0, 8.0};
    return value[Lib.CountTechs(manufacturing_quality.techs)];
  }


  // return malfunction penalty of a part
  public static double Penalty(ProtoPartSnapshot p, double scale = 0.5)
  {
    // get the module
    // note: if the part has no malfunction, default to no penality
    ProtoPartModuleSnapshot m = p.modules.Find(k => k.moduleName == "Malfunction");
    if (m == null) return 1.0;

    // return penalty;
    uint malfunctions = Lib.Proto.GetUInt(m, "malfunctions");
    return Math.Pow(scale, (double)malfunctions);
  }


  // return max malfunction count among all parts of a vessel, or all parts containing the specified module
  public static uint MaxMalfunction(Vessel v)
  {
    uint max_malfunction = 0;
    if (v.loaded)
    {
      foreach(Malfunction m in v.FindPartModulesImplementing<Malfunction>())
      {
        max_malfunction = Math.Max(max_malfunction, m.malfunctions);
      }
    }
    else
    {
      foreach(ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
      {
        foreach(ProtoPartModuleSnapshot m in p.modules)
        {
          if (m.moduleName == "Malfunction")
          {
            max_malfunction = Math.Max(max_malfunction, Lib.Proto.GetUInt(m, "malfunctions"));
          }
        }
      }
    }
    return max_malfunction;
  }


  // return average component quality
  public static double AverageQuality(Vessel v)
  {
    double quality_sum = 0.0;
    double quality_count = 0.0;
    if (v.loaded)
    {
      foreach(Malfunction m in v.FindPartModulesImplementing<Malfunction>())
      {
        quality_sum += m.quality;
        quality_count += 1.0;
      }
    }
    else
    {
      foreach(ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
      {
        foreach(ProtoPartModuleSnapshot m in p.modules)
        {
          if (m.moduleName == "Malfunction")
          {
            // note: deal with vessels launched before kerbalism was installed
            quality_sum += Math.Max(Lib.Proto.GetDouble(m, "quality", 1.0), 1.0);
            quality_count += 1.0;
          }
        }
      }
    }
    return quality_count > 0.0 ? quality_sum / quality_count : 0.0;
  }


  // return some kind of human readable description for quality
  public static string QualityToString(double quality)
  {
    if (quality <= 1.5) return "poor";
    if (quality <= 2.5) return "mediocre";
    if (quality <= 4.5) return "modest";
    if (quality <= 6.5) return "decent";
    return "good";
  }


  // cause a part at random to malfunction
  public static void CauseMalfunction(Vessel v)
  {
    // if vessel is loaded
    if (v.loaded)
    {
      // choose a part at random
      var modules = v.FindPartModulesImplementing<Malfunction>();
      if (modules.Count == 0) return;
      var m = modules[Lib.RandomInt(modules.Count)];

      // break it
      m.Break();
    }
    // if vessel is not loaded
    else
    {
      // choose a part at random
      List<KeyValuePair<ProtoPartSnapshot,ProtoPartModuleSnapshot>> modules = new List<KeyValuePair<ProtoPartSnapshot,ProtoPartModuleSnapshot>>();
      foreach(ProtoPartSnapshot part in v.protoVessel.protoPartSnapshots)
      {
        foreach(ProtoPartModuleSnapshot module in part.modules)
        {
          if (module.moduleName == "Malfunction") modules.Add(new KeyValuePair<ProtoPartSnapshot,ProtoPartModuleSnapshot>(part, module));
        }
      }
      if (modules.Count == 0) return;
      var pair = modules[Lib.RandomInt(modules.Count)];
      var p = pair.Key;
      var m = pair.Value;

      // get malfunction module from prefab, avoid corner case
      Malfunction malfunction = p.partInfo.partPrefab.FindModuleImplementing<Malfunction>();
      if (malfunction == null) return;

      // break it
      Malfunction.Break(v, m, malfunction);
    }
  }


  // return true if it make sense to trigger a malfunction on the vessel
  public static bool CanMalfunction(Vessel v)
  {
    if (v.loaded)
    {
      foreach(Malfunction m in v.FindPartModulesImplementing<Malfunction>())
      {
        if (m.malfunctions < 2u) return true;
      }
    }
    else
    {
      foreach(ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
      {
        foreach(ProtoPartModuleSnapshot m in p.modules)
        {
          if (m.moduleName == "Malfunction")
          {
            if (Lib.Proto.GetUInt(m, "malfunctions") < 2u) return true;
          }
        }
      }
    }
    return false;
  }
}


} // KERBALISM