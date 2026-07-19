using HarmonyLib;
using System.Collections;
using System.Reflection;

namespace KERBALISM
{
	internal static class SystemHeatHarmony
	{
		public static void Apply(Harmony harmony)
		{
			PatchPrefix(harmony, "SystemHeat.ModuleSystemHeatConverter", "PostProcess", nameof(SkipWhenConverterUpdaterPresent));
			PatchPrefix(harmony, "SystemHeat.ModuleSystemHeatHarvester", "PostProcess", nameof(SkipWhenHarvesterUpdaterPresent));
			PatchPrefix(harmony, "SystemHeat.ModuleSystemHeatFissionReactor", "HandleResourceActivities", nameof(SkipWhenFissionUpdaterPresent));
			PatchPrefix(harmony, "SystemHeat.ModuleSystemHeatFissionReactor", "DoCatchup", nameof(SkipWhenFissionUpdaterPresent));
			PatchPrefix(harmony, "SystemHeat.ModuleSystemHeatFissionEngine", "HandleResourceActivities", nameof(SkipWhenFissionEngineUpdaterPresent));
			PatchPrefix(harmony, "SystemHeat.ModuleSystemHeatFissionEngine", "DoCatchup", nameof(SkipWhenFissionEngineUpdaterPresent));
			PatchPrefix(harmony, "SystemHeat.ModuleSystemHeatRadiator", "FixedUpdate", nameof(SkipRadiatorRatesWhenKerbalismPresent));
			PatchPrefix(harmony, "ModuleActiveRadiator", "FixedUpdate", nameof(SkipStockRadiatorRatesWhenKerbalismPresent));
		}

		private static void PatchPrefix(Harmony harmony, string typeName, string methodName, string prefixName)
		{
			System.Type type = AccessTools.TypeByName(typeName);
			if (type == null)
				return;

			MethodInfo target = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
			MethodInfo prefix = AccessTools.Method(typeof(SystemHeatHarmony), prefixName);
			if (target != null && prefix != null)
				harmony.Patch(target, new HarmonyMethod(prefix));
		}

		private static bool SkipWhenConverterUpdaterPresent(object __instance, object result, double deltaTime)
		{
			if (!HasUpdater(__instance, typeof(SystemHeatConverterKerbalismUpdater)))
				return true;

			SystemHeat.UpdateFlux(__instance as PartModule, GetTimeFactor(result));
			return false;
		}

		private static bool SkipWhenHarvesterUpdaterPresent(object __instance, object result, double deltaTime)
		{
			if (!HasUpdater(__instance, typeof(SystemHeatHarvesterKerbalismUpdater)))
				return true;

			SystemHeat.UpdateFlux(__instance as PartModule, GetTimeFactor(result));
			return false;
		}

		private static bool SkipWhenFissionUpdaterPresent(object __instance)
		{
			// Fission engines inherit HandleResourceActivities/DoCatchup from the reactor base class.
			return !HasFissionKerbalismUpdater(__instance);
		}

		private static bool SkipWhenFissionEngineUpdaterPresent(object __instance)
		{
			return !HasFissionKerbalismUpdater(__instance);
		}

		private static bool HasFissionKerbalismUpdater(object instance)
		{
			return HasUpdater(instance, typeof(SystemHeatFissionReactorKerbalismUpdater))
				|| HasUpdater(instance, typeof(SystemHeatFissionEngineKerbalismUpdater));
		}

		private static bool SkipRadiatorRatesWhenKerbalismPresent(object __instance)
		{
			PartModule radiator = __instance as PartModule;
			if (radiator?.part == null)
				return true;

			if (radiator.part.FindModuleImplementing<SystemHeatRadiatorKerbalism>() == null)
				return true;

			ZeroResHandlerInputRates(radiator);
			return true;
		}

		private static bool SkipStockRadiatorRatesWhenKerbalismPresent(object __instance)
		{
			PartModule radiator = __instance as PartModule;
			if (radiator?.part == null)
				return true;

			SystemHeatRadiatorKerbalism wrapper = radiator.part.FindModuleImplementing<SystemHeatRadiatorKerbalism>();
			if (wrapper == null)
				return true;

			ZeroResHandlerInputRates(radiator);

			// A USRadiatorSwitch remains on the part to preserve mesh/function switching.
			// Once a real SystemHeat radiator is linked, suppress its inherited stock heat
			// transfer as well as its resource draw.
			if (radiator.moduleName == "USRadiatorSwitch"
				&& IntegrationUtils.FindModule(radiator.part, "ModuleSystemHeatRadiator") != null)
				return false;

			return true;
		}

		private static void ZeroResHandlerInputRates(PartModule radiator)
		{
			IList inputResources = SystemHeat.GetResHandlerInputResources(radiator);
			if (inputResources == null)
				return;

			for (int i = 0; i < inputResources.Count; i++)
			{
				object res = inputResources[i];
				if (res is ModuleResource moduleResource)
					moduleResource.rate = 0.0;
			}
		}

		private static float GetTimeFactor(object result)
		{
			if (result == null)
				return 1f;

			FieldInfo field = result.GetType().GetField("TimeFactor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field == null)
				return 1f;

			return (float)field.GetValue(result);
		}

		private static bool HasUpdater(object instance, System.Type updaterType)
		{
			PartModule module = instance as PartModule;
			if (module == null || module.part == null)
				return false;

			for (int i = 0; i < module.part.Modules.Count; i++)
			{
				PartModule candidate = module.part.Modules[i];
				if (candidate == null || !updaterType.IsInstanceOfType(candidate))
					continue;

				if (updaterType == typeof(SystemHeatConverterKerbalismUpdater) && candidate is SystemHeatConverterKerbalismUpdater converterUpdater)
				{
					if (converterUpdater.OwnsConverter(module))
						return true;
				}
				else if (updaterType == typeof(SystemHeatHarvesterKerbalismUpdater) && candidate is SystemHeatHarvesterKerbalismUpdater harvesterUpdater)
				{
					if (harvesterUpdater.OwnsHarvester(module))
						return true;
				}
				else
					return true;
			}

			return false;
		}
	}
}
