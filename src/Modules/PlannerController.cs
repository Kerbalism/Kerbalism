// ===================================================================================================================
// WIP
// ===================================================================================================================


using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;


namespace KERBALISM {


public sealed class PlannerController : PartModule
{
  // config
  [KSPField] public bool toggle;

  // persistence
  [KSPField(isPersistant = true)] public bool considered;


  public override void OnStart(StartState state)
  {
    if (state == StartState.Editor)
    {
      Events["Toggle"].active = toggle;
    }
  }


  void Update()
  {
    const string yes = "<b><color=#00ff00>yes</color></b>";
    const string no = "<b><color=#ff0000>no</color></b>";
    Events["Toggle"].guiName = Lib.BuildString("Consider in planner: ", considered ? yes : no);
  }


  [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "_", active = true)]
  public void Toggle()
  {
    considered = !considered;
  }
}


} // KERBALISM

