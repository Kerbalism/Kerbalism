// ====================================================================================================================
// network device
// ====================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class NetworkDevice : Device
{
  public NetworkDevice(Vessel vessel)
  {
    this.vessel = vessel;
  }

  public override string info()
  {
    return "<color=green>online</color>";
  }

  public override void ctrl(double value)
  {
    // do nothing
  }

  public Guid id()
  {
    return vessel.id;
  }

  Vessel vessel;
}


} // KERBALISM