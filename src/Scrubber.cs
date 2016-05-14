// ===================================================================================================================
// Scrubber module
// ===================================================================================================================



using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public class Scrubber : PartModule
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
  [KSPField(isPersistant = true)] public double ec_rate;                    // EC consumption rate per-second
  [KSPField(isPersistant = true)] public double co2_rate;                   // waste consumption rate per-second
  [KSPField(isPersistant = true)] public double efficiency = 0.0;           // waste->resource conversion rate
  [KSPField(isPersistant = true)] public double intake_rate = 1.0;         // Oxygen production rate inside breathable atmosphere
  [KSPField(isPersistant = true)] public string resource_name = "Oxygen";   // name of resource recycled
  [KSPField(isPersistant = true)] public string waste_name = "CO2";         // name of resource recycled

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
    return "Reclaim some of the " + resource_name + ".\n\n"
         + "<color=#99FF00>Requires:</color>\n"
         + " - ElectricCharge: " + Lib.HumanReadableRate(ec_rate);
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
      Status = co2 <= double.Epsilon ? "No " + waste_name : ec <= double.Epsilon ? "No Power" : "Running";
    }
    // if outside breathable atmosphere and disabled
    else
    {
      // set status
      Status = "Off";
    }

    // add efficiency to status
    Status += " (Efficiency: " + (efficiency * 100.0).ToString("F0") + "%)";
  }


  // implement scrubber mechanics for unloaded vessels
  public static void BackgroundUpdate(Vessel vessel, uint flight_id)
  {
    // get data
    ProtoPartModuleSnapshot m = Lib.GetProtoModule(vessel, flight_id, "Scrubber");
    bool is_enabled = Lib.GetProtoValue<bool>(m, "is_enabled");
    double ec_rate = Lib.GetProtoValue<double>(m, "ec_rate");
    double co2_rate = Lib.GetProtoValue<double>(m, "co2_rate");
    double efficiency = Lib.GetProtoValue<double>(m, "efficiency");
    double intake_rate = Lib.GetProtoValue(m, "intake_rate", 1.0); //< support versions before 0.9.9.5
    string resource_name = Lib.GetProtoValue(m, "resource_name", "Oxygen"); //< support versions before 0.9.9.5
    string waste_name = Lib.GetProtoValue(m, "waste_name", "CO2"); //< support versions before 0.9.9.5

    // if for some reason efficiency wasn't set, default to 50%
    // note: for example, resque vessels scrubbers get background update without prelaunch
    if (efficiency <= double.Epsilon) efficiency = 0.5;

    // get time elapsed from last update
    double elapsed_s = TimeWarp.fixedDeltaTime;

    // if inside breathable atmosphere
    if (Cache.VesselInfo(vessel).breathable)
    {
      // produce oxygen from the intake
      Lib.RequestResource(vessel, resource_name, -intake_rate * elapsed_s);
    }
    // if outside breathable atmosphere and enabled
    else if (is_enabled)
    {
      // recycle CO2+EC into oxygen
      double co2_required = co2_rate * elapsed_s;
      double co2 = Lib.RequestResource(vessel, waste_name, co2_required);
      double ec_required = ec_rate * elapsed_s * (co2 / co2_required);
      double ec = Lib.RequestResource(vessel, "ElectricCharge", ec_required);
      Lib.RequestResource(vessel, resource_name, -co2 * efficiency);
    }
  }


  // deduce efficiency from technological level
  public static double DeduceEfficiency()
  {
    double[] value = {0.5, 0.6, 0.7, 0.8, 0.9};
    return value[Lib.CountTechs(scrubber_efficiency.techs)];
  }


  // return read-only list of scrubbers in a vessel
  public static List<Scrubber> GetScrubbers(Vessel v, string resource_name="")
  {
    if (v.loaded)
    {
      var ret = v.FindPartModulesImplementing<Scrubber>();
      if (resource_name.Length > 0) ret = ret.FindAll(k => k.resource_name == resource_name);
      return ret == null ? new List<Scrubber>() : ret;
    }
    else
    {
      List<Scrubber> ret = new List<Scrubber>();
      foreach(ProtoPartSnapshot part in v.protoVessel.protoPartSnapshots)
      {
        foreach(ProtoPartModuleSnapshot module in part.modules)
        {
          if (module.moduleName == "Scrubber")
          {
            Scrubber scrubber = new Scrubber();
            scrubber.is_enabled = Lib.GetProtoValue<bool>(module, "is_enabled");
            scrubber.ec_rate = Lib.GetProtoValue<double>(module, "ec_rate");
            scrubber.co2_rate = Lib.GetProtoValue<double>(module, "co2_rate");
            scrubber.efficiency = Lib.GetProtoValue<double>(module, "efficiency");
            scrubber.intake_rate = Lib.GetProtoValue(module, "intake_rate", 1.0); //< support versions before 0.9.9.5
            scrubber.resource_name = Lib.GetProtoValue(module, "resource_name", "Oxygen"); //< support versions before 0.9.9.5
            scrubber.waste_name = Lib.GetProtoValue(module, "waste_name", "CO2"); //< support versions before 0.9.9.5
            if (resource_name.Length == 0 || scrubber.resource_name == resource_name) ret.Add(scrubber);
          }
        }
      }
      return ret;
    }
  }
}


} // KERBALISM