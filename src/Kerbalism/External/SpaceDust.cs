using System.Collections;

namespace KERBALISM
{
	internal static class SpaceDust
	{
		private static readonly OptionalAssembly assembly = new OptionalAssembly("SpaceDust");

		public static bool Installed => assembly.Installed;

		public static bool IsHarvester(PartModule module) => assembly.IsModule(module, "SpaceDust.ModuleSpaceDustHarvester");

		public static T Get<T>(object instance, string name, T fallback = default(T)) => assembly.Get(instance, name, fallback);
		public static void Set<T>(object instance, string name, T value) => assembly.Set(instance, name, value);
		public static object Call(object instance, string name, System.Type[] parameters = null, object[] args = null) => assembly.Call(instance, name, parameters, args);

		public static PartModule FindHarvesterModule(Part part)
		{
			if (part == null)
				return null;

			for (int i = 0; i < part.Modules.Count; i++)
			{
				PartModule module = part.Modules[i];
				if (IsHarvester(module) || module.moduleName == "ModuleSpaceDustHarvester")
					return module;
			}

			return null;
		}

		public static IList GetHarvestedResources(PartModule harvester)
		{
			IList resources = IntegrationReflection.GetList(harvester, "resources");
			if (resources != null && resources.Count > 0)
				return resources;

			resources = IntegrationReflection.GetList(harvester, "Resources");
			return resources;
		}

		public static string GetHarvestedResourceName(object entry) => IntegrationReflection.GetString(entry, "Name");

		public static double GetHarvestedResourceDensity(object entry)
		{
			double density = IntegrationReflection.GetDouble(entry, "density");
			return density > double.Epsilon ? density : 0.05d;
		}

		public static float GetHarvestedResourceBaseEfficiency(object entry) => IntegrationReflection.GetFloat(entry, "BaseEfficiency");

		public static double GetHarvestedResourceMinHarvestValue(object entry) => IntegrationReflection.GetDouble(entry, "MinHarvestValue");

		public static double SampleResource(string resourceName, CelestialBody body, double altitude, double latitude, double longitude)
		{
			if (!Installed || body == null)
				return 0d;

			System.Type mapType = assembly.Type("SpaceDust.SpaceDustResourceMap");
			if (mapType == null)
				return 0d;

			System.Reflection.PropertyInfo instanceProp = mapType.GetProperty("Instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
			object map = instanceProp?.GetValue(null, null);
			if (map == null)
				return 0d;

			System.Reflection.MethodInfo sample = mapType.GetMethod(
				"SampleResource",
				System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
				null,
				new[] { typeof(string), typeof(CelestialBody), typeof(double), typeof(double), typeof(double) },
				null);
			if (sample == null)
				return 0d;

			object result = sample.Invoke(map, new object[] { resourceName, body, altitude, latitude, longitude });
			return result is double value ? value : 0d;
		}
	}
}
