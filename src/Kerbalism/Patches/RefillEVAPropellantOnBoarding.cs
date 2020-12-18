#if !KSP15_16 && !KSP18 && !KSP110

using Harmony;

namespace KERBALISM
{
	// Prevent stock auto-refilling of EVA propellant in the KSP 1.11+ jetpack if we are using Monopropellant
	[HarmonyPatch(typeof(ModuleInventoryPart))]
	[HarmonyPatch("RefillEVAPropellant")]
	class RefillEVAPropellant
	{
		static bool Prefix()
		{
			if (Lib.EvaPropellantName() != "EVA Propellant")
			{
				return false;
			}

			return true;
		}
	}
}

#endif
