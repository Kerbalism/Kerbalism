// ====================================================================================================================
// greenhouse device
// ====================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class GreenhouseDevice : Device
{
  public GreenhouseDevice(Greenhouse greenhouse)
  {
    this.greenhouse = greenhouse;
  }

  public override string info()
  {
    return Lib.BuildString
    (
      "<color=", greenhouse.lamps > float.Epsilon ? "green" : "red", ">",
      "lamps: ", Lib.HumanReadablePerc(greenhouse.lamps),
      "</color>"
    );
  }

  public override void ctrl(double value)
  {
    greenhouse.lamps = (float)Lib.Clamp(value, 0.0, 1.0);
    if (!greenhouse.door_opened) greenhouse.OpenDoor();
  }

  Greenhouse greenhouse;
}


public sealed class ProtoGreenhouseDevice : Device
{
  public ProtoGreenhouseDevice(ProtoPartModuleSnapshot greenhouse)
  {
    this.greenhouse = greenhouse;
  }

  public override string info()
  {
    double lamps = Lib.Proto.GetFloat(greenhouse, "lamps");
    return Lib.BuildString
    (
      "<color=", lamps > double.Epsilon ? "green" : "red", ">",
      "lamps: ", Lib.HumanReadablePerc(lamps),
      "</color>"
    );
  }

  public override void ctrl(double value)
  {
    Lib.Proto.Set(greenhouse, "lamps", (float)Lib.Clamp(value, 0.0, 1.0));
    Lib.Proto.Set(greenhouse, "door_opened", true);
  }

  ProtoPartModuleSnapshot greenhouse;
}


} // KERBALISM