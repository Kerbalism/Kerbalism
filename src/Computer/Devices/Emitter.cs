// ====================================================================================================================
// tweakable emitter device
// ====================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class EmitterDevice : Device
{
  public EmitterDevice(Emitter emitter)
  {
    this.emitter = emitter;
  }

  public override string info()
  {
    return Lib.BuildString
    (
      "<color=", emitter.intensity > float.Epsilon ? "green" : "red", ">",
      "intensity: ", Lib.HumanReadablePerc(emitter.intensity),
      "</color>"
    );
  }

  public override void ctrl(double value)
  {
    emitter.intensity = (float)Lib.Clamp(value, 0.0, 1.0);
  }

  Emitter emitter;
}


public sealed class ProtoEmitterDevice : Device
{
  public ProtoEmitterDevice(ProtoPartModuleSnapshot emitter)
  {
    this.emitter = emitter;
  }

  public override string info()
  {
    float intensity = Lib.Proto.GetFloat(emitter, "intensity");
    return Lib.BuildString
    (
      "<color=", intensity > float.Epsilon ? "green" : "red", ">",
      "intensity: ", Lib.HumanReadablePerc(intensity),
      "</color>"
    );
  }

  public override void ctrl(double value)
  {
    Lib.Proto.Set(emitter, "intensity", (float)Lib.Clamp(value, 0.0, 1.0));
  }

  ProtoPartModuleSnapshot emitter;
}


} // KERBALISM