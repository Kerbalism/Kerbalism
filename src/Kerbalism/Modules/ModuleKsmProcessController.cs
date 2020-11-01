using UnityEngine;

namespace KERBALISM
{
	public class ProcessControllerData : ModuleData<ModuleKsmProcessController, ProcessControllerData>
	{
		public string processName;      // internal name of the process (i.e. "scrubber" or "sabatier")
		public double processCapacity; // this part modules original/total capacity to run this process
		public double processAmount = -1; // this part modules remaining capacity to run this process (-1 = not initialized)
		public bool isRunning;  // true/false, if process controller is turned on or not
		public bool isBroken;   // true if process controller is broken
		public Process Process { get; private set; } // the process associated with the process name, for convenience

		private PartVirtualResource processResource;

		public override void OnFirstInstantiate(ProtoPartModuleSnapshot protoModule, ProtoPartSnapshot protoPart)
		{
			isRunning = modulePrefab.running;
			isBroken = modulePrefab.broken;

			processName = modulePrefab.processName;
			Process = Profile.processes.Find(p => p.name == processName);
		}

		public void Setup(string processName, double processCapacity)
		{
			if(processResource != null)
			{
				VesselData.ResHandler.RemovePartVirtualResource(processResource);
				processResource = null;
			}

			this.processName = processName;
			this.processCapacity = processCapacity;

			Process = Profile.processes.Find(p => p.name == processName);

			Lib.LogDebug($"Setup {processName} resource {Process.resourceName} capacity ({processAmount}/{processCapacity})");
		}

		public override void OnLoad(ConfigNode node)
		{
			isRunning = Lib.ConfigValue(node, "isRunning", true);
			isBroken = Lib.ConfigValue(node, "isBroken", false);
			processName = Lib.ConfigValue(node, "processName", "");
			processAmount = Lib.ConfigValue(node, "processAmount", 1.0);
			processCapacity = Lib.ConfigValue(node, "processCapacity", 1.0);

			Process = Profile.processes.Find(p => p.name == processName);

			Lib.LogDebug($"Loaded {processName} resource {Process.resourceName} capacity ({processAmount}/{processCapacity})");
		}

		public override void OnSave(ConfigNode node)
		{
			node.AddValue("processName", processName);
			node.AddValue("isRunning", isRunning);
			node.AddValue("isBroken", isBroken);
			node.AddValue("processCapacity", processCapacity);
			node.AddValue("processAmount", processAmount);
		}

		public override void OnVesselDataUpdate()
		{
			if (moduleIsEnabled && !isBroken)
			{
				if (processResource == null)
				{
					processResource = new PartVirtualResource(Process.resourceName);
					processResource.SetCapacity(processCapacity);

					// processAmount = -1 means that the value was not initialized, and we use the process capacity as initial amount
					processResource.SetAmount(processAmount < 0 ? processCapacity : processAmount);

					VesselData.ResHandler.AddPartVirtualResource(processResource);
					Lib.LogDebug($"Initialized process {processName} resource {processResource.Name} @ {processResource.Amount}/{processResource.Capacity}");
				}

				processCapacity = processResource.Capacity;
				processAmount = processResource.Amount;
				VesselData.VesselProcesses.GetOrCreateProcessData(Process).RegisterProcessControllerCapacity(isRunning, processResource.Amount);
			}
		}
	}

	public class ModuleKsmProcessController : KsmPartModule<ModuleKsmProcessController, ProcessControllerData>, IModuleInfo, IAnimatedModule, IB9Switchable
	{
		[KSPField] public string processName = string.Empty;
		[KSPField] public double capacity = 1.0;
		[KSPField] public string uiGroupName = null;         // internal name of the UI group
		[KSPField] public string uiGroupDisplayName = null;  // display name of the UI group

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
				if (subtypeDataNode.HasValue(nameof(capacity)))
					switchedCapacity = Lib.ConfigValue(subtypeDataNode, nameof(capacity), 0.0);
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

			if (uiGroupName != null)
				runningField.group = new BasePAWGroup(uiGroupName, uiGroupDisplayName ?? uiGroupName, false);
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
		public string GetPrimaryField() {
			var process = Profile.processes.Find(p => p.name == processName);
			return process?.GetInfo(capacity, true);
		}
		public Callback<Rect> GetDrawModulePanelCallback() { return null; }

		// animation group support
		public void EnableModule() { }
		public void DisableModule() { }
		public bool ModuleIsActive() { return broken ? false : running; }
		public bool IsSituationValid() { return true; }
	}

} // KERBALISM

