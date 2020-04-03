using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{
	/// <summary>
	/// Replacement for ProcessController
	/// </summary>
	public class ModuleKsmProcessController : KsmPartModule<ModuleKsmProcessController, ProcessControllerData>, IModuleInfo, IAnimatedModule, ISwitchable
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
				prefabData.SetPartModuleReferences(this, this);
				prefabData.OnFirstInstantiate(null, null);
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

		public string GetSubtypeDescription(ConfigNode subtypeDataNode)
		{
			string switchedProcessName;
			if (subtypeDataNode.HasValue(nameof(processName)))
				switchedProcessName = Lib.ConfigValue(subtypeDataNode, nameof(processName), string.Empty);
			else
				switchedProcessName = processName;

			Process switchedProcess = Profile.processes.Find(p => p.name == switchedProcessName);

			if (switchedProcess != null)
			{
				double switchedCapacity;
				if (subtypeDataNode.HasValue(nameof(switchedCapacity)))
					switchedCapacity = Lib.ConfigValue(subtypeDataNode, nameof(switchedCapacity), 0.0);
				else
					switchedCapacity = capacity;

				if (switchedCapacity > 0.0)
					return switchedProcess.GetInfo(switchedCapacity, true);
			}

			return null;
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

			// we might be restarting with a different configuration
			if (moduleData.processName != processName || moduleData.processCapacity != capacity)
			{
				Lib.LogDebug($"Configuring with process '{processName}' (was '{moduleData.processName}')");
				moduleData.Setup(processName, capacity);
			}

			// PAW setup
			running = moduleData.isRunning;
			runningField.guiActive = runningField.guiActiveEditor = moduleData.Process.canToggle;
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

		

		public bool IsRunning()
		{
			return running && !string.IsNullOrEmpty(processName);
		}

		// part tooltip
		public override string GetInfo()
		{
			if (moduleData == null || moduleData.Process == null)
				return string.Empty;

			return moduleData.Process.GetInfo(moduleData.processCapacity, true);
		}

		// module info support
		public string GetModuleTitle()
		{
			if (moduleData == null || moduleData.Process == null)
				return string.Empty;

			return moduleData.Process.title;
		}

		public override string GetModuleDisplayName() => GetModuleTitle();
		public string GetPrimaryField() { return string.Empty; }
		public Callback<Rect> GetDrawModulePanelCallback() { return null; }

		// animation group support
		public void EnableModule() { }
		public void DisableModule() { }
		public bool ModuleIsActive() { return broken ? false : running; }
		public bool IsSituationValid() { return true; }
	}

} // KERBALISM

