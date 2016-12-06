using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class ProcessDevice : Device
{
  public ProcessDevice(ProcessController process_ctrl)
  {
    this.process_ctrl = process_ctrl;
  }

  public override string name()
  {
    return process_ctrl.title.ToLower();
  }

  public override uint part()
  {
    return process_ctrl.part.flightID;
  }

  public override string info()
  {
    return process_ctrl.running
      ? "<color=cyan>running</color>"
      : "<color=red>stopped</color>";
  }

  public override void ctrl(bool value)
  {
    if (!process_ctrl.toggle) return;
    process_ctrl.running = value;
  }

  public override void toggle()
  {
    ctrl(!process_ctrl.running);
  }

  ProcessController process_ctrl;
}


public sealed class ProtoProcessDevice : Device
{
  public ProtoProcessDevice(ProtoPartModuleSnapshot process_ctrl, ProcessController prefab, uint part_id)
  {
    this.process_ctrl = process_ctrl;
    this.prefab = prefab;
    this.part_id = part_id;
  }

  public override string name()
  {
    return prefab.title.ToLower();
  }

  public override uint part()
  {
    return part_id;
  }

  public override string info()
  {
    return Lib.Proto.GetBool(process_ctrl, "running")
      ? "<color=cyan>running</color>"
      : "<color=red>stopped</color>";
  }

  public override void ctrl(bool value)
  {
    if (!prefab.toggle) return;
    Lib.Proto.Set(process_ctrl, "running", value);
    ProtoPartSnapshot part_prefab = FlightGlobals.FindProtoPartByID(part_id);
    part_prefab.resources.Find(k => k.resourceName == prefab.resource).flowState = value;
  }

  public override void toggle()
  {
    ctrl(!Lib.Proto.GetBool(process_ctrl, "running"));
  }

  ProtoPartModuleSnapshot process_ctrl;
  ProcessController prefab;
  uint part_id;
}


} // KERBALISM



