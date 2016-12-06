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

  public override string name()
  {
    return "converter";
  }

  public override uint part()
  {
    return converter.part.flightID;
  }

  public override string info()
  {
    return converter.AlwaysActive ? "always on" : converter.IsActivated ? "<color=cyan>on</color>" : "<color=red>off</color>";
  }

  public override void ctrl(bool value)
  {
    if (converter.AlwaysActive) return;
    if (value) converter.StartResourceConverter();
    else converter.StopResourceConverter();
  }

  public override void toggle()
  {
    ctrl(!converter.IsActivated);
  }

  ModuleResourceConverter converter;
}


public sealed class ProtoConverterDevice : Device
{
  public ProtoConverterDevice(ProtoPartModuleSnapshot converter, ModuleResourceConverter prefab, uint part_id)
  {
    this.converter = converter;
    this.prefab = prefab;
    this.part_id = part_id;
  }

  public override string name()
  {
    return "converter";
  }

  public override uint part()
  {
    return part_id;
  }

  public override string info()
  {
    if (prefab.AlwaysActive) return "always on";
    bool is_on = Lib.Proto.GetBool(converter, "IsActivated");
    return is_on ? "<color=cyan>on</color>" : "<color=red>off</color>";
  }

  public override void ctrl(bool value)
  {
    if (prefab.AlwaysActive) return;
    Lib.Proto.Set(converter, "IsActivated", value);
  }

  public override void toggle()
  {
    ctrl(!Lib.Proto.GetBool(converter, "IsActivated"));
  }

  ProtoPartModuleSnapshot converter;
  ModuleResourceConverter prefab;
  uint part_id;
}


} // KERBALISM