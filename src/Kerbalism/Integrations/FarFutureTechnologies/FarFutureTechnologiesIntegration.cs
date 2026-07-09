using HarmonyLib;

namespace KERBALISM
{
	internal static class FarFutureTechnologiesIntegration
	{
		public static void ApplyHarmonyPatches(Harmony harmony)
		{
			FFTSettings.Load();
			FarFutureTechnologiesHarmony.Apply(harmony);
		}
	}
}
