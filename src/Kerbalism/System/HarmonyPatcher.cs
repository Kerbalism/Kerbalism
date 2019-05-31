using System;
using System.Reflection;
using System.Collections.Generic;
using Harmony;
using Harmony.ILCopying;
using Expansions.Serenity.DeployedScience.Runtime;

namespace KERBALISM
{
	[HarmonyPatch(typeof(DeployedScienceExperiment))]
	[HarmonyPatch("SendDataToComms")]
	class SDTCPatch {
		static bool Prefix(DeployedScienceExperiment __instance, ref bool __result, ref int count) {
			// get private vars
			ScienceSubject subject = Lib.ReflectionValue<ScienceSubject>(__instance, "subject");
			float storedScienceData = Lib.ReflectionValue<float>(__instance, "storedScienceData");
			if (__instance.Experiment != null && !(__instance.ExperimentVessel == null) && subject != null && !(__instance.Cluster == null) && __instance.sciencePart.Enabled && !(storedScienceData <= 0f) && __instance.ExperimentSituationValid) {

			}
			return false; // always return false so we don't continue to the original code
		}
	}
}
