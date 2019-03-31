﻿using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;


namespace KERBALISM
{


	public static class Science
	{
		// hard-coded transmission buffer size in Mb
		private const double buffer_capacity = 12.0;

		// pseudo-ctor
		public static void Init()
		{
			experiments = new Dictionary<string, ExperimentInfo>();

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

		private static Drive FindDrive(VesselData vd, string filename)
		{
			foreach (var d in vd.drives.Values)
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
			if (conn == null || String.IsNullOrEmpty(vi.transmitting)) return;

			// get filename of data being downloaded
			var exp_filename = vi.transmitting;

			var drive = FindDrive(vd, exp_filename);

			// if some data is being downloaded
			// - avoid cornercase at scene changes
			if (exp_filename.Length > 0 && drive != null)
			{
				// get file
				File file = drive.files[exp_filename];

				// determine how much data is transmitted
				double transmitted = Math.Min(file.size, conn.rate * elapsed_s);

				// consume data in the file
				file.size -= transmitted;

				// accumulate in the buffer
				file.buff += transmitted;

				// if buffer is full, or file was transmitted completely
				if (file.size <= double.Epsilon || file.buff > buffer_capacity)
				{
					// collect the science data
					Credit(exp_filename, file.buff, true, v.protoVessel);

					// reset the buffer
					file.buff = 0.0;
				}

				// if file was transmitted completely
				if (file.size <= double.Epsilon)
				{
					// remove the file
					drive.files.Remove(exp_filename);

					// same file on another drive?
					drive = FindDrive(vd, exp_filename);

					if (!file.silentTransmission && drive == null)
					{
						// inform the user
						Message.Post(
						  Lib.BuildString("<color=cyan><b>DATA RECEIVED</b></color>\nTransmission of <b>", Experiment(exp_filename).name, "</b> completed"),
						  Lib.TextVariant("Our researchers will jump on it right now", "The checksum is correct, data must be valid"));
					}
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
			if (ResourceCache.Info(v, "ElectricCharge").amount <= double.Epsilon) return string.Empty;

			// get first file flagged for transmission, AND has a ts at least 5 seconds old or is > 0.001Mb in size
			foreach (var drive in DB.Vessel(v).drives.Values)
			{
				double now = Planetarium.GetUniversalTime();
				foreach (var p in drive.files)
				{
					if (p.Value.send && (p.Value.ts + 3 < now || p.Value.size > 0.003)) return p.Key;
				}
			}

			// no file flagged for transmission
			return string.Empty;
		}


		// credit science for the experiment subject specified
		public static float Credit(string subject_id, double size, bool transmitted, ProtoVessel pv)
		{
			var credits = Value(subject_id, size);

			// credit the science
			var subject = ResearchAndDevelopment.GetSubjectByID(subject_id);
			subject.science += credits / HighLogic.CurrentGame.Parameters.Career.ScienceGainMultiplier;
			subject.scientificValue = ResearchAndDevelopment.GetSubjectValue(subject.science, subject);
			ResearchAndDevelopment.Instance.AddScience(credits, transmitted ? TransactionReasons.ScienceTransmission : TransactionReasons.VesselRecovery);

			// fire game event
			// - this could be slow or a no-op, depending on the number of listeners
			//   in any case, we are buffering the transmitting data and calling this
			//   function only once in a while
			GameEvents.OnScienceRecieved.Fire(credits, subject, pv, false);

			// return amount of science credited
			return credits;
		}


		// return value of some data about a subject, in science credits
		public static float Value(string subject_id, double size)
		{
			// get science subject
			// - if null, we are in sandbox mode
			var subject = ResearchAndDevelopment.GetSubjectByID(subject_id);
			if (subject == null) return 0.0f;

			// get science value
			// - the stock system 'degrade' science value after each credit, we don't
			float R = ResearchAndDevelopment.GetReferenceDataValue((float)size, subject);
			float S = subject.science;
			float C = subject.scienceCap;
			float credits = Mathf.Max(Mathf.Min(S + Mathf.Min(R, C), C) - S, 0.0f);

			credits *= HighLogic.CurrentGame.Parameters.Career.ScienceGainMultiplier;

			return credits;
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


		// create a new subject entry in the RnD
		// - experiment: experiment_id
		// - situation: an arbitrary situation, can insert biome at the end
		// - body: celestial body involved
		// - biome: biome involved, or empty
		// - multiplier: science multiplier for the body/situation
		public static string Generate_subject(ScienceExperiment experiment, CelestialBody body, ExperimentSituations sit, string biome)
		{
			// generate subject id
			string subject_id = Lib.BuildString(experiment.id, "@", body.name, sit + (experiment.BiomeIsRelevantWhile(sit) ? biome : ""));

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

				float multiplier = Multiplier(body, sit);
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

		private static float Multiplier(CelestialBody body, ExperimentSituations sit)
		{
			var values = body.scienceValues;
			switch(sit)
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

		public static string TestRequirements(string requirements, Vessel v)
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
					case "OrbitMinInclination": good = Math.Abs(v.orbit.inclination) >= Double.Parse(value); break;
					case "OrbitMaxInclination": good = Math.Abs(v.orbit.inclination) <= Double.Parse(value); break;
					case "OrbitMinEccentricity": good = Math.Abs(v.orbit.eccentricity) >= Double.Parse(value); break;
					case "OrbitMaxEccentricity": good = Math.Abs(v.orbit.eccentricity) <= Double.Parse(value); break;

					case "TemperatureMin": good = vi.temperature >= Double.Parse(value); break;
					case "TemperatureMax": good = vi.temperature <= Double.Parse(value); break;
					case "AltitudeMin": good = v.altitude >= Double.Parse(value); break;
					case "AltitudeMax": good = v.altitude <= Double.Parse(value); break;
					case "RadiationMin": good = vi.radiation >= Double.Parse(value); break;
					case "RadiationMax": good = vi.radiation <= Double.Parse(value); break;
					case "Microgravity": good = vi.zerog; break;
					case "Body": good = v.mainBody.name == value; break;
					case "Shadow": good = vi.sunlight < Double.Epsilon; break;
					case "CrewMin": good = vi.crew_count >= int.Parse(value); break;
					case "CrewMax": good = vi.crew_count <= int.Parse(value); break;
					case "CrewCapacityMin": good = vi.crew_capacity >= int.Parse(value); break;
					case "CrewCapacityMax": good = vi.crew_capacity <= int.Parse(value); break;
					case "VolumePerCrewMin": good = vi.volume_per_crew >= Double.Parse(value); break;
					case "VolumePerCrewMax": good = vi.volume_per_crew <= Double.Parse(value); break;
					case "Greenhouse": good = vi.greenhouses.Count > 0; break;
					case "Surface": good = Lib.Landed(v); break;
					case "Atmosphere": good = body.atmosphere && v.altitude < body.atmosphereDepth; break;
					case "Ocean": good = body.ocean && v.altitude < 0.0; break;
					case "Space": good = body.flightGlobalsIndex != 0 && !Lib.Landed(v) && v.altitude > body.atmosphereDepth; break;
					case "AbsoluteZero": good = vi.temperature < 30.0; break;
					case "InnerBelt": good = vi.inner_belt; break;
					case "OuterBelt": good = vi.outer_belt; break;
					case "MagneticBelt": good = vi.inner_belt || vi.outer_belt; break;
					case "Magnetosphere": good = vi.magnetosphere; break;
					case "Thermosphere": good = vi.thermosphere; break;
					case "Exosphere": good = vi.exosphere; break;
					case "InterPlanetary": good = body.flightGlobalsIndex == 0 && !vi.interstellar; break;
					case "InterStellar": good = body.flightGlobalsIndex == 0 && vi.interstellar; break;
				}

				if (!good) return s;
			}

			return string.Empty;
		}

		public static string RequirementText(string requirement)
		{
			var parts = Lib.Tokenize(requirement, ':');

			var condition = parts[0];
			string value = string.Empty;
			if (parts.Count > 1) value = parts[1];
						
			switch (condition)
			{
				case "OrbitMinInclination": return Lib.BuildString("Min. inclination ", value);
				case "OrbitMaxInclination": return Lib.BuildString("Max. inclination ", value);
				case "OrbitMinEccentricity": return Lib.BuildString("Min. eccentricity ", value);
				case "OrbitMaxEccentricity": return Lib.BuildString("Max. eccentricity ", value);
				case "AltitudeMin": return Lib.BuildString("Min. altitude ", Lib.HumanReadableRange(Double.Parse(value)));
				case "AltitudeMax": return Lib.BuildString("Max. altitude ", Lib.HumanReadableRange(Double.Parse(value)));
				case "RadiationMin": return Lib.BuildString("Min. radiation ", Lib.HumanReadableRadiation(Double.Parse(value)));
				case "RadiationMax": return Lib.BuildString("Max. radiation ", Lib.HumanReadableRadiation(Double.Parse(value)));
				case "Body": return value;
				case "TemperatureMin": return Lib.BuildString("Min. temperature ", Lib.HumanReadableTemp(Double.Parse(value)));
				case "TemperatureMax": return Lib.BuildString("Max. temperature ", Lib.HumanReadableTemp(Double.Parse(value)));
				case "CrewMin": return Lib.BuildString("Min. crew ", value);
				case "CrewMax": return Lib.BuildString("Max. crew ", value);
				case "CrewCapacityMin": return Lib.BuildString("Min. crew capacity ", value);
				case "CrewCapacityMax": return Lib.BuildString("Max. crew capacity ", value);
				case "VolumePerCrewMin": return Lib.BuildString("Min. vol./crew ", value);
				case "VolumePerCrewMax": return Lib.BuildString("Max. vol./crew ", value);
					
				default:
					return Lib.SpacesOnCaps(condition);
			}
		}

		public static void RegisterSampleMass(string experiment_id, double sampleMass)
		{
			// get experiment id out of subject id
			int i = experiment_id.IndexOf('@');
			var id = i > 0 ? experiment_id.Substring(0, i) : experiment_id;

			if (sampleMasses.ContainsKey(id) && sampleMasses[id] - sampleMass > double.Epsilon)
			{
				Lib.Log("Warning: different sample masses for Experiment " + id + " defined.");
			}
			sampleMasses.Add(id, sampleMass);
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
		static Dictionary<string, ExperimentInfo> experiments;
		readonly static Dictionary<string, double> sampleMasses = new Dictionary<string, double>();

	}

} // KERBALISM

