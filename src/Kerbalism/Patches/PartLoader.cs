using Harmony;

namespace KERBALISM
{
	[HarmonyPatch(typeof(PartLoader))]
	[HarmonyPatch("ParsePart")]
	class PartLoader_ParsePart
	{
		// when SpawnCrew is called by KSP or mods, make sure we set the helmet state
		// according to the Habitat current pressure
		static void Postfix(AvailablePart __result)
		{
			PartVolumeAndSurface.EvaluatePrefabAtCompilation(__result);


		}

	}
}
