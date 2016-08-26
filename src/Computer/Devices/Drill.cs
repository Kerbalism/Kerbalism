// ====================================================================================================================
// recycler device
// ====================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class DrillDevice : Device
{
  public DrillDevice(ModuleResourceHarvester drill)
  {
    this.drill = drill;
  }

  public override string info()
  {
    if (drill.AlwaysActive) return "<color=green>always on</color>";
    ModuleAnimationGroup deploy = drill.part.FindModuleImplementing<ModuleAnimationGroup>();
    return deploy == null || deploy.isDeployed
      ? (drill.IsActivated ? "<color=green>on</color>" : "<color=red>off</color>")
      : "<color=red>not deployed</color>";
  }

  public override void ctrl(double value)
  {
    if (drill.AlwaysActive) return;
    ModuleAnimationGroup deploy = drill.part.FindModuleImplementing<ModuleAnimationGroup>();
    if (deploy != null && !deploy.isDeployed) deploy.DeployModule();
    if (value > double.Epsilon) drill.StartResourceConverter();
    else drill.StopResourceConverter();
  }

  ModuleResourceHarvester drill;
}


public sealed class ProtoDrillDevice : Device
{
  public ProtoDrillDevice(ProtoPartModuleSnapshot drill, ModuleResourceHarvester prefab, ProtoPartModuleSnapshot deploy)
  {
    this.drill = drill;
    this.prefab = prefab;
    this.deploy = deploy;
  }

  public override string info()
  {
    if (prefab.AlwaysActive) return "<color=green>always on</color>";
    bool is_deployed = deploy == null || Lib.Proto.GetBool(deploy, "isDeployed");
    bool is_on = Lib.Proto.GetBool(drill, "IsActivated");
    return is_deployed ? (is_on ? "<color=green>on</color>" : "<color=red>off</color>") : "<color=red>not deployed</color>";
  }

  public override void ctrl(double value)
  {
    // note: it is impossible to determine if the drill made contact with the ground, so we have to assume it did
    if (prefab.AlwaysActive) return;
    if (deploy != null) Lib.Proto.Set(deploy, "isDeployed", true);
    Lib.Proto.Set(drill, "IsActivated", value > double.Epsilon);
  }

  ProtoPartModuleSnapshot drill;
  ModuleResourceHarvester prefab;
  ProtoPartModuleSnapshot deploy; //< animation module
}


} // KERBALISM