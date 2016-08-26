// ====================================================================================================================
// light device
// ====================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class LightDevice : Device
{
  public LightDevice(ModuleLight light)
  {
    this.light = light;
  }

  public override string info()
  {
    return light.isOn ? "<color=green>on</color>" : "<color=red>off</color>";
  }

  public override void ctrl(double value)
  {
    if (value > double.Epsilon) light.LightsOn();
    else light.LightsOff();
  }

  ModuleLight light;
}


public sealed class ProtoLightDevice : Device
{
  public ProtoLightDevice(ProtoPartModuleSnapshot light)
  {
    this.light = light;
  }

  public override string info()
  {
    bool is_on = Lib.Proto.GetBool(light, "isOn");
    return is_on ? "<color=green>on</color>" : "<color=red>off</color>";
  }

  public override void ctrl(double value)
  {
    Lib.Proto.Set(light, "isOn", value > double.Epsilon);
  }

  ProtoPartModuleSnapshot light;
}


} // KERBALISM