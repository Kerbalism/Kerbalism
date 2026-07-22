using HarmonyLib;

namespace KERBALISM
{
	internal static class SCANsatIntegration
	{
		public static void ApplyHarmonyPatches(Harmony harmony)
		{
			if (!Features.Science)
			{
				IntegrationUtils.Log("SCANsat science integration skipped (FeatureScience disabled).");
				return;
			}

			SCANsatHarmony.Apply(harmony);
		}
	}
}
