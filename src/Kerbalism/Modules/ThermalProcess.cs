using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KERBALISM
{
	public class ThermalProcess : PartModule, IPartCostModifier, IPartMassModifier
	{
		// TODO :
		// - auto-limit load based on coolant available
		// - internal resource depletion ETA
		// - planner tools :
		//	- set internal resource amounts -> floatrange (see UIPartActionFloatRange.slider.interactable +
		//  - external resources : 









		/// <summary>
		/// resource stored inside the module and that can require special condtions to be loaded/unloaded (ex : EnrichedUranium, DepletedFuel)
		/// </summary>
		public class InternalResource
		{
			public string name;
			public double rate;			// rate of consumption/production
			public double transferRate; // transfer rate when loading/unloading the resource
			public float amount;       // current stored amount
			public float maxAmount;    // storage capacity

			public bool loadingEnabled;
			public bool unloadingEnabled;
			public CrewSpecs loadingReqs;	// loading requirements
			public CrewSpecs unloadingReqs; // unloading requirements

			public enum TransferState { none, loading, unloading }
			public TransferState transferState = TransferState.none;

			public string virtualResID;

			private PartResourceDefinition stockDefinition;
			public float MassPerUnit => stockDefinition.density;
			public float CostPerUnit => stockDefinition.unitCost;

			private BaseEvent pawEvent;
			private BaseField pawSlider;
			private UI_FloatRange sliderControl;
			private ThermalProcess module;

			public InternalResource(ThermalProcess module, ConfigNode node)
			{
				this.module = module;
				name = Lib.ConfigValue(node, "name", string.Empty) ;
				rate = Lib.ConfigValue(node, "rate", 0.0);
				transferRate = Lib.ConfigValue(node, "transferRate", 0.0);
				amount = Lib.ConfigValue(node, "amount", 0f);
				maxAmount = Lib.ConfigValue(node, "maxAmount", float.MaxValue);
				loadingEnabled = Lib.ConfigValue(node, "loadingEnabled", true);
				unloadingEnabled = Lib.ConfigValue(node, "unloadingEnabled", true);
				loadingReqs = new CrewSpecs(Lib.ConfigValue(node, "loadingReqs", "false"));
				unloadingReqs = new CrewSpecs(Lib.ConfigValue(node, "unloadingReqs", "false"));

				stockDefinition = PartResourceLibrary.Instance.GetDefinition(name);
			}

#if !KSP15_16
			public void PAWInit(ThermalProcess module, BasePAWGroup resGroup)
#else
			public void PAWInit(ThermalProcess module)
#endif
			{
				pawEvent = new BaseEvent(module.Events, name, new BaseEventDelegate(PAWEvent), new KSPEvent());

				pawEvent.guiActive = true;
				pawEvent.guiName = name + " : " + amount.ToString("F2") + " / " + maxAmount.ToString("F2");


				sliderControl = new UI_FloatRange();
				sliderControl.minValue = 0f;
				sliderControl.maxValue = (float)maxAmount;

				pawSlider = new BaseField(new KSPField(), GetType().GetField("amount"), this);
				pawSlider.guiActive = true;
				pawSlider.guiActiveEditor = true;
				pawSlider.guiName = name;
				pawSlider.guiFormat = "F2";
				pawSlider.uiControlEditor = sliderControl;
				pawSlider.uiControlFlight = sliderControl;

#if !KSP15_16
				pawEvent.group = resGroup;
				pawSlider.group = resGroup;
#endif
				module.Events.Add(pawEvent);
				module.Fields.Add(pawSlider);
			}

			public void PAWEvent()
			{
				List<DialogGUIButton> buttons = new List<DialogGUIButton>();
				string info = Lib.BuildString("stored : ", amount.ToString("F2"), ", capacity : ", maxAmount.ToString("F2"), "\ntransfer rate : ", transferRate.ToString("F3"), "/s");
				if (!loadingEnabled) info = Lib.BuildString(info, "\nLoading unavailable for this resource");
				else if (loadingReqs.enabled && !loadingReqs.Check()) info = Lib.BuildString(info, "\nLoading unavailable : ", loadingReqs.Warning());
				else buttons.Add(new DialogGUIButton("Load from vessel", () => transferState = TransferState.loading));
				if (!unloadingEnabled) info = Lib.BuildString(info, "\nUnloading unavailable for this resource");
				else if (unloadingReqs.enabled && !unloadingReqs.Check()) info = Lib.BuildString(info, "\nUnloading unavailable : ", unloadingReqs.Warning());
				else buttons.Add(new DialogGUIButton("Unload to vessel", () => transferState = TransferState.unloading));

				buttons.Add(new DialogGUIButton("cancel", null));

				Lib.Popup(name + " internal storage", info, buttons.ToArray());
			}

			public void PAWUpdate(bool transferAvailable)
			{

				pawEvent.guiActive = transferAvailable;
				if (transferAvailable)
				{
					pawEvent.guiName = Lib.BuildString("Transfer ", name);
					if (transferState != TransferState.none) pawEvent.guiName = Lib.BuildString(pawEvent.guiName, " (", transferState.ToString(), "...)");
				}

				if (Lib.IsFlight() && sliderControl.partActionItem != null)
				{
					UIPartActionFloatRange actionItem = (UIPartActionFloatRange)pawSlider.uiControlEditor.partActionItem;

					actionItem.slider.interactable = false;
#if !KSP15_16
					// float input fields are only available since KSP 1.7.3
					if (Lib.KSPVersion >= new Version(1,7,3))
						actionItem.inputField.interactable = false;
#endif
				}
			}

			public void LoadFromVessel(Vessel v, double elapsedSec)
			{
				VesselResHandler resHandler = ResourceCache.GetVesselHandler(v);
				IResource res = resHandler.GetResource(v, name);
				if (res.Amount == 0.0)
				{
					transferState = TransferState.none;
					Message.Post("No " + name + " available on " + v.GetDisplayName() + "\nStopping transfer to " + module.processName);
					return;
				}

				amount = (float)resHandler.GetResource(v, virtualResID).Amount;

				if (amount == maxAmount) 
				{
					transferState = TransferState.none;
					return;
				}
				double amountToTransfer = transferRate * elapsedSec;
				Recipe transferRecipe = new Recipe(module.processName);
				transferRecipe.AddInput(name, amountToTransfer);
				transferRecipe.AddOutput(virtualResID, amountToTransfer, false);
				resHandler.AddRecipe(transferRecipe);
			}

			public void UnloadToVessel(Vessel v, double elapsedSec)
			{
				VesselResHandler resHandler = ResourceCache.GetVesselHandler(v);
				IResource res = resHandler.GetResource(v, name);
				if (res.Level == 1.0)
				{
					transferState = TransferState.none;
					Message.Post("No storage available on " + v.GetDisplayName() + " for " + name +  "\nStopping transfer from " + module.processName);
					return;
				}

				amount = (float)resHandler.GetResource(v, virtualResID).Amount;

				if (amount == 0.0)
				{
					transferState = TransferState.none;
					return;
				}
				double amountToTransfer = transferRate * elapsedSec;
				Recipe transferRecipe = new Recipe(module.processName);
				transferRecipe.AddInput(virtualResID, amountToTransfer);
				transferRecipe.AddOutput(name, amountToTransfer, false);
				resHandler.AddRecipe(transferRecipe);
			}

			public void LoadState(ConfigNode node)
			{
				name = Lib.ConfigValue(node, "name", string.Empty);
				amount = Lib.ConfigValue(node, "amount", 0f);
				virtualResID = Lib.ConfigValue(node, "virtualResID", string.Empty);
				transferState = Lib.ConfigValue(node, "transferState", TransferState.none);
			}

			public void SaveState(ConfigNode node)
			{
				node.AddValue("name", name);
				node.AddValue("amount", amount);
				node.AddValue("virtualResID", virtualResID);
				node.AddValue("transferState", transferState);
			}
		}

		public class Resource
		{
			public string name;
			public double rate; // rate of consumption/production
			public bool dump;   // should a produced resource be dumped if no storage available

			public Resource(ConfigNode node)
			{
				name = Lib.ConfigValue(node, "name", string.Empty);
				rate = Lib.ConfigValue(node, "rate", 0.0);
				dump = Lib.ConfigValue(node, "dump", true);
			}
		}

		public enum RunningState
		{
			Stopped,
			RequestStart,
			Starting,
			Running,
			Meltdown,
			Failure,
			EditorDecay
		}

		// config
		[KSPField] public double maxThermalPower;		// thermal power produced when running at nominal rate (kW)
		[KSPField] public double minThermalPower;       // minimal thermal power produced when running or starting (kW)
		[KSPField] public double thermalFeebackFactor;       // influence of the temperature on the minThermalPower. a value of 1.0 mean that minThermalPower is 100 % at nominalTemp and 200% at nominalTemp * 2.0. A value of 0.1 mean 110% at nominalTemp * 2.0.
		// a process with a high value will be able to start by itself without using the start resource but will be more prone to meltdown
		[KSPField] public double passiveCoolingPower;   // thermal power passively removed all the time (kW)
		[KSPField] public double nominalEnergy;         // nominal thermal capacity (kJ). Determine how fast the temperature will change and how much startup and coolant resource are needed.
		[KSPField] public double baseTemp = 285.0;   // base temperature (K)
		[KSPField] public double nominalTemp = 600.0;   // Nominal running temperature (K). Need to be reached by consuming the startup resource for the process to start
		[KSPField] public double meltdownTemp = 1200.0; // Meltdown temperature (K). Note that the combination of the nominal and meltdown energies and temperatures are all used to calculate the "Kelvin per kJ" factor of the process
		[KSPField] public double explosionTemp = 1600.0; // Temperature this will trigger a part explosion (K)
		[KSPField] public double passiveRadiation = 1600.0; // Temperature this will trigger a part explosion (K)
		[KSPField] public double nominalRadiation = 1600.0; // Temperature this will trigger a part explosion (K)
		[KSPField] public double meltdownRadiation = 1600.0; // Temperature this will trigger a part explosion (K)
		[KSPField] public double explosionRadiation = 1600.0; // Temperature this will trigger a part explosion (K)
		[KSPField] public string startupResource = "ElectricCharge";       // name of the resource that will be consumed until nominalEnergy is reached
		[KSPField] public double startupResourceMaxRate = 1.0;       // set to 0.0 to have the process start by itself whithout requiring any resource
		[KSPField] public double startupResourceKJ = 1.0;   // if 1.0, 1 unit is 1 kJ
		[KSPField] public string coolantResourceName = "Coolant";
		[KSPField] public double coolantResourceKJ = 1.0; // if 1.0, 1 unit is 1 kJ
		[KSPField] public FloatCurve thermalDecayCurve = new FloatCurve(); // keys : time after process shutdown (hours), values : heat generated as a percentage of minThermalPowerFactor ([0;1] range)
		[KSPField] public double powerFactor = 1.0;         // multiplier applied to all config-defined resource rates. rates * powerFactor = nominal rates. Allow to keep the same resource rate definitions for reactors that have the same balance but are more/less powerful

		// UI-only config
		[KSPField] public string processName = "thermal process";   // This is for UI purpose only.




		// internals
		//[KSPField(guiActive = true, guiActiveEditor = true, guiName = "storedHeatEnergy")]
		private double thermalEnergy;                     // thermal energy accumulated (kJ)
		private double currentLoad;
		private double temperature;
		private double decayFactor;
		public List<Resource> resources;						// inputs/outputs
		public List<InternalResource> internalResources;       // non-removable inputs/outputs (simulating no-flow)
		private float internalResourcesCost;
		private float internalResourcesMass;

		//[KSPField(guiActive = true, guiActiveEditor = true, guiName = "Thermal power", guiFormat = "F0", guiUnits = "kW")]
		public double thermalPowerProduced;                     // thermal power produced (kW)

		//[KSPField(guiActive = true, guiActiveEditor = true, guiName = "Coolant need", guiFormat = "F0", guiUnits = "kW")]
		public double coolantPowerRequired;                     // thermal power produced (kW)



		[KSPField(isPersistant = true)] public string processID; // I would prefer not having to use that
		[KSPField(isPersistant = true)] public string startOutputID;
		[KSPField(isPersistant = true)] public string nominalOutputID;
		[KSPField(isPersistant = true)] public string thermalOutputID;
		[KSPField(isPersistant = true)] public double KelvinPerkJ;

		//[KSPField(guiActive = true, guiActiveEditor = true, guiName = "state")]
		public RunningState state;

		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "State")]
		public string stateInfo;

		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "Temp")]
		public string thermalInfo;

		[KSPField(guiActive = true, guiActiveEditor = false, guiName = "Load limit safety"),
		UI_Toggle(scene = UI_Scene.All)]
		public bool loadLimitAutoMode = true;

		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "Load limiter", guiFormat = "P0"),
		UI_FloatRange(minValue = 0f, maxValue = 1f, stepIncrement = 0.01f, scene = UI_Scene.All)]
		public float loadLimit = 1f;

		[KSPField(isPersistant = true, guiName = "Hours since shutdown", guiFormat = "F1", guiUnits = "Hours"),
		UI_FloatRange(minValue = 0f, stepIncrement = 0.1f, scene = UI_Scene.Editor)]
		public float hoursSinceShutdown = float.MaxValue;

		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "Time to depletion")]
		public string resourcesInfo;

		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "ToggleState")]
		public void ToggleState()
		{
			if (Lib.IsEditor())
			{
				switch (state)
				{
					case RunningState.Stopped: state = RunningState.Starting; break;
					case RunningState.Starting: state = RunningState.Running; break;
					case RunningState.Running:
						state = RunningState.EditorDecay;
						hoursSinceShutdown = 0f;
						break;
					case RunningState.EditorDecay: state = RunningState.Stopped; break;

				}
			}
			else
			{
				switch (state)
				{
					case RunningState.Stopped:
						state = RunningState.RequestStart;
						break;
					case RunningState.Starting:
						state = RunningState.Stopped;
						ThermalReset();
						break;

					case RunningState.Running:
						state = RunningState.Stopped;
						ThermalReset();
						hoursSinceShutdown = 0f;
						break;
				}
			}
		}

		public override void OnLoad(ConfigNode node)
		{
			if (HighLogic.LoadedScene == GameScenes.LOADING)
			{
				resources = new List<Resource>();
				foreach (ConfigNode resNode in node.GetNodes("TP_RESOURCE")) resources.Add(new Resource(resNode));

				internalResources = new List<InternalResource>();
				foreach (ConfigNode intResNode in node.GetNodes("TP_INTERNAL_RESOURCE")) internalResources.Add(new InternalResource(this, intResNode));
			}
			else
			{
				GetInternalResourcesFromPrefab();
			}

			foreach (ConfigNode intResNode in node.GetNodes("TP_INTERNAL_RESOURCE_STATE"))
			{
				InternalResource intRes = internalResources.Find(p => p.name == Lib.ConfigValue(intResNode, "name", string.Empty));
				if (intRes == null) continue;
				intRes.LoadState(intResNode);
			}
		}

		private void GetInternalResourcesFromPrefab()
		{
			ThermalProcess prefab;
			try { prefab = (ThermalProcess)part.partInfo.partPrefab.Modules[part.Modules.IndexOf(this)]; }
			catch (Exception)
			{
				Lib.Log("ERROR : Could not find the ThermalProcess module prefab at index " + part.Modules.IndexOf(this) + " on part " + part.partName);
				enabled = isEnabled = moduleIsEnabled = false;
				return;
			}

			resources = prefab.resources;
			internalResources = prefab.internalResources;
		}

		public override void OnSave(ConfigNode node)
		{
			if (internalResources == null || internalResources.Count == 0 || processID == null) return;

			foreach (InternalResource intRes in internalResources)
			{
				ConfigNode internalResourcesNode = new ConfigNode("TP_INTERNAL_RESOURCE_STATE");
				intRes.SaveState(internalResourcesNode);
				node.AddNode(internalResourcesNode);
			}
		}

		public override void OnStart(StartState startState)
		{
			processID = Lib.IsFlight() ? processName + "_" + part.flightID : processName + "_" + part.GetInstanceID();
			startOutputID = "start_" + processID;
			nominalOutputID = "nominal_" + processID;
			thermalOutputID = "thermal_" + processID;
			KelvinPerkJ = (nominalTemp - baseTemp) / nominalEnergy;

#if !KSP15_16
			BasePAWGroup processGroup = new BasePAWGroup(processName, processName, false);
			Fields["thermalInfo"].group = processGroup;
			Fields["stateInfo"].group = processGroup;
			Fields["loadLimitAutoMode"].group = processGroup;
			Fields["loadLimit"].group = processGroup;
			Events["ToggleState"].group = processGroup;
			Fields["hoursSinceShutdown"].group = processGroup;
			BasePAWGroup resourcesGroup = new BasePAWGroup("Internal resources", "Internal resources", false);
			Fields["resourcesInfo"].group = resourcesGroup;
#endif
			GetInternalResourcesFromPrefab();

			foreach (InternalResource intRes in internalResources)
			{
#if !KSP15_16
				intRes.PAWInit(this, resourcesGroup);
#else
				intRes.PAWInit(this);
#endif
				intRes.virtualResID = intRes.name + "_" + processID;
				if (Lib.IsFlight())
				{
					// synchronize resource cache amounts with locally persisted amounts
					VirtualResource virtualRes = (VirtualResource)ResourceCache.GetResource(vessel, intRes.virtualResID);
					virtualRes.Amount = intRes.amount;
					virtualRes.Capacity = intRes.maxAmount;
				}
			}

			if (Lib.IsFlight()) ThermalReset();
		}

		public void Update()
		{

			double timeToDepletion = double.MaxValue;
			foreach (InternalResource intRes in internalResources)
			{
				
				intRes.PAWUpdate(state == RunningState.Stopped);
				if (intRes.rate < 0.0)
				{
					double effectiveRate = Math.Abs(intRes.rate) * powerFactor * (Lib.IsEditor() ? loadLimit : currentLoad);
					if (effectiveRate > 0.0) 
					{
						double resTimeToDepletion = intRes.amount / effectiveRate;
						if (resTimeToDepletion < timeToDepletion) timeToDepletion = resTimeToDepletion;
					}
				}
			}
			if (timeToDepletion == double.MaxValue) timeToDepletion = 0.0;

			if (Lib.IsFlight())
			{
				resourcesInfo = Lib.HumanReadableDuration(timeToDepletion);

				switch (state)
				{
					case RunningState.Stopped:
						Events["ToggleState"].active = true;
						Events["ToggleState"].guiName = Lib.BuildString("Start ", processName);
						break;
					case RunningState.Starting:
					case RunningState.Running:
						Events["ToggleState"].active = true;
						Events["ToggleState"].guiName = Lib.BuildString("Stop ", processName);
						break;
					default:
						Events["ToggleState"].active = false;
						break;
				}

				StringBuilder sb = new StringBuilder();

				if (temperature > nominalTemp + 1.0) sb.Append("<color=#ff2222><b>");
				sb.Append(temperature.ToString("F0"));
				sb.Append(" K");
				if (temperature > nominalTemp + 1.0) sb.Append("</b></color>");
				if (temperature < nominalTemp - 1.0)
				{
					sb.Append(" (nominal ");
					sb.Append(nominalTemp.ToString("F0"));
					sb.Append("K)");
				}
				else if (temperature > meltdownTemp + 1.0)
				{
					sb.Append(" (explosion ");
					sb.Append(nominalTemp.ToString("F0"));
					sb.Append("K)");
				}
				else if (temperature > nominalTemp + 1.0)
				{
					sb.Append(" (meltdown ");
					sb.Append(meltdownTemp.ToString("F0"));
					sb.Append("K)");
				}
				thermalInfo = sb.ToString();

				sb.Length = 0;

				switch (state)
				{
					case RunningState.Meltdown: sb.Append(Lib.Color(state.ToString().ToLower(), Lib.KColor.Red, true)); break;
					case RunningState.Failure: sb.Append(Lib.Color(state.ToString().ToLower(), Lib.KColor.Red, true)); break;
					case RunningState.Starting: sb.Append(Lib.Color(state.ToString().ToLower(), Lib.KColor.Yellow, true)); break;
					default: sb.Append(Lib.Color(state.ToString().ToLower(), Lib.KColor.Green, true)); break;
				}

				if (state == RunningState.Running)
				{
					sb.Append(", load ");
					sb.Append(currentLoad.ToString("P0"));
				}
				if (thermalPowerProduced > 0)
				{
					sb.Append(", heat ");
					sb.Append(thermalPowerProduced.ToString("F0"));
					sb.Append(" kW");
				}

				stateInfo = sb.ToString();
			}
			else
			{
				switch (state)
				{
					case RunningState.Stopped:
						Events["ToggleState"].guiName = "Simulate starting state";
						Fields["hoursSinceShutdown"].guiActiveEditor = false;
						thermalInfo = Lib.BuildString(baseTemp.ToString("F0"), " K");
						stateInfo = Lib.Color("Stopped", Lib.KColor.Yellow, true);
						resourcesInfo = "none";
						break;
					case RunningState.Starting:
						Events["ToggleState"].guiName = "Simulate running state";
						Fields["hoursSinceShutdown"].guiActiveEditor = false;
						thermalInfo = Lib.BuildString("warming up from ", baseTemp.ToString("F0"), " K to ", nominalTemp.ToString("F0"), " K");
						double startResRate = startupResourceMaxRate * powerFactor;
						stateInfo = Lib.BuildString(Lib.Color("Starting", Lib.KColor.Orange, true), ", -", startResRate.ToString("F2"), " ",
							PartResourceLibrary.Instance.GetDefinition(startupResource).abbreviation, "/s for ",
							Lib.HumanReadableDuration(nominalEnergy / (startResRate * startupResourceKJ)));
						resourcesInfo = "none";
						break;
					case RunningState.Running:
						Events["ToggleState"].guiName = "Simulate thermal decay";
						Fields["hoursSinceShutdown"].guiActiveEditor = false;
						double runningCoolant = (((maxThermalPower - minThermalPower) * loadLimit) + minThermalPower - passiveCoolingPower) * coolantResourceKJ;
						thermalInfo = Lib.BuildString(nominalTemp.ToString("F0"), " K, need ", runningCoolant.ToString("F0"), " ", coolantResourceName, "/s");
						stateInfo = Lib.Color("Running", Lib.KColor.Green, true);
						resourcesInfo = Lib.HumanReadableDuration(timeToDepletion);
						break;
					case RunningState.EditorDecay:
						Events["ToggleState"].guiName = "Simulate stopped state";
						((UI_FloatRange)Fields["hoursSinceShutdown"].uiControlEditor).maxValue = thermalDecayCurve.maxTime;
						Fields["hoursSinceShutdown"].guiActiveEditor = true;
						double decayCoolant = Math.Max((minThermalPower * thermalDecayCurve.Evaluate(hoursSinceShutdown)) - passiveCoolingPower, 0.0) * coolantResourceKJ;
						thermalInfo = Lib.BuildString("to ", baseTemp.ToString("F0"), " K, need ", decayCoolant.ToString("F0"), " ", coolantResourceName, "/s");
						stateInfo = Lib.Color("Thermal decay", Lib.KColor.Orange, true);
						resourcesInfo = "none";
						break;
				}
			}
		}

		public void FixedUpdate()
		{

			internalResourcesCost = 0f;
			internalResourcesMass = 0f;
			foreach (InternalResource intRes in internalResources)
			{
				internalResourcesCost += intRes.amount * intRes.CostPerUnit;
				internalResourcesMass += intRes.amount * intRes.MassPerUnit;

				switch (intRes.transferState)
				{
					case InternalResource.TransferState.loading: intRes.LoadFromVessel(vessel, Kerbalism.elapsed_s); break;
					case InternalResource.TransferState.unloading: intRes.UnloadToVessel(vessel, Kerbalism.elapsed_s); break;
				}
			}

			if (Lib.IsEditor()) return;

			VesselResHandler resHandler = ResourceCache.GetVesselHandler(vessel);

			switch (state)
			{
				case RunningState.RequestStart:

					internalResources.ForEach(a => a.transferState = InternalResource.TransferState.none);
					
					if (startupResourceMaxRate == 0.0)
					{
						state = RunningState.Running;
					}
					else
					{
						((VirtualResource)resHandler.GetResource(vessel, startOutputID)).Amount = 0.0;
						state = RunningState.Starting;
					}
					break;
				case RunningState.Starting:
					
					if (thermalEnergy >= nominalEnergy)
					{
						state = RunningState.Running;
						ThermalReset();
						return;
					}
					break;

				case RunningState.Running:
					// synchronize internal resources amounts after consumption/production in last resource sim step
					foreach (InternalResource intRes in internalResources)
						intRes.amount = (float)resHandler.GetResource(vessel, intRes.virtualResID).Amount;

					// get outputs results from last resource sim step
					VirtualResource nominalOutput = (VirtualResource)resHandler.GetResource(vessel, nominalOutputID);
					if (loadLimitAutoMode && thermalEnergy > 0.0) loadLimit *= (float)Math.Min(nominalEnergy / thermalEnergy, 1.0);

					currentLoad = nominalOutput.Amount * loadLimit;
					nominalOutput.Amount = 0.0;

					// create input/outputs recipe
					Recipe nominalRecipe = new Recipe(processName);
					double rateFactor = powerFactor * loadLimit * Kerbalism.elapsed_s;
					foreach (InternalResource intRes in internalResources)
					{
						if (intRes.rate > 0.0) nominalRecipe.AddOutput(intRes.virtualResID, intRes.rate * rateFactor, false);
						else if (intRes.rate < 0.0) nominalRecipe.AddInput(intRes.virtualResID, Math.Abs(intRes.rate) * rateFactor);
					}
					foreach (Resource extraRes in resources)
					{
						if (extraRes.rate > 0.0) nominalRecipe.AddOutput(extraRes.name, extraRes.rate * rateFactor, extraRes.dump);
						else if (extraRes.rate < 0.0) nominalRecipe.AddInput(extraRes.name, Math.Abs(extraRes.rate) * rateFactor);
					}
					// virtual resource used to check output level
					nominalRecipe.AddOutput(nominalOutputID, 1.0, false);
					// register recipe
					resHandler.AddRecipe(nominalRecipe);
					break;
			}


			if (state != RunningState.Running)
			{
				if (state == RunningState.Starting)
				{
					float thermalDecayTime = thermalDecayCurve.maxTime * (float)Math.Min(thermalEnergy / nominalEnergy, 1.0);
					if (hoursSinceShutdown > thermalDecayTime) hoursSinceShutdown = thermalDecayTime;
				}
				if (hoursSinceShutdown < thermalDecayCurve.maxTime)
				{
					hoursSinceShutdown += (float)(Kerbalism.elapsed_s / 3600.0);
					decayFactor = thermalDecayCurve.Evaluate(hoursSinceShutdown);
					ThermalUpdate(resHandler, decayFactor * minThermalPower);
				}
				else
				{
					ThermalUpdate(resHandler, 0.0);
				}
			}
			else
			{
				ThermalUpdate(resHandler, ((maxThermalPower - minThermalPower) * currentLoad) + minThermalPower);
			}



			if (temperature > explosionTemp)
			{
				if (part.parent != null) part.parent.AddThermalFlux(thermalEnergy / Kerbalism.elapsed_s) ;
				foreach (Part child in part.children.Where(p => p.parent == part)) child.AddThermalFlux(thermalEnergy / Kerbalism.elapsed_s);
				part.explode();
				return;
			}

			if (temperature > meltdownTemp && state != RunningState.Meltdown)
			{
				state = RunningState.Meltdown;
				hoursSinceShutdown = 0f;
				ThermalReset();
			}
		}


		private void ThermalReset()
		{
			// get thermal energy virtual resource
			VirtualResource thermalOutput = (VirtualResource)ResourceCache.GetResource(vessel, thermalOutputID);
			thermalOutput.Capacity = 0.0;
			thermalOutput.Amount = 0.0;
		}

		/// <summary> update thermal state </summary>
		private void ThermalUpdate(VesselResHandler resHandler, double thermalPowerAdded)
		{
			// get thermal energy virtual resource
			VirtualResource thermalOutput = (VirtualResource)resHandler.GetResource(vessel, thermalOutputID);

			// update stored heat based on removed amount (capacity was set at the energy level accounting for last step heat production)
			// don't do it when the virtual resource was just created
			if (thermalOutput.Capacity > 0.0)
				thermalEnergy = thermalOutput.Capacity - thermalOutput.Amount;

			// reset virtual resource amount
			thermalOutput.Amount = 0.0;


			if (state == RunningState.Starting)
			{
				VirtualResource startResourceOutput = (VirtualResource)resHandler.GetResource(vessel, startOutputID);
				thermalEnergy += startResourceOutput.Amount;

				startResourceOutput.Amount = 0.0;

				double heatNeeded = Math.Min(nominalEnergy - thermalEnergy, startupResourceMaxRate * startupResourceKJ * powerFactor * Kerbalism.elapsed_s);
				Recipe startRecipe = new Recipe(processName);
				startRecipe.AddInput(startupResource, heatNeeded);
				startRecipe.AddOutput(startOutputID, heatNeeded, false);
				resHandler.AddRecipe(startRecipe);
			}

			temperature = baseTemp + (thermalEnergy * KelvinPerkJ);

			// calculate thermal power (kW) dissipation needs based on the process power specs
			thermalPowerProduced = Math.Max(thermalPowerAdded - passiveCoolingPower, 0.0);
			// scale it by time elapsed to get energy (kJ), add it to the currently stored energy and save the value in the virtual resource capacity
			// this allow to keep track of the added/removed energy between updates without having to use a persisted variable for that.

			thermalOutput.Capacity = (thermalPowerProduced * Kerbalism.elapsed_s) + thermalEnergy;

			// when running or starting, we want to keep the process at the nominal energy (and temperature)
			double energyToRemove = thermalOutput.Capacity;
			if (state == RunningState.Running || state == RunningState.Starting)
				energyToRemove -= nominalEnergy;

			// do nothing is no cooling is requested
			if (energyToRemove <= 0.0)
			{
				coolantPowerRequired = 0.0;
			}
			else
			{
				energyToRemove *= coolantResourceKJ;
				coolantPowerRequired = energyToRemove / Kerbalism.elapsed_s;
				// create coolant recipe
				Recipe coolantRecipe = new Recipe(processName);
				coolantRecipe.AddInput(coolantResourceName, energyToRemove);
				coolantRecipe.AddOutput(thermalOutputID, energyToRemove, false);
				resHandler.AddRecipe(coolantRecipe);
			}




		}

		
		public float GetModuleCost(float defaultCost, ModifierStagingSituation sit) => internalResourcesCost;
		public ModifierChangeWhen GetModuleCostChangeWhen() => ModifierChangeWhen.CONSTANTLY;

		public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) => internalResourcesMass;
		public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.CONSTANTLY;
	}
}
