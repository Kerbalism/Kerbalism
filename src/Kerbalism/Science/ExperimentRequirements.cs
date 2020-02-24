using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KSP.Localization;

namespace KERBALISM
{
	public class ExperimentRequirements
	{


		public enum Require
		{
			OrbitMinInclination,
			OrbitMaxInclination,
			OrbitMinEccentricity,
			OrbitMaxEccentricity,
			OrbitMinArgOfPeriapsis,
			OrbitMaxArgOfPeriapsis,

			TemperatureMin,
			TemperatureMax,
			AltitudeMin,
			AltitudeMax,
			RadiationMin,
			RadiationMax,
			Shadow,
			Sunlight,
			CrewMin,
			CrewMax,
			CrewCapacityMin,
			CrewCapacityMax,
			VolumePerCrewMin,
			VolumePerCrewMax,
			Greenhouse,
			AtmosphereAltMin,
			AtmosphereAltMax,
			AltitudeBodyRadiusMin,
			AltitudeBodyRadiusMax,

			SunAngleMin,
			SunAngleMax,

			AbsoluteZero,
			InnerBelt,
			OuterBelt,
			MagneticBelt,
			Magnetosphere,
			InterStellar,

			SurfaceSpeedMin,
			SurfaceSpeedMax,
			VerticalSpeedMin,
			VerticalSpeedMax,
			SpeedMin,
			SpeedMax,
			DynamicPressureMin,
			DynamicPressureMax,
			StaticPressureMin,
			StaticPressureMax,
			AtmDensityMin,
			AtmDensityMax,
			AltAboveGroundMin,
			AltAboveGroundMax,

			Part,
			Module,
			MaxAsteroidDistance,

			AstronautComplexLevelMin,
			AstronautComplexLevelMax,
			TrackingStationLevelMin,
			TrackingStationLevelMax,
			MissionControlLevelMin,
			MissionControlLevelMax,
			AdministrationLevelMin,
			AdministrationLevelMax,
		}

		public class RequireDef
		{
			public Require require;
			public object value;

			public RequireDef(Require require, object requireValue)
			{
				this.require = require;
				this.value = requireValue;
			}
		}

		public class RequireResult
		{
			public RequireDef requireDef;
			public object value;
			public bool isValid;

			public RequireResult(RequireDef requireDef)
			{
				this.requireDef = requireDef;
			}
		}

		// not ideal because unboxing at but least we won't be parsing strings all the time and the array should be fast
		public RequireDef[] Requires { get; private set; }

		public ExperimentRequirements(string requires)
		{
			Requires = ParseRequirements(requires);
		}

		public bool TestRequirements(Vessel v, out RequireResult[] results, bool testAll = false)
		{
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.ExperimentRequirements.TestRequirements");
			VesselData vd = v.KerbalismData();

			results = new RequireResult[Requires.Length];

			bool good = true;

			for (int i = 0; i < Requires.Length; i++)
			{
				results[i] = new RequireResult(Requires[i]);
				switch (Requires[i].require)
				{
					case Require.OrbitMinInclination   : TestReq((c, r) => c >= r, v.orbit.inclination,         (double)Requires[i].value, results[i]); break;
					case Require.OrbitMaxInclination   : TestReq((c, r) => c <= r, v.orbit.inclination,         (double)Requires[i].value, results[i]); break;
					case Require.OrbitMinEccentricity  : TestReq((c, r) => c >= r, v.orbit.eccentricity,        (double)Requires[i].value, results[i]); break;
					case Require.OrbitMaxEccentricity  : TestReq((c, r) => c <= r, v.orbit.eccentricity,        (double)Requires[i].value, results[i]); break;
					case Require.OrbitMinArgOfPeriapsis: TestReq((c, r) => c >= r, v.orbit.argumentOfPeriapsis, (double)Requires[i].value, results[i]); break;
					case Require.OrbitMaxArgOfPeriapsis: TestReq((c, r) => c <= r, v.orbit.argumentOfPeriapsis, (double)Requires[i].value, results[i]); break;
					case Require.TemperatureMin        : TestReq((c, r) => c >= r, vd.EnvTemperature,           (double)Requires[i].value, results[i]); break;
					case Require.TemperatureMax        : TestReq((c, r) => c <= r, vd.EnvTemperature,           (double)Requires[i].value, results[i]); break;
					case Require.AltitudeMin           : TestReq((c, r) => c >= r, v.altitude,                  (double)Requires[i].value, results[i]); break;
					case Require.AltitudeMax           : TestReq((c, r) => c <= r, v.altitude,                  (double)Requires[i].value, results[i]); break;
					case Require.RadiationMin          : TestReq((c, r) => c >= r, vd.EnvRadiation,             (double)Requires[i].value, results[i]); break;
					case Require.RadiationMax          : TestReq((c, r) => c <= r, vd.EnvRadiation,             (double)Requires[i].value, results[i]); break;

					case Require.VolumePerCrewMin      : TestReq((c, r) => c >= r, vd.HabitatInfo.volumePerCrew,(double)Requires[i].value, results[i]); break;
					case Require.VolumePerCrewMax      : TestReq((c, r) => c <= r, vd.HabitatInfo.volumePerCrew,(double)Requires[i].value, results[i]); break;
					case Require.SunAngleMin           : TestReq((c, r) => c >= r, vd.EnvSunBodyAngle,      (double)Requires[i].value, results[i]); break;
					case Require.SunAngleMax           : TestReq((c, r) => c <= r, vd.EnvSunBodyAngle,      (double)Requires[i].value, results[i]); break;
					case Require.SurfaceSpeedMin       : TestReq((c, r) => c >= r, v.srfSpeed,              (double)Requires[i].value, results[i]); break;
					case Require.SurfaceSpeedMax       : TestReq((c, r) => c <= r, v.srfSpeed,              (double)Requires[i].value, results[i]); break;
					case Require.VerticalSpeedMin      : TestReq((c, r) => c >= r, v.verticalSpeed,         (double)Requires[i].value, results[i]); break;
					case Require.VerticalSpeedMax      : TestReq((c, r) => c <= r, v.verticalSpeed,         (double)Requires[i].value, results[i]); break;
					case Require.SpeedMin              : TestReq((c, r) => c >= r, v.speed,                 (double)Requires[i].value, results[i]); break;
					case Require.SpeedMax              : TestReq((c, r) => c <= r, v.speed,                 (double)Requires[i].value, results[i]); break;
					case Require.DynamicPressureMin    : TestReq((c, r) => c >= r, v.dynamicPressurekPa,    (double)Requires[i].value, results[i]); break;
					case Require.DynamicPressureMax    : TestReq((c, r) => c <= r, v.dynamicPressurekPa,    (double)Requires[i].value, results[i]); break;
					case Require.StaticPressureMin     : TestReq((c, r) => c >= r, v.staticPressurekPa,     (double)Requires[i].value, results[i]); break;
					case Require.StaticPressureMax     : TestReq((c, r) => c <= r, v.staticPressurekPa,     (double)Requires[i].value, results[i]); break;
					case Require.AtmDensityMin         : TestReq((c, r) => c >= r, v.atmDensity,            (double)Requires[i].value, results[i]); break;
					case Require.AtmDensityMax         : TestReq((c, r) => c <= r, v.atmDensity,            (double)Requires[i].value, results[i]); break;
					case Require.AltAboveGroundMin     : TestReq((c, r) => c >= r, v.heightFromTerrain,     (double)Requires[i].value, results[i]); break;
					case Require.AltAboveGroundMax     : TestReq((c, r) => c <= r, v.heightFromTerrain,     (double)Requires[i].value, results[i]); break;
					case Require.MaxAsteroidDistance   : TestReq((c, r) => c <= r, TestAsteroidDistance(v), (double)Requires[i].value, results[i]); break;

					case Require.AtmosphereAltMin      : TestReq((c, r) => c >= r, v.mainBody.atmosphere ? v.altitude / v.mainBody.atmosphereDepth : double.NaN, (double)Requires[i].value, results[i]); break;
					case Require.AtmosphereAltMax      : TestReq((c, r) => c <= r, v.mainBody.atmosphere ? v.altitude / v.mainBody.atmosphereDepth : double.NaN, (double)Requires[i].value, results[i]); break;

					case Require.AltitudeBodyRadiusMin : TestReq((c, r) => c >= r, v.altitude / v.mainBody.Radius, (double)Requires[i].value, results[i]); break;
					case Require.AltitudeBodyRadiusMax : TestReq((c, r) => c <= r, v.altitude / v.mainBody.Radius, (double)Requires[i].value, results[i]); break;

					case Require.CrewMin                 : TestReq((c, r) => c >= r, vd.CrewCount,                                           (int)Requires[i].value, results[i]); break;
					case Require.CrewMax                 : TestReq((c, r) => c <= r, vd.CrewCount,                                           (int)Requires[i].value, results[i]); break;
					case Require.CrewCapacityMin         : TestReq((c, r) => c >= r, vd.CrewCapacity,                                        (int)Requires[i].value, results[i]); break;
					case Require.CrewCapacityMax         : TestReq((c, r) => c <= r, vd.CrewCapacity,                                        (int)Requires[i].value, results[i]); break;
					case Require.AstronautComplexLevelMin: TestReq((c, r) => c >= r, GetFacilityLevel(SpaceCenterFacility.AstronautComplex), (int)Requires[i].value, results[i]); break;
					case Require.AstronautComplexLevelMax: TestReq((c, r) => c <= r, GetFacilityLevel(SpaceCenterFacility.AstronautComplex), (int)Requires[i].value, results[i]); break;
					case Require.TrackingStationLevelMin : TestReq((c, r) => c >= r, GetFacilityLevel(SpaceCenterFacility.TrackingStation),  (int)Requires[i].value, results[i]); break;
					case Require.TrackingStationLevelMax : TestReq((c, r) => c <= r, GetFacilityLevel(SpaceCenterFacility.TrackingStation),  (int)Requires[i].value, results[i]); break;
					case Require.MissionControlLevelMin  : TestReq((c, r) => c >= r, GetFacilityLevel(SpaceCenterFacility.MissionControl),   (int)Requires[i].value, results[i]); break;
					case Require.MissionControlLevelMax  : TestReq((c, r) => c <= r, GetFacilityLevel(SpaceCenterFacility.MissionControl),   (int)Requires[i].value, results[i]); break;
					case Require.AdministrationLevelMin  : TestReq((c, r) => c >= r, GetFacilityLevel(SpaceCenterFacility.Administration),   (int)Requires[i].value, results[i]); break;
					case Require.AdministrationLevelMax  : TestReq((c, r) => c <= r, GetFacilityLevel(SpaceCenterFacility.Administration),   (int)Requires[i].value, results[i]); break;

					case Require.Shadow         : TestReq(() => vd.EnvInFullShadow,                                                                                  results[i]); break; 
					case Require.Sunlight       : TestReq(() => vd.EnvInSunlight,                                                                                    results[i]); break; 
					case Require.Greenhouse     : TestReq(() => vd.Greenhouses.Count > 0,                                                                            results[i]); break;
					case Require.AbsoluteZero   : TestReq(() => vd.EnvTemperature < 30.0,                                                                            results[i]); break;
					case Require.InnerBelt      : TestReq(() => vd.EnvInnerBelt,                                                                                     results[i]); break;
					case Require.OuterBelt      : TestReq(() => vd.EnvOuterBelt,                                                                                     results[i]); break;
					case Require.MagneticBelt   : TestReq(() => vd.EnvInnerBelt || vd.EnvOuterBelt,                                                                  results[i]); break;
					case Require.Magnetosphere  : TestReq(() => vd.EnvMagnetosphere,                                                                                 results[i]); break;
					case Require.InterStellar   : TestReq(() => Lib.IsSun(v.mainBody) && vd.EnvInterstellar,                                                         results[i]); break;
					case Require.Part           : TestReq(() => Lib.HasPart(v, (string)Requires[i].value),															 results[i]); break;
					case Require.Module         : TestReq(() => Lib.FindModules(v.protoVessel, (string)Requires[i].value).Count > 0,								 results[i]); break;

					default: results[i].isValid = true; break;
				}

				if (!testAll && !results[i].isValid)
				{
					UnityEngine.Profiling.Profiler.EndSample();
					return false;
				}

				good &= results[i].isValid;
			}

			UnityEngine.Profiling.Profiler.EndSample();
			return good;
		}

		public bool TestProgressionRequirements()
		{
			RequireResult[] results = new RequireResult[Requires.Length];

			for (int i = 0; i < Requires.Length; i++)
			{
				results[i] = new RequireResult(Requires[i]);
				switch (Requires[i].require)
				{
					case Require.AstronautComplexLevelMin: TestReq((c, r) => c >= r, GetFacilityLevel(SpaceCenterFacility.AstronautComplex), (int)Requires[i].value, results[i]); break;
					case Require.AstronautComplexLevelMax: TestReq((c, r) => c <= r, GetFacilityLevel(SpaceCenterFacility.AstronautComplex), (int)Requires[i].value, results[i]); break;
					case Require.TrackingStationLevelMin: TestReq((c, r) => c >= r, GetFacilityLevel(SpaceCenterFacility.TrackingStation), (int)Requires[i].value, results[i]); break;
					case Require.TrackingStationLevelMax: TestReq((c, r) => c <= r, GetFacilityLevel(SpaceCenterFacility.TrackingStation), (int)Requires[i].value, results[i]); break;
					case Require.MissionControlLevelMin: TestReq((c, r) => c >= r, GetFacilityLevel(SpaceCenterFacility.MissionControl), (int)Requires[i].value, results[i]); break;
					case Require.MissionControlLevelMax: TestReq((c, r) => c <= r, GetFacilityLevel(SpaceCenterFacility.MissionControl), (int)Requires[i].value, results[i]); break;
					case Require.AdministrationLevelMin: TestReq((c, r) => c >= r, GetFacilityLevel(SpaceCenterFacility.Administration), (int)Requires[i].value, results[i]); break;
					case Require.AdministrationLevelMax: TestReq((c, r) => c <= r, GetFacilityLevel(SpaceCenterFacility.Administration), (int)Requires[i].value, results[i]); break;

					default: results[i].isValid = true; break;
				}

				if (!results[i].isValid)
					return false;
			}
			return true;
		}

		private void TestReq(Func<bool> Condition, RequireResult result)
		{
			result.isValid = Condition();
		}

		private void TestReq<T, U>(Func<T, U, bool> Condition, T val, U reqVal, RequireResult result)
		{
			result.value = val;
			result.isValid = Condition(val, reqVal);
		}

		private RequireDef[] ParseRequirements(string requires)
		{
			List<RequireDef> reqList = new List<RequireDef>();
			if (string.IsNullOrEmpty(requires))
				return reqList.ToArray();
			foreach (string s in requires.Split(','))
			{
				s.Trim();
				string[] reqString = s.Split(':');

				if (reqString.Length > 0)
				{
					reqString[0].Trim();
					if (reqString.Length > 1)
					{
						reqString[1].Trim();
						// key/value requirements
						if (!Enum.IsDefined(typeof(Require), reqString[0]))
						{
							Lib.Log("Could not parse the experiment requires '" + s + "'", Lib.LogLevel.Warning);
							continue;
						}
						Require reqEnum = (Require)Enum.Parse(typeof(Require), reqString[0]);
						reqList.Add(ParseRequiresValue(reqEnum, reqString[1]));
					}
					else
					{
						// boolean condition, no value
						if (!Enum.IsDefined(typeof(Require), reqString[0]))
						{
							Lib.Log("Could not parse the experiment requires '" + s + "'", Lib.LogLevel.Warning);
							continue;
						}
						Require reqEnum = (Require)Enum.Parse(typeof(Require), reqString[0]);
						reqList.Add(new RequireDef(reqEnum, null));
					}
				}
			}
			return reqList.ToArray();
		}

		private RequireDef ParseRequiresValue(Require req, string value)
		{
			switch (req)
			{
				case Require.OrbitMinInclination:
				case Require.OrbitMaxInclination:
				case Require.OrbitMinEccentricity:
				case Require.OrbitMaxEccentricity:
				case Require.OrbitMinArgOfPeriapsis:
				case Require.OrbitMaxArgOfPeriapsis:
				case Require.TemperatureMin:
				case Require.TemperatureMax:
				case Require.AltitudeMin:
				case Require.AltitudeMax:
				case Require.RadiationMin:
				case Require.RadiationMax:
				case Require.VolumePerCrewMin:
				case Require.VolumePerCrewMax:
				case Require.AtmosphereAltMin:
				case Require.AtmosphereAltMax:
				case Require.SunAngleMin:
				case Require.SunAngleMax:
				case Require.SurfaceSpeedMin:
				case Require.SurfaceSpeedMax:
				case Require.VerticalSpeedMin:
				case Require.VerticalSpeedMax:
				case Require.SpeedMin:
				case Require.SpeedMax:
				case Require.DynamicPressureMin:
				case Require.DynamicPressureMax:
				case Require.StaticPressureMin:
				case Require.StaticPressureMax:
				case Require.AtmDensityMin:
				case Require.AtmDensityMax:
				case Require.AltitudeBodyRadiusMin:
				case Require.AltitudeBodyRadiusMax:
				case Require.AltAboveGroundMin:
				case Require.AltAboveGroundMax:
				case Require.MaxAsteroidDistance:
					return new RequireDef(req, double.Parse(value));
				case Require.CrewMin:
				case Require.CrewMax:
				case Require.CrewCapacityMin:
				case Require.CrewCapacityMax:
				case Require.AstronautComplexLevelMin:
				case Require.AstronautComplexLevelMax:
				case Require.TrackingStationLevelMin:
				case Require.TrackingStationLevelMax:
				case Require.MissionControlLevelMin:
				case Require.MissionControlLevelMax:
				case Require.AdministrationLevelMin:
				case Require.AdministrationLevelMax:
					return new RequireDef(req, int.Parse(value));
				default:
					return new RequireDef(req, value);
			}
		}

		private int GetFacilityLevel(SpaceCenterFacility facility)
		{
			if (ScenarioUpgradeableFacilities.Instance == null || !ScenarioUpgradeableFacilities.Instance.enabled)
				return int.MaxValue;


			double maxlevel = ScenarioUpgradeableFacilities.GetFacilityLevelCount(facility);
			if (maxlevel <= 0) maxlevel = 2; // not sure why, but GetFacilityLevelCount return -1 in career
			return (int)Math.Round(ScenarioUpgradeableFacilities.GetFacilityLevel(facility) * maxlevel + 1); // They start counting at 0
		}

		private double TestAsteroidDistance(Vessel vessel)
		{
			var target = vessel.targetObject;
			var vesselPosition = Lib.VesselPosition(vessel);

			// while there is a target, only consider the targeted vessel
			if (!vessel.loaded || target != null)
			{
				// asteroid MUST be the target if vessel is unloaded
				if (target == null) return double.MaxValue;

				var targetVessel = target.GetVessel();
				if (targetVessel == null) return double.MaxValue;

				if (targetVessel.vesselType != VesselType.SpaceObject) return double.MaxValue;

				// this assumes that all vessels of type space object are asteroids.
				// should be a safe bet unless Squad introduces alien UFOs.
				var asteroidPosition = Lib.VesselPosition(targetVessel);
				return Vector3d.Distance(vesselPosition, asteroidPosition);
			}

			// there's no target and vessel is not unloaded
			// look for nearby asteroids
			double result = double.MaxValue;
			foreach (Vessel v in FlightGlobals.VesselsLoaded)
			{
				if (v.vesselType != VesselType.SpaceObject) continue;
				var asteroidPosition = Lib.VesselPosition(v);
				double distance = Vector3d.Distance(vesselPosition, asteroidPosition);
				if (distance < result) result = distance;
			}
			return result;
		}

		public static string ReqValueFormat(Require req, object reqValue)
		{
			switch (req)
			{
				case Require.OrbitMinEccentricity:
				case Require.OrbitMaxEccentricity:
				case Require.OrbitMinArgOfPeriapsis:
				case Require.OrbitMaxArgOfPeriapsis:
				case Require.AtmosphereAltMin:
				case Require.AtmosphereAltMax:
				case Require.AltitudeBodyRadiusMin:
				case Require.AltitudeBodyRadiusMax:
					return ((double)reqValue).ToString("F2");
				case Require.SunAngleMin:
				case Require.SunAngleMax:
				case Require.OrbitMinInclination:
				case Require.OrbitMaxInclination:
					return Lib.HumanReadableAngle((double)reqValue);
				case Require.TemperatureMin:
				case Require.TemperatureMax:
					return Lib.HumanReadableTemp((double)reqValue);
				case Require.AltitudeMin:
				case Require.AltitudeMax:
				case Require.AltAboveGroundMin:
				case Require.AltAboveGroundMax:
				case Require.MaxAsteroidDistance:
					return Lib.HumanReadableDistance((double)reqValue);
				case Require.RadiationMin:
				case Require.RadiationMax:
					return Lib.HumanReadableRadiation((double)reqValue);
				case Require.VolumePerCrewMin:
				case Require.VolumePerCrewMax:
					return Lib.HumanReadableVolume((double)reqValue);
				case Require.SurfaceSpeedMin:
				case Require.SurfaceSpeedMax:
				case Require.VerticalSpeedMin:
				case Require.VerticalSpeedMax:
				case Require.SpeedMin:
				case Require.SpeedMax:
					return Lib.HumanReadableSpeed((double)reqValue);
				case Require.DynamicPressureMin:
				case Require.DynamicPressureMax:
				case Require.StaticPressureMin:
				case Require.StaticPressureMax:
				case Require.AtmDensityMin:
				case Require.AtmDensityMax:
					return Lib.HumanReadablePressure((double)reqValue);
				case Require.CrewMin:
				case Require.CrewMax:
				case Require.CrewCapacityMin:
				case Require.CrewCapacityMax:
				case Require.AstronautComplexLevelMin:
				case Require.AstronautComplexLevelMax:
				case Require.TrackingStationLevelMin:
				case Require.TrackingStationLevelMax:
				case Require.MissionControlLevelMin:
				case Require.MissionControlLevelMax:
				case Require.AdministrationLevelMin:
				case Require.AdministrationLevelMax:
					return ((int)reqValue).ToString();
				default:
					return string.Empty;
			}
		}

		public static string ReqName(Require req)
		{
			switch (req)
			{
				case Require.OrbitMinInclination:      return Local.ExperimentReq_OrbitMinInclination;//"Min. inclination "
				case Require.OrbitMaxInclination:      return Local.ExperimentReq_OrbitMaxInclination;//"Max. inclination "
				case Require.OrbitMinEccentricity:     return Local.ExperimentReq_OrbitMinEccentricity;//"Min. eccentricity "
				case Require.OrbitMaxEccentricity:     return Local.ExperimentReq_OrbitMaxEccentricity;//"Max. eccentricity "
				case Require.OrbitMinArgOfPeriapsis:   return Local.ExperimentReq_OrbitMinArgOfPeriapsis;//"Min. argument of Pe "
				case Require.OrbitMaxArgOfPeriapsis:   return Local.ExperimentReq_OrbitMaxArgOfPeriapsis;//"Max. argument of Pe "
				case Require.TemperatureMin:           return Local.ExperimentReq_TemperatureMin;//"Min. temperature "
				case Require.TemperatureMax:           return Local.ExperimentReq_TemperatureMax;//"Max. temperature "
				case Require.AltitudeMin:              return Local.ExperimentReq_AltitudeMin;//"Min. altitude "
				case Require.AltitudeMax:              return Local.ExperimentReq_AltitudeMax;//"Max. altitude "
				case Require.RadiationMin:             return Local.ExperimentReq_RadiationMin;//"Min. radiation "
				case Require.RadiationMax:             return Local.ExperimentReq_RadiationMax;//"Max. radiation "
				case Require.VolumePerCrewMin:         return Local.ExperimentReq_VolumePerCrewMin;//"Min. vol./crew "
				case Require.VolumePerCrewMax:         return Local.ExperimentReq_VolumePerCrewMax;//"Max. vol./crew "
				case Require.SunAngleMin:              return Local.ExperimentReq_SunAngleMin;//"Min sun-surface angle"
				case Require.SunAngleMax:              return Local.ExperimentReq_SunAngleMax;//"Max sun-surface angle"
				case Require.SurfaceSpeedMin:          return Local.ExperimentReq_SurfaceSpeedMin;//"Min. surface speed "
				case Require.SurfaceSpeedMax:          return Local.ExperimentReq_SurfaceSpeedMax;//"Max. surface speed "
				case Require.VerticalSpeedMin:         return Local.ExperimentReq_VerticalSpeedMin;//"Min. vertical speed "
				case Require.VerticalSpeedMax:         return Local.ExperimentReq_VerticalSpeedMax;//"Max. vertical speed "
				case Require.SpeedMin:                 return Local.ExperimentReq_SpeedMin;//"Min. speed "
				case Require.SpeedMax:                 return Local.ExperimentReq_SpeedMax;//"Max. speed "
				case Require.DynamicPressureMin:       return Local.ExperimentReq_DynamicPressureMin;//"Min dynamic pressure"
				case Require.DynamicPressureMax:       return Local.ExperimentReq_DynamicPressureMax;//"Max dynamic pressure"
				case Require.StaticPressureMin:        return Local.ExperimentReq_StaticPressureMin;//"Min. pressure "
				case Require.StaticPressureMax:        return Local.ExperimentReq_StaticPressureMax;//"Max. pressure "
				case Require.AtmDensityMin:            return Local.ExperimentReq_AtmDensityMin;//"Min. atm. density "
				case Require.AtmDensityMax:            return Local.ExperimentReq_AtmDensityMax;//"Max. atm. density "
				case Require.AltAboveGroundMin:        return Local.ExperimentReq_AltAboveGroundMin;//"Min ground altitude"
				case Require.AltAboveGroundMax:        return Local.ExperimentReq_AltAboveGroundMax;//"Max ground altitude"
				case Require.MaxAsteroidDistance:      return Local.ExperimentReq_MaxAsteroidDistance;//"Max asteroid distance"
				case Require.AtmosphereAltMin:         return Local.ExperimentReq_AtmosphereAltMin;//"Min atmosphere altitude "
				case Require.AtmosphereAltMax:         return Local.ExperimentReq_AtmosphereAltMax;//"Max atmosphere altitude "
				case Require.AltitudeBodyRadiusMin:    return Local.ExperimentReq_AltitudeBodyRadiusMin;//"Min body radius altitude "
				case Require.AltitudeBodyRadiusMax:    return Local.ExperimentReq_AltitudeBodyRadiusMax;//"Max body radius altitude "
				case Require.CrewMin:                  return Local.ExperimentReq_CrewMin;//"Min. crew "
				case Require.CrewMax:                  return Local.ExperimentReq_CrewMax;//"Max. crew "
				case Require.CrewCapacityMin:          return Local.ExperimentReq_CrewCapacityMin;//"Min. crew capacity "
				case Require.CrewCapacityMax:          return Local.ExperimentReq_CrewCapacityMax;//"Max. crew capacity "
				case Require.AstronautComplexLevelMin: return Local.ExperimentReq_AstronautComplexLevelMin;//"Astronaut Complex min level "
				case Require.AstronautComplexLevelMax: return Local.ExperimentReq_AstronautComplexLevelMin;//"Astronaut Complex max level "
				case Require.TrackingStationLevelMin:  return Local.ExperimentReq_TrackingStationLevelMin;//"Tracking Station min level "
				case Require.TrackingStationLevelMax:  return Local.ExperimentReq_TrackingStationLevelMax;//"Tracking Station max level "
				case Require.MissionControlLevelMin:   return Local.ExperimentReq_MissionControlLevelMin;//"Mission Control min level "
				case Require.MissionControlLevelMax:   return Local.ExperimentReq_MissionControlLevelMax;//"Mission Control max level "
				case Require.AdministrationLevelMin:   return Local.ExperimentReq_AdministrationLevelMin;//"Administration min level "
				case Require.AdministrationLevelMax:   return Local.ExperimentReq_AdministrationLevelMax;//"Administration max level "
				case Require.Part:                     return Local.ExperimentReq_Part;//"Need part "
				case Require.Module:                   return Local.ExperimentReq_Module;//"Need module "

				case Require.AbsoluteZero:
				case Require.InnerBelt:
				case Require.OuterBelt:
				case Require.MagneticBelt:
				case Require.Magnetosphere:
				case Require.InterStellar:
				case Require.Shadow:
				case Require.Sunlight:
				case Require.Greenhouse:
				default:
					return req.ToString();
			}
		}
	}
}
