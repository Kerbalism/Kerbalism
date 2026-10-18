using HarmonyLib;
using System.Reflection;

namespace KERBALISM
{
	internal static class CryoTanksHarmony
	{
		public static void Apply(Harmony harmony)
		{
			MethodInfo processCryoTank = AccessTools.Method(typeof(Background), "ProcessCryoTank");
			MethodInfo skipBackground = AccessTools.Method(typeof(CryoTanksHarmony), nameof(SkipKerbalismBackgroundWhenUpdaterPresent));
			if (processCryoTank != null && skipBackground != null)
				harmony.Patch(processCryoTank, new HarmonyMethod(skipBackground));

			System.Type cryoType = AccessTools.TypeByName("SimpleBoiloff.ModuleCryoTank");
			MethodInfo fixedUpdate = cryoType == null ? null : AccessTools.Method(cryoType, "FixedUpdate");
			MethodInfo skipFixedUpdate = AccessTools.Method(typeof(CryoTanksHarmony), nameof(SkipNativeCryoFixedUpdateWhenUpdaterPresent));
			if (fixedUpdate != null && skipFixedUpdate != null)
				harmony.Patch(fixedUpdate, new HarmonyMethod(skipFixedUpdate));

			System.Type systemHeatCryoType = AccessTools.TypeByName("SystemHeat.ModuleSystemHeatCryoTank");
			if (systemHeatCryoType != null)
			{
				// Kerbalism already accounts for unloaded boiloff, so native catchup would count it twice.
				harmony.Patch(AccessTools.Method(systemHeatCryoType, "DoCatchup"),
					prefix: new HarmonyMethod(typeof(CryoTanksHarmony), nameof(SkipSystemHeatCatchupWhenUpdaterPresent)));
				// The native simulation and PAW both use this limit to decide whether cooling is sufficient.
				harmony.Patch(AccessTools.Method(systemHeatCryoType, "GetMaxSourceTemperature"),
					postfix: new HarmonyMethod(typeof(CryoTanksHarmony), nameof(ApplySystemHeatCoolingTolerance)));
			}
		}

		private static bool SkipSystemHeatCatchupWhenUpdaterPresent(PartModule __instance)
		{
			return !CryoSettings.Enabled || !__instance.part.HasModuleImplementingFast<SystemHeatCryoTankKerbalismUpdater>();
		}

		private static void ApplySystemHeatCoolingTolerance(PartModule __instance, ref float __result)
		{
			if (CryoSettings.Enabled && __instance.part.HasModuleImplementingFast<SystemHeatCryoTankKerbalismUpdater>())
				__result += SystemHeatCryoResourceSim.CoolingTemperatureToleranceK;
		}

		private static bool SkipKerbalismBackgroundWhenUpdaterPresent(ProtoPartSnapshot p)
		{
			return !PartHasUpdater(p, "CryoTankKerbalismUpdater") && !PartHasUpdater(p, "SystemHeatCryoTankKerbalismUpdater");
		}

		private static bool SkipNativeCryoFixedUpdateWhenUpdaterPresent(object __instance)
		{
			PartModule module = __instance as PartModule;
			if (module == null || module.part == null)
				return true;
			return !module.part.HasModuleImplementingFast<CryoTankKerbalismUpdater>()
				&& !module.part.HasModuleImplementingFast<SystemHeatCryoTankKerbalismUpdater>();
		}

		private static bool PartHasUpdater(ProtoPartSnapshot part, string moduleName)
		{
			return IntegrationUtils.TryFindPartModuleSnapshot(part, moduleName) != null;
		}
	}
}
