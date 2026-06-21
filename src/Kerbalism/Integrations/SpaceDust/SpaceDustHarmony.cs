using HarmonyLib;
using System.Reflection;

namespace KERBALISM
{
	internal static class SpaceDustHarmony
	{
		public static void Apply(Harmony harmony)
		{
			System.Type harvesterType = AccessTools.TypeByName("SpaceDust.ModuleSpaceDustHarvester");
			MethodInfo fixedUpdate = harvesterType?.GetMethod(
				"FixedUpdate",
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			MethodInfo prefix = AccessTools.Method(typeof(SpaceDustHarmony), nameof(SpaceDustFixedUpdatePrefix));
			MethodInfo postfix = AccessTools.Method(typeof(SpaceDustHarmony), nameof(SpaceDustFixedUpdatePostfix));
			if (fixedUpdate != null)
				harmony.Patch(fixedUpdate, new HarmonyMethod(prefix), new HarmonyMethod(postfix));

			System.Type backgroundType = AccessTools.TypeByName("SpaceDust.SpaceDustHarvesterBackground");
			MethodInfo backgroundProcess = backgroundType == null
				? null
				: AccessTools.Method(backgroundType, "Process", new[] { typeof(ProtoPartModuleSnapshot), typeof(Vessel) });
			if (backgroundProcess == null && backgroundType != null)
				backgroundProcess = AccessTools.Method(backgroundType, "Process");
			MethodInfo backgroundPrefix = AccessTools.Method(typeof(SpaceDustHarmony), nameof(SpaceDustBackgroundProcessPrefix));
			if (backgroundProcess != null && backgroundPrefix != null)
				harmony.Patch(backgroundProcess, new HarmonyMethod(backgroundPrefix));
		}

		private static void SpaceDustFixedUpdatePrefix(PartModule __instance)
		{
			if (__instance.part.FindModuleImplementing<SpaceDustHarvesterKerbalismUpdater>() != null)
				SpaceDustResourceBlocker.EnterBlock();
		}

		private static void SpaceDustFixedUpdatePostfix(PartModule __instance)
		{
			if (__instance.part.FindModuleImplementing<SpaceDustHarvesterKerbalismUpdater>() != null)
				SpaceDustResourceBlocker.ExitBlock();
		}

		private static bool SpaceDustBackgroundProcessPrefix(ProtoPartModuleSnapshot ___protoMiner, Vessel ___ves)
		{
			ProtoPartModuleSnapshot harvester = ___protoMiner;
			Vessel vessel = ___ves;

			if (harvester == null || vessel?.protoVessel == null)
				return true;

			foreach (ProtoPartSnapshot part in vessel.protoVessel.protoPartSnapshots)
			{
				if (!part.modules.Contains(harvester))
					continue;

				if (!part.modules.Exists(module => module.moduleName == "SpaceDustHarvesterKerbalismUpdater"))
					return true;

				Lib.Proto.Set(harvester, "Enabled", false);
				return false;
			}

			return true;
		}
	}
}
