// ====================================================================================================================
// gravity ring device
// ====================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class RingDevice : Device
{
  public RingDevice(GravityRing ring)
  {
    this.ring = ring;
  }

  public override string info()
  {
    if (!ring.opened) return "<color=red>closed</color>";
    return Lib.BuildString
    (
      "<color=", ring.speed > float.Epsilon ? "green" : "red", ">",
      "speed: ", Lib.HumanReadablePerc(ring.speed),
      "</color>"
    );
  }

  public override void ctrl(double value)
  {
    ring.speed = (float)Lib.Clamp(value, 0.0, 1.0);
    if (!ring.opened) ring.Open();
  }

  GravityRing ring;
}


public sealed class ProtoRingDevice : Device
{
  public ProtoRingDevice(ProtoPartModuleSnapshot ring)
  {
    this.ring = ring;
  }

  public override string info()
  {
    bool opened = Lib.Proto.GetBool(ring, "opened");
    double speed = Lib.Proto.GetFloat(ring, "speed");
    if (!opened) return "<color=red>closed</color>";
    return Lib.BuildString
    (
      "<color=", speed > double.Epsilon ? "green" : "red", ">",
      "speed: ", Lib.HumanReadablePerc(speed),
      "</color>"
    );
  }

  public override void ctrl(double value)
  {
    Lib.Proto.Set(ring, "speed", (float)Lib.Clamp(value, 0.0, 1.0));
    Lib.Proto.Set(ring, "opened", true);
  }

  ProtoPartModuleSnapshot ring;
}


} // KERBALISM