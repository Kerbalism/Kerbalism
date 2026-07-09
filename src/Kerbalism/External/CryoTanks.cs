using System.Collections;

namespace KERBALISM
{
	internal static class CryoTanks
	{
		private static readonly OptionalAssembly assembly = new OptionalAssembly("SimpleBoiloff");

		public static bool Installed => assembly.Installed;

		public static bool IsCryoTank(PartModule module) => assembly.IsModule(module, "SimpleBoiloff.ModuleCryoTank");

		public static T Get<T>(object instance, string name, T fallback = default(T)) => assembly.Get(instance, name, fallback);
		public static void Set<T>(object instance, string name, T value) => assembly.Set(instance, name, value);
		public static object Call(object instance, string name, System.Type[] parameters = null, object[] args = null) => assembly.Call(instance, name, parameters, args);

		public static IList GetFuels(PartModule tank)
		{
			return IntegrationReflection.GetList(tank, "fuels");
		}

		public static string GetFuelName(object fuelEntry) => IntegrationReflection.GetString(fuelEntry, "fuelName");

		public static float GetBoiloffRate(object fuelEntry) => IntegrationReflection.GetFloat(fuelEntry, "boiloffRate");

		public static bool GetCoolingEnabled(PartModule tank) => Get(tank, "CoolingEnabled", false);

		public static void SetCoolingEnabled(PartModule tank, bool value) => Set(tank, "CoolingEnabled", value);

		public static float GetCoolingCost(PartModule tank) => Get(tank, "CoolingCost", 0f);

		public static PartModule FindCryoTankModule(Part part)
		{
			if (part == null)
				return null;

			for (int i = 0; i < part.Modules.Count; i++)
			{
				PartModule module = part.Modules[i];
				if (IsCryoTank(module) || module.moduleName == "ModuleCryoTank")
					return module;
			}

			return null;
		}
	}
}
