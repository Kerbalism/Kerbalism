using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KERBALISM
{
	public class VesselSituations
	{
		private VesselData vd;
		public CelestialBody body { get; private set; }
		public int biomeIndex { get; private set; }
		private CBAttributeMapSO.MapAttribute biome;
		public List<ScienceSituation> situations { get; private set; } = new List<ScienceSituation>();
		public List<VirtualBiome> virtualBiomes { get; private set; } = new List<VirtualBiome>();

		public string BodyTitle => Lib.BodyDisplayName(body);
		public string BiomeTitle => biome != null ? biome.displayname : string.Empty;

		public string BodyName => body.name;
		public string BiomeName => biome != null ? biome.name.Replace(" ", string.Empty) : string.Empty;

		public string FirstSituationTitle =>
			biome != null
			? Lib.BuildString(BodyTitle, " ", situations[0].Title(), " ", BiomeTitle)
			: Lib.BuildString(BodyTitle, " ", situations[0].Title());

		public Situation FirstSituation => new Situation(body.flightGlobalsIndex, situations[0], biomeIndex);

		public string[] SituationsTitle
		{
			get
			{
				string[] situationsStr = new string[situations.Count];
				for (int i = 0; i < situations.Count; i++)
					situationsStr[i] = situations[i].Title();
				return situationsStr;
			}
		}

		public VesselSituations(VesselData vd)
		{
			this.vd = vd;
		}

		/// <summary> Require EnvLanded, EnvInnerBelt and EnvOuterBelt to evaluated first </summary>
		public void Update()
		{
			body = vd.Vessel.mainBody;
			GetSituationsAndVirtualBiomes();
			biomeIndex = GetBiomeIndex(vd.Vessel);
			if (biomeIndex >= 0)
				biome = body.BiomeMap.Attributes[biomeIndex];
			else
				biome = null;
		}

		public Situation GetExperimentSituation(ExperimentInfo expInfo)
		{
			ScienceSituation expSituation = ScienceSituation.None;

			foreach (ScienceSituation situation in situations)
			{
				if (situation.IsAvailableForExperiment(expInfo))
				{
					expSituation = situation;
					break;
				}
			}

			int expBiomeIndex = biomeIndex;
			if (expSituation.IsVirtualBiomesRelevantForExperiment(expInfo))
			{
				foreach (VirtualBiome virtualBiome in virtualBiomes)
				{
					if (expInfo.VirtualBiomes.Contains(virtualBiome))
					{
						expBiomeIndex = (int)virtualBiome;
						break;
					}
				}
			}

			return new Situation(body.flightGlobalsIndex, expSituation, expBiomeIndex);
		}

		/// <summary>
		/// Return a list of available situations and special biomes for the vessel.
		/// The method is made so the lists are ordered with specific situations first and global ones last,
		/// because experiments will use the first valid situation/biome found.
		/// </summary>
		private void GetSituationsAndVirtualBiomes()
		{
			situations.Clear();
			virtualBiomes.Clear();

			if (vd.EnvLanded)
			{
				switch (vd.Vessel.situation)
				{
					case Vessel.Situations.PRELAUNCH:
					case Vessel.Situations.LANDED: situations.Add(ScienceSituation.SrfLanded); break;
					case Vessel.Situations.SPLASHED: situations.Add(ScienceSituation.SrfSplashed); break;
				}
				situations.Add(ScienceSituation.Surface);
				situations.Add(ScienceSituation.BodyGlobal);

				if (vd.EnvStorm)
					virtualBiomes.Add(VirtualBiome.Storm);

				if ((vd.Vessel.latitude + 270.0) % 90.0 > 0.0)
					virtualBiomes.Add(VirtualBiome.NorthernHemisphere);
				else
					virtualBiomes.Add(VirtualBiome.SouthernHemisphere);

				virtualBiomes.Add(VirtualBiome.NoBiome);
				return;
			}

			if (body.atmosphere && vd.Vessel.altitude < body.atmosphereDepth)
			{
				if (vd.Vessel.altitude < body.scienceValues.flyingAltitudeThreshold)
				{
					situations.Add(ScienceSituation.FlyingLow);
				}
				else
				{
					if (vd.Vessel.verticalSpeed < 100.0
						&& (double.IsNaN(vd.Vessel.orbit.ApA) || vd.Vessel.orbit.ApA > body.atmosphereDepth)
						&& vd.Vessel.srfSpeed > vd.Vessel.speedOfSound * 5.0)
					{
						virtualBiomes.Add(VirtualBiome.Reentry);
					}
					situations.Add(ScienceSituation.FlyingHigh);
				}
				situations.Add(ScienceSituation.Flying);
				situations.Add(ScienceSituation.BodyGlobal);

				if (vd.EnvStorm)
					virtualBiomes.Add(VirtualBiome.Storm);

				if ((vd.Vessel.latitude + 270.0) % 90.0 > 0.0)
					virtualBiomes.Add(VirtualBiome.NorthernHemisphere);
				else
					virtualBiomes.Add(VirtualBiome.SouthernHemisphere);

				virtualBiomes.Add(VirtualBiome.NoBiome);
				return;
			}

			if (vd.EnvStorm)
				virtualBiomes.Add(VirtualBiome.Storm);

			if (vd.EnvInterstellar)
				virtualBiomes.Add(VirtualBiome.Interstellar);

			if (vd.EnvInnerBelt)
				virtualBiomes.Add(VirtualBiome.InnerBelt);
			else if (vd.EnvOuterBelt)
				virtualBiomes.Add(VirtualBiome.OuterBelt);

			if (vd.EnvMagnetosphere)
				virtualBiomes.Add(VirtualBiome.Magnetosphere);

			if (vd.Vessel.latitude > 0.0)
				virtualBiomes.Add(VirtualBiome.NorthernHemisphere);
			else
				virtualBiomes.Add(VirtualBiome.SouthernHemisphere);

			virtualBiomes.Add(VirtualBiome.NoBiome);

			if (vd.Vessel.altitude > body.scienceValues.spaceAltitudeThreshold)
				situations.Add(ScienceSituation.InSpaceHigh);
			else
				situations.Add(ScienceSituation.InSpaceLow);

			situations.Add(ScienceSituation.Space);
			situations.Add(ScienceSituation.BodyGlobal);
		}

		public static int GetBiomeIndex(Vessel vessel)
		{
			CBAttributeMapSO biomeMap = vessel.mainBody.BiomeMap;
			if (biomeMap == null)
				return -1;

			double lat = ((vessel.latitude + 180.0 + 90.0) % 180.0 - 90.0) * UtilMath.Deg2Rad; // clamp and convert to radians
			double lon = ((vessel.longitude + 360.0 + 180.0) % 360.0 - 180.0) * UtilMath.Deg2Rad; // clamp and convert to radians
			CBAttributeMapSO.MapAttribute biome = biomeMap.GetAtt(lat, lon);
			for (int i = biomeMap.Attributes.Length; i-- > 0;)
				if (biomeMap.Attributes[i] == biome)
					return i;

			return -1;
		}
	}
}
