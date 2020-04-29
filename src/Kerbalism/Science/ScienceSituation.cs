using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KSP.Localization;

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
		Surface       = 11,
		Flying        = 12,
		Space         = 13,
		BodyGlobal    = 14
	}

	/// <summary>
	/// virtual biomes are fake biomes that can be used to generate subjects for specific situations.
	/// We use the "biome" part of the stock subjectid system because that
	/// doesn't currently cause any issue (as opposed to using a custom situation).
	/// The enum value must be less than byte.Maxvalue (255), used for "no biome" in Situation,
	/// but musn't collide with real CB biomes indexes that start at 0
	/// </summary>
	// Note : if you add some virtual biomes, remember to update the related methods in ScienceSituationUtils
	public enum VirtualBiome : byte
	{
		None               = 0,
		NoBiome            = byte.MaxValue, // if used, will be registered as the global, biome-agnostic situation 
		NorthernHemisphere = 254,
		SouthernHemisphere = 253,
		InnerBelt          = 252,
		OuterBelt          = 251,
		Magnetosphere      = 250,
		Interstellar       = 249,
		Reentry            = 248,
		Storm              = 247
	}

	public static class ScienceSituationUtils
	{
		public static bool IsAvailableForExperiment(this ScienceSituation situation, ExperimentInfo experiment)
		{
			return (experiment.SituationMask & situation.BitValue()) != 0;
		}

		public static bool IsBiomesRelevantForExperiment(this ScienceSituation situation, ExperimentInfo experiment)
		{
			return (experiment.BiomeMask & situation.BitValue()) != 0 || (experiment.VirtualBiomeMask & situation.BitValue()) != 0;
		}

		public static bool IsBodyBiomesRelevantForExperiment(this ScienceSituation situation, ExperimentInfo experiment)
		{
			return (experiment.BiomeMask & situation.BitValue()) != 0;
		}

		public static bool IsVirtualBiomesRelevantForExperiment(this ScienceSituation situation, ExperimentInfo experiment)
		{
			return (experiment.VirtualBiomeMask & situation.BitValue()) != 0;
		}

		public static bool IsAvailableOnBody(this ScienceSituation situation, CelestialBody body)
		{
			switch (situation)
			{
				case ScienceSituation.SrfLanded:
				case ScienceSituation.Surface:
					if (!body.hasSolidSurface) return false;
					break;
				case ScienceSituation.SrfSplashed:
					if (!body.ocean || !body.hasSolidSurface) return false;
					break;
				case ScienceSituation.FlyingLow:
				case ScienceSituation.FlyingHigh:
				case ScienceSituation.Flying:
					if (!body.atmosphere) return false;
					break;
				case ScienceSituation.None:
					return false;
			}
			return true;
		}

		public static bool IsAvailableOnBody(this VirtualBiome virtualBiome, CelestialBody body)
		{
			switch (virtualBiome)
			{
				case VirtualBiome.InnerBelt:
					if (!Radiation.Info(body).model.has_inner) return false;
					break;
				case VirtualBiome.OuterBelt:
					if (!Radiation.Info(body).model.has_outer) return false;
					break;
				case VirtualBiome.Magnetosphere:
					if (!Radiation.Info(body).model.has_pause) return false;
					break;
				case VirtualBiome.Interstellar:
					if (!Sim.IsStar(body)) return false;
					break;
				case VirtualBiome.Reentry:
					if (!body.atmosphere) return false;
					break;
			}
			return true;
		}

		public static float BodyMultiplier(this ScienceSituation situation, CelestialBody body)
		{
			float result = 0f;
			switch (situation)
			{
				case ScienceSituation.Surface:
				case ScienceSituation.SrfLanded:
					result = body.scienceValues.LandedDataValue; break;
				case ScienceSituation.SrfSplashed:
					result = body.scienceValues.SplashedDataValue; break;
				case ScienceSituation.FlyingLow:
					result = body.scienceValues.FlyingLowDataValue; break;
				case ScienceSituation.Flying:
				case ScienceSituation.FlyingHigh:
					result = body.scienceValues.FlyingHighDataValue; break;
				case ScienceSituation.BodyGlobal:
				case ScienceSituation.Space:
				case ScienceSituation.InSpaceLow:
					result = body.scienceValues.InSpaceLowDataValue; break;
				case ScienceSituation.InSpaceHigh:
					result = body.scienceValues.InSpaceHighDataValue; break;
			}

			if (result == 0f)
			{
				Lib.Log("Science: invalid/unknown situation " + situation.ToString(), Lib.LogLevel.Error);
				return 1f; // returning 0 will result in NaN values
			}
			return result;
		}

		public static uint BitValue(this ScienceSituation situation)
		{
			switch (situation)
			{
				case ScienceSituation.None:          return 0;
				case ScienceSituation.SrfLanded:     return 1;     // 1 << 0
				case ScienceSituation.SrfSplashed:   return 2;     // 1 << 1
				case ScienceSituation.FlyingLow:     return 4;     // 1 << 2
				case ScienceSituation.FlyingHigh:    return 8;     // 1 << 3
				case ScienceSituation.InSpaceLow:    return 16;    // 1 << 4
				case ScienceSituation.InSpaceHigh:   return 32;    // 1 << 5
				case ScienceSituation.Surface:       return 2048;  // 1 << 11
				case ScienceSituation.Flying:        return 4096;  // 1 << 12
				case ScienceSituation.Space:         return 8192;  // 1 << 13
				case ScienceSituation.BodyGlobal:    return 16384; // 1 << 14
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

		public static List<ScienceSituation> BitMaskToSituations(uint bitMask)
		{
			List<ScienceSituation> situations = new List<ScienceSituation>();
			foreach (ScienceSituation situation in validSituations)
				if ((bitMask & BitValue(situation)) != 0)
					situations.Add(situation);
			return situations;
		}

		public static string Title(this ScienceSituation situation)
		{
			switch (situation)
			{
				case ScienceSituation.None:          return Local.Situation_None;//"none"
				case ScienceSituation.SrfLanded:     return Local.Situation_Landed;//"landed"
				case ScienceSituation.SrfSplashed:   return Local.Situation_Splashed;//"splashed"
				case ScienceSituation.FlyingLow:     return Local.Situation_Flyinglow;//"flying low"
				case ScienceSituation.FlyingHigh:    return Local.Situation_Flyinghigh;//"flying high"
				case ScienceSituation.InSpaceLow:    return Local.Situation_Spacelow;//"space low"
				case ScienceSituation.InSpaceHigh:   return Local.Situation_SpaceHigh;//"space high"
				case ScienceSituation.Surface:       return Local.Situation_Surface;//"surface"
				case ScienceSituation.Flying:        return Local.Situation_Flying;//"flying"
				case ScienceSituation.Space:         return Local.Situation_Space;//"space"
				case ScienceSituation.BodyGlobal:    return Local.Situation_BodyGlobal;//"global"
				default:                             return Local.Situation_None;//"none"
			}
		}

		public static string Title(this VirtualBiome virtualBiome)
		{
			switch (virtualBiome)
			{
				case VirtualBiome.NoBiome:            return Local.Situation_NoBiome;//"global"
				case VirtualBiome.NorthernHemisphere: return Local.Situation_NorthernHemisphere;//"north hemisphere"
				case VirtualBiome.SouthernHemisphere: return Local.Situation_SouthernHemisphere;//"south hemisphere"
				case VirtualBiome.InnerBelt:          return Local.Situation_InnerBelt;//"inner belt"
				case VirtualBiome.OuterBelt:          return Local.Situation_OuterBelt;//"outer belt"
				case VirtualBiome.Magnetosphere:      return Local.Situation_Magnetosphere;//"magnetosphere"
				case VirtualBiome.Interstellar:       return Local.Situation_Interstellar;//"interstellar"
				case VirtualBiome.Reentry:            return Local.Situation_Reentry;//"reentry"
				case VirtualBiome.Storm:              return Local.Situation_Storm;//"solar storm"
				default:                              return Local.Situation_None;//"none"
			}
		}

		public static string Serialize(this ScienceSituation situation)
		{
			switch (situation)
			{
				case ScienceSituation.SrfLanded:     return "SrfLanded";
				case ScienceSituation.SrfSplashed:   return "SrfSplashed";
				case ScienceSituation.FlyingLow:     return "FlyingLow";
				case ScienceSituation.FlyingHigh:    return "FlyingHigh";
				case ScienceSituation.InSpaceLow:    return "InSpaceLow";
				case ScienceSituation.InSpaceHigh:   return "InSpaceHigh";
				case ScienceSituation.Surface:       return "Surface" ;
				case ScienceSituation.Flying:        return "Flying";
				case ScienceSituation.Space:         return "Space";
				case ScienceSituation.BodyGlobal:    return "BodyGlobal";
				default:                             return "None";
			}
		}

		public static string Serialize(this VirtualBiome virtualBiome)
		{
			switch (virtualBiome)
			{
				case VirtualBiome.NoBiome:            return "NoBiome";
				case VirtualBiome.NorthernHemisphere: return "NorthernHemisphere";
				case VirtualBiome.SouthernHemisphere: return "SouthernHemisphere";
				case VirtualBiome.InnerBelt:          return "InnerBelt";
				case VirtualBiome.OuterBelt:          return "OuterBelt";
				case VirtualBiome.Magnetosphere:      return "Magnetosphere";
				case VirtualBiome.Interstellar:       return "Interstellar";
				case VirtualBiome.Reentry:            return "Reentry";
				case VirtualBiome.Storm:              return "Storm";
				default:                              return "None";
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
				case "Surface":	      return ScienceSituation.Surface;
				case "Flying":        return ScienceSituation.Flying;
				case "Space":         return ScienceSituation.Space;
				case "BodyGlobal":    return ScienceSituation.BodyGlobal;
				default:              return ScienceSituation.None;
			}
		}

		public static VirtualBiome VirtualBiomeDeserialize(string virtualBiome)
		{
			switch (virtualBiome)
			{
				case "NoBiome":            return VirtualBiome.NoBiome;
				case "NorthernHemisphere": return VirtualBiome.NorthernHemisphere;
				case "SouthernHemisphere": return VirtualBiome.SouthernHemisphere;
				case "InnerBelt":          return VirtualBiome.InnerBelt;
				case "OuterBelt":          return VirtualBiome.OuterBelt;
				case "Magnetosphere":      return VirtualBiome.Magnetosphere;
				case "Interstellar":       return VirtualBiome.Interstellar;
				case "Reentry":            return VirtualBiome.Reentry;
				case "Storm":              return VirtualBiome.Storm;
				default:                   return VirtualBiome.None;
			}
		}

		/// <summary> get the stock ExperimentSituation for our ScienceSituation value </summary>
		// Note: any modification in this should be reflected in ValidateSituationBitMask()
		public static ScienceSituation ToValidStockSituation(this ScienceSituation situation)
		{
			switch (situation)
			{
				case ScienceSituation.SrfLanded:   return ScienceSituation.SrfLanded;
				case ScienceSituation.SrfSplashed: return ScienceSituation.SrfSplashed;
				case ScienceSituation.FlyingLow:   return ScienceSituation.FlyingLow;
				case ScienceSituation.FlyingHigh:  return ScienceSituation.FlyingHigh;
				case ScienceSituation.InSpaceLow:  return ScienceSituation.InSpaceLow;
				case ScienceSituation.InSpaceHigh: return ScienceSituation.InSpaceHigh;
				case ScienceSituation.Surface:     return ScienceSituation.SrfLanded;
				case ScienceSituation.Flying:      return ScienceSituation.FlyingHigh;
				case ScienceSituation.Space:
				case ScienceSituation.BodyGlobal:
				default:                           return ScienceSituation.InSpaceLow;
			}
		}

		/// <summary> validate and convert our bitmasks to their stock equivalent</summary>
		// Note: any modification in this should be reflected in ToStockSituation()
		public static bool ValidateSituationBitMask(ref uint situationMask, uint biomeMask, out uint stockSituationMask, out uint stockBiomeMask, out string errorMessage)
		{
			errorMessage = string.Empty;

			stockSituationMask = GetStockBitMask(situationMask);
			stockBiomeMask = GetStockBitMask(biomeMask);

			if (MaskHasSituation(situationMask, ScienceSituation.Surface))
			{
				if (MaskHasSituation(situationMask, ScienceSituation.SrfSplashed) || MaskHasSituation(situationMask, ScienceSituation.SrfLanded))
				{
					errorMessage += "Experiment situation definition error : `Surface` can't be combined with `SrfSplashed` or `SrfLanded`\n";
					SetSituationBitInMask(ref situationMask, ScienceSituation.SrfSplashed, false);
					SetSituationBitInMask(ref situationMask, ScienceSituation.SrfLanded, false);
				}
				// make sure the stock masks don't have SrfSplashed
				SetSituationBitInMask(ref stockSituationMask, ScienceSituation.SrfSplashed, false);
				SetSituationBitInMask(ref stockBiomeMask, ScienceSituation.SrfSplashed, false);
				// patch Surface as SrfLanded in the stock masks
				SetSituationBitInMask(ref stockSituationMask, ScienceSituation.SrfLanded, true);
				SetSituationBitInMask(ref stockBiomeMask, ScienceSituation.SrfLanded, MaskHasSituation(biomeMask, ScienceSituation.Surface));
			}

			if (MaskHasSituation(situationMask, ScienceSituation.Flying))
			{
				if (MaskHasSituation(situationMask, ScienceSituation.FlyingHigh) || MaskHasSituation(situationMask, ScienceSituation.FlyingLow))
				{
					errorMessage += "Experiment situation definition error : `Flying` can't be combined with `FlyingHigh` or `FlyingLow`\n";
					SetSituationBitInMask(ref situationMask, ScienceSituation.FlyingHigh, false);
					SetSituationBitInMask(ref situationMask, ScienceSituation.FlyingLow, false);
				}
				// make sure the stock masks don't have FlyingLow
				SetSituationBitInMask(ref stockSituationMask, ScienceSituation.FlyingLow, false);
				SetSituationBitInMask(ref stockBiomeMask, ScienceSituation.FlyingLow, false);
				// patch Flying as FlyingHigh in the stock masks
				SetSituationBitInMask(ref stockSituationMask, ScienceSituation.FlyingHigh, true);
				SetSituationBitInMask(ref stockBiomeMask, ScienceSituation.FlyingHigh, MaskHasSituation(biomeMask, ScienceSituation.Flying));
			}

			if (MaskHasSituation(situationMask, ScienceSituation.Space))
			{
				if (MaskHasSituation(situationMask, ScienceSituation.InSpaceHigh) || MaskHasSituation(situationMask, ScienceSituation.InSpaceLow))
				{
					errorMessage += "Experiment situation definition error : `Space` can't be combined with `InSpaceHigh` or `InSpaceLow`\n";
					SetSituationBitInMask(ref situationMask, ScienceSituation.InSpaceHigh, false);
					SetSituationBitInMask(ref situationMask, ScienceSituation.InSpaceLow, false);
				}
				// make sure the stock masks don't have InSpaceHigh
				SetSituationBitInMask(ref stockSituationMask, ScienceSituation.InSpaceHigh, false);
				SetSituationBitInMask(ref stockBiomeMask, ScienceSituation.InSpaceHigh, false);
				// patch Space as InSpaceLow in the stock masks
				SetSituationBitInMask(ref stockSituationMask, ScienceSituation.InSpaceLow, true);
				SetSituationBitInMask(ref stockBiomeMask, ScienceSituation.InSpaceLow, MaskHasSituation(biomeMask, ScienceSituation.Space));
			}

			if (MaskHasSituation(situationMask, ScienceSituation.BodyGlobal))
			{
				if (situationMask != ScienceSituation.BodyGlobal.BitValue())
				{
					errorMessage += "Experiment situation definition error : `BodyGlobal` can't be combined with another situation\n";
					situationMask = ScienceSituation.BodyGlobal.BitValue();
				}
				stockSituationMask = ScienceSituation.InSpaceLow.BitValue();
				if (MaskHasSituation(biomeMask, ScienceSituation.BodyGlobal))
					stockBiomeMask = ScienceSituation.InSpaceLow.BitValue();
				else
					stockBiomeMask = 0;
			}

			return errorMessage == string.Empty;
		}

		public static uint GetStockBitMask(uint bitMask)
		{
			uint stockBitMask = 0;
			foreach (ScienceSituation situation in BitMaskToSituations(bitMask))
			{
				switch (situation)
				{
					case ScienceSituation.SrfLanded:
					case ScienceSituation.SrfSplashed:
					case ScienceSituation.FlyingLow:
					case ScienceSituation.FlyingHigh:
					case ScienceSituation.InSpaceLow:
					case ScienceSituation.InSpaceHigh:
						stockBitMask += situation.BitValue();
						break;
				}
			}
			return stockBitMask;
		}

		private static void SetSituationBitInMask(ref uint mask, ScienceSituation situation, bool value)
		{
			if (value)
				mask = (uint)((int)mask | (1 << (int)situation));
			else
				mask = (uint)((int)mask & ~(1 << (int)situation));
		}

		private static bool MaskHasSituation(uint mask, ScienceSituation situation)
		{
			return (mask & situation.BitValue()) != 0;
		}

		public static readonly ScienceSituation[] validSituations = new ScienceSituation[]
		{
			ScienceSituation.SrfLanded,
			ScienceSituation.SrfSplashed,
			ScienceSituation.FlyingLow,
			ScienceSituation.FlyingHigh,
			ScienceSituation.InSpaceLow,
			ScienceSituation.InSpaceHigh,
			ScienceSituation.Surface,
			ScienceSituation.Flying,
			ScienceSituation.Space,
			ScienceSituation.BodyGlobal
		};

		public static readonly VirtualBiome[] validVirtualBiomes = new VirtualBiome[]
		{
			VirtualBiome.NorthernHemisphere,
			VirtualBiome.SouthernHemisphere,
			VirtualBiome.InnerBelt,
			VirtualBiome.OuterBelt,
			VirtualBiome.Magnetosphere,
			VirtualBiome.Interstellar,
			VirtualBiome.Reentry,
			VirtualBiome.Storm
		};

		public const int minVirtualBiome = 247;

		public static readonly string[] validSituationsStrings = new string[]
		{
			"SrfLanded",
			"SrfSplashed",
			"FlyingLow",
			"FlyingHigh",
			"InSpaceLow",
			"InSpaceHigh",
			"Surface",
			"Flying",
			"Space",
			"BodyGlobal"
		};
	}
}
