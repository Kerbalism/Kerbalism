using System;
using System.Collections.Generic;
using Experience;
using UnityEngine;
using KSP.Localization;
using System.Collections;
using static KERBALISM.ExperimentRequirements;
using System.Linq;
using KERBALISM.Planner;

namespace KERBALISM
{

	public class ModuleKsmExperiment : PartModule, ISpecifics, IModuleInfo, IPartMassModifier, IMultipleDragCube, IPlannerModule
	{
		/// <summary> name of the associated experiment module definition </summary>
		[KSPField] public string id = string.Empty;

		/// <summary> don't show UI when the experiment is unavailable </summary>
		[KSPField] public bool hide_when_invalid = false;

		/// <summary> if true, deploy/retract animations will managed by the first found ModuleAnimationGroup </summary>
		[KSPField] public bool use_animation_group = false;
		[KSPField] public string retractedDragCube = "Retracted";
		[KSPField] public string deployedDragCube = "Deployed";

		/// <summary> if true, the experiment can run when shrouded (in bay or fairing) </summary>
		[KSPField] public bool allow_shrouded = true;

		// animations
		[KSPField] public string anim_deploy = string.Empty; // deploy animation
		[KSPField] public bool anim_deploy_reverse = false;

		[KSPField] public string anim_loop = string.Empty; // loop animation
		[KSPField] public bool anim_loop_reverse = false;

		// persistence
		[KSPField(isPersistant = true)] public string issue = string.Empty;
		[KSPField(isPersistant = true)] public int situationId;
		[KSPField(isPersistant = true)] public bool shrouded = false;
		[KSPField(isPersistant = true)] public double remainingSampleMass = 0.0;
		[KSPField(isPersistant = true)] public uint privateHdId = 0;
		[KSPField(isPersistant = true)] public bool firstStart = true;

		/// <summary> never set this directly, use the "State" property </summary>
		[KSPField(isPersistant = true)] private RunningState expState = RunningState.Stopped;
		[KSPField(isPersistant = true)] private ExpStatus status = ExpStatus.Stopped;

		public ExperimentModuleDefinition ModuleDefinition { get; set; }
		private Situation situation;
		public SubjectData Subject => subject; private SubjectData subject;

		// animations
		internal Animator deployAnimator;
		internal Animator loopAnimator;
		public ModuleAnimationGroup AnimationGroup { get; private set; }

		public string ExperimentID
		{
			get
			{
				return ModuleDefinition.Info.ExperimentId;				
			}
		}

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
				API.OnExperimentStateChanged.Notify(vessel, ExperimentID, status, newStatus);
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

		public override void OnLoad(ConfigNode node)
		{
			if (HighLogic.LoadedScene == GameScenes.LOADING)
				return;

			if (use_animation_group)
				AnimationGroup = part.Modules.OfType<ModuleAnimationGroup>().FirstOrDefault();

			base.OnLoad(node);

			Lib.LogDebug($"Loading with id '{id}'");

			if(string.IsNullOrEmpty(id))
			{
				ReInit();
				enabled = isEnabled = moduleIsEnabled = false;
			}
			else
			{
				ReInit();
				Start();
			}
		}

		public override void OnStart(StartState state)
		{
			Start();
		}

		private void ReInit()
		{
			ModuleDefinition = null;
			enabled = isEnabled = moduleIsEnabled = true;
		}

		private void Start()
		{
			Lib.LogDebug($"Starting id '{id}'");

			if (string.IsNullOrEmpty(id))
			{
				Lib.LogDebug($"Disabling experiment without id on '{part.partInfo.name}'");
				ReInit();
				enabled = isEnabled = moduleIsEnabled = false;
				return;
			}

			// create animators
			deployAnimator = new Animator(part, anim_deploy, anim_deploy_reverse);
			loopAnimator = new Animator(part, anim_loop, anim_loop_reverse);

			// set initial animation states
			SetDragCubes(Running);

			if (Running)
			{
				deployAnimator.Still(1f);
				loopAnimator.Play(false, true);
			}
			else
			{
				deployAnimator.Still(0f);
			}

			if (use_animation_group && AnimationGroup == null)
				AnimationGroup = part.Modules.OfType<ModuleAnimationGroup>().FirstOrDefault();

			if (AnimationGroup != null && !AnimationGroup.isDeployed && Running)
				AnimationGroup.DeployModule();

			Events["ToggleEvent"].guiActiveUncommand = true;
			Events["ToggleEvent"].externalToEVAOnly = true;
			Events["ToggleEvent"].requireFullControl = false;

			Events["ShowPopup"].guiActiveUncommand = true;
			Events["ShowPopup"].externalToEVAOnly = true;
			Events["ShowPopup"].requireFullControl = false;

			ModuleDefinition = ScienceDB.GetExperimentModuleDefinition(id);

			if (ModuleDefinition == null)
			{
				Lib.Log($"No MODULE_DEFINITION found with name `{id}`, is your config broken?", Lib.LogLevel.Error);
				enabled = isEnabled = moduleIsEnabled = false;
				return;
			}

			Actions["StartAction"].guiName = Local.Generic_START + ": " + ModuleDefinition.Info.Title;
			Actions["StopAction"].guiName = Local.Generic_STOP + ": " + ModuleDefinition.Info.Title;

			if (Lib.IsFlight())
			{
				foreach (var hd in part.FindModulesImplementing<HardDrive>())
				{
					if (hd.experiment_id == ExperimentID) privateHdId = part.flightID;
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
			if (!ModuleDefinition.SampleCollecting && ModuleDefinition.Info.SampleMass > 0.0 && remainingSampleMass == 0)
			{
				remainingSampleMass = ModuleDefinition.Info.SampleMass * ModuleDefinition.Samples;
				if (double.IsNaN(remainingSampleMass))
					Lib.Log("remainingSampleMass is NaN on first start " + id + " " + ModuleDefinition.Info.SampleMass + " / " + ModuleDefinition.Samples, Lib.LogLevel.Error);
			}
		}
		#endregion

		#region update methods

		public virtual void Update()
		{
			if (Lib.IsEditor()) // in the editor just update the gui name
			{
				// update ui
				Events["ToggleEvent"].guiName = Lib.StatusToggle(ModuleDefinition.Info.Title, StatusInfo(status, issue));
				return;
			}

			VesselData vd = vessel.KerbalismData();
			if (!vd.IsSimulated) return;

			bool hide = hide_when_invalid
				&& null == ScienceDB.GetSubjectData(ModuleDefinition.Info, GetSituation(vd));

			if (hide)
			{
				Events["ToggleEvent"].active = false;
				Events["ShowPopup"].active = false;
			}
			else
			{
				Events["ToggleEvent"].active = true;
				Events["ShowPopup"].active = true;

				if (subject != null)
				{
					Events["ToggleEvent"].guiName = Lib.StatusToggle(Lib.Ellipsis(ModuleDefinition.Info.Title, Styles.ScaleStringLength(25)), StatusInfo(status, issue));
					Events["ShowPopup"].guiName = Lib.StatusToggle(Local.StatuToggle_info, Lib.BuildString(ScienceValue(Subject), " ", State == RunningState.Forced ? subject.PercentCollectedTotal.ToString("P0") : RunningCountdown(ModuleDefinition.Info, Subject, ModuleDefinition.DataRate)));
				}
				else
				{
					Events["ToggleEvent"].guiName = Lib.StatusToggle(Lib.Ellipsis(ModuleDefinition.Info.Title, Styles.ScaleStringLength(25)), StatusInfo(status, issue));
					Events["ShowPopup"].guiName = Lib.StatusToggle(Local.StatuToggle_info, vd.VesselSituations.FirstSituationTitle);//"info"
				}
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
				subject = ScienceDB.GetSubjectData(ModuleDefinition.Info, situation, out situationId);
				UnityEngine.Profiling.Profiler.EndSample();
				return;
			}

			if (AnimationGroup != null && !AnimationGroup.isDeployed && Running)
			{
				situation = GetSituation(vd);
				subject = ScienceDB.GetSubjectData(ModuleDefinition.Info, situation, out situationId);
				UnityEngine.Profiling.Profiler.EndSample();
				Toggle();
				return;
			}

			shrouded = part.ShieldedFromAirstream;

			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.Experiment.FixedUpdate.RunningUpdate");
			RunningUpdate(
				vessel, vd, GetSituation(vd), this, privateHdId, shrouded,
				ModuleDefinition,
				expState,
				Kerbalism.elapsed_s,
				ref situationId,
				ref remainingSampleMass,
				out subject,
				out issue);
			UnityEngine.Profiling.Profiler.EndSample();

			var newStatus = GetStatus(expState, subject, issue);
			API.OnExperimentStateChanged.Notify(vessel, ExperimentID, status, newStatus);
			status = newStatus;

			UnityEngine.Profiling.Profiler.EndSample();
		}

		// note : we use a non-static method so it can be overriden
		public virtual void BackgroundUpdate(Vessel v, VesselData vd, ProtoPartModuleSnapshot m, double elapsed_s)
		{
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.Experiment.BackgroundUpdate");

			if (!vd.IsSimulated)
			{
				UnityEngine.Profiling.Profiler.EndSample();
				return;
			}

			RunningState expState = Lib.Proto.GetEnum(m, "expState", RunningState.Stopped);
			ExperimentInfo expInfo = ScienceDB.GetExperimentInfo(ExperimentID); // from prefab
			if (expInfo == null)
				return;

			if (!IsRunning(expState))
			{
				int notRunningSituationId = Situation.GetBiomeAgnosticIdForExperiment(GetSituation(vd).Id, expInfo);
				Lib.Proto.Set(m, "situationId", notRunningSituationId);
				UnityEngine.Profiling.Profiler.EndSample();
				return;
			}

			bool shrouded = Lib.Proto.GetBool(m, "shrouded", false);
			int situationId = Lib.Proto.GetInt(m, "situationId", 0);
			double remainingSampleMass = Lib.Proto.GetDouble(m, "remainingSampleMass", 0.0);
			uint privateHdId = Lib.Proto.GetUInt(m, "privateHdId", 0u);
			var oldStatus = Lib.Proto.GetEnum<ExpStatus>(m, "status", ExpStatus.Stopped);

			string issue;
			SubjectData subjectData;

			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.Experiment.BackgroundUpdate.RunningUpdate");
			RunningUpdate(
				v, vd, GetSituation(vd), this, privateHdId, shrouded, // "this" is the prefab
				ModuleDefinition,
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

			API.OnExperimentStateChanged.Notify(v, ExperimentID, oldStatus, newStatus);

			UnityEngine.Profiling.Profiler.EndSample();
		}

		private static void RunningUpdate(
			Vessel v, VesselData vd, Situation vs, ModuleKsmExperiment prefab, uint hdId, bool isShrouded,
			ExperimentModuleDefinition moduleDefinition, RunningState expState, double elapsed_s,
			ref int lastSituationId, ref double remainingSampleMass, out SubjectData subjectData, out string mainIssue)
		{
			mainIssue = string.Empty;

			subjectData = ScienceDB.GetSubjectData(moduleDefinition.Info, vs);

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

			if (moduleDefinition.RequiredEC > 0.0 && vd.ResHandler.ElectricCharge.AvailabilityFactor < 0.1)
			{
				mainIssue = Local.Module_Experiment_issue4;//"no Electricity"
				return;
			}

			if (moduleDefinition.CrewOperate && !moduleDefinition.CrewOperate.Check(v))
			{
				mainIssue = moduleDefinition.CrewOperate.Warning();
				return;
			}

			if (!moduleDefinition.SampleCollecting && remainingSampleMass <= 0.0 && moduleDefinition.Info.SampleMass > 0.0)
			{
				mainIssue = Local.Module_Experiment_issue6;//"depleted"
				return;
			}

			if (!v.loaded && subjectData.Situation.AtmosphericFlight())
			{
				mainIssue = Local.Module_Experiment_issue8;//"background flight"
				return;
			}

			RequireResult[] reqResults;
			if (!moduleDefinition.Requirements.TestRequirements(v, out reqResults))
			{
				mainIssue = Local.Module_Experiment_issue9;//"unmet requirement"
				return;
			}

			if (!HasRequiredResources(v, moduleDefinition.Resources, vd.ResHandler, out mainIssue))
			{
				mainIssue = Local.Module_Experiment_issue10;//"missing resource"
				return;
			}

			double chunkSizeMax = moduleDefinition.DataRate * elapsed_s;

			// Never again generate NaNs
			if (chunkSizeMax <= 0.0)
			{
				mainIssue = "Error : chunkSizeMax is 0.0";
				return;
			}

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

			Drive bufferDrive = null;
			double available;
			if (!moduleDefinition.Info.IsSample)
			{
				available = drive.FileCapacityAvailable();
				if (double.IsNaN(available)) Lib.LogStack("drive.FileCapacityAvailable() returned NaN", Lib.LogLevel.Error);

				if (drive.GetFileSend(subjectData.Id))
				{
					bufferDrive = Cache.TransmitBufferDrive(v);
					available += bufferDrive.FileCapacityAvailable();
					if (double.IsNaN(available)) Lib.LogStack("warpDrive.FileCapacityAvailable() returned NaN", Lib.LogLevel.Error);
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

			chunkSizeMax = Math.Min(chunkSize, available);

			double chunkProdFactor = chunkSizeMax / chunkSize;
			double resourcesProdFactor = 1.0;

			// note : since we can't scale the consume() amount by availability, when one of the resources (including EC)
			// is partially available but not the others, this will cause over-consumption of these other resources
			// Idally we should use a pure input recipe to avoid that but currently, recipes only scale inputs
			// if they have an output, it might be interseting to lift that limitation.
			if (moduleDefinition.RequiredEC > 0.0)
				resourcesProdFactor = Math.Min(resourcesProdFactor, vd.ResHandler.ElectricCharge.AvailabilityFactor);

			foreach (ObjectPair<string, double> res in moduleDefinition.Resources)
				resourcesProdFactor = Math.Min(resourcesProdFactor, ((VesselKSPResource)vd.ResHandler.GetResource(res.Key)).AvailabilityFactor);

			if (resourcesProdFactor == 0.0)
			{
				mainIssue = Local.Module_Experiment_issue10;//"missing resource"
				return;
			}

			chunkSize = chunkSizeMax * resourcesProdFactor;
			double massDelta = chunkSize * moduleDefinition.Info.MassPerMB;

#if DEBUG || DEVBUILD
			if (double.IsNaN(chunkSize))
				Lib.Log("chunkSize is NaN " + moduleDefinition.Info.ExperimentId + " " + chunkSizeMax + " / " + chunkProdFactor + " / " + resourcesProdFactor + " / " + available + " / " + vd.ResHandler.ElectricCharge.Amount + " / " + moduleDefinition.RequiredEC + " / " + moduleDefinition.DataRate, Lib.LogLevel.Error);

			if (double.IsNaN(massDelta))
				Lib.Log("mass delta is NaN " + moduleDefinition.Info.ExperimentId + " " + moduleDefinition.Info.SampleMass + " / " + chunkSize + " / " + moduleDefinition.Info.DataSize, Lib.LogLevel.Error);
#endif

			if (!moduleDefinition.Info.IsSample)
			{
				if (bufferDrive != null)
				{
					double s = Math.Min(chunkSize, bufferDrive.FileCapacityAvailable());
					bufferDrive.Record_file(subjectData, s, true);

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
			// note : Consume() calls should only factor in the drive available space limitation and not the
			// the resource available factor in order to have each resource AvailabilityFactor calculated correctly
			vd.ResHandler.ElectricCharge.Consume(moduleDefinition.RequiredEC * elapsed_s * chunkProdFactor, ResourceBroker.Experiment);
			foreach (ObjectPair<string, double> p in moduleDefinition.Resources)
				vd.ResHandler.Consume(p.Key, p.Value * elapsed_s * chunkProdFactor, ResourceBroker.Experiment);

			if (!moduleDefinition.SampleCollecting)
			{
				remainingSampleMass -= massDelta;
				remainingSampleMass = Math.Max(remainingSampleMass, 0.0);
			}
		}

		public virtual Situation GetSituation(VesselData vd)
		{
			return vd.VesselSituations.GetExperimentSituation(ModuleDefinition.Info);
		}

		private static Drive GetDrive(VesselData vesselData, uint hdId, double chunkSize, SubjectData subjectData)
		{
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.Experiment.GetDrive");
			bool isFile = subjectData.ExpInfo.SampleMass == 0.0;
			Drive drive = null;
			if (hdId != 0)
				drive = vesselData.Parts.Get(hdId).Drive;
			else
				drive = isFile ? Drive.FileDrive(vesselData, chunkSize) : Drive.SampleDrive(vesselData, chunkSize, subjectData);
			UnityEngine.Profiling.Profiler.EndSample();
			return drive;
		}

		private static bool HasRequiredResources(Vessel v, List<ObjectPair<string, double>> defs, VesselResHandler res, out string issue)
		{
			issue = string.Empty;
			if (defs.Count < 1)
				return true;

			// test if there are enough resources on the vessel
			foreach (var p in defs)
			{
				var ri = res.GetResource(p.Key);
				if (ri.Amount == 0.0)
				{
					issue = Local.Module_Experiment_issue12.Format(ri.Name);//"missing " + 
					return false;
				}
			}
			return true;
		}

		public void PlannerUpdate(VesselResHandler resHandler, PlannerVesselData vesselData)
		{
			if (Running) resHandler.ElectricCharge.Consume(ModuleDefinition.RequiredEC, ResourceBroker.Experiment);
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
						PostMultipleRunsMessage(ModuleDefinition.Info.Title, "");
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
			if ((AnimationGroup != null && AnimationGroup.DeployAnimation.isPlaying) || deployAnimator.Playing)
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
				if (loopAnimator.Playing)
					loopAnimator.StopLoop(stop);
				else
					stop();
			}
			else
			{
				// The same experiment must run only once on a vessel
				if (IsExperimentRunningOnVessel(vessel, ExperimentID))
				{
					PostMultipleRunsMessage(ModuleDefinition.Info.Title, vessel.vesselName);
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

		public static RunningState ProtoToggle(Vessel v, ModuleKsmExperiment prefab, ProtoPartModuleSnapshot protoModule, bool setForcedRun = false)
		{
			RunningState expState = Lib.Proto.GetEnum(protoModule, "expState", RunningState.Stopped);

			if (expState == RunningState.Broken)
			{
				ProtoSetState(v, prefab, protoModule, expState);
				return expState;
			}

			if (!IsRunning(expState))
			{
				if (IsExperimentRunningOnVessel(v, prefab.ExperimentID))
				{
					PostMultipleRunsMessage(ScienceDB.GetExperimentInfo(prefab.ExperimentID).Title, v.vesselName);
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

		private static void ProtoSetState(Vessel v, ModuleKsmExperiment prefab, ProtoPartModuleSnapshot protoModule, RunningState expState)
		{
			Lib.Proto.Set(protoModule, "expState", expState);

			var oldStatus = Lib.Proto.GetEnum<ExpStatus>(protoModule, "status", ExpStatus.Stopped);
			var newStatus = GetStatus(expState,
				ScienceDB.GetSubjectData(ScienceDB.GetExperimentInfo(prefab.ExperimentID), prefab.GetSituation(v.KerbalismData())),
				Lib.Proto.GetString(protoModule, "issue")
			);
			Lib.Proto.Set(protoModule, "status", newStatus);

			API.OnExperimentStateChanged.Notify(v, prefab.ExperimentID, oldStatus, newStatus);
		}

		/// <summary> works for loaded and unloaded vessel. very slow method, don't use it every tick </summary>
		public static bool IsExperimentRunningOnVessel(Vessel vessel, string experiment_id)
		{
			if (vessel.loaded)
			{
				foreach (ModuleKsmExperiment e in vessel.FindPartModulesImplementing<ModuleKsmExperiment>())
				{
					if (e.enabled && e.Running && e.ExperimentID == experiment_id)
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

						if (m.moduleName == "ModuleKsmExperiment"
							&& ((ModuleKsmExperiment)module_prefab).ExperimentID == experiment_id
							&& IsRunning(Lib.Proto.GetEnum(m, "expState", RunningState.Stopped)))
							return true;
					}
				}
			}

			return false;
		}

		[KSPEvent(guiActiveUnfocused = true, guiActive = true, guiActiveEditor = true, guiName = "_", active = true, groupName = "Science", groupDisplayName = "#KERBALISM_Group_Science")]//Science
		public void ToggleEvent()
		{
			Toggle();
		}

		[KSPEvent(guiActiveUnfocused = true, guiActive = true, guiName = "_", active = true, groupName = "Science", groupDisplayName = "#KERBALISM_Group_Science")]//Science
		public void ShowPopup()
		{
			new ExperimentPopup(vessel, this, part.flightID, part.partInfo.title);
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
			return Specs(ModuleDefinition);
		}

		public static Specifics Specs(ExperimentModuleDefinition moduleDefinition)
		{
			var specs = new Specifics();
			if (moduleDefinition?.Info == null)
			{
				specs.Add(Local.ExperimentInfo_Unknown);
				return specs;
			}

			if (!string.IsNullOrEmpty(moduleDefinition.Info.Description))
			{
				specs.Add(Lib.BuildString("<i>", moduleDefinition.Info.Description, "</i>"));
				specs.Add(string.Empty);
			}

			double expSize = moduleDefinition.Info.DataSize;
			if (moduleDefinition.Info.SampleMass == 0.0)
			{
				specs.Add(Local.Module_Experiment_Specifics_info1, Lib.HumanReadableDataSize(expSize));//"Data size"
				if(moduleDefinition.DataRate > 0)
				{
					specs.Add(Local.Module_Experiment_Specifics_info2, Lib.HumanReadableDataRate(moduleDefinition.DataRate));
					specs.Add(Local.Module_Experiment_Specifics_info3, Lib.HumanReadableDuration(moduleDefinition.Duration));
				}
			}
			else
			{
				specs.Add(Local.Module_Experiment_Specifics_info4, Lib.HumanReadableSampleSize(expSize));//"Sample size"
				specs.Add(Local.Module_Experiment_Specifics_info5, Lib.HumanReadableMass(moduleDefinition.Info.SampleMass));//"Sample mass"
				if (moduleDefinition.Info.SampleMass > 0.0 && !moduleDefinition.SampleCollecting)
					specs.Add(Local.Module_Experiment_Specifics_info6, moduleDefinition.Samples.ToString("F2"));//"Samples"
				if(moduleDefinition.Duration > 0)
					specs.Add(Local.Module_Experiment_Specifics_info7_sample, Lib.HumanReadableDuration(moduleDefinition.Duration));
			}

			if (moduleDefinition.Info.IncludedExperiments.Count > 0)
			{
				specs.Add(string.Empty);
				specs.Add(Lib.Color("Included experiments:", Lib.Kolor.Cyan, true));
				List<string> includedExpInfos = new List<string>();
				ExperimentInfo.GetIncludedExperimentTitles(moduleDefinition.Info, includedExpInfos);
				foreach (string includedExp in includedExpInfos)
				{
					specs.Add("• " + includedExp);
				}
			}

			List<string> situations = moduleDefinition.Info.AvailableSituations();
			if (situations.Count > 0)
			{
				specs.Add(string.Empty);
				specs.Add(Lib.Color(Local.Module_Experiment_Specifics_Situations, Lib.Kolor.Cyan, true));//"Situations:"
				foreach (string s in situations) specs.Add(Lib.BuildString("• <b>", s, "</b>"));
			}

			if (moduleDefinition.Info.ExpBodyConditions.HasConditions)
			{
				specs.Add(string.Empty);
				specs.Add(moduleDefinition.Info.ExpBodyConditions.ConditionsToString());
			}

			specs.Add(string.Empty);

			specs.Add(Lib.Color(Local.Module_Experiment_Specifics_info8, Lib.Kolor.Cyan, true));//"Needs:"

			if(moduleDefinition.RequiredEC > 0)
				specs.Add(Local.Module_Experiment_Specifics_info9, Lib.HumanReadableRate(moduleDefinition.RequiredEC));
			foreach (var p in moduleDefinition.Resources)
				specs.Add(p.Key, Lib.HumanReadableRate(p.Value));

			if (moduleDefinition.CrewOperate)
			{
				specs.Add(Local.Module_Experiment_Specifics_info11, moduleDefinition.CrewOperate.Info());
			}

			if(moduleDefinition.Requirements.Requires.Length > 0)
			{
				specs.Add(string.Empty);
				specs.Add(Lib.Color(Local.Module_Experiment_Requires, Lib.Kolor.Cyan, true));//"Requires:"
				foreach (RequireDef req in moduleDefinition.Requirements.Requires)
					specs.Add(Lib.BuildString("• <b>", ReqName(req.require), "</b>"), ReqValueFormat(req.require, req.value));
			}

			return specs;
		}

		// part tooltip
		public override string GetInfo()
		{
			if (ModuleDefinition == null)
				return string.Empty;

			return Specs().Info();
		}

		// IModuleInfo
		public string GetModuleTitle() => ModuleDefinition != null ? ModuleDefinition.Info.Title : "";
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
			Message.Post(Lib.Color(Local.Module_Experiment_MultipleRunsMessage_title, Lib.Kolor.Orange, true), Local.Module_Experiment_MultipleRunsMessage.Format(title,vesselName));//"ALREADY RUNNING""Can't start " +  + " a second time on vessel " + 
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
			if (ModuleDefinition.SampleCollecting || this.id != id) return 0;
			double maxSampleMass = ModuleDefinition.Info.SampleMass * ModuleDefinition.Samples;
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
				Lib.Log("Experiment remaining sample mass is NaN " + id, Lib.LogLevel.Error);
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
					deployAnimator = new Animator(part, anim_deploy, anim_deploy_reverse);
				}
				return deployAnimator.IsDefined;
			}
		}

		public string[] GetDragCubeNames() => new string[] { retractedDragCube, deployedDragCube };

		public void AssumeDragCubePosition(string name)
		{
			if (deployAnimator == null)
			{
				deployAnimator = new Animator(part, anim_deploy, anim_deploy_reverse);
			}

			if (name == retractedDragCube)
				deployAnimator.Still(0f);
			else if (name == deployedDragCube)
				deployAnimator.Still(1f);
		}

		public bool UsesProceduralDragCubes() => false;

		#endregion
	}

	internal class EditorTracker
	{
		private static EditorTracker instance;
		private readonly List<ModuleKsmExperiment> experiments = new List<ModuleKsmExperiment>();

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
				foreach (var experiment in part.FindModulesImplementing<ModuleKsmExperiment>())
				{
					if (!experiment.enabled) experiment.State = ModuleKsmExperiment.RunningState.Stopped;
					if (experiment.Running && !AllowStart(experiment))
					{
						// An experiment was added in recording state? Cheeky bugger!
						experiment.State = ModuleKsmExperiment.RunningState.Stopped;
						experiment.deployAnimator.Still(0);
					}
					experiments.Add(experiment);
				}
			}
		}

		internal bool AllowStart(ModuleKsmExperiment experiment)
		{
			foreach (var e in experiments)
				if (e.Running && e.ExperimentID == experiment.ExperimentID)
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
