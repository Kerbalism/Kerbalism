using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Harmony;
using KSP.Localization;

namespace KERBALISM
{
    /*
		Fix for sample taking from a ModuleAsteroid.
		The module search for a ModuleScienceContainer to store the data into, it won't find it with Kerbalism
		See issue : https://github.com/Kerbalism/Kerbalism/issues/458

		ModuleAsteroid.TakeSampleEVAEvent() code ("Take sample" PAW button callback) :
		```
		ModuleScienceContainer collector = FlightGlobals.ActiveVessel.FindPartModuleImplementing<ModuleScienceContainer>();
		performSampleExperiment(collector);
		```

		and then in ModuleAsteroid.performSampleExperiment(ModuleScienceContainer collector) : 
		```
		if (collector.HasData(experimentData))
		{
			ScreenMessages.PostScreenMessage("<color=orange>[" + collector.part.partInfo.title + "]: <i>" + experimentData.title + cacheAutoLOC_230121, 5f, ScreenMessageStyle.UPPER_LEFT);
			return;
		}
		GameEvents.OnExperimentDeployed.Fire(experimentData);
		if (collector.AddData(experimentData))
		{
			collector.ReviewData();
		}
		```
	 */

    [HarmonyPatch(typeof(ModuleAsteroid))]
	[HarmonyPatch("TakeSampleEVAEvent")]
	class ModuleAsteroid_TakeSampleEVAEvent
	{
		static bool Prefix(ModuleAsteroid __instance, ref ScienceExperiment ___experiment)
		{
			// Patch only if science is enabled
			if (!Features.Science) return true;

            // stock ModuleAsteroid.performSampleExperiment code : get situation and check availablility
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

            // stock ModuleAsteroid.performSampleExperiment code : create subject
            ScienceSubject subject = ResearchAndDevelopment.GetExperimentSubject(___experiment, experimentSituation, __instance.part.partInfo.name + __instance.part.flightID, __instance.part.partInfo.title, __instance.vessel.mainBody, string.Empty, string.Empty);

			// put the data on the EVA kerbal drive.
			if (FlightGlobals.ActiveVessel == null) return false;
			double size = ___experiment.baseValue * ___experiment.dataScale;
			FlightGlobals.ActiveVessel.TryGetVesselDataTemp(out VesselData vd);
			DriveData drive = DriveData.SampleDrive(vd, size);
			if (drive != null)
			{
				double mass = size * Settings.AsteroidSampleMassPerMB;
				SubjectData subjectData = ScienceDB.GetSubjectDataFromStockId(subject.id);
				drive.RecordSample(subjectData, size, mass, true);
				Message.Post(Lib.BuildString("<b><color=ffffff>", subject.title, "</color></b>\n", (mass * 1000.0).ToString("F1"), "<b><i> Kg of sample stored</i></b>"));
			}
			else
			{
				Message.Post("Not enough sample storage available");
			}

            // don't call ModuleAsteroid.TakeSampleEVAEvent (this will also prevent the call to ModuleAsteroid.performSampleExperiment)
            return false;
		}
	}
}
