using HarmonyLib;
using System.Reflection;

namespace KERBALISM
{
	internal static class PETTurbineHarmony
	{
		private static bool productionFailureLogged;
		private static long hookCallCount;
		private static float nextHookTraceTime;
		private static float nextSyncTraceTime;

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
			Lib.Log("PET turbine Harmony: patched ModulePETTurbine.UpdateResourceHandler");
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

			if (module.part.FindModuleImplementing<PETTurbineFixer>() == null)
				return true;

			try
			{
				++hookCallCount;
				double rate = PETTurbineResourceSim.GetLoadedRate(module, module.vessel);
				ResourceInfo ec = ResourceCache.GetResource(module.vessel, "ElectricCharge");
				double deferredBefore = ec.Deferred;
				double quantity = 0.0;
				if (rate > 0.0)
				{
					quantity = rate * Kerbalism.elapsed_s;
					ec.Produce(quantity, ResourceBroker.WindTurbine);
				}

				if (UnityEngine.Time.realtimeSinceStartup >= nextHookTraceTime)
				{
					nextHookTraceTime = UnityEngine.Time.realtimeSinceStartup + 2.0f;
					object deployState = PlanetsideExplorationTechnologies.Get<object>(module, "deployState", null);
					bool isActive = PlanetsideExplorationTechnologies.Get(module, "isActive", false);
					float chargeRate = PlanetsideExplorationTechnologies.Get(module, "chargeRate", float.NaN);
					float efficiencyCurve = PlanetsideExplorationTechnologies.Get(module, "efficiencyCurve", float.NaN);
					float efficiencyAngle = PlanetsideExplorationTechnologies.Get(module, "efficiencyAngle", float.NaN);
					float angleFactor = PETTurbineResourceSim.GetAngleEfficiency(module);
					string pawRate = PlanetsideExplorationTechnologies.Get(module, "flowRateDisplay", "<missing>");

					Lib.Log(string.Format(
						"PET_TRACE HOOK calls={0} vessel={1} part={2} landed={3} splashed={4} atm={5:R} water={6} deploy={7} active={8} charge={9:R} efficiency={10:R} efficiencyAngle={11:R} angleFactor={12:R} PAW={13} calculatedRate={14:R} elapsed={15:R} queued={16:R} EC(amount={17:R},capacity={18:R},deferred={19:R}->{20:R})",
						hookCallCount,
						module.vessel != null ? module.vessel.vesselName : "<null>",
						module.part.partInfo != null ? module.part.partInfo.name : module.part.name,
						module.vessel != null && module.vessel.Landed,
						module.vessel != null && module.vessel.Splashed,
						module.vessel != null ? module.vessel.atmDensity : double.NaN,
						module.part.WaterContact,
						deployState != null ? deployState.ToString() : "<null>",
						isActive,
						chargeRate,
						efficiencyCurve,
						efficiencyAngle,
						angleFactor,
						pawRate,
						rate,
						Kerbalism.elapsed_s,
						quantity,
						ec.Amount,
						ec.Capacity,
						deferredBefore,
						ec.Deferred));
				}
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

		internal static void TraceResourceSync(
			Vessel vessel,
			double windQuantity,
			double deferredBeforeClamp,
			double deferredAfterClamp,
			double amountBefore,
			double physicalAmountBefore,
			double amountAfter,
			double capacity,
			double elapsed)
		{
			if (UnityEngine.Time.realtimeSinceStartup < nextSyncTraceTime)
				return;

			nextSyncTraceTime = UnityEngine.Time.realtimeSinceStartup + 2.0f;
			Lib.Log(string.Format(
				"PET_TRACE SYNC vessel={0} windBrokerQuantity={1:R} deferredBeforeClamp={2:R} deferredApplied={3:R} cachedAmountBefore={4:R} physicalAmountBefore={5:R} amountAfter={6:R} capacity={7:R} elapsed={8:R}",
				vessel != null ? vessel.vesselName : "<null>",
				windQuantity,
				deferredBeforeClamp,
				deferredAfterClamp,
				amountBefore,
				physicalAmountBefore,
				amountAfter,
				capacity,
				elapsed));
		}
	}
}
