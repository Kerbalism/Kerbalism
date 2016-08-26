// ====================================================================================================================
// scrubber device
// ====================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class ScrubberDevice : Device
{
  public ScrubberDevice(Scrubber scrubber)
  {
    this.scrubber = scrubber;
  }

  public override string info()
  {
    return scrubber.is_enabled ? "<color=green>enabled</color>" : "<color=red>disabled</color>";
  }

  public override void ctrl(double value)
  {
    if (value > double.Epsilon) scrubber.ActivateEvent();
    else scrubber.DeactivateEvent();
  }

  Scrubber scrubber;
}


public sealed class ProtoScrubberDevice : Device
{
  public ProtoScrubberDevice(ProtoPartModuleSnapshot scrubber)
  {
    this.scrubber = scrubber;
  }

  public override string info()
  {
    return Lib.Proto.GetBool(scrubber, "is_enabled") ? "<color=green>enabled</color>" : "<color=red>disabled</color>";
  }

  public override void ctrl(double value)
  {
    Lib.Proto.Set(scrubber, "is_enabled", value > double.Epsilon);
  }

  ProtoPartModuleSnapshot scrubber;
}


} // KERBALISM