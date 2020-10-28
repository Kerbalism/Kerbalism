using UnityEngine;

namespace KERBALISM
{
	public class GreenhouseData : ModuleData<ModuleKsmGreenhouse, GreenhouseData>
	{
		public string growthProcessName;      // internal name of the food production process
		public double growthProcessCapacity;  // this part modules capacity to run the production process
		public Process GrowthProcess { get; private set; } // the process associated with the process name, for convenience

		public string setupProcessName;	      // internal name of the setup process
		public double setupProcessCapacity;   // this part modules capacity to run the setup process
		public Process SetupProcess { get; private set; } // the process associated with the process name, for convenience

		public string substrateResourceName; // name of the substrate resource
		public double growthRate; // Current max. rate [0..1] of growth process
		public bool growthRunning;  // true/false, is the growth process running?
		public bool setupRunning;  // true/false, is the setup process running?

		public override void OnFirstInstantiate(ProtoPartModuleSnapshot protoModule, ProtoPartSnapshot protoPart)
		{
			growthProcessName = modulePrefab.growthProcessName;
			growthProcessCapacity = modulePrefab.growthProcessCapacity;
			GrowthProcess = Profile.processes.Find(p => p.name == growthProcessName);

			setupProcessName = modulePrefab.setupProcessName;
			setupProcessCapacity = modulePrefab.setupProcessCapacity;
			SetupProcess = Profile.processes.Find(p => p.name == setupProcessName);

			substrateResourceName = modulePrefab.substrateResourceName;
			growthRunning = modulePrefab.growthRunning;
			setupRunning = modulePrefab.setupRunning;
		}

		public void SetupGrowthProcess(string growthProcessName, double growthProcessCapacity)
		{
			this.growthProcessName = growthProcessName;
			this.growthProcessCapacity = growthProcessCapacity;
			GrowthProcess = Profile.processes.Find(p => p.name == growthProcessName);
		}

		public void SetupSetupProcess(string setupProcessName, double setupProcessCapacity)
		{
			this.setupProcessName = setupProcessName;
			this.setupProcessCapacity = setupProcessCapacity;
			SetupProcess = Profile.processes.Find(p => p.name == setupProcessName);
		}

		public override void OnLoad(ConfigNode node)
		{
			growthProcessName = Lib.ConfigValue(node, "growthProcessName", "");
			growthProcessCapacity = Lib.ConfigValue(node, "growthProcessCapacity", 0.0);
			GrowthProcess = Profile.processes.Find(p => p.name == growthProcessName);

			setupProcessName = Lib.ConfigValue(node, "setupProcessName", "");
			setupProcessCapacity = Lib.ConfigValue(node, "setupProcessCapacity", 0.0);
			SetupProcess = Profile.processes.Find(p => p.name == setupProcessName);

			substrateResourceName = Lib.ConfigValue(node, "substrateResourceName", "");
			growthRate = Lib.ConfigValue(node, "growthRate", 0.0);
			growthRunning = Lib.ConfigValue(node, "growthRunning", true);
			setupRunning = Lib.ConfigValue(node, "setupRunning", true);
		}

		public override void OnSave(ConfigNode node)
		{
			node.AddValue("growthProcessName", growthProcessName);
			node.AddValue("growthProcessCapacity", growthProcessCapacity);

			node.AddValue("setupProcessName", setupProcessName);
			node.AddValue("setupProcessCapacity", setupProcessCapacity);

			node.AddValue("substrateResourceName", substrateResourceName);
			node.AddValue("growthRate", growthRate);
			node.AddValue("growthRunning", growthRunning);
			node.AddValue("setupRunning", setupRunning);
		}

		public override void OnVesselDataUpdate()
		{
			// TODO account for max radiation and min light
			if (moduleIsEnabled)
			{
				VesselData.VesselProcesses.GetOrCreateProcessData(GrowthProcess).RegisterProcessControllerCapacity(growthRunning, growthProcessCapacity * growthRate);
				VesselData.VesselProcesses.GetOrCreateProcessData(SetupProcess).RegisterProcessControllerCapacity(setupRunning, setupProcessCapacity);
			}
		}

		internal void UpdateSubstrateLevel(PartResourceWrapper substrateRes)
		{
			if (substrateRes != null && substrateRes.Capacity > 0)
				growthRate = substrateRes.Amount / substrateRes.Capacity;
			else
				growthRate = 0;
		}
	}

	public class ModuleKsmGreenhouse : KsmPartModule<ModuleKsmGreenhouse, GreenhouseData>, IBackgroundModule, IModuleInfo, IB9Switchable
	{
		[KSPField] public string growthProcessName = string.Empty; // process name of in-flight food production process
		[KSPField] public double growthProcessCapacity = 1.0;

		[KSPField] public string setupProcessName = string.Empty; // optional, process name of in-flight greenhouse setup process
		[KSPField] public double setupProcessCapacity = 1.0;

		[KSPField] public string substrateResourceName = "Substrate"; // name of the substrate resource.
		[KSPField] public double substrateCapacity = 50.0; // amount of substrate in the greenouse when fully operational

		[KSPField] public string anim_shutters;      // animation to manipulate shutters
		[KSPField] public bool anim_shutters_reverse = false;

		[KSPField] public string anim_plants;        // animation to represent plant growth graphically
		[KSPField] public bool anim_plants_reverse = false;

		[KSPField] public string lamps;              // object with emissive texture used to represent intensity graphically

		[KSPField(groupName = "Greenhouse", groupDisplayName = "#KERBALISM_Group_Greenhouse")]
		[UI_Toggle(scene = UI_Scene.All, requireFullControl = false, affectSymCounterparts = UI_Scene.None)]
		public bool setupRunning;

		[KSPField(groupName = "Greenhouse", groupDisplayName = "#KERBALISM_Group_Greenhouse")]
		[UI_Toggle(scene = UI_Scene.All, requireFullControl = false, affectSymCounterparts = UI_Scene.None)]
		public bool growthRunning;

		private BaseField growthRunningField;
		private BaseField setupRunningField;

		// animation handlers
		private Animator shutterAnimator;
		private Animator plantsAnimator;
		private Renderer lampsRenderer;
		private Color lampColor;

		// cached values
		private PartResourceWrapper substrateRes;

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
			if (subtypeDataNode.HasValue(nameof(growthProcessName)))
				switchedProcessName = Lib.ConfigValue(subtypeDataNode, nameof(growthProcessName), string.Empty);
			else
				switchedProcessName = growthProcessName;

			Process switchedProcess = Profile.processes.Find(p => p.name == switchedProcessName);

			if (switchedProcess != null)
			{
				double switchedCapacity;
				if (subtypeDataNode.HasValue(nameof(growthProcessCapacity)))
					switchedCapacity = Lib.ConfigValue(subtypeDataNode, nameof(growthProcessCapacity), 0.0);
				else
					switchedCapacity = growthProcessCapacity;

				if (switchedCapacity > 0.0)
					return switchedProcess.GetInfo(switchedCapacity, true);
			}

			return null;
		}

		public override void OnStart(StartState state)
		{
			// PAW setup

			// synchronize PAW state with data state
			growthRunning = moduleData.growthRunning;
			setupRunning = moduleData.setupRunning;

			// get BaseField references
			growthRunningField = Fields["growthRunning"];
			setupRunningField = Fields["setupRunning"];

			// add value modified callbacks to the toggles
			growthRunningField.OnValueModified += OnToggleGrowth;
			setupRunningField.OnValueModified += OnToggleSetup;

			// set visibility
			growthRunningField.guiActive = growthRunningField.guiActiveEditor = true;
			setupRunningField.guiActive = setupRunningField.guiActiveEditor = moduleData.SetupProcess != null;

			// set names
			growthRunningField.guiName = moduleData.GrowthProcess.title ?? "Grow food";
			setupRunningField.guiName = moduleData.SetupProcess?.title ?? "Generate Substrate";

			((UI_Toggle)growthRunningField.uiControlFlight).enabledText = Lib.Color("enabled", Lib.Kolor.Green);
			((UI_Toggle)growthRunningField.uiControlFlight).disabledText = Lib.Color("disabled", Lib.Kolor.Yellow);
			((UI_Toggle)growthRunningField.uiControlEditor).enabledText = Lib.Color("enabled", Lib.Kolor.Green);
			((UI_Toggle)growthRunningField.uiControlEditor).disabledText = Lib.Color("disabled", Lib.Kolor.Yellow);

			((UI_Toggle)setupRunningField.uiControlFlight).enabledText = Lib.Color("enabled", Lib.Kolor.Green);
			((UI_Toggle)setupRunningField.uiControlFlight).disabledText = Lib.Color("disabled", Lib.Kolor.Yellow);
			((UI_Toggle)setupRunningField.uiControlEditor).enabledText = Lib.Color("enabled", Lib.Kolor.Green);
			((UI_Toggle)setupRunningField.uiControlEditor).disabledText = Lib.Color("disabled", Lib.Kolor.Yellow);

			// animations and light
			shutterAnimator = new Animator(part, anim_shutters, anim_shutters_reverse);
			plantsAnimator = new Animator(part, anim_plants, anim_plants_reverse);

			// cache lamps renderer
			if (lamps.Length > 0)
			{
				foreach (var rdr in part.GetComponentsInChildren<Renderer>())
				{
					if (rdr.name == lamps) {
						lampsRenderer = rdr;
						lampColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);
						lampsRenderer.material.SetColor("_EmissiveColor", lampColor);
						break;
					}
				}
			}

			if (!part.Resources.Contains(substrateResourceName))
				Lib.AddResource(part, substrateResourceName, 0, substrateCapacity);
			substrateRes = new LoadedPartResourceWrapper(part.Resources[substrateResourceName]);

			Setup();

			shutterAnimator.Still(growthRunning ? 0f : 1f);
		}

		public void Update()
		{
			// TODO turn on lights if current light level is too low
			// set lamps emissive object
			if (lampsRenderer != null)
			{
				lampColor.a = (growthRunning || setupRunning) ? 1.0f : 0.0f;
				lampsRenderer.material.SetColor("_EmissiveColor", lampColor);
			}

			plantsAnimator.Still((float)moduleData.growthRate);
		}

		public void FixedUpdate()
		{
			moduleData.UpdateSubstrateLevel(substrateRes);
		}

		public void BackgroundUpdate(VesselData vd, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule, double elapsed_s)
		{
			if (!ModuleData.TryGetModuleData<ModuleKsmGreenhouse, GreenhouseData>(protoModule, out GreenhouseData greenhouseData))
				return;

			ProtoPartResourceWrapper substrateRes = null;

			foreach (ProtoPartResourceSnapshot protoResource in protoPart.resources)
			{
				if (protoResource.resourceName == greenhouseData.substrateResourceName)
				{
					substrateRes = new ProtoPartResourceWrapper(protoResource);
					break;
				}
			}

			greenhouseData.UpdateSubstrateLevel(substrateRes);
		}

		/// <summary>  start the module. must be idempotent: expect to be called several times </summary>
		private void Setup()
		{
			if (string.IsNullOrEmpty(growthProcessName))
			{
				Lib.LogDebug($"No growth process, disabling module");
				enabled = isEnabled = moduleIsEnabled = false;
				moduleData.moduleIsEnabled = false;
				return;
			}

			enabled = isEnabled = moduleIsEnabled = true;
			moduleData.moduleIsEnabled = true;

			// we might be restarting with a different configuration
			if (moduleData.growthProcessName != growthProcessName || moduleData.growthProcessCapacity != growthProcessCapacity)
			{
				Lib.LogDebug($"Configuring with growth process '{growthProcessName}' (was '{moduleData.growthProcessName}')");
				moduleData.SetupGrowthProcess(growthProcessName, growthProcessCapacity);
			}
			if (moduleData.setupProcessName != setupProcessName || moduleData.setupProcessCapacity != setupProcessCapacity)
			{
				Lib.LogDebug($"Configuring with setup process '{setupProcessCapacity}' (was '{moduleData.setupProcessCapacity}')");
				moduleData.SetupSetupProcess(setupProcessName, setupProcessCapacity);
			}

			setupRunningField.guiActive = setupRunningField.guiActiveEditor = moduleData.SetupProcess != null;
			growthRunning = moduleData.growthRunning;
			setupRunning = moduleData.setupRunning && setupRunningField.guiActive;
		}

		private void OnToggleSetup(object field) => ToggleSetup(moduleData);

		public static void ToggleSetup(GreenhouseData greenhouseData)
		{
			greenhouseData.setupRunning = !greenhouseData.setupRunning;

			if (greenhouseData.IsLoaded)
			{
				greenhouseData.loadedModule.setupRunning = greenhouseData.setupRunning;

				// refresh VAB/SPH ui
				if (Lib.IsEditor)
					GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
			}
		}

		private void OnToggleGrowth(object field) => ToggleGrowth(moduleData);

		public static void ToggleGrowth(GreenhouseData greenhouseData)
		{
			greenhouseData.growthRunning = !greenhouseData.growthRunning;

			if (greenhouseData.IsLoaded)
			{
				greenhouseData.loadedModule.growthRunning = greenhouseData.growthRunning;
				greenhouseData.loadedModule.shutterAnimator.Play(greenhouseData.growthRunning, false, null, Lib.IsEditor ? 4.0f : 1.0f);

				// refresh VAB/SPH ui
				if (Lib.IsEditor)
					GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
			}
		}


		// part tooltip
		public override string GetInfo()
		{
			var process = moduleData?.GrowthProcess ?? Profile.processes.Find(p => p.name == growthProcessName);
			string result = process.GetInfo(moduleData?.growthProcessCapacity ?? growthProcessCapacity, true);

			var setupProcess = moduleData?.SetupProcess ?? Profile.processes.Find(p => p.name == setupProcessName);
			if(setupProcess != null)
				result += "\n" + setupProcess.GetInfo(moduleData?.setupProcessCapacity ?? setupProcessCapacity, true);

			return result;
		}

		// automation
		public override AutomationAdapter[] CreateAutomationAdapter(KsmPartModule moduleOrPrefab, ModuleData moduleData)
		{
			return new AutomationAdapter[] {
				new GreenhouseGrowthAutomationAdapter(moduleOrPrefab, moduleData),
				new GreenhouseSetupAutomationAdapter(moduleOrPrefab, moduleData)
			};
		}

		// module info support
		public string GetModuleTitle() => Local.Greenhouse;
		public override string GetModuleDisplayName() => Local.Greenhouse;
		public string GetPrimaryField() => Local.Greenhouse;
		public Callback<Rect> GetDrawModulePanelCallback() => null;

		private abstract class GreenhouseAutomationAdapter : AutomationAdapter
		{
			protected ModuleKsmGreenhouse greenhouseModule => module as ModuleKsmGreenhouse;
			protected GreenhouseData data => moduleData as GreenhouseData;

			public GreenhouseAutomationAdapter(KsmPartModule module, ModuleData moduleData) : base(module, moduleData) { }
		}

		private class GreenhouseGrowthAutomationAdapter : GreenhouseAutomationAdapter
		{
			public GreenhouseGrowthAutomationAdapter(KsmPartModule module, ModuleData moduleData) : base(module, moduleData) { }


			public override string Name => "Greenhouse grow food"; // must be hardcoded
			public override string DisplayName => data.GrowthProcess.title;

			public override string Status => Lib.Color(data.growthRunning, Local.Generic_RUNNING, Lib.Kolor.Green, Local.Generic_STOPPED, Lib.Kolor.Orange);

			public override void Ctrl(bool value)
			{
				if(data.growthRunning != value)
					ToggleGrowth(data);
			}

			public override void Toggle()
			{
				ToggleGrowth(data);
			}
		}

		private class GreenhouseSetupAutomationAdapter : GreenhouseAutomationAdapter
		{
			public GreenhouseSetupAutomationAdapter(KsmPartModule module, ModuleData moduleData) : base(module, moduleData)
			{
				IsVisible = data.SetupProcess != null;
			}

			public override string Name => "Greenhouse generate substrate"; // must be hardcoded
			public override string DisplayName => data.SetupProcess?.title ?? "Generate substrate";

			public override string Status => Lib.Color(data.setupRunning, Local.Generic_RUNNING, Lib.Kolor.Green, Local.Generic_STOPPED, Lib.Kolor.Orange);

			public override void Ctrl(bool value)
			{
				if (data.setupRunning != value)
					ToggleSetup(data);
			}

			public override void Toggle()
			{
				ToggleSetup(data);
			}
		}
	}
}
