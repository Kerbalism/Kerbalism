// ====================================================================================================================
// crew device
// ====================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class CrewDevice : Device
{
  public CrewDevice(ProtoCrewMember crew)
  {
    this.crew = crew;
  }

  public override string info()
  {
    kerbal_data kd = DB.KerbalData(crew.name);
    int worst = 0;
    foreach(Rule r in Kerbalism.rules)
    {
      double problem = kd.kmon[r.name].problem;
      if (problem > r.danger_threshold) worst = Math.Max(worst, 2);
      else if (problem > r.warning_threshold) worst = Math.Max(worst, 1);
    }
    switch(worst)
    {
      case 2: return "<color=red>danger</color>";
      case 1: return "<color=yellow>warning</color>";
      default: return "<color=green>healthy</color>";
    }
  }

  public override void ctrl(double value)
  {
    // do nothing
  }

  ProtoCrewMember crew;
}


} // KERBALISM