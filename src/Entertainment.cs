// ===================================================================================================================
// Entertainment module
// influence the Quality Of Life mechanic
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
    return description + "\n\n<color=#999999>Factor: <b>" + rate.ToString("F2") + "</b></color>";
  }
}

	
} // KERBALISM