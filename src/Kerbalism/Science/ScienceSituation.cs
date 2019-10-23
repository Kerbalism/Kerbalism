using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KERBALISM
{
	public enum ScienceSituation : byte
	{
		None          = byte.MaxValue,
		// stock situations
		SrfLanded     = 0,
		SrfSplashed   = 1,
		FlyingLow     = 2,
		FlyingHigh    = 3,
		InSpaceLow    = 4,
		InSpaceHigh   = 5,
		// Kerbalism extensions
		InnerBelt     = 6,
		OuterBelt     = 7,
		Magnetosphere = 8,
		Reentry       = 9,
		Interstellar  = 10
	}

	public static class ScienceSituationUtils
	{
		/// <summary>
		/// The KSP stock function has the nasty habit of returning, on occasion,
		/// situations that should not exist (flying high/low with bodies that
		/// don't have atmosphere), so we have to force the situations a bit.
		/// </summary>
		public static ScienceSituation GetSituation(Vessel vessel)
		{
			CelestialBody body = vessel.mainBody;

			if (body.atmosphere // only on bodies with atmosphere
				&& vessel.verticalSpeed < 100 // descending
				&& vessel.altitude < body.atmosphereDepth // in the atmosphere
				&& vessel.altitude > body.scienceValues.flyingAltitudeThreshold // above the flying high treshold
				&& (double.IsNaN(vessel.orbit.ApA) || vessel.orbit.ApA > body.atmosphereDepth) // apoapsis above atmosphere or NaN
				&& vessel.srfSpeed > vessel.speedOfSound * 5) // mach 5
			{
				return ScienceSituation.Reentry;
			}

			if (Lib.Landed(vessel))
			{
				switch (vessel.situation)
				{
					case Vessel.Situations.PRELAUNCH:
					case Vessel.Situations.LANDED: return ScienceSituation.SrfLanded;
					case Vessel.Situations.SPLASHED: return ScienceSituation.SrfSplashed;
				}
			}

			if (vessel.altitude > body.scienceValues.spaceAltitudeThreshold)
				return ScienceSituation.InSpaceHigh;

			if (vessel.altitude > body.atmosphereDepth)
				return ScienceSituation.InSpaceLow;

			if (body.atmosphere && vessel.altitude > body.scienceValues.flyingAltitudeThreshold)
				return ScienceSituation.FlyingHigh;

			return ScienceSituation.FlyingLow;
		}

		public static float BodyMultiplier(this ScienceSituation situation, CelestialBody body)
		{
			float result = 0;
			switch (situation)
			{
				case ScienceSituation.SrfLanded:   result = body.scienceValues.LandedDataValue; break;
				case ScienceSituation.SrfSplashed: result = body.scienceValues.SplashedDataValue; break;
				case ScienceSituation.FlyingLow:   result = body.scienceValues.FlyingLowDataValue; break;
				case ScienceSituation.FlyingHigh:  result = body.scienceValues.FlyingHighDataValue; break;
				case ScienceSituation.InSpaceLow:  result = body.scienceValues.InSpaceLowDataValue; break;
				case ScienceSituation.InSpaceHigh: result = body.scienceValues.InSpaceHighDataValue; break;

				case ScienceSituation.InnerBelt:
				case ScienceSituation.OuterBelt:
					result = 1.3f * Math.Max(body.scienceValues.InSpaceHighDataValue, body.scienceValues.InSpaceLowDataValue);
					break;

				case ScienceSituation.Reentry:       result = 1.5f * body.scienceValues.FlyingHighDataValue; break;
				case ScienceSituation.Magnetosphere: result = 1.1f * body.scienceValues.FlyingHighDataValue; break;
				case ScienceSituation.Interstellar:  result = 15f * body.scienceValues.InSpaceHighDataValue; break;
			}

			if(result == 0)
			{
				Lib.Log("Science: invalid/unknown situation " + situation.ToString());
				result = 1.0f; // returning 0 will result in NaN values
			}
			return result;
		}

		public static uint BitValue(this ScienceSituation situation)
		{
			switch (situation)
			{
				case ScienceSituation.None:          return 0;
				case ScienceSituation.SrfLanded:     return 1;
				case ScienceSituation.SrfSplashed:   return 2;
				case ScienceSituation.FlyingLow:     return 4;
				case ScienceSituation.FlyingHigh:    return 8;
				case ScienceSituation.InSpaceLow:    return 16;
				case ScienceSituation.InSpaceHigh:   return 32;
				case ScienceSituation.InnerBelt:     return 64;
				case ScienceSituation.OuterBelt:     return 128;
				case ScienceSituation.Magnetosphere: return 256;
				case ScienceSituation.Reentry:       return 512;
				case ScienceSituation.Interstellar:  return 1024;
				default:                             return 0;
			}
		}

		public static uint SituationsToBitMask(List<ScienceSituation> scienceSituations)
		{
			uint bitMask = 0;
			foreach (ScienceSituation situation in scienceSituations)
				bitMask += situation.BitValue();

			return bitMask;
		}

		public static string Title(this ScienceSituation situation)
		{
			switch (situation)
			{
				case ScienceSituation.None:          return "none";
				case ScienceSituation.SrfLanded:     return "landed";
				case ScienceSituation.SrfSplashed:   return "splashed";
				case ScienceSituation.FlyingLow:     return "flying low";
				case ScienceSituation.FlyingHigh:    return "flying high";
				case ScienceSituation.InSpaceLow:    return "space low";
				case ScienceSituation.InSpaceHigh:   return "space high";
				case ScienceSituation.InnerBelt:     return "inner belt";
				case ScienceSituation.OuterBelt:     return "outer belt";
				case ScienceSituation.Magnetosphere: return "magnetosphere";
				case ScienceSituation.Reentry:       return "reentry";
				case ScienceSituation.Interstellar:  return "interstellar";
				default:                             return "none";
			}
		}

		public static string Serialize(this ScienceSituation situation)
		{
			switch (situation)
			{
				case ScienceSituation.None:          return "None";
				case ScienceSituation.SrfLanded:     return "SrfLanded";
				case ScienceSituation.SrfSplashed:   return "SrfSplashed";
				case ScienceSituation.FlyingLow:     return "FlyingLow";
				case ScienceSituation.FlyingHigh:    return "FlyingHigh";
				case ScienceSituation.InSpaceLow:    return "InSpaceLow";
				case ScienceSituation.InSpaceHigh:   return "InSpaceHigh";
				case ScienceSituation.InnerBelt:     return "InnerBelt";
				case ScienceSituation.OuterBelt:     return "OuterBelt";
				case ScienceSituation.Magnetosphere: return "Magnetosphere";
				case ScienceSituation.Reentry:       return "Reentry";
				case ScienceSituation.Interstellar:  return "Interstellar";
				default:                             return "None";
			}
		}

		public static ScienceSituation ScienceSituationDeserialize(string situation)
		{
			switch (situation)
			{
				case "SrfLanded":     return ScienceSituation.SrfLanded;
				case "SrfSplashed":   return ScienceSituation.SrfSplashed;
				case "FlyingLow":     return ScienceSituation.FlyingLow;
				case "FlyingHigh":    return ScienceSituation.FlyingHigh;
				case "InSpaceLow":    return ScienceSituation.InSpaceLow;
				case "InSpaceHigh":   return ScienceSituation.InSpaceHigh;
				case "InnerBelt":     return ScienceSituation.InnerBelt;
				case "OuterBelt":     return ScienceSituation.OuterBelt;
				case "Magnetosphere": return ScienceSituation.Magnetosphere;
				case "Reentry":       return ScienceSituation.Reentry;
				case "Interstellar":  return ScienceSituation.Interstellar;
				default:              return ScienceSituation.None;
			}
		}

		public static ExperimentSituations ToStockSituation(this ScienceSituation situation)
		{
			switch (situation)
			{
				case ScienceSituation.SrfLanded:    return ExperimentSituations.SrfLanded;
				case ScienceSituation.SrfSplashed:  return ExperimentSituations.SrfSplashed;
				case ScienceSituation.FlyingLow:    return ExperimentSituations.FlyingLow;
				case ScienceSituation.FlyingHigh:   return ExperimentSituations.FlyingHigh;
				case ScienceSituation.InSpaceLow:   return ExperimentSituations.InSpaceLow;
				case ScienceSituation.InSpaceHigh:  return ExperimentSituations.InSpaceHigh;
				case ScienceSituation.Reentry:      return ExperimentSituations.FlyingHigh;
				case ScienceSituation.InnerBelt:    return ExperimentSituations.InSpaceLow;
				case ScienceSituation.OuterBelt:
				case ScienceSituation.Magnetosphere:
				case ScienceSituation.Interstellar: return ExperimentSituations.InSpaceHigh;
				default:                            return ExperimentSituations.InSpaceLow;
			}
		}

		public static bool IsAvailableForExperiment(this ScienceSituation situation, ExperimentInfo experiment)
		{
			return (experiment.SituationMask & situation.BitValue()) != 0;
		}

		public static bool IsBiomesRelevantForExperiment(this ScienceSituation situation, ExperimentInfo experiment)
		{
			return (experiment.BiomeMask & situation.BitValue()) != 0;
		}

		public static bool IsAvailableOnBody(this ScienceSituation situation, CelestialBody body)
		{
			switch (situation)
			{
				case ScienceSituation.SrfLanded:
					if (!body.hasSolidSurface) return false;
					break;
				case ScienceSituation.SrfSplashed:
					if (!body.ocean || !body.hasSolidSurface) return false;
					break;
				case ScienceSituation.FlyingLow:
				case ScienceSituation.FlyingHigh:
				case ScienceSituation.Reentry:
					if (!body.atmosphere) return false;
					break;
				case ScienceSituation.InSpaceLow:
					break;
				case ScienceSituation.InSpaceHigh:
					break;
				case ScienceSituation.InnerBelt:
				case ScienceSituation.OuterBelt:
				case ScienceSituation.Magnetosphere:
					if (Lib.IsSun(body)) return false;
					break;
				case ScienceSituation.Interstellar:
					if (!Lib.IsSun(body)) return false;
					break;
				case ScienceSituation.None:
					return false;
			}

			return true;
		}


		public static readonly ScienceSituation[] validSituations = new ScienceSituation[]
		{
			ScienceSituation.SrfLanded,
			ScienceSituation.SrfSplashed,
			ScienceSituation.FlyingLow,
			ScienceSituation.FlyingHigh,
			ScienceSituation.InSpaceLow,
			ScienceSituation.InSpaceHigh,
			ScienceSituation.InnerBelt,
			ScienceSituation.OuterBelt,
			ScienceSituation.Magnetosphere,
			ScienceSituation.Reentry,
			ScienceSituation.Interstellar
		};

		public static readonly string[] validSituationsStrings = new string[]
		{
			"SrfLanded",
			"SrfSplashed",
			"FlyingLow",
			"FlyingHigh",
			"InSpaceLow",
			"InSpaceHigh",
			"InnerBelt",
			"OuterBelt",
			"Magnetosphere",
			"Reentry",
			"Interstellar"
		};
	}
}
