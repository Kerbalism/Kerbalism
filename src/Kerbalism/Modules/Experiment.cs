using System;
using System.Collections.Generic;
using Experience;
using UnityEngine;
using KSP.Localization;
using System.Collections;
using static KERBALISM.ExperimentRequirements;
using System.Linq;

namespace KERBALISM
{

	public class Experiment : PartModule, ISpecifics, IModuleInfo, IPartMassModifier, IConfigurable, IMultipleDragCube
	{
		// config
		[KSPField] public string experiment_id;               // id of associated experiment definition
		[KSPField] public string experiment_desc = string.Empty;  // some nice lines of text
		[KSPField] public double data_rate;                   // sampling rate in Mb/s
		[KSPField] public double ec_rate;                     // EC consumption rate per-second
		[KSPField] public double sample_amount = 0.0;         // the amount of samples this unit is shipped with
		[KSPField] public bool sample_collecting = false;     // if set to true, the experiment will generate mass out of nothing
		[KSPField] public bool allow_shrouded = true;         // true if data can be transmitted
		[KSPField] public string requires = string.Empty;     // additional requirements that must be met
		[KSPField] public string crew_operate = string.Empty; // operator crew. if set, crew has to be on vessel while recording
		[KSPField] public string crew_reset = string.Empty;   // reset crew. if set, experiment will stop recording after situation change
		[KSPField] public string crew_prepare = string.Empty; // prepare crew. if set, experiment will require crew to set up before it can start recording 
		[KSPField] public string resources = string.Empty;    // resources consumed by this experiment
		[KSPField] public bool hide_when_unavailable = false; // don't show UI when the experiment is unavailable
		[KSPField] public string retractedDragCube = "Retracted";
		[KSPField] public string deployedDragCube = "Deployed";
		[KSPField] public bool use_animation_group = false;   // if true, deploy/retract animations will managed by the first found ModuleAnimationGroup

		// animations
		[KSPField] public string anim_deploy = string.Empty; // deploy animation
		[KSPField] public bool anim_deploy_reverse = false;

		[KSPField] public string anim_loop = string.Empty; // loop animation
		[KSPField] public bool anim_loop_reverse = false;

		// persistence
		[KSPField(isPersistant = true)] public string issue = string.Empty;
		[KSPField(isPersistant = true)] public int situationId;
		[KSPField(isPersistant = true)] public bool didPrepare = false;
		[KSPField(isPersistant = true)] public bool shrouded = false;
		[KSPField(isPersistant = true)] public double remainingSampleMass = 0.0;
		[KSPField(isPersistant = true)] public uint privateHdId = 0;
		[KSPField(isPersistant = true)] public bool firstStart = true;

		/// <summary> never set this directly, use the "State" property </summary>
		[KSPField(isPersistant = true)] private RunningState expState = RunningState.Stopped;
		[KSPField(isPersistant = true)] private ExpStatus status = ExpStatus.Stopped;

		public ExperimentInfo ExpInfo { get; set; }
		private Situation situation;
		public SubjectData Subject => subject; private SubjectData subject;

		public ExperimentRequirements Requirements { get; private set; }
		public List<ObjectPair<string, double>> ResourceDefs { get; private set; }

		// animations
		internal Animator deployAnimator;
		internal Animator loopAnimator;
		public ModuleAnimationGroup AnimationGroup { get; private set; }

		private CrewSpecs operator_cs;
		private CrewSpecs reset_cs;
		private CrewSpecs prepare_cs;

		public bool isConfigurable = false;

		#region state/status

		public enum ExpStatus { Stopped, Running, Forced, Waiting, Issue, Broken }
		public enum RunningState { Stopped, Running, Forced, Broken }

		public RunningState State
		{
			get => expState;
			set
			{
				expState = value;

				var newStatus = GetStatus(value, Subject, issue);
				API.OnExperimentStateChanged.Notify(vessel, experiment_id, status, newStatus);
				status = newStatus;
			}
		}

		public ExpStatus Status => status;

		public static ExpStatus GetStatus(RunningState state, SubjectData subject, string issue)
		{
			switch (state)
			{
				case RunningState.Broken:
					return ExpStatus.Broken;
				case RunningState.Stopped:
					return ExpStatus.Stopped;
				case RunningState.Running:
					if (issue.Length > 0) return ExpStatus.Issue;
					if (subject == null || subject.ScienceRemainingToCollect <= 0.0) return ExpStatus.Waiting;
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

		public void Configure(bool enable)
		{
			enabled = enable;
			isEnabled = enable;
		}

		public void ModuleIsConfigured() => isConfigurable = true;

		public override void OnLoad(ConfigNode node)
		{
			if (HighLogic.LoadedScene == GameScenes.LOADING)
			{
				ResourceDefs = ParseResources(resources);
				Requirements = new ExperimentRequirements(requires);
			}

			if (use_animation_group)
				AnimationGroup = part.Modules.OfType<ModuleAnimationGroup>().FirstOrDefault();

			base.OnLoad(node);
		}

		public override void OnStart(StartState state)
		{
			// create animators
			deployAnimator = new Animator(part, anim_deploy);
			deployAnimator.reversed = anim_deploy_reverse;

			loopAnimator = new Animator(part, anim_loop);
			loopAnimator.reversed = anim_loop_reverse;

			// set initial animation states
			deployAnimator.Still(Running ? 1.0 : 0.0);
			SetDragCubes(Running);

			loopAnimator.Still(Running ? 1.0 : 0.0);
			if (Running) loopAnimator.Play(false, true);

			if (use_animation_group && AnimationGroup == null)
				AnimationGroup = part.Modules.OfType<ModuleAnimationGroup>().FirstOrDefault();

			if (AnimationGroup != null && !AnimationGroup.isDeployed && Running)
				AnimationGroup.DeployModule();

			// parse crew specs
			if (!string.IsNullOrEmpty(crew_operate))
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

			ExpInfo = ScienceDB.GetExperimentInfo(experiment_id);

			if (ExpInfo == null)
			{
				enabled = isEnabled = moduleIsEnabled = false;
				Lib.Log($"Error : ExpInfo for experiment_id `{experiment_id}` is null, does the config definition exists ?");
				return;
			}

			if (Lib.IsFlight())
			{
				foreach (var hd in part.FindModulesImplementing<HardDrive>())
				{
					if (hd.experiment_id == experiment_id) privateHdId = part.flightID;
				}

				if (firstStart)
				{
					FirstStart();
					firstStart = false;
				}
			}
		}

		private void FirstStart()
		{
			// initialize the remaining sample mass
			// this needs to be done only once just after launch
			if (!sample_collecting && ExpInfo.SampleMass > 0.0 && remainingSampleMass == 0)
			{
				remainingSampleMass = ExpInfo.SampleMass * sample_amount;
				if (double.IsNaN(remainingSampleMass))
					Lib.LogDebug("ERROR: remainingSampleMass is NaN on first start " + ExpInfo.ExperimentId + " " + ExpInfo.SampleMass + " / " + sample_amount);
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

		public virtual void Update()
		{
			// in flight
			if (Lib.IsFlight())
			{
				VesselData vd = vessel.KerbalismData();
				if (!vd.IsSimulated) return;

				if (prepare_cs == null || didPrepare || (hide_when_unavailable && status != ExpStatus.Issue))
				{
					Events["ToggleEvent"].active = true;
					Events["ShowPopup"].active = true;

					if (subject != null)
					{
						Events["ToggleEvent"].guiName = Lib.StatusToggle(Lib.Ellipsis(ExpInfo.Title, Styles.ScaleStringLength(25)), StatusInfo(status, issue));
						Events["ShowPopup"].guiName = Lib.StatusToggle("info", Lib.BuildString(ScienceValue(Subject), " ", State == RunningState.Forced ? subject.PercentCollectedTotal.ToString("P0") : RunningCountdown(ExpInfo, Subject, data_rate)));
					}
					else
					{
						Events["ToggleEvent"].guiName = Lib.StatusToggle(Lib.Ellipsis(ExpInfo.Title, Styles.ScaleStringLength(25)), StatusInfo(status, issue));
						Events["ShowPopup"].guiName = Lib.StatusToggle("info", vd.VesselSituations.FirstSituationTitle);
					}
				}
				else
				{
					Events["ToggleEvent"].active = false;
					Events["ShowPopup"].active = false;
				}

				Events["Prepare"].guiName = Lib.BuildString(Local.Module_Experiment_Prepare +" <b>", ExpInfo.Title, "</b>");//Prepare
				Events["Prepare"].active = !didPrepare && prepare_cs != null && subject == null;

				Events["Reset"].guiName = Lib.BuildString(Local.Module_Experiment_Reset +" <b>", ExpInfo.Title, "</b>");//Reset
				// we need a reset either if we have recorded data or did a setup
				bool resetActive = (reset_cs != null || prepare_cs != null) && subject != null;
				Events["Reset"].active = resetActive;
			}
			// in the editor
			else if (Lib.IsEditor())
			{
				// update ui
				Events["ToggleEvent"].guiName = Lib.StatusToggle(ExpInfo.Title, StatusInfo(status, issue));
				Events["Reset"].active = false;
				Events["Prepare"].active = false;
			}
		}

		public virtual void FixedUpdate()
		{
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.Experiment.FixedUpdate");

			// basic sanity checks
			if (Lib.IsEditor())
			{
				UnityEngine.Profiling.Profiler.EndSample();
				return;
			}

			VesselData vd = vessel.KerbalismData();

			if (!vd.IsSimulated)
			{
				UnityEngine.Profiling.Profiler.EndSample();
				return;
			}

			if (!Running)
			{
				situation = GetSituation(vd);
				subject = ScienceDB.GetSubjectData(ExpInfo, situation, out situationId);
				UnityEngine.Profiling.Profiler.EndSample();
				return;
			}

			if (AnimationGroup != null && !AnimationGroup.isDeployed && Running)
			{
				situation = GetSituation(vd);
				subject = ScienceDB.GetSubjectData(ExpInfo, situation, out situationId);
				UnityEngine.Profiling.Profiler.EndSample();
				Toggle();
				return;
			}

			shrouded = part.ShieldedFromAirstream;

			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.Experiment.FixedUpdate.RunningUpdate");
			RunningUpdate(
				vessel, vd, GetSituation(vd), this, privateHdId, didPrepare, shrouded,
				ResourceCache.GetResource(vessel, "ElectricCharge"),
				ResourceCache.Get(vessel),
				ResourceDefs,
				ExpInfo,
				expState,
				Kerbalism.elapsed_s,
				ref situationId,
				ref remainingSampleMass,
				out subject,
				out issue);
			UnityEngine.Profiling.Profiler.EndSample();

			var newStatus = GetStatus(expState, subject, issue);
			API.OnExperimentStateChanged.Notify(vessel, experiment_id, status, newStatus);
			status = newStatus;

			UnityEngine.Profiling.Profiler.EndSample();
		}

		// note : we use a non-static method so it can be overriden
		public virtual void BackgroundUpdate(Vessel v, VesselData vd, ProtoPartModuleSnapshot m, ResourceInfo ec, VesselResources resources, double elapsed_s)
		{
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.Experiment.BackgroundUpdate");

			if (!vd.IsSimulated)
			{
				UnityEngine.Profiling.Profiler.EndSample();
				return;
			}

			RunningState expState = Lib.Proto.GetEnum(m, "expState", RunningState.Stopped);
			ExperimentInfo expInfo = ScienceDB.GetExperimentInfo(experiment_id); // from prefab

			if (!IsRunning(expState))
			{
				int notRunningSituationId = Situation.GetBiomeAgnosticIdForExperiment(GetSituation(vd).Id, expInfo);
				Lib.Proto.Set(m, "situationId", notRunningSituationId);
				UnityEngine.Profiling.Profiler.EndSample();
				return;
			}

			bool didPrepare = Lib.Proto.GetBool(m, "didPrepare", false);
			bool shrouded = Lib.Proto.GetBool(m, "shrouded", false);
			int situationId = Lib.Proto.GetInt(m, "situationId", 0);
			double remainingSampleMass = Lib.Proto.GetDouble(m, "remainingSampleMass", 0.0);
			uint privateHdId = Lib.Proto.GetUInt(m, "privateHdId", 0u);
			var oldStatus = Lib.Proto.GetEnum<ExpStatus>(m, "status", ExpStatus.Stopped);

			string issue;
			SubjectData subjectData;

			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.Experiment.BackgroundUpdate.RunningUpdate");
			RunningUpdate(
				v, vd, GetSituation(vd), this, privateHdId, didPrepare, shrouded, // "this" is the prefab
				ec,
				resources,
				ResourceDefs, // from prefab
				expInfo,
				expState,
				elapsed_s,
				ref situationId,
				ref remainingSampleMass,
				out subjectData,
				out issue);
			UnityEngine.Profiling.Profiler.EndSample();

			var newStatus = GetStatus(expState, subjectData, issue);
			Lib.Proto.Set(m, "situationId", situationId);
			Lib.Proto.Set(m, "status", newStatus);
			Lib.Proto.Set(m, "issue", issue);

			if (expInfo.SampleMass > 0.0)
				Lib.Proto.Set(m, "remainingSampleMass", remainingSampleMass);

			API.OnExperimentStateChanged.Notify(v, experiment_id, oldStatus, newStatus);

			UnityEngine.Profiling.Profiler.EndSample();
		}

		private static void RunningUpdate(
			Vessel v, VesselData vd, Situation vs, Experiment prefab, uint hdId, bool didPrepare, bool isShrouded,
			ResourceInfo ec, VesselResources resources, List<ObjectPair<string, double>> resourceDefs,
			ExperimentInfo expInfo, RunningState expState, double elapsed_s,
			ref int lastSituationId, ref double remainingSampleMass, out SubjectData subjectData, out string mainIssue)
		{
			mainIssue = string.Empty;

			subjectData = ScienceDB.GetSubjectData(expInfo, vs);

			bool subjectHasChanged;
			if (subjectData != null)
			{
				subjectHasChanged = lastSituationId != subjectData.Situation.Id;
				lastSituationId = subjectData.Situation.Id;
			}
			else
			{
				lastSituationId = vd.VesselSituations.FirstSituation.Id;
				mainIssue = Local.Module_Experiment_issue1;//"invalid situation"
				return;
			}

			double scienceRemaining = subjectData.ScienceRemainingToCollect;

			if (expState != RunningState.Forced && scienceRemaining <= 0.0)
				return;

			if (isShrouded && !prefab.allow_shrouded)
			{
				mainIssue = Local.Module_Experiment_issue2;//"shrouded"
				return;
			}

			if (subjectHasChanged && prefab.crew_reset.Length > 0)
			{
				mainIssue = Local.Module_Experiment_issue3;//"reset required"
				return;
			}

			if (ec.Amount == 0.0 && prefab.ec_rate > 0.0)
			{
				mainIssue = Local.Module_Experiment_issue4;//"no Electricity"
				return;
			}

			if (!string.IsNullOrEmpty(prefab.crew_operate))
			{
				var cs = new CrewSpecs(prefab.crew_operate);
				if (!cs && Lib.CrewCount(v) > 0)
				{
					mainIssue = Local.Module_Experiment_issue5;//"crew on board"
					return;
				}
				else if (cs && !cs.Check(v))
				{
					mainIssue = cs.Warning();
					return;
				}
			}

			if (!prefab.sample_collecting && remainingSampleMass <= 0.0 && expInfo.SampleMass > 0.0)
			{
				mainIssue = Local.Module_Experiment_issue6;//"depleted"
				return;
			}

			if (!didPrepare && !string.IsNullOrEmpty(prefab.crew_prepare))
			{
				mainIssue = Local.Module_Experiment_issue7;//"not prepared"
				return;
			}

			if (!v.loaded && subjectData.Situation.AtmosphericFlight())
			{
				mainIssue = Local.Module_Experiment_issue8;//"background flight"
				return;
			}

			RequireResult[] reqResults;
			if (!prefab.Requirements.TestRequirements(v, out reqResults))
			{
				mainIssue = Local.Module_Experiment_issue9;//"unmet requirement"
				return;
			}

			if (!HasRequiredResources(v, resourceDefs, resources, out mainIssue))
			{
				mainIssue = Local.Module_Experiment_issue10;//"missing resource"
				return;
			}

			double chunkSizeMax = prefab.data_rate * elapsed_s;
			double chunkSize;
			if (expState != RunningState.Forced)
				chunkSize = Math.Min(chunkSizeMax, scienceRemaining / subjectData.SciencePerMB);
			else
				chunkSize = chunkSizeMax;

			Drive drive = GetDrive(vd, hdId, chunkSize, subjectData);
			if (drive == null)
			{
				mainIssue = Local.Module_Experiment_issue11;//"no storage space"
				return;
			}

			Drive warpDrive = null;
			double available;
			if (!expInfo.IsSample)
			{
				available = drive.FileCapacityAvailable();
				if (drive.GetFileSend(subjectData.Id))
				{
					warpDrive = Cache.WarpCache(v);
					available += warpDrive.FileCapacityAvailable();
				}
			}
			else
			{
				available = drive.SampleCapacityAvailable(subjectData);
			}

			if (available <= 0.0)
			{
				mainIssue = Local.Module_Experiment_issue11;//"no storage space"
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

			if (prefab.ec_rate > 0.0)
				prodFactor = Math.Min(prodFactor, Lib.Clamp(ec.Amount / (prefab.ec_rate * elapsed_s), 0.0, 1.0));

			foreach (ObjectPair<string, double> p in resourceDefs)
			{
				if (p.Value <= 0.0) continue;
				ResourceInfo ri = resources.GetResource(v, p.Key);
				prodFactor = Math.Min(prodFactor, Lib.Clamp(ri.Amount / (p.Value * elapsed_s), 0.0, 1.0));
			}

			if (prodFactor == 0.0)
			{
				mainIssue = Local.Module_Experiment_issue10;//"missing resource"
				return;
			}

			chunkSize = chunkSizeMax * prodFactor;
			elapsed_s *= prodFactor;
			double massDelta = chunkSize * expInfo.MassPerMB;

#if DEBUG || DEVBUILD
			if (Double.IsNaN(chunkSize))
				Lib.Log("ERROR: chunkSize is NaN " + expInfo.ExperimentId + " " + chunkSizeMax + " / " + prodFactor + " / " + available + " / " + ec.Amount + " / " + prefab.ec_rate + " / " + prefab.data_rate);

			if (Double.IsNaN(massDelta))
				Lib.Log("ERROR: mass delta is NaN " + expInfo.ExperimentId + " " + expInfo.SampleMass + " / " + chunkSize + " / " + expInfo.DataSize);
#endif

			if (!expInfo.IsSample)
			{
				if (warpDrive != null)
				{
					double s = Math.Min(chunkSize, warpDrive.FileCapacityAvailable());
					warpDrive.Record_file(subjectData, s, true);

					if (chunkSize > s) // only write to persisted drive if the data cannot be transmitted in this tick
						drive.Record_file(subjectData, chunkSize - s, true);
					else if (!drive.files.ContainsKey(subjectData)) // if everything is transmitted, create an empty file so the player know what is happening
						drive.Record_file(subjectData, 0.0, true);
				}
				else
				{
					drive.Record_file(subjectData, chunkSize, true);
				}
			}
			else
			{
				drive.Record_sample(subjectData, chunkSize, massDelta);
			}

			// consume resources
			ec.Consume(prefab.ec_rate * elapsed_s, ResourceBroker.Experiment);
			foreach (ObjectPair<string, double> p in resourceDefs)
				resources.Consume(v, p.Key, p.Value * elapsed_s, ResourceBroker.Experiment);

			if (!prefab.sample_collecting)
			{
				remainingSampleMass -= massDelta;
				remainingSampleMass = Math.Max(remainingSampleMass, 0.0);
			}
		}

		public virtual Situation GetSituation(VesselData vd)
		{
			return vd.VesselSituations.GetExperimentSituation(ExpInfo);
		}

		private static Drive GetDrive(VesselData vesselData, uint hdId, double chunkSize, SubjectData subjectData)
		{
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.Experiment.GetDrive");
			bool isFile = subjectData.ExpInfo.SampleMass == 0.0;
			Drive drive = null;
			if (hdId != 0)
				drive = vesselData.GetPartData(hdId).Drive;
			else
				drive = isFile ? Drive.FileDrive(vesselData, chunkSize) : Drive.SampleDrive(vesselData, chunkSize, subjectData);
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
				var ri = res.GetResource(v, p.Key);
				if (ri.Amount == 0.0)
				{
					issue = Localizer.Format("#KERBALISM_Module_Experiment_issue12", ri.ResourceName);//"missing " + 
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
						PostMultipleRunsMessage(ExpInfo.Title, "");
						return State;
					}
					State = RunningState.Running;
				}

				if (AnimationGroup != null)
				{
					// extend automatically, retract manually
					if (Running && !AnimationGroup.isDeployed)
						AnimationGroup.DeployModule();
				}
				else
				{
					deployAnimator.Play(!Running, false);
					SetDragCubes(Running);
				}

				GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
				return State;
			}

			if (Lib.IsFlight() && !vessel.IsControllable)
				return State;

			// nervous clicker? wait for it, goddamnit.
			if ((AnimationGroup != null && AnimationGroup.DeployAnimation.isPlaying) || deployAnimator.Playing())
				return State;

			if (Running)
			{
				if (setForcedRun && expState == RunningState.Running)
				{
					State = RunningState.Forced;
					return State;
				}
				// stop experiment
				// plays the deploy animation in reverse
				// if an external deploy animation module is used, we don't retract automatically
				Action stop = delegate ()
				{
					State = RunningState.Stopped;
					deployAnimator.Play(true, false);
					SetDragCubes(false);
				};

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
					PostMultipleRunsMessage(ExpInfo.Title, vessel.vesselName);
					return State;
				}

				// start experiment
				// play the deploy animation, when it's done start the loop animation
				if (AnimationGroup != null)
				{
					if (!AnimationGroup.isDeployed)
						AnimationGroup.DeployModule();

					State = setForcedRun ? RunningState.Forced : RunningState.Running;
				}
				else
				{
					deployAnimator.Play(false, false, delegate ()
					{
						State = setForcedRun ? RunningState.Forced : RunningState.Running;
						loopAnimator.Play(false, true);
						SetDragCubes(true);
					});
				}
			}
			return State;
		}

		public static RunningState ProtoToggle(Vessel v, Experiment prefab, ProtoPartModuleSnapshot protoModule, bool setForcedRun = false)
		{
			RunningState expState = Lib.Proto.GetEnum(protoModule, "expState", RunningState.Stopped);

			if (expState == RunningState.Broken)
			{
				ProtoSetState(v, prefab, protoModule, expState);
				return expState;
			}

			if (!IsRunning(expState))
			{
				if (IsExperimentRunningOnVessel(v, prefab.experiment_id))
				{
					PostMultipleRunsMessage(ScienceDB.GetExperimentInfo(prefab.experiment_id).Title, v.vesselName);
					{
						ProtoSetState(v, prefab, protoModule, expState);
						return expState;
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

			ProtoSetState(v, prefab, protoModule, expState);
			return expState;
		}

		private static void ProtoSetState(Vessel v, Experiment prefab, ProtoPartModuleSnapshot protoModule, RunningState expState)
		{
			Lib.Proto.Set(protoModule, "expState", expState);

			var oldStatus = Lib.Proto.GetEnum<ExpStatus>(protoModule, "status", ExpStatus.Stopped);
			var newStatus = GetStatus(expState,
				ScienceDB.GetSubjectData(ScienceDB.GetExperimentInfo(prefab.experiment_id), prefab.GetSituation(v.KerbalismData())),
				Lib.Proto.GetString(protoModule, "issue")
			);
			Lib.Proto.Set(protoModule, "status", newStatus);

			API.OnExperimentStateChanged.Notify(v, prefab.experiment_id, oldStatus, newStatus);
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
		[KSPEvent(guiActiveUnfocused = true, guiActive = true, guiActiveEditor = true, guiName = "_", active = true, groupName = "Science", groupDisplayName = "#KERBALISM_Group_Science")]//Science
#endif
		public void ToggleEvent()
		{
			Toggle();
		}

#if KSP15_16
		[KSPEvent(guiActiveUnfocused = true, guiActive = true, guiName = "_", active = true)]
#else
		[KSPEvent(guiActiveUnfocused = true, guiActive = true, guiName = "_", active = true, groupName = "Science", groupDisplayName = "#KERBALISM_Group_Science")]//Science
#endif
		public void ShowPopup()
		{
			new ExperimentPopup(vessel, this, part.flightID, part.partInfo.title);
		}

#if KSP15_16
		[KSPEvent(guiActiveUnfocused = true, guiName = "_", active = false)]
#else
		[KSPEvent(guiActiveUnfocused = true, guiName = "_", active = false, groupName = "Science", groupDisplayName = "#KERBALISM_Group_Science")]//Science
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
					Local.Module_Experiment_Message1,//"I'm not qualified for this"
					Local.Module_Experiment_Message2,//"I will not even know where to start"
					Local.Module_Experiment_Message3//"I'm afraid I can't do that"
				  ),
				  reset_cs.Warning()
				);
			}

			didPrepare = true;

			Message.Post(
			  Local.Module_Experiment_Message4,//"Preparation Complete"
			  Lib.TextVariant
			  (
				Local.Module_Experiment_Message5,//"Ready to go"
				Local.Module_Experiment_Message6//"Let's start doing some science!"
			  )
			);
		}

#if KSP15_16
		[KSPEvent(guiActiveUnfocused = true, guiName = "_", active = false)]
#else
		[KSPEvent(guiActiveUnfocused = true, guiName = "_", active = false, groupName = "Science", groupDisplayName = "#KERBALISM_Group_Science")]//Science
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
				if (showMessage)
				{
					Message.Post(
					  Lib.TextVariant
					  (
						Local.Module_Experiment_Message1,//"I'm not qualified for this"
						Local.Module_Experiment_Message2,//"I will not even know where to start"
						Local.Module_Experiment_Message3//"I'm afraid I can't do that"
					  ),
					  reset_cs.Warning()
					);
				}
				return false;
			}

			situationId = 0;
			didPrepare = false;

			if (showMessage)
			{
				Message.Post(
				  Local.Module_Experiment_Message7,//"Reset Done"
				  Lib.TextVariant
				  (
					Local.Module_Experiment_Message8,//"It's good to go again"
					Local.Module_Experiment_Message9//"Ready for the next bit of science"
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

		public static string RunningStateInfo(RunningState state)
		{
			switch (state)
			{
				case RunningState.Stopped: return Lib.Color(Local.Module_Experiment_runningstate1, Lib.Kolor.Yellow);//"stopped"
				case RunningState.Running: return Lib.Color(Local.Module_Experiment_runningstate2, Lib.Kolor.Green);//"started"
				case RunningState.Forced: return Lib.Color(Local.Module_Experiment_runningstate3, Lib.Kolor.Red);//"forced run"
				case RunningState.Broken: return Lib.Color(Local.Module_Experiment_runningstate4, Lib.Kolor.Red);//"broken"
				default: return string.Empty;
			}

		}

		public static string StatusInfo(ExpStatus status, string issue = null)
		{
			switch (status)
			{
				case ExpStatus.Stopped: return Lib.Color(Local.Module_Experiment_runningstate1, Lib.Kolor.Yellow);//"stopped"
				case ExpStatus.Running: return Lib.Color(Local.Module_Experiment_runningstate5, Lib.Kolor.Green);//"running"
				case ExpStatus.Forced: return Lib.Color(Local.Module_Experiment_runningstate3, Lib.Kolor.Red);//"forced run"
				case ExpStatus.Waiting: return Lib.Color(Local.Module_Experiment_runningstate6, Lib.Kolor.Science);//"waiting"
				case ExpStatus.Broken: return Lib.Color(Local.Module_Experiment_runningstate4, Lib.Kolor.Red);//"broken"
				case ExpStatus.Issue: return Lib.Color(string.IsNullOrEmpty(issue) ? Local.Module_Experiment_issue_title : issue, Lib.Kolor.Orange);//"issue"
				default: return string.Empty;
			}
		}

		public static string RunningCountdown(ExperimentInfo expInfo, SubjectData subjectData, double dataRate, bool compact = true)
		{
			double count;
			if (subjectData != null)
				count = Math.Max(1.0 - subjectData.PercentCollectedTotal, 0.0) * (expInfo.DataSize / dataRate);
			else
				count = expInfo.DataSize / dataRate;

			return Lib.HumanReadableCountdown(count, compact);
		}

		public static string ScienceValue(SubjectData subjectData)
		{
			if (subjectData != null)
				return Lib.BuildString(Lib.HumanReadableScience(subjectData.ScienceCollectedTotal), " / ", Lib.HumanReadableScience(subjectData.ScienceMaxValue));
			else
				return Lib.Color(Local.Module_Experiment_ScienceValuenone, Lib.Kolor.Science, true);//"none"
		}

		// specifics support
		public Specifics Specs()
		{
			Specifics specs = SpecsWithoutRequires(ExpInfo, this);

			if (Requirements.Requires.Length > 0)
			{
				specs.Add(string.Empty);
				specs.Add(Lib.Color(Local.Module_Experiment_Requires, Lib.Kolor.Cyan, true));//"Requires:"
				foreach (RequireDef req in Requirements.Requires)
					specs.Add(Lib.BuildString("• <b>", ReqName(req.require), "</b>"), ReqValueFormat(req.require, req.value));
			}

			return specs;
		}

		public static Specifics SpecsWithoutRequires(ExperimentInfo expInfo, Experiment prefab)
		{
			var specs = new Specifics();
			if (expInfo == null)
			{
				specs.Add(Local.ExperimentInfo_Unknown);
				return specs;
			}

			if (!string.IsNullOrEmpty(prefab.experiment_desc))
			{
				specs.Add(Lib.BuildString("<i>", prefab.experiment_desc, "</i>"));
				specs.Add(string.Empty);
			}

			double expSize = expInfo.DataSize;
			if (expInfo.SampleMass == 0.0)
			{
				specs.Add(Local.Module_Experiment_Specifics_info1, Lib.HumanReadableDataSize(expSize));//"Data size"
				specs.Add(Local.Module_Experiment_Specifics_info2, Lib.HumanReadableDataRate(prefab.data_rate));//"Data rate"
				specs.Add(Local.Module_Experiment_Specifics_info3, Lib.HumanReadableDuration(expSize / prefab.data_rate));//"Duration"
			}
			else
			{
				specs.Add(Local.Module_Experiment_Specifics_info4, Lib.HumanReadableSampleSize(expSize));//"Sample size"
				specs.Add(Local.Module_Experiment_Specifics_info5, Lib.HumanReadableMass(expInfo.SampleMass));//"Sample mass"
				if (expInfo.SampleMass > 0.0 && !prefab.sample_collecting)
					specs.Add(Local.Module_Experiment_Specifics_info6, prefab.sample_amount.ToString("F2"));//"Samples"
				specs.Add(Local.Module_Experiment_Specifics_info7_sample, Lib.HumanReadableDuration(expSize / prefab.data_rate));//"Duration"
			}

			List<string> situations = expInfo.AvailableSituations();
			if (situations.Count > 0)
			{
				specs.Add(string.Empty);
				specs.Add(Lib.Color(Local.Module_Experiment_Specifics_Situations, Lib.Kolor.Cyan, true));//"Situations:"
				foreach (string s in situations) specs.Add(Lib.BuildString("• <b>", s, "</b>"));
			}

			if (expInfo.ExpBodyConditions.HasConditions)
			{
				specs.Add(string.Empty);
				specs.Add(expInfo.ExpBodyConditions.ConditionsToString());
			}

			specs.Add(string.Empty);
			specs.Add(Lib.Color(Local.Module_Experiment_Specifics_info8, Lib.Kolor.Cyan, true));//"Needs:"

			specs.Add(Local.Module_Experiment_Specifics_info9, Lib.HumanReadableRate(prefab.ec_rate));//"EC"
			foreach (var p in ParseResources(prefab.resources))
				specs.Add(p.Key, Lib.HumanReadableRate(p.Value));

			if (prefab.crew_prepare.Length > 0)
			{
				var cs = new CrewSpecs(prefab.crew_prepare);
				specs.Add(Local.Module_Experiment_Specifics_info10, cs ? cs.Info() : Local.Module_Experiment_Specifics_info10_none);//"Preparation""none"
			}
			if (prefab.crew_operate.Length > 0)
			{
				var cs = new CrewSpecs(prefab.crew_operate);
				specs.Add(Local.Module_Experiment_Specifics_info11, cs ? cs.Info() : Local.Module_Experiment_Specifics_info11_unmanned);//"Operation""unmanned"
			}
			if (prefab.crew_reset.Length > 0)
			{
				var cs = new CrewSpecs(prefab.crew_reset);
				specs.Add(Local.Module_Experiment_Specifics_info12, cs ? cs.Info() : Local.Module_Experiment_Specifics_info12_none);//"Reset""none"
			}

			return specs;
		}

		// part tooltip
		public override string GetInfo()
		{
			if (!isConfigurable && ExpInfo != null)
				return Specs().Info();

			return string.Empty;
		}

		// IModuleInfo
		public string GetModuleTitle() => ExpInfo != null ? ExpInfo.Title : "";
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
			Message.Post(Lib.Color(Local.Module_Experiment_MultipleRunsMessage_title, Lib.Kolor.Orange, true), Localizer.Format("#KERBALISM_Module_Experiment_MultipleRunsMessage", title,vesselName));//"ALREADY RUNNING""Can't start " +  + " a second time on vessel " + 
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
			double maxSampleMass = ExpInfo.SampleMass * sample_amount;
			if (remainingSampleMass >= maxSampleMass) return 0;
			double delta = Math.Max(restoredAmount, maxSampleMass - remainingSampleMass);
			remainingSampleMass += delta;
			remainingSampleMass = Math.Min(maxSampleMass, remainingSampleMass);
			return delta;
		}

		// IPartMassModifier
		public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) {
			if (Double.IsNaN(remainingSampleMass))
			{
#if DEBUG || DEVBUILD // this is logspammy, don't do it in releases
				Lib.Log("ERROR: Experiment remaining sample mass is NaN " + experiment_id);
#endif
				return 0;
			}
			return (float)remainingSampleMass;
		}
		public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

		#endregion

		#region drag cubes

		private void SetDragCubes(bool deployed)
		{
			if (deployAnimator == null)
				return;

			part.DragCubes.SetCubeWeight(retractedDragCube, deployed ? 0f : 1f);
			part.DragCubes.SetCubeWeight(deployedDragCube, deployed ? 1f : 0f);
		}


		public bool IsMultipleCubesActive
		{
			get
			{
				if (deployAnimator == null)
				{
					deployAnimator = new Animator(part, anim_deploy);
					deployAnimator.reversed = anim_deploy_reverse;
				}
				return deployAnimator.IsDefined;
			}
		}

		public string[] GetDragCubeNames() => new string[] { retractedDragCube, deployedDragCube };

		public void AssumeDragCubePosition(string name)
		{
			if (deployAnimator == null)
			{
				deployAnimator = new Animator(part, anim_deploy);
				deployAnimator.reversed = anim_deploy_reverse;
			}

			if (name == retractedDragCube)
				deployAnimator.Still(0.0);
			else if (name == deployedDragCube)
				deployAnimator.Still(1.0);
		}

		public bool UsesProceduralDragCubes() => false;

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
