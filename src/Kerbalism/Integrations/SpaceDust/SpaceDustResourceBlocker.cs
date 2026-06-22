using System;
using HarmonyLib;

namespace KERBALISM
{
	/// <summary>
	/// While native SpaceDust harvesters run logic/UI, block their direct Part.RequestResource calls
	/// so Kerbalism owns resource accounting on parts with SpaceDustHarvesterKerbalismUpdater.
	/// </summary>
	internal static class SpaceDustResourceBlocker
	{
		[ThreadStatic]
		private static int blockDepth;

		internal static bool IsBlocking => blockDepth > 0;

		internal static void EnterBlock() => blockDepth++;

		internal static void ExitBlock()
		{
			if (blockDepth > 0)
				blockDepth--;
		}

		internal static bool ShouldBlockRequest(Part part)
		{
			if (!IsBlocking || part == null)
				return false;

			return part.FindModuleImplementing<SpaceDustHarvesterKerbalismUpdater>() != null;
		}

		internal static bool TryBlockRequest(Part part, double demand, ref double __result)
		{
			if (!ShouldBlockRequest(part))
				return false;

			// Pretend the request succeeded without touching part resources.
			__result = demand;
			return true;
		}
	}

	[HarmonyPatch(typeof(Part), "RequestResource", new[] { typeof(int), typeof(double) })]
	internal static class Patch_Part_RequestResource_SpaceDust_Int2
	{
		private static bool Prefix(Part __instance, double demand, ref double __result)
		{
			return !SpaceDustResourceBlocker.TryBlockRequest(__instance, demand, ref __result);
		}
	}

	[HarmonyPatch(typeof(Part), "RequestResource", new[] { typeof(int), typeof(double), typeof(ResourceFlowMode), typeof(bool) })]
	internal static class Patch_Part_RequestResource_SpaceDust_Int4
	{
		private static bool Prefix(Part __instance, double demand, ref double __result)
		{
			return !SpaceDustResourceBlocker.TryBlockRequest(__instance, demand, ref __result);
		}
	}

	[HarmonyPatch(typeof(Part), "RequestResource", new[] { typeof(string), typeof(double), typeof(ResourceFlowMode), typeof(bool) })]
	internal static class Patch_Part_RequestResource_SpaceDust_String4
	{
		private static bool Prefix(Part __instance, double demand, ref double __result)
		{
			return !SpaceDustResourceBlocker.TryBlockRequest(__instance, demand, ref __result);
		}
	}
}
