// ===================================================================================================================
// store entertainment rate
// ===================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public class Entertainment : PartModule
{
  // config
  [KSPField] public string description;                     // verbose description shown in part tooltip

  // persistence
  [KSPField(isPersistant = true)] public double rate = 1.5; // entertainment rate
  [KSPField] public bool vessel_wide;                       // if true, the entertainment is provided to the whole vessel irregardless of internal space

  // editor/r&d info
  public override string GetInfo()
  {
    return Lib.BuildString
    (
      Lib.Specifics(description),
      Lib.Specifics(true, "Comfort", rate.ToString("F1"))
    );
  }
}


} // KERBALISM