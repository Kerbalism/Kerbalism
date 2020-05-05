using Harmony;

namespace KERBALISM
{
	[HarmonyPatch(typeof(InternalModel))]
	[HarmonyPatch("SpawnCrew")]
	class InternalModel_SpawnCrew
	{
		// force instantiation of the helmets
		static void Prefix(InternalModel __instance)
		{
			foreach (InternalSeat internalSeat in __instance.seats)
			{
				internalSeat.allowCrewHelmet = true;
			}
		}

		// when SpawnCrew is called by KSP or mods, make sure we set the helmet state
		// according to the Habitat current pressure
		static void Postfix(InternalModel __instance)
		{
			if (!__instance.part.TryGetFlightModuleDataOfType(out HabitatData habitatData))
				return;

			bool requireSuit = habitatData.RequireSuit;
			foreach (InternalSeat internalSeat in __instance.seats)
			{
				if (internalSeat.kerbalRef == null)
					continue;

				internalSeat.kerbalRef.ShowHelmet(requireSuit);
			}
		}
	}

	[HarmonyPatch(typeof(InternalModel))]
	[HarmonyPatch("SetVisible")]
	class InternalModel_SetVisible
	{
		// Don't show the internal model on non-deployed habitats
		static void Prefix(InternalModel __instance, ref bool visible)
		{
			// if visible == false, we don't care
			if (!visible)
				return;

			// otherwise, set the visible parameter to the habitat deployed state before continuing to the method
			if (!__instance.part.TryGetFlightModuleDataOfType(out HabitatData habitatData))
				return;

			visible = habitatData.IsDeployed;
		}
	}

	[HarmonyPatch(typeof(Kerbal))]
	[HarmonyPatch("ShowHelmet")]
	class Kerbal_ShowHelmet
	{
		// Here we completely override the stock method because if the original ShowHelmet(false) 
		// is called it will actually do Object.Destroy(helmetTransform.gameObject), resulting in 
		// any latter call to ShowHelmet(true) to fail to actually show the helmets.
		// To get around that, we used to respawn the whole vessel IVA, but patching ShowHelmet()
		// is a lot cleaner and faster.
		static bool Prefix(Kerbal __instance, bool show)
		{
			if (__instance.helmetTransform != null)
			{
				__instance.helmetTransform.gameObject.SetActive(show);
				return false;
			}
			return true;
		}
	}
}
