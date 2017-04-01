using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class PlannerController : PartModule
{
  // config
  [KSPField] public bool toggle = true;                       // true to show the toggle button in editor

  // persistence
  [KSPField(isPersistant = true)] public bool considered;     // true to consider the part modules in planner


  public override void OnStart(StartState state)
  {
    if (Lib.IsEditor())
    {
      Events["Toggle"].active = toggle;
    }
  }

  void Update()
  {
    Events["Toggle"].guiName = Lib.StatusToggle
    (
      "Simulate in planner",
      considered ? "<b><color=#00ff00>yes</color></b>" : "<b><color=#ffff00>no</color></b>"
    );
  }

  [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "_", active = true)]
  public void Toggle()
  {
    considered = !considered;
  }
}


} // KERBALISM

