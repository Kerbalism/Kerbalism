// ====================================================================================================================
// recycler device
// ====================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class RecyclerDevice : Device
{
  public RecyclerDevice(Recycler recycler)
  {
    this.recycler = recycler;
  }

  public override string info()
  {
    return recycler.is_enabled ? "<color=green>enabled</color>" : "<color=red>disabled</color>";
  }

  public override void ctrl(double value)
  {
    if (value > double.Epsilon) recycler.ActivateEvent();
    else recycler.DeactivateEvent();
  }

  Recycler recycler;
}


public sealed class ProtoRecyclerDevice : Device
{
  public ProtoRecyclerDevice(ProtoPartModuleSnapshot recycler)
  {
    this.recycler = recycler;
  }

  public override string info()
  {
    return Lib.Proto.GetBool(recycler, "is_enabled") ? "<color=green>enabled</color>" : "<color=red>disabled</color>";
  }

  public override void ctrl(double value)
  {
    Lib.Proto.Set(recycler, "is_enabled", value > double.Epsilon);
  }

  ProtoPartModuleSnapshot recycler;
}


} // KERBALISM