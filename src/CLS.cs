// ====================================================================================================================
// interface with ConnectedLivingSpaces
// ====================================================================================================================


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;


namespace KERBALISM {


static class CLS
{
  static PropertyInfo cls;

  static CLS()
  {
    Type cls_type = AssemblyLoader
      .loadedAssemblies
      .SelectMany(a => a.assembly.GetExportedTypes())
      .SingleOrDefault(t => t.FullName == "ConnectedLivingSpace.CLSAddon");

    if (cls_type != null) cls = cls_type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
  }

  public static bool has()
  {
    return cls != null;
  }

  public static ConnectedLivingSpace.ICLSAddon get()
  {
    return cls == null ? null : (ConnectedLivingSpace.ICLSAddon)cls.GetValue(null, null);
  }
}


} // KERBALISM