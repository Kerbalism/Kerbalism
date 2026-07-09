using HarmonyLib;

namespace KERBALISM
{
	internal static class IntegrationBootstrap
	{
		private static bool applied;
		private static bool systemHeatHarmonyApplied;

		public static void ApplyHarmonyPatches(Harmony harmony)
		{
			if (applied || harmony == null)
				return;

			applied = true;

			Apply("CryoTanks", CryoTanks.Installed, () => CryoTanksIntegration.ApplyHarmonyPatches(harmony));
			Apply("NearFutureElectrical", NearFutureElectrical.Installed, () => NearFutureElectricalIntegration.ApplyHarmonyPatches(harmony));
			Apply("FarFutureTechnologies", FarFutureTechnologies.Installed, () => FarFutureTechnologiesIntegration.ApplyHarmonyPatches(harmony));
			Apply("SpaceDust", SpaceDust.Installed, () => SpaceDustIntegration.ApplyHarmonyPatches(harmony));
			Apply("DynamicRadiation", true, () => DynamicRadiationIntegration.ApplyHarmonyPatches(harmony));

			TryApplySystemHeatHarmony(harmony);
		}

		public static void ApplyDeferredHarmonyPatches(Harmony harmony)
		{
			TryApplySystemHeatHarmony(harmony);
		}

		private static void TryApplySystemHeatHarmony(Harmony harmony)
		{
			if (systemHeatHarmonyApplied || harmony == null || !SystemHeat.Installed)
				return;

			systemHeatHarmonyApplied = true;
			Apply("SystemHeat", true, () => SystemHeatIntegration.ApplyHarmonyPatches(harmony));
		}

		private static void Apply(string name, bool enabled, System.Action apply)
		{
			if (!enabled)
				return;

			try
			{
				apply();
				IntegrationUtils.Log(name + " integration enabled.");
			}
			catch (System.Exception ex)
			{
				IntegrationUtils.LogError(name + " integration failed: " + ex);
			}
		}
	}
}
