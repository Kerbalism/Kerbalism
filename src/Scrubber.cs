// ===================================================================================================================
// Scrubber module
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

  // .cfg
  // note: persistent because required in background processing
  [KSPField] public double ec_rate;                    // EC consumption rate per-second
  [KSPField] public double co2_rate;                   // waste consumption rate per-second
  [KSPField] public double efficiency = 0.0;           // waste->resource conversion rate
  [KSPField] public double intake_rate = 1.0;         // Oxygen production rate inside breathable atmosphere
  [KSPField] public string resource_name = "Oxygen";   // name of resource recycled
  [KSPField] public string waste_name = "CO2";         // name of resource recycled

  // persistence
  // note: also configurable per-part
  [KSPField(isPersistant = true)] public bool is_enabled = true;            // if the scrubber is enabled

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

    // get time elapsed from last update
    double elapsed_s = TimeWarp.fixedDeltaTime;

    // if inside breathable atmosphere
    if (Cache.VesselInfo(this.vessel).breathable)
    {
      // produce oxygen from the intake
      this.part.RequestResource(resource_name, -intake_rate * elapsed_s);

      // set status
      Status = "Intake";
    }
    // if outside breathable atmosphere and enabled
    else if (is_enabled)
    {
      // recycle CO2+EC into oxygen
      double co2_required = co2_rate * elapsed_s;
      double co2 = this.part.RequestResource(waste_name, co2_required);
      double ec_required = ec_rate * elapsed_s * (co2 / co2_required);
      double ec = this.part.RequestResource("ElectricCharge", ec_required);
      this.part.RequestResource(resource_name, -co2 * efficiency);

      // set status
      Status = co2 <= double.Epsilon ? Lib.BuildString("No ", waste_name) : ec <= double.Epsilon ? "No Power" : "Running";
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
  public static void BackgroundUpdate(Vessel vessel, ProtoPartModuleSnapshot m, Scrubber scrubber)
  {
    // get data
    bool is_enabled = Lib.Proto.GetBool(m, "is_enabled");
    double ec_rate = scrubber.ec_rate;
    double co2_rate = scrubber.co2_rate;
    double efficiency = Lib.Proto.GetDouble(m, "efficiency");
    double intake_rate = scrubber.intake_rate;
    string resource_name = scrubber.resource_name;
    string waste_name = scrubber.waste_name;

    // if for some reason efficiency wasn't set, default to 50%
    // note: for example, resque vessels scrubbers get background update without prelaunch
    if (efficiency <= double.Epsilon) efficiency = 0.5;

    // get time elapsed from last update
    double elapsed_s = TimeWarp.fixedDeltaTime;

    // if inside breathable atmosphere
    if (Cache.VesselInfo(vessel).breathable)
    {
      // produce oxygen from the intake
      Lib.Resource.Request(vessel, resource_name, -intake_rate * elapsed_s);
    }
    // if outside breathable atmosphere and enabled
    else if (is_enabled)
    {
      // recycle CO2+EC into oxygen
      double co2_required = co2_rate * elapsed_s;
      double co2 = Lib.Resource.Request(vessel, waste_name, co2_required);
      double ec_required = ec_rate * elapsed_s * (co2 / co2_required);
      double ec = Lib.Resource.Request(vessel, "ElectricCharge", ec_required);
      Lib.Resource.Request(vessel, resource_name, -co2 * efficiency);
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