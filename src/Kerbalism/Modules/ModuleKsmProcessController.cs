using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{
	/// <summary>
	/// Replacement for ProcessController
	/// </summary>
	public class ModuleKsmProcessController : KsmPartModule<ModuleKsmProcessController, ProcessControllerData>, IModuleInfo, IAnimatedModule, ISpecifics, ISwitchable
	{
		[KSPField] public string processName = string.Empty;
		[KSPField] public double capacity = 1.0;
		[KSPField] public string id = string.Empty;       // this is only for identifying the module with B9PS on parts that have multiple process controllers for the same process
		[KSPField] public string uiGroup = null;          // display name of the UI group

		[KSPField]
		[UI_Toggle(scene = UI_Scene.All, affectSymCounterparts = UI_Scene.None)]
		public bool running;

		// internal state
		public bool broken = false;
		private BaseField runningField;

		// parsing configs at prefab compilation
		public override void OnLoad(ConfigNode node)
		{
			if (HighLogic.LoadedScene == GameScenes.LOADING)
			{
				ProcessControllerData prefabData = new ProcessControllerData();
				prefabData.OnInstantiate(this, null, null);
				moduleData = prefabData;
			}
		}

		public void OnSwitchActivate()
		{
			Lib.LogDebug($"B9PS : activating {moduleName} with id '{id}'");
			Setup();
		}

		public void OnSwitchDeactivate()
		{
			Lib.LogDebug($"B9PS : deactivating {moduleName}");
			enabled = isEnabled = moduleIsEnabled = false;
			moduleData.moduleIsEnabled = false;
		}

		public override void OnStart(StartState state)
		{
			runningField = Fields["running"];
			runningField.OnValueModified += (field) => Toggle(moduleData, true);

			((UI_Toggle)runningField.uiControlFlight).enabledText = Lib.Color(Local.Generic_ENABLED.ToLower(), Lib.Kolor.Green);
			((UI_Toggle)runningField.uiControlFlight).disabledText = Lib.Color(Local.Generic_DISABLED.ToLower(), Lib.Kolor.Yellow);
			((UI_Toggle)runningField.uiControlEditor).enabledText = Lib.Color(Local.Generic_ENABLED.ToLower(), Lib.Kolor.Green);
			((UI_Toggle)runningField.uiControlEditor).disabledText = Lib.Color(Local.Generic_DISABLED.ToLower(), Lib.Kolor.Yellow);

			Setup();
		}

		/// <summary>  start the module. must be idempotent: expect to be called several times </summary>
		private void Setup()
		{
			Lib.LogDebug($"ProcessController on {part.name} starting with process '{processName}'");

			if (string.IsNullOrEmpty(processName))
			{
				Lib.LogDebug($"No process, disabling module");
				enabled = isEnabled = moduleIsEnabled = false;
				moduleData.moduleIsEnabled = false;
				return;
			}

			enabled = isEnabled = moduleIsEnabled = true;
			moduleData.moduleIsEnabled = true;

			// make sure part data is valid (if we have it), we might be restarting with a different configuration
			if (moduleData.processName != processName || moduleData.processCapacity != capacity)
			{
				Lib.LogDebug($"Restarting with different process '{processName}' (was '{moduleData.processName}'), discarding old part data");
				moduleData.OnInstantiate(this, null, null);
			}

			// PAW setup
			running = moduleData.Process != null && moduleData.isRunning;
			runningField.guiActive = runningField.guiActiveEditor = moduleData.Process != null && moduleData.Process.canToggle;
			runningField.guiName = moduleData.Process.title;

			if (uiGroup != null)
				runningField.group = new BasePAWGroup(uiGroup, uiGroup, false);
		}

		public static void Toggle(ProcessControllerData processData, bool isLoaded)
		{
			if (processData.isBroken)
				return;

			processData.isRunning = !processData.isRunning;
	
			if (isLoaded)
			{
				processData.loadedModule.running = processData.isRunning;

				// refresh VAB/SPH ui
				if (Lib.IsEditor)
					GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
			}
		}

		// part tooltip
		public override string GetInfo()
		{
			if (string.IsNullOrEmpty(processName))
				return string.Empty;

			return Specs().Info(moduleData.Process.desc);
		}

		public bool IsRunning() {
			return running && !string.IsNullOrEmpty(processName);
		}

		// specifics support
		public Specifics Specs()
		{
			Process process = moduleData.Process; //Profile.processes.Find(k => k.name == processName);
			if (process == null)
				return new Specifics();
			return process.Specifics(capacity);
		}

		// module info support
		public string GetModuleTitle() { return "Process controller"; }
		public override string GetModuleDisplayName() { return "Process controller"; }
		public string GetPrimaryField() { return string.Empty; }
		public Callback<Rect> GetDrawModulePanelCallback() { return null; }

		// animation group support
		public void EnableModule() { }
		public void DisableModule() { }
		public bool ModuleIsActive() { return broken ? false : running; }
		public bool IsSituationValid() { return true; }
	}

} // KERBALISM

