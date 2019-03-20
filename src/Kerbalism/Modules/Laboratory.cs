using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{

	public sealed class Laboratory: PartModule, IModuleInfo, ISpecifics, IContractObjectiveModule
	{
		// config
		[KSPField] public double ec_rate;						// ec consumed per-second
		[KSPField] public double analysis_rate;					// analysis speed in Mb/s
		[KSPField] public string researcher = string.Empty;		// required crew for analysis
		[KSPField] public bool cleaner = true;					// can clean experiments

		// persistence
		[KSPField(isPersistant = true)] public bool running;	// true if the lab is active

		// status enum
		private enum Status
		{
			DISABLED = 0,
			NO_EC,
			NO_SAMPLE,
			NO_RESEARCHER,
			RUNNING
		}

		// other data
		private CrewSpecs researcher_cs;                            // crew specs for the researcher
		private static CrewSpecs background_researcher_cs;          // crew specs for the researcher in background simulation
		private string current_sample = null;                       // sample currently being analyzed
		private static string background_sample = null;             // sample currently being analyzed in background simulation
		private Status status = Status.DISABLED;                    // laboratory status
		private string status_txt = string.Empty;                   // status string to show next to the ui button
		private Resource_info ec = null;							// resource info for EC

		// localized strings
		private static readonly string localized_title = Lib.BuildString("<size=1><color=#00000000>00</color></size>", Localizer.Format("#KERBALISM_Laboratory_Title"));
		private static readonly string localized_toggle = Localizer.Format("#KERBALISM_Laboratory_Toggle");
		private static readonly string localized_enabled = Localizer.Format("#KERBALISM_Generic_ENABLED");
		private static readonly string localized_disabled = Localizer.Format("#KERBALISM_Generic_DISABLED");
		private static readonly string localized_noEC = Lib.Color("yellow", Localizer.Format("#KERBALISM_Laboratory_NoEC"));
		private static readonly string localized_noSample = Localizer.Format("#KERBALISM_Laboratory_NoSample");
		private static readonly string localized_cleaned = Localizer.Format("#KERBALISM_Laboratory_Cleaned");
		private static readonly string localized_results = Localizer.Format("#KERBALISM_Laboratory_Results");

		public override void OnStart(StartState state)
		{
			// don't break tutorial scenarios
			if (Lib.DisableScenario(this)) return;

			// set UI text
			Actions["Action"].guiName = Localizer.Format("#KERBALISM_Laboratory_Action");
			Events["CleanExperiments"].guiName = Localizer.Format("#KERBALISM_Laboratory_Clean");

			// do nothing in the editors and when compiling parts
			if (!Lib.IsFlight()) return;

			// parse crew specs
			researcher_cs = new CrewSpecs(researcher);
		}

		public void Update()
		{
			if (Lib.IsFlight())
			{
				// get status text
				SetStatusText();
				Events["Toggle"].guiName = Lib.StatusToggle(localized_toggle, status_txt);

				// if a cleaner and either a researcher is not required, or the researcher is present
				if (cleaner && (!researcher_cs || researcher_cs.Check(part.protoModuleCrew))) Events["CleanExperiments"].active = true;
				else Events["CleanExperiments"].active = false;
			}
			else Events["Toggle"].guiName = Lib.StatusToggle(localized_toggle, running ? localized_enabled : localized_disabled);
		}

		public void FixedUpdate()
		{
			// do nothing in the editor
			if (Lib.IsEditor()) return;

			// if enabled
			if (running)
			{
				// if a researcher is not required, or the researcher is present
				if (!researcher_cs || researcher_cs.Check(part.protoModuleCrew))
				{
					// get next sample to analyze
					current_sample = NextSample(vessel);

					double rate = analysis_rate;
					if(researcher_cs) {
						int bonus = researcher_cs.Bonus(part.protoModuleCrew);
						double crew_gain = 1 + bonus * Settings.LaboratoryCrewLevelBonus;
						crew_gain = Lib.Clamp(crew_gain, 1, Settings.MaxLaborartoryBonus);
						rate *= crew_gain;
					}

					// if there is a sample to analyze
					if (current_sample != null)
					{
						// consume EC
						ec = ResourceCache.Info(vessel, "ElectricCharge");
						ec.Consume(ec_rate * Kerbalism.elapsed_s);

						// if there was ec
						// - comparing against amount in previous simulation step
						if (ec.amount > double.Epsilon)
						{
							// analyze the sample
							Analyze(vessel, current_sample, rate * Kerbalism.elapsed_s);
							status = Status.RUNNING;
						}
						// if there was no ec
						else status = Status.NO_EC;
					}
					// if there is no sample to analyze
					else status = Status.NO_SAMPLE;
				}
				// if a researcher is required, but missing
				else status = Status.NO_RESEARCHER;
			}
			// if disabled
			else status = Status.DISABLED;
		}

		public static void BackgroundUpdate(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, Laboratory lab, Resource_info ec, double elapsed_s)
		{
			// if enabled
			if (Lib.Proto.GetBool(m, "running"))
			{
				// if a researcher is not required, or the researcher is present
				background_researcher_cs = new CrewSpecs(lab.researcher);
				if (!background_researcher_cs || background_researcher_cs.Check(p.protoModuleCrew))
				{
					double rate = lab.analysis_rate;
					if(background_researcher_cs) {
						int bonus = background_researcher_cs.Bonus(p.protoModuleCrew);
						double crew_gain = 1 + bonus * Settings.LaboratoryCrewLevelBonus;
						crew_gain = Lib.Clamp(crew_gain, 1, Settings.MaxLaborartoryBonus);
						rate *= crew_gain;
					}

					// get sample to analyze
					background_sample = NextSample(v);

					// if there is a sample to analyze
					if (background_sample != null)
					{
						// consume EC
						ec.Consume(lab.ec_rate * elapsed_s);

						// if there was ec
						// - comparing against amount in previous simulation step
						if (ec.amount > double.Epsilon)
						{
							// analyze the sample
							Analyze(v, background_sample, rate * elapsed_s);
						}
					}
				}
			}
		}

		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Toggle", active = true)]
		public void Toggle()
		{
			running = !running;

			// refresh VAB/SPH ui
			if (Lib.IsEditor()) GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
		}

		[KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "Clean", active = true)]
		public void CleanExperiments()
		{
			List<ModuleScienceExperiment> modules = vessel.FindPartModulesImplementing<ModuleScienceExperiment>();
			bool message = false;
			foreach (ModuleScienceExperiment m in modules)
			{
				if (m.resettable && m.Inoperable)
				{
					m.ResetExperiment();
					message = true;
				}
			}
			// inform the user
			if (message) Message.Post(localized_cleaned);
		}

		// action groups
		[KSPAction("Action")] public void Action(KSPActionParam param) { Toggle(); }

		public override string GetInfo()
		{
			return Specs().Info(Localizer.Format("#KERBALISM_Laboratory_Specs"));
		}

		// specifics support
		public Specifics Specs()
		{
			Specifics specs = new Specifics();
			specs.Add(Localizer.Format("#KERBALISM_Laboratory_Researcher"), new CrewSpecs(researcher).Info());
			if (cleaner) specs.Add(Localizer.Format("#KERBALISM_Laboratory_CanClean"));
			specs.Add(Localizer.Format("#KERBALISM_Laboratory_ECrate"), Lib.HumanReadableRate(ec_rate));
			specs.Add(Localizer.Format("#KERBALISM_Laboratory_rate"), Lib.HumanReadableDataRate(analysis_rate));
			return specs;
		}

		// contract objective support
		public bool CheckContractObjectiveValidity() { return true; }

		public string GetContractObjectiveType() { return "Laboratory"; }

		// get next sample to analyze, return null if there isn't a sample
		private static string NextSample(Vessel v)
		{
			// for each sample
			foreach (KeyValuePair<string, Sample> sample in DB.Vessel(v).drive.samples)
			{
				// if flagged for analysis
				if (sample.Value.analyze) return sample.Key;
			}

			// there was no sample to analyze
			return null;
		}

		// analyze a sample
		private static void Analyze(Vessel v, string filename, double amount)
		{
			// get vessel drive
			Drive drive = DB.Vessel(v).drive;

			// get sample
			Sample sample = drive.samples[filename];

			// analyze, and produce data
			amount = Math.Min(amount, sample.size);
			bool completed = amount >= sample.size - double.Epsilon;
			drive.Delete_sample(filename, amount);
			drive.Record_file(filename, amount, false);

			// if the analysis is completed
			if (completed)
			{
				// inform the user
				Message.Post(Lib.BuildString(Lib.Color("cyan", Localizer.Format("#KERBALISM_Laboratory_Analysis"), true), "\n",
					Localizer.Format("#KERBALISM_Laboratory_Analyzed", Lib.Bold(v.vesselName), Lib.Bold(Science.Experiment(filename).name))), localized_results);

				if(PreferencesBasic.Instance.transmitScience)
					drive.Transmit_file(filename);

				// record landmark event
				if (!Lib.Landed(v)) DB.landmarks.space_analysis = true;
			}
		}

		private void SetStatusText()
		{
			switch (status)
			{
				case Status.DISABLED:
					status_txt = localized_disabled;
					break;
				case Status.NO_EC:
					status_txt = localized_noEC;
					break;
				case Status.NO_RESEARCHER:
					status_txt = Lib.Color("yellow", researcher_cs.Warning());
					break;
				case Status.NO_SAMPLE:
					status_txt = localized_noSample;
					break;
				case Status.RUNNING:
					status_txt = Lib.Color("green", Science.Experiment(current_sample).name);
					break;
			}
		}

		// module info support
		public string GetModuleTitle() { return localized_title; } // attempt to display at the top
		public override string GetModuleDisplayName() { return localized_title; } // Attempt to display at top of tooltip
		public string GetPrimaryField() { return String.Empty; }
		public Callback<Rect> GetDrawModulePanelCallback() { return null; }
	}


} // KERBALISM


