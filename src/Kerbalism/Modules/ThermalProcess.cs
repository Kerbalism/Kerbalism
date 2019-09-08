using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Reflection;
using System.Collections;

namespace KERBALISM
{
	public interface IBackgroundModule<MD, PM>
	where MD : PartModuleData
	where PM : PartModule
	{
		void BackgroundUpdate(Vessel v, PartData pd, MD md, PM prefab, double elapsedSec);
	}

	public class ThermalProcess : PartModule, IPartCostModifier, IPartMassModifier, IBackgroundModule<ThermalProcess.ThermalProcessData, ThermalProcess>
	{
		// TODO :
		// - better "safety" toggle that work at high timewarp rate (maybe we just need to cheat the thermal system)
		// - radiation
		// - planner tools :
		//	- set internal resource amounts -> floatrange (see UIPartActionFloatRange.slider.interactable +
		//  - external resources : 


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

		/// <summary> Short process name</summary>
		[KSPField] public string processName = "thermal process";   // This is for UI purpose only.

		/// <summary>
		/// Thermal energy stored at nominal temperature (kJ). Higher value mean the process will be more stable but harder to start.
		/// Determine how fast the temperature will change and how much startup resource is needed to reach the nominal temperature.
		/// </summary>
		[KSPField] public double nominalEnergy;

		/// <summary> stopped state temperature (K)</summary>
		[KSPField] public double baseTemp = 285.0;

		/// <summary>
		/// Nominal running temperature (K). Need to be reached by consuming the startup resource for the process to start producing resources.
		/// The combination of baseTemp, nominalTemp and nominalEnergy determine the specific heat capacity of the process :
		/// `KelvinPerkJ = (nominalTemp - baseTemp) / nominalEnergy`
		/// </summary>
		[KSPField] public double nominalTemp = 600.0;

		/// <summary>
		/// Meltdown temperature (K).
		/// Set to a lower value than `baseTemp` to disable the feature.
		/// </summary>
		[KSPField] public double meltdownTemp = double.MinValue;

		/// <summary>
		/// Temperature (K) that will trigger a part explosion and release heat to adjacent parts.
		/// Set to a lower value than `baseTemp` to disable the feature.
		/// </summary>
		[KSPField] public double explosionTemp = double.MinValue;

		/// <summary>
		/// Additional heat (kW) generated at 100% load.
		/// The effective heat generated is scaled linearly with the current load
		/// </summary>
		[KSPField] public double maxLoadThermalPower;

		/// <summary>
		/// Minimal load ([0;1] factor) as long as the process is starting or running. Determine the minimum consumption of all input resources.
		/// If the draw on output resources result in a lower load (not enough consumers or storage capacity), the output resources will be dumped.
		/// </summary>
		[KSPField] public double minLoad;

		/// <summary>
		/// Additional heat (kW) generated as a function of the process temperature
		/// keys : temperature (K), values : heat generated (kW)
		/// </summary>
		[KSPField] public FloatCurve thermalFeedbackCurve = new FloatCurve();

		/// <summary>
		/// Heat generated as a function of time after process shutdown
		/// keys : time (hours), values : heat generated (kW)
		/// </summary>
		[KSPField] public FloatCurve thermalDecayCurve = new FloatCurve();

		/// <summary>
		/// Config-convenience multiplier applied to all config-defined resource rates : effective rate = rate * powerFactor.
		/// Allow to keep the same resource rate definitions for processes that have the same balance but are more/less powerful
		/// </summary>
		[KSPField] public double powerFactor = 1.0;

		/// <summary> name of the resource that will be consumed until nominalEnergy is reached. Default : `ElectricCharge`</summary>
		[KSPField] public string startupResource = "ElectricCharge";

		/// <summary> startupResource rate. Set it to 0.0 to have the process start by itself whithout requiring any resource. </summary>
		[KSPField] public double startupResourceRate = 1.0;

		/// <summary> kJ per unit of startupResource. default `1.0`</summary>
		[KSPField] public double startupResourceKJ = 1.0;

		/// <summary> name of the coolant resource. Default : `Coolant`</summary>
		[KSPField] public string coolantResource = "Coolant";

		/// <summary> kJ per unit of coolantResourceName. default `1.0`</summary>
		[KSPField] public double coolantResourceKJ = 1.0;

		/// <summary> rad/s when the process is active, starting or while in thermal decay. default `0.0`</summary>
		[KSPField] public double nominalRadiation = 0.0; // Temperature this will trigger a part explosion (K)

		/// <summary> rad/s after meltdown. default `0.0`</summary>
		[KSPField] public double meltdownRadiation = 0.0; // Temperature this will trigger a part explosion (K)

		/// <summary> rad released on explosion. default `0.0`</summary>
		[KSPField] public double explosionRadiation = 0.0; // Temperature this will trigger a part explosion (K)

		/// <summary> name of overheat animation</summary>
		[KSPField] public string overheatAnimationName;

		// internals
		[PersistentField] 

		private float internalResourcesCost;
		private float internalResourcesMass;
		private Animator overheatAnimation;

		//[KSPField(guiActive = true, guiActiveEditor = true, guiName = "Thermal power", guiFormat = "F0", guiUnits = "kW")]
		public double coolingPowerRequested;                     // thermal power produced in excess after passiveCoolingPower hs been applied (kW)

		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "State")]
		public string stateInfo;

		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "Temp")]
		public string thermalInfo;

		[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiName = "Load limit safety"),
		UI_Toggle(scene = UI_Scene.All)]
		public bool loadLimitAutoMode = true;

		[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Load limiter", guiFormat = "P0"),
		UI_FloatRange(minValue = 0f, maxValue = 1f, stepIncrement = 0.01f, scene = UI_Scene.All)]
		public float loadLimit = 1f;

		[KSPField(isPersistant = true, guiName = "Hours since shutdown", guiFormat = "F1", guiUnits = "Hours"),
		UI_FloatRange(minValue = 0f, stepIncrement = 0.1f, scene = UI_Scene.Editor)]
		public float hoursSinceShutdown = float.MaxValue;

		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "Time to depletion")]
		public string resourcesInfo;

		public bool IsInHeatDecay => state != RunningState.Starting && state != RunningState.Running && hoursSinceShutdown < thermalDecayCurve.maxTime;

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

		public override void OnSave(ConfigNode node)
		{
			// TODO : investigate consequences of "startingOutputID == null" (needed because there is a case of OnSave being called before OnStart, don't remember when)
			if (internalResources == null || internalResources.Count == 0 || startingOutputID == null) return;

			foreach (InternalResource intRes in internalResources)
			{
				ConfigNode internalResourcesNode = new ConfigNode("TP_INTERNAL_RESOURCE_STATE");
				intRes.SaveState(internalResourcesNode);
				node.AddNode(internalResourcesNode);
			}
		}

		public override void OnStart(StartState startState)
		{

			overheatAnimation = new Animator(part, overheatAnimationName);

			string processID = Lib.IsFlight() ? processName + "_" + part.flightID : processName + "_" + part.GetInstanceID();
			startingOutputID = "start_" + processID;
			runningOutputID = "nominal_" + processID;
			thermalOutputID = "thermal_" + processID;

			// set load limiter min value
			((UI_FloatRange)Fields["loadLimit"].uiControlEditor).minValue = (float)minLoad;
			((UI_FloatRange)Fields["loadLimit"].uiControlFlight).minValue = (float)minLoad;

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

			if (Lib.IsFlight())
			{
				ThermalOutputReset();
				SetRadiationLevel();
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
						ThermalOutputReset();
						break;

					case RunningState.Running:
						state = RunningState.Stopped;
						ThermalOutputReset();
						hoursSinceShutdown = 0f;
						break;
				}
			}
		}

		public void Update()
		{
			// Update internal resources PAW and get time to depletion
			double timeToDepletion = double.MaxValue;
			foreach (InternalResource intRes in internalResources)
			{
				
				intRes.PAWUpdate(state == RunningState.Stopped);
				if (intRes.type == ResType.input)
				{
					double effectiveRate = intRes.rate * powerFactor * (Lib.IsEditor() ? loadLimit : currentLoad);
					if (effectiveRate > 0.0) 
					{
						double resTimeToDepletion = intRes.amount / effectiveRate;
						if (resTimeToDepletion < timeToDepletion) timeToDepletion = resTimeToDepletion;
					}
				}
			}
			if (timeToDepletion == double.MaxValue) timeToDepletion = 0.0;

			// flight PAW update
			if (Lib.IsFlight())
			{

				overheatAnimation.Still(Lib.Clamp((temperature - nominalTemp) * (1.0 / (meltdownTemp - nominalTemp)), 0.0, 1.0));

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

				if (temperature > nominalTemp + 1.0)
					sb.Append(Lib.Color(Lib.BuildString(temperature.ToString("F0"), " K"), Lib.KColor.Red, true));
				else if (temperature < nominalTemp - 1.0)
					sb.Append(Lib.Color(Lib.BuildString(temperature.ToString("F0"), " K"), Lib.KColor.Yellow, true));
				else
					sb.Append(Lib.Color(Lib.BuildString(temperature.ToString("F0"), " K"), Lib.KColor.Green, true));

				if (temperature < nominalTemp - 1.0)
				{
					sb.Append(" (nominal ");
					sb.Append(nominalTemp.ToString("F0"));
					sb.Append("K)");
				}
				else if (temperature > meltdownTemp + 1.0 && explosionTemp > baseTemp)
				{
					sb.Append(" (explosion ");
					sb.Append(explosionTemp.ToString("F0"));
					sb.Append("K)");
				}
				else if (temperature > nominalTemp + 1.0 && meltdownTemp > baseTemp)
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
				if (coolingPowerRequested > 1.0)
				{
					sb.Append(IsInHeatDecay ? ", decay heat " : ", heat ");
					sb.Append(coolingPowerRequested.ToString("F0"));
					sb.Append(" kW");
				}

				stateInfo = sb.ToString();
			}
			// Editor PAW update
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
						double startResRate = startupResourceRate * powerFactor;
						stateInfo = Lib.BuildString(Lib.Color("Starting", Lib.KColor.Orange, true), ", -", startResRate.ToString("F2"), " ",
							PartResourceLibrary.Instance.GetDefinition(startupResource).abbreviation, "/s for ",
							Lib.HumanReadableDuration(nominalEnergy / (startResRate * startupResourceKJ)));
						resourcesInfo = "none";
						break;
					case RunningState.Running:
						Events["ToggleState"].guiName = "Simulate thermal decay";
						Fields["hoursSinceShutdown"].guiActiveEditor = false;
						double runningCoolant = (thermalFeedbackCurve.Evaluate((float)nominalTemp) + (maxLoadThermalPower * loadLimit)) * coolantResourceKJ; // TODO: fix that
						thermalInfo = Lib.BuildString(nominalTemp.ToString("F0"), " K, need ", runningCoolant.ToString("F0"), " ", coolantResource, "/s");
						stateInfo = Lib.Color("Running", Lib.KColor.Green, true);
						resourcesInfo = Lib.HumanReadableDuration(timeToDepletion);
						break;
					case RunningState.EditorDecay:
						Events["ToggleState"].guiName = "Simulate stopped state";
						((UI_FloatRange)Fields["hoursSinceShutdown"].uiControlEditor).maxValue = thermalDecayCurve.maxTime;
						Fields["hoursSinceShutdown"].guiActiveEditor = true;
						double decayCoolant = 0.0; // Math.Max(nominalThermalPower * thermalDecayCurve.Evaluate(hoursSinceShutdown), 0.0) * coolantResourceKJ;
						thermalInfo = Lib.BuildString("to ", baseTemp.ToString("F0"), " K, need ", decayCoolant.ToString("F0"), " ", coolantResource, "/s");
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

			// update thermal decay time elapsed
			if (state != RunningState.Running && hoursSinceShutdown < thermalDecayCurve.maxTime)
				hoursSinceShutdown += (float)(Kerbalism.elapsed_s / 3600.0);

			VesselResHandler vesselResHandler = ResourceCache.GetVesselHandler(vessel);

			switch (state)
			{
				case RunningState.RequestStart:
					internalResources.ForEach(a => a.transferState = InternalResource.TransferState.none);
					((VirtualResource)vesselResHandler.GetResource(vessel, startingOutputID)).Amount = 0.0;
					if (thermalEnergy >= nominalEnergy)
					{
						ThermalOutputReset();
						state = RunningState.Running;
						RunningUpdate(this, vesselResHandler, temperature, currentLoad);
						break;
					}
					state = RunningState.Starting;
					break;

				case RunningState.Starting:
					if (thermalEnergy >= nominalEnergy)
					{
						// clamp thermalEnergy to avoid immediate meltdown/explosion at high timewarp speeds.
						thermalEnergy = nominalEnergy;
						ThermalOutputReset();
						state = RunningState.Running;
						RunningUpdate(this, vesselResHandler, temperature, currentLoad);
						break;
					}

					StartingUpdate(this, vesselResHandler, thermalEnergy, temperature, ref hoursSinceShutdown);
					break;

				case RunningState.Running:
					RunningUpdate(this, vesselResHandler, temperature, currentLoad);
					break;

				default:
					StoppedUpdate(vesselResHandler);
					break;

			}

			if (temperature > explosionTemp)
			{
				if (part.parent != null) part.parent.AddThermalFlux(thermalEnergy / Kerbalism.elapsed_s) ;
				foreach (Part child in part.children.Where(p => p.parent == part)) child.AddThermalFlux(thermalEnergy / Kerbalism.elapsed_s);
				part.explode();
				if (explosionRadiation > 0.0) API.InjectRadiation(explosionRadiation);
				return;
			}

			if (temperature > meltdownTemp && state != RunningState.Meltdown)
			{
				state = RunningState.Meltdown;
				hoursSinceShutdown = 0f;
				ThermalOutputReset();
				SetRadiationLevel();
			}

			if (temperature > nominalTemp)
			{
				// TODO : transfer some heat to part (to play thermal anim) but don't make it explode
			}
		}

		private static void StartingUpdate(ThermalProcess tp, VesselResHandler vr, double thermalEnergy, double temperature, ref float hoursSinceShutdown)
		{
			tp.ProcessResources(vr);

			// decrease thermal decay time elapsed proportionally to the starting progression.
			// this way thermal decay will occur even if the start process fails.
			// this simulate the process chemical/nuclear reaction "reactivity activation" when warming up
			float startProgress = (float)(thermalEnergy / tp.nominalEnergy);
			float activationHours = tp.thermalDecayCurve.maxTime * (1f - startProgress);
			if (hoursSinceShutdown > activationHours) hoursSinceShutdown = activationHours;

			// we are "activating" the reaction, so we scale down the thermal decay heat generation with the starting progress
			tp.ProcessThermals(vr, tp.thermalFeedbackCurve.Evaluate((float)temperature) + (tp.thermalDecayCurve.Evaluate(hoursSinceShutdown) * startProgress));
		}

		private static void RunningUpdate(ThermalProcess tp, VesselResHandler vr, double temperature, double currentLoad)
		{
			tp.ProcessResources(vr);
			tp.ProcessThermals(vr, tp.thermalFeedbackCurve.Evaluate((float)temperature) + (tp.maxLoadThermalPower * currentLoad));
		}

		private static void StoppedUpdate(ThermalProcess tp, VesselResHandler vesselRes)
		{
			ProcessThermals(vesselRes, thermalDecayCurve.Evaluate(hoursSinceShutdown));
		}

		private static void ThermalOutputReset(Vessel v, string thermalOutputID)
		{
			VirtualResource thermalOutput = (VirtualResource)ResourceCache.GetResource(v, thermalOutputID);
			thermalOutput.Capacity = 0.0;
			thermalOutput.Amount = 0.0;
		}

		/// <summary> update thermal state </summary>
		private static void ProcessThermals(
			ThermalProcess tp, Vessel v, VesselResHandler vesselRes,				// refs
			double elapsedSec, double thermalPowerAdded,							// method parameters
			string thermalOutputID, string startingOutputID, RunningState state,	// persisted values (read)
			ref double thermalEnergy,                                               // persisted values (read/write)
			out double temperature,                                                 // persisted values (write)
			out double coolingPowerRequested)                                       // loaded UI only (?)
		{
			// get thermal energy virtual resource
			VirtualResource thermalOutput = (VirtualResource)vesselRes.GetResource(v, thermalOutputID);

			// update stored heat based on removed amount (capacity was set at the energy level accounting for last step heat production)
			// don't do it when the virtual resource was just created
			if (thermalOutput.Capacity > 0.0)
				thermalEnergy = thermalOutput.Capacity - thermalOutput.Amount;

			// reset virtual resource amount
			thermalOutput.Amount = 0.0;


			if (state == RunningState.Starting)
			{
				VirtualResource startResourceOutput = (VirtualResource)vesselRes.GetResource(v, startingOutputID);
				thermalEnergy += startResourceOutput.Amount;

				startResourceOutput.Amount = 0.0;

				double heatNeeded = Math.Min(tp.nominalEnergy - thermalEnergy, tp.startupResourceRate * tp.startupResourceKJ * tp.powerFactor * elapsedSec);
				Recipe startRecipe = new Recipe(tp.processName);
				startRecipe.AddInput(tp.startupResource, heatNeeded);
				startRecipe.AddOutput(startingOutputID, heatNeeded, false);
				vesselRes.AddRecipe(startRecipe);
			}

			temperature = tp.baseTemp + (thermalEnergy * KelvinPerKJ(tp));

			// calculate thermal power (kW) dissipation needs based on the process power specs
			coolingPowerRequested = Math.Max(thermalPowerAdded, 0.0);
			// scale it by time elapsed to get energy (kJ), add it to the currently stored energy and save the value in the virtual resource capacity
			// this allow to keep track of the added/removed energy between updates without having to use a persisted variable for that.

			thermalOutput.Capacity = (coolingPowerRequested * elapsedSec) + thermalEnergy;

			// when running or starting, we want to keep the process at the nominal energy (and temperature)
			double energyToRemove = thermalOutput.Capacity;
			if (state == RunningState.Running || state == RunningState.Starting)
				energyToRemove -= tp.nominalEnergy;

			// do nothing is no cooling is requested
			if (energyToRemove > 0.0)
			{
				energyToRemove *= tp.coolantResourceKJ;
				// create coolant recipe
				Recipe coolantRecipe = new Recipe(tp.processName);
				coolantRecipe.AddInput(tp.coolantResource, energyToRemove);
				coolantRecipe.AddOutput(thermalOutputID, energyToRemove, false);
				vesselRes.AddRecipe(coolantRecipe);
			}
		}

		private static void ProcessResources(
			ThermalProcess tp, Vessel v, VesselResHandler vesselRes,
			List<InternalResource> internalResources, List<Resource> resources,
			double elapsedSec, double thermalEnergy,
			string runningOutputID, bool loadLimitAutoMode, double loadLimit, RunningState state,
			out double currentLoad)
		{
			// synchronize internal resources amounts after consumption/production in last resource sim step
			foreach (InternalResource intRes in internalResources)
				intRes.amount = (float)vesselRes.GetResource(v, intRes.virtualResID).Amount;

			// get outputs results from last resource sim step
			VirtualResource nominalOutput = (VirtualResource)vesselRes.GetResource(v, runningOutputID);

			if (loadLimitAutoMode && loadLimit > tp.minLoad && thermalEnergy > 0.0) loadLimit = (float)Math.Max(loadLimit * Math.Min(tp.nominalEnergy / thermalEnergy, 1.0), tp.minLoad);

			currentLoad = nominalOutput.Amount * loadLimit;
			nominalOutput.Amount = 0.0;

			double minLoadInputRate = currentLoad < tp.minLoad ? tp.minLoad - currentLoad : 0.0;

			// create input/output recipe
			Recipe recipe = new Recipe(tp.processName);
			double rateFactor = loadLimit * tp.powerFactor * elapsedSec;
			if (state == RunningState.Starting) rateFactor *= thermalEnergy / tp.nominalEnergy;
			foreach (InternalResource intRes in internalResources)
			{
				if (state == RunningState.Starting && !intRes.useWhileStarting) continue;

				switch (intRes.type)
				{
					case ResType.input:
						recipe.AddInput(intRes.virtualResID, intRes.rate * rateFactor);
						if (minLoadInputRate > 0.0) vesselRes.Consume(v, intRes.virtualResID, intRes.rate * minLoadInputRate * elapsedSec, tp.processName);
						break;
					case ResType.output:
						recipe.AddOutput(intRes.virtualResID, intRes.rate * rateFactor, false);
						break;
				}
			}
			foreach (Resource extraRes in resources)
			{
				if (state == RunningState.Starting && !extraRes.useWhileStarting) continue;

				switch (extraRes.type)
				{
					case ResType.input:
						recipe.AddInput(extraRes.name, extraRes.rate * rateFactor);
						if (minLoadInputRate > 0.0) vesselRes.Consume(v, extraRes.name, extraRes.rate * minLoadInputRate * elapsedSec, tp.processName);
						break;
					case ResType.output:
						recipe.AddOutput(extraRes.name, extraRes.rate * rateFactor, extraRes.dump);
						break;
				}
			}
			// virtual resource used to check output level
			recipe.AddOutput(runningOutputID, 1.0, false);
			// register recipe
			vesselRes.AddRecipe(recipe);
		}

		private void SetRadiationLevel()
		{
			Emitter em = part.FindModuleImplementing<Emitter>();
			if (em != null) em.radiation = state == RunningState.Meltdown ? meltdownRadiation : nominalRadiation;
		}

		private static double KelvinPerKJ(ThermalProcess tp) => (tp.nominalTemp - tp.baseTemp) / tp.nominalEnergy;


		public float GetModuleCost(float defaultCost, ModifierStagingSituation sit) => internalResourcesCost;
		public ModifierChangeWhen GetModuleCostChangeWhen() => ModifierChangeWhen.CONSTANTLY;

		public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) => internalResourcesMass;
		public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.CONSTANTLY;

		public void BackgroundUpdate(Vessel v, PartData pd, ThermalProcessData md, ThermalProcess prefab, double elapsedSec)
		{
			throw new NotImplementedException();
		}

		#region Resource classes

		public enum ResType { none = 0, input, output }

		/// <summary> "normal" resource consumed or produced by the thermal process </summary>
		public class Resource
		{
			/// <summary> resource name, must match a stock defined resource</summary>
			public string name;
			/// <summary> is the resource an input or output </summary>
			public ResType type;
			/// <summary> rate of consumption/production </summary>
			public double rate;
			/// <summary> should a produced resource be dumped if no storage available </summary>
			public bool dump;
			/// <summary> is the resource produced or consumed while the process is starting (default false) </summary>
			public bool useWhileStarting;


			/// <summary> ctor for config loading, meant to be called only on the prefab </summary>
			public Resource(ConfigNode node)
			{
				name = Lib.ConfigValue(node, "name", string.Empty);
				type = Lib.ConfigEnum(node, "type", ResType.input);
				rate = Lib.ConfigValue(node, "rate", 0.0);
				dump = Lib.ConfigValue(node, "dump", true);
				useWhileStarting = Lib.ConfigValue(node, "useWhileStarting", false);
			}
		}

		/// <summary> resource stored inside the module and that can require special condtions to be loaded/unloaded (ex : EnrichedUranium, DepletedFuel) </summary>
		[PersistentIListData(createInstance = false, instanceIdentifierField = "name")]
		public class InternalResource
		{
			// config definitions
			/// <summary> resource name, must match a stock defined resource</summary>
			public string name;
			/// <summary> is the resource an input or output </summary>
			public ResType type;
			/// <summary> rate of consumption/production </summary>
			public double rate;
			/// <summary> is the resource produced or consumed while the process is starting (default true) </summary>
			public bool useWhileStarting;
			/// <summary> transfer rate when loading/unloading the resource </summary>
			public double transferRate;
			/// <summary> current stored amount, persisted </summary>
			[PersistentField] public float amount;
			/// <summary> internal storage capacity </summary>
			public float maxAmount;
			/// <summary> default true, is vessel to internal transfer available in flight ? </summary>
			public bool loadingEnabled;
			/// <summary> default true, is internal to vessel transfer available in flight ? </summary>
			public bool unloadingEnabled;
			/// <summary> default false, crew requirements for loading availability </summary>
			public CrewSpecs loadingReqs;
			/// <summary> default false, crew requirements for unloading availability </summary>
			public CrewSpecs unloadingReqs;

			// stock resource derived data
			private PartResourceDefinition stockDefinition;
			public float MassPerUnit => stockDefinition.density;
			public float CostPerUnit => stockDefinition.unitCost;

			// internal variables
			public enum TransferState { none, loading, unloading }
			[PersistentField] public TransferState transferState = TransferState.none;
			[PersistentField] public string virtualResID;
			private ThermalProcess module;

			// PAW UI
			private BaseEvent pawEvent;
			private BaseField pawSlider;
			private UI_FloatRange sliderControl;

			/// <summary> ctor for config loading, meant to be called only on the prefab </summary>

			public InternalResource(ThermalProcess module, ConfigNode node)
			{
				this.module = module;
				name = Lib.ConfigValue(node, "name", string.Empty);
				type = Lib.ConfigEnum(node, "type", ResType.input);
				rate = Lib.ConfigValue(node, "rate", 0.0);
				useWhileStarting = Lib.ConfigValue(node, "useWhileStarting", true);
				transferRate = Lib.ConfigValue(node, "transferRate", 0.0);
				amount = Lib.ConfigValue(node, "amount", 0f);
				maxAmount = Lib.ConfigValue(node, "maxAmount", float.MaxValue);
				loadingEnabled = Lib.ConfigValue(node, "loadingEnabled", true);
				unloadingEnabled = Lib.ConfigValue(node, "unloadingEnabled", true);
				loadingReqs = new CrewSpecs(Lib.ConfigValue(node, "loadingReqs", "false"));
				unloadingReqs = new CrewSpecs(Lib.ConfigValue(node, "unloadingReqs", "false"));

				try { stockDefinition = PartResourceLibrary.Instance.GetDefinition(name); }
				catch (Exception)
				{
					Lib.Log("ERROR : ThermalProcess '" + module.processName + "' on part '" + module.part.partName + "' has '" + name + "' internal resource defined but that resource doesn't exist.");
					throw;
				}
			}

			/// <summary> load resource state from the provided node </summary>
			public void LoadState(ConfigNode node)
			{
				name = Lib.ConfigValue(node, "name", string.Empty);
				amount = Lib.ConfigValue(node, "amount", 0f);
				virtualResID = Lib.ConfigValue(node, "virtualResID", string.Empty);
				transferState = Lib.ConfigValue(node, "transferState", TransferState.none);
			}

			/// <summary> save resource state in the provided node </summary>
			public void SaveState(ConfigNode node)
			{
				node.AddValue("name", name);
				node.AddValue("amount", amount);
				node.AddValue("virtualResID", virtualResID);
				node.AddValue("transferState", transferState);
			}

			/// <summary> PAW amount slider and transfer button creation, to be called in OnStart() </summary>
#if !KSP15_16
			// Note that this will fail on KSP 1.7.0 as PAW groups is a feature that was added in 1.7.1
			public void PAWInit(ThermalProcess module, BasePAWGroup resGroup)
#else
			public void PAWInit(ThermalProcess module)
#endif
			{
				// transfer button
				pawEvent = new BaseEvent(module.Events, name, new BaseEventDelegate(TransferPAWEvent), new KSPEvent());

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

			/// <summary> PAW UI update, to be called from Update()</summary>
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
					if (Lib.KSPVersion >= new Version(1, 7, 3))
						actionItem.inputField.interactable = false;
#endif
				}
			}

			/// <summary> Check transfer conditions and show resource transfer popup</summary>
			private void TransferPAWEvent()
			{
				List<DialogGUIButton> buttons = new List<DialogGUIButton>();
				string info = Lib.Color(Lib.BuildString("Transfer rate : ", transferRate.ToString("F3"), "/s"), Lib.KColor.Yellow, true);
				if (!loadingEnabled) info = Lib.BuildString(info, "\n", Lib.Color("Loading unavailable for this resource", Lib.KColor.Orange, true));
				else if (loadingReqs.enabled && !loadingReqs.Check()) info = Lib.BuildString(info, "\n", "Loading unavailable : ", Lib.Color(loadingReqs.Warning(), Lib.KColor.Orange, true));
				else buttons.Add(new DialogGUIButton("Load from vessel", () => transferState = TransferState.loading));
				if (!unloadingEnabled) info = Lib.BuildString(info, "\n", Lib.Color("Unloading unavailable for this resource", Lib.KColor.Orange, true));
				else if (unloadingReqs.enabled && !unloadingReqs.Check()) info = Lib.BuildString(info, "\n", "Unloading unavailable : ", Lib.Color(unloadingReqs.Warning(), Lib.KColor.Orange, true));
				else buttons.Add(new DialogGUIButton("Unload to vessel", () => transferState = TransferState.unloading));

				buttons.Add(new DialogGUIButton("cancel", null));

				Lib.Popup(name + " internal storage", info, buttons.ToArray());
			}

			/// <summary> transfer the resource from the vessel to the internal storage </summary>
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

			/// <summary> transfer the resource to the vessel from the internal storage </summary>
			public void UnloadToVessel(Vessel v, double elapsedSec)
			{
				VesselResHandler resHandler = ResourceCache.GetVesselHandler(v);
				IResource res = resHandler.GetResource(v, name);
				if (res.Level == 1.0)
				{
					transferState = TransferState.none;
					Message.Post("No storage available on " + v.GetDisplayName() + " for " + name + "\nStopping transfer from " + module.processName);
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
		}

		public class ThermalProcessData : PartModuleData
		{
			[PersistentField] private double thermalEnergy;                     // thermal energy accumulated (kJ)
			[PersistentField] private double currentLoad;
			[PersistentField] private double temperature;

			[PersistentField] public string startingOutputID;
			[PersistentField] public string runningOutputID;
			[PersistentField] public string thermalOutputID;
			[PersistentField] public RunningState state;

			[PersistentField] public bool loadLimitAutoMode = true;

			[PersistentField] public float loadLimit = 1f;

			[PersistentField] public float hoursSinceShutdown = float.MaxValue;

			[PersistentField] public List<Resource> resources;                      // inputs/outputs
			[PersistentField] public List<InternalResource> internalResources;       // non-removable inputs/outputs (simulating no-flow)
		}


		#endregion
	}






}
