using System;
using System.Collections.Generic;
using Experience;
using UnityEngine;


namespace KERBALISM
{


	// EXPERIMENTAL
	public sealed class Experiment : PartModule, ISpecifics
	{
		// config
		[KSPField] public string experiment;          // id of associated experiment definition
		[KSPField] public string situations;          // comma-separed list of situations
		[KSPField] public double data_rate;           // sampling rate in Mb/s
		[KSPField] public double ec_rate;             // EC consumption rate per-second
		[KSPField] public bool transmissible = true;  // true if data can be transmitted
		[KSPField] public string crew = string.Empty;   // operator crew
		[KSPField] public string reset = string.Empty;  // reset crew. if set, experiments will stop working on situation change

		// animations
		[KSPField] public string deploy = string.Empty; // deploy animation

		// persistence
		[KSPField(isPersistant = true)] public bool recording;
		[KSPField(isPersistant = true)] public string issue = string.Empty;
		[KSPField(isPersistant = true)] public string last_subject_id = string.Empty;

		// animations
		Animator deploy_anim;

		// crew specs
		CrewSpecs operator_cs;
		CrewSpecs reset_cs;

		// experiment title
		string exp_name;


		public override void OnStart(StartState state)
		{
			// don't break tutorial scenarios
			if (Lib.DisableScenario(this)) return;

			// create animator
			deploy_anim = new Animator(part, deploy);

			// set initial animation state
			deploy_anim.Still(recording ? 1.0 : 0.0);

			// parse crew specs
			if(!string.IsNullOrEmpty(crew))
				operator_cs = new CrewSpecs(crew);
			if (!string.IsNullOrEmpty(reset))
				reset_cs = new CrewSpecs(reset);

			// get experiment title
			exp_name = ResearchAndDevelopment.GetExperiment(experiment).experimentTitle;
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

				// update ui
				Events["Toggle"].guiName = Lib.StatusToggle(exp_name, !recording ? "stopped" : issue.Length == 0 ? "recording" : Lib.BuildString("<color=#ffff00>", issue, "</color>"));
				Events["Reset"].guiName = Lib.BuildString("Reset <b>", exp_name, "</b>");
				Events["Reset"].active = reset_cs != null && reset_cs.Check(v);
			}
			// in the editor
			else if (Lib.IsEditor())
			{
				// update ui
				Events["Toggle"].guiName = Lib.StatusToggle(exp_name, recording ? "recording" : "stopped");
				Events["Reset"].active = false;
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

			// if experiment is active
			if (recording)
			{
				// detect conditions
				// - comparing against amount in previous step
				bool has_ec = ec.amount > double.Epsilon;
				bool has_operator = operator_cs == null || operator_cs.Check(vessel);
				string sit = Science.Situation(vessel, situations);

				// deduce issues
				issue = string.Empty;
				if (sit.Length == 0) issue = "invalid situation";
				else if (!has_operator) issue = "no operator";
				else if (!has_ec) issue = "missing <b>EC</b>";

				string subject_id = string.Empty;
				if (issue.Length == 0)
				{
					// generate subject id
					subject_id = Science.Generate_subject(experiment, vessel.mainBody, sit, Science.Biome(vessel, sit), Science.Multiplier(vessel, sit));
					bool needsReset = reset_cs != null
						&& !string.IsNullOrEmpty(last_subject_id) && subject_id != last_subject_id;

					if (needsReset) issue = "reset required";
				}

				// if there are no issues
				if (issue.Length == 0)
				{
					last_subject_id = subject_id;

					// record in drive
					if (transmissible)
						DB.Vessel(vessel).drive.Record_file(subject_id, data_rate * Kerbalism.elapsed_s, true, true);
					else
						DB.Vessel(vessel).drive.Record_sample(subject_id, data_rate * Kerbalism.elapsed_s);

					// consume ec
					ec.Consume(ec_rate * Kerbalism.elapsed_s);
				}
			}
		}


		public static void BackgroundUpdate(Vessel v, ProtoPartModuleSnapshot m, Experiment exp, Resource_info ec, double elapsed_s)
		{
			// if experiment is active
			if (!Lib.Proto.GetBool(m, "recording"))
				return;

			// detect conditions
			// - comparing against amount in previous step
			bool has_ec = ec.amount > double.Epsilon;
			bool has_operator = string.IsNullOrEmpty(exp.crew) || new CrewSpecs(exp.crew).Check(v);

			string sit = Science.Situation(v, exp.situations);

			// deduce issues
			string issue = string.Empty;
			if (sit.Length == 0) issue = "invalid situation";
			else if (!has_operator) issue = "no operator";
			else if (!has_ec) issue = "missing <b>EC</b>";

			string subject_id = string.Empty;
			if (issue.Length == 0)
			{
				// generate subject id
				subject_id = Science.Generate_subject(exp.experiment, v.mainBody, sit, Science.Biome(v, sit), Science.Multiplier(v, sit));
				bool needsReset = !string.IsNullOrEmpty(exp.reset)
					&& !string.IsNullOrEmpty(exp.last_subject_id) && subject_id != exp.last_subject_id;

				if (needsReset) issue = "reset required";
			}

			Lib.Proto.Set(m, "issue", issue);

			// if there are no issues
			if (issue.Length == 0)
			{
				Lib.Proto.Set(m, "last_subject_id", subject_id);

				// record in drive
				if (exp.transmissible)
					DB.Vessel(v).drive.Record_file(subject_id, exp.data_rate * elapsed_s, true, true);
				else
					DB.Vessel(v).drive.Record_sample(subject_id, exp.data_rate * elapsed_s);

				// consume ec
				ec.Consume(exp.ec_rate * elapsed_s);
			}
		}

		[KSPEvent(guiActiveUnfocused = true, guiName = "_", active = false)]
		public void Reset()
		{
			// disable for dead eva kerbals
			Vessel v = FlightGlobals.ActiveVessel;
			if (v == null || EVA.IsDead(v)) return;

			// check trait
			if (!reset_cs.Check(v))
			{
				Message.Post
				(
				  Lib.TextVariant
				  (
					"I'm not qualified for this",
					"I will not even know where to start",
					"I'm afraid I can't do that"
				  ),
				  reset_cs.Warning()
				);
				return;
			}

			last_subject_id = string.Empty;
		}

		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = true)]
		public void Toggle()
		{
			// toggle recording
			recording = !recording;

			// play deploy animation if exist
			deploy_anim.Play(!recording, false);

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
			var specs = new Specifics();
			specs.Add("Name", ResearchAndDevelopment.GetExperiment(experiment).experimentTitle);
			specs.Add("Data rate", Lib.HumanReadableDataRate(data_rate));
			specs.Add("EC required", Lib.HumanReadableRate(ec_rate));
			if (crew.Length > 0) specs.Add("Operator", new CrewSpecs(crew).Info());
			if (reset.Length > 0) specs.Add("Reset", new CrewSpecs(reset).Info());
			specs.Add(string.Empty);
			specs.Add("<color=#00ffff>Situations:</color>", string.Empty);
			var tokens = Lib.Tokenize(situations, ',');
			foreach (string s in tokens) specs.Add(Lib.BuildString("• <b>", s, "</b>"));
			return specs;
		}
	}


} // KERBALISM

