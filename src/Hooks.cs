// ====================================================================================================================
// Hooks for other mods
// ====================================================================================================================
// this class is meant to be copied into the source of other mods that wish to interact with Kerbalism wihout compiling against it


#if false


using System;
using System.Reflection;

  
public static class Kerbalism_Hooks
{
  // return true if Kerbalism is installed
  public static bool HasKerbalism()
  {
    foreach(var a in AssemblyLoader.loadedAssemblies)
    {
      if (a.name == "Kerbalism") return true;
    }
    return false;
  }
  
  // enable/disable resource consumption for a specific kerbal, do nothing if Kerbalism isn't installed
  // - k_name: name of the kerbal
  // - disabled: true to disable resource consumption, false to re-enable it
  public static void DisableKerbal(string k_name, bool disabled)
  {
    foreach(var a in AssemblyLoader.loadedAssemblies)
    {
      if (a.name == "Kerbalism")
      {
        Type db_type = a.assembly.GetType("Kerbalism.DB");
        MethodInfo disable_kerbal = db_type.GetMethod("DisableKerbal");
        disable_kerbal.Invoke(null, new Object[]{k_name, disabled});
        return;
      }
    }
  }
}


#endif