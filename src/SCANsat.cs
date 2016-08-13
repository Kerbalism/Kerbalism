// ===================================================================================================================
// functions to interact with SCANsat
// ===================================================================================================================


using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;



namespace KERBALISM {


public static class SCANsat
{
  // store initialized flag, for lazy initialization
  static bool initialized;

  // true to get ec consumption for versions before 16.4
  static bool legacy_ec;

  // reflection type of SCANUtils static class in SCANsat assembly, if present
  static Type SCANUtils;


  // obtain the SCANUtils class from SCANsat assembly if it is loaded, only called once
  static void lazy_init()
  {
    // search for compatible SCANsat assembly
    if (!initialized)
    {
      foreach(var a in AssemblyLoader.loadedAssemblies)
      {
        var version = a.assembly.GetName().Version;
        if (a.name == "SCANsat" && version.Major == 1 && version.Minor >= 6)
        {
          SCANUtils = a.assembly.GetType("SCANsat.SCANUtil");
          legacy_ec = version.Minor == 6 && version.Revision < 4;
          break;
        }
      }
      initialized = true;
    }
  }

  // interrupt scanning of a SCANsat module
  // - v: vessel that own the module
  // - m: protomodule of a SCANsat or a resource scanner
  // - p: prefab of the part owning the module
  public static bool stopScanner(Vessel v, ProtoPartModuleSnapshot m, Part part_prefab)
  {
    lazy_init();
    if (SCANUtils != null)
    {
      return (bool)SCANUtils.GetMethod("unregisterSensorExternal").Invoke(null, new System.Object[]{v, m, part_prefab});
    }
    return false;
  }


  // resume scanning of a SCANsat module
  // - v: vessel that own the module
  // - m: protomodule of a SCANsat or a resource scanner
  // - p: prefab of the part owning the module
  public static bool resumeScanner(Vessel v, ProtoPartModuleSnapshot m, Part part_prefab)
  {
    lazy_init();
    if (SCANUtils != null)
    {
      return (bool)SCANUtils.GetMethod("registerSensorExternal").Invoke(null, new System.Object[]{v, m, part_prefab});
    }
    return false;
  }


  // return true if a SCANsat scanner is deployed in the editors, or is not deployable at all
  // - p: part owning the SCANsat module
  // - m: SCANsat module
  public static bool isDeployed(Part p, PartModule m)
  {
    if (m.moduleName == "SCANsat")
    {
      // find out if this is deployable
      bool deployable = m.Events["editorExtend"].active || m.Events["editorRetract"].active;

      // find out if it is deployed in the editor
      return !deployable || m.Events["editorRetract"].active;
    }
    else if (m.moduleName == "ModuleSCANresourceScanner")
    {
      // assume deployed
      bool deployed = true;

      // resource scanners use SQUAD parts, that are animated using another module
      foreach(PartModule animator in p.Modules)
      {
        if (animator.moduleName == "ModuleAnimationGroup")
        {
          // find out if this is deployable
          bool deployable = animator.Events["DeployModule"].active || animator.Events["RetractModule"].active;

          // find out if it is deployed in the editor
          deployed = !deployable || animator.Events["RetractModule"].active;
        }
      }
      return deployed;
    }
    return false; //< make the compiler happy
  }


  public static double EcConsumption(PartModule m)
  {
    if (!legacy_ec)
    {
      List<ModuleResource> resources = Lib.ReflectionValue<List<ModuleResource>>(m, "resourceInputs");
      foreach(ModuleResource res in resources)
      {
        if (res.name == "ElectricCharge") return res.rate;
      }
      return 0.0;
    }
    // versions before 16.4
    else
    {
      return Lib.ReflectionValue<float>(m, "power");
    }
  }
}


} // KERBALISM