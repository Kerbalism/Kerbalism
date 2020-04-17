using Harmony;

namespace KERBALISM
{
	[HarmonyPatch(typeof(InternalModel))]
	[HarmonyPatch("SpawnCrew")]
	class InternalModel_SpawnCrew
	{
		static bool Prefix(InternalModel __instance)
		{

			if (!__instance.part.TryGetFlightModuleDataOfType(out HabitatData habitatData))
				return true;

			bool requireSuit = habitatData.RequireSuit;

			foreach (InternalSeat internalSeat in __instance.seats)
			{
				internalSeat.allowCrewHelmet = requireSuit;
			}

			return true;
		}
	}
}
