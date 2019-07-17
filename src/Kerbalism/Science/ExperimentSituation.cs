using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{
	/// <summary>
	/// This is an extension to the situations that stock KSP provides.
	/// </summary>
	public enum KerbalismSituations
	{
		None = 0,

		SrfLanded = (1 << 0), // 1
		SrfSplashed = (1 << 1), // 2
		FlyingLow = (1 << 2), // 4
		FlyingHigh = (1 << 3), // 8
		InSpaceLow = (1 << 4), // 16
		InSpaceHigh = (1 << 5), // 32

		// Kerbalism extensions
		InnerBelt = (1 << 6), // 64
		OuterBelt = (1 << 7), // 128
		Magnetosphere = (1 << 8), // 256
		Reentry = (1 << 9), // 512
		Interstellar = (1 << 10) // 1024
	}

	/// <summary>
	/// Replacement for KSPs own ExperimentSituations
	/// </summary>
	public sealed class ExperimentSituation
	{
		private Vessel vessel;
		private KerbalismSituations sit = KerbalismSituations.None;

		public ExperimentSituation(Vessel vessel)
		{
			this.vessel = vessel;
			FixSituation();
		}

		public bool AtmosphericFlight()
		{
			switch(sit)
			{
				case KerbalismSituations.FlyingLow:
				case KerbalismSituations.FlyingHigh:
				case KerbalismSituations.Reentry:
					return true;
				default:
					return false;
			}
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

			//if (vi.magnetosphere) sit = KerbalismSituations.Magnetosphere;
			//if (vi.outer_belt) sit = KerbalismSituations.OuterBelt;
			//if (vi.inner_belt) sit = KerbalismSituations.InnerBelt;

			// leave these as an easter eggs, for now
			if (vi.interstellar && Lib.IsSun(body))
			{
				sit = KerbalismSituations.Interstellar;
				return;
			}
			if (body.atmosphere	// only on bodies with atmosphere
				&& vessel.verticalSpeed < 100 // descending
				&& vessel.altitude < body.atmosphereDepth // in the atmosphere
				&& vessel.altitude > body.scienceValues.flyingAltitudeThreshold // above the flying high treshold
				&& (double.IsNaN(vessel.orbit.ApA) || vessel.orbit.ApA > body.atmosphereDepth) // apoapsis above atmosphere or NaN
				&& vessel.srfSpeed > vessel.speedOfSound * 5) // mach 5
			{
				sit = KerbalismSituations.Reentry;
				return;
			}

			if(vi.landed) {
				var s = ScienceUtil.GetExperimentSituation(vessel);
				switch (s)
				{
					case ExperimentSituations.SrfLanded: sit = KerbalismSituations.SrfLanded; return;
					case ExperimentSituations.SrfSplashed: sit = KerbalismSituations.SrfSplashed; return;
				}
			}

			var treshold = body.scienceValues.spaceAltitudeThreshold;
			if (alt > treshold)
			{
				sit = KerbalismSituations.InSpaceHigh;
				return;
			}

			if(alt > body.atmosphereDepth)
			{
				sit = KerbalismSituations.InSpaceLow;
				return;
			}

			if(body.atmosphere && alt > body.scienceValues.flyingAltitudeThreshold)
			{
				sit = KerbalismSituations.FlyingHigh;
				return;
			}

			sit = KerbalismSituations.FlyingLow;
		}

		public override string ToString()
		{
			return sit.ToString();
		}

		internal bool IsAvailable(ExperimentInfo exp)
		{
			// make sure to not supersede SpaceHigh with our custom situations, otherwise
			// those experiments won't run any more while in space high and in a belt
			var s = sit;
			if ((exp.situationMask & (int)s) != 0) return true;

			if (s >= KerbalismSituations.InnerBelt) s = KerbalismSituations.InSpaceHigh;
			return (exp.situationMask & (int)s) != 0;
		}

		public bool BiomeIsRelevant(ExperimentInfo experiment)
		{
			return (experiment.biomeMask & (int)sit) != 0;
		}

		public float Multiplier(ExperimentInfo experiment)
		{
			var values = vessel.mainBody.scienceValues;

			var s = sit;

			// only consider kerbalism situation multipliers if the experiment is enabled for them
			if (experiment.situationMask < (int)KerbalismSituations.InnerBelt)
				s = StockSituation(sit);

			switch(s)
			{
				case KerbalismSituations.SrfLanded: return values.LandedDataValue;
				case KerbalismSituations.SrfSplashed: return values.SplashedDataValue;
				case KerbalismSituations.FlyingLow: return values.FlyingLowDataValue;
				case KerbalismSituations.FlyingHigh: return values.FlyingHighDataValue;
				case KerbalismSituations.InSpaceLow: return values.InSpaceLowDataValue;
				case KerbalismSituations.InSpaceHigh: return values.FlyingHighDataValue;
					
				case KerbalismSituations.InnerBelt:
				case KerbalismSituations.OuterBelt:
					return 1.3f * Math.Max(values.InSpaceHighDataValue, values.InSpaceLowDataValue);

				case KerbalismSituations.Reentry: return 1.5f * values.FlyingHighDataValue;
				case KerbalismSituations.Magnetosphere: return 1.1f * values.FlyingHighDataValue;
				case KerbalismSituations.Interstellar: return 15f * values.InSpaceHighDataValue;
			}

			Lib.Log("Science: invalid/unknown situation " + sit.ToString());
			return 0;
		}

		internal KerbalismSituations StockSituation(KerbalismSituations s)
		{
			if (s < KerbalismSituations.InnerBelt)
				return s;

			if (s == KerbalismSituations.Reentry)
				return KerbalismSituations.FlyingHigh;

			return KerbalismSituations.InSpaceHigh;
		}
	}
}
