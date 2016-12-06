using System;


namespace KERBALISM {


public static class RemoteTech
{
  static RemoteTech()
  {
    foreach(var a in AssemblyLoader.loadedAssemblies)
    {
      var version = a.assembly.GetName().Version;
      if (a.name == "RemoteTech")
      {
        API = a.assembly.GetType("RemoteTech.API.API");
        IsEnabled = API.GetMethod("IsRemoteTechEnabled");
        IsConnected = API.GetMethod("HasAnyConnection");
        break;
      }
    }
  }

  // return true if RemoteTech is enabled for the current game
  public static bool Enabled()
  {
    return API != null && (bool)IsEnabled.Invoke(null, new Object[]{});
  }

  // return true if the vessel is connected according to RemoteTech
  public static bool Connected(Guid id)
  {
    return API != null && (bool)IsConnected.Invoke(null, new Object[]{id});
  }

  // reflection type of SCANUtils static class in SCANsat assembly, if present
  static Type API;
  static System.Reflection.MethodInfo IsEnabled;
  static System.Reflection.MethodInfo IsConnected;
}


} // KERBALISM

