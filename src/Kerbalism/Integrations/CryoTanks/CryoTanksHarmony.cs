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
			return module.part.FindModuleImplementing<CryoTankKerbalismUpdater>() == null
				&& module.part.FindModuleImplementing<SystemHeatCryoTankKerbalismUpdater>() == null;
		}

		private static bool PartHasUpdater(ProtoPartSnapshot part, string moduleName)
		{
			return IntegrationUtils.TryFindPartModuleSnapshot(part, moduleName) != null;
		}
	}
}
