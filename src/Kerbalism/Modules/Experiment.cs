using System;
using System.Collections.Generic;
using Experience;
using UnityEngine;


namespace KERBALISM
{


	// EXPERIMENTAL
	public sealed class Experiment : PartModule, ISpecifics, IPartMassModifier
	{
		// config
		[KSPField] public string experiment_id;               // id of associated experiment definition
		[KSPField] public string experiment_desc = string.Empty;  // some nice lines of text
		[KSPField] public double data_rate;                   // sampling rate in Mb/s
		[KSPField] public double ec_rate;                     // EC consumption rate per-second
		[KSPField] public float sample_mass = 0f;             // if set to anything but 0, the experiment is a sample.
		[KSPField] public bool sample_collecting = false;     // if set to true, the experiment will generate mass out of nothing
		[KSPField] public bool allow_shrouded = true;         // true if data can be transmitted

		[KSPField] public string requires = string.Empty;     // additional requirements that must be met

		[KSPField] public string crew_operate = string.Empty; // operator crew. if set, crew has to be on vessel while recording
		[KSPField] public string crew_reset = string.Empty;   // reset crew. if set, experiment will stop recording after situation change
		[KSPField] public string crew_prepare = string.Empty; // prepare crew. if set, experiment will require crew to set up before it can start recording 

		// animations
		[KSPField] public string anim_deploy = string.Empty; // deploy animation

		// persistence
		[KSPField(isPersistant = true)] public bool recording;
		[KSPField(isPersistant = true)] public string issue = string.Empty;
		[KSPField(isPersistant = true)] public string last_subject_id = string.Empty;
		[KSPField(isPersistant = true)] public bool didPrepare = false;
		[KSPField(isPersistant = true)] public double dataSampled = 0.0;
		[KSPField(isPersistant = true)] public bool shrouded = false;
		[KSPField(isPersistant = true)] public double remainingSampleMass = -1;
		[KSPField(isPersistant = true)] public bool broken = false;

		// animations
		Animator deployAnimator;

		CrewSpecs operator_cs;
		CrewSpecs reset_cs;
		CrewSpecs prepare_cs;

		private ScienceExperiment exp;
		private String situationIssue = String.Empty;
		[KSPField(guiActive = false, guiName = "_")] private string ExperimentStatus = string.Empty;

		public override void OnLoad(ConfigNode node)
		{
			// build up science sample mass database
			if (HighLogic.LoadedScene == GameScenes.LOADING)
			{
				Science.RegisterSampleMass(experiment_id, sample_mass);
			}
		}

		public override void OnStart(StartState state)
		{
			// don't break tutorial scenarios
			if (Lib.DisableScenario(this)) return;

			if(remainingSampleMass < 0) remainingSampleMass = sample_mass;

			// create animator
			deployAnimator = new Animator(part, anim_deploy);

			// set initial animation state
			deployAnimator.Still(recording ? 1.0 : 0.0);

			// parse crew specs
			if(!string.IsNullOrEmpty(crew_operate))
				operator_cs = new CrewSpecs(crew_operate);
			if (!string.IsNullOrEmpty(crew_reset))
				reset_cs = new CrewSpecs(crew_reset);
			if (!string.IsNullOrEmpty(crew_prepare))
				prepare_cs = new CrewSpecs(crew_prepare);

			exp = ResearchAndDevelopment.GetExperiment(experiment_id);
		}

		public void Update()
		{
			// in flight
			if (Lib.IsFlight())
			{
				Vessel v = FlightGlobals.ActiveVessel;
				if (v == null || EVA.IsDead(v)) return;

				// get info from cache
				Vessel_info vi = Cache.VesselInfo(vessel);

				// do nothing if vessel is invalid
				if (!vi.is_valid) return;

				var sampleSize = (exp.scienceCap * exp.dataScale);
				var recordedPercent = Lib.HumanReadablePerc(dataSampled / sampleSize);
				var eta = data_rate < double.Epsilon || dataSampled >= sampleSize ? " done" : " T-" + Lib.HumanReadableDuration((sampleSize - dataSampled) / data_rate);

				// update ui
				Events["Toggle"].guiName = Lib.StatusToggle(exp.experimentTitle, !recording ? "stopped" : issue.Length == 0 ? "recording " + Lib.HumanReadablePerc(dataSampled / sampleSize) : Lib.BuildString("<color=#ffff00>", issue, "</color>"));
				Events["Toggle"].active = (prepare_cs == null || didPrepare) && issue.Length == 0;

				Events["Prepare"].guiName = Lib.BuildString("Prepare <b>", exp.experimentTitle, "</b>");
				Events["Prepare"].active = !didPrepare && prepare_cs != null && string.IsNullOrEmpty(last_subject_id);

				Events["Reset"].guiName = Lib.BuildString("Reset <b>", exp.experimentTitle, "</b>");
				// we need a reset either if we have recorded data or did a setup
				bool resetActive = sample_mass < float.Epsilon && (reset_cs != null || prepare_cs != null) && !string.IsNullOrEmpty(last_subject_id);
				Events["Reset"].active = resetActive;

				Fields["ExperimentStatus"].guiName = exp.experimentTitle;
				Fields["ExperimentStatus"].guiActive = true;

				if (issue.Length > 0)
					ExperimentStatus = Lib.BuildString("<color=#ffff00>", issue, "</color>");
				else if (dataSampled > 0)
				{
					string a = string.Empty;
					string b = string.Empty;

					if (sample_mass < float.Epsilon)
					{
						a = Lib.HumanReadableDataSize(dataSampled);
						b = Lib.HumanReadableDataSize(sampleSize);
					}
					else
					{
						a = Lib.HumanReadableSampleSize(dataSampled);
						b = Lib.HumanReadableSampleSize(sampleSize);
					}

					ExperimentStatus = Lib.BuildString(a, "/", b, eta);
				}
				else
					ExperimentStatus = Lib.BuildString("ready ",
						Lib.HumanReadableDataSize(sampleSize), " in ", Lib.HumanReadableDuration(sampleSize / data_rate));
			}
			// in the editor
			else if (Lib.IsEditor())
			{
				// update ui
				Events["Toggle"].guiName = Lib.StatusToggle(exp.experimentTitle, recording ? "recording" : "stopped");
				Events["Reset"].active = false;
				Events["Prepare"].active = false;
			}
		}

		public void FixedUpdate()
		{
			// do nothing in the editor
			if (Lib.IsEditor()) return;

			// do nothing if vessel is invalid
			if (!Cache.VesselInfo(vessel).is_valid) return;

			// get ec handler
			Resource_info ec = ResourceCache.Info(vessel, "ElectricCharge");
			shrouded = part.ShieldedFromAirstream;
			string subject_id;
			issue = TestForIssues(vessel, exp, ec, this, broken,
				remainingSampleMass, didPrepare, shrouded, last_subject_id, out subject_id);

			// if experiment is active and there are no issues
			if (recording && issue.Length == 0 && dataSampled < exp.scienceCap * exp.dataScale)
			{
				if (last_subject_id != subject_id) dataSampled = 0;
				last_subject_id = subject_id;

				// record in drive
				double elapsed = Kerbalism.elapsed_s;

				double chunkSize = data_rate * elapsed;
				var info = Science.Experiment(subject_id);
				double massDelta = sample_mass * chunkSize / info.max_amount;

				bool isSample = sample_mass < float.Epsilon;
				var drive = isSample
						   ? DB.Vessel(vessel).BestDrive(chunkSize, 0)
						   : DB.Vessel(vessel).BestDrive(0, Lib.SampleSizeToSlots(chunkSize));

				// on high time warp this chunk size could be too big, but we could store a sizable amount if we processed less
				double maxCapacity = isSample ? drive.FileCapacityAvailable() : drive.SampleCapacityAvailable(subject_id);
				if(maxCapacity < chunkSize) {
					double factor = maxCapacity / chunkSize;
					chunkSize *= factor;
					massDelta *= factor;
					elapsed *= factor;
				}

				bool stored = false;
				if (sample_mass < float.Epsilon)
					stored = drive.Record_file(subject_id, chunkSize, true, true);
				else
					stored = drive.Record_sample(subject_id, chunkSize, massDelta);

				if(stored)
				{
					// consume ec
					ec.Consume(ec_rate * elapsed);
					dataSampled += chunkSize;
					dataSampled = Math.Min(dataSampled, exp.scienceCap * exp.dataScale);
					if(!sample_collecting)
					{
						remainingSampleMass -= massDelta;
						remainingSampleMass = Math.Max(remainingSampleMass, 0);
					}
				}
				else
				{
					issue = "insufficient storage";
				}
			}
		}


		public static void BackgroundUpdate(Vessel v, ProtoPartModuleSnapshot m, Experiment experiment, Resource_info ec, double elapsed_s)
		{
			var exp = ResearchAndDevelopment.GetExperiment(experiment.experiment_id);
			bool didPrepare = Lib.Proto.GetBool(m, "didPrepare", false);
			bool shrouded = Lib.Proto.GetBool(m, "shrouded", false);
			string last_subject_id = Lib.Proto.GetString(m, "last_subject_id", "");
			double remainingSampleMass = Lib.Proto.GetDouble(m, "remainingSampleMass", 0);
			bool broken = Lib.Proto.GetBool(m, "broken", false);

			string subject_id;
			string issue = TestForIssues(v, exp, ec, experiment, broken,
				remainingSampleMass, didPrepare, shrouded, last_subject_id, out subject_id);
			Lib.Proto.Set(m, "issue", issue);

			// if experiment is active
			if (!Lib.Proto.GetBool(m, "recording"))
				return;

			double dataSampled = Lib.Proto.GetDouble(m, "dataSampled");
			if (last_subject_id != subject_id) dataSampled = 0;

			// if there are no issues
			if (issue.Length == 0 && dataSampled < exp.scienceCap * exp.dataScale)
			{
				Lib.Proto.Set(m, "last_subject_id", subject_id);

				// record in drive
				double chunkSize = experiment.data_rate * elapsed_s;
				var info = Science.Experiment(subject_id);
				double massDelta = experiment.sample_mass * chunkSize / info.max_amount;

				bool isSample = experiment.sample_mass < float.Epsilon;
				var drive = isSample
						   ? DB.Vessel(v).BestDrive(chunkSize, 0)
						   : DB.Vessel(v).BestDrive(0, Lib.SampleSizeToSlots(chunkSize));

				// on high time warp this chunk size could be too big, but we could store a sizable amount if we processed less
				double maxCapacity = isSample ? drive.FileCapacityAvailable() : drive.SampleCapacityAvailable(subject_id);
				if (maxCapacity < chunkSize)
				{
					double factor = maxCapacity / chunkSize;
					chunkSize *= factor;
					massDelta *= factor;
					elapsed_s *= factor;
				}

				bool stored = false;
				if (experiment.sample_mass < float.Epsilon)
					stored = drive.Record_file(subject_id, chunkSize, true, true);
				else
					stored = drive.Record_sample(subject_id, chunkSize, massDelta);

				if (stored)
				{
					// consume ec
					ec.Consume(experiment.ec_rate * elapsed_s);
					dataSampled += chunkSize;
					dataSampled = Math.Min(dataSampled, exp.scienceCap * exp.dataScale);

					if (!experiment.sample_collecting)
					{
						remainingSampleMass -= massDelta;
						remainingSampleMass = Math.Max(remainingSampleMass, 0);
						Lib.Proto.Set(m, "remainingSampleMass", remainingSampleMass);
					}

					Lib.Proto.Set(m, "dataSampled", dataSampled);
				}
				else
				{
					issue = "insufficient storage";
					Lib.Proto.Set(m, "issue", issue);
				}
			}
		}

		public void ReliablityEvent(bool breakdown)
		{
			broken = breakdown;
		}

		private static string TestForIssues(Vessel v, ScienceExperiment exp, Resource_info ec, Experiment experiment, bool broken,
			double remainingSampleMass, bool didPrepare, bool isShrouded, string last_subject_id, out string subject_id)
		{
			var sit = ScienceUtil.GetExperimentSituation(v);
			var biome = ScienceUtil.GetExperimentBiome(v.mainBody, v.latitude, v.longitude);
			subject_id = Science.Generate_subject(exp, v.mainBody, sit, biome);

			if (broken)
				return "broken";

			if (isShrouded && !experiment.allow_shrouded)
				return "shrouded";
			
			bool needsReset = experiment.crew_reset.Length > 0
				&& !string.IsNullOrEmpty(last_subject_id) && subject_id != last_subject_id;
			if (needsReset) return "reset required";

			if (ec.amount < double.Epsilon && experiment.ec_rate > double.Epsilon)
				return "missing <b>EC</b>";
			
			if (!string.IsNullOrEmpty(experiment.crew_operate))
			{
				var cs = new CrewSpecs(experiment.crew_operate);
				if (!cs.Check(v)) return cs.Warning();
			}

			if (!experiment.sample_collecting && remainingSampleMass < double.Epsilon && experiment.sample_mass > double.Epsilon)
				return "depleted";

			string situationIssue = Science.TestRequirements(experiment.requires, v);
			if (situationIssue.Length > 0)
				return Science.RequirementText(situationIssue);
			
			if (!exp.IsAvailableWhile(sit, v.mainBody))
				return "invalid situation";

			if (!didPrepare && !string.IsNullOrEmpty(experiment.crew_prepare))
				return "not prepared";

			var drive = DB.Vessel(v).BestDrive();
			double available = experiment.sample_mass < float.Epsilon ? drive.FileCapacityAvailable() : drive.SampleCapacityAvailable();
			if (experiment.data_rate * Kerbalism.elapsed_s > available)
				return "insufficient storage";

			return string.Empty;
		}

		[KSPEvent(guiActiveUnfocused = true, guiName = "_", active = false)]
		public void Prepare()
		{
			// disable for dead eva kerbals
			Vessel v = FlightGlobals.ActiveVessel;
			if (v == null || EVA.IsDead(v))
				return;

			if (prepare_cs == null)
				return;

			// check trait
			if (!prepare_cs.Check(v))
			{
				Message.Post(
				  Lib.TextVariant
				  (
					"I'm not qualified for this",
					"I will not even know where to start",
					"I'm afraid I can't do that"
				  ),
				  reset_cs.Warning()
				);
			}

			// generate subject id
			var sit = ScienceUtil.GetExperimentSituation(v);
			var biome = ScienceUtil.GetExperimentBiome(vessel.mainBody, vessel.latitude, vessel.longitude);
			last_subject_id = Science.Generate_subject(exp, vessel.mainBody, sit, biome);
			didPrepare = true;

			Message.Post(
			  "Preparation Complete",
			  Lib.TextVariant
			  (
				"Ready to go",
				"Let's start doing some science!"
			  )
			);
		}

		[KSPEvent(guiActiveUnfocused = true, guiName = "_", active = false)]
		public void Reset()
		{
			Reset(true);
		}

		public bool Reset(bool showMessage)
		{
			// disable for dead eva kerbals
			Vessel v = FlightGlobals.ActiveVessel;
			if (v == null || EVA.IsDead(v))
				return false;

			if (reset_cs == null)
				return false;

			// check trait
			if (!reset_cs.Check(v))
			{
				if(showMessage)
				{
					Message.Post(
					  Lib.TextVariant
					  (
						"I'm not qualified for this",
						"I will not even know where to start",
						"I'm afraid I can't do that"
					  ),
					  reset_cs.Warning()
					);
				}
				return false;
			}

			last_subject_id = string.Empty;
			didPrepare = false;

			if(showMessage)
			{
				Message.Post(
				  "Reset Done",
				  Lib.TextVariant
				  (
					"It's good to go again",
					"Ready for the next bit of science"
				  )
				);
			}
			return true; 
		}

		[KSPEvent(guiActiveUnfocused = true, guiActive = true, guiActiveEditor = true, guiName = "_", active = true)]
		public void Toggle()
		{
			// toggle recording
			recording = !recording;

			// play deploy animation if exist
			deployAnimator.Play(!recording, false);

			// refresh VAB/SPH ui
			if (Lib.IsEditor()) GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
		}

		// action groups
		[KSPAction("#KERBALISM_Experiment_Action")] public void Action(KSPActionParam param) { Toggle(); }

		// part tooltip
		public override string GetInfo()
		{
			return Specs().Info();
		}

		// specifics support
		public Specifics Specs()
		{
			if(exp == null) exp = ResearchAndDevelopment.GetExperiment(experiment_id);

			var specs = new Specifics();
			specs.Add(Lib.BuildString("<b>", ResearchAndDevelopment.GetExperiment(experiment_id).experimentTitle, "</b>"));

			if(!string.IsNullOrEmpty(experiment_desc))
			{
				specs.Add(Lib.BuildString("<i>", experiment_desc, "</i>"));
			}
			specs.Add(string.Empty);

			double expSize = exp.scienceCap * exp.dataScale;
			if (sample_mass < float.Epsilon)
			{
				specs.Add("Data", Lib.HumanReadableDataSize(expSize));
				specs.Add("Data rate", Lib.HumanReadableDataRate(data_rate));
				specs.Add("Duration", Lib.HumanReadableDuration(expSize / data_rate));
			}
			else
			{
				specs.Add("Sample size", Lib.HumanReadableSampleSize(expSize));
				specs.Add("Sample mass", Lib.HumanReadableMass(sample_mass));
				specs.Add("Duration", Lib.HumanReadableDuration(expSize / data_rate));
			}

			specs.Add("EC required", Lib.HumanReadableRate(ec_rate));
			if (crew_operate.Length > 0) specs.Add("Opration", new CrewSpecs(crew_operate).Info());
			if (crew_reset.Length > 0) specs.Add("Reset", new CrewSpecs(crew_reset).Info());
			if (crew_prepare.Length > 0) specs.Add("Preparation", new CrewSpecs(crew_prepare).Info());

			if(!string.IsNullOrEmpty(requires))
			{
				specs.Add(string.Empty);
				specs.Add("<color=#00ffff>Requirements:</color>", string.Empty);
				var tokens = Lib.Tokenize(requires, ',');
				foreach (string s in tokens) specs.Add(Lib.BuildString("• <b>", Science.RequirementText(s), "</b>"));
			}
			return specs;
		}

		// module mass support
		public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) { return sample_collecting ? 0 : (float)remainingSampleMass; }
		public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
	}


} // KERBALISM

