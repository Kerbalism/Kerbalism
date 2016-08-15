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
  [KSPField] public string description;

  // persistence
  [KSPField(isPersistant = true)] public double rate = 1.5;

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