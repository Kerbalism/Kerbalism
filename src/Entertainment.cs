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
  [KSPField(isPersistant = true)] public double rate;

  // editor/r&d info
  public override string GetInfo()
  {
    return description + (rate > double.Epsilon ? "\n\n<color=#999999>Comfort: <b>" + rate.ToString("F1") + "</b></color>" : "");
  }
}


} // KERBALISM