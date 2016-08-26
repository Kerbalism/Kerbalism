// ====================================================================================================================
// converter device
// ====================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class ConverterDevice : Device
{
  public ConverterDevice(ModuleResourceConverter converter)
  {
    this.converter = converter;
  }

  public override string info()
  {
    return converter.AlwaysActive ? "<color=green>always on</color>" : converter.IsActivated ? "<color=green>on</color>" : "<color=red>off</color>";
  }

  public override void ctrl(double value)
  {
    if (converter.AlwaysActive) return;
    if (value > double.Epsilon) converter.StartResourceConverter();
    else converter.StopResourceConverter();
  }

  ModuleResourceConverter converter;
}


public sealed class ProtoConverterDevice : Device
{
  public ProtoConverterDevice(ProtoPartModuleSnapshot converter, ModuleResourceConverter prefab)
  {
    this.converter = converter;
    this.prefab = prefab;
  }

  public override string info()
  {
    if (prefab.AlwaysActive) return "<color=green>always on</color>";
    bool is_on = Lib.Proto.GetBool(converter, "IsActivated");
    return is_on ? "<color=green>on</color>" : "<color=red>off</color>";
  }

  public override void ctrl(double value)
  {
    if (prefab.AlwaysActive) return;
    Lib.Proto.Set(converter, "IsActivated", value > double.Epsilon);
  }

  ProtoPartModuleSnapshot converter;
  ModuleResourceConverter prefab;
}


} // KERBALISM