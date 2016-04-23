// ===================================================================================================================
// Malfunction module
// implement the malfunction mechanic
// ===================================================================================================================


using System;
using System.Collections.Generic;
using HighlightingSystem;
using UnityEngine;


namespace KERBALISM {


public class Malfunction : PartModule
{
  // manufacturing quality technologies
  public class ManufacturingQuality
  {
    public ManufacturingQuality()
    {
      var cfg = Lib.ParseConfig("Kerbalism/Patches/Malfunctions/ManufacturingQuality");
      this.techs[0] = Lib.ConfigValue(cfg, "tech0", "advConstruction");
      this.techs[1] = Lib.ConfigValue(cfg, "tech1", "specializedConstruction");
      this.techs[2] = Lib.ConfigValue(cfg, "tech2", "composites");
      this.techs[3] = Lib.ConfigValue(cfg, "tech2", "metaMaterials");
    }
    public string[] techs = {"", "", "", ""};
  }
  public static ManufacturingQuality manufacturing_quality = new ManufacturingQuality();


  // cfg
  [KSPField(isPersistant = true)] public double min_lifetime;             // no-malfunctions guaranteed lifetime in seconds
  [KSPField(isPersistant = true)] public double max_lifetime;             // malfunctions guaranteed lifetime in seconds

  // data
  [KSPField(isPersistant = true)] public uint malfunctions;               // level of malfunctions
  [KSPField(isPersistant = true)] public double age;                      // age since last malfunction
  [KSPField(isPersistant = true)] public double lifetime;                 // current lifetime
  [KSPField(isPersistant = true)] public double quality;                  // tech dependent quality factor
  [KSPField(isPersistant = true)] public string malfunction_msg;          // malfunction message content
  [KSPField(isPersistant = true)] public string repair_msg;               // repair message content
  [KSPField(isPersistant = true)] public string status_msg;               // rmb status message content

  // rmb status
  [KSPField(guiActive = false, guiName = "Malfunction")] public string Status;


  public override string GetInfo()
  {
    return "This part can malfunction";
  }


  public static string PrepareMsg(string s, Vessel v, uint malfunctions)
  {
    return s.Replace("$VESSEL", v.vesselName)
            .Replace("$PERC", (Math.Pow(0.5, (double)malfunctions) * 100.0).ToString("F0") + "%")
            .Replace("$OVERHEATING", (Math.Pow(4.0, (double)malfunctions) * 100.0).ToString("F0") + "%")
            .Replace("$NEWLINE", "\n");
  }


  public override void OnLoad(ConfigNode node)
  {
    // do nothing in the editors and when compiling parts
    if (!HighLogic.LoadedSceneIsFlight) return;

    // apply serialized malfunction level
    if (malfunctions > 0) Apply(Math.Pow(0.5, malfunctions));
  }


  // repair event
  [KSPEvent(guiActiveUnfocused = true, guiName = "Repair", active = false)]
  public void Repair()
  {
    // do nothing if something is wrong, or the eva kerbal is dead
    Vessel v = FlightGlobals.ActiveVessel;
    if (v == null || !v.isEVA || EVA.IsDead(v)) return;

    // if the kerbal isn't an engineer, show a message and do nothing
    if (v.GetVesselCrew()[0].trait != "Engineer")
    {
      Message.Post("Only <b>Engineers</b> can repair parts");
      return;
    }

    // restore full functionality
    Apply(Math.Pow(2.0, (double)malfunctions));
    malfunctions = 0;

    // show a message
    Message.Post(Severity.relax, PrepareMsg(repair_msg, v, malfunctions));
  }


  // trigger malfunction
  public void Break()
  {
    // reset age and lifetime
    age = 0.0;
    lifetime = 0.0;

    // apply malfunction penalty immediately
    Apply(0.5);

    // increase malfunction
    ++malfunctions;

    // show message
    if (DB.Ready() && DB.VesselData(vessel.id).cfg_malfunction == 1)
    {
      Message.Post(Severity.warning, PrepareMsg(malfunction_msg, vessel, malfunctions));
    }

    // record first malfunction
    if (DB.Ready()) DB.NotificationData().first_malfunction = 1;
  }


  // trigger malfunction for unloaded module
  public static void Break(Vessel v, ProtoPartModuleSnapshot m)
  {
    // get data
    uint malfunctions = Lib.GetProtoValue<uint>(m, "malfunctions");
    double lifetime = Lib.GetProtoValue<double>(m, "lifetime");
    double age = Lib.GetProtoValue<double>(m, "age");
    string malfunction_msg = m.moduleValues.GetValue("malfunction_msg");

    // reset age and lifetime
    age = 0.0;
    lifetime = 0.0;

    // increase malfunction
    ++malfunctions;

    // show message
    if (DB.Ready() && DB.VesselData(v.id).cfg_malfunction == 1)
    {
      Message.Post(Severity.warning, PrepareMsg(malfunction_msg, v, malfunctions));
    }

    // record first malfunction
    if (DB.Ready()) DB.NotificationData().first_malfunction = 1;

    // save data
    Lib.SetProtoValue<uint>(m, "malfunctions", malfunctions);
    Lib.SetProtoValue<double>(m, "lifetime", lifetime);
    Lib.SetProtoValue<double>(m, "age", age);
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
        case "ModuleResourceConverter":     Apply((ModuleResourceConverter)m, k);    break;
        case "ModuleResourceHarvester":     Apply((ModuleResourceHarvester)m, k);    break;
        case "ModuleReactionWheel":         Apply((ModuleReactionWheel)m, k);        break;
        case "Antenna":                     Apply((Antenna)m, k);                    break;
        //TODO: ModuleCurvedSolarPanel malfunctions
      }
    }
  }


  void Apply(ModuleEngines m, double k)
  {
    m.heatProduction /= (float)(k * k);
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
    m.penalty *= k;
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

    // update rmb ui
    Events["Repair"].active = malfunctions > 0;
    Fields["Status"].guiActive = malfunctions > 0;
    Status = malfunctions == 0 ? "" : PrepareMsg(status_msg, vessel, malfunctions);

    // generate lifetime if necessary
    if (lifetime <= double.Epsilon)
    {
      lifetime = min_lifetime + (max_lifetime - min_lifetime) * Lib.RandomDouble();
    }

    // accumulate age
    age += TimeWarp.fixedDeltaTime * RadiationInfluence(vessel) / quality;

    // check age and malfunction if needed
    if (age > lifetime) Break();
  }


  // implement malfunction mechanics for unloaded vessels
  public static void BackgroundUpdate(Vessel vessel, uint flight_id)
  {
    // get data
    ProtoPartModuleSnapshot m = Lib.GetProtoModule(vessel, flight_id, "Malfunction");
    uint malfunctions = Lib.GetProtoValue<uint>(m, "malfunctions");
    double min_lifetime = Lib.GetProtoValue<double>(m, "min_lifetime");
    double max_lifetime = Lib.GetProtoValue<double>(m, "max_lifetime");
    double lifetime = Lib.GetProtoValue<double>(m, "lifetime");
    double age = Lib.GetProtoValue<double>(m, "age");
    double quality = Lib.GetProtoValue<double>(m, "quality");
    string malfunction_msg = m.moduleValues.GetValue("malfunction_msg");

    // if for some reason quality wasn't set, default to 1.0 (but don't save it)
    // note: for example, resque vessels failure get background update without prelaunch
    if (quality <= double.Epsilon) quality = 1.0;

    // generate lifetime if necessary
    if (lifetime <= double.Epsilon)
    {
      lifetime = min_lifetime + (max_lifetime - min_lifetime) * Lib.RandomDouble();
    }

    // accumulate age
    age += TimeWarp.fixedDeltaTime * RadiationInfluence(vessel) / quality;

    // save data
    // note: done before checking for malfunction because proto Break change data again
    Lib.SetProtoValue<double>(m, "lifetime", lifetime);
    Lib.SetProtoValue<double>(m, "age", age);

    // check age and malfunction if needed
    if (age > lifetime) Break(vessel, m);
  }


  // deduce quality from technological level
  public static double DeduceQuality()
  {
    double[] value = {1.0, 2.0, 4.0, 6.0, 8.0};
    return value[Lib.CountTechs(manufacturing_quality.techs)];
  }


  // return malfunction penalty of a part
  public static double Penalty(ProtoPartSnapshot p)
  {
    // get the module
    // note: if the part has no malfunction, default to no penality
    ProtoPartModuleSnapshot m = p.modules.Find(k => k.moduleName == "Malfunction");
    if (m == null) return 1.0;

    // return penality;
    uint malfunctions = Lib.GetProtoValue<uint>(m, "malfunctions");
    return Math.Pow(0.5, (double)malfunctions);
  }


  // return max malfunction count among all parts of a vessel
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
            max_malfunction = Math.Max(max_malfunction, Lib.GetProtoValue<uint>(m, "malfunctions"));
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
            quality_sum += Lib.GetProtoValue<double>(m, "quality");
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
      var p = modules[Lib.RandomInt(modules.Count)];
      var m = p.Value;

      // break it
      Malfunction.Break(v, m);
    }
  }


  // return true if a vessel has a failure module
  public static bool CanMalfunction(Vessel v)
  {
    if (v.loaded)
    {
      return v.FindPartModulesImplementing<Malfunction>().Count > 0;
    }
    else
    {
      foreach(ProtoPartSnapshot part in v.protoVessel.protoPartSnapshots)
      {
        if (part.modules.Find(k => k.moduleName == "Malfunction") != null) return true;
      }
    }
    return false;
  }


  // used to influence aging speed using radiation
  public static double RadiationInfluence(Vessel v)
  {
    vessel_info vi = Cache.VesselInfo(v);
    return vi.radiation > Settings.StormRadiation * 0.9
      ? (Lib.CrewCapacity(v) > 0 ? 3.0 : 5.0)
      : 1.0;
  }
}


} // KERBALISM