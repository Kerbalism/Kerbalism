using HarmonyLib;
using System.Reflection;

namespace KERBALISM
{
	internal static class PETTurbineHarmony
	{
		private static bool productionFailureLogged;

		public static void Apply(Harmony harmony)
		{
			System.Type turbineType = AccessTools.TypeByName(PlanetsideExplorationTechnologies.TurbineTypeName);
			if (turbineType == null)
			{
				Lib.Log("PET turbine Harmony: type not found: " + PlanetsideExplorationTechnologies.TurbineTypeName, Lib.LogLevel.Warning);
				return;
			}

			MethodInfo target = AccessTools.Method(turbineType, "UpdateResourceHandler");
			MethodInfo prefix = AccessTools.Method(typeof(PETTurbineHarmony), nameof(ProduceEcThroughKerbalism));
			if (target == null || prefix == null)
			{
				Lib.Log("PET turbine Harmony: UpdateResourceHandler patch target/prefix missing", Lib.LogLevel.Warning);
				return;
			}

			harmony.Patch(target, new HarmonyMethod(prefix));
		}

		/// <summary>
		/// Replace PET stock EC output with Kerbalism deferred production. This runs after PET
		/// has updated its wind efficiency, so the PAW and Kerbalism use the same rate.
		/// </summary>
		private static bool ProduceEcThroughKerbalism(object __instance)
		{
			PartModule module = __instance as PartModule;
			if (module == null || module.part == null)
				return true;

			if (!module.part.HasModuleImplementingFast<PETTurbineFixer>())
				return true;

			try
			{
				double rate = PETTurbineResourceSim.GetLoadedRate(module, module.vessel);
				if (rate > 0.0)
					ResourceCache.GetResource(module.vessel, "ElectricCharge").Produce(rate * Kerbalism.elapsed_s, ResourceBroker.WindTurbine);
				return false;
			}
			catch (System.Exception ex)
			{
				if (!productionFailureLogged)
				{
					productionFailureLogged = true;
					Lib.Log("PET turbine Kerbalism production failed; falling back to PET: " + ex, Lib.LogLevel.Error);
				}
				return true;
			}
		}
	}
}
