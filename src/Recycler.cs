// ===================================================================================================================
// recycle a resource into another
// ===================================================================================================================



using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class Recycler : PartModule
{
  [KSPField] public string resource_name;              // name of resource recycled
  [KSPField] public string waste_name;                 // name of waste used
  [KSPField] public double ec_rate;                    // EC consumption rate per-second
  [KSPField] public double waste_rate;                 // waste consumption rate per-second
  [KSPField] public double waste_ratio = 1.0;          // proportion of waste recycled into resource
  [KSPField] public string display_name = "Recycler";  // short identifier for the recycler in the RMB ui
  [KSPField] public string filter_name = "";           // name of a special filter resource, if any
  [KSPField] public double filter_rate;                // filter consumption rate per-second
  [KSPField] public bool   use_efficiency;             // true to influence the recycler by efficiency

  // persistence
  // note: also configurable per-part
  [KSPField(isPersistant = true)] public bool is_enabled = true;  // if the recycler is enabled
  [KSPField(isPersistant = true)] public double efficiency = 1.0; // waste->resource conversion rate

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
    Events["ActivateEvent"].guiName = Lib.BuildString("Enable ", display_name);
    Events["DeactivateEvent"].guiName = Lib.BuildString("Disable ", display_name);
    Events["ToggleInEditorEvent"].guiName = Lib.BuildString("Toggle ", display_name);
  }


  // editor/r&d info
  public override string GetInfo()
  {
    string filter_str = filter_name.Length > 0 && filter_rate > 0.0
      ? Lib.BuildString("\n - ", filter_name, ": ", Lib.HumanReadableRate(filter_rate))
      : "";

    return Lib.BuildString
    (
      "Recycle some of the ", resource_name, ".\n\n",
      "<color=#99FF00>Requires:</color>\n",
       " - ElectricCharge: ", Lib.HumanReadableRate(ec_rate), "\n",
       " - ", resource_name, ": ", Lib.HumanReadableRate(waste_rate * waste_ratio), "\n",
       " - ", waste_name, ": ", Lib.HumanReadableRate(waste_rate),
       filter_str
    );
  }


  // implement recycler mechanics
  public void FixedUpdate()
  {
    // do nothing in the editor
    if (HighLogic.LoadedSceneIsEditor) return;

    if (use_efficiency)
    {
      // deduce quality from technological level if necessary
      // note: done at prelaunch to avoid problems with start()/load() and the tech tree being not consistent
      if (vessel.situation == Vessel.Situations.PRELAUNCH) efficiency = Scrubber.DeduceEfficiency();

      // if for some reason efficiency wasn't set, default to 50%
      // note: for example, resque vessels never get to prelaunch
      if (efficiency <= double.Epsilon) efficiency = 0.5;
    }

    if (is_enabled)
    {
      // get resource cache
      vessel_resources resources = ResourceCache.Get(vessel);

      // recycle EC+waste+filter into resource
      resource_recipe recipe = new resource_recipe(resource_recipe.scrubber_priority);
      recipe.Input(waste_name, waste_rate * Kerbalism.elapsed_s);
      recipe.Input("ElectricCharge", ec_rate * Kerbalism.elapsed_s);
      if (filter_name.Length > 0 && filter_rate > double.Epsilon)
      {
        recipe.Input(filter_name, filter_rate * Kerbalism.elapsed_s);
      }
      recipe.Output(resource_name, waste_rate * waste_ratio * efficiency * Kerbalism.elapsed_s);
      resources.Transform(recipe);

      // set status
      Status = "Running";
    }
    else
    {
      Status = "Off";
    }

    // add efficiency to status
    if (use_efficiency) Status += Lib.BuildString(" (Efficiency: ", (efficiency * 100.0).ToString("F0"), "%)");
  }


  // implement recycler mechanics for unloaded vessels
  public static void BackgroundUpdate(Vessel vessel, ProtoPartModuleSnapshot m, Recycler recycler, vessel_resources resources, double elapsed_s)
  {
    if (Lib.Proto.GetBool(m, "is_enabled"))
    {
      // recycle EC+waste+filter into resource
      resource_recipe recipe = new resource_recipe(resource_recipe.scrubber_priority);
      recipe.Input(recycler.waste_name, recycler.waste_rate * elapsed_s);
      recipe.Input("ElectricCharge", recycler.ec_rate * elapsed_s);
      if (recycler.filter_name.Length > 0 && recycler.filter_rate > double.Epsilon)
      {
        recipe.Input(recycler.filter_name, recycler.filter_rate * elapsed_s);
      }
      recipe.Output(recycler.resource_name, recycler.waste_rate * recycler.waste_ratio * Lib.Proto.GetDouble(m, "efficiency", 1.0) * elapsed_s);
      resources.Transform(recipe);
    }
  }


  // return partial data about scrubbers in a vessel
  public class partial_data { public bool is_enabled; }
  public static List<partial_data> PartialData(Vessel v)
  {
    List<partial_data> ret = new List<partial_data>();
    if (v.loaded)
    {
      foreach(var recycler in v.FindPartModulesImplementing<Recycler>())
      {
        var data = new partial_data();
        data.is_enabled = recycler.is_enabled;
        ret.Add(data);
      }
    }
    else
    {
      foreach(ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
      {
        foreach(ProtoPartModuleSnapshot m in p.modules)
        {
          if (m.moduleName == "Recycler")
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