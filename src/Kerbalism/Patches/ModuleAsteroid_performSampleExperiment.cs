using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Harmony;
using KSP.Localization;

namespace KERBALISM
{
	[HarmonyPatch(typeof(ModuleAsteroid))]
	[HarmonyPatch("TakeSampleEVAEvent")]
	class ModuleAsteroid_TakeSampleEVAEvent
	{
		static bool Prefix(ModuleAsteroid __instance, ref ScienceExperiment ___experiment)
		{
			if (!Features.Science) return true;

			ExperimentSituations experimentSituation = ScienceUtil.GetExperimentSituation(__instance.vessel);
			string message = string.Empty;
			if (!ScienceUtil.RequiredUsageExternalAvailable(__instance.vessel, FlightGlobals.ActiveVessel, (ExperimentUsageReqs)__instance.experimentUsageMask, ___experiment, ref message))
			{
				ScreenMessages.PostScreenMessage("<b><color=orange>" + message + "</color></b>", 6f, ScreenMessageStyle.UPPER_LEFT);
				return false;
			}

			if (!___experiment.IsAvailableWhile(experimentSituation, __instance.vessel.mainBody))
			{
				ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_230133", ___experiment.experimentTitle), 5f, ScreenMessageStyle.UPPER_CENTER);
				return false;
			}

			ScienceSubject subject = ResearchAndDevelopment.GetExperimentSubject(___experiment, experimentSituation, __instance.part.partInfo.name + __instance.part.flightID, __instance.part.partInfo.title, __instance.vessel.mainBody, string.Empty, string.Empty);
			//subject = ResearchAndDevelopment.GetExperimentSubject(experiment, experimentSituation, base.part.partInfo.name + base.part.flightID, base.part.partInfo.title, base.vessel.mainBody, text, text2);

			if (FlightGlobals.ActiveVessel == null) return false;
			double size = ___experiment.baseValue * ___experiment.dataScale;
			Drive drive = Drive.SampleDrive(FlightGlobals.ActiveVessel, size);
			if (drive != null)
			{
				double mass = size * Settings.AsteroidSampleMassPerMB;
				drive.Record_sample(subject.id, size, mass);
				Message.Post(Lib.BuildString("<b><color=ffffff>", subject.title, "</color></b>\n", (mass * 1000.0).ToString("F1"), "<b><i> Kg of sample stored</i></b>"));
			}
			else
			{
				Message.Post("Not enough sample storage available");
			}
			return false;
		}
	}
}
