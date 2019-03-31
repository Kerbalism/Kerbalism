using System;
using System.Reflection;

namespace KERBALISM
{
	public static class ModuleManager
	{
		public static int MM_major;
		public static int MM_minor;
		public static int MM_rev;
		static ModuleManager()
		{
			foreach (var a in AssemblyLoader.loadedAssemblies)
			{
				if (a.name == "ModuleManager")
				{
					Version v = a.assembly.GetName().Version;
					MM_major = v.Major;
					MM_minor = v.Minor;
					MM_rev = v.Revision;
					break;
				}
			}
		}
	}

}
