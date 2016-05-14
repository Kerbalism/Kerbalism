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


  // post something using kerbalism message system
  public static void Message(string msg)
  {
    lazy_init();
    if (K == null) return;
    K.GetMethod("hook_Message").Invoke(null, new Object[]{msg});
  }


  // kill a kerbal
  public static void Kill(Vessel v, ProtoCrewMember c)
  {
    lazy_init();
    if (K == null) return;
    K.GetMethod("hook_Kill").Invoke(null, new Object[]{v, c});
  }


  // trigger a breakdown event for a kerbal
  public static void Breakdown(Vessel v, ProtoCrewMember c)
  {
    lazy_init();
    if (K == null) return;
    K.GetMethod("hook_Breakdown").Invoke(null, new Object[]{v, c});
  }


  // enable/disable resource consumption for a specific kerbal, do nothing if Kerbalism isn't installed
  // - k_name: name of the kerbal
  // - disabled: true to disable resource consumption, false to re-enable it
  public static void DisableKerbal(string k_name, bool disabled)
  {
    lazy_init();
    if (K == null) return;
    K.GetMethod("hook_DisableKerbal").Invoke(null, new Object[]{k_name, disabled});
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
    K.GetMethod("hook_InjectRadiation").Invoke(null, new Object[]{k_name, rad_amount});
  }


  // return true if the vessel is in sunlight
  // this does the same as v.directSunlight, but also work in background
  public static bool InSunlight(Vessel v)
  {
    lazy_init();
    if (K == null) return false;
    return (bool)K.GetMethod("hook_InSunlight").Invoke(null, new Object[]{v});
  }


  // return true if the vessel is in a breathable atmosphere
  public static bool Breathable(Vessel v)
  {
    lazy_init();
    if (K == null) return false;
    return (bool)K.GetMethod("hook_Breathable").Invoke(null, new Object[]{v});
  }


  // return the radiation level for a vessel, in rad/s
  public static double RadiationLevel(Vessel v)
  {
    lazy_init();
    if (K == null) return 0.0;
    return (double)K.GetMethod("hook_RadiationLevel").Invoke(null, new Object[]{v});
  }


  // return the link state of a vessel
  //   0: no link
  //   1: indirect link (relayed)
  //   2: direct link
  public static uint LinkStatus(Vessel v)
  {
    lazy_init();
    if (K == null) return 0;
    return (uint)K.GetMethod("hook_LinkStatus").Invoke(null, new Object[]{v});
  }


  // return how bad the malfunction situation is for a vessel
  //  0: no malfunction in any part
  //  1: at least a component has up to 1 malfunction
  //  2: at least a component has up to 2 malfunctions
  public static uint Malfunctions(Vessel v)
  {
    lazy_init();
    if (K == null) return 0;
    return (uint)K.GetMethod("hook_Malfunctions").Invoke(null, new Object[]{v});
  }


  // return true if a storm is inbound toward the vessel location
  public static bool StormIncoming(Vessel v)
  {
    lazy_init();
    if (K == null) return false;
    return (bool)K.GetMethod("hook_StormIncoming").Invoke(null, new Object[]{v});
  }


  // return true if a storm is in progress at the vessel location
  public static bool StormInProgress(Vessel v)
  {
    lazy_init();
    if (K == null) return false;
    return (bool)K.GetMethod("hook_StormInProgress").Invoke(null, new Object[]{v});
  }


  // return true if the vessel is inside a magnetosphere
  public static bool InsideMagnetosphere(Vessel v)
  {
    lazy_init();
    if (K == null) return false;
    return (bool)K.GetMethod("hook_InsideMagnetosphere").Invoke(null, new Object[]{v});
  }


  // return true if the vessel is inside a radiation belt
  public static bool InsideBelt(Vessel v)
  {
    lazy_init();
    if (K == null) return false;
    return (bool)K.GetMethod("hook_InsideBelt").Invoke(null, new Object[]{v});
  }


  // return the living space factor for the internal space that contain the specified kerbal
  public static double LivingSpace(string k_name)
  {
    lazy_init();
    if (K == null) return 1.0;
    return (double)K.GetMethod("hook_LivingSpace").Invoke(null, new Object[]{k_name});
  }


  // return the entertainment factor for the internal space that contain the specified kerbal
  public static double Entertainment(string k_name)
  {
    lazy_init();
    if (K == null) return 1.0;
    return (double)K.GetMethod("hook_Entertainment").Invoke(null, new Object[]{k_name});
  }


  // return the shielding factor for the internal space that contain the specified kerbal
  public static double Shielding(string k_name)
  {
    lazy_init();
    if (K == null) return 0.0;
    return (double)K.GetMethod("hook_Shielding").Invoke(null, new Object[]{k_name});
  }
  
  
  // return true if a part in a loaded vessel has one or more malfunctions
  public static bool Malfunctioned(Part part)
  {
    lazy_init();
    if (K == null) return false;
    return (bool)K.GetMethod("hook_Malfunctioned").Invoke(null, new Object[]{part});
  }
  
  
  // repair a malfunctioned part in a loaded vessel
  public static void Repair(Part part)
  {
    lazy_init();
    if (K == null) return;
    K.GetMethod("hook_Repair").Invoke(null, new Object[]{part});
  }
}


#endif