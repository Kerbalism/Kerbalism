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

			// SCANsat keeps native map production. KerbalismScansat only converts coverage
			// percent growth into science Files — no Harmony paint divert.
			IntegrationUtils.Log("SCANsat science sidecar ready (native map).");
		}
	}
}
