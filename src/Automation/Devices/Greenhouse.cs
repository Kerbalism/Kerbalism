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

  public override string name()
  {
    return "greenhouse";
  }

  public override uint part()
  {
    return greenhouse.part.flightID;
  }

  public override string info()
  {
    return greenhouse.active ? "<color=cyan>enabled</color>" : "<color=red>disabled</color>";
  }

  public override void ctrl(bool value)
  {
    if (greenhouse.active != value) greenhouse.Toggle();
  }

  public override void toggle()
  {
    ctrl(!greenhouse.active);
  }

  Greenhouse greenhouse;
}


public sealed class ProtoGreenhouseDevice : Device
{
  public ProtoGreenhouseDevice(ProtoPartModuleSnapshot greenhouse, uint part_id)
  {
    this.greenhouse = greenhouse;
    this.part_id = part_id;
  }

  public override string name()
  {
    return "greenhouse";
  }

  public override uint part()
  {
    return part_id;
  }

  public override string info()
  {
    bool active = Lib.Proto.GetBool(greenhouse, "active");
    return active ? "<color=cyan>enabled</color>" : "<color=red>disabled</color>";
  }

  public override void ctrl(bool value)
  {
    Lib.Proto.Set(greenhouse, "active", value);
  }

  public override void toggle()
  {
    ctrl(!Lib.Proto.GetBool(greenhouse, "active"));
  }

  ProtoPartModuleSnapshot greenhouse;
  uint part_id;
}


} // KERBALISM