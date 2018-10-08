namespace KerbalismBootstrap
{
	public static class Util
	{
		public static AssemblyLoader.LoadedAssembly FindKerbalism()
		{
			foreach (var a in AssemblyLoader.loadedAssemblies)
				if (a.name == BinName)
					return a;
			return null;
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
