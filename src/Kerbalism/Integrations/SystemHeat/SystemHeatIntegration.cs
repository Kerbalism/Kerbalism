using HarmonyLib;

namespace KERBALISM
{
	internal static class SystemHeatIntegration
	{
		public static void ApplyHarmonyPatches(Harmony harmony)
		{
			SystemHeatHarmony.Apply(harmony);
		}
	}
}
