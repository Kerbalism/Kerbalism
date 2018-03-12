using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class AntennaDevice : Device
{
  public AntennaDevice(Antenna antenna)
  {
    this.antenna = antenna;
    this.animator = antenna.part.FindModuleImplementing<ModuleAnimationGroup>();
  }

  public override string name()
  {
    return "antenna";
  }

  public override uint part()
  {
    return antenna.part.flightID;
  }

  public override string info()
  {
    return animator == null
      ? "fixed"
      : antenna.extended
      ? "<color=cyan>deployed</color>"
      : "<color=red>retracted</color>";
  }

  public override void ctrl(bool value)
  {
    if (animator != null)
    {
      if (!antenna.extended && value) animator.DeployModule();
      else if (antenna.extended && !value) animator.RetractModule();
    }
  }

  public override void toggle()
  {
    if (animator != null)
    {
      ctrl(!antenna.extended);
    }
  }

  Antenna antenna;
  ModuleAnimationGroup animator;
}


public sealed class ProtoAntennaDevice : Device
{
  public ProtoAntennaDevice(ProtoPartModuleSnapshot antenna, uint part_id)
  {
    this.antenna = antenna;
    this.animator = FlightGlobals.FindProtoPartByID(part_id).FindModule("ModuleAnimationGroup");
    this.part_id = part_id;

  }

  public override string name()
  {
    return "antenna";
  }

  public override uint part()
  {
    return part_id;
  }

  public override string info()
  {
    return animator == null
      ? "fixed"
      : Lib.Proto.GetBool(antenna, "extended")
      ? "<color=cyan>deployed</color>"
      : "<color=red>retracted</color>";
  }

  public override void ctrl(bool value)
  {
    if (animator != null)
    {
      Lib.Proto.Set(antenna, "extended", value);
      Lib.Proto.Set(animator, "isDeployed", value);
    }
  }

  public override void toggle()
  {
    if (animator != null)
    {
      ctrl(!Lib.Proto.GetBool(antenna, "extended"));
    }
  }

  ProtoPartModuleSnapshot antenna;
  ProtoPartModuleSnapshot animator;
  uint part_id;
}


} // KERBALISM





