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

  public override string name()
  {
    return "generator";
  }

  public override uint part()
  {
    return generator.part.flightID;
  }

  public override string info()
  {
    return generator.isAlwaysActive ? "always on" : generator.generatorIsActive ? "<color=cyan>on</color>" : "<color=red>off</color>";
  }

  public override void ctrl(bool value)
  {
    if (generator.isAlwaysActive) return;
    if (value) generator.Activate();
    else generator.Shutdown();
  }

  public override void toggle()
  {
    ctrl(!generator.generatorIsActive);
  }

  ModuleGenerator generator;
}


public sealed class ProtoGeneratorDevice : Device
{
  public ProtoGeneratorDevice(ProtoPartModuleSnapshot generator, ModuleGenerator prefab, uint part_id)
  {
    this.generator = generator;
    this.prefab = prefab;
    this.part_id = part_id;
  }

  public override string name()
  {
    return "generator";
  }

  public override uint part()
  {
    return part_id;
  }

  public override string info()
  {
    if (prefab.isAlwaysActive) return "always on";
    bool is_on = Lib.Proto.GetBool(generator, "generatorIsActive");
    return is_on ? "<color=cyan>on</color>" : "<color=red>off</color>";
  }

  public override void ctrl(bool value)
  {
    if (prefab.isAlwaysActive) return;
    Lib.Proto.Set(generator, "generatorIsActive", value);
  }

  public override void toggle()
  {
    ctrl(!Lib.Proto.GetBool(generator, "generatorIsActive"));
  }

  ProtoPartModuleSnapshot generator;
  ModuleGenerator prefab;
  uint part_id;
}


} // KERBALISM