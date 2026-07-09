using System;
using System.Collections.Generic;
using KSP.Localization;
using UnityEngine;

namespace KERBALISM
{
	public class ProcessControllerSystemHeat : ProcessController, IConfigurable
	{
		[KSPField] public string systemHeatModuleID = "";
		[KSPField] public float shutdownTemperature = 1000f;
		[KSPField] public float systemOutletTemperature = 1000f;
		[KSPField] public float systemPower = 0f;
		[KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "#KERBALISM_FissionReactor_power", groupName = "fissionreactor", groupDisplayName = "#LOC_SystemHeat_ModuleSystemHeatFissionReactor_UIGroup_Title"), UI_FloatRange(scene = UI_Scene.All, minValue = 0f, maxValue = 100f, stepIncrement = 1f)]
		public float CurrentPowerPercent = 100f;
		[KSPField] public float MinimumThrottle = 10f;
		[KSPField] public float meltdownTemperature = 0f;
		[KSPField] public float MaximumTemperature = 2000f;
		[KSPField] public float CoreDamageRate = 0f;
		[KSPField(isPersistant = true)] public float CoreDamage = 0f;
		[KSPField] public FloatCurve coreDamageCurve = new FloatCurve();
		[KSPField(isPersistant = true, guiActive = true, guiName = "#LOC_SystemHeat_ModuleSystemHeatFissionReactor_Field_CurrentSafetyOverride", groupName = "fissionreactor", groupDisplayName = "#LOC_SystemHeat_ModuleSystemHeatFissionReactor_UIGroup_Title"), UI_FloatRange(minValue = 700f, maxValue = 2000f, stepIncrement = 100f)]
		public float CurrentSafetyOverride = 1000f;
		[KSPField] public bool allowManualShutdownTemperatureControl = false;
		[KSPField(isPersistant = false, guiActive = true, guiName = "#LOC_SystemHeat_ModuleSystemHeatFissionReactor_Field_CoreStatus", groupName = "fissionreactor", groupDisplayName = "#LOC_SystemHeat_ModuleSystemHeatFissionReactor_UIGroup_Title")]
		public string CoreStatus = "100.00 %";
		[KSPField] public FloatCurve systemEfficiency = new FloatCurve();
		[KSPField] public bool AutoShutdown = true;
		[KSPField] public bool GeneratesHeat = false;
		[KSPField(isPersistant = true)] public bool deployed;
		[KSPField] public string deployModuleType = "";
		[KSPField] public bool requireDeploy = false;
		[KSPField] public string processID = "";
		[KSPField(isPersistant = false, guiActive = true, guiActiveEditor = true, guiName = "#LOC_SystemHeat_ModuleSystemHeatConverter_Field_Efficiency", groupName = "Process", groupDisplayName = "#KERBALISM_Process_info")]
		public string ConverterOfEfficiency = "";

		private PartModule heatModule;
		private bool requiresDeploy;
		private bool waitingForDeployAnimation;
		private int deployAnimationSettleFrames;

		private double lastAppliedCapacity = -1;
		private double configuredCapacity = -1;
		private float lastUiPowerPercent = -1f;
		private int flightThermalGraceFrames = 0;
		private int fissionLoopRestoreFrames = 0;
		private const int DeployAnimationSettleFrames = 2;

		public string ReactorPowerStatus => IsRunning() ? CurrentPowerPercent.ToString("0.#") + "%" : Local.Generic_STOPPED;

		private double ReactorPowerScale => IsRunning() ? Mathf.Clamp(CurrentPowerPercent, 0f, 100f) / 100.0 : 0.0;

		private bool IsFissionReactor() => resource == "_Nukereactor";

		private float EffectiveShutdownTemperature() => IsFissionReactor() ? CurrentSafetyOverride : shutdownTemperature;

		internal bool RequiresDeployGate() => requiresDeploy;

		internal void OnRunningChanged()
		{
			if (DeployGateActive() && !IsDeployedForUse() && running)
			{
				base.SetRunning(false);
				ClearFlux();
			}

			if (IsRunning() && CurrentPowerPercent <= 0f)
				CurrentPowerPercent = 100f;
			else if (IsRunning())
				CurrentPowerPercent = Mathf.Clamp(CurrentPowerPercent, MinimumThrottle, 100f);

			lastUiPowerPercent = CurrentPowerPercent;
			if (IsFissionReactor() && HighLogic.LoadedSceneIsFlight)
				SyncFissionPowerUiField();

			if (!IsRunning())
				SetEfficiencyPlaceholder();

			if (SystemHeatEditorSimulation.IsEditorScene && heatModule != null)
			{
				lastAppliedCapacity = -1;
				if (IsRunning())
					ApplyThermalCapacityScale(force: true);
				else
					GenerateHeatEditor();
				Lib.RefreshPlanner();
			}
		}

		public override string GetInfo()
		{
			string info = base.GetInfo();
			if (HighLogic.LoadedSceneIsFlight)
				return info;

			if (systemPower == 0f)
				return info;

			float infoShutdown = IsFissionReactor() ? CurrentSafetyOverride : shutdownTemperature;
			string sh = SystemHeat.FormatPartInfoAdd(systemPower, systemOutletTemperature, infoShutdown);

			int pos = info.IndexOf("\n\n");
			return pos < 0 ? info + sh : info.Substring(0, pos) + sh + info.Substring(pos);
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
			if (IsFissionReactor())
			{
				NormalizePersistedFissionPower();

				heatModule = SystemHeat.FindHeatModule(part, systemHeatModuleID);
				if (heatModule != null)
					SystemHeatBackgroundThermal.RestoreLoadedFissionLoopTemperature(part, heatModule);
			}
		}

		private void NormalizePersistedFissionPower()
		{
			if (broken)
				return;

			if (running && CurrentPowerPercent <= 0f)
				CurrentPowerPercent = 100f;
			else if (running)
				CurrentPowerPercent = Mathf.Clamp(CurrentPowerPercent, MinimumThrottle, 100f);

			lastUiPowerPercent = CurrentPowerPercent;
		}

		private void SyncFissionPowerUiField()
		{
			if (!IsFissionReactor())
				return;

			lastUiPowerPercent = CurrentPowerPercent;
			BaseField powerField = Fields[nameof(CurrentPowerPercent)];
			if (powerField != null)
				powerField.SetValue(CurrentPowerPercent, this);
		}

		public new void Start()
		{
			base.Start();

			InitializeDeployState();
			heatModule = SystemHeat.FindHeatModule(part, systemHeatModuleID);
			NormalizePersistedFissionPower();
			SyncFissionPowerUiField();

			if (IsRunning() && CurrentPowerPercent <= 0f)
				CurrentPowerPercent = 100f;

			Fields[nameof(ConverterOfEfficiency)].guiActive = systemPower > 0f;
			Fields[nameof(ConverterOfEfficiency)].guiActiveEditor = systemPower > 0f;
			if (systemPower > 0f)
			{
				Fields[nameof(ConverterOfEfficiency)].guiName = Localizer.Format("#LOC_SystemHeat_ModuleSystemHeatConverter_Field_Efficiency", title);
				SetEfficiencyPlaceholder();
			}

			if (SystemHeatEditorSimulation.IsEditorScene && IsRunning())
				SyncPlannerPseudoResource();

			SetupFissionReactorFields();

			if (IsFissionReactor() && (broken || CoreDamage >= 100f))
				ApplyMeltdownState();
			else if (IsFissionReactor() && HighLogic.LoadedSceneIsFlight)
				RestoreLoadedFissionState();
		}

		private void RestoreLoadedFissionState()
		{
			if (heatModule == null)
				heatModule = SystemHeat.FindHeatModule(part, systemHeatModuleID);
			if (heatModule == null)
				return;

			RestorePersistedFissionPowerFromProto();
			NormalizePersistedFissionPower();
			SyncFissionPowerUiField();

			SystemHeatBackgroundThermal.RestoreLoadedFissionLoopTemperature(part, heatModule);

			if (!IsRunning())
				return;

			flightThermalGraceFrames = 5;
			fissionLoopRestoreFrames = 5;
			lastAppliedCapacity = -1;
			bool restoredPseudo = TryRestorePersistedFissionPseudoFromProto();
			if (restoredPseudo)
				RefreshThermalEfficiencyDisplay();
			else
				ApplyThermalCapacityScale(force: true);
		}

		private bool TryRestorePersistedFissionPseudoFromProto()
		{
			if (part.protoPartSnapshot == null || !part.Resources.Contains(resource))
				return false;

			ProtoPartResourceSnapshot protoRes = part.protoPartSnapshot.resources.Find(k => k.resourceName == resource);
			if (protoRes == null || protoRes.maxAmount <= 0.0)
				return false;

			double amount = protoRes.amount;
			double maxAmount = protoRes.maxAmount;
			if (amount <= 0.0)
				return false;

			Lib.SetResource(part, resource, amount, maxAmount);
			Lib.SetResourceFlow(part, resource, protoRes.flowState);
			lastAppliedCapacity = amount;
			return true;
		}

		private void RestorePersistedFissionPowerFromProto()
		{
			if (part.protoPartSnapshot == null)
				return;

			foreach (ProtoPartModuleSnapshot protoModule in part.protoPartSnapshot.modules)
			{
				if (protoModule.moduleName != moduleName
					|| Lib.Proto.GetString(protoModule, "resource") != resource)
					continue;

				float savedPower = Lib.Proto.GetFloat(protoModule, nameof(CurrentPowerPercent), CurrentPowerPercent);
				if (savedPower >= MinimumThrottle)
					CurrentPowerPercent = savedPower;
				return;
			}
		}

		private bool ShouldDeferFissionThermalShutdown(float loopK)
		{
			if (!IsFissionReactor() || flightThermalGraceFrames <= 0 || heatModule == null || part.protoPartSnapshot == null)
				return false;

			float shutdown = EffectiveShutdownTemperature();
			if (loopK <= shutdown)
				return false;

			ProtoPartModuleSnapshot protoHeat = null;
			foreach (ProtoPartModuleSnapshot protoModule in part.protoPartSnapshot.modules)
			{
				if (protoModule.moduleName != heatModule.moduleName)
					continue;

				string protoModuleId = Lib.Proto.GetString(protoModule, "moduleID");
				string liveModuleId = SystemHeat.GetModuleId(heatModule);
				if (string.IsNullOrEmpty(liveModuleId) || protoModuleId == liveModuleId)
				{
					protoHeat = protoModule;
					break;
				}

				if (protoHeat == null)
					protoHeat = protoModule;
			}

			if (protoHeat == null || !Lib.Proto.GetBool(protoHeat, "backgroundFluxAnchorValid"))
				return false;

			float anchorTemp = Lib.Proto.GetFloat(protoHeat, "backgroundFluxAnchorTemperature");
			return anchorTemp > 0f && anchorTemp <= shutdown;
		}

		private void InitializeDeployState()
		{
			requiresDeploy = requireDeploy && part.FindModuleImplementing<ModuleAnimationGroup>() != null;
			if (!requiresDeploy || Lib.IsEditor())
				deployed = true;
			else
			{
				SyncDeployedFromAnimator();
				if (!deployed && running)
					base.SetRunning(false);
			}
		}

		private void SyncDeployedFromAnimator()
		{
			ModuleAnimationGroup animator = part.FindModuleImplementing<ModuleAnimationGroup>();
			if (animator == null)
				return;

			bool wasDeployed = deployed;
			if (animator.isDeployed)
			{
				AdvanceDeployWait(animator);
				deployed = IsAnimatorReadyForUse(animator);
			}
			else
			{
				waitingForDeployAnimation = false;
				deployAnimationSettleFrames = 0;
				deployed = false;
			}

			if (wasDeployed && !deployed && running)
				base.SetRunning(false);
		}

		internal bool IsDeployedForUse()
		{
			if (!DeployGateActive())
				return true;

			ModuleAnimationGroup animator = part.FindModuleImplementing<ModuleAnimationGroup>();
			return animator == null || IsAnimatorReadyForUse(animator);
		}

		internal void MarkDeployStarted()
		{
			if (!DeployGateActive())
				return;

			waitingForDeployAnimation = true;
			deployAnimationSettleFrames = DeployAnimationSettleFrames;
			deployed = false;
		}

		public override string GetModuleDisplayName()
		{
			if (!string.IsNullOrEmpty(deployModuleType))
				return deployModuleType;
			return base.GetModuleDisplayName();
		}

		private bool DeployGateActive() => requiresDeploy && !Lib.IsEditor();

		private bool IsAnimatorReadyForUse(ModuleAnimationGroup animator)
		{
			if (animator == null)
				return true;

			if (!animator.isDeployed)
				return false;

			if (deployAnimationSettleFrames > 0)
				return false;

			return !waitingForDeployAnimation || !DeployAnimationGate.IsDeployAnimationPlaying(animator);
		}

		private void AdvanceDeployWait(ModuleAnimationGroup animator)
		{
			if (deployAnimationSettleFrames > 0)
				deployAnimationSettleFrames--;

			if (waitingForDeployAnimation
				&& deployAnimationSettleFrames <= 0
				&& !DeployAnimationGate.IsDeployAnimationPlaying(animator))
				waitingForDeployAnimation = false;
		}

		public new void EnableModule()
		{
			if (!DeployGateActive())
				return;

			ModuleAnimationGroup animator = part.FindModuleImplementing<ModuleAnimationGroup>();
			deployed = IsAnimatorReadyForUse(animator);
		}

		public new void DisableModule()
		{
			if (!DeployGateActive())
				return;

			waitingForDeployAnimation = false;
			deployAnimationSettleFrames = 0;
			deployed = false;
			if (running)
				base.SetRunning(false);

			ClearFlux();
		}

		public new bool ModuleIsActive()
		{
			return IsDeployedForUse() && !broken && running;
		}

		public new bool IsSituationValid() => true;

		private void SetupFissionReactorFields()
		{
			if (!IsFissionReactor())
			{
				Fields[nameof(CurrentSafetyOverride)].guiActive = false;
				Fields[nameof(CurrentSafetyOverride)].guiActiveEditor = false;
				Fields[nameof(CurrentPowerPercent)].guiActive = false;
				Fields[nameof(CurrentPowerPercent)].guiActiveEditor = false;
				Fields[nameof(CoreStatus)].guiActive = false;
				return;
			}

			BaseField safetyField = Fields[nameof(CurrentSafetyOverride)];
			safetyField.guiActive = allowManualShutdownTemperatureControl;
			safetyField.guiActiveEditor = allowManualShutdownTemperatureControl;

			UI_FloatRange editorRange = (UI_FloatRange)safetyField.uiControlEditor;
			editorRange.minValue = 700f;
			editorRange.maxValue = MaximumTemperature;

			UI_FloatRange flightRange = (UI_FloatRange)safetyField.uiControlFlight;
			flightRange.minValue = 700f;
			flightRange.maxValue = MaximumTemperature;

			RefreshReactorPowerField();
			lastUiPowerPercent = CurrentPowerPercent;
			UpdateCoreStatus();
		}

		private void RefreshReactorPowerField()
		{
			if (!IsFissionReactor())
				return;

			bool showPower = !broken && CoreDamage < 100f;
			BaseField powerField = Fields[nameof(CurrentPowerPercent)];
			powerField.guiActive = showPower;
			powerField.guiActiveEditor = showPower;

			if (!showPower)
				return;

			powerField.guiName = Localizer.Format("#KERBALISM_FissionReactor_power");

			UI_FloatRange editorPowerRange = (UI_FloatRange)powerField.uiControlEditor;
			editorPowerRange.minValue = MinimumThrottle;
			editorPowerRange.maxValue = 100f;

			UI_FloatRange flightPowerRange = (UI_FloatRange)powerField.uiControlFlight;
			flightPowerRange.minValue = MinimumThrottle;
			flightPowerRange.maxValue = 100f;
		}

		private void SyncReactorPowerFromUi()
		{
			if (!IsFissionReactor() || broken || Mathf.Approximately(CurrentPowerPercent, lastUiPowerPercent))
				return;

			lastUiPowerPercent = CurrentPowerPercent;
			CurrentPowerPercent = Mathf.Clamp(CurrentPowerPercent, MinimumThrottle, 100f);

			if (!IsRunning())
				return;

			lastAppliedCapacity = -1;
			ApplyThermalCapacityScale(force: true);
			if (SystemHeatEditorSimulation.IsEditorScene)
				SyncPlannerPseudoResource();
		}

		private void UpdateCoreStatus()
		{
			if (!IsFissionReactor())
				return;

			if (CoreDamage >= 100f || broken)
			{
				CoreStatus = Localizer.Format("#LOC_SystemHeat_ModuleSystemHeatFissionReactor_Field_CoreStatus_Meltdown");
				return;
			}

			float loopK = heatModule != null ? SystemHeat.CurrentLoopTemperature(heatModule) : 0f;
			float health = SystemHeatEditorSimulation.GetCoreHealthPercent(
				loopK, meltdownTemperature, MaximumTemperature, CoreDamage);
			CoreStatus = string.Format("{0:F2} %", health);
		}

		internal void SyncPlannerPseudoResource()
		{
			if (!Lib.IsEditor())
				return;

			double fullCapacity = capacity * Math.Max(lastMultiplier, 1);
			if (configuredCapacity <= 0)
				configuredCapacity = fullCapacity;

			if (!IsRunning())
				return;

			Configure(true, Math.Max(lastMultiplier, 1));
			double throttledCapacity = fullCapacity * ReactorPowerScale;
			Lib.SetResource(part, resource, throttledCapacity, throttledCapacity);
			Lib.SetResourceFlow(part, resource, true);
			lastAppliedCapacity = throttledCapacity;
		}

		private void SetEfficiencyPlaceholder()
		{
			ConverterOfEfficiency = Localizer.Format("#LOC_SystemHeat_ModuleSystemHeatConverter_Field_Efficiency_Value", "-1");
		}

		public new void Update()
		{
			if (DeployGateActive())
				SyncDeployedFromAnimator();

			if (heatModule == null)
				heatModule = SystemHeat.FindHeatModule(part, systemHeatModuleID);

			if (heatModule != null)
			{
				if (HighLogic.LoadedSceneIsEditor)
				{
					GenerateHeatEditor();
					if (IsRunning())
						ApplyThermalCapacityScale();
					else
						SetEfficiencyPlaceholder();
				}
				else if (HighLogic.LoadedSceneIsFlight && !IsRunning())
					SetEfficiencyPlaceholder();
			}

			SyncReactorPowerFromUi();

			if (!part.IsPAWVisible())
				return;

			if (DeployGateActive())
				Events["Toggle"].guiActive = IsDeployedForUse() && !broken;
			else
				Events["Toggle"].guiActive = !broken;

			Events["Toggle"].guiName = Lib.StatusToggle(lastMultiplier + " " + title,
				broken ? Local.ProcessController_broken
					: running ? Local.ProcessController_running
					: Local.ProcessController_stopped);

			if (Events["DumpValve"].active)
			{
				Events["DumpValve"].guiActive = !DeployGateActive() || IsDeployedForUse();
				ProcessControllerUiHelper.RefreshDumpValveLabel(this);
			}
		}

		public override void Configure(bool enable, int multiplier)
		{
			configuredCapacity = capacity * multiplier;
			base.Configure(enable, multiplier);

			if (heatModule == null)
				heatModule = SystemHeat.FindHeatModule(part, systemHeatModuleID);

			if (!enable)
			{
				SetRunning(false);
				ClearFlux();
			}
			else
				lastAppliedCapacity = -1;
		}

		public void SetReactorPowerPercent(float percent)
		{
			if (percent <= 0f)
			{
				CurrentPowerPercent = 0f;
				SetRunning(false);
			}
			else
			{
				CurrentPowerPercent = Mathf.Clamp(Mathf.Max(percent, MinimumThrottle), 0f, 100f);
				SetRunning(true);
			}

			lastAppliedCapacity = -1;
			lastUiPowerPercent = CurrentPowerPercent;
			if (IsRunning())
				ApplyThermalCapacityScale(force: true);
			else
				SetEfficiencyPlaceholder();
		}

		public new void SetRunning(bool value)
		{
			base.SetRunning(value);
			if (IsFissionReactor())
			{
				ApplyFissionPseudoResourceState(value);
				if (!value)
					SyncFissionPowerUiField();
			}
		}

		private void ApplyFissionPseudoResourceState(bool active)
		{
			if (!part.Resources.Contains(resource))
				return;

			PartResource pseudo = part.Resources[resource];
			if (!active)
				Lib.SetResource(part, resource, 0, pseudo.maxAmount > 0 ? pseudo.maxAmount : Math.Max(capacity, 0.0));
		}

		public void FixedUpdate()
		{
			if (heatModule == null)
				heatModule = SystemHeat.FindHeatModule(part, systemHeatModuleID);

			if (heatModule == null || !HighLogic.LoadedSceneIsFlight)
				return;

			if (IsFissionReactor() && fissionLoopRestoreFrames > 0)
			{
				fissionLoopRestoreFrames--;
				SystemHeatBackgroundThermal.RestoreLoadedFissionLoopTemperature(part, heatModule);
				if (IsRunning())
				{
					lastAppliedCapacity = -1;
					ApplyThermalCapacityScale(force: true);
				}
			}

			if (IsFissionReactor() && flightThermalGraceFrames > 0)
				flightThermalGraceFrames--;

			GenerateHeatFlight();
			UpdateSystemHeatFlight();
			if (IsRunning())
				ApplyThermalCapacityScale();
			else
				SetEfficiencyPlaceholder();
		}

		private void GenerateHeatEditor()
		{
			if (heatModule == null)
				return;

			if (IsRunning())
				SystemHeat.AddFlux(heatModule, resource, systemOutletTemperature, (float)(systemPower * lastMultiplier * ReactorPowerScale), true);
			else
				ClearFlux();
		}

		private void GenerateHeatFlight()
		{
			if (ModuleIsActive())
			{
				float fluxScale = IsRunning() ? (float)ReactorPowerScale : 0f;
				SystemHeat.AddFlux(heatModule, resource, systemOutletTemperature, systemPower * fluxScale * lastMultiplier, true);
			}
			else
				ClearFlux();
		}

		private void UpdateSystemHeatFlight()
		{
			if (broken)
				return;

			float loopK = SystemHeat.CurrentLoopTemperature(heatModule);
			ApplyCoreDamage(loopK, TimeWarp.fixedDeltaTime);
			UpdateCoreStatus();
			if (broken)
				return;

			if (AutoShutdown && IsRunning() && loopK > EffectiveShutdownTemperature() && !ShouldDeferFissionThermalShutdown(loopK))
			{
				ScreenMessages.PostScreenMessage(new ScreenMessage(
					IsFissionReactor()
						? Localizer.Format("#LOC_SystemHeat_ModuleSystemHeatFissionReactor_Message_EmergencyShutdown",
							EffectiveShutdownTemperature().ToString("F0"), part.partInfo.title)
						: Localizer.Format("#LOC_SystemHeat_ModuleSystemHeatConverter_Message_Shutdown", part.partInfo.title),
					IsFissionReactor() ? 5.0f : 3.0f,
					ScreenMessageStyle.UPPER_CENTER));
				SetRunning(false);
			}
		}

		private void RefreshThermalEfficiencyDisplay()
		{
			if (heatModule == null || systemPower <= 0f)
				return;

			float loopK = SystemHeat.CurrentLoopTemperature(heatModule);
			double thermalEff = SystemHeatEditorSimulation.CalculateProcessEfficiency(systemEfficiency, loopK, systemPower, false);
			double displayEff = IsFissionReactor() ? Math.Min(thermalEff, 1.0) : thermalEff;
			ConverterOfEfficiency = Localizer.Format("#LOC_SystemHeat_ModuleSystemHeatConverter_Field_Efficiency_Value", (displayEff * 100f).ToString("F1"));
		}

		private void ApplyThermalCapacityScale(bool force = false)
		{
			if (!(HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor) || heatModule == null)
			{
				lastAppliedCapacity = -1;
				return;
			}

			if (!IsRunning())
			{
				lastAppliedCapacity = -1;
				SetEfficiencyPlaceholder();
				return;
			}

			if (configuredCapacity <= 0)
			{
				PartResource pr = part.Resources[resource];
				configuredCapacity = (pr != null && pr.maxAmount > 0) ? pr.maxAmount : Math.Max(capacity, 0.0);
			}

			float loopK = SystemHeat.CurrentLoopTemperature(heatModule);
			if (AutoShutdown && !SystemHeatEditorSimulation.IsEditorScene && loopK > EffectiveShutdownTemperature()
				&& !ShouldDeferFissionThermalShutdown(loopK))
			{
				if (running)
				{
					lastAppliedCapacity = -1;
					SetRunning(false);
				}
				return;
			}

			double thermalEff = SystemHeatEditorSimulation.CalculateProcessEfficiency(systemEfficiency, loopK, systemPower, SystemHeatEditorSimulation.IsEditorScene);
			double displayEff = IsFissionReactor() ? Math.Min(thermalEff, 1.0) : thermalEff;

			ConverterOfEfficiency = Localizer.Format("#LOC_SystemHeat_ModuleSystemHeatConverter_Field_Efficiency_Value", (displayEff * 100f).ToString("F1"));

			double desiredCapacity = SystemHeatEditorSimulation.IsEditorScene
				? configuredCapacity * ReactorPowerScale
				: configuredCapacity * thermalEff * ReactorPowerScale;

			if (!force && Math.Abs(desiredCapacity - lastAppliedCapacity) <= (configuredCapacity * SystemHeatEditorSimulation.HystFrac))
				return;

			Lib.SetResource(part, resource, desiredCapacity, desiredCapacity);
			Lib.RefreshPlanner();

			lastAppliedCapacity = desiredCapacity;
		}

		private void ApplyCoreDamage(float loopTemperature, float elapsedSeconds)
		{
			if (!IsFissionReactor() || meltdownTemperature <= 0f || elapsedSeconds <= 0f)
				return;

			CoreDamage = SystemHeatEditorSimulation.AccumulateCoreDamage(
				loopTemperature, meltdownTemperature, MaximumTemperature, CoreDamage,
				CoreDamageRate, coreDamageCurve, elapsedSeconds);
			if (CoreDamage < 100f)
				return;

			BreakForMeltdown();
		}

		private void BreakForMeltdown()
		{
			ApplyMeltdownState();
			ScreenMessages.PostScreenMessage(new ScreenMessage(
				Localizer.Format("#LOC_SystemHeat_ModuleSystemHeatFissionReactor_Field_ReactorOutput_Meltdown") + " ??" + part.partInfo.title,
				5.0f,
				ScreenMessageStyle.UPPER_CENTER));
		}

		private void ApplyMeltdownState()
		{
			CoreDamage = 100f;
			SetRunning(false);
			running = false;
			ReliablityEvent(true);
			broken = true;
			CurrentPowerPercent = 0f;
			isEnabled = false;
			enabled = false;
			UpdateCoreStatus();
			RefreshReactorPowerField();

			ClearFlux();

			foreach (Reliability reliability in part.FindModulesImplementing<Reliability>())
			{
				if (!MatchesProcessReliability(reliability))
					continue;

				reliability.broken = true;
				reliability.critical = true;
			}
		}

		private bool MatchesProcessReliability(Reliability reliability)
		{
			return reliability.type == moduleName
				|| reliability.type == nameof(ProcessController)
				|| reliability.type == "ProcessController";
		}

		private void ClearFlux()
		{
			SystemHeat.AddFlux(heatModule, resource, 0f, 0f, false);
		}

		public static string BackgroundUpdate(
			Vessel v,
			ProtoPartSnapshot partSnapshot,
			ProtoPartModuleSnapshot moduleSnapshot,
			PartModule protoPartModule,
			Part protoPart,
			Dictionary<string, double> availableResources,
			List<KeyValuePair<string, double>> resourceChangeRequest,
			double elapsed_s)
		{
			if (Lib.Proto.GetString(moduleSnapshot, "resource") == "_Nukereactor")
				SystemHeatBackgroundThermal.SyncFrozenProcessReactor(v, partSnapshot, moduleSnapshot, protoPartModule, protoPart, elapsed_s);
			else
				SystemHeatBackgroundThermal.TryRun(v, elapsed_s);
			return Localizer.Format("#LOC_SystemHeat_ModuleSystemHeatConverter_DisplayName");
		}
	}
}
