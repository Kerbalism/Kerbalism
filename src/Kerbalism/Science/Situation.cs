using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KERBALISM
{
	/// <summary>
	/// Stores a body, science situation and biome combination, intended as a replacement for
	/// the second part (after the "@") of the stock string-based subject id.
	/// generate an unique int number to be used as a key in one of the scienceDB dictionaries
	/// </summary>
	public class Situation : IEquatable<Situation>
	{
		public const int agnosticBiomeIndex = byte.MaxValue;

		public CelestialBody Body { get; private set; }
		public ScienceSituation ScienceSituation { get; private set; }
		public CBAttributeMapSO.MapAttribute Biome { get; private set; }
		public VirtualBiome VirtualBiome { get; private set; }

		/// <summary>
		/// Store the situation fields as a unique 32-bit array (int) :
		/// - 16 first bits : body index (ushort)
		/// - 8 next bits : vessel situation (byte)
		/// - 8 last bits : biome index (byte)
		/// </summary>
		public int Id { get; private set; }

		public Situation(int bodyIndex, ScienceSituation situation, int biomeIndex = -1)
		{
			ScienceSituation = situation;
			Body = FlightGlobals.Bodies[bodyIndex];

			if (biomeIndex >= 0)
			{
				if (biomeIndex >= ScienceSituationUtils.minVirtualBiome)
				{
					VirtualBiome = (VirtualBiome)biomeIndex;
				}
				else if (Body.BiomeMap != null)
				{
					Biome = Body.BiomeMap.Attributes[biomeIndex];
				}
			}

			Id = FieldsToId(bodyIndex, situation, biomeIndex);
		}

		/// <summary> garanteed to be unique for each body/situation/biome combination</summary>
		public override int GetHashCode() => Id;

		public bool Equals(Situation other) => other != null ? Id == other.Id : false;

		public static bool operator == (Situation a, Situation b) { return a.Equals(b); }
		public static bool operator != (Situation a, Situation b) { return !a.Equals(b); }

		public override bool Equals(object obj)
		{
			if (obj == null)
				return false;

			Situation objAs = obj as Situation;

			if (objAs == null)
				return false;

			return Equals(objAs);
		}

		public static int FieldsToId(int bodyIndex, ScienceSituation situation, int biomeIndex = -1)
		{
			if (biomeIndex < 0)
				biomeIndex = agnosticBiomeIndex;

			return ((byte)biomeIndex << 8 | (byte)situation) << 16 | (ushort)bodyIndex;
		}

		public static void IdToFields(int id, out int bodyIndex, out int situation, out int biomeIndex)
		{
			biomeIndex = (byte)(id >> 24);
			situation = (byte)(id >> 16);
			bodyIndex = (ushort)id;
			if (biomeIndex == agnosticBiomeIndex) biomeIndex = -1;
		}

		public static int GetBiomeAgnosticIdForExperiment(int situationId, ExperimentInfo expInfo)
		{
			ScienceSituation sit = (ScienceSituation)(byte)(situationId >> 16);
			if (!sit.IsBiomesRelevantForExperiment(expInfo))
			{
				return situationId | (agnosticBiomeIndex << 24);
			}
			return situationId;
		}

		public int GetBiomeAgnosticId()
		{
			return Id | (agnosticBiomeIndex << 24);
		}

		public Situation GetBiomeAgnosticSituation()
		{
			return new Situation((ushort)Id, (ScienceSituation)(byte)(Id >> 16));
		}

		public string Title =>
			Biome != null
			? Lib.BuildString(BodyTitle, " ", ScienceSituationTitle, " ", BiomeTitle)
			: Lib.BuildString(BodyTitle, " ", ScienceSituationTitle);

		public string BodyTitle => Body.displayName;
		public string BiomeTitle => Biome != null ? Biome.displayname : VirtualBiome != VirtualBiome.None ? VirtualBiome.Title() : string.Empty;
		public string ScienceSituationTitle => ScienceSituation.Title();

		public string BodyName => Body.name;
		public string BiomeName => Biome != null ? Biome.name.Replace(" ", string.Empty) : VirtualBiome != VirtualBiome.None ? VirtualBiome.Serialize() : string.Empty;
		public string ScienceSituationName => ScienceSituation.Serialize();
		public string StockScienceSituationName => ScienceSituation.ToValidStockSituation().Serialize();

		public override string ToString()
		{
			return Lib.BuildString(BodyName, ScienceSituationName, BiomeName);
		}

		public double SituationMultiplier => ScienceSituation.BodyMultiplier(Body);

		public string GetTitleForExperiment(ExperimentInfo expInfo)
		{
			if (ScienceSituation.IsBiomesRelevantForExperiment(expInfo))
				return Lib.BuildString(BodyTitle, " ", ScienceSituationTitle, " ", BiomeTitle);
			else
				return Lib.BuildString(BodyTitle, " ", ScienceSituationTitle);
		}

		public string GetStockIdForExperiment(ExperimentInfo expInfo)
		{
			if (ScienceSituation.IsBiomesRelevantForExperiment(expInfo))
				return Lib.BuildString(BodyName, StockScienceSituationName, BiomeName);
			else
				return Lib.BuildString(BodyName, StockScienceSituationName);
		}

		public bool AtmosphericFlight()
		{
			switch (ScienceSituation)
			{
				case ScienceSituation.FlyingLow:
				case ScienceSituation.FlyingHigh:
				case ScienceSituation.Flying:
					return true;
				default:
					return false;
			}
		}
	}
}
