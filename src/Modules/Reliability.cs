// ===================================================================================================================
// cause components to malfunction
// ===================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;


namespace KERBALISM {


public sealed class Reliability : PartModule
{
  // config+persistence
  [KSPField(isPersistant = true)] public string type = string.Empty;  // type of component


  // config
  [KSPField] public double mtbf   = 21600000.0;                       // mean time between failures, in seconds
  [KSPField] public string trait  = string.Empty;                     // trait required to repair, or empty if any trait can repair the component
  [KSPField] public uint   level  = 0;                                // experience level required to repair
  [KSPField] public string desc   = string.Empty;                     // short description of malfunction effect


  // persistence
  [KSPField(isPersistant = true)] public uint   malfunctions;         // level of malfunction
  [KSPField(isPersistant = true)] public double quality;              // manufacturing quality at time of launch
  [KSPField(isPersistant = true)] public double epoch;                // epoch of next failure in seconds
  [KSPField(isPersistant = true)] public double start;                // used to determine message on inspection


  // show malfunction description
  [KSPField(guiActive = false, guiName = "_")] public string Status;


  // store the component
  Component component;


  // pseudo-ctor
  public override void OnStart(StartState state)
  {
    // do nothing in the editors and when compiling parts
    if (!HighLogic.LoadedSceneIsFlight) return;

    // setup ui
    Fields["Status"].guiName = type;
    Events["Inspect"].guiName = Lib.BuildString("Inspect <i>(", type, ")</i>");
    Events["Repair"].guiName = Lib.BuildString("Repair <i>(", type, ")</i>");

    // initialize the reliability component
    switch(type)
    {
      case "Engine": component = new EngineComponent(part.FindModulesImplementing<ModuleEngines>()); break;
      case "Panel": component = new PanelComponent(part.FindModulesImplementing<ModuleDeployableSolarPanel>()); break;
      case "Generator": component = new GeneratorComponent(part.FindModulesImplementing<ModuleGenerator>()); break;
      case "Converter": component = new ConverterComponent(part.FindModulesImplementing<ModuleResourceConverter>()); break;
      case "Harvester": component = new HarvesterComponent(part.FindModulesImplementing<ModuleResourceHarvester>()); break;
      case "ReactionWheel": component = new ReactionWheelComponent(part.FindModulesImplementing<ModuleReactionWheel>()); break;
      case "RCS": component = new RCSComponent(part.FindModulesImplementing<ModuleRCS>()); break;
      case "Greenhouse": component = new GreenhouseComponent(part.FindModulesImplementing<Greenhouse>()); break;
      case "GravityRing": component = new GravityRingComponent(part.FindModulesImplementing<GravityRing>()); break;
      case "Emitter": component = new EmitterComponent(part.FindModulesImplementing<Emitter>()); break;
      default: component = new UnknownComponent(); break;
    }

    // apply serialized malfunction
    if (malfunctions > 0) component.Apply(Math.Pow(0.5, malfunctions));
  }


  public void Update()
  {
    if (malfunctions == 1) Status = Lib.BuildString("<color=yellow>", desc, "</color>");
    else if (malfunctions == 2) Status = Lib.BuildString("<color=red>", desc, "</color>");
    Fields["Status"].guiActive = malfunctions > 0;
    Events["Inspect"].active = malfunctions == 0;
    Events["Repair"].active = malfunctions > 0;
    SetHighlight(part);
  }


  public void FixedUpdate()
  {
    // do nothing in the editor
    if (HighLogic.LoadedSceneIsEditor) return;

    // do nothing until tech tree is ready
    if (!Lib.TechReady()) return;

    // if it has not malfunctioned
    if (malfunctions < 2)
    {
      // deduce quality from technological level if necessary
      if (quality <= double.Epsilon)
      {
        quality = DeduceQuality();
      }

      // calculate epoch of failure if necessary
      if (epoch <= double.Epsilon)
      {
        start = Planetarium.GetUniversalTime();
        epoch = start + mtbf * quality * 2.0 * Lib.RandomDouble();
      }

      // if it has failed, trigger malfunction
      if (Planetarium.GetUniversalTime() > epoch) Break();
    }
  }


  public static void BackgroundUpdate(Vessel v, ProtoPartModuleSnapshot m, Reliability reliability, double elapsed_s)
  {
    // if it has not malfunctioned
    if (Lib.Proto.GetUInt(m, "malfunctions") < 2)
    {
      // get epoch
      double epoch = Lib.Proto.GetDouble(m, "epoch");

      // calculate epoch of failure if necessary
      if (epoch <= double.Epsilon)
      {
        double quality = Lib.Proto.GetDouble(m, "quality", 1.0);
        double start = Planetarium.GetUniversalTime();
        epoch = start + reliability.mtbf * quality * 2.0 * Lib.RandomDouble();
        Lib.Proto.Set(m, "start", start);
        Lib.Proto.Set(m, "epoch", epoch);
      }

      // if it has failed, trigger malfunction
      if (Planetarium.GetUniversalTime() > epoch) Break(v, m);
    }
  }


  // show a message with some hint on time to next failure
  [KSPEvent(guiActiveUnfocused = true, guiName = "Inspect", active = false)]
  public void Inspect()
  {
    Vessel v = FlightGlobals.ActiveVessel;
    if (v == null || !v.isEVA || EVA.KerbalData(v).eva_dead) return;

    double time_k = (Planetarium.GetUniversalTime() - start) / (epoch - start);
    if (time_k < 0.2) Message.Post("It is practically new");
    else if (time_k < 0.4) Message.Post("It is still in good shape");
    else if (time_k < 0.6) Message.Post("It will keep working for some more time");
    else if (time_k < 0.8) Message.Post("It is reaching its operational limits");
    else Message.Post("It could fail at any moment now");
  }


  // repair malfunctioned component
  [KSPEvent(guiActiveUnfocused = true, guiName = "Repair", active = false)]
  public void Repair()
  {
    Vessel v = FlightGlobals.ActiveVessel;
    if (v == null || !v.isEVA || EVA.KerbalData(v).eva_dead) return;
    ProtoCrewMember c = v.GetVesselCrew()[0];
    if (trait.Length > 0 && c.trait != trait)
    {
      Message.Post(Lib.BuildString("Only <b>", trait, "s</b> can repair this component"));
      return;
    }
    if (c.experienceLevel < level)
    {
      Message.Post(Lib.BuildString("<b>", c.name, "</b> doesn't have enough experience"));
      return;
    }

    component.Apply(Math.Pow(2.0, malfunctions));
    malfunctions = 0;

    Message.Post(Lib.BuildString("<b>", type, "</b> repaired"), repair_subtext[Lib.RandomInt(repair_subtext.Length)]);
  }


  public void Break()
  {
    if (malfunctions == 2) return;
    ++malfunctions;
    epoch = 0.0;
    component.Apply(0.5);
    ShowMessage(vessel, type);
  }


  public static void Break(Vessel v, ProtoPartModuleSnapshot m)
  {
    // get malfunction level
    uint malfunctions = Lib.Proto.GetUInt(m, "malfunctions");

    // limit number of malfunctions per-component
    if (malfunctions >= 2u) return;

    // increase malfunction
    Lib.Proto.Set(m, "malfunctions", ++malfunctions);

    // reset epoch
    Lib.Proto.Set(m, "epoch", 0.0);

    // show message
    ShowMessage(v, Lib.Proto.GetString(m, "type"));
  }


  // set highlighting
  public static void SetHighlight(Part p)
  {
    // get max malfunction among all reliability components in the part
    uint max_malfunctions = 0;
    foreach(Reliability m in p.FindModulesImplementing<Reliability>())
    {
      max_malfunctions = Math.Max(max_malfunctions, m.malfunctions);
    }

    // note: when solar panels break (the stock mechanic), this throw exceptions inside KSP
    try
    {
      if (DB.Ready() && DB.VesselData(p.vessel.id).cfg_highlights > 0)
      {
        switch(max_malfunctions)
        {
          case 0:
            p.SetHighlightDefault();
            break;

          case 1:
            p.SetHighlightType(Part.HighlightType.AlwaysOn);
            p.SetHighlightColor(Color.yellow);
            p.SetHighlight(true, false);
            break;

          default:
            p.SetHighlightType(Part.HighlightType.AlwaysOn);
            p.SetHighlightColor(Color.red);
            p.SetHighlight(true, false);
            break;
        }
      }
      else
      {
        p.SetHighlightDefault();
      }
    }
    catch {}
  }


  public static void ShowMessage(Vessel v, string type)
  {
    if (DB.Ready() && DB.VesselData(v.id).cfg_malfunction == 1)
    {
      Message.Post(Severity.warning, Lib.BuildString("<b>", type, "</b> malfunctioned on <b>", v.vesselName, "</b>"));
    }
  }


  public override string GetInfo()
  {
    return Lib.BuildString
    (
      Lib.Specifics(Lib.BuildString("The ", type, " can malfunction")),
      Lib.Specifics(true, "MTBF <i>(basic quality)</i>", Lib.HumanReadableDuration(mtbf)),
      Lib.Specifics(true, "MTBF <i>(max quality)</i>", Lib.HumanReadableDuration(mtbf * 5.0)),
      Lib.Specifics(true, "Repair specialization", trait.Length > 0 ? trait : "Any"),
      Lib.Specifics(true, "Repair experience level", level.ToString()),
      Lib.Specifics(true, "Effect", desc)
    );
  }


  static string[] repair_subtext =
  {
    "A powerkick did the trick",
    "Ductape, is there something it can't fix?",
    "Fully operational again"
  };


  // ==========================================================================
  // # UTILITIES
  // ==========================================================================


  // return malfunction penalty of a part
  public static double Penalty(ProtoPartSnapshot p, string type, double scale = 0.5)
  {
    // get the module
    // note: if the part has no malfunction, default to no penality
    ProtoPartModuleSnapshot m = p.modules.Find(k => k.moduleName == "Reliability" && Lib.Proto.GetString(k, "type") == type);
    if (m == null) return 1.0;

    // return penalty;
    uint malfunctions = Lib.Proto.GetUInt(m, "malfunctions");
    return Math.Pow(scale, (double)malfunctions);
  }


  // cause a part at random to malfunction
  public static void CauseMalfunction(Vessel v)
  {
    // if vessel is loaded
    if (v.loaded)
    {
      // choose a module at random
      var modules = v.FindPartModulesImplementing<Reliability>();
      if (modules.Count == 0) return;
      var m = modules[Lib.RandomInt(modules.Count)];

      // break it
      m.Break();
    }
    // if vessel is not loaded
    else
    {
      // get all reliability modules
      var modules = new List<KeyValuePair<ProtoPartSnapshot,ProtoPartModuleSnapshot>>();
      foreach(ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
      {
        foreach(ProtoPartModuleSnapshot m in p.modules)
        {
          if (m.moduleName == "Reliability")
          {
            modules.Add(new KeyValuePair<ProtoPartSnapshot,ProtoPartModuleSnapshot>(p, m));
          }
        }
      }

      // choose one at random
      if (modules.Count == 0) return;
      var pair = modules[Lib.RandomInt(modules.Count)];

      // break it
      Reliability.Break(v, pair.Value);
    }
  }


  // return true if it make sense to trigger a malfunction on the vessel
  public static bool CanMalfunction(Vessel v)
  {
    if (v.loaded)
    {
      foreach(Reliability m in v.FindPartModulesImplementing<Reliability>())
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
          if (m.moduleName == "Reliability")
          {
            if (Lib.Proto.GetUInt(m, "malfunctions") < 2u) return true;
          }
        }
      }
    }
    return false;
  }


  // return average component quality
  public static double AverageQuality(Vessel v)
  {
    double quality_sum = 0.0;
    double quality_count = 0.0;
    if (v.loaded)
    {
      foreach(Reliability m in v.FindPartModulesImplementing<Reliability>())
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
          if (m.moduleName == "Reliability")
          {
            quality_sum += Lib.Proto.GetDouble(m, "quality", 1.0);
            quality_count += 1.0;
          }
        }
      }
    }
    return quality_count > 0.0 ? quality_sum / quality_count : 0.0;
  }


  // return max malfunction count among all parts of a vessel, or all parts containing the specified module
  public static uint MaxMalfunction(Vessel v)
  {
    uint max_malfunction = 0;
    if (v.loaded)
    {
      foreach(Reliability m in v.FindPartModulesImplementing<Reliability>())
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
          if (m.moduleName == "Reliability")
          {
            max_malfunction = Math.Max(max_malfunction, Lib.Proto.GetUInt(m, "malfunctions"));
          }
        }
      }
    }
    return max_malfunction;
  }


  // ==========================================================================
  // # QUALITY
  // ==========================================================================


  // deduce quality from technological level
  public static double DeduceQuality()
  {
    double[] value = {1.0, 2.0, 3.0, 4.0, 5.0};
    return value[Lib.CountTech(manufacturing_quality.techs)];
  }


  // return some kind of human readable description for quality
  public static string QualityToString(double quality)
  {
    if (quality <= 1.5) return "poor";
    if (quality <= 2.5) return "mediocre";
    if (quality <= 4.5) return "modest";
    if (quality <= 6.5) return "good";
    return "good";
  }


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


  // ==========================================================================
  // # COMPONENTS
  // ==========================================================================


  public abstract class Component
  {
    // break/repair the component
    public abstract void Apply(double k);
  }


  sealed class EngineComponent : Component
  {
    public EngineComponent(List<ModuleEngines> engines)
    {
      this.engines = engines;
    }

    public override void Apply(double k)
    {
      foreach(var m in engines)
      {
        m.heatProduction /= (float)k;
      }
    }

    List<ModuleEngines> engines;
  }


  sealed class PanelComponent : Component
  {
    public PanelComponent(List<ModuleDeployableSolarPanel> panels)
    {
      this.panels = panels;
    }

    public override void Apply(double k)
    {
      foreach(var m in panels)
      {
        m.resources[0].rate *= k;
      }
    }

    List<ModuleDeployableSolarPanel> panels;
  }


  sealed class GeneratorComponent : Component
  {
    public GeneratorComponent(List<ModuleGenerator> generators)
    {
      this.generators = generators;
    }

    public override void Apply(double k)
    {
      foreach(var m in generators)
      {
        foreach(var r in m.outputList) r.rate *= k;
      }
    }

    List<ModuleGenerator> generators;
  }


  sealed class ConverterComponent : Component
  {
    public ConverterComponent(List<ModuleResourceConverter> converters)
    {
      this.converters = converters;
    }

    public override void Apply(double k)
    {
      foreach(var m in converters)
      {
        foreach(var r in m.outputList) r.Ratio *= k;
      }
    }

    List<ModuleResourceConverter> converters;
  }


  sealed class HarvesterComponent : Component
  {
    public HarvesterComponent(List<ModuleResourceHarvester> harvesters)
    {
      this.harvesters = harvesters;
    }

    public override void Apply(double k)
    {
      foreach(var m in harvesters)
      {
        m.Efficiency *= (float)k;
      }
    }

    List<ModuleResourceHarvester> harvesters;
  }


  sealed class ReactionWheelComponent : Component
  {
    public ReactionWheelComponent(List<ModuleReactionWheel> reaction_wheels)
    {
      this.reaction_wheels = reaction_wheels;
    }

    public override void Apply(double k)
    {
      foreach(var m in reaction_wheels)
      {
        m.PitchTorque *= (float)k;
        m.YawTorque *= (float)k;
        m.RollTorque *= (float)k;
      }
    }

    List<ModuleReactionWheel> reaction_wheels;
  }

  sealed class RCSComponent : Component
  {
    public RCSComponent(List<ModuleRCS> rcs_engines)
    {
      this.rcs_engines = rcs_engines;
    }

    public override void Apply(double k)
    {
      foreach(var m in rcs_engines)
      {
        foreach(var prop in m.propellants)
        {
          prop.ratio /= (float)Math.Pow(k, 1.5);
        }
      }
    }

    List<ModuleRCS> rcs_engines;
  }


  sealed class GreenhouseComponent : Component
  {
    public GreenhouseComponent(List<Greenhouse> greenhouses)
    {
      this.greenhouses = greenhouses;
    }

    public override void Apply(double k)
    {
      foreach(var m in greenhouses)
      {
        m.ec_rate /= k;
      }
    }

    List<Greenhouse> greenhouses;
  }

  sealed class GravityRingComponent : Component
  {
    public GravityRingComponent(List<GravityRing> rings)
    {
      this.rings = rings;
    }

    public override void Apply(double k)
    {
      foreach(var m in rings)
      {
        m.ec_rate /= k;
      }
    }

    List<GravityRing> rings;
  }


  sealed class EmitterComponent : Component
  {
    public EmitterComponent(List<Emitter> emitters)
    {
      this.emitters = emitters;
    }

    public override void Apply(double k)
    {
      foreach(var m in emitters)
      {
        m.ec_rate /= k;
      }
    }

    List<Emitter> emitters;
  }

  sealed class UnknownComponent : Component
  {
    public override void Apply(double k) {}
  }
}


} // KERBALISM

