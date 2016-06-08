// ===================================================================================================================
// store entertainment rate
// ===================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public class Entertainment : PartModule
{
  // .cfg
  [KSPField(isPersistant = true)] public string description;
  [KSPField(isPersistant = true)] public double rate = 1.5;

  // editor/r&d info
  public override string GetInfo()
  {
    return Lib.BuildString(description, "\n\n<color=#999999>Comfort: <b>", rate.ToString("F1"), "</b></color>");
  }
}


} // KERBALISM