namespace KERBALISM
{
	internal static class NearFutureElectrical
	{
		private static readonly OptionalAssembly assembly = new OptionalAssembly("NearFutureElectrical");

		public static bool Installed => assembly.Installed;

		public static bool IsCapacitor(PartModule module) => assembly.IsModule(module, "NearFutureElectrical.DischargeCapacitor");

		public static T Get<T>(object instance, string name, T fallback = default(T)) => assembly.Get(instance, name, fallback);
		public static void Set<T>(object instance, string name, T value) => assembly.Set(instance, name, value);
		public static object Call(object instance, string name, System.Type[] parameters = null, object[] args = null) => assembly.Call(instance, name, parameters, args);

		public static PartModule FindCapacitorModule(Part part)
		{
			if (part == null)
				return null;

			for (int i = 0; i < part.Modules.Count; i++)
			{
				PartModule module = part.Modules[i];
				if (IsCapacitor(module) || module.moduleName == "DischargeCapacitor")
					return module;
			}

			return null;
		}

		public static void Enable(PartModule capacitor) => Call(capacitor, "Enable");

		public static void Disable(PartModule capacitor) => Call(capacitor, "Disable");

		public static void Discharge(PartModule capacitor) => Call(capacitor, "Discharge");
	}
}
