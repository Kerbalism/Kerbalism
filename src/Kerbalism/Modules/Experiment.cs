using System;
using System.Collections.Generic;
using Experience;
using UnityEngine;
using KSP.Localization;
using System.Collections;
using static KERBALISM.ExperimentRequirements;

namespace KERBALISM
{

	public class Experiment : PartModule, ISpecifics, IModuleInfo, IPartMassModifier, IModuleRollout
	{
		// config
		[KSPField] public string experiment_id;               // id of associated experiment definition
		[KSPField] public string experiment_desc = string.Empty;  // some nice lines of text
		[KSPField] public double data_rate;                   // sampling rate in Mb/s
		[KSPField] public double ec_rate;                     // EC consumption rate per-second
		[KSPField] public float sample_mass = 0f;             // if set to anything but 0, the experiment is a sample.
		[KSPField] public float sample_reservoir = 0f;        // the amount of sampling mass this unit is shipped with
		[KSPField] public bool sample_collecting = false;     // if set to true, the experiment will generate mass out of nothing
		[KSPField] public bool allow_shrouded = true;         // true if data can be transmitted
		[KSPField] public string requires = string.Empty;     // additional requirements that must be met
		[KSPField] public string crew_operate = string.Empty; // operator crew. if set, crew has to be on vessel while recording
		[KSPField] public string crew_reset = string.Empty;   // reset crew. if set, experiment will stop recording after situation change
		[KSPField] public string crew_prepare = string.Empty; // prepare crew. if set, experiment will require crew to set up before it can start recording 
		[KSPField] public string resources = string.Empty;    // resources consumed by this experiment
		[KSPField] public bool hide_when_unavailable = false; // don't show UI when the experiment is unavailable

		// animations
		[KSPField] public string anim_deploy = string.Empty; // deploy animation
		[KSPField] public bool anim_deploy_reverse = false;

		[KSPField] public string anim_loop = string.Empty; // deploy animation
		[KSPField] public bool anim_loop_reverse = false;

		// persistence
		[KSPField(isPersistant = true)] public string issue = string.Empty;
		[KSPField(isPersistant = true)] public string last_subject_id = string.Empty;
		[KSPField(isPersistant = true)] public bool didPrepare = false;
		[KSPField(isPersistant = true)] public bool shrouded = false;
		[KSPField(isPersistant = true)] public double remainingSampleMass = 0.0;
		[KSPField(isPersistant = true)] public uint privateHdId = 0;

		/// <summary> never set this directly, use the "State" property </summary>
		[KSPField(isPersistant = true)] private RunningState expState = RunningState.Stopped;
		[KSPField(isPersistant = true)] private ExpStatus status = ExpStatus.Stopped;

		public ExperimentInfo ExpInfo => expInfo;
		private ExperimentInfo expInfo;
		public ExperimentRequirements Requirements { get; private set; }
		public List<ObjectPair<string, double>> ResourceDefs { get; private set; }

		// animations
		internal Animator deployAnimator;
		internal Animator loopAnimator;

		private CrewSpecs operator_cs;
		private CrewSpecs reset_cs;
		private CrewSpecs prepare_cs;
		
		// private double next_check = 0;

		#region state/status

		public enum ExpStatus { Stopped, Running, Forced, Waiting, Issue, Broken }
		public enum RunningState { Stopped, Running, Forced, Broken }

		public RunningState State
		{
			get => expState;
			set
			{
				expState = value;
				status = GetStatus(value, ExpInfo, issue);
			}
		}

		public ExpStatus Status => status;

		public static ExpStatus GetStatus(RunningState state, ExperimentInfo expInfo, string issue)
		{
			switch (state)
			{
				case RunningState.Broken:
					return ExpStatus.Broken;
				case RunningState.Stopped:
					return ExpStatus.Stopped;
				case RunningState.Running:
					if (issue.Length > 0) return ExpStatus.Issue;
					if (expInfo.SubjectScienceRemainingToCollect <= 0f) return ExpStatus.Waiting;
					return ExpStatus.Running;
				case RunningState.Forced:
					if (issue.Length > 0) return ExpStatus.Issue;
					return ExpStatus.Forced;
				default:
					return ExpStatus.Stopped;
			}
		}

		public static bool IsRunning(ExpStatus status)
			=> status == ExpStatus.Running || status == ExpStatus.Forced || status == ExpStatus.Waiting || status == ExpStatus.Issue;
		public static bool IsRunning(RunningState state)
			=> state == RunningState.Running || state == RunningState.Forced;
		public bool Running
			=> IsRunning(expState);
		public static bool IsBroken(RunningState state)
			=> state == RunningState.Broken;
		public bool Broken
			=> IsBroken(State);

		#endregion

		#region init / parsing

		public override void OnLoad(ConfigNode node)
		{
			// build up science sample mass database
			if (HighLogic.LoadedScene == GameScenes.LOADING)
			{
				ResourceDefs = ParseResources(resources);
				Requirements = new ExperimentRequirements(requires);

				if (experiment_id == null)
				{
					Lib.Log("ERROR: EXPERIMENT WITHOUT EXPERIMENT_ID IN PART " + part);
				}
				else
				{
					Science.RegisterSampleMass(experiment_id, sample_mass);
				}
			}

			base.OnLoad(node);
		}

		public override void OnStart(StartState state)
		{
			// don't break tutorial scenarios
			if (Lib.DisableScenario(this)) return;

			if (last_subject_id == string.Empty) last_subject_id = experiment_id;

			// create animators
			deployAnimator = new Animator(part, anim_deploy);
			deployAnimator.reversed = anim_deploy_reverse;

			loopAnimator = new Animator(part, anim_loop);
			loopAnimator.reversed = anim_loop_reverse;

			// set initial animation states
			deployAnimator.Still(Running ? 1.0 : 0.0);
			loopAnimator.Still(Running ? 1.0 : 0.0);
			if (Running) loopAnimator.Play(false, true);

			// parse crew specs
			if(!string.IsNullOrEmpty(crew_operate))
				operator_cs = new CrewSpecs(crew_operate);
			if (!string.IsNullOrEmpty(crew_reset))
				reset_cs = new CrewSpecs(crew_reset);
			if (!string.IsNullOrEmpty(crew_prepare))
				prepare_cs = new CrewSpecs(crew_prepare);

			ResourceDefs = ParseResources(resources);
			Requirements = new ExperimentRequirements(requires);

			Events["ToggleEvent"].guiActiveUncommand = true;
			Events["ToggleEvent"].externalToEVAOnly = true;
			Events["ToggleEvent"].requireFullControl = false;

			Events["ShowPopup"].guiActiveUncommand = true;
			Events["ShowPopup"].externalToEVAOnly = true;
			Events["ShowPopup"].requireFullControl = false;

			Events["Prepare"].guiActiveUncommand = true;
			Events["Prepare"].externalToEVAOnly = true;
			Events["Prepare"].requireFullControl = false;

			Events["Reset"].guiActiveUncommand = true;
			Events["Reset"].externalToEVAOnly = true;
			Events["Reset"].requireFullControl = false;

			if (Lib.IsFlight())
			{
				foreach (var hd in part.FindModulesImplementing<HardDrive>())
				{
					if (hd.experiment_id == experiment_id) privateHdId = part.flightID;
				}
			}
		}

		/// <summary>Called by Callbacks just after rollout to launch pad</summary>
		public void OnRollout()
		{
			if (Lib.DisableScenario(this)) return;

			// initialize the remaining sample mass
			// this needs to be done only once just after launch
			if (!sample_collecting)
			{
				remainingSampleMass = sample_mass;
				if (sample_reservoir > float.Epsilon)
					remainingSampleMass = sample_reservoir;
			}
		}

		private static List<ObjectPair<string, double>> noResources = new List<ObjectPair<string, double>>();
		internal static List<ObjectPair<string, double>> ParseResources(string resources, bool logErros = false)
		{
			if (string.IsNullOrEmpty(resources)) return noResources;

			List<ObjectPair<string, double>> defs = new List<ObjectPair<string, double>>();
			var reslib = PartResourceLibrary.Instance.resourceDefinitions;

			foreach (string s in Lib.Tokenize(resources, ','))
			{
				// definitions are Resource@rate
				var p = Lib.Tokenize(s, '@');
				if (p.Count != 2) continue;             // malformed definition
				string res = p[0];
				if (!reslib.Contains(res)) continue;    // unknown resource
				double rate = double.Parse(p[1]);
				if (res.Length < 1 || rate < double.Epsilon) continue;  // rate <= 0
				defs.Add(new ObjectPair<string, double>(res, rate));
			}
			return defs;
		}

		#endregion

		#region update methods

		public void Update()
		{
			if (expInfo == null)
				expInfo = Science.Experiment(last_subject_id);

			// in flight
			if (Lib.IsFlight())
			{
				Vessel v = FlightGlobals.ActiveVessel;
				if (v == null || EVA.IsDead(v)) return;

				// do nothing if vessel is invalid
				if (!vessel.KerbalismIsValid()) return;

				if (prepare_cs == null || didPrepare || (hide_when_unavailable && status != ExpStatus.Issue))
				{
					Events["ToggleEvent"].active = true;
					Events["ShowPopup"].active = true;

					Events["ToggleEvent"].guiName = Lib.StatusToggle(Lib.Ellipsis(ExpInfo.Name, Styles.ScaleStringLength(25)), StatusInfo(status, issue));
					Events["ShowPopup"].guiName = Lib.BuildString("info : ", ScienceValue(ExpInfo), " ", State == RunningState.Forced ? ExpInfo.SubjectPercentCollectedTotal.ToString("P0") : RunningCountdown(ExpInfo, data_rate));
				}
				else
				{
					Events["ToggleEvent"].active = false;
					Events["ShowPopup"].active = false;
				}

				Events["Prepare"].guiName = Lib.BuildString("Prepare <b>", ExpInfo.Name, "</b>");
				Events["Prepare"].active = !didPrepare && prepare_cs != null && string.IsNullOrEmpty(last_subject_id);

				Events["Reset"].guiName = Lib.BuildString("Reset <b>", ExpInfo.Name, "</b>");
				// we need a reset either if we have recorded data or did a setup
				bool resetActive = (reset_cs != null || prepare_cs != null) && !string.IsNullOrEmpty(last_subject_id);
				Events["Reset"].active = resetActive;
			}
			// in the editor
			else if (Lib.IsEditor())
			{
				// update ui
				Events["ToggleEvent"].guiName = Lib.StatusToggle(ExpInfo.Name, StatusInfo(status, issue));
				Events["Reset"].active = false;
				Events["Prepare"].active = false;
			}
		}

		public void FixedUpdate()
		{
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.Experiment.FixedUpdate");

			// basic sanity checks
			if (Lib.IsEditor())
			{
				UnityEngine.Profiling.Profiler.EndSample();
				return;
			}
			if (!vessel.KerbalismIsValid())
			{
				UnityEngine.Profiling.Profiler.EndSample();
				return;
			}
			//if (next_check > Planetarium.GetUniversalTime())
			//{
			//	UnityEngine.Profiling.Profiler.EndSample();
			//	return;
			//}

			if (!Running)
			{
				ExperimentSituation situation = Science.GetExperimentSituation(vessel);
				Science.GetSubjectId(experiment_id, vessel, situation, out last_subject_id, out expInfo);
				UnityEngine.Profiling.Profiler.EndSample();
				return;
			}

			shrouded = part.ShieldedFromAirstream;

			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.Experiment.FixedUpdate.RunningUpdate");
			RunningUpdate(
				vessel, this, privateHdId, didPrepare, shrouded,
				ResourceCache.GetResource(vessel, "ElectricCharge"),
				ResourceCache.Get(vessel),
				ResourceDefs,
				expState,
				Kerbalism.elapsed_s,
				ref last_subject_id,
				ref remainingSampleMass,
				out expInfo,
				out issue);
			UnityEngine.Profiling.Profiler.EndSample();

			status = GetStatus(expState, ExpInfo, issue);

			UnityEngine.Profiling.Profiler.EndSample();
		}

		public static void BackgroundUpdate(Vessel v, ProtoPartModuleSnapshot m, Experiment prefab, ResourceInfo ec, VesselResources resources, double elapsed_s)
		{
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.Experiment.BackgroundUpdate");
			RunningState expState = Lib.Proto.GetEnum(m, "expState", RunningState.Stopped);

			if (!IsRunning(expState))
			{
				ExperimentSituation situation = Science.GetExperimentSituation(v);
				string subject_id = Science.GetSubjectId(prefab.experiment_id, v, situation);
				Lib.Proto.Set(m, "last_subject_id", subject_id);
				UnityEngine.Profiling.Profiler.EndSample();
				return;
			}
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.Experiment.BackgroundUpdate.GetProto");

			bool didPrepare = Lib.Proto.GetBool(m, "didPrepare", false);
			bool shrouded = Lib.Proto.GetBool(m, "shrouded", false);
			string last_subject_id = Lib.Proto.GetString(m, "last_subject_id", "");
			double remainingSampleMass = Lib.Proto.GetDouble(m, "remainingSampleMass", 0.0);
			uint privateHdId = Lib.Proto.GetUInt(m, "privateHdId", 0u);

			UnityEngine.Profiling.Profiler.EndSample();

			string issue;
			ExperimentInfo expInfo;

			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.Experiment.BackgroundUpdate.RunningUpdate");
			RunningUpdate(
				v, prefab, privateHdId, didPrepare, shrouded,
				ec,
				resources,
				prefab.ResourceDefs,
				expState,
				elapsed_s,
				ref last_subject_id,
				ref remainingSampleMass,
				out expInfo,
				out issue);
			UnityEngine.Profiling.Profiler.EndSample();

			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.Experiment.BackgroundUpdate.SetProto");
			Lib.Proto.Set(m, "last_subject_id", last_subject_id);
			Lib.Proto.Set(m, "status", GetStatus(expState, expInfo, issue));
			Lib.Proto.Set(m, "issue", issue);

			if (prefab.sample_mass > 0f)
				Lib.Proto.Set(m, "remainingSampleMass", remainingSampleMass);
			UnityEngine.Profiling.Profiler.EndSample();

			UnityEngine.Profiling.Profiler.EndSample();
		}

		private static void RunningUpdate(
			Vessel v, Experiment prefab, uint hdId, bool didPrepare, bool isShrouded,
			ResourceInfo ec, VesselResources resources, List<ObjectPair<string, double>> resourceDefs,
			RunningState expState, double elapsed_s,
			ref string subjectId, ref double remainingSampleMass, out ExperimentInfo expInfo, out string mainIssue)
		{
			mainIssue = string.Empty;
			ExperimentSituation situation = Science.GetExperimentSituation(v);
			string newSubjectId;
			bool isSituationValid = Science.GetSubjectId(prefab.experiment_id, v, situation, out newSubjectId, out expInfo);

			bool subjectHasChanged = subjectId != newSubjectId;
			if (subjectHasChanged)
				subjectId = newSubjectId;

			if (!isSituationValid)
			{
				mainIssue = "invalid situation";
				return;
			}

			float scienceRemaining = expInfo.SubjectScienceRemainingToCollect;

			if (expState != RunningState.Forced && scienceRemaining <= 0f)
				return;

			if (isShrouded && !prefab.allow_shrouded)
			{
				mainIssue = "shrouded";
				return;
			}

			if (subjectHasChanged && prefab.crew_reset.Length > 0)
			{
				mainIssue = "reset required";
				return;
			}

			if (ec.Amount == 0.0 && prefab.ec_rate > 0.0)
			{
				mainIssue = "no Electricity";
				return;
			}

			if (!string.IsNullOrEmpty(prefab.crew_operate))
			{
				var cs = new CrewSpecs(prefab.crew_operate);
				if (!cs && Lib.CrewCount(v) > 0)
				{
					mainIssue = "crew on board";
					return;
				}
				else if (cs && !cs.Check(v))
				{
					mainIssue = cs.Warning();
					return;
				}
			}

			if (!prefab.sample_collecting && remainingSampleMass <= 0.0 && prefab.sample_mass > 0.0)
			{
				mainIssue = "depleted";
				return;
			}

			if (!didPrepare && !string.IsNullOrEmpty(prefab.crew_prepare))
			{
				mainIssue = "not prepared";
				return;
			}

			if (!v.loaded && situation.AtmosphericFlight())
			{
				mainIssue = "background flight";
				return;
			}

			RequireResult[] reqResults;
			if (!prefab.Requirements.TestRequirements(v, out reqResults))
			{
				mainIssue = "unmet requirement";
				return;
			}

			if (!HasRequiredResources(v, resourceDefs, resources, out mainIssue))
			{
				mainIssue = "invalid requirement";
				return;
			}

			// create the subject at the last possible moment, otherwise we will fill
			// the RnD archives with empty subjects
			expInfo.CreateSubjectInRnD(v, situation);

			double chunkSizeMax = prefab.data_rate * elapsed_s;
			double chunkSize;
			if (expState != RunningState.Forced)
				chunkSize = Math.Min(chunkSizeMax, scienceRemaining / expInfo.SubjectSciencePerMB);
			else
				chunkSize = chunkSizeMax;

			Drive drive = GetDrive(prefab, v, hdId, chunkSize, newSubjectId);
			Drive warpDrive = null;
			bool isFile = prefab.sample_mass <= 0.0;
			double available;
			if (isFile)
			{
				available = drive.FileCapacityAvailable();
				if (drive.GetFileSend(newSubjectId))
				{
					warpDrive = Cache.WarpCache(v);
					available += warpDrive.FileCapacityAvailable();
				}
			}
			else
			{
				available = drive.SampleCapacityAvailable(newSubjectId);
			}

			if (available <= 0.0)
			{
				mainIssue = "insufficient_storage";
				return;
			}

			// TODO : disabled for now, I'm lazy
			//if (!string.IsNullOrEmpty(issue))
			//{
			//	next_check = Planetarium.GetUniversalTime() + Math.Max(3, Kerbalism.elapsed_s * 3);
			//	UnityEngine.Profiling.Profiler.EndSample();
			//	return;
			//}

			chunkSize = Math.Min(chunkSize, available);

			// TODO : prodfactor rely on resource capacity, resulting in wrong (lower) rate at high timewarp speeds if resource capacity is too low
			// There is no way to fix that currently, this is another example of why virtual ressource recipes are needed
			double prodFactor;
			prodFactor = chunkSize / chunkSizeMax;
			prodFactor = Math.Min(prodFactor, Lib.Clamp(ec.Amount / (prefab.ec_rate * elapsed_s), 0.0, 1.0));
			foreach (ObjectPair<string, double> p in resourceDefs)
			{
				ResourceInfo ri = resources.GetResource(v, p.a);
				prodFactor = Math.Min(prodFactor, Lib.Clamp(ri.Amount / (p.b * elapsed_s), 0.0, 1.0));
			}

			if (prodFactor == 0.0)
			{
				mainIssue = "insufficient resources";
				return;
			}

			chunkSize = chunkSizeMax * prodFactor;
			elapsed_s *= prodFactor;
			double massDelta = prefab.sample_mass * chunkSize / expInfo.MaxAmount;

			if (isFile)
			{
				if (warpDrive != null)
				{
					double s = Math.Min(chunkSize, warpDrive.FileCapacityAvailable());
					warpDrive.Record_file(newSubjectId, s, true);

					if (chunkSize > s) // only write to persisted drive if the data cannot be transmitted in this tick
						drive.Record_file(newSubjectId, chunkSize - s, true);
					else if (!drive.files.ContainsKey(newSubjectId)) // if everything is transmitted, create an empty file so the player know what is happening
						drive.Record_file(newSubjectId, 0.0, true);
				}
				else
				{
					drive.Record_file(newSubjectId, chunkSize, true);
				}
			}
			else
			{
				drive.Record_sample(newSubjectId, chunkSize, massDelta);
			}

			// consume resources
			ec.Consume(prefab.ec_rate * elapsed_s, "experiment");
			foreach (ObjectPair<string, double> p in resourceDefs)
				resources.Consume(v, p.a, p.b * elapsed_s, "experiment");

			if (!prefab.sample_collecting)
			{
				remainingSampleMass -= massDelta;
				remainingSampleMass = Math.Max(remainingSampleMass, 0.0);
			}
		}

		private static Drive GetDrive(Experiment experiment, Vessel vessel, uint hdId, double chunkSize, string subject_id)
		{
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.Experiment.GetDrive");
			bool isFile = experiment.sample_mass < float.Epsilon;
			Drive drive = null;
			if (hdId != 0) drive = DB.Drive(hdId);
			else drive = isFile ? Drive.FileDrive(vessel, chunkSize) : Drive.SampleDrive(vessel, chunkSize, subject_id);
			UnityEngine.Profiling.Profiler.EndSample();
			return drive;
		}

		private static bool HasRequiredResources(Vessel v, List<ObjectPair<string, double>> defs, VesselResources res, out string issue)
		{
			issue = string.Empty;
			if (defs.Count < 1)
				return true;

			// test if there are enough resources on the vessel
			foreach (var p in defs)
			{
				var ri = res.GetResource(v, p.a);
				if (ri.Amount == 0.0)
				{
					issue = "missing " + ri.ResourceName;
					return false;
				}
			}
			return true;
		}

		#endregion

		#region user interaction



		public RunningState Toggle(bool setForcedRun = false)
		{
			if (State == RunningState.Broken)
				return State;

			if (Lib.IsEditor())
			{
				if (Running)
				{
					State = RunningState.Stopped;
				}
				else
				{
					if (!EditorTracker.Instance.AllowStart(this))
					{
						PostMultipleRunsMessage(Science.Experiment(experiment_id).Name, "");
						return State;
					}
					State = RunningState.Running;
				}

				deployAnimator.Play(!Running, false);
				GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
				return State;
			}

			if (Lib.IsFlight() && !vessel.IsControllable)
				return State;

			if (deployAnimator.Playing())
				return State; // nervous clicker? wait for it, goddamnit.
			
			if (Running)
			{
				if (setForcedRun && expState == RunningState.Running)
				{
					State = RunningState.Forced;
					return State;
				}
				// stop experiment
				// plays the deploy animation in reverse
				Action stop = delegate () { State = RunningState.Stopped; deployAnimator.Play(true, false); };

				// wait for loop animation to stop before deploy animation
				if (loopAnimator.Playing())
					loopAnimator.Stop(stop);
				else
					stop();
			}
			else
			{
				// The same experiment must run only once on a vessel
				if (IsExperimentRunningOnVessel(vessel, experiment_id))
				{
					PostMultipleRunsMessage(Science.Experiment(experiment_id).Name, vessel.vesselName);
					return State;
				}
				// start experiment
				// play the deploy animation, when it's done start the loop animation
				deployAnimator.Play(false, false, delegate ()
				{
					State = setForcedRun ? RunningState.Forced : RunningState.Running;
					loopAnimator.Play(false, true);
				});
			}
			return State;
		}

		public static void ProtoToggle(Vessel v, Experiment prefab, ProtoPartModuleSnapshot protoModule, bool setForcedRun = false)
		{
			RunningState expState = Lib.Proto.GetEnum(protoModule, "expState", RunningState.Stopped);

			if (expState == RunningState.Broken)
			{
				ProtoSetState(protoModule, expState);
				return;
			}
				

			if (!IsRunning(expState))
			{
				if (IsExperimentRunningOnVessel(v, prefab.experiment_id))
				{
					PostMultipleRunsMessage(Science.Experiment(prefab.experiment_id).Name, v.vesselName);
					{
						ProtoSetState(protoModule, expState);
						return;
					}
				}
				expState = setForcedRun ? RunningState.Forced : RunningState.Running;
			}
			else if (setForcedRun && expState == RunningState.Running)
			{
				expState = RunningState.Forced;
			}
			else
			{
				expState = RunningState.Stopped;
			}

			ProtoSetState(protoModule, expState);
			return;
		}

		public static void ProtoSetState(ProtoPartModuleSnapshot protoModule, RunningState expState)
		{
			Lib.Proto.Set(protoModule, "expState", expState);
			Lib.Proto.Set(protoModule, "status",
				GetStatus
				(
					expState,
					Science.Experiment(Lib.Proto.GetString(protoModule, "last_subject_id")),
					Lib.Proto.GetString(protoModule, "issue")
				));
		}

		/// <summary> works for loaded and unloaded vessel. very slow method, don't use it every tick </summary>
		public static bool IsExperimentRunningOnVessel(Vessel vessel, string experiment_id)
		{
			if (vessel.loaded)
			{
				foreach (Experiment e in vessel.FindPartModulesImplementing<Experiment>())
				{
					if (e.enabled && e.Running && e.experiment_id == experiment_id)
						return true;
				}
			}
			else
			{
				var PD = new Dictionary<string, Lib.Module_prefab_data>();
				foreach (ProtoPartSnapshot p in vessel.protoVessel.protoPartSnapshots)
				{
					// get part prefab (required for module properties)
					Part part_prefab = PartLoader.getPartInfoByName(p.partName).partPrefab;
					// get all module prefabs
					var module_prefabs = part_prefab.FindModulesImplementing<PartModule>();
					// clear module indexes
					PD.Clear();
					foreach (ProtoPartModuleSnapshot m in p.modules)
					{
						// get the module prefab
						// if the prefab doesn't contain this module, skip it
						PartModule module_prefab = Lib.ModulePrefab(module_prefabs, m.moduleName, PD);
						if (!module_prefab) continue;
						// if the module is disabled, skip it
						// note: this must be done after ModulePrefab is called, so that indexes are right
						if (!Lib.Proto.GetBool(m, "isEnabled")) continue;

						if (m.moduleName == "Experiment"
							&& ((Experiment)module_prefab).experiment_id == experiment_id
							&& IsRunning(Lib.Proto.GetEnum(m, "expState", RunningState.Stopped)))
							return true;
					}
				}
			}

			return false;
		}

#if KSP15_16
		[KSPEvent(guiActiveUnfocused = true, guiActive = true, guiActiveEditor = true, guiName = "_", active = true)]
#else
		[KSPEvent(guiActiveUnfocused = true, guiActive = true, guiActiveEditor = true, guiName = "_", active = true, groupName = "Science", groupDisplayName = "Science")]
#endif
		public void ToggleEvent()
		{
			Toggle();
		}

#if KSP15_16
		[KSPEvent(guiActiveUnfocused = true, guiActive = true, guiName = "_", active = true)]
#else
		[KSPEvent(guiActiveUnfocused = true, guiActive = true, guiName = "_", active = true, groupName = "Science", groupDisplayName = "Science")]
#endif
		public void ShowPopup()
		{
			new SciencePopup(vessel, this);
		}

#if KSP15_16
		[KSPEvent(guiActiveUnfocused = true, guiName = "_", active = false)]
#else
		[KSPEvent(guiActiveUnfocused = true, guiName = "_", active = false, groupName = "Science", groupDisplayName = "Science")]
#endif
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

#if KSP15_16
		[KSPEvent(guiActiveUnfocused = true, guiName = "_", active = false)]
#else
		[KSPEvent(guiActiveUnfocused = true, guiName = "_", active = false, groupName = "Science", groupDisplayName = "Science")]
#endif
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

		// action groups
		[KSPAction("Start")]
		public void StartAction(KSPActionParam param)
		{
			if (!Running) Toggle();
		}
		[KSPAction("Stop")]
		public void StopAction(KSPActionParam param)
		{
			if (Running) Toggle();
		}

#endregion

#region info / UI

		public static string StatusInfo(ExpStatus status, string issue = null)
		{
			switch (status)
			{
				case ExpStatus.Stopped: return Lib.Color("stopped", Lib.KColor.Yellow);
				case ExpStatus.Running: return Lib.Color("running", Lib.KColor.Green);
				case ExpStatus.Forced: return Lib.Color("forced run", Lib.KColor.Red);
				case ExpStatus.Waiting: return Lib.Color("waiting", Lib.KColor.Science);
				case ExpStatus.Broken: return Lib.Color("broken", Lib.KColor.Red);
				case ExpStatus.Issue: return Lib.Color(string.IsNullOrEmpty(issue) ? "issue" : issue, Lib.KColor.Orange);
				default: return string.Empty;
			}
		}

		public static string RunningCountdown(ExperimentInfo expInfo, double dataRate, bool compact = true)
		{
			double count;
			if (expInfo.SubjectExistsInRnD)
				count = Math.Max(1.0 - expInfo.SubjectPercentCollectedTotal, 0.0) * (expInfo.MaxAmount / dataRate);
			else
				count = expInfo.MaxAmount / dataRate;

			return Lib.HumanReadableCountdown(count, compact);
		}

		public static string ScienceValue(ExperimentInfo expInfo)
		{
			if (expInfo.SubjectExistsInRnD)
				return Lib.BuildString(Lib.HumanReadableScience(expInfo.SubjectScienceCollectedTotal), " / ", Lib.HumanReadableScience(expInfo.SubjectScienceMaxValue));
			else
				return Lib.Color("unknown", Lib.KColor.Science, true);
		}

		// specifics support
		public Specifics Specs()
		{
			Specifics specs = SpecsWithoutRequires();

			if (Requirements.Requires.Length > 0)
			{
				specs.Add(string.Empty);
				specs.Add("<color=#00ffff>Requires:</color>", string.Empty);
				foreach (RequireDef req in Requirements.Requires)
					specs.Add(Lib.BuildString("• <b>", ReqName(req.require), "</b>"), ReqValueFormat(req.require, req.value));
			}

			return specs;
		}

		public Specifics SpecsWithoutRequires()
		{
			var specs = new Specifics();
			var exp = Science.Experiment(experiment_id);
			if (exp == null)
			{
				specs.Add(Localizer.Format("#KERBALISM_ExperimentInfo_Unknown"));
				return specs;
			}

			if (!string.IsNullOrEmpty(experiment_desc))
			{
				specs.Add(Lib.BuildString("<i>", experiment_desc, "</i>"));
				specs.Add(string.Empty);
			}

			double expSize = exp.MaxAmount;
			if (sample_mass < float.Epsilon)
			{
				specs.Add("Data size", Lib.HumanReadableDataSize(expSize));
				specs.Add("Data rate", Lib.HumanReadableDataRate(data_rate));
				specs.Add("Duration", Lib.HumanReadableDuration(expSize / data_rate));
			}
			else
			{
				specs.Add("Sample size", Lib.HumanReadableSampleSize(expSize));
				specs.Add("Sample mass", Lib.HumanReadableMass(sample_mass));
				if (!sample_collecting && Math.Abs(sample_reservoir - sample_mass) > double.Epsilon && sample_mass > double.Epsilon)
					specs.Add("Experiments", "" + Math.Round(sample_reservoir / sample_mass, 0));
				specs.Add("Duration", Lib.HumanReadableDuration(expSize / data_rate));
			}

			List<string> situations = exp.Situations();
			if (situations.Count > 0)
			{
				specs.Add(string.Empty);
				specs.Add("<color=#00ffff>Situations:</color>", string.Empty);
				foreach (string s in situations) specs.Add(Lib.BuildString("• <b>", s, "</b>"));
			}

			specs.Add(string.Empty);
			specs.Add("<color=#00ffff>Needs:</color>");

			specs.Add("EC", Lib.HumanReadableRate(ec_rate));
			foreach (var p in ParseResources(resources))
				specs.Add(p.a, Lib.HumanReadableRate(p.b));

			if (crew_prepare.Length > 0)
			{
				var cs = new CrewSpecs(crew_prepare);
				specs.Add("Preparation", cs ? cs.Info() : "none");
			}
			if (crew_operate.Length > 0)
			{
				var cs = new CrewSpecs(crew_operate);
				specs.Add("Operation", cs ? cs.Info() : "unmanned");
			}
			if (crew_reset.Length > 0)
			{
				var cs = new CrewSpecs(crew_reset);
				specs.Add("Reset", cs ? cs.Info() : "none");
			}

			return specs;
		}

		// part tooltip
		public override string GetInfo()
		{
			return Specs().Info();
		}

		// IModuleInfo
		public string GetModuleTitle()
		{
			ExperimentInfo expInfo = Science.Experiment(experiment_id);
			if (expInfo == null)
				return Localizer.Format("#KERBALISM_ExperimentInfo_Unknown");
			return expInfo.Name;
		}
		public override string GetModuleDisplayName() { return GetModuleTitle(); }
		public string GetPrimaryField() { return string.Empty; }
		public Callback<Rect> GetDrawModulePanelCallback() { return null; }

#endregion

#region utility / other

		public void ReliablityEvent(bool breakdown)
		{
			if (breakdown) State = RunningState.Broken;
			else State = RunningState.Stopped;
		}

		public static void PostMultipleRunsMessage(string title, string vesselName)
		{
			Message.Post(Lib.Color("ALREADY RUNNING", Lib.KColor.Orange, true), "Can't start " + title + " a second time on vessel " + vesselName);
		}

#endregion

#region sample mass

		internal static double RestoreSampleMass(double restoredAmount, ProtoPartModuleSnapshot m, string id)
		{
			if (IsBroken(Lib.Proto.GetEnum<RunningState>(m, "expState"))) return 0.0;

			var experiment_id = Lib.Proto.GetString(m, "experiment_id", string.Empty);
			if (experiment_id != id) return 0.0;

			var sample_collecting = Lib.Proto.GetBool(m, "sample_collecting", false);
			if (sample_collecting) return 0.0;

			double remainingSampleMass = Lib.Proto.GetDouble(m, "remainingSampleMass", 0.0);
			double sample_reservoir = Lib.Proto.GetDouble(m, "sample_reservoir", 0.0);
			if (remainingSampleMass >= sample_reservoir) return 0;

			double delta = Math.Max(restoredAmount, sample_reservoir - remainingSampleMass);
			remainingSampleMass += delta;
			remainingSampleMass = Math.Min(sample_reservoir, remainingSampleMass);
			Lib.Proto.Set(m, "remainingSampleMass", remainingSampleMass);
			return delta;
		}

		internal double RestoreSampleMass(double restoredAmount, string id)
		{
			if (Broken) return 0;
			if (sample_collecting || experiment_id != id) return 0;
			if (remainingSampleMass >= sample_reservoir) return 0;
			double delta = Math.Max(restoredAmount, sample_reservoir - remainingSampleMass);
			remainingSampleMass += delta;
			remainingSampleMass = Math.Min(sample_reservoir, remainingSampleMass);
			return delta;
		}

		// IPartMassModifier
		public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) { return (float)remainingSampleMass; }
		public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

#endregion
	}

	internal class EditorTracker
	{
		private static EditorTracker instance;
		private readonly List<Experiment> experiments = new List<Experiment>();

		static EditorTracker()
		{
			if (instance == null)
				instance = new EditorTracker();
		}

		private EditorTracker()
		{
			if(instance == null) {
				instance = this;
				GameEvents.onEditorShipModified.Add(instance.ShipModified);
			}
		}

		internal void ShipModified(ShipConstruct construct)
		{
			experiments.Clear();
			foreach(var part in construct.Parts)
			{
				foreach (var experiment in part.FindModulesImplementing<Experiment>())
				{
					if (!experiment.enabled) experiment.State = Experiment.RunningState.Stopped;
					if (experiment.Running && !AllowStart(experiment))
					{
						// An experiment was added in recording state? Cheeky bugger!
						experiment.State = Experiment.RunningState.Stopped;
						experiment.deployAnimator.Still(0);
					}
					experiments.Add(experiment);
				}
			}
		}

		internal bool AllowStart(Experiment experiment)
		{
			foreach (var e in experiments)
				if (e.Running && e.experiment_id == experiment.experiment_id)
					return false;
			return true;
		}

		internal static EditorTracker Instance
		{
			get
			{
				if (instance == null)
					instance = new EditorTracker();
				return instance;
			}
		}
	}
} // KERBALISM
