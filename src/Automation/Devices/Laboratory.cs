using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class LaboratoryDevice : Device
{
  public LaboratoryDevice(Laboratory lab)
  {
    this.lab = lab;
  }

  public override string name()
  {
    return "laboratory";
  }

  public override uint part()
  {
    return lab.part.flightID;
  }

  public override string info()
  {
    return lab.running ? "<color=cyan>active</color>" : "<color=red>disabled</color>";
  }

  public override void ctrl(bool value)
  {
    if (lab.running != value) lab.Toggle();
  }

  public override void toggle()
  {
    ctrl(!lab.running);
  }

  Laboratory lab;
}


public sealed class ProtoLaboratoryDevice : Device
{
  public ProtoLaboratoryDevice(ProtoPartModuleSnapshot lab, uint part_id)
  {
    this.lab = lab;
    this.part_id = part_id;
  }

  public override string name()
  {
    return "laboratory";
  }

  public override uint part()
  {
    return part_id;
  }

  public override string info()
  {
    return Lib.Proto.GetBool(lab, "running") ? "<color=cyan>active</color>" : "<color=red>disabled</color>";
  }

  public override void ctrl(bool value)
  {
    Lib.Proto.Set(lab, "running", value);
  }

  public override void toggle()
  {
    ctrl(!Lib.Proto.GetBool(lab, "running"));
  }

  ProtoPartModuleSnapshot lab;
  uint part_id;
}


} // KERBALISM

