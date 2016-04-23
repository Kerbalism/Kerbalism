// ====================================================================================================================
// Hooks for other mods
// ====================================================================================================================
// this class is meant to be copied into the source of other mods that wish to interact with Kerbalism wihout compiling against it


#if false


using System;
using System.Reflection;


public static class Kerbalism_Hooks
{
  // store initialized flag, for lazy initialization
  static bool initialized = false;

  // reflection type of Kerbalism class, if present
  static Type K = null;


  // obtain the Kerbalism class if the assembly is loaded, only called once
  static void lazy_init()
  {
    // search for compatible assembly
    if (!initialized)
    {
      foreach(var a in AssemblyLoader.loadedAssemblies)
      {
        if (a.name == "Kerbalism")
        {
          K = a.assembly.GetType("KERBALISM.Kerbalism");
          break;
        }
      }
      initialized = true;
    }
  }


  // return true if Kerbalism is installed
  public static bool HasKerbalism()
  {
    lazy_init();
    return K != null;
  }


  // enable/disable resource consumption for a specific kerbal, do nothing if Kerbalism isn't installed
  // - k_name: name of the kerbal
  // - disabled: true to disable resource consumption, false to re-enable it
  public static void DisableKerbal(string k_name, bool disabled)
  {
    lazy_init();
    if (K == null) return;
    K.GetMethod("hook_DisableKerbal").Invoke(null, new System.Object[]{k_name, disabled});
  }


  // injiect radiation into a Kerbal
  // - k_name: name of the kerbal
  // - amount: amount of radiation to inject, in rad
  // note: you can use negative amounts to remove radiation from a kerbal
  // note: if you are calling this function at every simulation step it will make sense to scale the amount by TimeWarp.FixedDeltaTime
  public static void InjectRadiation(string k_name, double rad_amount)
  {
    lazy_init();
    if (K == null) return;
    K.GetMethod("hook_InjectRadiation").Invoke(null, new System.Object[]{k_name, rad_amount});
  }
}


#endif