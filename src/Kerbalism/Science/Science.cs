﻿using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;


namespace KERBALISM
{

	public static class Science
	{
		// this controls how fast science is credited while it is being transmitted.
		// try to be conservative here, because crediting introduces a lag
		private const double buffer_science_value = 0.3;

		// this is for auto-transmit throttling
		public const double min_file_size = 0.002;

		// pseudo-ctor
		public static void Init()
		{
			// make the science dialog invisible, just once
			if (Features.Science)
			{
				GameObject prefab = AssetBase.GetPrefab("ScienceResultsDialog");
				if (Settings.ScienceDialog)
				{
					prefab.gameObject.AddOrGetComponent<Hijacker>();
				}
				else
				{
					prefab.gameObject.AddOrGetComponent<MiniHijacker>();
				}
			}
		}

		private static Drive FindDrive(Vessel v, string filename)
		{
			foreach (var d in Drive.GetDrives(v, true))
			{
				if (d.files.ContainsKey(filename))
				{
					return d;
				}
			}
			return null;
		}

		// consume EC for transmission, and transmit science data
		public static void Update(Vessel v, Vessel_info vi, VesselData vd, Vessel_resources resources, double elapsed_s)
		{
			// do nothing if science system is disabled
			if (!Features.Science) return;

			// avoid corner-case when RnD isn't live during scene changes
			// - this avoid losing science if the buffer reach threshold during a scene change
			if (HighLogic.CurrentGame.Mode != Game.Modes.SANDBOX && ResearchAndDevelopment.Instance == null) return;

			// get connection info
			ConnectionInfo conn = vi.connection;
			if (conn == null) return;
			if (String.IsNullOrEmpty(vi.transmitting)) return;

			Drive warpCache = Cache.WarpCache(v);
			bool isWarpCache = false;

			double transmitSize = conn.rate * elapsed_s;
			while(warpCache.files.Count > 0 || // transmit EVERYTHING in the cache, regardless of transmitSize.
			      (transmitSize > double.Epsilon && !String.IsNullOrEmpty(vi.transmitting)))
			{
				// get filename of data being downloaded
				var exp_filename = vi.transmitting;
				if (string.IsNullOrEmpty(exp_filename))
					break;

				Drive drive = null;
				if (warpCache.files.ContainsKey(exp_filename)) {
					drive = warpCache;
					isWarpCache = true;
				}
				else
				{
					drive = FindDrive(v, exp_filename);
					isWarpCache = false;
				}

				if (drive == null) break;

				File file = drive.files[exp_filename];

				if(isWarpCache) {
					file.buff = file.size;
					file.size = 0;
					transmitSize -= file.size;
				} else {
					if (transmitSize < double.Epsilon)
						break;

					// determine how much data is transmitted
					double transmitted = Math.Min(file.size, transmitSize);
					transmitSize -= transmitted;

					// consume data in the file
					file.size -= transmitted;

					// accumulate in the buffer
					file.buff += transmitted;
				}

				// special case: file size on drive = 0 -> buffer is 0, so no need to do anyhting. just delete.
				if (file.buff > double.Epsilon)
				{
					// this is the science value remaining for this experiment
					var remainingValue = Value(exp_filename, 0);

					// this is the science value of this sample
					double dataValue = Value(exp_filename, file.buff);
					bool doCredit = file.size <= double.Epsilon || dataValue > buffer_science_value;;

					// if buffer science value is high enough or file was transmitted completely
					if (doCredit)
					{
						var totalValue = TotalValue(exp_filename);

						// collect the science data
						Credit(exp_filename, file.buff, true, v.protoVessel);

						// reset the buffer
						file.buff = 0.0;

						// this was the last useful bit, there is no more value in the experiment
						if (remainingValue >= 0.1 && remainingValue - dataValue < 0.1)
						{

							Message.Post(
								Lib.BuildString(Lib.HumanReadableScience(totalValue), " ", Experiment(exp_filename).FullName(exp_filename), " completed"),
							  Lib.TextVariant(
									"Our researchers will jump on it right now",
									"This cause some excitement",
									"These results are causing a brouhaha in R&D",
									"Our scientists look very confused",
									"The scientists won't believe these readings"
								));
						}
					}
				}

				// if file was transmitted completely
				if (file.size <= double.Epsilon)
				{
					// remove the file
					drive.files.Remove(exp_filename);
					vi.transmitting = Science.Transmitting(v, true);
				}
			}
		}

		// return name of file being transmitted from vessel specified
		public static string Transmitting(Vessel v, bool linked)
		{
			// never transmitting if science system is disabled
			if (!Features.Science) return string.Empty;

			// not transmitting if unlinked
			if (!linked) return string.Empty;

			// not transmitting if there is no ec left
			if (!Lib.IsPowered(v)) return string.Empty;

			foreach(var p in Cache.WarpCache(v).files)
				return p.Key;

			// get first file flagged for transmission, AND has a ts at least 5 seconds old or is > 0.001Mb in size
			foreach (var drive in Drive.GetDrives(v, true))
			{
				double now = Planetarium.GetUniversalTime();
				foreach (var p in drive.files)
				{
					if (drive.GetFileSend(p.Key) && (p.Value.ts + 3 < now || p.Value.size > min_file_size)) return p.Key;
				}
			}

			// no file flagged for transmission
			return string.Empty;
		}

		public static void ClearDeferred()
		{
			deferredCredit.Clear();
		}

		public static void CreditAllDeferred()
		{
			foreach(var deferred in deferredCredit.Values)
			{
				Credit(deferred.subject_id, deferred.size, true, deferred.pv, true);
			}
			deferredCredit.Clear();
		}

		private static void CreditDeferred(string subject_id, double size, ProtoVessel pv)
		{
			if (deferredCredit.ContainsKey(subject_id))
			{
				var deferred = deferredCredit[subject_id];
				deferred.size += size;
				deferred.pv = pv;

				var credits = Value(subject_id, deferred.size);
				if(credits >= buffer_science_value)
				{
					deferredCredit.Remove(subject_id);
					Credit(subject_id, deferred.size, true, pv, true);
				}
			}
			else
			{
				deferredCredit.Add(subject_id, new DeferredCreditValues(subject_id, size, pv));
			}
		}

		// credit science for the experiment subject specified
		public static float Credit(string subject_id, double size, bool transmitted, ProtoVessel pv, bool enforced_credit = false)
		{
			var credits = Value(subject_id, size);

			if(!enforced_credit && transmitted && credits < buffer_science_value) {
				CreditDeferred(subject_id, size, pv);
				return credits;
			}

			if(deferredCredit.ContainsKey(subject_id)) {
				var deferred = deferredCredit[subject_id];
				size += deferred.size;
				deferred.size = 0;
				credits = Value(subject_id, size);
			}

			// credit the science
			var subject = ResearchAndDevelopment.GetSubjectByID(subject_id);
			if(subject == null)
			{
				Lib.Log("WARNING: science subject " + subject_id + " cannot be credited in R&D");
			}
			else
			{
				subject.science += credits / HighLogic.CurrentGame.Parameters.Career.ScienceGainMultiplier;
				subject.scientificValue = ResearchAndDevelopment.GetSubjectValue(subject.science, subject);
				ResearchAndDevelopment.Instance.AddScience(credits, transmitted ? TransactionReasons.ScienceTransmission : TransactionReasons.VesselRecovery);

				// fire game event
				// - this could be slow or a no-op, depending on the number of listeners
				//   in any case, we are buffering the transmitting data and calling this
				//   function only once in a while
				GameEvents.OnScienceRecieved.Fire(credits, subject, pv, false);

				API.OnScienceReceived.Fire(credits, subject, pv, transmitted);
			}

			// return amount of science credited
			return credits;
		}

		// return value of some data about a subject, in science credits
		public static float Value(string subject_id, double size = 0)
		{
			if (string.IsNullOrEmpty(subject_id))
				return 0;
			
			if(size < double.Epsilon)
			{
				var exp = Science.Experiment(subject_id);
				size = exp.max_amount;
			}

			// get science subject
			// - if null, we are in sandbox mode
			var subject = ResearchAndDevelopment.GetSubjectByID(subject_id);
			if (subject == null) return 0.0f;

			double R = size / subject.dataScale * subject.subjectValue;
			double S = subject.science;
			double C = subject.scienceCap;
			double credits = Math.Max(Math.Min(S + Math.Min(R, C), C) - S, 0.0);

			credits *= HighLogic.CurrentGame.Parameters.Career.ScienceGainMultiplier;

			return (float)credits;
		}

		// return total value of some data about a subject, in science credits
		public static float TotalValue(string subject_id)
		{
			var exp = Science.Experiment(subject_id);
			var size = exp.max_amount;

			// get science subject
			// - if null, we are in sandbox mode
			var subject = ResearchAndDevelopment.GetSubjectByID(subject_id);
			if (subject == null) return 0.0f;

			double credits = size / subject.dataScale * subject.subjectValue;
			credits *= HighLogic.CurrentGame.Parameters.Career.ScienceGainMultiplier;

			return (float)credits;
		}

		// return module acting as container of an experiment
		public static IScienceDataContainer Container(Part p, string experiment_id)
		{
			// first try to get a stock experiment module with the right experiment id
			// - this support parts with multiple experiment modules, like eva kerbal
			foreach (ModuleScienceExperiment exp in p.FindModulesImplementing<ModuleScienceExperiment>())
			{
				if (exp.experimentID == experiment_id) return exp;
			}

			// if none was found, default to the first module implementing the science data container interface
			// - this support third-party modules that implement IScienceDataContainer, but don't derive from ModuleScienceExperiment
			return p.FindModuleImplementing<IScienceDataContainer>();
		}


		// return info about an experiment
		public static ExperimentInfo Experiment(string subject_id)
		{
			ExperimentInfo info;
			if (!experiments.TryGetValue(subject_id, out info))
			{
				info = new ExperimentInfo(subject_id);
				experiments.Add(subject_id, info);
			}
			return info;
		}

		public static string Generate_subject_id(string experiment_id, Vessel v)
		{
			var body = v.mainBody;
			ScienceExperiment experiment = ResearchAndDevelopment.GetExperiment(experiment_id);
			ExperimentSituation sit = GetExperimentSituation(v);

			var sitStr = sit.ToString();
			if(!string.IsNullOrEmpty(sitStr))
			{
				if (sit.BiomeIsRelevant(Experiment(experiment_id)))
					sitStr += ScienceUtil.GetExperimentBiome(v.mainBody, v.latitude, v.longitude);
			}

			// generate subject id
			return Lib.BuildString(experiment_id, "@", body.name, sitStr);
		}

		public static string Generate_subject(string experiment_id, Vessel v)
		{
			var subject_id = Generate_subject_id(experiment_id, v);

			// in sandbox, do nothing else
				if (ResearchAndDevelopment.Instance == null) return subject_id;

			// if the subject id was never added to RnD
			if (ResearchAndDevelopment.GetSubjectByID(subject_id) == null)
			{
				// get subjects container using reflection
				// - we tried just changing the subject.id instead, and
				//   it worked but the new id was obviously used only after
				//   putting RnD through a serialization->deserialization cycle
				var subjects = Lib.ReflectionValue<Dictionary<string, ScienceSubject>>
				(
				  ResearchAndDevelopment.Instance,
				  "scienceSubjects"
				);

				var experiment = ResearchAndDevelopment.GetExperiment(experiment_id);
				var sit = GetExperimentSituation(v);
				var biome = ScienceUtil.GetExperimentBiome(v.mainBody, v.latitude, v.longitude);
				float multiplier = sit.Multiplier(Experiment(experiment_id));
				var cap = multiplier * experiment.baseValue;

				// create new subject
				ScienceSubject subject = new ScienceSubject
				(
				  		subject_id,
						Lib.BuildString(experiment.experimentTitle, " (", Lib.SpacesOnCaps(sit + biome), ")"),
						experiment.dataScale,
				  		multiplier,
						cap
				);

				// add it to RnD
				subjects.Add(subject_id, subject);
			}

			return subject_id;
		}

		public static string TestRequirements(string experiment_id, string requirements, Vessel v)
		{
			CelestialBody body = v.mainBody;
			Vessel_info vi = Cache.VesselInfo(v);

			List<string> list = Lib.Tokenize(requirements, ',');
			foreach (string s in list)
			{
				var parts = Lib.Tokenize(s, ':');

				var condition = parts[0];
				string value = string.Empty;
				if(parts.Count > 1) value = parts[1];

				bool good = true;
				switch (condition)
				{
					case "OrbitMinInclination": good = Math.Abs(v.orbit.inclination) >= double.Parse(value); break;
					case "OrbitMaxInclination": good = Math.Abs(v.orbit.inclination) <= double.Parse(value); break;
					case "OrbitMinEccentricity": good = v.orbit.eccentricity >= double.Parse(value); break;
					case "OrbitMaxEccentricity": good = v.orbit.eccentricity <= double.Parse(value); break;
					case "OrbitMinArgOfPeriapsis": good = v.orbit.argumentOfPeriapsis >= double.Parse(value); break;
					case "OrbitMaxArgOfPeriapsis": good = v.orbit.argumentOfPeriapsis <= double.Parse(value); break;

					case "TemperatureMin": good = vi.temperature >= double.Parse(value); break;
					case "TemperatureMax": good = vi.temperature <= double.Parse(value); break;
					case "AltitudeMin": good = v.altitude >= double.Parse(value); break;
					case "AltitudeMax": good = v.altitude <= double.Parse(value); break;
					case "RadiationMin": good = vi.radiation >= double.Parse(value); break;
					case "RadiationMax": good = vi.radiation <= double.Parse(value); break;
					case "Microgravity": good = vi.zerog; break;
					case "Body": good = TestBody(v.mainBody.name, value); break;
					case "Shadow": good = vi.sunlight < double.Epsilon; break;
					case "Sunlight": good = vi.sunlight > 0.5; break;
					case "CrewMin": good = vi.crew_count >= int.Parse(value); break;
					case "CrewMax": good = vi.crew_count <= int.Parse(value); break;
					case "CrewCapacityMin": good = vi.crew_capacity >= int.Parse(value); break;
					case "CrewCapacityMax": good = vi.crew_capacity <= int.Parse(value); break;
					case "VolumePerCrewMin": good = vi.volume_per_crew >= double.Parse(value); break;
					case "VolumePerCrewMax": good = vi.volume_per_crew <= double.Parse(value); break;
					case "Greenhouse": good = vi.greenhouses.Count > 0; break;
					case "Surface": good = Lib.Landed(v); break;
					case "Atmosphere": good = body.atmosphere && v.altitude < body.atmosphereDepth; break;
					case "AtmosphereBody": good = body.atmosphere; break;
					case "AtmosphereAltMin": good = body.atmosphere && (v.altitude / body.atmosphereDepth) >= double.Parse(value); break;
					case "AtmosphereAltMax": good = body.atmosphere && (v.altitude / body.atmosphereDepth) <= double.Parse(value); break;

					case "BodyWithAtmosphere": good = body.atmosphere; break;
					case "BodyWithoutAtmosphere": good = !body.atmosphere; break;
						
					case "SunAngleMin": good = Lib.IsSun(v.mainBody) || Lib.SunBodyAngle(v) >= double.Parse(value); break;
					case "SunAngleMax": good = Lib.IsSun(v.mainBody) || Lib.SunBodyAngle(v) <= double.Parse(value); break;

					case "Vacuum": good = !body.atmosphere || v.altitude > body.atmosphereDepth; break;
					case "Ocean": good = body.ocean && v.altitude < 0.0; break;
					case "PlanetarySpace": good = body.flightGlobalsIndex != 0 && !Lib.Landed(v) && v.altitude > body.atmosphereDepth; break;
					case "AbsoluteZero": good = vi.temperature < 30.0; break;
					case "InnerBelt": good = vi.inner_belt; break;
					case "OuterBelt": good = vi.outer_belt; break;
					case "MagneticBelt": good = vi.inner_belt || vi.outer_belt; break;
					case "Magnetosphere": good = vi.magnetosphere; break;
					case "Thermosphere": good = vi.thermosphere; break;
					case "Exosphere": good = vi.exosphere; break;
					case "InterPlanetary": good = body.flightGlobalsIndex == 0 && !vi.interstellar; break;
					case "InterStellar": good = body.flightGlobalsIndex == 0 && vi.interstellar; break;

					case "SurfaceSpeedMin": good = v.srfSpeed >= double.Parse(value); break;
					case "SurfaceSpeedMax": good = v.srfSpeed <= double.Parse(value); break;
					case "VerticalSpeedMin": good = v.verticalSpeed >= double.Parse(value); break;
					case "VerticalSpeedMax": good = v.verticalSpeed <= double.Parse(value); break;
					case "SpeedMin": good = v.speed >= double.Parse(value); break;
					case "SpeedMax": good = v.speed <= double.Parse(value); break;
					case "DynamicPressureMin": good = v.dynamicPressurekPa >= double.Parse(value); break;
					case "DynamicPressureMax": good = v.dynamicPressurekPa <= double.Parse(value); break;
					case "StaticPressureMin": good = v.staticPressurekPa >= double.Parse(value); break;
					case "StaticPressureMax": good = v.staticPressurekPa <= double.Parse(value); break;
					case "AtmDensityMin": good = v.atmDensity >= double.Parse(value); break;
					case "AtmDensityMax": good = v.atmDensity <= double.Parse(value); break;
					case "AltAboveGroundMin": good = v.heightFromTerrain >= double.Parse(value); break;
					case "AltAboveGroundMax": good = v.heightFromTerrain <= double.Parse(value); break;

					case "Part": good = Lib.HasPart(v, value); break;
					case "Module": good = Lib.FindModules(v.protoVessel, value).Count > 0; break;
						
					case "AstronautComplexLevelMin":
						good = (ScenarioUpgradeableFacilities.Instance == null) || ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.AstronautComplex) >= (double.Parse(value) - 1) / 2.0;
						break;
					case "AstronautComplexLevelMax":
						good = (ScenarioUpgradeableFacilities.Instance == null) || ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.AstronautComplex) <= (double.Parse(value) - 1) / 2.0;
						break;

					case "TrackingStationLevelMin":
						good = (ScenarioUpgradeableFacilities.Instance == null) || ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.TrackingStation) >= (double.Parse(value) - 1) / 2.0;
						break;
					case "TrackingStationLevelMax":
						good = (ScenarioUpgradeableFacilities.Instance == null) || ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.TrackingStation) <= (double.Parse(value) - 1) / 2.0;
						break;

					case "MissionControlLevelMin":
						good = (ScenarioUpgradeableFacilities.Instance == null) || ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.MissionControl) >= (double.Parse(value) - 1) / 2.0;
						break;
					case "MissionControlLevelMax":
						good = (ScenarioUpgradeableFacilities.Instance == null) || ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.MissionControl) <= (double.Parse(value) - 1) / 2.0;
						break;

					case "AdministrationLevelMin":
						good = (ScenarioUpgradeableFacilities.Instance == null) || ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.Administration) >= (double.Parse(value) - 1) / 2.0;
						break;
					case "AdministrationLevelMax":
						good = (ScenarioUpgradeableFacilities.Instance == null) || ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.Administration) <= (double.Parse(value) - 1) / 2.0;
						break;

					case "MaxAsteroidDistance": good = AsteroidDistance(v) <= double.Parse(value); break;
				}

				if (!good) return s;
			}

			var subject_id = Science.Generate_subject_id(experiment_id, v);

			var exp = Science.Experiment(subject_id);
			var sit = GetExperimentSituation(v);

			if (!v.loaded && sit.AtmosphericFlight())
				return "Background flight";

			if (!sit.IsAvailable(exp))
				return "Invalid situation";


			// At this point we know the situation is valid and the experiment can be done
			// create it in R&D
			Science.Generate_subject(experiment_id, v);

			return string.Empty;
		}

		public static ExperimentSituation GetExperimentSituation(Vessel v)
		{
			return new ExperimentSituation(v);
		}

		private static bool TestBody(string bodyName, string requirement)
		{
			foreach(string s in Lib.Tokenize(requirement, ';'))
			{
				if (s == bodyName) return true;
				if(s[0] == '!' && s.Substring(1) == bodyName) return false;
			}
			return false;
		}

		private static double AsteroidDistance(Vessel vessel)
		{
			var target = vessel.targetObject;
			var vesselPosition = Lib.VesselPosition(vessel);

			// while there is a target, only consider the targeted vessel
			if(!vessel.loaded || target != null)
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
			foreach(Vessel v in FlightGlobals.VesselsLoaded)
			{
				if (v.vesselType != VesselType.SpaceObject) continue;
				var asteroidPosition = Lib.VesselPosition(v);
				double distance = Vector3d.Distance(vesselPosition, asteroidPosition);
				if (distance < result) result = distance;
			}
			return result;
		}

		public static string RequirementText(string requirement)
		{
			var parts = Lib.Tokenize(requirement, ':');

			var condition = parts[0];
			string value = string.Empty;
			if (parts.Count > 1) value = parts[1];
						
			switch (condition)
			{
				case "OrbitMinInclination": return Lib.BuildString("Min. inclination ", value, "°");
				case "OrbitMaxInclination": return Lib.BuildString("Max. inclination ", value, "°");
				case "OrbitMinEccentricity": return Lib.BuildString("Min. eccentricity ", value);
				case "OrbitMaxEccentricity": return Lib.BuildString("Max. eccentricity ", value);
				case "OrbitMinArgOfPeriapsis": return Lib.BuildString("Min. argument of Pe ", value);
				case "OrbitMaxArgOfPeriapsis": return Lib.BuildString("Max. argument of Pe ", value);
				case "AltitudeMin": return Lib.BuildString("Min. altitude ", Lib.HumanReadableRange(Double.Parse(value)));
				case "AltitudeMax":
					var v = Double.Parse(value);
					if (v >= 0) return Lib.BuildString("Max. altitude ", Lib.HumanReadableRange(v));
					return Lib.BuildString("Min. depth ", Lib.HumanReadableRange(-v));
				case "RadiationMin": return Lib.BuildString("Min. radiation ", Lib.HumanReadableRadiation(Double.Parse(value)));
				case "RadiationMax": return Lib.BuildString("Max. radiation ", Lib.HumanReadableRadiation(Double.Parse(value)));
				case "Body": return PrettyBodyText(value);
				case "TemperatureMin": return Lib.BuildString("Min. temperature ", Lib.HumanReadableTemp(Double.Parse(value)));
				case "TemperatureMax": return Lib.BuildString("Max. temperature ", Lib.HumanReadableTemp(Double.Parse(value)));
				case "CrewMin": return Lib.BuildString("Min. crew ", value);
				case "CrewMax": return Lib.BuildString("Max. crew ", value);
				case "CrewCapacityMin": return Lib.BuildString("Min. crew capacity ", value);
				case "CrewCapacityMax": return Lib.BuildString("Max. crew capacity ", value);
				case "VolumePerCrewMin": return Lib.BuildString("Min. vol./crew ", Lib.HumanReadableVolume(double.Parse(value)));
				case "VolumePerCrewMax": return Lib.BuildString("Max. vol./crew ", Lib.HumanReadableVolume(double.Parse(value)));
				case "MaxAsteroidDistance": return Lib.BuildString("Max. asteroid distance ", Lib.HumanReadableRange(double.Parse(value)));

				case "SunAngleMin": return Lib.BuildString("Min. sun angle ", Lib.HumanReadableAngle(double.Parse(value)));
				case "SunAngleMax": return Lib.BuildString("Max. sun angle ", Lib.HumanReadableAngle(double.Parse(value)));
					
				case "AtmosphereBody": return "Body with atmosphere";
				case "AtmosphereAltMin": return Lib.BuildString("Min. atmosphere altitude ", value);
				case "AtmosphereAltMax": return Lib.BuildString("Max. atmosphere altitude ", value);
					
				case "SurfaceSpeedMin": return Lib.BuildString("Min. surface speed ", Lib.HumanReadableSpeed(double.Parse(value)));
				case "SurfaceSpeedMax": return Lib.BuildString("Max. surface speed ", Lib.HumanReadableSpeed(double.Parse(value)));
				case "VerticalSpeedMin": return Lib.BuildString("Min. vertical speed ", Lib.HumanReadableSpeed(double.Parse(value)));
				case "VerticalSpeedMax": return Lib.BuildString("Max. vertical speed ", Lib.HumanReadableSpeed(double.Parse(value)));
				case "SpeedMin": return Lib.BuildString("Min. speed ", Lib.HumanReadableSpeed(double.Parse(value)));
				case "SpeedMax": return Lib.BuildString("Max. speed ", Lib.HumanReadableSpeed(double.Parse(value)));
				case "DynamicPressureMin": return Lib.BuildString("Min. dynamic pressure ", Lib.HumanReadablePressure(double.Parse(value)));
				case "DynamicPressureMax": return Lib.BuildString("Max. dynamic pressure ", Lib.HumanReadablePressure(double.Parse(value)));
				case "StaticPressureMin": return Lib.BuildString("Min. pressure ", Lib.HumanReadablePressure(double.Parse(value)));
				case "StaticPressureMax": return Lib.BuildString("Max. pressure ", Lib.HumanReadablePressure(double.Parse(value)));
				case "AtmDensityMin": return Lib.BuildString("Min. atm. density ", Lib.HumanReadablePressure(double.Parse(value)));
				case "AtmDensityMax": return Lib.BuildString("Max. atm. density ", Lib.HumanReadablePressure(double.Parse(value)));
				case "AltAboveGroundMin": return Lib.BuildString("Min. ground altitude ", Lib.HumanReadableRange(double.Parse(value)));
				case "AltAboveGroundMax": return Lib.BuildString("Max. ground altitude ", Lib.HumanReadableRange(double.Parse(value)));

				case "MissionControlLevelMin": return Lib.BuildString(ScenarioUpgradeableFacilities.GetFacilityName(SpaceCenterFacility.MissionControl), " level ", value);
				case "MissionControlLevelMax": return Lib.BuildString(ScenarioUpgradeableFacilities.GetFacilityName(SpaceCenterFacility.MissionControl), " max. level ", value);
				case "AdministrationLevelMin": return Lib.BuildString(ScenarioUpgradeableFacilities.GetFacilityName(SpaceCenterFacility.Administration), " level ", value);
				case "AdministrationLevelMax": return Lib.BuildString(ScenarioUpgradeableFacilities.GetFacilityName(SpaceCenterFacility.Administration), " max. level ", value);
				case "TrackingStationLevelMin": return Lib.BuildString(ScenarioUpgradeableFacilities.GetFacilityName(SpaceCenterFacility.TrackingStation), " level ", value);
				case "TrackingStationLevelMax": return Lib.BuildString(ScenarioUpgradeableFacilities.GetFacilityName(SpaceCenterFacility.TrackingStation), " max. level ", value);
				case "AstronautComplexLevelMin": return Lib.BuildString(ScenarioUpgradeableFacilities.GetFacilityName(SpaceCenterFacility.AstronautComplex), " level ", value);
				case "AstronautComplexLevelMax": return Lib.BuildString(ScenarioUpgradeableFacilities.GetFacilityName(SpaceCenterFacility.AstronautComplex), " max. level ", value);

				case "Part": return Lib.BuildString("Needs part ", value);
				case "Module": return Lib.BuildString("Needs module ", value);

				default:
					return Lib.SpacesOnCaps(condition);
			}
		}

		public static string PrettyBodyText(string requires)
		{
			string result = "";
			foreach(var s in Lib.Tokenize(requires, ';'))
			{
				if (result.Length > 0) result += ", ";
				if (s[0] == '!') result += "not " + s.Substring(1);
				else result += s;
			}
			return result;
		}

		public static void RegisterSampleMass(string experiment_id, double sampleMass)
		{
			// get experiment id out of subject id
			int i = experiment_id.IndexOf('@');
			var id = i > 0 ? experiment_id.Substring(0, i) : experiment_id;

			if (sampleMasses.ContainsKey(id))
			{
				if (Math.Abs(sampleMasses[id] - sampleMass) > double.Epsilon)
					Lib.Log("Science Warning: different sample masses for Experiment " + id + " defined.");
			}
			else
			{
				sampleMasses.Add(id, sampleMass);
				Lib.Log("Science: registered sample mass for " + id + ": " + sampleMass.ToString("F3"));
			}
		}

		public static double GetSampleMass(string experiment_id)
		{
			// get experiment id out of subject id
			int i = experiment_id.IndexOf('@');
			var id = i > 0 ? experiment_id.Substring(0, i) : experiment_id;

			if (!sampleMasses.ContainsKey(id)) return 0;
			return sampleMasses[id];
		}

		// experiment info cache
		static readonly Dictionary<string, ExperimentInfo> experiments = new Dictionary<string, ExperimentInfo>();
		readonly static Dictionary<string, double> sampleMasses = new Dictionary<string, double>();

		private class DeferredCreditValues {
			internal string subject_id;
			internal double size;
			internal ProtoVessel pv;

			public DeferredCreditValues(string subject_id, double size, ProtoVessel pv)
			{
				this.subject_id = subject_id;
				this.size = size;
				this.pv = pv;
			}
		}

		static readonly Dictionary<string, DeferredCreditValues> deferredCredit = new Dictionary<string, DeferredCreditValues>();
	}

} // KERBALISM

