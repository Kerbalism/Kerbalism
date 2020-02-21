using Harmony;

namespace KERBALISM
{
	[HarmonyPatch(typeof(InternalModel))]
	[HarmonyPatch("SpawnCrew")]
	class InternalModel_SpawnCrew
	{
		static bool Prefix(InternalModel __instance)
		{

			if (!__instance.part.vessel.KerbalismData().Parts.TryGet(__instance.part.flightID, out PartData pd))
				return true;

			if (pd.Habitat == null)
				return true;

			bool requireSuit = pd.Habitat.RequireSuit;

			foreach (InternalSeat internalSeat in __instance.seats)
			{
				internalSeat.allowCrewHelmet = requireSuit;
			}

			return true;
		}
	}
}
