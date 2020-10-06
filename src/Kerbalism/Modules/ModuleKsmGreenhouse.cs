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
		public bool isRunning;  // true/false, is this thing running?

		public override void OnFirstInstantiate(ProtoPartModuleSnapshot protoModule, ProtoPartSnapshot protoPart)
		{
			growthProcessName = modulePrefab.growthProcessName;
			growthProcessCapacity = modulePrefab.growthProcessCapacity;
			GrowthProcess = Profile.processes.Find(p => p.name == growthProcessName);

			setupProcessName = modulePrefab.setupProcessName;
			setupProcessCapacity = modulePrefab.setupProcessCapacity;
			SetupProcess = Profile.processes.Find(p => p.name == setupProcessName);

			substrateResourceName = modulePrefab.substrateResourceName;
			isRunning = modulePrefab.running;
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
			isRunning = Lib.ConfigValue(node, "isRunning", true);
		}

		public override void OnSave(ConfigNode node)
		{
			node.AddValue("growthProcessName", growthProcessName);
			node.AddValue("growthProcessCapacity", growthProcessCapacity);

			node.AddValue("setupProcessName", setupProcessName);
			node.AddValue("setupProcessCapacity", setupProcessCapacity);

			node.AddValue("substrateResourceName", substrateResourceName);
			node.AddValue("growthRate", growthRate);
			node.AddValue("isRunning", isRunning);
		}

		public override void OnVesselDataUpdate()
		{
			// TODO account for max radiation and min light
			if (moduleIsEnabled)
			{
				var growthCapacity = growthProcessCapacity * growthRate;
				VesselData.VesselProcesses.GetOrCreateProcessData(GrowthProcess).RegisterProcessControllerCapacity(isRunning, growthProcessCapacity);
				VesselData.VesselProcesses.GetOrCreateProcessData(SetupProcess).RegisterProcessControllerCapacity(isRunning, setupProcessCapacity);
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
		[KSPField] public double minLight = 400;           // minimum lighting flux required for growth, in W/m^2
		[KSPField] public double maxRadiation = 0.00001;   // maximum radiation allowed for growth in rad/s (plants are very tolerant towards radiation)

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

		[KSPField(guiActiveUnfocused = true, groupName = "Greenhouse", groupDisplayName = "#KERBALISM_Group_Greenhouse")]
		[UI_Toggle(scene = UI_Scene.All, affectSymCounterparts = UI_Scene.None)]
		public bool running;
		private BaseField runningField;

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
			runningField = Fields["running"];
			runningField.OnValueModified += (field) => Toggle(moduleData, true);

			shutterAnimator = new Animator(part, anim_shutters, anim_shutters_reverse);
			plantsAnimator = new Animator(part, anim_plants, anim_plants_reverse);

			shutterAnimator.Still(running ? 1f : 0f);

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

			((UI_Toggle)runningField.uiControlFlight).enabledText = Lib.Color(Local.Generic_ENABLED.ToLower(), Lib.Kolor.Green);
			((UI_Toggle)runningField.uiControlFlight).disabledText = Lib.Color(Local.Generic_DISABLED.ToLower(), Lib.Kolor.Yellow);
			((UI_Toggle)runningField.uiControlEditor).enabledText = Lib.Color(Local.Generic_ENABLED.ToLower(), Lib.Kolor.Green);
			((UI_Toggle)runningField.uiControlEditor).disabledText = Lib.Color(Local.Generic_DISABLED.ToLower(), Lib.Kolor.Yellow);

			Setup();
		}

		public void Update()
		{
			// TODO turn on lights if current light level is too low
			// set lamps emissive object
			if (lampsRenderer != null)
				lampColor.a = running ? 1.0f : 0.0f;

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
			Lib.LogDebug($"Greenhouse on {part.name} starting with growth process '{growthProcessName}' / setup process '{setupProcessName}'");

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

			// we might be restarting with a different configuration
			if (moduleData.setupProcessName != setupProcessName || moduleData.setupProcessCapacity != setupProcessCapacity)
			{
				Lib.LogDebug($"Configuring with setup process '{setupProcessCapacity}' (was '{moduleData.setupProcessCapacity}')");
				moduleData.SetupSetupProcess(setupProcessName, setupProcessCapacity);
			}

			// PAW setup
			running = moduleData.isRunning;
			runningField.guiActive = runningField.guiActiveEditor = moduleData.GrowthProcess.canToggle;
			runningField.guiName = moduleData.GrowthProcess.title;
		}

		public static void Toggle(GreenhouseData greenhouseData, bool isLoaded)
		{
			greenhouseData.isRunning = !greenhouseData.isRunning;

			if (isLoaded)
			{
				greenhouseData.loadedModule.running = greenhouseData.isRunning;

				// refresh VAB/SPH ui
				if (Lib.IsEditor)
					GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
			}
		}

		public bool IsRunning()
		{
			return running && !string.IsNullOrEmpty(growthProcessName);
		}

		// part tooltip
		public override string GetInfo()
		{
			if (moduleData == null || moduleData.GrowthProcess == null)
				return string.Empty;

			string result = moduleData.GrowthProcess.GetInfo(moduleData.growthProcessCapacity, true);

			if(moduleData.SetupProcess != null)
				result += "\n" + moduleData.SetupProcess.GetInfo(moduleData.growthProcessCapacity, true);

			return result;
		}

		// module info support
		public string GetModuleTitle()
		{
			if (moduleData == null || moduleData.GrowthProcess == null)
				return string.Empty;

			return moduleData.GrowthProcess.title;
		}

		public override string GetModuleDisplayName() => GetModuleTitle();
		public string GetPrimaryField() { return string.Empty; }
		public Callback<Rect> GetDrawModulePanelCallback() { return null; }


		/*
		PRODUCTION_RECIPE
		{
			INPUT
			{
				name = KsmWasteAtmosphere
				substitute = Oxygen
				rate = 0.1
			}

			INPUT
			{
				name = Ammonia
				rate = 0.02
			}

			INPUT
			{
				name = Water
				rate = 0.02
			}

			OUTPUT
			{
				name = Food
				rate = 0.02
			}

			OUTPUT
			{
				name = Oxygen
				rate = 0.02
				dumpByDefault = true
			}
		}

		SETUP_RECIPE
		{
			INPUT
			{
				name = KsmWasteAtmosphere
				substitute = Oxygen
				rate = 0.1
			}

			INPUT
			{
				name = Ammonia
				rate = 0.02
			}

			INPUT
			{
				name = Water
				rate = 0.02
			}

			INPUT
			{
				name = Substrate
				rate = 0.02
			}

			OUTPUT
			{
				name = Oxygen
				rate = 0.1
				dumpByDefault = true
			}
		}

		*/
	}
}
