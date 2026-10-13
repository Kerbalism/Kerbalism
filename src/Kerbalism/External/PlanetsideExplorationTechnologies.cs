namespace KERBALISM
{
	internal static class PlanetsideExplorationTechnologies
	{
		private static readonly OptionalAssembly assembly = new OptionalAssembly("PlanetsideExplorationTechnologies");

		public const string TurbineModuleName = "ModulePETTurbine";
		public const string TurbineTypeName = "PlanetsideExplorationTechnologies.Modules.ModulePETTurbine";

		public static bool Installed => assembly.Installed;

		public static bool IsTurbine(PartModule module)
		{
			return assembly.IsModule(module, TurbineTypeName) || (module != null && module.moduleName == TurbineModuleName);
		}

		public static T Get<T>(object instance, string name, T fallback = default(T)) => assembly.Get(instance, name, fallback);

		public static void Set<T>(object instance, string name, T value) => assembly.Set(instance, name, value);

		public static object Call(object instance, string name, System.Type[] parameters = null, object[] args = null)
			=> assembly.Call(instance, name, parameters, args);

		public static PartModule FindTurbineModule(Part part)
		{
			if (part == null)
				return null;

			for (int i = 0; i < part.Modules.Count; i++)
			{
				PartModule module = part.Modules[i];
				if (IsTurbine(module))
					return module;
			}

			return null;
		}
	}
}
