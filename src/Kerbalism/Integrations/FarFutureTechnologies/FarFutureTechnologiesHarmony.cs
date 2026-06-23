using HarmonyLib;
using System.Reflection;

namespace KERBALISM
{
	internal static class FarFutureTechnologiesHarmony
	{
		public static void Apply(Harmony harmony)
		{
			System.Type reactor = AccessTools.TypeByName("FarFutureTechnologies.FusionReactor");
			System.Type engine = AccessTools.TypeByName("FarFutureTechnologies.FusionEngine");
			MethodInfo reactorPrefix = AccessTools.Method(typeof(FarFutureTechnologiesHarmony), nameof(SkipWhenFusionReactorUpdaterPresent));
			MethodInfo enginePrefix = AccessTools.Method(typeof(FarFutureTechnologiesHarmony), nameof(SkipWhenFusionEngineUpdaterPresent));
			Patch(harmony, reactor, "GeneratePower", reactorPrefix);
			Patch(harmony, reactor, "RechargeCapacitors", reactorPrefix);
			Patch(harmony, engine, "GeneratePower", enginePrefix);
			Patch(harmony, engine, "RechargeCapacitors", enginePrefix);

			System.Type antimatterTank = AccessTools.TypeByName("FarFutureTechnologies.ModuleAntimatterTank");
			MethodInfo skipAntimatter = AccessTools.Method(typeof(FarFutureTechnologiesHarmony), nameof(SkipWhenAntimatterUpdaterPresent));
			Patch(harmony, antimatterTank, "DoCatchup", skipAntimatter);
			Patch(harmony, antimatterTank, "ConsumeCharge", skipAntimatter);
			Patch(harmony, antimatterTank, "DoDetonation", skipAntimatter);
		}

		private static void Patch(Harmony harmony, System.Type type, string methodName, MethodInfo prefix)
		{
			MethodInfo target = type == null ? null : AccessTools.Method(type, methodName);
			if (target != null && prefix != null)
				harmony.Patch(target, new HarmonyMethod(prefix));
		}

		private static bool SkipWhenFusionReactorUpdaterPresent(object __instance)
		{
			return !FarFutureTechnologies.HasKerbalismFusionUpdater(__instance as PartModule);
		}

		private static bool SkipWhenFusionEngineUpdaterPresent(object __instance)
		{
			return !HasUpdater<FFTFusionEngineKerbalismUpdater>(__instance);
		}

		private static bool SkipWhenAntimatterUpdaterPresent(object __instance)
		{
			return !FarFutureTechnologies.HasKerbalismAntimatterUpdater(__instance as PartModule);
		}

		private static bool HasUpdater<T>(object instance) where T : PartModule
		{
			PartModule module = instance as PartModule;
			return module != null && module.part != null && module.part.FindModuleImplementing<T>() != null;
		}
	}
}
