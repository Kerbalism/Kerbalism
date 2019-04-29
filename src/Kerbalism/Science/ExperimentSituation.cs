using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{

	/// <summary>
	/// Replacement for KSPs own ExperimentSituations
	/// </summary>
	public sealed class ExperimentSituation
	{
		private Vessel vessel;
		private ExperimentSituations sit;

		public ExperimentSituation(Vessel vessel)
		{
			this.vessel = vessel;
			this.sit = FixSituation();
		}

		private ExperimentSituations FixSituation()
		{
			var body = vessel.mainBody;
			var alt = vessel.altitude;

			var treshold = body.scienceValues.spaceAltitudeThreshold;
			if (alt > treshold)
			{
				return ExperimentSituations.InSpaceHigh;
			}

			if(!body.atmosphere && alt > body.atmosphereDepth)
			{
				return ExperimentSituations.InSpaceLow;
			}

			if(body.atmosphere && alt > body.scienceValues.flyingAltitudeThreshold)
			{
				return ExperimentSituations.FlyingHigh;
			}

			return ScienceUtil.GetExperimentSituation(vessel);
		}

		internal bool IsAvailable(ScienceExperiment exp, CelestialBody mainBody)
		{
			return exp.IsAvailableWhile(sit, vessel.mainBody);
		}

		public override string ToString()
		{
			//return "PolarOrbit"; <-- this could be... interesting.
			return sit.ToString();
		}

		public bool BiomeIsRelevant(ScienceExperiment experiment)
		{
			return experiment.BiomeIsRelevantWhile(sit);
		}

		public float Multiplier(CelestialBody body)
		{
			var values = body.scienceValues;
			switch (sit)
			{
				case ExperimentSituations.SrfLanded: return values.LandedDataValue;
				case ExperimentSituations.SrfSplashed: return values.SplashedDataValue;
				case ExperimentSituations.FlyingLow: return values.FlyingLowDataValue;
				case ExperimentSituations.FlyingHigh: return values.FlyingHighDataValue;
				case ExperimentSituations.InSpaceLow: return values.InSpaceLowDataValue;
				case ExperimentSituations.InSpaceHigh: return values.FlyingHighDataValue;
			}

			Lib.Log("Science: invalid/unknown situation");
			return 0;
		}
	}
}
