using HarmonyLib;

namespace KERBALISM
{
	internal static class NearFutureElectricalIntegration
	{
		public static void ApplyHarmonyPatches(Harmony harmony)
		{
			NearFutureElectricalHarmony.Apply(harmony);
		}
	}
}
