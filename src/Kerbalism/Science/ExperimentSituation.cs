using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{
	public enum KerbalismSituations
	{
		None, InnerBelt, OuterBelt, Magnetosphere, Reentry, Interstellar
	}

	/// <summary>
	/// Replacement for KSPs own ExperimentSituations
	/// </summary>
	public sealed class ExperimentSituation
	{
		private Vessel vessel;
		private ExperimentSituations sit;
		private KerbalismSituations kerbalism_sit = KerbalismSituations.None;

		public ExperimentSituation(Vessel vessel)
		{
			this.vessel = vessel;
			FixSituation();
		}

		/// <summary>
		/// The KSP stock function has the nasty habit of returning, on occasion,
		/// situations that should not exist (flying high/low with bodies that
		/// don't have atmosphere), so we have to force the situations a bit.
		/// </summary>
		private void FixSituation()
		{
			var body = vessel.mainBody;
			var alt = vessel.altitude;
			var vi = Cache.VesselInfo(vessel);


			// if (vi.magnetosphere) kerbalism_sit = KerbalismSituations.Magnetosphere;
			// if (vi.outer_belt) kerbalism_sit = KerbalismSituations.OuterBelt;
			// if (vi.inner_belt) kerbalism_sit = KerbalismSituations.InnerBelt;

			// leave these as an easter egg
			if (vi.interstellar && body.flightGlobalsIndex == 0) kerbalism_sit = KerbalismSituations.Interstellar;

			if (body.atmosphere	
				&& vessel.altitude < body.atmosphereDepth
				&& vessel.altitude > body.scienceValues.flyingAltitudeThreshold
				&& vessel.orbit.ApA > body.atmosphereDepth
				&& vessel.srfSpeed > 1984)
			{
				kerbalism_sit = KerbalismSituations.Reentry;
			}

			var treshold = body.scienceValues.spaceAltitudeThreshold;
			if (alt > treshold)
			{
				sit = ExperimentSituations.InSpaceHigh;
				return;
			}

			if(!body.atmosphere && alt > body.atmosphereDepth)
			{
				sit = ExperimentSituations.InSpaceLow;
				return;
			}

			if(body.atmosphere && alt > body.scienceValues.flyingAltitudeThreshold)
			{
				sit = ExperimentSituations.FlyingHigh;
				return;
			}

			sit = ScienceUtil.GetExperimentSituation(vessel);
		}

		internal bool IsAvailable(ScienceExperiment exp, CelestialBody mainBody)
		{
			return exp.IsAvailableWhile(sit, vessel.mainBody);
		}

		public override string ToString()
		{
			if (kerbalism_sit != KerbalismSituations.None)
				return kerbalism_sit.ToString();
			return sit.ToString();
		}

		public bool BiomeIsRelevant(ScienceExperiment experiment)
		{
			if(kerbalism_sit != KerbalismSituations.None)
				return false;

			return experiment.BiomeIsRelevantWhile(sit);
		}

		public float Multiplier(CelestialBody body)
		{
			var values = body.scienceValues;

			switch(kerbalism_sit)
			{
				case KerbalismSituations.InnerBelt:
				case KerbalismSituations.OuterBelt:
					return 1.3f * Math.Max(values.InSpaceHighDataValue, values.InSpaceLowDataValue);
				case KerbalismSituations.Reentry:
					return 1.5f * values.FlyingHighDataValue;
				case KerbalismSituations.Magnetosphere:
					return 1.1f * values.FlyingHighDataValue;
				case KerbalismSituations.Interstellar:
					return 3.5f * values.InSpaceHighDataValue;
			}

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
