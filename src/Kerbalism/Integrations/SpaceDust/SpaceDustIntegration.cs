using HarmonyLib;

namespace KERBALISM
{
	internal static class SpaceDustIntegration
	{
		public static void ApplyHarmonyPatches(Harmony harmony)
		{
			SpaceDustHarmony.Apply(harmony);
		}
	}
}
