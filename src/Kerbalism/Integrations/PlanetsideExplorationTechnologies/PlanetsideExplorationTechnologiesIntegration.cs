using HarmonyLib;

namespace KERBALISM
{
	internal static class PlanetsideExplorationTechnologiesIntegration
	{
		public static void ApplyHarmonyPatches(Harmony harmony)
		{
			PETTurbineHarmony.Apply(harmony);
		}
	}
}
