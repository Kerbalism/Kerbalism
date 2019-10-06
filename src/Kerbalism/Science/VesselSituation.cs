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
	public class VesselSituation : IEquatable<VesselSituation>
	{
		public const int agnosticBiomeIndex = byte.MaxValue;

		public CelestialBody Body { get; private set; }
		public ScienceSituation ScienceSituation { get; private set; }
		public CBAttributeMapSO.MapAttribute Biome { get; private set; }

		/// <summary>
		/// Store the situation fields as a unique 32-bit array (int) :
		/// - 16 first bits : body index (ushort)
		/// - 8 next bits : vessel situation (byte)
		/// - 8 last bits : biome index (byte)
		/// </summary>
		public int Id { get; private set; }

		public VesselSituation(int bodyIndex, ScienceSituation situation, int biomeIndex = -1)
		{
			ScienceSituation = situation;
			Body = FlightGlobals.Bodies[bodyIndex];

			if (biomeIndex >= 0 && Body.BiomeMap != null)
				Biome = Body.BiomeMap.Attributes[biomeIndex];
			else
				Biome = null;

			Id = FieldsToId(bodyIndex, situation, biomeIndex);
		}

		/// <summary> garanteed to be unique for each body/situation/biome combination</summary>
		public override int GetHashCode() => Id;

		public bool Equals(VesselSituation other) => other != null ? Id == other.Id : false;

		public static bool operator == (VesselSituation a, VesselSituation b) { return a.Equals(b); }
		public static bool operator != (VesselSituation a, VesselSituation b) { return !a.Equals(b); }

		public override bool Equals(object obj)
		{
			if (obj == null)
				return false;

			VesselSituation objAs = obj as VesselSituation;

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

		public static bool IsIdValid(int id, bool logErrors = false)
		{
			int bodyIndex;
			int situation;
			int biomeIndex;

			IdToFields(id, out bodyIndex, out situation, out biomeIndex);

			if (FlightGlobals.Bodies.Count > bodyIndex + 1)
			{
				if (logErrors) Lib.Log("VesselSituation ID '" + id + "' is not valid : body index '" + bodyIndex + "' doesn't exists");
				return false;
			}

			if (!Enum.IsDefined(typeof(ScienceSituation), situation))
			{
				if (logErrors) Lib.Log("VesselSituation ID '" + id + "' is not valid : situation '" + situation + "' doesn't exists");
				return false;
			}

			if (biomeIndex >= 0 && FlightGlobals.Bodies[bodyIndex].BiomeMap == null)
			{
				if (logErrors) Lib.Log("VesselSituation ID '" + id + "' is not valid : biome index '" + biomeIndex + "' is defined but body '" + FlightGlobals.Bodies[bodyIndex].name + "' has no biomes");
				return false;
			}

			if (FlightGlobals.Bodies[bodyIndex].BiomeMap.Attributes.Length > biomeIndex + 1)
			{
				if (logErrors) Lib.Log("VesselSituation ID '" + id + "' is not valid : biome index '" + biomeIndex + "' doesn't exists on body '" + FlightGlobals.Bodies[bodyIndex].name + "'");
				return false;
			}

			return true;
		}

		public int GetBiomeAgnosticId()
		{
			return Id | (agnosticBiomeIndex << 24);
		}

		public VesselSituation GetBiomeAgnosticSituation()
		{
			return new VesselSituation((ushort)Id, (ScienceSituation)(byte)(Id >> 16));
		}

		public string Title =>
			Biome != null
			? Lib.BuildString(BodyTitle, " ", ScienceSituationTitle, " ", BiomeTitle)
			: Lib.BuildString(BodyTitle, " ", ScienceSituationTitle);

		public string BodyTitle => Body.name;
		public string BiomeTitle => Biome != null ? Biome.displayname : string.Empty;
		public string ScienceSituationTitle => ScienceSituation.Title();

		public string BodyName => Body.name;
		public string BiomeName => Biome != null ? Biome.name.Replace(" ", string.Empty) : string.Empty;
		public string ScienceSituationName => ScienceSituation.Serialize();

		public override string ToString()
		{
			return Lib.BuildString(BodyName, ScienceSituationName, BiomeName);
		}

		public ExperimentSituations StockSituation => ScienceSituation.ToStockSituation();

		public float SituationMultiplier => ScienceSituation.BodyMultiplier(Body);

		public string ExperimentSituationName(ExperimentInfo expInfo)
		{
			if (ScienceSituation.IsBiomesRelevantForExperiment(expInfo))
				return Lib.BuildString(BodyTitle, " ", ScienceSituationTitle, " ", BiomeTitle);
			else
				return Lib.BuildString(BodyTitle, " ", ScienceSituationTitle);
		}

		public string ExperimentSituationId(ExperimentInfo expInfo)
		{
			if (ScienceSituation.IsBiomesRelevantForExperiment(expInfo))
				return Lib.BuildString(BodyName, ScienceSituationName, BiomeName);
			else
				return Lib.BuildString(BodyName, ScienceSituationName);
		}

		public bool AtmosphericFlight()
		{
			switch (ScienceSituation)
			{
				case ScienceSituation.FlyingLow:
				case ScienceSituation.FlyingHigh:
				case ScienceSituation.Reentry:
					return true;
				default:
					return false;
			}
		}

		public static VesselSituation GetExperimentSituation(Vessel vessel)
		{
			return new VesselSituation(
				vessel.mainBody.flightGlobalsIndex,
				ScienceSituationUtils.GetSituation(vessel),
				GetBiomeIndex(vessel));
		}

		private static int GetBiomeIndex(Vessel v)
		{
			int currentBiomeIndex = -1;

			if (v.mainBody.BiomeMap != null)
			{
				// ScienceUtil.GetExperimentBiome
				CBAttributeMapSO biomeMap = v.mainBody.BiomeMap;
				double lat = v.latitude * 0.01745329238474369;
				double lon = v.longitude * 0.01745329238474369;

				// CBAttributeMapSO.GetAtt
				lon -= Math.PI / 2.0;
				lon = UtilMath.WrapAround(lon, 0.0, Math.PI * 2.0);
				double y = lat * (1.0 / Math.PI) + 0.5;
				double x = 1.0 - lon * 0.15915494309189535;
				Color pixelColor = biomeMap.GetPixelColor(x, y);

				float currentSqrMag = float.MaxValue;
				for (int i = 0; i < biomeMap.Attributes.Length; i++)
				{
					if (!biomeMap.Attributes[i].notNear)
					{
						float sqrMag = RGBColorSqrMag(pixelColor, biomeMap.Attributes[i].mapColor);
						if (sqrMag < currentSqrMag && (biomeMap.nonExactThreshold == -1f || sqrMag < biomeMap.nonExactThreshold))
						{
							currentBiomeIndex = i;
							currentSqrMag = sqrMag;
						}
					}
				}
			}

			return currentBiomeIndex;
		}

		private static float RGBColorSqrMag(Color colA, Color colB)
		{
			float num = colA.r - colB.r;
			float num2 = num * num;
			num = colA.g - colB.g;
			num2 += num * num;
			num = colA.b - colB.b;
			return num2 + num * num;
		}
	}
}
