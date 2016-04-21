// ===================================================================================================================
// Utility functions to interact with SCANsat
// ===================================================================================================================


using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;



namespace KERBALISM {


public static class SCANsat
{
  // store initialized flag, for lazy initialization
  static bool initialized = false;

  // reflection type of SCANUtils static class in SCANsat assembly, if present
  static Type SCANUtils = null;


  // obtain the SCANUtils class from SCANsat assembly if it is loaded, only called once
  static void lazy_init()
  {
    // search for compatible SCANsat assembly
    if (!initialized)
    {
      foreach(var a in AssemblyLoader.loadedAssemblies)
      {
        if (a.name == "SCANsat" && a.assembly.GetName().Version.Major == 1 && a.assembly.GetName().Version.Minor >= 6)
        {
          SCANUtils = a.assembly.GetType("SCANsat.SCANUtils");
          break;
        }
      }
      initialized = true;
    }
  }


  // return true if a SCANsat module is scanning
  // - m: protomodule of a SCANsat or a resource scanner
  public static bool isScanning(ProtoPartModuleSnapshot m)
  {
    return Convert.ToBoolean(m.moduleValues.GetValue("scanning"));
  }


  // return true if a SCANsat module *was* scanning, before we stopped it for lack of EC
  // - m: protomodule of a SCANsat or a resource scanner
  public static bool wasScanning(ProtoPartModuleSnapshot m)
  {
    return Convert.ToBoolean(m.moduleValues.GetValue("was_scanning"));
  }


  // interrupt scanning of a SCANsat module, and record this in the 'was_scanning' entry in the protomodule
  // - v: vessel that own the module
  // - m: protomodule of a SCANsat or a resource scanner
  // - p: prefab of the part owning the module
  public static bool stopScanner(Vessel v, ProtoPartModuleSnapshot m, Part part_prefab)
  {
    lazy_init();
    if (SCANUtils != null)
    {
      m.moduleValues.AddValue("was_scanning", true.ToString());
      return (bool)SCANUtils.GetMethod("unregisterSensorExternal").Invoke(null, new System.Object[]{v, m, part_prefab});
    }
    return false;
  }


  // resume scanning of a SCANsat module, and clear the entry 'was_scanning' in the protomodule
  // - v: vessel that own the module
  // - m: protomodule of a SCANsat or a resource scanner
  // - p: prefab of the part owning the module
  public static bool resumeScanner(Vessel v, ProtoPartModuleSnapshot m, Part part_prefab)
  {
    lazy_init();
    if (SCANUtils != null)
    {
      m.moduleValues.RemoveValue("was_scanning");
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
}


} // KERBALISM