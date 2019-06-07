using System;
using System.Collections.Generic;

namespace KerbalismBootstrap
{
	public static class Util
	{
		public static AssemblyLoader.LoadedAssembly FindKerbalismBin()
		{
			foreach (var a in AssemblyLoader.loadedAssemblies)
				if (a.name == BinName || a.name == BinName_R)
					return a;
			return null;
		}

		public static bool IsDllLoaded
		{
			get
			{
				foreach (AssemblyLoader.LoadedAssembly a in AssemblyLoader.loadedAssemblies)
					if (a.name == "Kerbalism")
						return true;
				return false;
			}
		}

		public static string BinName
		{
			get
			{
				return "Kerbalism" + Versioning.version_major.ToString() + Versioning.version_minor.ToString();
			}
		}

		public static string BinName_R
		{
			get
			{
				return "Kerbalism" + Versioning.version_major.ToString() + Versioning.version_minor.ToString() + Versioning.Revision;
			}
		}

		// This is just so we have 1.3 compat!
		public static void AddToLoadedTypesDict( ref Dictionary<Type, Dictionary<String, Type>> dict, Type loadedType, Type type )
		{
			if (!dict.ContainsKey( loadedType ))
			{
				dict[loadedType] = new Dictionary<string, Type>();
			}
			dict[loadedType][type.Name] = type;
		}
	}
}
