using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace KERBALISM
{
	/// <summary>
	/// Offline thermal simulation for SystemHeat loops on unloaded vessels.
	///
	/// Ground truth is what SystemHeat itself persists on ModuleSystemHeat when the vessel leaves the loaded
	/// state : currentLoopTemperature, nominalLoopTemperature and the per-module totalSystemFlux (whose sum is
	/// the live net loop flux). The proto reconstruction of producers and radiators is only trusted for
	/// *changes* : on the first background step after unload, the difference between the observed net flux and
	/// the reconstructed one is stored per loop as a residual (see <see cref="SystemHeatLoopCalibration"/>) and
	/// re-applied on every later step. Anything the reconstruction can't see (unpatched radiators, convection,
	/// third-party heat modules...) is therefore preserved instead of being mistaken for missing cooling.
	///
	/// Loop semantics follow HeatLoop.SimulateIteration : a loop with active heat producers never drops below
	/// its nominal temperature, only heats up while the net flux is positive, and cools back toward nominal /
	/// ambient otherwise. Integration is closed-form (exponential approach to the equilibrium temperature) with
	/// producer shutdown thresholds handled as events, so a step costs O(events) instead of O(elapsed / 10 s).
	/// Core damage is rate-only, like SystemHeat's own reactors : the background never writes an instant meltdown.
	/// </summary>
	public static class SystemHeatBackgroundThermal
	{
		private static readonly Dictionary<Guid, double> lastRunTime = new Dictionary<Guid, double>();

		private static readonly string[] FusionReactorModuleNames = { "FusionReactor", "ModuleFusionEngine" };
		private static readonly string[] ProcessReliabilityTypes = { "ProcessControllerSystemHeat", "ProcessController" };
		private static readonly string[] NativeFissionReliabilityTypes = { "ModuleSystemHeatFissionReactor", "ModuleSystemHeatFissionEngine" };
		private static readonly string[] RadiatorReliabilityTypes = { "SystemHeatRadiatorKerbalism", "ModuleSystemHeatRadiator", "ModuleActiveRadiator", "USRadiatorSwitch" };

		internal static bool Enabled = true;
		private static bool? systemHeatInstalled;
		internal static bool Active
		{
			get
			{
				if (!Enabled)
					return false;
				if (!systemHeatInstalled.HasValue)
					systemHeatInstalled = SystemHeat.Installed;
				return systemHeatInstalled.Value;
			}
		}
		internal static float RadiatorCoefficient = 1f;

		private const float FluxEpsilonKw = 0.01f;
		/// <summary>SystemHeatSettings.HeatLoopDecayCoefficient : passive decay toward nominal of a loop without any flux.</summary>
		private const float HeatLoopDecayCoefficient = 0.15f;
		/// <summary>Hard floor for loop temperature integration (space baseline), not ambient environment.</summary>
		private const float MinimumLoopTemperatureK = 4f;
		private const float MaximumLoopTemperatureK = 5000f;
		/// <summary>Stock SystemHeat radiator patches reach their rated rejection at 400 K.</summary>
		private const float StockRadiatorRatedTemperatureK = 400f;
		private const float TemperatureToleranceK = 0.05f;
		/// <summary>ModuleSystemHeatFissionReactor.CoreDamageRate default : integrity lost per K of exceedance per second.</summary>
		private const float DefaultNativeCoreDamageRate = 0.005f;
		private const int MaxThermalEventsPerStep = 16;
		private const int DamageSamplesPerSegment = 16;
		/// <summary>
		/// Written on every ModuleSystemHeat snapshot the background touches. It is not a KSPField, so the next
		/// Vessel.Unload() / save rebuilt from the live module drops it : its presence means the persisted loop
		/// state is ours, not SystemHeat's, and must not be used to calibrate a loop.
		/// </summary>
		private const string BackgroundSimulatedField = "kerbalismBackgroundSimulated";

		#region loaded-side capture

		/// <summary>
		/// Sync fission ProcessController fields into proto. Call at pack / rails / scene / save boundaries — not
		/// every FixedUpdate. ModuleSystemHeat is left alone : SystemHeat persists the loop state itself, and
		/// Vessel.Unload() / saves rebuild the snapshot from the live module anyway.
		/// </summary>
		public static void CaptureLoadedTemperatures(Vessel v)
		{
			if (!Active || v == null || !v.loaded || v.parts == null)
				return;

			for (int p = 0; p < v.parts.Count; p++)
			{
				Part part = v.parts[p];
				if (part == null || part.protoPartSnapshot == null || part.Modules == null)
					continue;

				for (int i = 0; i < part.Modules.Count; i++)
				{
					PartModule module = part.Modules[i];
					if (module != null && module.moduleName == "ProcessControllerSystemHeat")
						CaptureLoadedFissionReactorState(part, module as ProcessControllerSystemHeat);
				}
			}
		}

		/// <summary>
		/// Sync NFE fission ProcessController state into proto before the vessel packs so background
		/// automation and Profile modifiers see the same running flag as the loaded part.
		/// </summary>
		public static void CaptureLoadedFissionReactorState(Part part)
		{
			if (!Active || part == null || part.protoPartSnapshot == null || part.Modules == null)
				return;

			for (int i = 0; i < part.Modules.Count; i++)
			{
				PartModule module = part.Modules[i];
				if (module == null || module.moduleName != "ProcessControllerSystemHeat")
					continue;

				CaptureLoadedFissionReactorState(part, module as ProcessControllerSystemHeat);
			}
		}

		private static void CaptureLoadedFissionReactorState(Part part, ProcessControllerSystemHeat process)
		{
			if (process == null || !process.IsFissionReactor())
				return;

			ProtoPartModuleSnapshot protoModule = GetLoadedModuleSnapshot(process, part.protoPartSnapshot)
				?? FindMatchingProcessModuleSnapshot(part.protoPartSnapshot, process.resource);
			if (protoModule == null)
				return;

			Lib.Proto.Set(protoModule, nameof(ProcessController.running), process.running);
			Lib.Proto.Set(protoModule, nameof(ProcessController.broken), process.broken);
			Lib.Proto.Set(protoModule, nameof(ProcessControllerSystemHeat.CurrentPowerPercent), process.CurrentPowerPercent);
			Lib.Proto.Set(protoModule, nameof(ProcessControllerSystemHeat.CoreDamage), process.CoreDamage);

			if (string.IsNullOrEmpty(process.resource) || !part.Resources.Contains(process.resource))
				return;

			PartResource pseudo = part.Resources[process.resource];
			ProtoPartResourceSnapshot protoResource = FindPartResource(part.protoPartSnapshot, process.resource);
			if (protoResource == null)
				return;

			protoResource.flowState = pseudo.flowState;
			protoResource.amount = pseudo.amount;
			protoResource.maxAmount = pseudo.maxAmount;
		}

		/// <summary>Sync loaded fission proto for every loaded vessel (scene leave, pause, save).</summary>
		public static void CaptureAllLoadedFissionReactors()
		{
			if (!Active || !HighLogic.LoadedSceneIsFlight)
				return;

			if (FlightGlobals.Vessels == null)
				return;

			foreach (Vessel v in FlightGlobals.Vessels)
			{
				if (v == null || !v.loaded)
					continue;

				CaptureLoadedTemperatures(v);
			}
		}

		/// <summary>The vessel is live again : SystemHeat owns the loops, drop the background calibration.</summary>
		public static void OnVesselLoaded(Vessel v)
		{
			if (!Active || v == null)
				return;

			VesselData vd = v.KerbalismData();
			if (vd.systemHeatLoops != null && vd.systemHeatLoops.Count > 0)
				vd.systemHeatLoops.Clear();
		}

		/// <summary>
		/// After loading a vessel, make sure the live loop has a usable temperature. SystemHeat restores
		/// currentLoopTemperature itself; this only covers loops that were never simulated (0 K).
		/// </summary>
		public static void RestoreLoadedFissionLoopTemperature(Part part, PartModule heatModule)
		{
			if (!Active || part == null || heatModule == null)
				return;

			if (SystemHeat.CurrentLoopTemperature(heatModule, 0f) > 0f)
				return;

			ProtoPartModuleSnapshot protoHeat = part.protoPartSnapshot != null
				? GetLoadedModuleSnapshot(heatModule, part.protoPartSnapshot)
				: null;
			float temp = protoHeat != null ? Lib.Proto.GetFloat(protoHeat, "currentLoopTemperature") : 0f;
			if (temp <= 0f)
				temp = GetFallbackLoopTemperature();

			SystemHeat.Set(heatModule, "currentLoopTemperature", temp);
			if (protoHeat != null)
				Lib.Proto.Set(protoHeat, "currentLoopTemperature", temp);
		}

		private static ProtoPartModuleSnapshot GetLoadedModuleSnapshot(PartModule module, ProtoPartSnapshot protoPart)
		{
			if (module == null)
				return null;

			if (module.snapshot != null)
				return module.snapshot;

			return FindMatchingLoadedHeatModuleSnapshot(protoPart, module);
		}

		private static ProtoPartModuleSnapshot FindMatchingLoadedHeatModuleSnapshot(ProtoPartSnapshot protoPart, PartModule module)
		{
			if (protoPart == null || module == null || protoPart.modules == null)
				return null;

			string moduleId = SystemHeat.GetModuleId(module);
			ProtoPartModuleSnapshot fallback = null;

			foreach (ProtoPartModuleSnapshot protoModule in protoPart.modules)
			{
				if (protoModule.moduleName != module.moduleName)
					continue;

				if (fallback == null)
					fallback = protoModule;

				string protoModuleId = Lib.Proto.GetString(protoModule, "moduleID");
				if (string.IsNullOrEmpty(moduleId) || protoModuleId == moduleId)
					return protoModule;
			}

			return fallback;
		}

		#endregion

		#region frozen fission pseudo-resource sync

		/// <summary>
		/// Refresh frozen fission reactor pseudo-resources before Profile rules run on unloaded vessels.
		/// No-ops when TryRun already simulated this vessel this UT; SimulateVessel writes
		/// the same capacities after the loop step.
		/// </summary>
		public static void PrepareFrozenFissionReactors(Vessel v, double elapsed_s)
		{
			if (!Active || v == null || v.loaded || elapsed_s <= 0f)
				return;

			if (lastRunTime.TryGetValue(v.id, out double last) && last == Planetarium.GetUniversalTime())
				return;

			SyncAllFrozenFissionReactors(v, elapsed_s);
		}

		private static void SyncAllFrozenFissionReactors(Vessel v, double elapsed_s)
		{
			if (v.protoVessel == null)
				return;

			foreach (ProtoPartSnapshot part in v.protoVessel.protoPartSnapshots)
			{
				Part prefab = PartLoader.getPartInfoByName(part.partName).partPrefab;

				foreach (ProtoPartModuleSnapshot module in part.modules)
				{
					if (module.moduleName != "ProcessControllerSystemHeat")
						continue;

					PartModule processPrefab = FindMatchingPrefabModule(prefab, module, "ProcessControllerSystemHeat");
					if (!IsFissionProcessController(prefab, module, processPrefab))
						continue;

					SyncFrozenProcessReactor(v, part, module, processPrefab, prefab, elapsed_s);
				}
			}
		}

		private static void SyncFrozenFissionReactorsFromLoops(Vessel v, Dictionary<int, LoopState> loops, float elapsed_s)
		{
			foreach (LoopState loop in loops.Values)
			{
				List<HeatProducer> producers = loop.heatProducers;
				for (int i = 0; i < producers.Count; i++)
				{
					HeatProducer producer = producers[i];
					if (!producer.isFissionProcess || producer.part == null || producer.module == null)
						continue;
					if (producer.module.moduleName != "ProcessControllerSystemHeat")
						continue;

					Part prefab = PartLoader.getPartInfoByName(producer.part.partName).partPrefab;
					PartModule processPrefab = FindMatchingPrefabModule(prefab, producer.module, "ProcessControllerSystemHeat");
					SyncFrozenProcessReactor(v, producer.part, producer.module, processPrefab, prefab, elapsed_s, false);
				}
			}
		}

		public static void TryRun(Vessel v, double elapsed_s)
		{
			if (!Active || v == null || elapsed_s <= 0.0 || v.loaded)
				return;

			double now = Planetarium.GetUniversalTime();
			if (lastRunTime.TryGetValue(v.id, out double last) && last == now)
				return;
			lastRunTime[v.id] = now;

			SimulateVessel(v, (float)elapsed_s);
		}

		public static void SyncFrozenProcessReactor(Vessel v, ProtoPartSnapshot part, ProtoPartModuleSnapshot module, PartModule processPrefab, Part partPrefab, double elapsed_s)
		{
			SyncFrozenProcessReactor(v, part, module, processPrefab, partPrefab, elapsed_s, true);
		}

		private static void SyncFrozenProcessReactor(Vessel v, ProtoPartSnapshot part, ProtoPartModuleSnapshot module, PartModule processPrefab, Part partPrefab, double elapsed_s, bool ensureSimulated)
		{
			if (!Active || v == null || part == null || module == null || partPrefab == null || v.loaded)
				return;

			if (!IsFissionProcessController(partPrefab, module, processPrefab))
				return;

			string resource = Lib.Proto.GetString(module, "resource");
			if (string.IsNullOrEmpty(resource))
				return;

			ProtoPartResourceSnapshot pseudoResource = FindPartResource(part, resource);
			if (pseudoResource == null)
				return;

			if (Lib.Proto.GetBool(module, "broken"))
			{
				pseudoResource.flowState = false;
				return;
			}

			if (ensureSimulated)
				EnsureUnloadedFissionLoopSimulated(v, (float)elapsed_s);

			ProtoPartModuleSnapshot heatModule = GetLinkedHeatModule(part, partPrefab, Lib.Proto.GetString(module, "systemHeatModuleID"));
			float loopTemperature = heatModule != null ? Lib.Proto.GetFloat(heatModule, "currentLoopTemperature") : GetFallbackLoopTemperature();
			if (loopTemperature <= 0f)
				loopTemperature = GetFallbackLoopTemperature();

			if (Lib.Proto.GetBool(module, "broken"))
			{
				pseudoResource.flowState = false;
				return;
			}

			bool running = Lib.Proto.GetBool(module, "running");

			if (!running)
			{
				ClearFrozenFissionPseudoResource(pseudoResource);
				return;
			}

			SyncFrozenFissionPowerPercent(module, processPrefab);

			float capacity = IntegrationReflection.GetFloat(processPrefab, "capacity", Lib.Proto.GetFloat(module, "capacity"));
			float heatPower = GetProcessHeatPower(part, partPrefab, module, processPrefab);
			float throttle = GetProcessThrottle(module);
			FloatCurve efficiencyCurve = IntegrationReflection.GetField<FloatCurve>(processPrefab, "systemEfficiency");
			double thermalEff = SystemHeatEditorSimulation.CalculateProcessEfficiency(efficiencyCurve, loopTemperature, heatPower, false);
			double desiredCapacity = Math.Max(0.0, capacity * thermalEff * throttle);

			double threshold = Math.Max(capacity, 1.0f) * SystemHeatEditorSimulation.HystFrac;
			if (Math.Abs(pseudoResource.amount - desiredCapacity) > threshold || Math.Abs(pseudoResource.maxAmount - desiredCapacity) > threshold)
			{
				pseudoResource.amount = desiredCapacity;
				pseudoResource.maxAmount = desiredCapacity;
			}
			pseudoResource.flowState = desiredCapacity > 0.0;
		}

		private static void SyncFrozenFissionPowerPercent(ProtoPartModuleSnapshot module, PartModule processPrefab)
		{
			float minThrottle = processPrefab != null
				? IntegrationReflection.GetFloat(processPrefab, "MinimumThrottle", 10f)
				: 10f;
			float power = Lib.Proto.GetFloat(module, "CurrentPowerPercent", 0f);
			if (power < minThrottle)
				Lib.Proto.Set(module, "CurrentPowerPercent", minThrottle);
			else if (power > 100f)
				Lib.Proto.Set(module, "CurrentPowerPercent", 100f);
		}

		#endregion

		#region simulation state

		private class LoopState
		{
			internal float volume;
			internal string coolantName;
			/// <summary>Loop temperature at the start of the step (persisted by SystemHeat, or by the previous background step).</summary>
			internal float temperature;
			/// <summary>currentLoopTemperature found in proto. 0 = the loop was never simulated live.</summary>
			internal float observedTemperature;
			/// <summary>Sum of the persisted per-module totalSystemFlux : the live net loop flux at unload.</summary>
			internal float observedNetFluxKw;
			/// <summary>The persisted loop state was already rewritten by a background step (calibration lost with VesselData).</summary>
			internal bool observationStale;
			/// <summary>nominalLoopTemperature persisted by SystemHeat.</summary>
			internal float persistedNominalTemperature;
			internal float nominalWeightedSum;
			internal float nominalWeight;
			/// <summary>Heat from sources without a shutdown threshold (cryo coolers, FFT fusion).</summary>
			internal float constantFluxKw;
			/// <summary>Heat from the currently active producers.</summary>
			internal float producerFluxKw;
			internal float heatSinkFluxOffsetKw;
			internal float netFluxKw;
			internal bool hasActiveProducer;
			internal SystemHeatLoopCalibration calibration;
			internal readonly List<ProtoPartModuleSnapshot> heatModules = new List<ProtoPartModuleSnapshot>();
			internal readonly List<HeatProducer> heatProducers = new List<HeatProducer>();
			internal readonly List<HeatSink> heatSinks = new List<HeatSink>();
			internal readonly List<RadiatorRejector> radiators = new List<RadiatorRejector>();
		}

		/// <summary>Rejection model of one radiator, resolved once per step (prefab lookups and curve reference cached).</summary>
		private class RadiatorRejector
		{
			internal FloatCurve curve;
			internal float scaleFactor = 1f;
			internal float constantPowerKw;
			/// <summary>Stock-style transfer power : ramps linearly to the rated value at 400 K.</summary>
			internal bool linearToRated;

			internal float Evaluate(float loopTemperature)
			{
				if (curve != null)
					return Mathf.Max(0f, curve.Evaluate(loopTemperature)) * scaleFactor;
				if (linearToRated)
					return constantPowerKw * Mathf.Clamp01(loopTemperature / StockRadiatorRatedTemperatureK) * scaleFactor;
				return constantPowerKw * scaleFactor;
			}
		}

		private class HeatProducer
		{
			internal ProtoPartSnapshot part;
			internal ProtoPartModuleSnapshot module;
			internal float powerKw;
			internal bool active;
			internal float outletTemperature;
			/// <summary>Heat module coolant volume : SystemHeat weights the nominal temperature by it.</summary>
			internal float nominalWeight = 1f;
			internal float shutdownTemperature = float.MaxValue;
			internal bool autoShutdown = true;
			internal float meltdownTemperature;
			internal float maximumTemperature;
			internal float coreDamageRate;
			internal FloatCurve coreDamageCurve;
			internal bool isFissionProcess;
			internal bool isNativeFission;
			/// <summary>Damage accumulated during this step : CoreDamage % for process reactors, CoreIntegrity % for native ones.</summary>
			internal float pendingDamage;
		}

		private class HeatSink
		{
			internal ProtoPartSnapshot part;
			internal ProtoPartModuleSnapshot module;
			internal PartModule prefab;
		}

		/// <summary>A producer / radiator resolved to its ModuleSystemHeat : proto snapshot, prefab module, loop id and volume.</summary>
		private struct HeatLink
		{
			internal ProtoPartModuleSnapshot snapshot;
			internal PartModule prefabModule;
			internal int loopId;
			internal float volume;
		}

		#endregion

		#region vessel step

		private static void SimulateVessel(Vessel v, float elapsed_s)
		{
			var loops = new Dictionary<int, LoopState>();

			foreach (ProtoPartSnapshot part in v.protoVessel.protoPartSnapshots)
			{
				Part prefab = PartLoader.getPartInfoByName(part.partName).partPrefab;
				int heatModuleOrdinal = 0;

				foreach (ProtoPartModuleSnapshot module in part.modules)
				{
					if (module.moduleName == "ModuleSystemHeat")
					{
						// moduleID is not persisted : snapshots and prefab modules of the same type share their ordering.
						PartModule heatPrefab = GetPrefabHeatModuleAt(prefab, heatModuleOrdinal++);
						LoopState loop = EnsureLoop(loops, Lib.Proto.GetInt(module, "currentLoopID"));
						loop.volume += GetHeatModuleVolume(heatPrefab);
						if (loop.coolantName == null)
							loop.coolantName = IntegrationReflection.GetString(heatPrefab, "coolantName", "default");

						float loopTemp = Lib.Proto.GetFloat(module, "currentLoopTemperature");
						if (loopTemp > 0f)
						{
							loop.temperature = loopTemp;
							loop.observedTemperature = loopTemp;
						}
						loop.persistedNominalTemperature = Mathf.Max(loop.persistedNominalTemperature, Lib.Proto.GetFloat(module, "nominalLoopTemperature"));
						loop.observedNetFluxKw += Lib.Proto.GetFloat(module, "totalSystemFlux");
						loop.observationStale |= Lib.Proto.GetBool(module, BackgroundSimulatedField, false);
						loop.heatModules.Add(module);
					}
					else if (module.moduleName == "ProcessControllerSystemHeat")
					{
						PartModule processPrefab = FindMatchingPrefabModule(prefab, module, "ProcessControllerSystemHeat");
						if (!TryResolveHeatLink(part, prefab, Lib.Proto.GetString(module, "systemHeatModuleID"), out HeatLink link))
							continue;

						float meltdown = GetProcessField(prefab, module, "meltdownTemperature", 0f);
						float maximum = GetProcessField(prefab, module, "MaximumTemperature", 0f);
						bool isFissionProcess = ProcessControllerSystemHeat.HasCoreDamageConfig(meltdown, maximum);

						LoopState loop = EnsureLoop(loops, link.loopId);
						HeatProducer producer = new HeatProducer
						{
							part = part,
							module = module,
							shutdownTemperature = isFissionProcess
								? GetFissionSafetyOverride(prefab, module, processPrefab)
								: GetProcessField(prefab, module, "shutdownTemperature", float.MaxValue),
							autoShutdown = processPrefab == null || IntegrationReflection.GetBool(processPrefab, "AutoShutdown", true),
							meltdownTemperature = isFissionProcess ? meltdown : 0f,
							maximumTemperature = maximum > 0f ? maximum : 2000f,
							coreDamageRate = GetProcessField(prefab, module, "CoreDamageRate", 0f),
							coreDamageCurve = IntegrationReflection.GetField<FloatCurve>(processPrefab, "coreDamageCurve"),
							isFissionProcess = isFissionProcess
						};
						loop.heatProducers.Add(producer);

						if (!IsProcessOperational(part, prefab, module, processPrefab))
							continue;

						float power = GetProcessHeatPower(part, prefab, module, processPrefab) * GetProcessThrottle(module);
						float outlet = IntegrationReflection.GetFloat(processPrefab, "systemOutletTemperature", GetProcessField(prefab, module, "systemOutletTemperature", 0f));
						ActivateProducer(producer, power, outlet, link.volume);
					}
					else if (module.moduleName == "HarvesterSystemHeat")
					{
						if (!Lib.Proto.GetBool(module, "deployed") || !Lib.Proto.GetBool(module, "running") || Lib.Proto.GetString(module, "issue").Length > 0)
							continue;

						RegisterProducer(loops, part, prefab, module, Lib.Proto.GetString(module, "systemHeatModuleID"),
							GetHarvesterHeatPower(prefab, module),
							GetHarvesterField(prefab, module, "systemOutletTemperature", 0f),
							GetHarvesterField(prefab, module, "shutdownTemperature", float.MaxValue));
					}
					else if (module.moduleName == "SystemHeatRadiatorKerbalism")
					{
						if (!IsRadiatorOperational(part, prefab, module))
							continue;

						int loopId = GetRadiatorLoopId(part, prefab, module);
						if (loopId < 0)
							continue;

						EnsureLoop(loops, loopId).radiators.Add(BuildRadiatorRejector(part, prefab, module));
					}
					else if (module.moduleName == "ModuleSystemHeatRadiator" || module.moduleName == "ModuleActiveRadiator")
					{
						if (IntegrationUtils.TryFindPartModuleSnapshot(part, "SystemHeatRadiatorKerbalism") != null)
							continue;

						if (!IsNativeRadiatorOperational(part, module))
							continue;

						int loopId = GetNativeRadiatorLoopId(part, prefab, module);
						if (loopId < 0)
							continue;

						EnsureLoop(loops, loopId).radiators.Add(BuildRadiatorRejector(part, prefab, module));
					}
					else if (module.moduleName == "SystemHeatConverterKerbalismUpdater")
					{
						ProtoPartModuleSnapshot converter = IntegrationUtils.TryFindPartModuleSnapshot(part, "ModuleSystemHeatConverter");
						if (converter == null || !Lib.Proto.GetBool(converter, "IsActivated"))
							continue;

						PartModule converterPrefab = FindPrefabModule(prefab, "ModuleSystemHeatConverter");
						if (converterPrefab == null)
							continue;

						RegisterProducer(loops, part, prefab, converter, GetSystemHeatModuleId(converterPrefab),
							IntegrationReflection.GetFloat(converterPrefab, "systemPower"),
							IntegrationReflection.GetFloat(converterPrefab, "systemOutletTemperature"),
							IntegrationReflection.GetFloat(converterPrefab, "shutdownTemperature", float.MaxValue));
					}
					else if (module.moduleName == "SystemHeatHarvesterKerbalismUpdater")
					{
						ProtoPartModuleSnapshot harvester = IntegrationUtils.TryFindPartModuleSnapshot(part, "ModuleSystemHeatHarvester");
						if (harvester == null || !Lib.Proto.GetBool(harvester, "IsActivated"))
							continue;

						PartModule harvesterPrefab = FindPrefabModule(prefab, "ModuleSystemHeatHarvester");
						if (harvesterPrefab == null)
							continue;

						RegisterProducer(loops, part, prefab, harvester, GetSystemHeatModuleId(harvesterPrefab),
							IntegrationReflection.GetFloat(harvesterPrefab, "systemPower"),
							IntegrationReflection.GetFloat(harvesterPrefab, "systemOutletTemperature"),
							IntegrationReflection.GetFloat(harvesterPrefab, "shutdownTemperature", float.MaxValue));
					}
					else if (module.moduleName == "ModuleSpaceDustHarvester")
					{
						TryAddSpaceDustHarvesterHeat(part, prefab, module, loops);
					}
					else if (module.moduleName == "SystemHeatFissionReactorKerbalismUpdater")
					{
						ProtoPartModuleSnapshot reactor = IntegrationUtils.FindPartModuleSnapshot(part, "ModuleSystemHeatFissionReactor");
						if (reactor == null)
							continue;

						PartModule reactorPrefab = FindPrefabModule(prefab, "ModuleSystemHeatFissionReactor");
						string heatModuleId = reactorPrefab != null ? GetSystemHeatModuleId(reactorPrefab) : "reactor";
						if (!TryResolveHeatLink(part, prefab, heatModuleId, out HeatLink link))
							continue;

						RegisterNativeFissionProducer(EnsureLoop(loops, link.loopId), part, reactor, reactorPrefab, link.volume);
					}
					else if (module.moduleName == "SystemHeatFissionEngineKerbalismUpdater")
					{
						ProtoPartModuleSnapshot engine = FindFissionEngineSnapshot(part, module);
						if (engine == null)
							continue;

						PartModule enginePrefab = FindFissionEnginePrefab(prefab, engine);
						if (!TryResolveFissionEngineHeatLink(part, prefab, enginePrefab, out HeatLink link))
							continue;

						RegisterNativeFissionProducer(EnsureLoop(loops, link.loopId), part, engine, enginePrefab, link.volume);
					}
					else if (module.moduleName == "ModuleSystemHeatCryoTank")
					{
						if (!PartHasModule(part, "SystemHeatCryoTankKerbalismUpdater"))
							continue;

						PartModule cryoPrefab = FindCryoTankPrefab(prefab, module);
						if (cryoPrefab == null)
							continue;

						if (!TryResolveHeatLink(part, prefab, GetSystemHeatModuleId(cryoPrefab), out HeatLink link))
							continue;

						float loopTemperature = Lib.Proto.GetFloat(link.snapshot, "currentLoopTemperature");
						if (loopTemperature <= 0f)
							loopTemperature = GetFallbackLoopTemperature();

						float heat = GetCryoTankCoolingHeatPower(part, module, cryoPrefab, loopTemperature);
						if (heat <= 0f)
							continue;

						EnsureLoop(loops, link.loopId).constantFluxKw += heat;
					}
					else if (module.moduleName == "ModuleSystemHeatSink")
					{
						if (!IsHeatSinkOperational(part, module))
							continue;

						PartModule sinkPrefab = FindHeatSinkPrefab(prefab, module);
						string heatModuleId = sinkPrefab != null
							? GetSystemHeatModuleId(sinkPrefab)
							: Lib.Proto.GetString(module, "systemHeatModuleID");
						int loopId = GetLinkedLoopId(part, prefab, heatModuleId);
						if (loopId < 0)
							continue;

						EnsureLoop(loops, loopId).heatSinks.Add(new HeatSink
						{
							part = part,
							module = module,
							prefab = sinkPrefab
						});
					}
					else if (module.moduleName == "FFTFusionReactorKerbalismUpdater" || module.moduleName == "FFTFusionEngineKerbalismUpdater")
					{
						string fftReactorModule = module.moduleName == "FFTFusionEngineKerbalismUpdater"
							? "ModuleFusionEngine"
							: "FusionReactor";
						ProtoPartModuleSnapshot reactor = IntegrationUtils.FindPartModuleSnapshot(part, fftReactorModule);
						if (reactor == null || !Lib.Proto.GetBool(reactor, "Enabled"))
							continue;

						if (!TryGetFusionReactorHeatConfig(prefab, out string heatModuleId, out float systemPower))
							continue;

						int loopId = GetLinkedLoopId(part, prefab, heatModuleId);
						if (loopId < 0)
							continue;

						EnsureLoop(loops, loopId).constantFluxKw += systemPower;
					}
				}
			}

			if (loops.Count == 0)
				return;

			VesselData vd = v.KerbalismData();
			float heatScale = GetHeatScale();
			float environmentTemperature = GetEnvironmentTemperature(v);

			foreach (LoopState loop in loops.Values)
			{
				if (loop.volume <= 0f)
					loop.volume = 1f;
				RefreshLoopProducers(loop);
				loop.netFluxKw = ReconstructedNetFluxKw(loop, loop.temperature, heatScale);
			}

			ApplyHeatSinkStorage(loops, elapsed_s, heatScale);

			foreach (KeyValuePair<int, LoopState> entry in loops)
			{
				LoopState loop = entry.Value;

				SystemHeat.GetCoolantProperties(loop.coolantName, out float density, out float heatCapacity);
				float thermalCapacity = loop.volume * density * heatCapacity; // kJ/K
				if (thermalCapacity <= 0f)
					continue;

				loop.calibration = ResolveCalibration(vd, entry.Key, loop, heatScale);
				IntegrateLoop(v, loop, thermalCapacity, elapsed_s, heatScale, environmentTemperature);
				loop.netFluxKw = NetFluxKw(loop, loop.temperature, heatScale);

				foreach (ProtoPartModuleSnapshot heatModule in loop.heatModules)
				{
					Lib.Proto.Set(heatModule, "currentLoopTemperature", loop.temperature);
					Lib.Proto.Set(heatModule, "currentLoopFlux", loop.netFluxKw);
					Lib.Proto.Set(heatModule, BackgroundSimulatedField, true);
				}

				ApplyAccumulatedCoreDamage(v, loop);
			}

			SyncFrozenFissionReactorsFromLoops(v, loops, elapsed_s);
		}

		private static LoopState EnsureLoop(Dictionary<int, LoopState> loops, int loopId)
		{
			if (!loops.TryGetValue(loopId, out LoopState loop))
			{
				loop = new LoopState();
				loops[loopId] = loop;
			}
			return loop;
		}

		private static HeatProducer RegisterProducer(
			Dictionary<int, LoopState> loops,
			ProtoPartSnapshot part,
			Part prefab,
			ProtoPartModuleSnapshot module,
			string heatModuleId,
			float powerKw,
			float outletTemperature,
			float shutdownTemperature)
		{
			if (!TryResolveHeatLink(part, prefab, heatModuleId, out HeatLink link))
				return null;

			LoopState loop = EnsureLoop(loops, link.loopId);
			HeatProducer producer = new HeatProducer { part = part, module = module, shutdownTemperature = shutdownTemperature };
			loop.heatProducers.Add(producer);
			ActivateProducer(producer, powerKw, outletTemperature, link.volume);
			return producer;
		}

		private static void RegisterNativeFissionProducer(LoopState loop, ProtoPartSnapshot part, ProtoPartModuleSnapshot reactor, PartModule reactorPrefab, float nominalWeight)
		{
			float critical = GetNativeFissionCriticalTemperature(reactorPrefab, reactor);
			float maximum = GetNativeFissionMaximumTemperature(reactorPrefab, reactor);
			HeatProducer producer = new HeatProducer
			{
				part = part,
				module = reactor,
				shutdownTemperature = GetNativeFissionSafetyOverride(reactorPrefab, reactor),
				meltdownTemperature = critical > 0f && maximum > critical ? critical : 0f,
				maximumTemperature = maximum,
				coreDamageRate = reactorPrefab != null
					? IntegrationReflection.GetFloat(reactorPrefab, "CoreDamageRate", DefaultNativeCoreDamageRate)
					: DefaultNativeCoreDamageRate,
				isNativeFission = true
			};
			loop.heatProducers.Add(producer);

			if (!Lib.Proto.GetBool(reactor, "Enabled"))
				return;

			float throttle = Lib.Proto.GetFloat(reactor, "CurrentReactorThrottle");
			float nominal = reactorPrefab != null ? IntegrationReflection.GetFloat(reactorPrefab, "NominalTemperature") : 0f;
			ActivateProducer(producer, GetReactorWasteHeat(reactorPrefab, throttle), nominal, nominalWeight);
		}

		private static void ActivateProducer(HeatProducer producer, float powerKw, float outletTemperature, float nominalWeight)
		{
			if (powerKw <= 0f)
				return;

			producer.active = true;
			producer.powerKw = powerKw;
			producer.outletTemperature = outletTemperature;
			producer.nominalWeight = nominalWeight > 0f ? nominalWeight : 1f;
		}

		private static void RefreshLoopProducers(LoopState loop)
		{
			loop.producerFluxKw = 0f;
			loop.nominalWeightedSum = 0f;
			loop.nominalWeight = 0f;
			loop.hasActiveProducer = false;

			for (int i = 0; i < loop.heatProducers.Count; i++)
			{
				HeatProducer producer = loop.heatProducers[i];
				if (!producer.active)
					continue;

				loop.producerFluxKw += producer.powerKw;
				loop.hasActiveProducer = true;
				if (producer.outletTemperature > 0f)
				{
					loop.nominalWeightedSum += producer.outletTemperature * producer.nominalWeight;
					loop.nominalWeight += producer.nominalWeight;
				}
			}
		}

		#endregion

		#region flux model

		/// <summary>ModuleSystemHeat.AddFlux scales every flux by InternalHeatProductionFactor / 0.025.</summary>
		private static float GetHeatScale()
		{
			double factor = PhysicsGlobals.InternalHeatProductionFactor / 0.025;
			return factor > 0.0 && !double.IsNaN(factor) && !double.IsInfinity(factor) ? (float)factor : 1f;
		}

		/// <summary>HeatLoop.GetEnvironmentTemperature : body temperature at altitude, space baseline otherwise.</summary>
		private static float GetEnvironmentTemperature(Vessel v)
		{
			if (v == null || v.mainBody == null)
				return MinimumLoopTemperatureK;

			double temperature = v.mainBody.GetTemperature(v.altitude);
			if (double.IsNaN(temperature) || temperature > 50000.0)
				return MinimumLoopTemperatureK;
			return Mathf.Clamp((float)temperature, MinimumLoopTemperatureK, 50000f);
		}

		private static SystemHeatLoopCalibration ResolveCalibration(VesselData vd, int loopId, LoopState loop, float heatScale)
		{
			if (vd.systemHeatLoops == null)
				vd.systemHeatLoops = new Dictionary<int, SystemHeatLoopCalibration>();

			if (vd.systemHeatLoops.TryGetValue(loopId, out SystemHeatLoopCalibration calibration))
				return calibration;

			// Never simulated live (contract-spawned, or unloaded before SystemHeat ran), or the persisted state is
			// already ours (VesselData lost after the first step) : nothing trustworthy to calibrate against.
			if (loop.observedTemperature <= 0f || loop.observationStale)
				return null;

			// First background step after unload : proto still holds the live state, so the reconstruction
			// evaluated at the observed temperature can be compared directly with the observed net flux.
			float residual = loop.observedNetFluxKw - ReconstructedNetFluxKw(loop, loop.observedTemperature, heatScale);
			if (Mathf.Abs(residual) <= FluxEpsilonKw)
				residual = 0f;

			calibration = new SystemHeatLoopCalibration(residual, loop.observedTemperature);
			vd.systemHeatLoops[loopId] = calibration;
			return calibration;
		}

		private static float ReconstructedNetFluxKw(LoopState loop, float loopTemperature, float heatScale)
		{
			return (loop.constantFluxKw + loop.producerFluxKw - loop.heatSinkFluxOffsetKw - RadiatorRejectTotal(loop, loopTemperature)) * heatScale;
		}

		private static float ResidualKw(LoopState loop, float loopTemperature)
		{
			SystemHeatLoopCalibration calibration = loop.calibration;
			if (calibration == null || calibration.residualKw == 0f)
				return 0f;

			if (calibration.residualKw > 0f || calibration.referenceTemperatureK <= 0f)
				return calibration.residualKw;

			// Unmodeled rejection : SystemHeat radiator curves are linear from 0 K, scale the same way.
			return calibration.residualKw * Mathf.Max(0f, loopTemperature / calibration.referenceTemperatureK);
		}

		private static float NetFluxKw(LoopState loop, float loopTemperature, float heatScale)
		{
			return ReconstructedNetFluxKw(loop, loopTemperature, heatScale) + ResidualKw(loop, loopTemperature);
		}

		/// <summary>HeatLoop.PositiveFlux : what raises a loop that sits below its nominal temperature.</summary>
		private static float PositiveFluxKw(LoopState loop, float heatScale)
		{
			float positive = (loop.constantFluxKw + loop.producerFluxKw) * heatScale;
			if (loop.calibration != null && loop.calibration.residualKw > 0f)
				positive += loop.calibration.residualKw;
			return positive;
		}

		private static float RadiatorRejectTotal(LoopState loop, float loopTemperature)
		{
			int count = loop.radiators.Count;
			if (count == 0 || loopTemperature <= MinimumLoopTemperatureK)
				return 0f;

			float total = 0f;
			for (int i = 0; i < count; i++)
				total += loop.radiators[i].Evaluate(loopTemperature);
			return total;
		}

		/// <summary>
		/// HeatLoop.CalculateNominalTemperature : volume-weighted outlet temperature of the active producers,
		/// the persisted nominal when only unmodeled heat is present, the environment when nothing heats the loop.
		/// </summary>
		private static float FloorTemperature(LoopState loop, float environmentTemperature)
		{
			bool unmodeledHeat = loop.calibration != null && loop.calibration.residualKw > FluxEpsilonKw;
			if (!loop.hasActiveProducer && !unmodeledHeat)
				return environmentTemperature;

			float nominal = loop.hasActiveProducer && loop.nominalWeight > 0f
				? loop.nominalWeightedSum / loop.nominalWeight
				: 0f;
			if (nominal <= MinimumLoopTemperatureK)
				nominal = loop.persistedNominalTemperature;
			return Mathf.Max(nominal, environmentTemperature);
		}

		#endregion

		#region integration

		private static void IntegrateLoop(Vessel v, LoopState loop, float thermalCapacityKjPerK, float elapsed_s, float heatScale, float environmentTemperature)
		{
			float kelvinPerKj = 1000f / thermalCapacityKjPerK;
			float temperature = Mathf.Clamp(loop.temperature, MinimumLoopTemperatureK, MaximumLoopTemperatureK);
			float remaining = elapsed_s;

			for (int iteration = 0; remaining > 0f && iteration < MaxThermalEventsPerStep; iteration++)
			{
				if (ShutdownProducersAtOrAbove(v, loop, temperature))
					RefreshLoopProducers(loop);

				float floor = FloorTemperature(loop, environmentTemperature);

				if (temperature < floor - TemperatureToleranceK)
				{
					// Below nominal : SystemHeat raises the loop with the gross positive flux and clamps at nominal
					// (and never lets a loop sit below the environment temperature).
					float positiveFlux = PositiveFluxKw(loop, heatScale);
					if (positiveFlux <= FluxEpsilonKw)
					{
						temperature = floor;
						continue;
					}

					float rate = positiveFlux * kelvinPerKj;
					float timeToFloor = (floor - temperature) / rate;
					if (timeToFloor >= remaining)
					{
						AccumulateCoreDamage(loop, temperature, temperature, 0f, rate, remaining);
						temperature += rate * remaining;
						break;
					}

					AccumulateCoreDamage(loop, temperature, temperature, 0f, rate, timeToFloor);
					temperature = floor;
					remaining -= timeToFloor;
					continue;
				}

				float net = NetFluxKw(loop, temperature, heatScale);
				if (net <= FluxEpsilonKw)
				{
					// Balanced or over-cooled : hold at nominal, or cool back toward the higher of nominal and
					// the equilibrium. Nothing else can happen during this step.
					if (temperature <= floor + TemperatureToleranceK)
					{
						temperature = floor;
						AccumulateCoreDamage(loop, floor, floor, 0f, 0f, remaining);
						break;
					}

					float target = floor;
					if (NetFluxKw(loop, floor, heatScale) > FluxEpsilonKw)
						target = FindEquilibriumTemperature(loop, floor, temperature, heatScale);

					float coolingRate = ApproachRate(loop, temperature, target, kelvinPerKj, heatScale);
					if (!loop.hasActiveProducer)
						coolingRate += HeatLoopDecayCoefficient * kelvinPerKj;

					AccumulateCoreDamage(loop, temperature, target, coolingRate, 0f, remaining);
					temperature = Approach(temperature, target, coolingRate, 0f, remaining);
					break;
				}

				// Heating : approach the equilibrium above us, stopping at the next producer shutdown threshold.
				float equilibrium = NetFluxKw(loop, MaximumLoopTemperatureK, heatScale) > FluxEpsilonKw
					? float.PositiveInfinity
					: FindEquilibriumTemperature(loop, temperature, MaximumLoopTemperatureK, heatScale);
				float heatingRate = 0f;
				float linearRate = 0f;
				if (float.IsPositiveInfinity(equilibrium))
					linearRate = net * kelvinPerKj;
				else
					heatingRate = ApproachRate(loop, temperature, equilibrium, kelvinPerKj, heatScale);

				float threshold = NextShutdownTemperature(loop, temperature);
				float ceiling = Mathf.Min(threshold, MaximumLoopTemperatureK);
				float timeToCeiling = TimeToReach(temperature, equilibrium, heatingRate, linearRate, ceiling);

				if (timeToCeiling >= remaining)
				{
					AccumulateCoreDamage(loop, temperature, equilibrium, heatingRate, linearRate, remaining);
					temperature = Approach(temperature, equilibrium, heatingRate, linearRate, remaining);
					break;
				}

				AccumulateCoreDamage(loop, temperature, equilibrium, heatingRate, linearRate, timeToCeiling);
				temperature = ceiling;
				remaining -= timeToCeiling;

				if (threshold > MaximumLoopTemperatureK)
				{
					// Pinned at the cap with nothing left to shut down.
					AccumulateCoreDamage(loop, temperature, temperature, 0f, 0f, remaining);
					break;
				}
				// Otherwise loop : the shutdown check at the top trips every producer whose threshold was just reached.
			}

			loop.temperature = Mathf.Clamp(temperature, MinimumLoopTemperatureK, MaximumLoopTemperatureK);
		}

		/// <summary>Temperature after t seconds : exponential approach to target at rate k, or linear when k is 0.</summary>
		private static float Approach(float start, float target, float k, float linearRate, float t)
		{
			float result = k > 0f && !float.IsInfinity(target)
				? target + (start - target) * Mathf.Exp(-k * t)
				: start + linearRate * t;
			return Mathf.Clamp(result, MinimumLoopTemperatureK, MaximumLoopTemperatureK);
		}

		/// <summary>Seconds until a rising trajectory reaches threshold, +inf when it never does.</summary>
		private static float TimeToReach(float start, float target, float k, float linearRate, float threshold)
		{
			if (threshold <= start)
				return 0f;

			if (k > 0f && !float.IsInfinity(target))
			{
				if (target <= threshold)
					return float.PositiveInfinity;

				float ratio = (threshold - target) / (start - target);
				if (ratio >= 1f)
					return 0f;
				if (ratio <= 0f)
					return float.PositiveInfinity;
				return -Mathf.Log(ratio) / k;
			}

			if (linearRate <= 0f)
				return float.PositiveInfinity;
			return (threshold - start) / linearRate;
		}

		/// <summary>
		/// First-order rate of the exponential approach from one temperature to another, from the secant of
		/// the net flux between both (exact for the piecewise-linear SystemHeat radiator curves).
		/// </summary>
		private static float ApproachRate(LoopState loop, float from, float to, float kelvinPerKj, float heatScale)
		{
			float span = to - from;
			if (Mathf.Abs(span) < 0.01f)
				return 1e6f;

			float slope = (NetFluxKw(loop, from, heatScale) - NetFluxKw(loop, to, heatScale)) / span; // kW/K, > 0 for a monotonic loop
			if (slope <= 0f)
				slope = Mathf.Abs(NetFluxKw(loop, from, heatScale)) / Mathf.Abs(span);
			return Mathf.Max(slope * kelvinPerKj, 1e-9f);
		}

		/// <summary>Root of the (monotonically decreasing) net flux between low and high.</summary>
		private static float FindEquilibriumTemperature(LoopState loop, float low, float high, float heatScale)
		{
			for (int i = 0; i < 24; i++)
			{
				float mid = (low + high) * 0.5f;
				float flux = NetFluxKw(loop, mid, heatScale);
				if (Mathf.Abs(flux) <= FluxEpsilonKw)
					return mid;

				if (flux > 0f)
					low = mid;
				else
					high = mid;
			}
			return (low + high) * 0.5f;
		}

		private static float NextShutdownTemperature(LoopState loop, float temperature)
		{
			float next = float.MaxValue;
			for (int i = 0; i < loop.heatProducers.Count; i++)
			{
				HeatProducer producer = loop.heatProducers[i];
				if (!producer.active || !producer.autoShutdown)
					continue;
				if (producer.shutdownTemperature > temperature + TemperatureToleranceK && producer.shutdownTemperature < next)
					next = producer.shutdownTemperature;
			}
			return next;
		}

		private static bool ShutdownProducersAtOrAbove(Vessel v, LoopState loop, float temperature)
		{
			bool any = false;
			for (int i = 0; i < loop.heatProducers.Count; i++)
			{
				HeatProducer producer = loop.heatProducers[i];
				if (!producer.active || !producer.autoShutdown)
					continue;
				if (temperature + TemperatureToleranceK < producer.shutdownTemperature)
					continue;

				ShutdownProducer(v, producer);
				producer.active = false;
				producer.powerKw = 0f;
				any = true;
			}
			return any;
		}

		private static void ShutdownProducer(Vessel v, HeatProducer producer)
		{
			switch (producer.module.moduleName)
			{
				case "ProcessControllerSystemHeat":
					ShutdownProcessProducer(v, producer);
					break;
				case "HarvesterSystemHeat":
					Lib.Proto.Set(producer.module, "running", false);
					break;
				case "ModuleSystemHeatConverter":
				case "ModuleSystemHeatHarvester":
					Lib.Proto.Set(producer.module, "IsActivated", false);
					break;
				case "ModuleSystemHeatFissionReactor":
				case "ModuleSystemHeatFissionEngine":
				case "ModuleSpaceDustHarvester":
					Lib.Proto.Set(producer.module, "Enabled", false);
					break;
			}
		}

		private static void ShutdownProcessProducer(Vessel v, HeatProducer producer)
		{
			Part prefab = PartLoader.getPartInfoByName(producer.part.partName).partPrefab;
			PartModule processPrefab = FindMatchingPrefabModule(prefab, producer.module, "ProcessControllerSystemHeat");

			if (IsFissionProcessController(prefab, producer.module, processPrefab))
			{
				SetProtoFissionRunning(v, producer.part, producer.module, false);
				Lib.Proto.Set(producer.module, nameof(ProcessControllerSystemHeat.CurrentPowerPercent), 0f);
				string resource = GetProcessResourceName(producer.module, processPrefab);
				ProtoPartResourceSnapshot pseudo = !string.IsNullOrEmpty(resource)
					? FindPartResource(producer.part, resource)
					: null;
				if (pseudo != null)
					ClearFrozenFissionPseudoResource(pseudo);
				return;
			}

			Lib.Proto.Set(producer.module, "running", false);
			SetPseudoResourceFlow(producer.part, producer.module, processPrefab, false);
		}

		#endregion

		#region core damage

		/// <summary>
		/// Sample the analytic trajectory over one segment and accumulate rate-based core damage for every
		/// reactor on the loop whose meltdown threshold the trajectory exceeds. No instantaneous
		/// temperature-to-damage floor : like SystemHeat, only time spent above the threshold hurts.
		/// </summary>
		private static void AccumulateCoreDamage(LoopState loop, float start, float target, float k, float linearRate, float duration)
		{
			if (duration <= 0f)
				return;

			float lowestMeltdown = float.MaxValue;
			for (int i = 0; i < loop.heatProducers.Count; i++)
			{
				HeatProducer producer = loop.heatProducers[i];
				if (producer.meltdownTemperature > 0f && producer.meltdownTemperature < lowestMeltdown)
					lowestMeltdown = producer.meltdownTemperature;
			}
			if (lowestMeltdown == float.MaxValue)
				return;

			// Segments are monotonic : nothing to do when both ends sit below every threshold.
			float end = Approach(start, target, k, linearRate, duration);
			if (Mathf.Max(start, end) <= lowestMeltdown)
				return;

			float dt = duration / DamageSamplesPerSegment;
			for (int s = 0; s < DamageSamplesPerSegment; s++)
			{
				float temperature = Approach(start, target, k, linearRate, (s + 0.5f) * dt);
				if (temperature <= lowestMeltdown)
					continue;

				for (int i = 0; i < loop.heatProducers.Count; i++)
				{
					HeatProducer producer = loop.heatProducers[i];
					if (producer.meltdownTemperature <= 0f || temperature <= producer.meltdownTemperature)
						continue;

					if (producer.isFissionProcess)
					{
						if (producer.coreDamageRate > 0f)
						{
							float curveMult = producer.coreDamageCurve != null && producer.coreDamageCurve.Curve.length > 0
								? producer.coreDamageCurve.Evaluate(temperature)
								: 1f;
							producer.pendingDamage += producer.coreDamageRate * curveMult * dt * 100f;
						}
						else
						{
							// No rate configured : fall back to SystemHeat's exceedance-proportional model.
							producer.pendingDamage += DefaultNativeCoreDamageRate * (temperature - producer.meltdownTemperature) * dt;
						}
					}
					else if (producer.isNativeFission)
					{
						float rate = producer.coreDamageRate > 0f ? producer.coreDamageRate : DefaultNativeCoreDamageRate;
						producer.pendingDamage += rate * (temperature - producer.meltdownTemperature) * dt;
					}
				}
			}
		}

		private static void ApplyAccumulatedCoreDamage(Vessel v, LoopState loop)
		{
			for (int i = 0; i < loop.heatProducers.Count; i++)
			{
				HeatProducer producer = loop.heatProducers[i];
				if (producer.pendingDamage <= 0f)
					continue;

				if (producer.isFissionProcess)
				{
					float damage = Mathf.Clamp(Lib.Proto.GetFloat(producer.module, "CoreDamage") + producer.pendingDamage, 0f, 100f);
					Lib.Proto.Set(producer.module, "CoreDamage", damage);
					if (damage >= 100f)
						BreakProcessReactor(v, producer.part, producer.module);
				}
				else if (producer.isNativeFission)
				{
					float integrity = Mathf.Clamp(Lib.Proto.GetFloat(producer.module, "CoreIntegrity", 100f) - producer.pendingDamage, 0f, 100f);
					Lib.Proto.Set(producer.module, "CoreIntegrity", integrity);
					if (integrity <= 0f)
						BreakNativeFissionReactor(v, producer.part, producer.module);
				}

				producer.pendingDamage = 0f;
			}
		}

		private static void BreakProcessReactor(Vessel v, ProtoPartSnapshot part, ProtoPartModuleSnapshot module)
		{
			v.KerbalismData().ResetReliabilityStatus();
			SetProtoFissionRunning(v, part, module, false);
			Lib.Proto.Set(module, "broken", true);
			Lib.Proto.Set(module, "isEnabled", false);
			Lib.Proto.Set(module, "enabled", false);
			Lib.Proto.Set(module, "CurrentPowerPercent", 0f);
			Lib.Proto.Set(module, "CoreDamage", 100f);

			PartModule prefab = FindMatchingPrefabModule(part.partPrefab, module, "ProcessControllerSystemHeat");
			string resource = prefab != null ? IntegrationReflection.GetString(prefab, "resource") : Lib.Proto.GetString(module, "resource");
			ProtoPartResourceSnapshot res = FindPartResource(part, resource);
			if (res != null)
				res.flowState = false;

			SetReliabilityState(part, ProcessReliabilityTypes, true);
		}

		private static void BreakNativeFissionReactor(Vessel v, ProtoPartSnapshot part, ProtoPartModuleSnapshot module)
		{
			v.KerbalismData().ResetReliabilityStatus();
			Lib.Proto.Set(module, "Enabled", false);
			Lib.Proto.Set(module, "CurrentReactorThrottle", 0f);
			Lib.Proto.Set(module, "CurrentThrottle", 0f);
			Lib.Proto.Set(module, "CurrentElectricalGeneration", 0f);
			Lib.Proto.Set(module, "MaxElectricalGeneration", 0f);
			Lib.Proto.Set(module, "CoreIntegrity", 0f);

			SetReliabilityState(part, NativeFissionReliabilityTypes, true);
		}

		/// <summary>Set broken + critical on every Reliability module of the part whose type is in the list.</summary>
		private static void SetReliabilityState(ProtoPartSnapshot part, string[] types, bool brokenCritical)
		{
			foreach (ProtoPartModuleSnapshot reliability in part.modules)
			{
				if (reliability.moduleName != "Reliability")
					continue;

				if (Array.IndexOf(types, Lib.Proto.GetString(reliability, "type")) < 0)
					continue;

				Lib.Proto.Set(reliability, "broken", brokenCritical);
				Lib.Proto.Set(reliability, "critical", brokenCritical);
			}
		}

		private static bool HasCriticalReliability(ProtoPartSnapshot part, string[] types)
		{
			foreach (ProtoPartModuleSnapshot reliability in part.modules)
			{
				if (reliability.moduleName != "Reliability")
					continue;
				if (Array.IndexOf(types, Lib.Proto.GetString(reliability, "type")) < 0)
					continue;
				if (Lib.Proto.GetBool(reliability, "broken") && Lib.Proto.GetBool(reliability, "critical"))
					return true;
			}
			return false;
		}

		#endregion

		#region heat sinks

		private static void ApplyHeatSinkStorage(Dictionary<int, LoopState> loops, float elapsed_s, float heatScale)
		{
			if (elapsed_s <= 0f)
				return;

			foreach (LoopState loop in loops.Values)
			{
				if (loop.heatSinks.Count == 0)
					continue;

				float netFlux = ReconstructedNetFluxKw(loop, loop.temperature, heatScale);
				if (netFlux <= FluxEpsilonKw)
					continue;

				for (int i = 0; i < loop.heatSinks.Count; i++)
				{
					netFlux = ReconstructedNetFluxKw(loop, loop.temperature, heatScale);
					if (netFlux <= FluxEpsilonKw)
						break;

					HeatSink sink = loop.heatSinks[i];
					float storedEnergy = StoreHeatInSink(sink, netFlux / heatScale, elapsed_s);
					if (storedEnergy <= 0f)
						continue;

					loop.heatSinkFluxOffsetKw += storedEnergy / elapsed_s;
				}

				loop.netFluxKw = ReconstructedNetFluxKw(loop, loop.temperature, heatScale);
			}
		}

		private static float StoreHeatInSink(HeatSink sink, float availableFluxKw, float elapsed_s)
		{
			if (sink == null || sink.module == null || availableFluxKw <= 0f)
				return 0f;

			float maxRate = IntegrationReflection.GetFloat(sink.prefab, "maxHeatRate", Lib.Proto.GetFloat(sink.module, "maxHeatRate"));
			float maxStorage = IntegrationReflection.GetFloat(sink.prefab, "heatStorageMaximum", Lib.Proto.GetFloat(sink.module, "heatStorageMaximum"));
			float storageMass = IntegrationReflection.GetFloat(sink.prefab, "heatStorageMass", Lib.Proto.GetFloat(sink.module, "heatStorageMass", 1f));
			float specificHeat = IntegrationReflection.GetFloat(sink.prefab, "heatStorageSpecificHeat", Lib.Proto.GetFloat(sink.module, "heatStorageSpecificHeat", 1.26f));
			float heatStored = Lib.Proto.GetFloat(sink.module, "heatStored");

			if (maxRate <= 0f || maxStorage <= heatStored)
				return 0f;

			float remainingStorage = maxStorage - heatStored;
			float availableEnergy = availableFluxKw * elapsed_s;
			float rateLimitedEnergy = maxRate * elapsed_s;
			float storedEnergy = Mathf.Min(remainingStorage, availableEnergy, rateLimitedEnergy);
			if (storedEnergy <= 0f)
				return 0f;

			Lib.Proto.Set(sink.module, "heatStored", heatStored + storedEnergy);

			if (storageMass > 0f && specificHeat > 0f)
			{
				float storageTemperature = Lib.Proto.GetFloat(sink.module, "storageTemperature");
				storageTemperature += storedEnergy / (specificHeat * storageMass);
				Lib.Proto.Set(sink.module, "storageTemperature", Mathf.Clamp(storageTemperature, 0f, 5000f));
			}

			return storedEnergy;
		}

		#endregion

		#region radiators

		private static RadiatorRejector BuildRadiatorRejector(ProtoPartSnapshot part, Part prefab, ProtoPartModuleSnapshot module)
		{
			float scale = Lib.Proto.GetFloat(module, "scale", 1f);
			if (scale <= 0f)
				scale = 1f;
			float scaleEmissionPower = Lib.Proto.GetFloat(module, "scaleEmissionPower", 2f);
			float scaleFactor = (float)Math.Pow(scale, scaleEmissionPower);

			RadiatorRejector rejector = new RadiatorRejector { scaleFactor = scaleFactor };

			if (TryGetUSRadiatorSelectedPower(part, prefab, module, out float selectedPower))
			{
				rejector.constantPowerKw = selectedPower;
				rejector.linearToRated = true;
				return rejector;
			}

			string radiatorModuleName = Lib.Proto.GetString(module, "radiatorModuleName", "ModuleSystemHeatRadiator");
			PartModule nativeRadiator = FindPrefabModule(prefab, radiatorModuleName)
				?? FindPrefabModule(prefab, "ModuleSystemHeatRadiator")
				?? FindPrefabModule(prefab, "ModuleActiveRadiator");

			FloatCurve curve = IntegrationReflection.GetField<FloatCurve>(nativeRadiator, "temperatureCurve");
			if (HasKeys(curve))
			{
				rejector.curve = curve;
				return rejector;
			}

			PartModule shRadiator = FindPrefabModule(prefab, "SystemHeatRadiatorKerbalism");
			if (shRadiator != null)
			{
				curve = IntegrationReflection.GetField<FloatCurve>(shRadiator, "temperatureCurve");
				if (HasKeys(curve))
				{
					// The sidecar rebuilds its own curve with the scale already applied.
					rejector.curve = curve;
					rejector.scaleFactor = 1f;
					return rejector;
				}

				curve = IntegrationReflection.GetField<FloatCurve>(shRadiator, "baseTemperatureCurve");
				if (HasKeys(curve))
				{
					rejector.curve = curve;
					return rejector;
				}
			}

			float inputPower = GetRadiatorInputResourcePower(prefab, module);
			if (inputPower > 0f)
			{
				rejector.constantPowerKw = inputPower;
				return rejector;
			}

			if (nativeRadiator != null)
			{
				float maxTransfer = IntegrationReflection.GetFloat(nativeRadiator, "maxEnergyTransfer", 0f);
				if (maxTransfer > 0f)
				{
					rejector.constantPowerKw = maxTransfer;
					return rejector;
				}
			}

			rejector.constantPowerKw = 100f * RadiatorCoefficient;
			return rejector;
		}

		private static bool HasKeys(FloatCurve curve)
		{
			return curve != null && curve.Curve != null && curve.Curve.length > 0;
		}

		private static float GetRadiatorInputResourcePower(Part prefab, ProtoPartModuleSnapshot module)
		{
			string radiatorModuleName = Lib.Proto.GetString(module, "radiatorModuleName", "ModuleSystemHeatRadiator");
			PartModule radiator = FindPrefabModule(prefab, radiatorModuleName)
				?? FindPrefabModule(prefab, "ModuleSystemHeatRadiator")
				?? FindPrefabModule(prefab, "ModuleActiveRadiator")
				?? FindPrefabModule(prefab, "SystemHeatRadiatorKerbalism");
			if (radiator == null)
				return 0f;

			float power = 0f;
			IList inputResources = SystemHeat.GetResHandlerInputResources(radiator);
			if (inputResources != null)
			{
				for (int i = 0; i < inputResources.Count; i++)
				{
					if (inputResources[i] is ModuleResource res)
						power += (float)res.rate;
				}
			}

			return power > 0f ? power : 0f;
		}

		private static int GetNativeRadiatorLoopId(ProtoPartSnapshot part, Part prefab, ProtoPartModuleSnapshot nativeModule)
		{
			PartModule nativePrefab = FindMatchingPrefabModule(prefab, nativeModule, nativeModule.moduleName);
			string heatModuleId = nativePrefab != null
				? GetSystemHeatModuleId(nativePrefab)
				: Lib.Proto.GetString(nativeModule, "systemHeatModuleID");
			return GetLinkedLoopId(part, prefab, heatModuleId);
		}

		/// <summary>
		/// A native radiator counts when its persisted IsCooling (deployed and enabled in flight) is set and no
		/// Reliability failure is recorded. The prefab is never consulted : IsCooling is only written by
		/// FixedUpdate, so it reads false on every prefab and would drop every radiator on the vessel.
		/// </summary>
		private static bool IsNativeRadiatorOperational(ProtoPartSnapshot part, ProtoPartModuleSnapshot nativeModule)
		{
			if (!Lib.Proto.GetBool(nativeModule, "IsCooling", true))
				return false;

			return !IsRadiatorReliabilityBroken(part);
		}

		private static bool IsRadiatorOperational(ProtoPartSnapshot part, Part prefab, ProtoPartModuleSnapshot radiatorModule)
		{
			if (!Lib.Proto.GetBool(radiatorModule, "IsCooling", true))
				return false;

			if (TryGetUSRadiatorSelectedPower(part, prefab, radiatorModule, out float selectedPower) && selectedPower <= 0f)
				return false;

			return !IsRadiatorReliabilityBroken(part);
		}

		private static bool IsRadiatorReliabilityBroken(ProtoPartSnapshot part)
		{
			foreach (ProtoPartModuleSnapshot module in part.modules)
			{
				if (module.moduleName != "Reliability" || !Lib.Proto.GetBool(module, "broken"))
					continue;

				if (Array.IndexOf(RadiatorReliabilityTypes, Lib.Proto.GetString(module, "type")) >= 0)
					return true;
			}

			return false;
		}

		private static string GetConfiguredRadiatorModuleName(Part prefab, ProtoPartModuleSnapshot radiatorModule)
		{
			PartModule wrapperPrefab = FindPrefabModule(prefab, "SystemHeatRadiatorKerbalism");
			string fallback = IntegrationReflection.GetString(wrapperPrefab, "radiatorModuleName", "ModuleSystemHeatRadiator");
			return Lib.Proto.GetString(radiatorModule, "radiatorModuleName", fallback);
		}

		private static bool TryGetUSRadiatorSelectedPower(ProtoPartSnapshot part, Part prefab, ProtoPartModuleSnapshot radiatorModule, out float selectedPower)
		{
			selectedPower = 0f;
			if (GetConfiguredRadiatorModuleName(prefab, radiatorModule) != "USRadiatorSwitch")
				return false;

			PartModule nativePrefab = FindPrefabModule(prefab, "USRadiatorSwitch");
			ProtoPartModuleSnapshot nativeSnapshot = IntegrationUtils.TryFindPartModuleSnapshot(part, "USRadiatorSwitch");
			if (nativePrefab == null || nativeSnapshot == null)
				return true;

			int selection = Lib.Proto.GetInt(nativeSnapshot, "CurrentSelection", IntegrationReflection.GetInt(nativePrefab, "CurrentSelection", -1));
			string powersString = IntegrationReflection.GetString(nativePrefab, "RadiatorPower");
			if (string.IsNullOrEmpty(powersString))
				return true;

			string[] powers = powersString.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
			if (selection < 0 || selection >= powers.Length)
				return true;

			if (!float.TryParse(powers[selection].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out selectedPower)
				|| float.IsNaN(selectedPower) || float.IsInfinity(selectedPower))
				selectedPower = 0f;
			else
				// Stock ModuleActiveRadiator transfer power is fifty times the
				// equivalent SystemHeat temperature-curve output in kW.
				selectedPower = Math.Max(0f, selectedPower / 50f);
			return true;
		}

		private static int GetRadiatorLoopId(ProtoPartSnapshot part, Part prefab, ProtoPartModuleSnapshot module)
		{
			string radiatorModuleName = Lib.Proto.GetString(module, "radiatorModuleName", "ModuleSystemHeatRadiator");
			PartModule radiator = FindPrefabModule(prefab, radiatorModuleName)
				?? FindPrefabModule(prefab, "ModuleSystemHeatRadiator")
				?? FindPrefabModule(prefab, "ModuleActiveRadiator")
				?? FindPrefabModule(prefab, "SystemHeatRadiatorKerbalism");
			string heatModuleId = radiator != null
				? GetSystemHeatModuleId(radiator, Lib.Proto.GetString(module, "systemHeatModuleID"))
				: Lib.Proto.GetString(module, "systemHeatModuleID");

			return GetLinkedLoopId(part, prefab, heatModuleId);
		}

		#endregion

		#region save migration

		/// <summary>
		/// Kerbalism 3.42 could not see native radiators from the background (prefab IsCooling check) and
		/// integrated the resulting imbalance with a thermal mass 1000x too small, so every running fission
		/// reactor was written as melted down on the first background step after unload (#1200). Called once
		/// per save last written by 3.42 : revert every thermal meltdown recorded on a SystemHeat fission
		/// reactor, restart it, and put overheated fission loops back at their nominal temperature.
		/// Legitimate meltdowns that happened under 3.42 are reverted too; the two cannot be told apart.
		/// Every persisted loop is also flagged as background-written : 3.42 rewrote loop temperatures without
		/// a marker, so none of them can be used to calibrate the reconstruction.
		/// </summary>
		public static void MigrateFalseMeltdowns()
		{
			if (!Active)
				return;

			List<ProtoVessel> protoVessels = HighLogic.CurrentGame?.flightState?.protoVessels;
			if (protoVessels == null)
				return;

			int reactors = 0;
			int loopsReset = 0;
			var loopResets = new Dictionary<int, float>();

			foreach (ProtoVessel pv in protoVessels)
			{
				if (pv?.protoPartSnapshots == null)
					continue;

				loopResets.Clear();
				bool touched = false;

				foreach (ProtoPartSnapshot part in pv.protoPartSnapshots)
				{
					Part prefab = part.partInfo != null ? part.partInfo.partPrefab : PartLoader.getPartInfoByName(part.partName)?.partPrefab;
					if (prefab == null)
						continue;

					foreach (ProtoPartModuleSnapshot module in part.modules)
					{
						if (module.moduleName == "ModuleSystemHeat")
						{
							Lib.Proto.Set(module, BackgroundSimulatedField, true);
						}
						else if (module.moduleName == "ProcessControllerSystemHeat")
						{
							PartModule processPrefab = FindMatchingPrefabModule(prefab, module, "ProcessControllerSystemHeat");
							if (!IsFissionProcessController(prefab, module, processPrefab))
								continue;

							ProtoPartModuleSnapshot heatModule = GetLinkedHeatModule(part, prefab, Lib.Proto.GetString(module, "systemHeatModuleID"));
							float meltdown = GetProcessField(prefab, module, "meltdownTemperature", 0f);
							float outlet = IntegrationReflection.GetFloat(processPrefab, "systemOutletTemperature", GetProcessField(prefab, module, "systemOutletTemperature", 0f));

							if (Lib.Proto.GetBool(module, "broken")
								&& Lib.Proto.GetFloat(module, "CoreDamage") >= 100f
								&& HasCriticalReliability(part, ProcessReliabilityTypes))
							{
								RevertProcessReactorMeltdown(part, module, processPrefab);
								QueueLoopReset(loopResets, heatModule, 0f, outlet);
								reactors++;
								touched = true;
							}
							else if (meltdown > 0f && QueueLoopReset(loopResets, heatModule, meltdown, outlet))
							{
								touched = true;
							}
						}
						else if (module.moduleName == "ModuleSystemHeatFissionReactor" || module.moduleName == "ModuleSystemHeatFissionEngine")
						{
							if (Lib.Proto.GetFloat(module, "CoreIntegrity", 100f) > 0f || !HasCriticalReliability(part, NativeFissionReliabilityTypes))
								continue;

							PartModule reactorPrefab = FindPrefabModule(prefab, module.moduleName);
							RevertNativeReactorMeltdown(part, module);
							reactors++;
							touched = true;

							string heatModuleId = reactorPrefab != null ? GetSystemHeatModuleId(reactorPrefab) : "";
							float nominal = reactorPrefab != null ? IntegrationReflection.GetFloat(reactorPrefab, "NominalTemperature") : 0f;
							QueueLoopReset(loopResets, GetLinkedHeatModule(part, prefab, heatModuleId), 0f, nominal);
						}
					}
				}

				if (loopResets.Count > 0)
					loopsReset += ApplyLoopResets(pv, loopResets);

				if (touched)
					pv.KerbalismData().ResetReliabilityStatus();
			}

			if (reactors > 0 || loopsReset > 0)
				Lib.Log("SystemHeat: reverted " + reactors + " reactor meltdown(s) and reset " + loopsReset + " overheated heat module(s) recorded by the Kerbalism 3.42 background simulation (#1200)");
		}

		/// <summary>
		/// Queue the loop of a heat module for a reset to nominal. With a threshold, only when the loop is above it.
		/// </summary>
		private static bool QueueLoopReset(Dictionary<int, float> loopResets, ProtoPartModuleSnapshot heatModule, float thresholdK, float fallbackNominalK)
		{
			if (heatModule == null)
				return false;

			float temperature = Lib.Proto.GetFloat(heatModule, "currentLoopTemperature");
			if (thresholdK > 0f && temperature <= thresholdK)
				return false;

			int loopId = Lib.Proto.GetInt(heatModule, "currentLoopID");
			if (loopResets.ContainsKey(loopId))
				return true;

			float nominal = Lib.Proto.GetFloat(heatModule, "nominalLoopTemperature");
			if (nominal <= MinimumLoopTemperatureK)
				nominal = fallbackNominalK;
			if (nominal <= 0f)
				nominal = MinimumLoopTemperatureK;

			loopResets[loopId] = nominal;
			return true;
		}

		/// <summary>Every ModuleSystemHeat on the loop gets the reset : the live loop takes the temperature of whichever module is added last.</summary>
		private static int ApplyLoopResets(ProtoVessel pv, Dictionary<int, float> loopResets)
		{
			int count = 0;
			foreach (ProtoPartSnapshot part in pv.protoPartSnapshots)
			{
				foreach (ProtoPartModuleSnapshot module in part.modules)
				{
					if (module.moduleName != "ModuleSystemHeat")
						continue;

					if (!loopResets.TryGetValue(Lib.Proto.GetInt(module, "currentLoopID"), out float nominal))
						continue;

					Lib.Proto.Set(module, "currentLoopTemperature", nominal);
					Lib.Proto.Set(module, "currentLoopFlux", 0f);
					count++;
				}
			}
			return count;
		}

		private static void RevertProcessReactorMeltdown(ProtoPartSnapshot part, ProtoPartModuleSnapshot module, PartModule processPrefab)
		{
			Lib.Proto.Set(module, "broken", false);
			Lib.Proto.Set(module, "isEnabled", true);
			Lib.Proto.Set(module, "enabled", true);
			Lib.Proto.Set(module, "CoreDamage", 0f);
			// It was producing heat when the false meltdown was written, so it was running.
			Lib.Proto.Set(module, nameof(ProcessController.running), true);
			Lib.Proto.Set(module, nameof(ProcessControllerSystemHeat.CurrentPowerPercent), 100f);

			ProtoPartResourceSnapshot pseudo = FindPartResource(part, GetProcessResourceName(module, processPrefab));
			if (pseudo != null)
				pseudo.flowState = true;

			SetReliabilityState(part, ProcessReliabilityTypes, false);
		}

		private static void RevertNativeReactorMeltdown(ProtoPartSnapshot part, ProtoPartModuleSnapshot module)
		{
			Lib.Proto.Set(module, "CoreIntegrity", 100f);
			Lib.Proto.Set(module, "Enabled", true);
			Lib.Proto.Set(module, "CurrentReactorThrottle", 100f);
			Lib.Proto.Set(module, "CurrentThrottle", 100f);

			SetReliabilityState(part, NativeFissionReliabilityTypes, false);
		}

		#endregion

		#region helpers

		private static float GetFallbackLoopTemperature()
		{
			return MinimumLoopTemperatureK;
		}

		/// <summary>k-th ModuleSystemHeat of the prefab (snapshots and prefab modules of one type share their ordering).</summary>
		private static PartModule GetPrefabHeatModuleAt(Part prefab, int ordinal)
		{
			if (prefab == null)
				return null;

			PartModule first = null;
			int index = 0;
			for (int i = 0; i < prefab.Modules.Count; i++)
			{
				PartModule heat = prefab.Modules[i];
				if (heat == null || heat.moduleName != "ModuleSystemHeat")
					continue;

				if (first == null)
					first = heat;
				if (index == ordinal)
					return heat;
				index++;
			}

			return first;
		}

		/// <summary>Ordinal of the prefab ModuleSystemHeat with this moduleID among the part's heat modules, -1 when there is none (empty id = first).</summary>
		private static int GetPrefabHeatModuleOrdinal(Part prefab, string moduleId)
		{
			if (prefab == null)
				return -1;

			int index = 0;
			for (int i = 0; i < prefab.Modules.Count; i++)
			{
				PartModule heat = prefab.Modules[i];
				if (heat == null || heat.moduleName != "ModuleSystemHeat")
					continue;

				if (string.IsNullOrEmpty(moduleId) || GetModuleId(heat) == moduleId)
					return index;
				index++;
			}

			return -1;
		}

		private static ProtoPartModuleSnapshot GetHeatModuleSnapshotAt(ProtoPartSnapshot part, int ordinal)
		{
			int index = 0;
			foreach (ProtoPartModuleSnapshot module in part.modules)
			{
				if (module.moduleName != "ModuleSystemHeat")
					continue;

				if (index == ordinal)
					return module;
				index++;
			}
			return null;
		}

		private static float GetHeatModuleVolume(PartModule prefabHeatModule)
		{
			return prefabHeatModule != null ? IntegrationReflection.GetFloat(prefabHeatModule, "volume", 1f) : 1f;
		}

		/// <summary>Resolve a systemHeatModuleID to the part's ModuleSystemHeat snapshot, prefab module, loop id and coolant volume.</summary>
		private static bool TryResolveHeatLink(ProtoPartSnapshot part, Part prefab, string moduleId, out HeatLink link)
		{
			link = default;
			if (part == null)
				return false;

			if (prefab == null)
			{
				link.snapshot = FindHeatModuleSnapshot(part, moduleId);
			}
			else
			{
				int ordinal = GetPrefabHeatModuleOrdinal(prefab, moduleId);
				if (ordinal < 0)
					return false;

				link.prefabModule = GetPrefabHeatModuleAt(prefab, ordinal);
				link.snapshot = GetHeatModuleSnapshotAt(part, ordinal) ?? FindHeatModuleSnapshot(part, moduleId);
			}

			if (link.snapshot == null)
				return false;

			link.loopId = Lib.Proto.GetInt(link.snapshot, "currentLoopID");
			link.volume = GetHeatModuleVolume(link.prefabModule);
			return true;
		}

		private static ProtoPartModuleSnapshot GetLinkedHeatModule(ProtoPartSnapshot part, Part prefab, string moduleId)
		{
			return TryResolveHeatLink(part, prefab, moduleId, out HeatLink link) ? link.snapshot : null;
		}

		private static int GetLinkedLoopId(ProtoPartSnapshot part, Part prefab, string moduleId)
		{
			return TryResolveHeatLink(part, prefab, moduleId, out HeatLink link) ? link.loopId : -1;
		}

		private static ProtoPartModuleSnapshot FindHeatModuleSnapshot(ProtoPartSnapshot part, string moduleId)
		{
			ProtoPartModuleSnapshot fallback = null;
			foreach (ProtoPartModuleSnapshot module in part.modules)
			{
				if (module.moduleName != "ModuleSystemHeat")
					continue;

				if (fallback == null)
					fallback = module;

				if (string.IsNullOrEmpty(moduleId) || Lib.Proto.GetString(module, "moduleID") == moduleId)
					return module;
			}

			if (fallback == null)
				IntegrationUtils.LogError("Part [" + part.partInfo.title + "] has no ModuleSystemHeat snapshot.");
			return fallback;
		}

		private static float GetProcessHeatPower(ProtoPartSnapshot part, Part prefab, ProtoPartModuleSnapshot module, PartModule processPrefab)
		{
			if (HasNoWasteHeatSubtype(part))
				return 0f;

			float systemPower;
			string resource = Lib.Proto.GetString(module, "resource");
			if (processPrefab != null)
				systemPower = IntegrationReflection.GetFloat(processPrefab, "systemPower");
			else
			{
				systemPower = 0f;
				foreach (PartModule pm in prefab.Modules)
				{
					if (pm.moduleName != "ProcessControllerSystemHeat")
						continue;
					if (string.IsNullOrEmpty(resource) || IntegrationReflection.GetString(pm, "resource") == resource)
					{
						systemPower = IntegrationReflection.GetFloat(pm, "systemPower");
						break;
					}
				}
				if (systemPower <= 0f)
					systemPower = Lib.Proto.GetFloat(module, "systemPower");
			}

			int multiplier = Lib.Proto.GetInt(module, "lastMultiplier", 1);
			if (multiplier <= 0)
				multiplier = 1;

			return systemPower * multiplier;
		}

		private static float GetProcessThrottle(ProtoPartModuleSnapshot module)
		{
			float percent = Lib.Proto.GetFloat(module, "CurrentPowerPercent", 100f);
			return Mathf.Clamp(percent, 0f, 100f) / 100f;
		}

		private static bool IsFissionProcessController(Part prefab, ProtoPartModuleSnapshot module, PartModule processPrefab)
		{
			if (processPrefab is ProcessControllerSystemHeat heatPrefab)
				return heatPrefab.IsFissionReactor();

			float meltdown = GetProcessField(prefab, module, "meltdownTemperature", 0f);
			float maximum = GetProcessField(prefab, module, "MaximumTemperature", 0f);
			return ProcessControllerSystemHeat.HasCoreDamageConfig(meltdown, maximum);
		}

		private static ProtoPartModuleSnapshot FindMatchingProcessModuleSnapshot(ProtoPartSnapshot part, string resource)
		{
			foreach (ProtoPartModuleSnapshot module in part.modules)
			{
				if (module.moduleName != "ProcessControllerSystemHeat")
					continue;

				if (Lib.Proto.GetString(module, "resource") == resource)
					return module;
			}

			return null;
		}

		private static void ClearFrozenFissionPseudoResource(ProtoPartResourceSnapshot pseudoResource)
		{
			pseudoResource.flowState = false;
			if (pseudoResource.amount > 0.0)
				pseudoResource.amount = 0.0;
		}

		private static void SetPseudoResourceFlow(ProtoPartSnapshot part, ProtoPartModuleSnapshot module, PartModule processPrefab, bool flowState)
		{
			string resource = processPrefab != null
				? IntegrationReflection.GetString(processPrefab, "resource", Lib.Proto.GetString(module, "resource"))
				: Lib.Proto.GetString(module, "resource");
			ProtoPartResourceSnapshot pseudoResource = FindPartResource(part, resource);
			if (pseudoResource == null)
				return;

			if (!flowState)
				ClearFrozenFissionPseudoResource(pseudoResource);
			else
				pseudoResource.flowState = true;
		}

		private static bool PartHasModule(ProtoPartSnapshot part, string moduleName)
		{
			foreach (ProtoPartModuleSnapshot module in part.modules)
			{
				if (module.moduleName == moduleName)
					return true;
			}
			return false;
		}

		private static bool IsProcessOperational(ProtoPartSnapshot part, Part prefab, ProtoPartModuleSnapshot module, PartModule processPrefab)
		{
			if (Lib.Proto.GetBool(module, "broken") || !Lib.Proto.GetBool(module, "running"))
				return false;

			bool requireDeploy = processPrefab != null
				? IntegrationReflection.GetBool(processPrefab, "requireDeploy", false)
				: Lib.Proto.GetBool(module, "requireDeploy");

			if (requireDeploy && !Lib.IsEditor() && prefab.HasModuleImplementingFast<ModuleAnimationGroup>())
			{
				ProtoPartModuleSnapshot animator = IntegrationUtils.TryFindPartModuleSnapshot(part, "ModuleAnimationGroup");
				if (animator != null)
				{
					if (!Lib.Proto.GetBool(animator, "isDeployed"))
						return false;
				}
				else if (!Lib.Proto.GetBool(module, "deployed"))
				{
					return false;
				}
			}

			return true;
		}

		private static float GetProcessField(Part prefab, ProtoPartModuleSnapshot module, string fieldName, float fallback)
		{
			string resource = Lib.Proto.GetString(module, "resource");
			foreach (PartModule pm in prefab.Modules)
			{
				if (pm.moduleName != "ProcessControllerSystemHeat")
					continue;
				if (string.IsNullOrEmpty(resource) || IntegrationReflection.GetString(pm, "resource") == resource)
					return IntegrationReflection.GetFloat(pm, fieldName, fallback);
			}
			return Lib.Proto.GetFloat(module, fieldName, fallback);
		}

		private static float GetHarvesterHeatPower(Part prefab, ProtoPartModuleSnapshot module)
		{
			string resource = Lib.Proto.GetString(module, "resource");
			foreach (PartModule pm in prefab.Modules)
			{
				if (pm.moduleName != "HarvesterSystemHeat")
					continue;
				if (string.IsNullOrEmpty(resource) || IntegrationReflection.GetString(pm, "resource") == resource)
					return IntegrationReflection.GetFloat(pm, "systemPower");
			}
			return Lib.Proto.GetFloat(module, "systemPower");
		}

		private static float GetHarvesterField(Part prefab, ProtoPartModuleSnapshot module, string fieldName, float fallback)
		{
			string resource = Lib.Proto.GetString(module, "resource");
			foreach (PartModule pm in prefab.Modules)
			{
				if (pm.moduleName != "HarvesterSystemHeat")
					continue;
				if (string.IsNullOrEmpty(resource) || IntegrationReflection.GetString(pm, "resource") == resource)
					return IntegrationReflection.GetFloat(pm, fieldName, fallback);
			}
			return Lib.Proto.GetFloat(module, fieldName, fallback);
		}

		private static void EnsureUnloadedFissionLoopSimulated(Vessel v, float elapsed_s)
		{
			TryRun(v, elapsed_s);
		}

		private static string GetProcessResourceName(ProtoPartModuleSnapshot module, PartModule processPrefab)
		{
			if (processPrefab != null)
				return IntegrationReflection.GetString(processPrefab, "resource", Lib.Proto.GetString(module, "resource"));
			return Lib.Proto.GetString(module, "resource");
		}

		private static void SetProtoFissionRunning(Vessel v, ProtoPartSnapshot part, ProtoPartModuleSnapshot module, bool value)
		{
			if (Lib.Proto.GetBool(module, nameof(ProcessController.running)) == value)
				return;

			Lib.Proto.Set(module, nameof(ProcessController.running), value);
			if (!value)
				Lib.Proto.Set(module, nameof(ProcessControllerSystemHeat.CurrentPowerPercent), 0f);
		}

		private static void TryAddSpaceDustHarvesterHeat(ProtoPartSnapshot part, Part prefab, ProtoPartModuleSnapshot harvesterModule, Dictionary<int, LoopState> loops)
		{
			if (!PartHasModule(part, "SpaceDustHarvesterKerbalismUpdater") || !Lib.Proto.GetBool(harvesterModule, "Enabled"))
				return;

			PartModule harvesterPrefab = FindMatchingPrefabModule(prefab, harvesterModule, "ModuleSpaceDustHarvester")
				?? FindPrefabModule(prefab, "ModuleSpaceDustHarvester");
			if (harvesterPrefab == null)
				return;

			float systemPower = SpaceDust.Get(harvesterPrefab, "SystemPower", 0f);
			if (systemPower <= 0f)
				return;

			RegisterProducer(loops, part, prefab, harvesterModule, SpaceDust.Get(harvesterPrefab, "HeatModuleID", ""),
				systemPower,
				SpaceDust.Get(harvesterPrefab, "SystemOutletTemperature", 0f),
				SpaceDust.Get(harvesterPrefab, "ShutdownTemperature", float.MaxValue));
		}

		private static bool IsHeatSinkOperational(ProtoPartSnapshot part, ProtoPartModuleSnapshot sinkModule)
		{
			if (!Lib.Proto.GetBool(sinkModule, "storageEnabled", true))
				return false;

			foreach (ProtoPartModuleSnapshot module in part.modules)
			{
				if (module.moduleName != "Reliability" || !Lib.Proto.GetBool(module, "broken"))
					continue;

				if (Lib.Proto.GetString(module, "type") == "ModuleSystemHeatSink")
					return false;
			}

			return true;
		}

		private static float GetFissionSafetyOverride(Part prefab, ProtoPartModuleSnapshot module, PartModule processPrefab)
		{
			float protoOverride = Lib.Proto.GetFloat(module, "CurrentSafetyOverride", 0f);
			if (protoOverride > 0f)
				return protoOverride;

			if (processPrefab != null)
			{
				float prefabOverride = IntegrationReflection.GetFloat(processPrefab, "CurrentSafetyOverride", 0f);
				if (prefabOverride > 0f)
					return prefabOverride;
			}

			float meltdown = GetProcessField(prefab, module, "meltdownTemperature", 1300f);
			return meltdown > 0f ? meltdown : 1000f;
		}

		private static PartModule FindMatchingPrefabModule(Part prefab, ProtoPartModuleSnapshot module, string moduleName)
		{
			string resource = Lib.Proto.GetString(module, "resource");
			foreach (PartModule pm in prefab.Modules)
			{
				if (pm.moduleName != moduleName)
					continue;
				if (string.IsNullOrEmpty(resource) || IntegrationReflection.GetString(pm, "resource") == resource)
					return pm;
			}
			return null;
		}

		private static ProtoPartModuleSnapshot FindFissionEngineSnapshot(ProtoPartSnapshot part, ProtoPartModuleSnapshot updaterModule)
		{
			string moduleId = Lib.Proto.GetString(updaterModule, "engineModuleID");
			ProtoPartModuleSnapshot fallback = null;
			foreach (ProtoPartModuleSnapshot module in part.modules)
			{
				if (module.moduleName != "ModuleSystemHeatFissionEngine")
					continue;

				if (fallback == null)
					fallback = module;
				if (!string.IsNullOrEmpty(moduleId) && Lib.Proto.GetString(module, "moduleID") == moduleId)
					return module;
			}
			return fallback;
		}

		private static PartModule FindFissionEnginePrefab(Part prefab, ProtoPartModuleSnapshot engineModule)
		{
			string moduleId = Lib.Proto.GetString(engineModule, "moduleID");
			PartModule fallback = null;
			foreach (PartModule engine in prefab.Modules)
			{
				if (engine.moduleName != "ModuleSystemHeatFissionEngine")
					continue;
				if (fallback == null)
					fallback = engine;
				if (string.IsNullOrEmpty(moduleId) || GetModuleId(engine) == moduleId)
					return engine;
			}
			return fallback;
		}

		private static bool TryResolveFissionEngineHeatLink(ProtoPartSnapshot part, Part prefab, PartModule enginePrefab, out HeatLink link)
		{
			if (enginePrefab != null && TryResolveHeatLink(part, prefab, GetSystemHeatModuleId(enginePrefab), out link))
				return true;

			foreach (PartModule heatEngine in prefab.Modules)
			{
				if (heatEngine.moduleName != "ModuleSystemHeatEngine")
					continue;
				if (TryResolveHeatLink(part, prefab, GetSystemHeatModuleId(heatEngine), out link))
					return true;
			}

			// A part with a single heat module needs no id.
			link = default;
			return CountPrefabHeatModules(prefab) == 1 && TryResolveHeatLink(part, prefab, "", out link);
		}

		private static int CountPrefabHeatModules(Part prefab)
		{
			int count = 0;
			for (int i = 0; i < prefab.Modules.Count; i++)
			{
				PartModule heat = prefab.Modules[i];
				if (heat != null && heat.moduleName == "ModuleSystemHeat")
					count++;
			}
			return count;
		}

		private static float GetNativeFissionSafetyOverride(PartModule reactorPrefab, ProtoPartModuleSnapshot reactorModule)
		{
			float protoOverride = Lib.Proto.GetFloat(reactorModule, "CurrentSafetyOverride", 0f);
			if (protoOverride > 0f)
				return protoOverride;

			return reactorPrefab != null ? IntegrationReflection.GetFloat(reactorPrefab, "CriticalTemperature", 1300f) : 1300f;
		}

		private static float GetNativeFissionCriticalTemperature(PartModule reactorPrefab, ProtoPartModuleSnapshot reactorModule)
		{
			float protoCritical = Lib.Proto.GetFloat(reactorModule, "CriticalTemperature", 0f);
			if (protoCritical > 0f)
				return protoCritical;

			return reactorPrefab != null ? IntegrationReflection.GetFloat(reactorPrefab, "CriticalTemperature", 1300f) : 1300f;
		}

		private static float GetNativeFissionMaximumTemperature(PartModule reactorPrefab, ProtoPartModuleSnapshot reactorModule)
		{
			float protoMaximum = Lib.Proto.GetFloat(reactorModule, "MaximumTemperature", 0f);
			if (protoMaximum > 0f)
				return protoMaximum;

			return reactorPrefab != null ? IntegrationReflection.GetFloat(reactorPrefab, "MaximumTemperature", 2000f) : 2000f;
		}

		private static float GetReactorWasteHeat(PartModule reactorPrefab, float throttlePercent)
		{
			if (reactorPrefab == null)
				return 0f;

			float heat = EvaluateCurveField(reactorPrefab, "HeatGeneration", throttlePercent);
			float elec = EvaluateCurveField(reactorPrefab, "ElectricalGeneration", throttlePercent);
			return Math.Max(0f, heat - elec);
		}

		private static PartModule FindCryoTankPrefab(Part prefab, ProtoPartModuleSnapshot module)
		{
			string moduleId = Lib.Proto.GetString(module, "moduleID");
			PartModule fallback = null;

			foreach (PartModule cryo in prefab.Modules)
			{
				if (cryo.moduleName != "ModuleSystemHeatCryoTank")
					continue;
				if (fallback == null)
					fallback = cryo;
				if (string.IsNullOrEmpty(moduleId) || GetModuleId(cryo) == moduleId)
					return cryo;
			}

			return fallback;
		}

		private static PartModule FindHeatSinkPrefab(Part prefab, ProtoPartModuleSnapshot module)
		{
			string moduleId = Lib.Proto.GetString(module, "moduleID");
			PartModule fallback = null;

			foreach (PartModule sink in prefab.Modules)
			{
				if (sink.moduleName != "ModuleSystemHeatSink")
					continue;
				if (fallback == null)
					fallback = sink;
				if (string.IsNullOrEmpty(moduleId) || GetModuleId(sink) == moduleId)
					return sink;
			}

			return fallback;
		}

		private static float GetCryoTankCoolingHeatPower(ProtoPartSnapshot part, ProtoPartModuleSnapshot module, PartModule cryoPrefab, float loopTemperature)
		{
			if (!Lib.Proto.GetBool(module, "CoolingEnabled") || !Lib.Proto.GetBool(module, "CoolingAllowed"))
				return 0f;

			IList fuels = IntegrationReflection.GetField<IList>(cryoPrefab, "fuels");
			if (fuels == null)
				return 0f;

			double fuelAmount = 0.0;
			float heatCost = IntegrationReflection.GetFloat(cryoPrefab, "CoolingHeatCost");
			float maxCryoTemperature = 0f;
			foreach (object fuel in fuels)
			{
				if (fuel == null)
					continue;

				Type fuelType = fuel.GetType();
				string fuelName = ReadField<string>(fuel, fuelType, "fuelName");
				if (string.IsNullOrEmpty(fuelName))
					continue;

				ProtoPartResourceSnapshot protoFuel = part.resources.Find(r => r.resourceName == fuelName);
				if (protoFuel == null || protoFuel.amount <= double.Epsilon)
					continue;

				fuelAmount += protoFuel.amount;
				float cryoTemperature = ReadField<float>(fuel, fuelType, "cryoTemperature");
				if (cryoTemperature <= 0f)
					cryoTemperature = ReadField<float>(fuel, fuelType, "CryocoolerTemperature");
				if (cryoTemperature > maxCryoTemperature)
					maxCryoTemperature = cryoTemperature;

				float entryCost = ReadField<float>(fuel, fuelType, "coolingHeatCost");
				if (entryCost <= 0f)
					entryCost = ReadField<float>(fuel, fuelType, "CoolingHeatCost");
				if (entryCost > 0f)
					heatCost = Math.Max(heatCost, entryCost);
			}

			if (fuelAmount <= double.Epsilon || heatCost <= 0f)
				return 0f;

			if (maxCryoTemperature > 0f && loopTemperature > maxCryoTemperature)
				return 0f;

			return (float)(heatCost * fuelAmount * 0.001);
		}

		private static ProtoPartResourceSnapshot FindPartResource(ProtoPartSnapshot part, string resource)
		{
			if (part == null || part.resources == null || string.IsNullOrEmpty(resource))
				return null;

			for (int i = 0; i < part.resources.Count; i++)
			{
				ProtoPartResourceSnapshot res = part.resources[i];
				if (res != null && res.resourceName == resource)
					return res;
			}
			return null;
		}

		private static bool TryGetFusionReactorHeatConfig(Part prefab, out string heatModuleId, out float systemPower)
		{
			heatModuleId = "";
			systemPower = 0f;

			foreach (string moduleName in FusionReactorModuleNames)
			{
				PartModule module = FindPrefabModule(prefab, moduleName);
				if (module == null)
					continue;

				Type type = module.GetType();
				heatModuleId = ReadField<string>(module, type, "HeatModuleID") ?? "";
				systemPower = ReadField<float>(module, type, "SystemPower");
				return systemPower > 0f;
			}
			return false;
		}

		private static string GetModuleId(PartModule module)
		{
			return IntegrationReflection.GetString(module, "moduleID");
		}

		private static string GetSystemHeatModuleId(PartModule module, string fallback = "")
		{
			return IntegrationReflection.GetString(module, "systemHeatModuleID", fallback);
		}

		private static float EvaluateCurveField(PartModule module, string fieldName, float x)
		{
			FloatCurve curve = IntegrationReflection.GetField<FloatCurve>(module, fieldName);
			return curve == null ? 0f : curve.Evaluate(x);
		}

		private static PartModule FindPrefabModule(Part prefab, string moduleName)
		{
			foreach (PartModule module in prefab.Modules)
			{
				if (module.moduleName == moduleName)
					return module;
			}
			return null;
		}

		private static bool HasNoWasteHeatSubtype(ProtoPartSnapshot part)
		{
			foreach (ProtoPartModuleSnapshot module in part.modules)
			{
				if (module.moduleName == "ModuleB9PartSwitch" && Lib.Proto.GetString(module, "currentSubtype") == "Size0Radiators")
					return true;
			}
			return false;
		}

		private static T ReadField<T>(PartModule module, Type type, string fieldName)
		{
			FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field == null)
				return default;
			object value = field.GetValue(module);
			return value is T typed ? typed : default;
		}

		private static T ReadField<T>(object target, Type type, string fieldName)
		{
			if (target == null || type == null)
				return default;

			FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field == null)
				return default;
			object value = field.GetValue(target);
			return value is T typed ? typed : default;
		}

		#endregion
	}
}
