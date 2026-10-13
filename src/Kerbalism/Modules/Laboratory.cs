using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{

	public class Laboratory: PartModule, IModuleInfo, ISpecifics, IContractObjectiveModule
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
			NO_STORAGE,
			NO_SAMPLE,
			NO_RESEARCHER,
			RUNNING
		}

		// other data
		private CrewSpecs researcher_cs;                            // crew specs for the researcher
		private static CrewSpecs background_researcher_cs;          // crew specs for the researcher in background simulation
		private SubjectData current_sample = null;                       // sample currently being analyzed
		private static SubjectData background_sample = null;             // sample currently being analyzed in background simulation
		private Status status = Status.DISABLED;                    // laboratory status
		private string status_txt = string.Empty;                   // status string to show next to the ui button
		private ResourceInfo ec = null;                            // resource info for EC

		// localized strings
		private static readonly string localized_title = Lib.BuildString("<size=1><color=#00000000>00</color></size>", Local.Laboratory_Title);
		private static readonly string localized_toggle = Local.Laboratory_Toggle;
		private static readonly string localized_enabled = Local.Generic_ENABLED;
		private static readonly string localized_disabled = Local.Generic_DISABLED;
		private static readonly string localized_noEC = Lib.Color(Local.Laboratory_NoEC, Lib.Kolor.Orange);
		private static readonly string localized_noSample = Local.Laboratory_NoSample;
		private static readonly string localized_cleaned = Local.Laboratory_Cleaned;
		private static readonly string localized_results = Local.Laboratory_Results;
		private static readonly string localized_noStorage = Local.Laboratory_Nostorage;//"No storage available"

		public override void OnStart(StartState state)
		{
			// don't break tutorial scenarios
			if (Lib.DisableScenario(this)) return;

			// set UI text
			Actions["Action"].guiName = Local.Laboratory_Action;
			Events["CleanExperiments"].guiName = Local.Laboratory_Clean;

			// do nothing in the editors and when compiling parts
			if (!Lib.IsFlight()) return;

			// parse crew specs
			researcher_cs = new CrewSpecs(researcher);
		}

		public void Update()
		{
			if (!part.IsPAWVisible())
				return;

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

					double rate = EffectiveRate(researcher_cs, part.protoModuleCrew);

					// if there is a sample to analyze
					if (current_sample != null)
					{
						// consume EC
						ec = ResourceCache.GetResource(vessel, "ElectricCharge");
						ec.Consume(ec_rate * Kerbalism.elapsed_s, ResourceBroker.Laboratory);

						// if there was ec
						// - comparing against amount in previous simulation step
						if (ec.Amount > double.Epsilon)
						{
							// analyze the sample
							status = Analyze(vessel, current_sample, rate * Kerbalism.elapsed_s);
							running = status == Status.RUNNING;
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

		public static void BackgroundUpdate(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, Laboratory lab, ResourceInfo ec, double elapsed_s)
		{
			// if enabled
			if (Lib.Proto.GetBool(m, "running"))
			{
				// if a researcher is not required, or the researcher is present
				background_researcher_cs = new CrewSpecs(lab.researcher);
				if (!background_researcher_cs || background_researcher_cs.Check(p.protoModuleCrew))
				{
					double rate = lab.EffectiveRate(background_researcher_cs, p.protoModuleCrew);

					// get sample to analyze
					background_sample = NextSample(v);

					// if there is a sample to analyze
					if (background_sample != null)
					{
						// consume EC
						ec.Consume(lab.ec_rate * elapsed_s, ResourceBroker.Laboratory);

						// if there was ec
						// - comparing against amount in previous simulation step
						if (ec.Amount > double.Epsilon)
						{
							// analyze the sample
							var status = Analyze(v, background_sample, rate * elapsed_s);
							if (status != Status.RUNNING)
								Lib.Proto.Set(m, "running", false);
						}
					}
				}
			}
		}

		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "#KERBALISM_Laboratory_Toggle", active = true, groupName = "Science", groupDisplayName = "#KERBALISM_Group_Science")]//"Toggle Lab"Science
		public void Toggle()
		{
			running = !running;

			// refresh VAB/SPH ui
			if (Lib.IsEditor()) GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
		}

		[KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "#KERBALISM_Laboratory_Clean", active = true, groupName = "Science", groupDisplayName = "#KERBALISM_Group_Science")]//Clean Lab""Science
		public void CleanExperiments()
		{
			bool message = false;

			var stockExperiments = vessel.FindPartModulesImplementing<ModuleScienceExperiment>();
			foreach (ModuleScienceExperiment m in stockExperiments)
			{
				if (m.resettable && m.Inoperable)
				{
					m.ResetExperiment();
					message = true;
				}
			}

			var kerbalismExperiments = vessel.FindPartModulesImplementing<Experiment>();
			foreach (Experiment m in kerbalismExperiments)
			{
				message |= m.Reset(false);
			}


			// inform the user
			if (message) Message.Post(localized_cleaned);
		}

		// action groups
		[KSPAction("Action")] public void Action(KSPActionParam param) { Toggle(); }

		public override string GetInfo()
		{
			return Specs().Info(Local.Laboratory_Specs);
		}

		// specifics support
		public Specifics Specs()
		{
			Specifics specs = new Specifics();
			specs.Add(Local.Laboratory_Researcher, new CrewSpecs(researcher).Info());
			if (cleaner) specs.Add(Local.Laboratory_CanClean);
			specs.Add(Local.Laboratory_ECrate, Lib.HumanOrSIRate(ec_rate, Lib.ECResID));
			specs.Add(Local.Laboratory_rate, Lib.HumanReadableDataRate(analysis_rate));
			return specs;
		}

		// contract objective support
		public bool CheckContractObjectiveValidity() { return true; }

		public string GetContractObjectiveType() { return "Laboratory"; }

		private double EffectiveRate(CrewSpecs specs, List<ProtoCrewMember> crew)
		{
			double rate = analysis_rate;
			if (specs)
			{
				int bonus = specs.Bonus(crew);
				double crew_gain = 1 + bonus * Settings.LaboratoryCrewLevelBonus;
				crew_gain = Lib.Clamp(crew_gain, 1, Settings.MaxLaborartoryBonus);
				rate *= crew_gain;
			}
			return rate;
		}

		/// <summary>
		/// Combined analysis rate of all powered, staffed labs currently running on the vessel.
		/// </summary>
		private static double GetVesselAnalysisRate(Vessel v)
		{
			if (v == null) return 0.0;

			ResourceInfo ec = ResourceCache.GetResource(v, "ElectricCharge");
			if (ec.Amount <= double.Epsilon) return 0.0;

			double rate = 0.0;
			if (v.loaded)
			{
				foreach (Laboratory lab in v.FindPartModulesImplementing<Laboratory>())
				{
					if (!lab.isEnabled) continue;
					if (!lab.running) continue;
					if (lab.researcher_cs && !lab.researcher_cs.Check(lab.part.protoModuleCrew)) continue;
					rate += lab.EffectiveRate(lab.researcher_cs, lab.part.protoModuleCrew);
				}
			}
			else if (v.protoVessel != null)
			{
				foreach (var module in Background.Background_PMs(v))
				{
					if (module.type != Background.Module_type.Laboratory) continue;
					if (!Lib.Proto.GetBool(module.m, "running")) continue;

					Laboratory lab = module.module_prefab as Laboratory;
					if (lab == null) continue;

					CrewSpecs specs = new CrewSpecs(lab.researcher);
					if (specs && !specs.Check(module.p.protoModuleCrew)) continue;
					rate += lab.EffectiveRate(specs, module.p.protoModuleCrew);
				}
			}
			return rate;
		}

		/// <summary>
		/// Get the sample currently being analyzed and its estimated completion time.
		/// </summary>
		public static bool TryGetAnalysisETA(Vessel v, out Sample sample, out double eta)
		{
			sample = v == null ? null : NextAnalyzableSample(v);
			eta = double.NaN;
			if (sample == null) return false;

			double rate = GetVesselAnalysisRate(v);
			if (rate <= double.Epsilon) return false;

			eta = sample.size / rate;
			return !double.IsNaN(eta) && !double.IsInfinity(eta);
		}

		private static Sample NextAnalyzableSample(Vessel v)
		{
			foreach (var drive in Drive.GetDrives(v, true))
			{
				foreach (Sample sample in drive.samples.Values)
				{
					if (sample.analyze) return sample;
				}
			}
			return null;
		}

		// get next sample to analyze, return null if there isn't a sample
		private static SubjectData NextSample(Vessel v)
		{
			Sample sample = NextAnalyzableSample(v);
			return sample == null ? null : sample.subjectData;
		}

		// analyze a sample
		private static Status Analyze(Vessel v, SubjectData subject, double amount)
		{
			Sample sample = null;
			Drive sampleDrive = null;
			foreach (var d in Drive.GetDrives(v, true))
			{
				if (d.samples.ContainsKey(subject) && d.samples[subject].analyze)
				{
					sample = d.samples[subject];
					sampleDrive = d;
					break;
				}
			}

			bool completed = false;
			if(sample != null)
			{
				completed = amount > sample.size;
				amount = Math.Min(amount, sample.size);
			}

			Drive fileDrive = Drive.FileDrive(v.KerbalismData(), amount);

			if (fileDrive == null)
				return Status.NO_STORAGE;

			if(sample != null)
			{
				bool recorded = fileDrive.Record_file(subject, amount, false);

				double massRemoved = 0.0;
				if (recorded)
					massRemoved = sampleDrive.Delete_sample(subject, amount);
				else
				{
					Message.Post(
						Lib.Color(Lib.BuildString(Local.Laboratory_Analysis, " ", Local.Laboratory_stopped), Lib.Kolor.Red),//"stopped"
						Local.Laboratory_Notspace//"Not enough space on hard drive"
					);

					return Status.NO_STORAGE;
				}

				// return sample mass to experiment if needed
				if (massRemoved > 0.0) RestoreSampleMass(v, subject, massRemoved);
			}

			// if the analysis is completed
			if (completed)
			{
				if(!PreferencesScience.Instance.analyzeSamples)
				{
					// only inform the user if auto-analyze is turned off
					// otherwise we could be spamming "Analysis complete" messages
					Message.Post(Lib.BuildString(Lib.Color(Local.Laboratory_Analysis, Lib.Kolor.Science, true), "\n",
						Local.Laboratory_Analyzed.Format(Lib.Bold(v.vesselName), Lib.Bold(subject.FullTitle))), localized_results);
				}

				if (PreferencesScience.Instance.transmitScience)
					fileDrive.Send(subject.Id, true);

				// record landmark event
				if (!Lib.Landed(v)) DB.landmarks.space_analysis = true;
			}

			return Status.RUNNING;
		}

		private static void RestoreSampleMass(Vessel v, SubjectData filename, double restoredAmount)
		{
			if(v.loaded) // loaded vessel
			{
				foreach (var experiment in v.FindPartModulesImplementing<Experiment>())
				{
					restoredAmount -= experiment.RestoreSampleMass(restoredAmount, filename.ExpInfo.ExperimentId);
				}
			}
			else // unloaded vessel
			{
				foreach (ProtoPartModuleSnapshot m in ProtoPartModuleCache.GetModules(v.protoVessel, "Experiment"))
				{
					restoredAmount -= Experiment.RestoreSampleMass(restoredAmount, m, filename.ExpInfo.ExperimentId);
					if (restoredAmount < double.Epsilon) return;
				}
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
				case Status.NO_STORAGE:
					status_txt = localized_noStorage;
					break;
				case Status.NO_RESEARCHER:
					status_txt = Lib.Color(researcher_cs.Warning(), Lib.Kolor.Orange);
					break;
				case Status.NO_SAMPLE:
					status_txt = localized_noSample;
					break;
				case Status.RUNNING:
					status_txt = Lib.Color(current_sample.FullTitle, Lib.Kolor.Green);
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


