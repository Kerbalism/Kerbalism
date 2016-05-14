// ===================================================================================================================
// Recycler module
// recycle a resource into another
// ===================================================================================================================



using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public class Recycler : PartModule
{
  [KSPField(isPersistant = true)] public string resource_name;              // name of resource recycled
  [KSPField(isPersistant = true)] public string waste_name;                 // name of waste used
  [KSPField(isPersistant = true)] public double ec_rate;                    // EC consumption rate per-second
  [KSPField(isPersistant = true)] public double waste_rate;                 // waste consumption rate per-second
  [KSPField(isPersistant = true)] public double waste_ratio = 1.0;          // proportion of waste recycled into resource
  [KSPField(isPersistant = true)] public string display_name = "Recycler";  // short identifier for the recycler in the RMB ui
  [KSPField(isPersistant = true)] public string filter_name = "";           // name of a special filter resource, if any
  [KSPField(isPersistant = true)] public double filter_rate;                // filter consumption rate per-second

  // persistence
  // note: also configurable per-part
  [KSPField(isPersistant = true)] public bool is_enabled = true;            // if the recycler is enabled

  // rmb status
  [KSPField(guiActive = true, guiName = "Recycler")] public string Status;  // description of current state

  // rmb status in editor
  [KSPField(guiActiveEditor = true, guiName = "Recycler")] public string EditorStatus; // description of current state (in the editor)


  // rmb enable
  [KSPEvent(guiActive = true, guiName = "Enable Recycler", active = false)]
  public void ActivateEvent()
  {
    Events["ActivateEvent"].active = false;
    Events["DeactivateEvent"].active = true;
    is_enabled = true;
  }


  // rmb disable
  [KSPEvent(guiActive = true, guiName = "Disable Recycler", active = false)]
  public void DeactivateEvent()
  {
    Events["ActivateEvent"].active = true;
    Events["DeactivateEvent"].active = false;
    is_enabled = false;
  }


  // editor toggle
  [KSPEvent(guiActiveEditor = true, guiName = "Toggle Recycler", active = true)]
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

    // set display name
    Fields["Status"].guiName = display_name;
    Fields["EditorStatus"].guiName = display_name;
    Events["ActivateEvent"].guiName = "Enable " + display_name;
    Events["DeactivateEvent"].guiName = "Disable " + display_name;
    Events["ToggleInEditorEvent"].guiName = "Toggle " + display_name;
  }


  // editor/r&d info
  public override string GetInfo()
  {
    return "Recycle some of the " + resource_name + ".\n\n"
         + "<color=#99FF00>Requires:</color>\n"
         + " - ElectricCharge: " + Lib.HumanReadableRate(ec_rate) + "\n"
         + " - " + resource_name + ": " + Lib.HumanReadableRate(waste_rate * waste_ratio) + "\n"
         + " - " + waste_name + ": " + Lib.HumanReadableRate(waste_rate)
         + (filter_name.Length > 0 && filter_rate > 0.0
         ? "\n - " + filter_name + ": " + Lib.HumanReadableRate(filter_rate)
         : "");
  }


  // implement recycler mechanics
  public void FixedUpdate()
  {
    // do nothing in the editor
    if (HighLogic.LoadedSceneIsEditor) return;

    if (is_enabled)
    {
      // calculate worst required resource percentual
      double worst_input = 1.0;
      double waste_required = waste_rate * TimeWarp.fixedDeltaTime;
      double waste_amount = Lib.GetResourceAmount(vessel, waste_name);
      worst_input = Math.Min(worst_input, waste_amount / waste_required);
      double ec_required = ec_rate * TimeWarp.fixedDeltaTime;
      double ec_amount = Lib.GetResourceAmount(vessel, "ElectricCharge");
      worst_input = Math.Min(worst_input, ec_amount / ec_required);
      double filter_required = filter_rate * TimeWarp.fixedDeltaTime;
      if (filter_name.Length > 0 && filter_rate > 0.0)
      {
        double filter_amount = Lib.GetResourceAmount(vessel, filter_name);
        worst_input = Math.Min(worst_input, filter_amount / filter_required);
      }

      // recycle EC+waste+filter into resource
      this.part.RequestResource(waste_name, waste_required * worst_input);
      this.part.RequestResource("ElectricCharge", ec_required * worst_input);
      this.part.RequestResource(filter_name, filter_required * worst_input);
      this.part.RequestResource(resource_name, -waste_required * worst_input * waste_ratio);

      // set status
      Status = waste_amount <= double.Epsilon ? "No " + waste_name : ec_amount <= double.Epsilon ? "No Power" : "Running";
    }
    else
    {
      Status = "Off";
    }
  }


  // implement recycler mechanics for unloaded vessels
  public static void BackgroundUpdate(Vessel vessel, uint flight_id)
  {
    // get data
    ProtoPartModuleSnapshot m = Lib.GetProtoModule(vessel, flight_id, "Recycler");
    bool is_enabled = Lib.GetProtoValue<bool>(m, "is_enabled");
    string resource_name = Lib.GetProtoValue<string>(m, "resource_name");
    string waste_name = Lib.GetProtoValue<string>(m, "waste_name");
    double ec_rate = Lib.GetProtoValue<double>(m, "ec_rate");
    double waste_rate = Lib.GetProtoValue<double>(m, "waste_rate");
    double waste_ratio = Lib.GetProtoValue<double>(m, "waste_ratio");
    string filter_name = Lib.GetProtoValue<string>(m, "filter_name");
    double filter_rate = Lib.GetProtoValue<double>(m, "filter_rate");

    if (is_enabled)
    {
      // calculate worst required resource percentual
      double worst_input = 1.0;
      double waste_required = waste_rate * TimeWarp.fixedDeltaTime;
      double waste_amount = Lib.GetResourceAmount(vessel, waste_name);
      worst_input = Math.Min(worst_input, waste_amount / waste_required);
      double ec_required = ec_rate * TimeWarp.fixedDeltaTime;
      double ec_amount = Lib.GetResourceAmount(vessel, "ElectricCharge");
      worst_input = Math.Min(worst_input, ec_amount / ec_required);
      double filter_required = filter_rate * TimeWarp.fixedDeltaTime;
      if (filter_name.Length > 0 && filter_rate > 0.0)
      {
        double filter_amount = Lib.GetResourceAmount(vessel, filter_name);
        worst_input = Math.Min(worst_input, filter_amount / filter_required);
      }

      // recycle EC+waste+filter into resource
      Lib.RequestResource(vessel, waste_name, waste_required * worst_input);
      Lib.RequestResource(vessel, "ElectricCharge", ec_required * worst_input);
      Lib.RequestResource(vessel, filter_name, filter_required * worst_input);
      Lib.RequestResource(vessel, resource_name, -waste_required * worst_input * waste_ratio);
    }
  }


  // return read-only list of recyclers in a vessel
  public static List<Recycler> GetRecyclers(Vessel v, string resource_name="")
  {
    if (v.loaded)
    {
      var ret = v.FindPartModulesImplementing<Recycler>();
      if (resource_name.Length > 0) ret = ret.FindAll(k => k.resource_name == resource_name);
      return ret == null ? new List<Recycler>() : ret;
    }
    else
    {
      List<Recycler> ret = new List<Recycler>();
      foreach(ProtoPartSnapshot part in v.protoVessel.protoPartSnapshots)
      {
        foreach(ProtoPartModuleSnapshot module in part.modules)
        {
          if (module.moduleName == "Recycler")
          {
            Recycler recycler = new Recycler();
            recycler.is_enabled = Lib.GetProtoValue<bool>(module, "is_enabled");
            recycler.resource_name = Lib.GetProtoValue<string>(module, "resource_name");
            recycler.waste_name = Lib.GetProtoValue<string>(module, "waste_name");
            recycler.ec_rate = Lib.GetProtoValue<double>(module, "ec_rate");
            recycler.waste_rate = Lib.GetProtoValue<double>(module, "waste_rate");
            recycler.waste_ratio = Lib.GetProtoValue<double>(module, "waste_ratio");
            recycler.display_name = Lib.GetProtoValue<string>(module, "display_name");
            recycler.filter_name = Lib.GetProtoValue<string>(module, "filter_name");
            recycler.filter_rate = Lib.GetProtoValue<double>(module, "filter_rate");

            if (resource_name.Length == 0 || recycler.resource_name == resource_name) ret.Add(recycler);
          }
        }
      }
      return ret;
    }
  }
}


} // KERBALISM