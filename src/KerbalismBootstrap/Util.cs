namespace KerbalismBootstrap
{
	public static class Util
	{
		public static AssemblyLoader.LoadedAssembly FindKerbalismBin()
		{
			foreach (var a in AssemblyLoader.loadedAssemblies)
				if (a.name == BinName)
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
	}
}
