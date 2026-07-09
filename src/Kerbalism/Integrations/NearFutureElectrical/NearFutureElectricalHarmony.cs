using HarmonyLib;
using System.Reflection;

namespace KERBALISM
{
	internal static class NearFutureElectricalHarmony
	{
		public static void Apply(Harmony harmony)
		{
			System.Type capacitor = AccessTools.TypeByName("NearFutureElectrical.DischargeCapacitor");
			if (capacitor == null)
				return;

			MethodInfo prefix = AccessTools.Method(typeof(NearFutureElectricalHarmony), nameof(SkipWhenUpdaterPresent));
			Patch(harmony, capacitor, "OnFixedUpdate", prefix);
			Patch(harmony, capacitor, "DoCatchup", prefix);
			PatchRefresh(harmony, capacitor, "Enable");
			PatchRefresh(harmony, capacitor, "Disable");
			PatchRefresh(harmony, capacitor, "Discharge");
			PatchRefresh(harmony, capacitor, "ToggleAction");
		}

		private static void Patch(Harmony harmony, System.Type type, string methodName, MethodInfo prefix)
		{
			MethodInfo target = AccessTools.Method(type, methodName);
			if (target != null && prefix != null)
				harmony.Patch(target, new HarmonyMethod(prefix));
		}

		private static void PatchRefresh(Harmony harmony, System.Type type, string methodName)
		{
			MethodInfo target = AccessTools.Method(type, methodName);
			MethodInfo postfix = AccessTools.Method(typeof(NearFutureElectricalHarmony), nameof(RefreshPlanner));
			if (target != null && postfix != null)
				harmony.Patch(target, null, new HarmonyMethod(postfix));
		}

		private static bool SkipWhenUpdaterPresent(object __instance)
		{
			PartModule module = __instance as PartModule;
			return module == null || module.part == null || module.part.FindModuleImplementing<NFECapacitorKerbalismUpdater>() == null;
		}

		private static void RefreshPlanner()
		{
			if (Lib.IsEditor())
				Lib.RefreshPlanner();
		}
	}
}
