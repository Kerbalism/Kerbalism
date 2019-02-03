using System;
using System.Reflection;

namespace KERBALISM
{
	public static class ModuleManager
	{
		public static int MM_major;
		public static int MM_minor;
#if !KSP13
		public static int MM_rev;
#endif
		static ModuleManager()
		{
			foreach (var a in AssemblyLoader.loadedAssemblies)
			{
				if (a.name == "ModuleManager")
				{
					Version v = a.assembly.GetName().Version;
					MM_major = v.Major;
					MM_minor = v.Minor;
#if !KSP13
					MM_rev = v.Revision;
#endif
					break;
				}
			}
		}
	}

}
