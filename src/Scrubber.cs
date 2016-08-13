// ===================================================================================================================
// scrub co2 from a vessel, in a simplistic way
// ===================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class Scrubber : PartModule
{
  // scrubber efficiency technologies
  public class ScrubberEfficiency
  {
    public ScrubberEfficiency()
    {
      var cfg = Lib.ParseConfig("Kerbalism/Patches/System/ScrubberEfficiency");
      this.techs[0] = Lib.ConfigValue(cfg, "tech0", "miniaturization");
      this.techs[1] = Lib.ConfigValue(cfg, "tech1", "precisionEngineering");
      this.techs[2] = Lib.ConfigValue(cfg, "tech2", "scienceTech");
      this.techs[3] = Lib.ConfigValue(cfg, "tech3", "experimentalScience");
    }
    public string[] techs = {"", "", "", ""};
  }
  public static ScrubberEfficiency scrubber_efficiency = new ScrubberEfficiency();

  // config
  [KSPField] public double ec_rate;                    // EC consumption rate per-second
  [KSPField] public double co2_rate;                   // waste consumption rate per-second
  [KSPField] public double co2_ratio = 1.0;            // proportion of waste recycled into resource
  [KSPField] public double intake_rate = 1.0;          // Oxygen production rate inside breathable atmosphere
  [KSPField] public string resource_name = "Oxygen";   // name of resource recycled
  [KSPField] public string waste_name = "CO2";         // name of resource recycled

  // persistence
  [KSPField(isPersistant = true)] public bool is_enabled = true;  // if the scrubber is enabled
  [KSPField(isPersistant = true)] public double efficiency = 0.5; // waste->resource conversion rate

  // rmb status
  [KSPField(guiActive = true, guiName = "Scrubber")] public string Status;  // description of current scrubber state

  // rmb status in editor
  [KSPField(guiActiveEditor = true, guiName = "Scrubber")] public string EditorStatus; // description of current scrubber state (in the editor)


  // rmb enable
  [KSPEvent(guiActive = true, guiName = "Enable Scrubber", active = false)]
  public void ActivateEvent()
  {
    Events["ActivateEvent"].active = false;
    Events["DeactivateEvent"].active = true;
    is_enabled = true;
  }


  // rmb disable
  [KSPEvent(guiActive = true, guiName = "Disable Scrubber", active = false)]
  public void DeactivateEvent()
  {
    Events["ActivateEvent"].active = true;
    Events["DeactivateEvent"].active = false;
    is_enabled = false;
  }


  // editor toggle
  [KSPEvent(guiActiveEditor = true, guiName = "Toggle Scrubber", active = true)]
  public void ToggleInEditorEvent()
  {
    is_enabled = !is_enabled;
    EditorStatus = is_enabled ? "Active" : "Disabled";
  }


  // pseudo-ctor
  public override void OnStart(StartState state)
  {
    // enable/disable rmb ui events based on initial enabled state as per .cfg files
    Events["ActivateEvent"].active = !is_enabled;
    Events["DeactivateEvent"].active = is_enabled;

    // set rmb editor ui status
    EditorStatus = is_enabled ? "Active" : "Disabled";
  }


  // editor/r&d info
  public override string GetInfo()
  {
    return Lib.BuildString
    (
      "Reclaim some of the ", resource_name, ".\n\n",
      "<color=#99FF00>Requires:</color>\n",
      " - ElectricCharge: ", Lib.HumanReadableRate(ec_rate)
    );
  }


  // implement scrubber mechanics
  public void FixedUpdate()
  {
    // do nothing in the editor
    if (HighLogic.LoadedSceneIsEditor) return;

    // deduce quality from technological level if necessary
    // note: done at prelaunch to avoid problems with start()/load() and the tech tree being not consistent
    if (vessel.situation == Vessel.Situations.PRELAUNCH) efficiency = DeduceEfficiency();

    // if for some reason efficiency wasn't set, default to 50%
    // note: for example, resque vessels never get to prelaunch
    if (efficiency <= double.Epsilon) efficiency = 0.5;

    // get vessel info from the cache
    vessel_info vi = Cache.VesselInfo(vessel);

    // do nothing if vessel is invalid
    if (!vi.is_valid) return;

    // get resource cache
    vessel_resources resources = ResourceCache.Get(vessel);

    // get elapsed time
    double elapsed_s = Kerbalism.elapsed_s * vi.time_dilation;

    // if inside breathable atmosphere
    if (vi.breathable)
    {
      // produce oxygen from the intake
      resources.Produce(vessel, resource_name, intake_rate * elapsed_s);

      // set status
      Status = "Intake";
    }
    // if outside breathable atmosphere and enabled
    else if (is_enabled)
    {
      // transform waste + ec into resource
      resource_recipe recipe = new resource_recipe(resource_recipe.scrubber_priority);
      recipe.Input(waste_name, co2_rate * elapsed_s);
      recipe.Input("ElectricCharge", ec_rate * elapsed_s);
      recipe.Output(resource_name, co2_rate * co2_ratio * efficiency * elapsed_s);
      resources.Transform(recipe);

      // set status
      Status = "Running";
    }
    // if outside breathable atmosphere and disabled
    else
    {
      // set status
      Status = "Off";
    }

    // add efficiency to status
    Status += Lib.BuildString(" (Efficiency: ", (efficiency * 100.0).ToString("F0"), "%)");
  }


  // implement scrubber mechanics for unloaded vessels
  public static void BackgroundUpdate(Vessel vessel, ProtoPartModuleSnapshot m, Scrubber scrubber, vessel_info info, vessel_resources resources, double elapsed_s)
  {
    // if inside breathable atmosphere
    if (info.breathable)
    {
      // produce oxygen from the intake
      resources.Produce(vessel, scrubber.resource_name, scrubber.intake_rate * elapsed_s);
    }
    // if outside breathable atmosphere and enabled
    else if (Lib.Proto.GetBool(m, "is_enabled"))
    {
      // transform waste + ec into resource
      resource_recipe recipe = new resource_recipe(resource_recipe.scrubber_priority);
      recipe.Input(scrubber.waste_name, scrubber.co2_rate * elapsed_s);
      recipe.Input("ElectricCharge", scrubber.ec_rate * elapsed_s);
      recipe.Output(scrubber.resource_name, scrubber.co2_rate * scrubber.co2_ratio * Lib.Proto.GetDouble(m, "efficiency", 0.5) * elapsed_s);
      resources.Transform(recipe);
    }
  }


  // deduce efficiency from technological level
  public static double DeduceEfficiency()
  {
    double[] value = {0.5, 0.6, 0.7, 0.8, 0.9};
    return value[Lib.CountTechs(scrubber_efficiency.techs)];
  }


  // return partial data about scrubbers in a vessel
  public class partial_data { public bool is_enabled; }
  public static List<partial_data> PartialData(Vessel v)
  {
    List<partial_data> ret = new List<partial_data>();
    if (v.loaded)
    {
      foreach(var scrubber in v.FindPartModulesImplementing<Scrubber>())
      {
        var data = new partial_data();
        data.is_enabled = scrubber.is_enabled;
        ret.Add(data);
      }
    }
    else
    {
      foreach(ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
      {
        foreach(ProtoPartModuleSnapshot m in p.modules)
        {
          if (m.moduleName == "Scrubber")
          {
            var data = new partial_data();
            data.is_enabled = Lib.Proto.GetBool(m, "is_enabled");
            ret.Add(data);
          }
        }
      }
    }
    return ret;
  }
}


} // KERBALISM