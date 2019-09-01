using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KERBALISM
{
	public class ThermalProcess : PartModule
	{

		/// <summary>
		/// resource stored inside the module and that can require special condtions to be loaded/unloaded (ex : EnrichedUranium, DepletedFuel)
		/// </summary>
		public class InternalResource
		{
			public string name;
			public double rate;			// rate of consumption/production
			public double transferRate; // transfer rate when loading/unloading the resource
			public double amount;		// current stored amount
			public double maxAmount;    // storage capacity

			public bool loadingEnabled;
			public bool unloadingEnabled;
			public CrewSpecs loadingReqs;	// loading requirements
			public CrewSpecs unloadingReqs; // unloading requirements

			public enum TransferState { none, loading, unloading }
			public TransferState transferState = TransferState.none;

			public string virtualResID;

			public BaseEvent pawEvent;
			public BaseField pawStatus;
			public string statusInfo;
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
			}

			public void PAWInit(ThermalProcess module)
			{
				pawEvent = new BaseEvent(module.Events, name, new BaseEventDelegate(PAWEvent), new KSPEvent());

				pawEvent.guiActive = true;
				pawEvent.guiName = name + " : " + amount.ToString("F2") + " / " + maxAmount.ToString("F2");
				module.Events.Add(pawEvent);

				pawStatus = new BaseField(new KSPField(), GetType().GetField("statusInfo"), this);
				pawStatus.guiActive = true;
				pawStatus.guiActiveEditor = true;
				pawStatus.guiName = name;
				module.Fields.Add(pawStatus);
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

				statusInfo = Lib.BuildString( amount.ToString("F2"), " / ", maxAmount.ToString("F2"));
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

				amount = resHandler.GetResource(v, virtualResID).Amount;

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

				amount = resHandler.GetResource(v, virtualResID).Amount;

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
			StartFailed,
			Running,
			Stopping,
			Meltdown,
			MeltdownStopped,
			Failure
		}

		// config
		[KSPField] public double maxThermalPower;		// thermal power produced when running at nominal rate (kW)
		[KSPField] public double minThermalPower;       // minimal thermal power produced when running or starting (kW)
		[KSPField] public double thermalFeebackFactor;       // influence of the temperature on the minThermalPower. a value of 1.0 mean that minThermalPower is 100 % at nominalTemp and 200% at nominalTemp * 2.0. A value of 0.1 mean 110% at nominalTemp * 2.0.
		// a process with a high value will be able to start by itself without using the start resource but will be more prone to meltdown
		[KSPField] public double passiveCoolingPower;   // thermal power passively removed all the time (kW)
		[KSPField] public double nominalEnergy;         // nominal thermal capacity (kJ). Determine how fast the temperature will change and how much startup and coolant resource are needed. 
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
		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "storedHeatEnergy")]
		private double thermalEnergy;                     // thermal energy accumulated (kJ)
		private double currentLoad;
		private double temperature;
		private double decayFactor;
		public List<Resource> resources;						// inputs/outputs
		public List<InternalResource> internalResources;       // non-removable inputs/outputs (simulating no-flow)

		[KSPField(isPersistant = true)] public float hoursSinceShutdown;
		[KSPField(isPersistant = true)] public string processID; // I would prefer not having to use that
		[KSPField(isPersistant = true)] public string startOutputID;
		[KSPField(isPersistant = true)] public string nominalOutputID;
		[KSPField(isPersistant = true)] public string thermalOutputID;
		[KSPField(isPersistant = true)] public double KelvinPerkJ;

		public float silly = 0f;

		// PAW
		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "processInfo")]
		public string processInfo;

		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "Load limiter", guiFormat = "P0"),
		UI_FloatRange(minValue = 0f, maxValue = 1f, stepIncrement = 0.01f, scene = UI_Scene.All)]
		public float loadLimit = 1f;



		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "state")]
		public RunningState state;

		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Toggle", active = true)]
		public void Toggle()
		{
			switch (state)
			{
				case RunningState.Stopped:
				case RunningState.Stopping:
				case RunningState.StartFailed:
					state = RunningState.RequestStart;
					break;
				case RunningState.Starting:
					state = RunningState.StartFailed;
					ThermalReset();
					break;

				case RunningState.Running:
					state = RunningState.Stopping;
					ThermalReset();
					hoursSinceShutdown = 0f;
					break;
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

			foreach (ConfigNode intResNode in node.GetNodes("TP_INTERNAL_RESOURCE_STATE"))
			{
				InternalResource intRes = internalResources.Find(p => p.name == Lib.ConfigValue(intResNode, "name", string.Empty));
				if (intRes == null) continue;
				intRes.LoadState(intResNode);
			}
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
			KelvinPerkJ = nominalTemp / nominalEnergy;

			if (Lib.IsEditor()) return;

			VesselResHandler resHandler = ResourceCache.GetVesselHandler(vessel);
			foreach (InternalResource intRes in internalResources)
			{
				intRes.virtualResID = intRes.name + "_" + processID;
				// synchronize resource cache amounts with locally persisted amounts
				VirtualResource virtualRes = (VirtualResource)resHandler.GetResource(vessel, intRes.virtualResID);
				virtualRes.Amount = intRes.amount;
				virtualRes.Capacity = intRes.maxAmount;
				intRes.PAWInit(this);
			}

		}

		public void Update()
		{

			if (state == RunningState.Stopped)
				temperature = vessel.KerbalismData().EnvTemperature;
			else
				temperature = thermalEnergy * KelvinPerkJ;

			string basicTempInfo;
			if (temperature < nominalTemp + 1.0) basicTempInfo = Lib.BuildString("temp. ", temperature.ToString("F0"), " K");
			else basicTempInfo = Lib.BuildString("<color=#ff2222><b>temp. ", temperature.ToString("F0"), " K</b></color>");

			string fullTempInfo;
			if (temperature < nominalTemp - 1.0) fullTempInfo = Lib.BuildString(basicTempInfo, " (", nominalTemp.ToString("F0"), " K nominal)");
			else if (temperature > meltdownTemp + 1.0) fullTempInfo = Lib.BuildString(basicTempInfo, " (explosion ", explosionTemp.ToString("F0"), "K)");
			else if (temperature > nominalTemp + 1.0) fullTempInfo = Lib.BuildString(basicTempInfo, " (meltdown ", meltdownTemp.ToString("F0"), "K)");
			else fullTempInfo = basicTempInfo;

			switch (state)
			{
				case RunningState.Stopped:
					Events["Toggle"].guiActive = true;
					Events["Toggle"].guiName = Lib.BuildString("Start ", processName);
					Fields["processInfo"].guiName = "Stopped";
					processInfo = fullTempInfo;
					break;
				case RunningState.Starting:
					Events["Toggle"].guiActive = true;
					Events["Toggle"].guiName = Lib.BuildString("Stop ", processName);
					Fields["processInfo"].guiName = "Warming up";
					processInfo = fullTempInfo;
					break;
				case RunningState.StartFailed:
					Events["Toggle"].guiActive = true;
					Events["Toggle"].guiName = Lib.BuildString("Start ", processName);
					Fields["processInfo"].guiName = "Start failed";
					processInfo = fullTempInfo;
					break;
				case RunningState.Running:
					Events["Toggle"].guiActive = true;
					Events["Toggle"].guiName = Lib.BuildString("Stop ", processName);
					Fields["processInfo"].guiName = "Running";
					processInfo = Lib.BuildString("load ", currentLoad.ToString("P0"), ", ", fullTempInfo);
					break;
				case RunningState.Stopping:
					Events["Toggle"].guiActive = true;
					Events["Toggle"].guiName = Lib.BuildString("Start ", processName);
					Fields["processInfo"].guiName = "Stopping";
					processInfo = Lib.BuildString("waste heat ", (decayFactor * minThermalPower).ToString("F0"), "kW, ", basicTempInfo);
					break;
				case RunningState.Meltdown:
				case RunningState.MeltdownStopped:
					Events["Toggle"].guiActive = false;
					Fields["processInfo"].guiName = "Meltdown";
					processInfo = Lib.BuildString("waste heat ", (decayFactor * minThermalPower).ToString("F0"), "kW, ", basicTempInfo);
					break;
				case RunningState.Failure:
					Events["Toggle"].guiActive = false;
					Fields["processInfo"].guiName = "Failure";
					break;
			}

			foreach (InternalResource intRes in internalResources) intRes.PAWUpdate(state == RunningState.Stopped);
		}

		public void FixedUpdate()
		{
			if (Lib.IsEditor()) return;

			VesselResHandler resHandler = ResourceCache.GetVesselHandler(vessel);

			switch (state)
			{
				case RunningState.RequestStart:

					internalResources.ForEach(a => a.transferState = InternalResource.TransferState.none);

					double environnementEnergy = vessel.KerbalismData().EnvTemperature / KelvinPerkJ;
					if (environnementEnergy < 0.0) thermalEnergy = environnementEnergy;
					else thermalEnergy = Math.Max(thermalEnergy, environnementEnergy);
					
					if (startupResourceMaxRate == 0.0)
					{
						state = RunningState.Running;
					}
					else
					{
						((VirtualResource)resHandler.GetResource(vessel, startOutputID)).Amount = 0.001;
						state = RunningState.Starting;
					}
					break;
				case RunningState.Starting:

					VirtualResource startResourceOutput = (VirtualResource)resHandler.GetResource(vessel, startOutputID);
					thermalEnergy += startResourceOutput.Amount;
					if (startResourceOutput.Amount == 0.0)
					{
						state = RunningState.StartFailed;
						ThermalReset();
						return;
					}
					if (thermalEnergy >= nominalEnergy)
					{
						state = RunningState.Running;
						ThermalReset();
						return;
					}

					startResourceOutput.Amount = 0.0;

					double heatNeeded = Math.Min(nominalEnergy - thermalEnergy, startupResourceMaxRate * startupResourceKJ * powerFactor * Kerbalism.elapsed_s);
					Recipe startRecipe = new Recipe(processName);
					startRecipe.AddInput(startupResource, heatNeeded);
					startRecipe.AddOutput(startOutputID, heatNeeded, false);
					resHandler.AddRecipe(startRecipe);
					break;

				case RunningState.StartFailed:
					if (thermalEnergy < vessel.KerbalismData().EnvTemperature / KelvinPerkJ)
					{
						state = RunningState.Stopped;
						return;
					}
					thermalEnergy -= passiveCoolingPower * Kerbalism.elapsed_s;

					//ThermalUpdate(resHandler, 0.0, ref thermalEnergy);
					break;

				case RunningState.Running:
					// synchronize internal resources amounts after consumption/production in last resource sim step
					foreach (InternalResource intRes in internalResources)
						intRes.amount = resHandler.GetResource(vessel, intRes.virtualResID).Amount;

					// get outputs results from last resource sim step
					VirtualResource nominalOutput = (VirtualResource)resHandler.GetResource(vessel, nominalOutputID);
					currentLoad = nominalOutput.Amount * loadLimit;
					nominalOutput.Amount = 0.0;

					ThermalUpdate(resHandler, ((maxThermalPower - minThermalPower) * currentLoad) + minThermalPower, ref thermalEnergy);

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

				case RunningState.Stopping:
					if (ThermalDecayUpdate(resHandler, thermalDecayCurve, ref hoursSinceShutdown, ref thermalEnergy, out decayFactor))
						state = RunningState.Stopped;
					break;

				case RunningState.Meltdown:
					if (ThermalDecayUpdate(resHandler, thermalDecayCurve, ref hoursSinceShutdown, ref thermalEnergy, out decayFactor))
						state = RunningState.MeltdownStopped;
					break;

			}

			foreach (InternalResource intRes in internalResources)
			{
				switch (intRes.transferState)
				{
					case InternalResource.TransferState.loading: intRes.LoadFromVessel(vessel, Kerbalism.elapsed_s); break;
					case InternalResource.TransferState.unloading: intRes.UnloadToVessel(vessel, Kerbalism.elapsed_s); break;
				}

			}

			if (thermalEnergy > explosionTemp / KelvinPerkJ)
			{
				if (part.parent != null) part.parent.AddThermalFlux(thermalEnergy / Kerbalism.elapsed_s) ;
				foreach (Part child in part.children.Where(p => p.parent == part)) child.AddThermalFlux(thermalEnergy / Kerbalism.elapsed_s);
				part.explode();
				return;
			}

			if (thermalEnergy > meltdownTemp / KelvinPerkJ)
				state = RunningState.Meltdown;

		}


		private void ThermalReset()
		{
			// get thermal energy virtual resource
			VirtualResource thermalOutput = (VirtualResource)ResourceCache.GetResource(vessel, thermalOutputID);
			thermalOutput.Capacity = 0.0;
			thermalOutput.Amount = 0.0;
		}

		/// <summary> update thermals while the process is stopping. Return true when temperature has has been removed, false otherwise </summary>
		private bool ThermalDecayUpdate(VesselResHandler resHandler, FloatCurve thermalDecayCurve, ref float hoursSinceShutdown, ref double storedHeatEnergy, out double decayFactor)
		{
			hoursSinceShutdown += (float)(Kerbalism.elapsed_s / 3600.0);
			decayFactor = thermalDecayCurve.Evaluate(hoursSinceShutdown);
			ThermalUpdate(resHandler, decayFactor * minThermalPower, ref storedHeatEnergy);
			return decayFactor < 0.01;
		}

		/// <summary> update thermal state </summary>
		private void ThermalUpdate(VesselResHandler resHandler, double thermalPowerProduced, ref double storedHeatEnergy)
		{
			// get thermal energy virtual resource
			VirtualResource thermalOutput = (VirtualResource)resHandler.GetResource(vessel, thermalOutputID);

			// update stored heat based on removed amount (capacity was set at the energy level accounting for last step heat production)
			// don't do it when the virtual resource was just created
			if (thermalOutput.Capacity > 0.0) storedHeatEnergy = thermalOutput.Capacity - thermalOutput.Amount;

			// reset virtual resource amount
			thermalOutput.Amount = 0.0;

			// calculate thermal power (kW) dissipation needs based on the process power specs
			double coolantPowerNeed = Math.Max(thermalPowerProduced - passiveCoolingPower, 0.0);
			// scale it by time elapsed to get energy (kJ), add it to the currently stored energy and save the value in the virtual resource capacity
			// this allow to keep track of the added/removed energy between updates without having to use a persisted variable for that.
			thermalOutput.Capacity = (coolantPowerNeed * coolantResourceKJ * Kerbalism.elapsed_s) + storedHeatEnergy;

			// when running or starting, we want to keep the process at the nominal energy (and temperature)
			double coolantEnergyNeed = thermalOutput.Capacity;
			if (state == RunningState.Running || state == RunningState.Starting)
				coolantEnergyNeed -= nominalEnergy;

			// do nothing is no cooling is requested
			if (coolantEnergyNeed <= 0.0) return;


			
			// create coolant recipe
			Recipe coolantRecipe = new Recipe(processName);
			coolantRecipe.AddInput(coolantResourceName, coolantEnergyNeed);
			coolantRecipe.AddOutput(thermalOutputID, coolantEnergyNeed, false);
			resHandler.AddRecipe(coolantRecipe);
		}



	}
}
