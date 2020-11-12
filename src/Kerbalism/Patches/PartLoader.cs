using Harmony;

namespace KERBALISM
{
	[HarmonyPatch(typeof(PartLoader))]
	[HarmonyPatch("ParsePart")]
	class PartLoader_ParsePart
	{
		static void Postfix(AvailablePart __result)
		{
			PartVolumeAndSurface.EvaluatePrefabAtCompilation(__result);
		}
	}
}
