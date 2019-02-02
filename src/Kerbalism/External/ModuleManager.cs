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
					MM_major = a.versionMajor;
					MM_minor = a.versionMinor;
#if !KSP13
					MM_rev = a.versionRevision;
#endif
					break;
				}
			}
		}
	}

}
