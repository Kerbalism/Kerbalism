using System;
using System.Collections.Generic;
using Experience;
using System.Linq;
using UnityEngine;
using KSP.Localization;
using System.Collections;
using static KERBALISM.ExperimentRequirements;
using static KERBALISM.ExperimentData;
using KERBALISM.Planner;

namespace KERBALISM
{


	public class ModuleKsmExperiment : KsmPartModule<ModuleKsmExperiment, ExperimentData>, ISpecifics, IModuleInfo, IPartMassModifier, IMultipleDragCube, IPlannerModule, IBackgroundModule, ISwitchable
	{
		#region FIELDS

		/// <summary> name of the associated experiment module definition </summary>
		[KSPField] public string id = string.Empty;

		/// <summary> don't show UI when the experiment is unavailable </summary>
		[KSPField] public bool hide_when_invalid = false;

		/// <summary> if true, the experiment can run when shrouded (in bay or fairing) </summary>
		[KSPField] public bool allow_shrouded = true;

		// animations definition
		[KSPField] public string anim_deploy = string.Empty; // deploy animation
		[KSPField] public bool anim_deploy_reverse = false;

		[KSPField] public string anim_loop = string.Empty; // loop animation
		[KSPField] public bool anim_loop_reverse = false;

		/// <summary>
		/// if true, deploy/retract animations will managed by the first (by index) found ModuleAnimationGroup
		/// Note that using an animation group is incompatible with using a loop animation
		/// </summary>
		[KSPField] public bool use_animation_group = false;

		// optional : custom drag cubes definitions
		[KSPField] public string retractedDragCube = "Retracted";
		[KSPField] public string deployedDragCube = "Deployed";

		// animation handlers
		private Animator deployAnimator;
		private Animator loopAnimator;
		private ModuleAnimationGroup animationGroup;

		#endregion

		#region LIFECYCLE

		public override void OnLoad(ConfigNode node)
		{
			if (HighLogic.LoadedScene == GameScenes.LOADING)
			{
				ExperimentData prefabData = new ExperimentData();
				prefabData.SetPartModuleReferences(this, this);
				prefabData.OnFirstInstantiate(null, null);
				moduleData = prefabData;
			}

			if (use_animation_group)
				animationGroup = part.Modules.OfType<ModuleAnimationGroup>().FirstOrDefault();
		}

		public void OnSwitchActivate()
		{
			Lib.LogDebug($"B9PS : activating {moduleName} with id '{id}'");

			if (moduleData.SetupDefinition(id))
			{
				enabled = isEnabled = moduleIsEnabled = true;
				moduleData.moduleIsEnabled = true;
				Setup();
			}
			else
			{
				OnSwitchDeactivate();
			}
		}

		public void OnSwitchDeactivate()
		{
			Lib.LogDebug($"B9PS : deactivating {moduleName}");
			enabled = isEnabled = moduleIsEnabled = false;
			moduleData.moduleIsEnabled = false;
		}

		public override void OnStart(StartState state)
		{
			Lib.LogDebug($"Starting id '{id}'");

			// create animators
			deployAnimator = new Animator(part, anim_deploy, anim_deploy_reverse);
			loopAnimator = new Animator(part, anim_loop, anim_loop_reverse);

			// set initial animation states
			if (moduleData.IsRunningRequested)
			{
				deployAnimator.Still(1f);
				loopAnimator.Play(false, true);
				SetDragCubes(true);
			}
			else
			{
				deployAnimator.Still(0f);
				SetDragCubes(false);
			}

			if (use_animation_group && animationGroup == null)
				animationGroup = part.Modules.OfType<ModuleAnimationGroup>().FirstOrDefault();

			if (animationGroup != null && !animationGroup.isDeployed && moduleData.IsRunningRequested)
			{
				animationGroup.DeployModule();
			}

			Events["ToggleEvent"].guiActiveUncommand = true;
			Events["ToggleEvent"].externalToEVAOnly = true;
			Events["ToggleEvent"].requireFullControl = false;

			Events["ShowPopup"].guiActiveUncommand = true;
			Events["ShowPopup"].externalToEVAOnly = true;
			Events["ShowPopup"].requireFullControl = false;

			if (moduleData.moduleIsEnabled)
			{
				moduleData.CheckPrivateDriveId();
				Setup();
			}
		}

		private void Setup()
		{
			Lib.LogDebug($"Setup with id '{id}'");

			Actions["StartAction"].guiName = Lib.BuildString(Local.Generic_START, ": ", moduleData.ModuleDefinition.Info.Title);
			Actions["StopAction"].guiName = Lib.BuildString(Local.Generic_STOP, ": ", moduleData.ModuleDefinition.Info.Title);
		}

		#endregion

		#region EVALUATION

		public virtual void Update()
		{
			if (Lib.IsEditor || vessel == null) // in the editor just update the gui name
			{
				// update ui
				Events["ToggleEvent"].guiName = Lib.StatusToggle(moduleData.ExperimentTitle, StatusInfo(moduleData.Status, moduleData.issue));
				return;
			}

			if (!vessel.TryGetVesselData(out VesselData vd) || !vd.IsSimulated)
				return;

			bool hide = hide_when_invalid && moduleData.Subject == null;

			if (hide)
			{
				Events["ToggleEvent"].active = false;
				Events["ShowPopup"].active = false;
			}
			else
			{
				Events["ToggleEvent"].active = true;
				Events["ShowPopup"].active = true;

				Events["ToggleEvent"].guiName = Lib.StatusToggle(Lib.Ellipsis(moduleData.ExperimentTitle, 25), StatusInfo(moduleData.Status, moduleData.issue));

				if (moduleData.Subject != null)
				{
					Events["ShowPopup"].guiName = Lib.StatusToggle(Local.StatuToggle_info,
						Lib.BuildString(
							ScienceValue(moduleData.Subject),
							" ",
							moduleData.State == RunningState.Forced
							? moduleData.Subject.PercentCollectedTotal.ToString("P0")
							: RunningCountdown(moduleData.ModuleDefinition.Info, moduleData.Subject, moduleData.ModuleDefinition.DataRate)));
				}
				else
				{
					Events["ShowPopup"].guiName = Lib.StatusToggle(Local.StatuToggle_info, vd.VesselSituations.FirstSituationTitle);//"info"
				}
			}

			if (animationGroup != null && !animationGroup.isDeployed && moduleData.IsRunningRequested)
			{
				Toggle(moduleData);
			}
		}

		public virtual void FixedUpdate()
		{
			// basic sanity checks
			if (Lib.IsEditor)
				return;

			moduleData.shrouded = part.ShieldedFromAirstream;

			if (!moduleData.IsRunningRequested || !vessel.TryGetVesselData(out VesselData vd) || !vd.IsSimulated)
				return;

			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.Experiment.FixedUpdate.RunningUpdate");
			RunningUpdate(vessel, vd, moduleData, this, Kerbalism.elapsed_s);
			moduleData.UpdateAfterExperimentUpdate();
			UnityEngine.Profiling.Profiler.EndSample();
		}

		public void BackgroundUpdate(VesselData vd, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule, double elapsed_s)
		{
			if (!ModuleData.TryGetModuleData<ModuleKsmExperiment, ExperimentData>(protoModule, out ExperimentData experimentData))
				return;

			if (!experimentData.IsRunningRequested)
				return;

			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.Experiment.BackgroundUpdate.RunningUpdate");
			RunningUpdate(vd.Vessel, vd, experimentData, this, elapsed_s);
			experimentData.UpdateAfterExperimentUpdate();
			UnityEngine.Profiling.Profiler.EndSample();
		}

		private static void RunningUpdate(Vessel v, VesselData vd, ExperimentData ed, ModuleKsmExperiment prefab, double elapsed_s)
		{
			ed.issue = string.Empty;

			if (ed.Subject == null)
			{
				ed.issue = Local.Module_Experiment_issue1;//"invalid situation"
				return;
			}

			double scienceRemaining = ed.Subject.ScienceRemainingToCollect;

			if (ed.State != RunningState.Forced && scienceRemaining <= 0.0)
				return;

			if (ed.shrouded && !prefab.allow_shrouded)
			{
				ed.issue = Local.Module_Experiment_issue2;//"shrouded"
				return;
			}

			// note : since we can't scale the consume() amount by availability, when one of the resources (including EC)
			// is partially available but not the others, this will cause over-consumption of these other resources
			// Idally we should use a pure input recipe to avoid that but currently, recipes only scale inputs
			// if they have an output, it might be interresting to lift that limitation.
			double resourcesProdFactor = 1.0;

			if (ed.ModuleDefinition.RequiredEC > 0.0)
			{
				if (vd.ResHandler.ElectricCharge.AvailabilityFactor == 0.0)
				{
					ed.issue = Local.Module_Experiment_issue4;//"no Electricity"
					return;
				}
				else
				{
					resourcesProdFactor = Math.Min(resourcesProdFactor, vd.ResHandler.ElectricCharge.AvailabilityFactor);
				}
			}

			if (ed.ModuleDefinition.Resources.Count > 0)
			{
				// test if there are enough resources on the vessel
				foreach (var p in ed.ModuleDefinition.Resources)
				{
					VesselResource vr = vd.ResHandler.GetResource(p.Key);
					if (vr.AvailabilityFactor == 0.0)
					{
						ed.issue = Local.Module_Experiment_issue12.Format(vr.Title); //"missing <<1>>"
						return;
					}
					else
					{
						resourcesProdFactor = Math.Min(resourcesProdFactor, vr.AvailabilityFactor);
					}
				}
			}

			if (ed.ModuleDefinition.CrewOperate && !ed.ModuleDefinition.CrewOperate.Check(v))
			{
				ed.issue = ed.ModuleDefinition.CrewOperate.Warning();
				return;
			}

			ExperimentInfo expInfo = ed.ModuleDefinition.Info;

			if (!ed.ModuleDefinition.SampleCollecting && ed.remainingSampleMass <= 0.0 && expInfo.SampleMass > 0.0)
			{
				ed.issue = Local.Module_Experiment_issue6;//"depleted"
				return;
			}

			if (!v.loaded && ed.Subject.Situation.AtmosphericFlight())
			{
				ed.issue = Local.Module_Experiment_issue8;//"background flight"
				return;
			}

			if (!ed.ModuleDefinition.Requirements.TestRequirements(vd, out RequireResult[] reqResults))
			{
				ed.issue = Local.Module_Experiment_issue9;//"unmet requirement"
				return;
			}

			double chunkSizeMax = ed.ModuleDefinition.DataRate * elapsed_s;

			// Never again generate NaNs
			if (chunkSizeMax <= 0.0)
			{
				ed.issue = "Error : chunkSizeMax is 0.0";
				return;
			}

			double chunkSize;
			if (ed.State != RunningState.Forced)
				chunkSize = Math.Min(chunkSizeMax, scienceRemaining / ed.Subject.SciencePerMB);
			else
				chunkSize = chunkSizeMax;

			bool isSample = expInfo.IsSample;
			DriveData drive;
			if (ed.PrivateDriveFlightId == 0 || !ModuleData.TryGetModuleData<ModuleKsmDrive, DriveData>(ed.PrivateDriveFlightId, out drive))
			{
				if (isSample)
					drive = DriveData.SampleDrive(vd, chunkSize, ed.Subject);
				else
					drive = DriveData.FileDrive(vd, chunkSize);
			}

			if (drive == null)
			{
				ed.issue = Local.Module_Experiment_issue11;//"no storage space"
				return;
			}

			DriveData bufferDrive = null;
			double available;
			if (isSample)
			{
				available = drive.SampleCapacityAvailable(ed.Subject);
			}
			else
			{
				available = drive.FileCapacityAvailable();
				if (double.IsNaN(available)) Lib.LogStack("drive.FileCapacityAvailable() returned NaN", Lib.LogLevel.Error);

				if (drive.GetFileSend(ed.Subject.Id))
				{
					bufferDrive = Cache.TransmitBufferDrive(v);
					available += bufferDrive.FileCapacityAvailable();
					if (double.IsNaN(available)) Lib.LogStack("warpDrive.FileCapacityAvailable() returned NaN", Lib.LogLevel.Error);
				}
			}

			if (available <= 0.0)
			{
				ed.issue = Local.Module_Experiment_issue11;//"no storage space"
				return;
			}

			chunkSizeMax = Math.Min(chunkSize, available);

			double chunkProdFactor = chunkSizeMax / chunkSize;

			chunkSize = chunkSizeMax * resourcesProdFactor;

			double massDelta = chunkSize * expInfo.MassPerMB;

#if DEBUG || DEVBUILD
			if (double.IsNaN(chunkSize))
				Lib.Log("chunkSize is NaN " + expInfo.ExperimentId + " " + chunkSizeMax + " / " + chunkProdFactor + " / " + resourcesProdFactor + " / " + available + " / " + vd.ResHandler.ElectricCharge.Amount + " / " + ed.ModuleDefinition.RequiredEC + " / " + ed.ModuleDefinition.DataRate, Lib.LogLevel.Error);

			if (double.IsNaN(massDelta))
				Lib.Log("mass delta is NaN " + expInfo.ExperimentId + " " + expInfo.SampleMass + " / " + chunkSize + " / " + expInfo.DataSize, Lib.LogLevel.Error);
#endif

			if (isSample)
			{
				drive.RecordSample(ed.Subject, chunkSize, massDelta);
			}
			else
			{
				if (bufferDrive != null)
				{
					double s = Math.Min(chunkSize, bufferDrive.FileCapacityAvailable());
					bufferDrive.RecordFile(ed.Subject, s, true);

					if (chunkSize > s) // only write to persisted drive if the data cannot be transmitted in this tick
						drive.RecordFile(ed.Subject, chunkSize - s, true);
					else if (!drive.files.ContainsKey(ed.Subject)) // if everything is transmitted, create an empty file so the player know what is happening
						drive.RecordFile(ed.Subject, 0.0, true);
				}
				else
				{
					drive.RecordFile(ed.Subject, chunkSize, true);
				}
			}

			// Consume EC and resources
			// note : Consume() calls only factor in the drive available space limitation and not the resource availability factor, this is intended
			// note 2 : Since drive available space is determined by the transmit buffer drive space, itself determined by EC availability,
			// we don't totally escape a feeback effect
			vd.ResHandler.ElectricCharge.Consume(ed.ModuleDefinition.RequiredEC * elapsed_s * chunkProdFactor, ResourceBroker.Experiment);

			foreach (ObjectPair<string, double> p in ed.ModuleDefinition.Resources)
				vd.ResHandler.Consume(p.Key, p.Value * elapsed_s * chunkProdFactor, ResourceBroker.Experiment);

			if (!ed.ModuleDefinition.SampleCollecting)
			{
				ed.remainingSampleMass = Math.Max(ed.remainingSampleMass - massDelta, 0.0);
			}
		}


		public void PlannerUpdate(VesselResHandler resHandler, VesselDataShip vesselData)
		{
			if (moduleData.IsExperimentRunning)
				resHandler.ElectricCharge.Consume(moduleData.ModuleDefinition.RequiredEC, ResourceBroker.Experiment);
		}

		#endregion

		#region user interaction

		[KSPEvent(guiActiveUnfocused = true, guiActive = true, guiActiveEditor = true, guiName = "_", active = true, groupName = "Science", groupDisplayName = "#KERBALISM_Group_Science")]//Science
		public void ToggleEvent()
		{
			Toggle(moduleData);
		}

		[KSPEvent(guiActiveUnfocused = true, guiActive = true, guiName = "_", active = true, groupName = "Science", groupDisplayName = "#KERBALISM_Group_Science")]//Science
		public void ShowPopup()
		{
			new ExperimentPopup(moduleData);
		}

		// action groups
		[KSPAction("Start")]
		public void StartAction(KSPActionParam param)
		{
			if (!moduleData.IsRunningRequested) Toggle(moduleData);
		}

		[KSPAction("Stop")]
		public void StopAction(KSPActionParam param)
		{
			if (moduleData.IsRunningRequested) Toggle(moduleData);
		}

		public static void Toggle(ExperimentData ed, bool setForcedRun = false)
		{
			if (ed.IsBroken || !ed.moduleIsEnabled)
				return;

			// if setting forced run on an already running experiment
			if (setForcedRun && ed.State == RunningState.Running)
			{
				ed.State = RunningState.Forced;
				return;
			}

			// abort if the experiment animation is already playing
			if (ed.loadedModule != null)
			{
				if ((ed.loadedModule.animationGroup != null && ed.loadedModule.animationGroup.DeployAnimation.isPlaying)
					|| ed.loadedModule.deployAnimator.Playing
					|| ed.loadedModule.loopAnimator.IsLoopStopping)
					return;
			}

			// stopping
			if (ed.IsRunningRequested)
			{
				// if vessel is unloaded
				if (ed.loadedModule == null)
				{
					ed.State = RunningState.Stopped;
					return;
				}
				// if vessel loaded or in the editor
				else
				{
					// stop experiment
					// plays the deploy animation in reverse
					// if an external deploy animation module is used, we don't retract automatically
					Action onLoopStop = delegate ()
					{
						ed.State = RunningState.Stopped;
						ed.loadedModule.deployAnimator.Play(true, false, null, Lib.IsEditor ? 5f : 1f);
						ed.loadedModule.SetDragCubes(false);
						if (Lib.IsEditor)
							Planner.Planner.RefreshPlanner();
					};

					// wait for loop animation to stop before deploy animation
					if (ed.loadedModule.loopAnimator.Playing)
						ed.loadedModule.loopAnimator.StopLoop(onLoopStop);
					else
						onLoopStop();
				}
			}
			// starting
			else
			{
				CheckMultipleRun(ed);

				// if vessel is unloaded
				if (ed.loadedModule == null)
				{
					ed.State = setForcedRun ? RunningState.Forced : RunningState.Running;
					return;
				}
				// if vessel loaded or in the editor
				else
				{
					// in case of an animation group, we start the experiment immediatly
					if (ed.loadedModule.animationGroup != null)
					{
						if (!ed.loadedModule.animationGroup.isDeployed)
						{
							ed.loadedModule.animationGroup.DeployModule();
							ed.State = setForcedRun ? RunningState.Forced : RunningState.Running;
							if (Lib.IsEditor)
								Planner.Planner.RefreshPlanner();
						}
					}
					// if using our own animation handler, when the animation is done playing,
					// set the experiment running state and start the loop animation
					else
					{
						Action onDeploy = delegate ()
						{
							ed.State = setForcedRun ? RunningState.Forced : RunningState.Running;
							ed.loadedModule.loopAnimator.Play(false, true);
							ed.loadedModule.SetDragCubes(true);
							if (Lib.IsEditor)
								Planner.Planner.RefreshPlanner();
						};

						ed.loadedModule.deployAnimator.Play(false, false, onDeploy, Lib.IsEditor ? 5f : 1f);
					}
				}
			}
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
			return Specs(moduleData.ModuleDefinition);
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
			if (moduleData?.ModuleDefinition == null)
				return string.Empty;

			return Specs().Info();
		}

		// IModuleInfo
		public string GetModuleTitle() => moduleData?.ModuleDefinition != null ? moduleData.ModuleDefinition.Info.Title : "";
		public override string GetModuleDisplayName() { return GetModuleTitle(); }
		public string GetPrimaryField() { return string.Empty; }
		public Callback<Rect> GetDrawModulePanelCallback() { return null; }

		#endregion

		#region utility / other

		public void ReliablityEvent(bool breakdown)
		{
			if (breakdown)
				moduleData.State = RunningState.Broken;
			else
				moduleData.State = RunningState.Stopped;
		}

		/// <summary>
		/// Check if the same same experiment is already running on the vessel, and disable it if toggleOther is true
		/// 
		/// </summary>
		public static bool CheckMultipleRun(ExperimentData thisExpData, bool toggleOther = true)
		{
			VesselDataBase vd = thisExpData.partData.vesselData;
			bool hasOtherRunning = false;

			foreach (PartData partData in vd.PartList)
			{
				for (int i = 0; i < partData.modules.Count; i++)
				{
					if (partData.modules[i] is ExperimentData expData
						&& expData.moduleIsEnabled
						&& expData.ExperimentID == thisExpData.ExperimentID
						&& expData.IsRunningRequested)
					{
						if (toggleOther)
						{
							Toggle(expData);

							Message.Post(
								Lib.Color(Local.Module_Experiment_MultipleRunsMessage_title, Lib.Kolor.Orange, true),
								string.Format("{0} was already running on vessel {1}\nThe module on {2} has been disabled",
								expData.ExperimentTitle, expData.VesselData.VesselName, partData.Title));
						}
						hasOtherRunning |= true;
					}
				}
			}

			return hasOtherRunning;
		}

		#endregion

		#region sample mass

		// IPartMassModifier
		public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
		{
			if (moduleData == null)
				return 0f;

			if (double.IsNaN(moduleData.remainingSampleMass))
			{
				Lib.LogDebug("Experiment remaining sample mass is NaN " + id, Lib.LogLevel.Error);
				return 0f;
			}
			return (float)moduleData.remainingSampleMass;
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

		#region EDITOR MULTIPLE RUN TRACKER

		private static List<string> editorRunningExperiments = new List<string>();

		public static void CheckEditorExperimentMultipleRun()
		{
			foreach (PartData partData in VesselDataShip.LoadedParts)
			{
				for (int i = 0; i < partData.modules.Count; i++)
				{
					if (partData.modules[i] is ExperimentData expData && expData.moduleIsEnabled && expData.IsRunningRequested)
					{
						if (editorRunningExperiments.Contains(expData.ExperimentID))
						{
							Toggle(expData);
						}
						else
						{
							editorRunningExperiments.Add(expData.ExperimentID);
						}
					}
				}
			}

			editorRunningExperiments.Clear();
		}
		#endregion
	}


} // KERBALISM
