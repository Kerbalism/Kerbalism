using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;


namespace KERBALISM {


public sealed class ProcessController : PartModule, IModuleInfo, IAnimatedModule, ISpecifics, IConfigurable
{
  // config
  [KSPField] public string resource = string.Empty; // pseudo-resource to control
  [KSPField] public string title = string.Empty;    // name to show on ui
  [KSPField] public string desc = string.Empty;     // description to show on tooltip
  [KSPField] public double capacity = 1.0;          // amount of associated pseudo-resource
  [KSPField] public bool   toggle = true;           // show the enable/disable toggle

  // persistence/config
  // note: the running state doesn't need to be serialized, as it can be deduced from resource flow
  // but we find useful to set running to true in the cfg node for some processes, and not others
  [KSPField(isPersistant = true)] public bool running;


  public void Start()
  {
    // don't break tutorial scenarios
    if (Lib.DisableScenario(this)) return;

    // configure on start
    Configure(true);

    // set action group ui
    Actions["Action"].guiName = Lib.BuildString("Start/Stop ", title);

    // hide toggle if specified
    Events["Toggle"].active = toggle;
    Actions["Action"].active = toggle;

    // deal with non-toggable processes
    if (!toggle) running = true;
  }

  public void Configure(bool enable)
  {
    if (enable)
    {
      // if never set
      // - this is the case in the editor, the first time, or in flight
      //   in the case the module was added post-launch, or EVA kerbals
      if (!part.Resources.Contains(resource))
      {
        // add the resource
        // - always add the specified amount, even in flight
        Lib.AddResource(part, resource, capacity, capacity);
      }
    }
    else
    {
      Lib.RemoveResource(part, resource, 0.0, capacity);
    }
  }


  public void Update()
  {
    // update flow mode of resource
    // note: this has to be done constantly to prevent the user from changing it
    Lib.SetResourceFlow(part, resource, running);

    // update rmb ui
    Events["Toggle"].guiName = Lib.StatusToggle(title, running ? "running" : "stopped");
  }


  [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = true)]
  public void Toggle()
  {
    // switch status
    running = !running;
  }


  // action groups
  [KSPAction("_")] public void Action(KSPActionParam param) { Toggle(); }


  // part tooltip
  public override string GetInfo()
  {
    return Specs().info(desc);
  }

  // specifics support
  public Specifics Specs()
  {
    Specifics specs = new Specifics();
    Process process = Profile.processes.Find(k => k.modifiers.Contains(resource));
    if (process != null)
    {
      foreach(var pair in process.inputs)
      {
        if (!process.modifiers.Contains(pair.Key))
          specs.add(pair.Key, Lib.BuildString("<color=#ff0000>", Lib.HumanReadableRate(pair.Value * capacity), "</color>"));
        else
          specs.add("Half-life", Lib.HumanReadableDuration(0.5 / pair.Value));
      }
      foreach(var pair in process.outputs)
      {
        specs.add(pair.Key, Lib.BuildString("<color=#00ff00>", Lib.HumanReadableRate(pair.Value * capacity), "</color>"));
      }
    }
    return specs;
  }

  // module info support
  public string GetModuleTitle() { return title; }
  public string GetPrimaryField() { return string.Empty; }
  public Callback<Rect> GetDrawModulePanelCallback() { return null; }


  // animation group support
  public void EnableModule()      { }
  public void DisableModule()     { }
  public bool ModuleIsActive()    { return running; }
  public bool IsSituationValid()  { return true; }
}


} // KERBALISM

