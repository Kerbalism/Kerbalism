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
			public double maxAmount;	// storage capacity

			public CrewSpecs loadingReqs;	// loading requirements
			public CrewSpecs unloadingReqs; // unloading requirements

			public string virtualResID;

			public InternalResource(ConfigNode node)
			{
				name = Lib.ConfigValue(node, "name", string.Empty) ;
				rate = Lib.ConfigValue(node, "rate", 0.0);
				transferRate = Lib.ConfigValue(node, "transferRate", 0.0);
				amount = Lib.ConfigValue(node, "amount", 0.0);
				maxAmount = Lib.ConfigValue(node, "maxAmount", double.MaxValue);
				loadingReqs = new CrewSpecs(Lib.ConfigValue(node, "loadingReqs", "true"));
				unloadingReqs = new CrewSpecs(Lib.ConfigValue(node, "unloadingReqs", "true"));
			}

			public void LoadState(ConfigNode node)
			{
				name = Lib.ConfigValue(node, "name", string.Empty);
				amount = Lib.ConfigValue(node, "amount", 0.0);
				virtualResID = Lib.ConfigValue(node, "virtualResID", string.Empty);
			}

			public void SaveState(ConfigNode node)
			{
				node.AddValue("name", name);
				node.AddValue("amount", amount);
				node.AddValue("virtualResID", virtualResID);
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
		[KSPField] public double minThermalPower;       // minimal thermal power produced when running (kW)
		[KSPField] public double passiveCoolingPower;	// thermal power passively removed all the time (kW)

		[KSPField] public double meltdownEnergy;		// accumulated thermal energy required to trigger a meltdown (kJ)
		[KSPField] public double explosionEnergy;       // accumulated thermal energy required for the part to explode (kJ)
		[KSPField] public double startupDuration;		// time before the process will start producing power and heat (hours)
		[KSPField] public string startupResource;       // name of the resource consumed during startup
		[KSPField] public double startupResourceRate;   // rate of the resource consumed during startup
		[KSPField] public double powerFactor = 1.0;         // multiplier applied to all config-defined resource rates. rates * powerFactor = nominal rates. Allow to keep the same resource rate definitions for reactors that have the same balance but are more/less powerful
		[KSPField] public string coolantResourceName = "Coolant";
		[KSPField] public double coolantResourceKJ = 1.0; // ex : 1.0 -> 1 unit of coolant == 1 kJ
		[KSPField] public FloatCurve thermalDecayCurve = new FloatCurve(); // keys : time after process shutdown (hours), values : heat generated as a percentage of minThermalPowerFactor ([0;1] range)

		// UI-only config
		[KSPField] public string processName = "thermal process";   // This is for UI purpose only.
		[KSPField] public double nominalTemp = 800.0;	// This is for UI purpose only. Nominal running temperature (K)
		[KSPField] public double meltdownTemp = 1200.0; // This is for UI purpose only. Meltdown temperature (K)

		// internals
		private double overheatEnergy;                     // thermal energy accumulated (kJ)
		private double currentLoad;
		private double temperature;
		public List<Resource> resources;						// inputs/outputs
		public List<InternalResource> internalResources;       // non-removable inputs/outputs (simulating no-flow)

		[KSPField(isPersistant = true)] public double startCountdown;
		[KSPField(isPersistant = true)] public float hoursSinceShutdown;

		[KSPField(isPersistant = true)] public string processID; // I would prefer not having to use that
		[KSPField(isPersistant = true)] public string startOutputID;
		[KSPField(isPersistant = true)] public string nominalOutputID;
		[KSPField(isPersistant = true)] public string thermalOutputID;

		// PAW
		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "T")]
		public string processInfo;
		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "state")]
		public RunningState state;

		public override void OnLoad(ConfigNode node)
		{
			if (HighLogic.LoadedScene == GameScenes.LOADING)
			{
				resources = new List<Resource>();
				foreach (ConfigNode resNode in node.GetNodes("TP_RESOURCE")) resources.Add(new Resource(resNode));

				internalResources = new List<InternalResource>();
				foreach (ConfigNode intResNode in node.GetNodes("TP_INTERNAL_RESOURCE")) internalResources.Add(new InternalResource(intResNode));
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
			if (internalResources == null || internalResources.Count == 0) return;

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

			if (Lib.IsEditor()) return;

			VesselResHandler resHandler = ResourceCache.GetVesselHandler(vessel);
			foreach (InternalResource intRes in internalResources)
			{
				intRes.virtualResID = intRes.name + "_" + processID;
				// synchronize resource cache amounts with locally persisted amounts
				VirtualResource virtualRes = (VirtualResource)resHandler.GetResource(vessel, intRes.virtualResID);
				virtualRes.Amount = intRes.amount;
				virtualRes.Capacity = intRes.maxAmount;
			}

		}

		public void Update()
		{
			switch (state)
			{
				case RunningState.Stopped:
					Events["Toggle"].guiName = Lib.BuildString("Start ", processName);
					Fields["processInfo"].guiName = "Stopped";
					break;
				case RunningState.Starting:
					Events["Toggle"].guiName = Lib.BuildString("Stop ", processName);
					if (startupDuration == 0.0) break;
					temperature = ((1.0 - (startCountdown / (startupDuration * 3600.0))) * (nominalTemp - vessel.KerbalismData().EnvTemperature)) + vessel.KerbalismData().EnvTemperature;
					Fields["processInfo"].guiName = "Warming up";
					processInfo = Lib.BuildString(Lib.HumanReadableCountdown(startCountdown), ", temp. ", temperature.ToString("F0") + " K");
					break;
				case RunningState.Running:
					Events["Toggle"].guiName = Lib.BuildString("Stop ", processName);
					temperature = ((overheatEnergy / meltdownEnergy) * (meltdownTemp - nominalTemp)) + nominalTemp;
					Fields["processInfo"].guiName = "Running";
					if (overheatEnergy > 0.001)
						processInfo = Lib.BuildString("load ", currentLoad.ToString("P1"), ", <color=#ff2222><b>temp. ", temperature.ToString("F0") + " K</b></color>");
					else
						processInfo = Lib.BuildString("load ", currentLoad.ToString("P1"), ", temp. ", temperature.ToString("F0") + " K");
					break;
				case RunningState.Stopping:
					Events["Toggle"].guiName = Lib.BuildString("Start ", processName);
					Fields["processInfo"].guiName = "Stopping";
					break;
				case RunningState.Meltdown:
					Events["Toggle"].guiActive = false;
					Fields["processInfo"].guiName = "Meltdown";
					break;
				case RunningState.MeltdownStopped:
					Events["Toggle"].guiActive = false;
					Fields["processInfo"].guiName = "Meltdown";
					break;
				case RunningState.Failure:
					Events["Toggle"].guiActive = false;
					Fields["processInfo"].guiName = "Failure";
					break;
				default:
					break;
			}
		}

		public void FixedUpdate()
		{
			if (Lib.IsEditor()) return;

			VesselResHandler resHandler = ResourceCache.GetVesselHandler(vessel);

			switch (state)
			{
				case RunningState.Stopped:
					break;

				case RunningState.Starting:

					VirtualResource startResource = (VirtualResource)resHandler.GetResource(vessel, startOutputID);

					if (startResource.Amount < 1.0)
					{
						state = RunningState.Stopped;
						return;
					}

					startCountdown -= Kerbalism.elapsed_s;

					if (startCountdown <= 0.0)
					{
						state = RunningState.Running;
						startResource.Amount = 0.0;
						return;
					}

					startResource.Amount = 0.0;
					Recipe startRecipe = new Recipe(processName);
					startRecipe.AddInput(startupResource, startupResourceRate * powerFactor * Kerbalism.elapsed_s);
					startRecipe.AddOutput(startOutputID, 1.0, false);
					resHandler.AddRecipe(startRecipe);
					break;

				case RunningState.Running:
					// synchronize internal resources amounts after consumption/production in last resource sim step
					foreach (InternalResource intRes in internalResources)
						intRes.amount = resHandler.GetResource(vessel, intRes.virtualResID).Amount;

					// get outputs results from last resource sim step
					VirtualResource nominalOutput = (VirtualResource)resHandler.GetResource(vessel, nominalOutputID);
					currentLoad = nominalOutput.Amount;
					nominalOutput.Amount = 0.0;

					ThermalUpdate(resHandler, ((maxThermalPower - minThermalPower) * currentLoad) + minThermalPower, ref overheatEnergy);

					// create input/outputs recipe
					Recipe nominalRecipe = new Recipe(processName);
					foreach (InternalResource intRes in internalResources)
					{
						if (intRes.rate > 0.0) nominalRecipe.AddOutput(intRes.virtualResID, intRes.rate * powerFactor * Kerbalism.elapsed_s, false);
						else if (intRes.rate < 0.0) nominalRecipe.AddInput(intRes.virtualResID, Math.Abs(intRes.rate) * powerFactor * Kerbalism.elapsed_s);
					}
					foreach (Resource extraRes in resources)
					{
						if (extraRes.rate > 0.0) nominalRecipe.AddOutput(extraRes.name, extraRes.rate * powerFactor * Kerbalism.elapsed_s, extraRes.dump);
						else if (extraRes.rate < 0.0) nominalRecipe.AddInput(extraRes.name, Math.Abs(extraRes.rate) * powerFactor * Kerbalism.elapsed_s);
					}
					// virtual resource used to check output level
					nominalRecipe.AddOutput(nominalOutputID, 1.0, false);
					// register recipe
					resHandler.AddRecipe(nominalRecipe);
					break;

				case RunningState.Stopping:
					if (ThermalDecayUpdate(resHandler, thermalDecayCurve, ref hoursSinceShutdown, ref overheatEnergy))
						state = RunningState.Stopped;
					break;

				case RunningState.Meltdown:
					if (ThermalDecayUpdate(resHandler, thermalDecayCurve, ref hoursSinceShutdown, ref overheatEnergy))
						state = RunningState.MeltdownStopped;
					break;

			}

			if (overheatEnergy > explosionEnergy)
			{
				if (part.parent != null) part.parent.AddThermalFlux(overheatEnergy);
				foreach (Part child in part.children) child.AddThermalFlux(overheatEnergy);
				part.explode();
				return;
			}

			if (overheatEnergy > meltdownEnergy)
				state = RunningState.Meltdown;

		}

		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Toggle", active = true)]
		public void Toggle()
		{
			switch (state)
			{
				case RunningState.Stopped:
				case RunningState.Stopping:
					state = RunningState.Starting;
					startCountdown = startupDuration * 3600.0;
					((VirtualResource)ResourceCache.GetResource(vessel, startOutputID)).Amount = 1.0;
					break;
				case RunningState.Starting:
					state = RunningState.Stopping;
					hoursSinceShutdown = 0f; // TODO : scale this by start time
					break;
				case RunningState.Running:
					state = RunningState.Stopping;
					hoursSinceShutdown = 0f;
					break;
			}
		}

		/// <summary> update thermals while the process is stopping. Return true when all decay heat has been removed, false otherwise </summary>
		private bool ThermalDecayUpdate(VesselResHandler resHandler, FloatCurve thermalDecayCurve, ref float hoursSinceShutdown, ref double overheatEnergy)
		{
			hoursSinceShutdown += (float)(Kerbalism.elapsed_s / 3600.0);
			float decayFactor = thermalDecayCurve.Evaluate(hoursSinceShutdown);
			ThermalUpdate(resHandler, decayFactor * minThermalPower, ref overheatEnergy);
			return overheatEnergy <= 0.001 && decayFactor <= 0.001;
		}

		/// <summary> update thermal state </summary>
		private void ThermalUpdate(VesselResHandler resHandler, double thermalPowerProduced, ref double overheatEnergy)
		{
			// get thermal energy virtual resource
			VirtualResource thermalOutput = (VirtualResource)resHandler.GetResource(vessel, thermalOutputID);

			// update stored heat based on non-removed amount
			if (!thermalOutput.IsNewInstance)
				overheatEnergy = thermalOutput.Capacity - thermalOutput.Amount;

			// calculate thermal power (kW) dissipation needs based on the process power specs
			double coolantPowerNeed = Math.Max(thermalPowerProduced - passiveCoolingPower, 0.0);
			// scale it by time elapsed to get energy (kJ), then add the thermal energy stored in excess
			double coolantEnergyNeed = (coolantPowerNeed * coolantResourceKJ * Kerbalism.elapsed_s) + overheatEnergy;

			// reset virtual resource amount and capacity
			// we set the thermalOutput virtual resource capacity to the requested need,
			// this way we can keep track of exactly how much KJ of heat weren't removed.
			thermalOutput.Amount = 0.0;
			thermalOutput.Capacity = coolantEnergyNeed;

			if (coolantEnergyNeed == 0.0) return;

			// create coolant recipe
			Recipe coolantRecipe = new Recipe(processName);
			coolantRecipe.AddInput(coolantResourceName, coolantEnergyNeed);
			coolantRecipe.AddOutput(thermalOutputID, coolantEnergyNeed, false);
			resHandler.AddRecipe(coolantRecipe);
		}



	}
}
