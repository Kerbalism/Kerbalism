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
            .Replace("$RANGE", (Math.Pow(0.7071, (double)malfunctions) * 100.0).ToString("F0") + "%")
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
    if (v == null || !v.isEVA || EVA.IsDead(v)) return;

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


  // trigger malfunction
  public void Break()
  {
    // limit number of malfunctions per-component
    if (malfunctions >= 2u) return;

    // apply malfunction penalty immediately
    Apply(0.5);

    // increase malfunctions
    ++malfunctions;

    // reset age and lifetime
    age = 0.0;
    lifetime = 0.0;

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

    // limit number of malfunctions per-component
    if (malfunctions >= 2u) return;

    // increase malfunction
    ++malfunctions;

    // reset age and lifetime
    age = 0.0;
    lifetime = 0.0;

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
        case "ModuleResourceConverter":
        case "ModuleKPBSConverter":                                                         //< support PlanetaryBaseSystem converter
        case "FissionReactor":              Apply((ModuleResourceConverter)m, k);    break; //< support NearFuture reactor
        case "ModuleResourceHarvester":     Apply((ModuleResourceHarvester)m, k);    break;
        case "ModuleReactionWheel":         Apply((ModuleReactionWheel)m, k);        break;
        case "Antenna":                     Apply((Antenna)m, k);                    break;
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
    m.penalty *= Math.Sqrt(k);
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
    var av = FlightGlobals.ActiveVessel;
    Events["Repair"].active = malfunctions > 0 && av != null && av.isEVA;
    Events["Inspect"].active = malfunctions == 0 && av != null && av.isEVA;
    Fields["Status"].guiActive = malfunctions > 0;
    Status = malfunctions == 0 ? "" : PrepareMsg(status_msg, vessel, malfunctions);

    // generate lifetime if necessary
    if (lifetime <= double.Epsilon)
    {
      lifetime = min_lifetime + (max_lifetime - min_lifetime) * Lib.RandomDouble();
    }

    // accumulate age
    age += TimeWarp.fixedDeltaTime
         * AgingCurve(age, min_lifetime, max_lifetime)
         * IncentiveRedundancy(vessel, part.flightID)
         / quality;

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
    age += TimeWarp.fixedDeltaTime
         * AgingCurve(age, min_lifetime, max_lifetime)
         * IncentiveRedundancy(vessel, flight_id)
         / quality;

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
  public static double Penalty(ProtoPartSnapshot p, double scale = 0.5)
  {
    // get the module
    // note: if the part has no malfunction, default to no penality
    ProtoPartModuleSnapshot m = p.modules.Find(k => k.moduleName == "Malfunction");
    if (m == null) return 1.0;

    // return penalty;
    uint malfunctions = Lib.GetProtoValue<uint>(m, "malfunctions");
    return Math.Pow(scale, (double)malfunctions);
  }


  // return max malfunction count among all parts of a vessel, or all parts containing the specified module
  public static uint MaxMalfunction(Vessel v, string module_name = "")
  {
    uint max_malfunction = 0;
    if (v.loaded)
    {
      foreach(Malfunction m in v.FindPartModulesImplementing<Malfunction>())
      {
        if (module_name.Length == 0 || m.part.Modules.Contains(module_name))
        {
          max_malfunction = Math.Max(max_malfunction, m.malfunctions);
        }
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
            if (module_name.Length == 0 || p.modules.Find(k => k.moduleName == module_name) != null)
            {
              max_malfunction = Math.Max(max_malfunction, Lib.GetProtoValue<uint>(m, "malfunctions"));
            }
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
            if (Lib.GetProtoValue<uint>(m, "malfunctions") < 2u) return true;
          }
        }
      }
    }
    return false;
  }
  
  
  // used to incentive redundancy by slowing down aging when another part with the same module already failed
  // note: this assume there is only a supported module per-part, if that isn't the case the last one is used
  static double IncentiveRedundancy(Vessel v, uint flight_id)  
  {  
    if (v.loaded)
    {
      string module_name = "";
      foreach(var p in v.Parts)      
      {
        if (p.flightID == flight_id)
        {
          foreach(PartModule m in p.Modules)
          {
            switch(m.moduleName)
            {
              case "ModuleEngines":
              case "ModuleEnginesFX":
              case "ModuleDeployableSolarPanel":
              case "ModuleGenerator":
              case "ModuleResourceConverter":
              case "ModuleKPBSConverter":
              case "FissionReactor":
              case "ModuleResourceHarvester":
              case "ModuleReactionWheel":
              case "Antenna":
                module_name = m.moduleName; break;
            }
          }
          if (module_name.Length == 0) return 1.0;
        }
      }
      
      foreach(var p in v.Parts)
      {
        if (p.flightID == flight_id) continue;        
        if (p.Modules.Contains("Malfunction") && p.Modules.Contains(module_name))
        {
          return 1.0 / (double)(p.Modules.GetModule<Malfunction>().malfunctions + 1u);
        }
      }
    }
    else
    {
      string module_name = "";
      foreach(var p in v.protoVessel.protoPartSnapshots)      
      {
        if (p.flightID == flight_id)
        {
          foreach(var m in p.modules)
          {
            switch(m.moduleName)
            {
              case "ModuleEngines":
              case "ModuleEnginesFX":
              case "ModuleDeployableSolarPanel":
              case "ModuleGenerator":
              case "ModuleResourceConverter":
              case "ModuleKPBSConverter":
              case "FissionReactor":
              case "ModuleResourceHarvester":
              case "ModuleReactionWheel":
              case "Antenna":
                module_name = m.moduleName; break;
            }
          }
          if (module_name.Length == 0) return 1.0;
        }
      }
      
      foreach(var p in v.protoVessel.protoPartSnapshots)
      {
        if (p.flightID == flight_id) continue;        
        var m = p.modules.Find(k => k.moduleName == "Malfunction");
        if (m != null && p.modules.Find(k => k.moduleName == module_name) != null)
        {
          return 1.0 / (double)(Lib.ConfigValue(m.moduleValues, "malfunctions", 0u) + 1u);
        }
      }
    }
    return 1.0;
  }


  // used to make malfunctions less relevant in the middle of lifetime
  // note: normalized so it return 1.0 on average
  public static double AgingCurve(double age, double min_lifetime, double max_lifetime)
  {
    double k = Math.Min(Math.Max(age - min_lifetime, 0.0) / max_lifetime, 1.0);
    return (1.0 - (Math.Min(Math.Min(1.0, k + 0.75), Math.Min(1.0, 1.75 - k)) - 0.75) * 2.0) * 1.6;
  } 
}


} // KERBALISM