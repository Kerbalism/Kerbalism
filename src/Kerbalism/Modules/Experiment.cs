﻿using System;
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

		private ScienceExperiment exp;

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
			exp = ResearchAndDevelopment.GetExperiment(experiment);
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
				Events["Toggle"].guiName = Lib.StatusToggle(exp.experimentTitle, !recording ? "stopped" : issue.Length == 0 ? "recording" : Lib.BuildString("<color=#ffff00>", issue, "</color>"));
				Events["Reset"].guiName = Lib.BuildString("Reset <b>", exp.experimentTitle, "</b>");
				Events["Reset"].active = reset_cs != null && reset_cs.Check(v) && !string.IsNullOrEmpty(last_subject_id);
			}
			// in the editor
			else if (Lib.IsEditor())
			{
				// update ui
				Events["Toggle"].guiName = Lib.StatusToggle(exp.experimentTitle, recording ? "recording" : "stopped");
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

				var sit = ScienceUtil.GetExperimentSituation(vessel);

				// deduce issues
				issue = string.Empty;
				if (!exp.IsAvailableWhile(sit, vessel.mainBody)) issue = "invalid situation";
				else if (!has_operator) issue = "no operator";
				else if (!has_ec) issue = "missing <b>EC</b>";

				string subject_id = string.Empty;
				if (issue.Length == 0)
				{
					// generate subject id
					var biome = ScienceUtil.GetExperimentBiome(vessel.mainBody, vessel.latitude, vessel.longitude);
					subject_id = Science.Generate_subject(exp, vessel.mainBody, sit, biome);

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


		public static void BackgroundUpdate(Vessel v, ProtoPartModuleSnapshot m, Experiment experiment, Resource_info ec, double elapsed_s)
		{
			// if experiment is active
			if (!Lib.Proto.GetBool(m, "recording"))
				return;

			// detect conditions
			// - comparing against amount in previous step
			bool has_ec = ec.amount > double.Epsilon;
			bool has_operator = string.IsNullOrEmpty(experiment.crew) || new CrewSpecs(experiment.crew).Check(v);

			var sit = ScienceUtil.GetExperimentSituation(v);
			var exp = ResearchAndDevelopment.GetExperiment(experiment.experiment);

			// deduce issues
			string issue = string.Empty;
			if (!exp.IsAvailableWhile(sit, v.mainBody)) issue = "invalid situation";
			else if (!has_operator) issue = "no operator";
			else if (!has_ec) issue = "missing <b>EC</b>";

			string subject_id = string.Empty;
			if (issue.Length == 0)
			{
				// generate subject id
				var biome = ScienceUtil.GetExperimentBiome(v.mainBody, v.latitude, v.longitude);
				subject_id = Science.Generate_subject(exp, v.mainBody, sit, biome);

				var last_subject_id = Lib.Proto.GetString(m, "last_subject_id", "");
				bool needsReset = !string.IsNullOrEmpty(experiment.reset)
				                         && !string.IsNullOrEmpty(last_subject_id) && subject_id != last_subject_id;

				if (needsReset) issue = "reset required";
			}

			Lib.Proto.Set(m, "issue", issue);

			// if there are no issues
			if (issue.Length == 0)
			{
				Lib.Proto.Set(m, "last_subject_id", subject_id);

				// record in drive
				if (experiment.transmissible)
					DB.Vessel(v).drive.Record_file(subject_id, experiment.data_rate * elapsed_s, true, true);
				else
					DB.Vessel(v).drive.Record_sample(subject_id, experiment.data_rate * elapsed_s);

				// consume ec
				ec.Consume(experiment.ec_rate * elapsed_s);
			}
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
			return specs;
		}
	}


} // KERBALISM

