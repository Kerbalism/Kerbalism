using HarmonyLib;

namespace KERBALISM
{
	internal static class CryoTanksIntegration
	{
		public static void ApplyHarmonyPatches(Harmony harmony)
		{
			CryoSettings.Load();
			CryoTanksHarmony.Apply(harmony);
		}
	}
}
