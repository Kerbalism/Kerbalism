// ====================================================================================================================
// generator device
// ====================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class GeneratorDevice : Device
{
  public GeneratorDevice(ModuleGenerator generator)
  {
    this.generator = generator;
  }

  public override string info()
  {
    return generator.isAlwaysActive ? "<color=green>always on</color>" : generator.generatorIsActive ? "<color=green>on</color>" : "<color=red>off</color>";
  }

  public override void ctrl(double value)
  {
    if (generator.isAlwaysActive) return;
    if (value > double.Epsilon) generator.Activate();
    else generator.Shutdown();
  }

  ModuleGenerator generator;
}


public sealed class ProtoGeneratorDevice : Device
{
  public ProtoGeneratorDevice(ProtoPartModuleSnapshot generator, ModuleGenerator prefab)
  {
    this.generator = generator;
    this.prefab = prefab;
  }

  public override string info()
  {
    if (prefab.isAlwaysActive) return "<color=green>always on</color>";
    bool is_on = Lib.Proto.GetBool(generator, "generatorIsActive");
    return is_on ? "<color=green>on</color>" : "<color=red>off</color>";
  }

  public override void ctrl(double value)
  {
    if (prefab.isAlwaysActive) return;
    Lib.Proto.Set(generator, "generatorIsActive", value > double.Epsilon);
  }

  ProtoPartModuleSnapshot generator;
  ModuleGenerator prefab;
}


} // KERBALISM