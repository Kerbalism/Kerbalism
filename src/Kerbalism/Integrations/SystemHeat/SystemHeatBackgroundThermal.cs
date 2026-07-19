using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace KERBALISM
{
	/// <summary>
	/// Minimal offline thermal simulation for SystemHeat loops on unloaded vessels.
	/// </summary>
	public static class SystemHeatBackgroundThermal
	{
		private static readonly Dictionary<Guid, double> lastRunTime = new Dictionary<Guid, double>();

		private static readonly string[] FusionReactorModuleNames = { "FusionReactor", "ModuleFusionEngine" };

		internal static bool Enabled = true;
		internal static float RadiatorCoefficient = 1f;
		private const float TransientTemperatureTolerance = 5f;
		private const float FluxEpsilonKw = 0.01f;
		private const float CoolantDensity = 1f;
		private const float CoolantHeatCapacity = 4.18f;
		private const float MaxThermalStepSeconds = 10f;
		private const string FluxAnchorKwField = "backgroundFluxAnchorKw";
		private const string FluxAnchorTemperatureField = "backgroundFluxAnchorTemperature";
		private const string FluxAnchorValidField = "backgroundFluxAnchorValid";
		/// <summary>Hard floor for loop temperature integration (space baseline), not ambient environment.</summary>
		private const float MinimumLoopTemperatureK = 4f;
		/// <summary>Stock SystemHeat radiator patches reach their rated rejection at 400 K.</summary>
		private const float StockRadiatorRatedTemperatureK = 400f;

		public static void CaptureLoadedTemperatures(Vessel v)
		{
			if (!Enabled || v == null || !v.loaded)
				return;

			foreach (Part part in v.parts)
			{
				if (part == null || part.protoPartSnapshot == null)
					continue;

				CaptureLoadedFissionReactorState(part);

				foreach (PartModule module in part.Modules)
				{
					if (module == null || !IsLoadedHeatLoopModule(module))
						continue;

					ProtoPartModuleSnapshot protoModule = FindMatchingLoadedHeatModuleSnapshot(part.protoPartSnapshot, module);
					if (protoModule == null)
						continue;

					float temperature = SystemHeat.CurrentLoopTemperature(module, 0f);
					float flux = SystemHeat.Get(module, "currentLoopFlux", Lib.Proto.GetFloat(protoModule, "currentLoopFlux"));
					if (temperature > 0f)
						Lib.Proto.Set(protoModule, "currentLoopTemperature", temperature);

					Lib.Proto.Set(protoModule, "currentLoopFlux", flux);
					CaptureFluxAnchorOnHeatModule(protoModule, temperature, flux);
				}
			}
		}

		/// <summary>
		/// Sync NFE fission ProcessController state into proto before the vessel packs so background
		/// automation and Profile modifiers see the same running flag as the loaded part.
		/// </summary>
		public static void CaptureLoadedFissionReactorState(Part part)
		{
			foreach (ProcessControllerSystemHeat process in part.FindModulesImplementing<ProcessControllerSystemHeat>())
			{
				if (process == null || process.resource != "_Nukereactor")
					continue;

				ProtoPartModuleSnapshot protoModule = FindMatchingProcessModuleSnapshot(part.protoPartSnapshot, process.resource);
				if (protoModule == null)
					continue;

				Lib.Proto.Set(protoModule, nameof(ProcessController.running), process.running);
				Lib.Proto.Set(protoModule, nameof(ProcessController.broken), process.broken);
				Lib.Proto.Set(protoModule, nameof(ProcessControllerSystemHeat.CurrentPowerPercent), process.CurrentPowerPercent);
				Lib.Proto.Set(protoModule, nameof(ProcessControllerSystemHeat.CoreDamage), process.CoreDamage);

				if (!part.Resources.Contains(process.resource))
					continue;

				PartResource pseudo = part.Resources[process.resource];
				ProtoPartResourceSnapshot protoResource = part.protoPartSnapshot.resources.Find(k => k.resourceName == process.resource);
				if (protoResource == null)
					continue;

				protoResource.flowState = pseudo.flowState;
				protoResource.amount = pseudo.amount;
				protoResource.maxAmount = pseudo.maxAmount;
			}
		}

		/// <summary>Sync fission reactor proto before leaving the flight scene.</summary>
		public static void CaptureAllLoadedFissionReactors()
		{
			if (!Enabled || !HighLogic.LoadedSceneIsFlight)
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

		/// <summary>
		/// Refresh frozen fission reactor pseudo-resources before Profile rules run on unloaded vessels.
		/// </summary>
		public static void PrepareFrozenFissionReactors(Vessel v, double elapsed_s)
		{
			if (!Enabled || v == null || v.loaded || elapsed_s <= 0f)
				return;

			foreach (ProtoPartSnapshot part in v.protoVessel.protoPartSnapshots)
			{
				Part prefab = PartLoader.getPartInfoByName(part.partName).partPrefab;

				foreach (ProtoPartModuleSnapshot module in part.modules)
				{
					if (module.moduleName != "ProcessControllerSystemHeat"
						|| Lib.Proto.GetString(module, "resource") != "_Nukereactor")
						continue;

					if (part.resources.Find(k => k.resourceName == "_Nukereactor") == null)
						continue;

					PartModule processPrefab = FindMatchingPrefabModule(prefab, module, "ProcessControllerSystemHeat");
					SyncFrozenProcessReactor(v, part, module, processPrefab, prefab, elapsed_s);
				}
			}
		}

		private static void CaptureFluxAnchorOnHeatModule(ProtoPartModuleSnapshot protoModule, float temperature, float flux)
		{
			// ModuleSystemHeat UI flux is often gross producer input (+MW) or radiator capacity (-MW),
			// not net loop balance. At a stable operating temperature net loop flux is ~0.
			if (Lib.Proto.GetBool(protoModule, "ignoreTemperature", false))
				flux = 0f;
			else if (flux > FluxEpsilonKw && temperature > 0f)
				flux = 0f;

			Lib.Proto.Set(protoModule, FluxAnchorKwField, flux);
			Lib.Proto.Set(protoModule, FluxAnchorTemperatureField, temperature > 0f ? temperature : 0f);
			Lib.Proto.Set(protoModule, FluxAnchorValidField, true);
		}

		private static void TryCaptureFluxAnchorOnLoop(LoopState loop, ProtoPartModuleSnapshot heatModule)
		{
			if (Lib.Proto.GetBool(heatModule, FluxAnchorValidField))
			{
				bool ignoreTemperature = Lib.Proto.GetBool(heatModule, "ignoreTemperature", false);
				float temperature = Lib.Proto.GetFloat(heatModule, FluxAnchorTemperatureField);
				float flux = Lib.Proto.GetFloat(heatModule, FluxAnchorKwField);

				if (ignoreTemperature)
				{
					if (temperature > 0f)
						loop.anchorTemperature = temperature;
					if (!loop.hasFluxAnchor)
					{
						loop.anchorFluxKw = 0f;
						loop.hasFluxAnchor = temperature > 0f;
					}
					return;
				}

				loop.anchorFluxKw = flux;
				if (temperature > 0f)
					loop.anchorTemperature = temperature;
				loop.hasFluxAnchor = true;
				return;
			}

			// Save can run before scene-switch capture; persisted loop temperature is still the flight equilibrium.
			float loopTemp = Lib.Proto.GetFloat(heatModule, "currentLoopTemperature");
			if (loopTemp > 0f && !loop.hasFluxAnchor)
			{
				loop.anchorTemperature = loopTemp;
				loop.anchorFluxKw = 0f;
				loop.hasFluxAnchor = true;
			}
		}

		private static bool IsLoadedHeatLoopModule(PartModule module)
		{
			return module.moduleName == "ModuleSystemHeat" || SystemHeat.IsModuleSystemHeat(module);
		}

		private static ProtoPartModuleSnapshot FindMatchingLoadedHeatModuleSnapshot(ProtoPartSnapshot protoPart, PartModule module)
		{
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

		public static void TryRun(Vessel v, double elapsed_s)
		{
			if (!Enabled || v == null || elapsed_s <= 0.0 || v.loaded)
				return;

			double now = Planetarium.GetUniversalTime();
			if (lastRunTime.TryGetValue(v.id, out double last) && last == now)
				return;
			lastRunTime[v.id] = now;

			SimulateVessel(v, (float)elapsed_s);
		}

		public static void SyncFrozenProcessReactor(Vessel v, ProtoPartSnapshot part, ProtoPartModuleSnapshot module, PartModule processPrefab, Part partPrefab, double elapsed_s)
		{
			if (v == null || part == null || module == null || partPrefab == null || v.loaded)
				return;

			if (Lib.Proto.GetString(module, "resource") != "_Nukereactor")
				return;

			ProtoPartResourceSnapshot pseudoResource = part.resources.Find(k => k.resourceName == "_Nukereactor");
			if (pseudoResource == null)
				return;

			if (Lib.Proto.GetBool(module, "broken"))
			{
				pseudoResource.flowState = false;
				return;
			}

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

		private class LoopState
		{
			internal float volume;
			internal float temperature;
			internal float previousTemperature;
			internal float netFluxKw;
			internal float producerFluxKw;
			internal float heatSinkFluxOffsetKw;
			internal float outletTemperature;
			internal float shutdownTemperature = float.MaxValue;
			internal bool hasActiveProducer;
			internal bool hasRadiator;
			internal bool hasFluxAnchor;
			internal float anchorFluxKw;
			internal float anchorTemperature;
			internal readonly List<ProtoPartModuleSnapshot> heatModules = new List<ProtoPartModuleSnapshot>();
			internal readonly List<HeatProducer> heatProducers = new List<HeatProducer>();
			internal readonly List<HeatSink> heatSinks = new List<HeatSink>();
			internal readonly List<RadiatorRejector> radiators = new List<RadiatorRejector>();
		}

		private class RadiatorRejector
		{
			internal ProtoPartSnapshot part;
			internal Part prefab;
			internal ProtoPartModuleSnapshot module;
		}

		private class HeatProducer
		{
			internal ProtoPartSnapshot part;
			internal ProtoPartModuleSnapshot module;
			internal float shutdownTemperature;
			internal float meltdownTemperature;
			internal float maximumTemperature;
			internal float coreDamageRate;
			internal FloatCurve coreDamageCurve;
		}

		private class HeatSink
		{
			internal ProtoPartSnapshot part;
			internal ProtoPartModuleSnapshot module;
			internal PartModule prefab;
		}

		private static void SimulateVessel(Vessel v, float elapsed_s)
		{
			var loops = new Dictionary<int, LoopState>();
			var riskLoopIds = new HashSet<int>();
			var temperatureSensitiveLoopIds = new HashSet<int>();

			foreach (ProtoPartSnapshot part in v.protoVessel.protoPartSnapshots)
			{
				Part prefab = PartLoader.getPartInfoByName(part.partName).partPrefab;

				foreach (ProtoPartModuleSnapshot module in part.modules)
				{
					if (module.moduleName == "ModuleSystemHeat")
					{
						int loopId = Lib.Proto.GetInt(module, "currentLoopID");
						float loopTemp = Lib.Proto.GetFloat(module, "currentLoopTemperature");
						float volume = GetModuleVolume(prefab, module);

						if (!loops.TryGetValue(loopId, out LoopState loop))
						{
							loop = new LoopState
							{
								temperature = loopTemp > 0f ? loopTemp : GetFallbackLoopTemperature()
							};
							loops[loopId] = loop;
						}

						loop.volume += volume;
						if (loopTemp > 0f)
							loop.temperature = loopTemp;
						loop.heatModules.Add(module);
						TryCaptureFluxAnchorOnLoop(loop, module);
					}
					else if (module.moduleName == "ProcessControllerSystemHeat")
					{
						PartModule processPrefab = FindMatchingPrefabModule(prefab, module, "ProcessControllerSystemHeat");
						int loopId = GetLinkedLoopId(part, prefab, Lib.Proto.GetString(module, "systemHeatModuleID"));
						if (loopId < 0)
							continue;

						bool isFissionProcess = Lib.Proto.GetString(module, "resource") == "_Nukereactor";
						float meltdown = GetProcessField(prefab, module, "meltdownTemperature", 0f);
						float maximum = GetProcessField(prefab, module, "MaximumTemperature", 0f);
						if (meltdown > 0f && maximum > meltdown)
							riskLoopIds.Add(loopId);

						float shutdown = isFissionProcess
							? GetFissionSafetyOverride(prefab, module, processPrefab)
							: GetProcessField(prefab, module, "shutdownTemperature", float.MaxValue);
						EnsureLoop(loops, loopId, v);
						LoopState loop = loops[loopId];
						loop.shutdownTemperature = Math.Min(loop.shutdownTemperature, shutdown);
						loop.heatProducers.Add(new HeatProducer
						{
							part = part,
							module = module,
							shutdownTemperature = shutdown,
							meltdownTemperature = meltdown,
							maximumTemperature = maximum > 0f ? maximum : 2000f,
							coreDamageRate = GetProcessField(prefab, module, "CoreDamageRate", 0f),
							coreDamageCurve = IntegrationReflection.GetField(processPrefab, "coreDamageCurve", new FloatCurve())
						});

						if (!IsProcessOperational(part, prefab, module, processPrefab))
							continue;

						float power = GetProcessHeatPower(part, prefab, module, processPrefab) * GetProcessThrottle(module);
						loop.producerFluxKw += power;
						MarkActiveProducer(loop, IntegrationReflection.GetFloat(processPrefab, "systemOutletTemperature", GetProcessField(prefab, module, "systemOutletTemperature", 0f)), power);
					}
					else if (module.moduleName == "HarvesterSystemHeat")
					{
						if (!Lib.Proto.GetBool(module, "deployed") || !Lib.Proto.GetBool(module, "running") || Lib.Proto.GetString(module, "issue").Length > 0)
							continue;

						float power = GetHarvesterHeatPower(prefab, module);
						int loopId = GetLinkedLoopId(part, prefab, Lib.Proto.GetString(module, "systemHeatModuleID"));
						if (loopId < 0)
							continue;

						float shutdown = GetHarvesterField(prefab, module, "shutdownTemperature", float.MaxValue);
						EnsureLoop(loops, loopId, v);
						LoopState loop = loops[loopId];
						loop.producerFluxKw += power;
						loop.shutdownTemperature = Math.Min(loop.shutdownTemperature, shutdown);
						loop.heatProducers.Add(new HeatProducer { part = part, module = module, shutdownTemperature = shutdown });
						MarkActiveProducer(loop, GetHarvesterField(prefab, module, "systemOutletTemperature", 0f), power);
					}
					else if (module.moduleName == "SystemHeatRadiatorKerbalism")
					{
						if (!IsRadiatorOperational(part, prefab, module))
							continue;

						int loopId = GetRadiatorLoopId(part, prefab, module);
						if (loopId < 0)
							continue;

						EnsureLoop(loops, loopId, v);
						LoopState loop = loops[loopId];
						RegisterLoopRadiator(loop, part, prefab, module);
					}
					else if (module.moduleName == "ModuleSystemHeatRadiator" || module.moduleName == "ModuleActiveRadiator")
					{
						if (IntegrationUtils.TryFindPartModuleSnapshot(part, "SystemHeatRadiatorKerbalism") != null)
							continue;

						if (!IsNativeRadiatorOperational(part, prefab, module))
							continue;

						int loopId = GetNativeRadiatorLoopId(part, prefab, module);
						if (loopId < 0)
							continue;

						EnsureLoop(loops, loopId, v);
						RegisterLoopRadiator(loops[loopId], part, prefab, module);
					}
					else if (module.moduleName == "SystemHeatConverterKerbalismUpdater")
					{
						ProtoPartModuleSnapshot converter = IntegrationUtils.TryFindPartModuleSnapshot(part, "ModuleSystemHeatConverter");
						if (converter == null || !Lib.Proto.GetBool(converter, "IsActivated"))
							continue;

						PartModule converterPrefab = FindPrefabModule(prefab, "ModuleSystemHeatConverter");
						if (converterPrefab == null)
							continue;

						int loopId = GetLinkedLoopId(part, prefab, GetSystemHeatModuleId(converterPrefab));
						if (loopId < 0)
							continue;

						EnsureLoop(loops, loopId, v);
						LoopState loop = loops[loopId];
						loop.producerFluxKw += IntegrationReflection.GetFloat(converterPrefab, "systemPower");
						loop.shutdownTemperature = Math.Min(loop.shutdownTemperature, IntegrationReflection.GetFloat(converterPrefab, "shutdownTemperature", float.MaxValue));
						loop.heatProducers.Add(new HeatProducer { part = part, module = converter, shutdownTemperature = IntegrationReflection.GetFloat(converterPrefab, "shutdownTemperature", float.MaxValue) });
						MarkActiveProducer(loop, IntegrationReflection.GetFloat(converterPrefab, "systemOutletTemperature"), IntegrationReflection.GetFloat(converterPrefab, "systemPower"));
					}
					else if (module.moduleName == "SystemHeatHarvesterKerbalismUpdater")
					{
						ProtoPartModuleSnapshot harvester = IntegrationUtils.TryFindPartModuleSnapshot(part, "ModuleSystemHeatHarvester");
						if (harvester == null || !Lib.Proto.GetBool(harvester, "IsActivated"))
							continue;

						PartModule harvesterPrefab = FindPrefabModule(prefab, "ModuleSystemHeatHarvester");
						if (harvesterPrefab == null)
							continue;

						int loopId = GetLinkedLoopId(part, prefab, GetSystemHeatModuleId(harvesterPrefab));
						if (loopId < 0)
							continue;

						EnsureLoop(loops, loopId, v);
						LoopState loop = loops[loopId];
						loop.producerFluxKw += IntegrationReflection.GetFloat(harvesterPrefab, "systemPower");
						loop.shutdownTemperature = Math.Min(loop.shutdownTemperature, IntegrationReflection.GetFloat(harvesterPrefab, "shutdownTemperature", float.MaxValue));
						loop.heatProducers.Add(new HeatProducer { part = part, module = harvester, shutdownTemperature = IntegrationReflection.GetFloat(harvesterPrefab, "shutdownTemperature", float.MaxValue) });
						MarkActiveProducer(loop, IntegrationReflection.GetFloat(harvesterPrefab, "systemOutletTemperature"), IntegrationReflection.GetFloat(harvesterPrefab, "systemPower"));
					}
					else if (module.moduleName == "ModuleSpaceDustHarvester")
					{
						TryAddSpaceDustHarvesterHeat(part, prefab, module, loops, riskLoopIds, v, true);
					}
					else if (module.moduleName == "SystemHeatFissionReactorKerbalismUpdater")
					{
						ProtoPartModuleSnapshot reactor = IntegrationUtils.FindPartModuleSnapshot(part, "ModuleSystemHeatFissionReactor");
						if (reactor == null)
							continue;

						PartModule reactorPrefab = FindPrefabModule(prefab, "ModuleSystemHeatFissionReactor");
						string heatModuleId = reactorPrefab != null ? GetSystemHeatModuleId(reactorPrefab) : "reactor";
						int loopId = GetLinkedLoopId(part, prefab, heatModuleId);
						if (loopId < 0)
							continue;

						EnsureLoop(loops, loopId, v);
						LoopState loop = loops[loopId];
						float critical = GetNativeFissionCriticalTemperature(reactorPrefab, reactor);
						bool enabled = Lib.Proto.GetBool(reactor, "Enabled");
						bool loopIsCoreRisk = critical > 0f && loop.temperature > critical;
						if (!enabled && !loopIsCoreRisk)
							continue;

						riskLoopIds.Add(loopId);
						float shutdown = GetNativeFissionSafetyOverride(reactorPrefab, reactor);
						loop.shutdownTemperature = Math.Min(loop.shutdownTemperature, shutdown);
						loop.heatProducers.Add(new HeatProducer
						{
							part = part,
							module = reactor,
							shutdownTemperature = shutdown,
							meltdownTemperature = critical,
							maximumTemperature = GetNativeFissionMaximumTemperature(reactorPrefab, reactor)
						});
						if (enabled && loop.temperature <= shutdown)
						{
							float throttle = Lib.Proto.GetFloat(reactor, "CurrentReactorThrottle");
							float heat = GetReactorWasteHeat(reactorPrefab, throttle);
							loop.producerFluxKw += heat;
							MarkActiveProducer(loop, reactorPrefab != null ? IntegrationReflection.GetFloat(reactorPrefab, "NominalTemperature") : 0f, heat);
						}
					}
					else if (module.moduleName == "SystemHeatFissionEngineKerbalismUpdater")
					{
						ProtoPartModuleSnapshot engine = FindFissionEngineSnapshot(part, module);
						if (engine == null)
							continue;

						PartModule enginePrefab = FindFissionEnginePrefab(prefab, engine);
						int loopId = GetFissionEngineLoopId(part, prefab, enginePrefab);
						if (loopId < 0)
							continue;

						EnsureLoop(loops, loopId, v);
						LoopState loop = loops[loopId];
						float critical = GetNativeFissionCriticalTemperature(enginePrefab, engine);
						bool enabled = Lib.Proto.GetBool(engine, "Enabled");
						bool loopIsCoreRisk = critical > 0f && loop.temperature > critical;
						if (!enabled && !loopIsCoreRisk)
							continue;

						riskLoopIds.Add(loopId);
						float shutdown = GetNativeFissionSafetyOverride(enginePrefab, engine);
						loop.shutdownTemperature = Math.Min(loop.shutdownTemperature, shutdown);
						loop.heatProducers.Add(new HeatProducer
						{
							part = part,
							module = engine,
							shutdownTemperature = shutdown,
							meltdownTemperature = critical,
							maximumTemperature = GetNativeFissionMaximumTemperature(enginePrefab, engine)
						});

						if (enabled && loop.temperature <= shutdown)
						{
							float throttle = Lib.Proto.GetFloat(engine, "CurrentReactorThrottle");
							float heat = GetReactorWasteHeat(enginePrefab, throttle);
							loop.producerFluxKw += heat;
							MarkActiveProducer(loop, enginePrefab != null ? IntegrationReflection.GetFloat(enginePrefab, "NominalTemperature") : 0f, heat);
						}
					}
					else if (module.moduleName == "ModuleSystemHeatCryoTank")
					{
						if (!PartHasModule(part, "SystemHeatCryoTankKerbalismUpdater"))
							continue;

						PartModule cryoPrefab = FindCryoTankPrefab(prefab, module);
						if (cryoPrefab == null)
							continue;

						int loopId = GetLinkedLoopId(part, prefab, GetSystemHeatModuleId(cryoPrefab));
						if (loopId < 0)
							continue;

						EnsureLoop(loops, loopId, v);
						float loopTemperature = GetLinkedLoopTemperature(part, prefab, GetSystemHeatModuleId(cryoPrefab), v);
						float heat = GetCryoTankCoolingHeatPower(part, module, cryoPrefab, loopTemperature);
						if (heat <= 0f)
							continue;

						temperatureSensitiveLoopIds.Add(loopId);
						loops[loopId].producerFluxKw += heat;
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

						EnsureLoop(loops, loopId, v);
						loops[loopId].heatSinks.Add(new HeatSink
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

						EnsureLoop(loops, loopId, v);
						loops[loopId].producerFluxKw += systemPower;
					}
				}
			}

			foreach (LoopState loop in loops.Values)
				SyncLoopNetFlux(loop);

			ApplyHeatSinkStorage(loops, elapsed_s);

			foreach (KeyValuePair<int, LoopState> entry in loops)
			{
				LoopState loop = entry.Value;
				if (loop.volume <= 0f)
					loop.volume = 1f;

				float thermalMass = loop.volume * CoolantDensity * CoolantHeatCapacity;
				if (thermalMass <= 0f)
					continue;

				loop.previousTemperature = loop.temperature;
				if (ShouldFreezeLoopAtAnchor(loop))
				{
					loop.temperature = loop.anchorTemperature;
					SyncLoopNetFlux(loop);

					foreach (ProtoPartModuleSnapshot heatModule in loop.heatModules)
					{
						Lib.Proto.Set(heatModule, "currentLoopTemperature", loop.temperature);
						Lib.Proto.Set(heatModule, "currentLoopFlux", loop.netFluxKw);
					}

					ApplyLoopThermalEffects(v, loop, elapsed_s);
					continue;
				}

				AdvanceLoopTemperatureOverDuration(loop, thermalMass, elapsed_s);

				foreach (ProtoPartModuleSnapshot heatModule in loop.heatModules)
				{
					Lib.Proto.Set(heatModule, "currentLoopTemperature", loop.temperature);
					Lib.Proto.Set(heatModule, "currentLoopFlux", loop.netFluxKw);
				}

				ApplyLoopThermalEffects(v, loop, elapsed_s);
			}

		}

		private static bool ShouldFreezeLoopAtAnchor(LoopState loop)
		{
			return loop.hasFluxAnchor && loop.anchorTemperature > MinimumLoopTemperatureK;
		}

		private static void ApplyLoopThermalEffects(Vessel v, LoopState loop, float elapsed_s)
		{
			foreach (HeatProducer producer in loop.heatProducers)
				ApplyCoreDamage(v, producer, loop, elapsed_s);

			if (loop.temperature < loop.shutdownTemperature)
				return;

			foreach (HeatProducer producer in loop.heatProducers)
			{
				if (loop.temperature < producer.shutdownTemperature)
					continue;

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
		}

		private static void ShutdownProcessProducer(Vessel v, HeatProducer producer)
		{
			Part prefab = PartLoader.getPartInfoByName(producer.part.partName).partPrefab;
			PartModule processPrefab = FindMatchingPrefabModule(prefab, producer.module, "ProcessControllerSystemHeat");
			if (processPrefab != null && !IntegrationReflection.GetBool(processPrefab, "AutoShutdown", true))
				return;

			if (Lib.Proto.GetString(producer.module, "resource") == "_Nukereactor")
			{
				SetProtoFissionRunning(v, producer.part, producer.module, false);
				ProtoPartResourceSnapshot pseudo = producer.part.resources.Find(k => k.resourceName == "_Nukereactor");
				if (pseudo != null)
					ClearFrozenFissionPseudoResource(pseudo);
				return;
			}

			Lib.Proto.Set(producer.module, "running", false);
			SetPseudoResourceFlow(producer.part, producer.module, processPrefab, false);
		}

		private static void EnsureLoop(Dictionary<int, LoopState> loops, int loopId, Vessel v)
		{
			if (!loops.TryGetValue(loopId, out LoopState _))
			{
				loops[loopId] = new LoopState { temperature = GetFallbackLoopTemperature() };
			}
		}

		private static void MarkActiveProducer(LoopState loop, float outletTemperature, float power)
		{
			if (power <= 0f)
				return;

			loop.hasActiveProducer = true;
			if (outletTemperature > loop.outletTemperature)
				loop.outletTemperature = outletTemperature;
		}

		private static void ApplyHeatSinkStorage(Dictionary<int, LoopState> loops, float elapsed_s)
		{
			if (elapsed_s <= 0f)
				return;

			foreach (LoopState loop in loops.Values)
			{
				if (loop.heatSinks.Count == 0)
					continue;

				float netFlux = GetLoopNetFluxKw(loop, loop.temperature);
				if (netFlux <= FluxEpsilonKw)
					continue;

				for (int i = 0; i < loop.heatSinks.Count; i++)
				{
					netFlux = GetLoopNetFluxKw(loop, loop.temperature);
					if (netFlux <= FluxEpsilonKw)
						break;

					HeatSink sink = loop.heatSinks[i];
					float storedEnergy = StoreHeatInSink(sink, netFlux, elapsed_s);
					if (storedEnergy <= 0f)
						continue;

					loop.heatSinkFluxOffsetKw += storedEnergy / elapsed_s;
				}

				SyncLoopNetFlux(loop);
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

		private static void RegisterLoopRadiator(LoopState loop, ProtoPartSnapshot part, Part prefab, ProtoPartModuleSnapshot module)
		{
			loop.hasRadiator = true;
			loop.radiators.Add(new RadiatorRejector { part = part, prefab = prefab, module = module });
		}

		private static void SyncLoopNetFlux(LoopState loop)
		{
			loop.netFluxKw = GetLoopNetFluxKw(loop, loop.temperature);
		}

		private static float GetLoopNetFluxKw(LoopState loop, float loopTemperature)
		{
			return loop.producerFluxKw - loop.heatSinkFluxOffsetKw - GetRadiatorRejectTotal(loop, loopTemperature);
		}

		private static float GetRadiatorRejectTotal(LoopState loop, float loopTemperature)
		{
			int count = loop.radiators.Count;
			if (count == 0)
				return 0f;

			if (loopTemperature <= MinimumLoopTemperatureK)
				return 0f;

			float total = 0f;
			for (int i = 0; i < count; i++)
			{
				RadiatorRejector radiator = loop.radiators[i];
				total += GetRadiatorRejectPower(radiator.part, radiator.prefab, radiator.module, loopTemperature);
			}
			return total;
		}

		private static void AdvanceLoopTemperatureOverDuration(LoopState loop, float thermalMass, float elapsed_s, float maxTemperature = 5000f)
		{
			float remaining = elapsed_s;

			SyncLoopNetFlux(loop);

			while (remaining > 0f)
			{
				float step = Mathf.Min(remaining, MaxThermalStepSeconds);
				AdvanceLoopTemperature(loop, thermalMass, step, maxTemperature);
				remaining -= step;
			}
			SyncLoopNetFlux(loop);
		}

		private static void AdvanceLoopTemperature(LoopState loop, float thermalMass, float elapsed_s, float maxTemperature = 5000f)
		{
			float startTemperature = loop.temperature;
			float startFlux = GetLoopNetFluxKw(loop, startTemperature);
			loop.netFluxKw = startFlux;
			if (Mathf.Abs(startFlux) <= FluxEpsilonKw)
				return;

			float deltaT = startFlux * 1000f / thermalMass * elapsed_s;
			float targetTemperature = Mathf.Clamp(startTemperature + deltaT, MinimumLoopTemperatureK, maxTemperature);
			if (Mathf.Abs(targetTemperature - startTemperature) <= 0.001f)
				return;

			float targetFlux = GetLoopNetFluxKw(loop, targetTemperature);
			if (HasFluxSignChange(startFlux, targetFlux))
			{
				loop.temperature = FindFluxEquilibriumTemperature(loop, startTemperature, targetTemperature);
				loop.netFluxKw = GetLoopNetFluxKw(loop, loop.temperature);
				return;
			}

			loop.temperature = targetTemperature;
			loop.netFluxKw = targetFlux;
		}

		private static bool HasFluxSignChange(float a, float b)
		{
			return (a > FluxEpsilonKw && b < -FluxEpsilonKw)
				|| (a < -FluxEpsilonKw && b > FluxEpsilonKw);
		}

		private static float FindFluxEquilibriumTemperature(LoopState loop, float a, float b)
		{
			float low = Mathf.Min(a, b);
			float high = Mathf.Max(a, b);
			float lowFlux = GetLoopNetFluxKw(loop, low);
			float highFlux = GetLoopNetFluxKw(loop, high);

			if (!HasFluxSignChange(lowFlux, highFlux))
				return (low + high) * 0.5f;

			for (int i = 0; i < 24; i++)
			{
				float mid = (low + high) * 0.5f;
				float midFlux = GetLoopNetFluxKw(loop, mid);
				if (Mathf.Abs(midFlux) <= FluxEpsilonKw)
					return mid;

				if (HasFluxSignChange(lowFlux, midFlux))
				{
					high = mid;
					highFlux = midFlux;
				}
				else
				{
					low = mid;
					lowFlux = midFlux;
				}
			}

			return (low + high) * 0.5f;
		}

		private static float GetFallbackLoopTemperature()
		{
			return MinimumLoopTemperatureK;
		}

		private static float GetModuleVolume(Part prefab, ProtoPartModuleSnapshot module)
		{
			string moduleId = Lib.Proto.GetString(module, "moduleID");
			PartModule fallback = null;
			foreach (PartModule heat in prefab.FindModulesImplementing<PartModule>())
			{
				if (heat.moduleName != "ModuleSystemHeat")
					continue;

				if (fallback == null)
					fallback = heat;

				if (string.IsNullOrEmpty(moduleId) || GetModuleId(heat) == moduleId)
					return IntegrationReflection.GetFloat(heat, "volume", 1f);
			}

			if (fallback != null)
				return IntegrationReflection.GetFloat(fallback, "volume", 1f);
			return 1f;
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
			ProtoPartResourceSnapshot pseudoResource = part.resources.Find(k => k.resourceName == resource);
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

			if (requireDeploy && !Lib.IsEditor() && prefab.FindModuleImplementing<ModuleAnimationGroup>() != null)
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

		private static void ApplyCoreDamage(Vessel v, HeatProducer producer, LoopState loop, float elapsed_s)
		{
			if (producer.meltdownTemperature <= 0f)
				return;

			float averageTemperature = (loop.previousTemperature + loop.temperature) * 0.5f;
			switch (producer.module.moduleName)
			{
				case "ProcessControllerSystemHeat":
					ApplyCoreDamageAtTemperature(
						v, producer.part, producer.module, averageTemperature,
						producer.meltdownTemperature, producer.maximumTemperature,
						producer.coreDamageRate, producer.coreDamageCurve, elapsed_s);
					break;
				case "ModuleSystemHeatFissionReactor":
				case "ModuleSystemHeatFissionEngine":
					ApplyNativeCoreDamageAtTemperature(v, producer.part, producer.module, averageTemperature, producer.meltdownTemperature, producer.maximumTemperature);
					break;
			}
		}

		private static bool ApplyCoreDamageAtTemperature(
			Vessel v,
			ProtoPartSnapshot part,
			ProtoPartModuleSnapshot module,
			float loopTemperature,
			float damageStart,
			float maximumTemperature,
			float coreDamageRate,
			FloatCurve coreDamageCurve,
			float elapsed_s)
		{
			if (damageStart <= 0f || maximumTemperature <= damageStart)
				return false;

			float damage = SystemHeatEditorSimulation.AccumulateCoreDamage(
				loopTemperature, damageStart, maximumTemperature,
				Lib.Proto.GetFloat(module, "CoreDamage"),
				coreDamageRate, coreDamageCurve, elapsed_s);
			Lib.Proto.Set(module, "CoreDamage", damage);
			if (damage < 100f)
				return false;

			BreakProcessReactor(v, part, module);
			return true;
		}

		private static bool ApplyNativeCoreDamageAtTemperature(Vessel v, ProtoPartSnapshot part, ProtoPartModuleSnapshot module, float loopTemperature, float damageStart, float maximumTemperature)
		{
			if (damageStart <= 0f || maximumTemperature <= damageStart)
				return false;

			float currentIntegrity = Mathf.Clamp(Lib.Proto.GetFloat(module, "CoreIntegrity", 100f), 0f, 100f);
			float currentDamage = 100f - currentIntegrity;
			float damage = SystemHeatEditorSimulation.SyncCoreDamageFromTemperature(
				loopTemperature, damageStart, maximumTemperature, currentDamage);
			float integrity = Mathf.Clamp(100f - damage, 0f, 100f);
			Lib.Proto.Set(module, "CoreIntegrity", integrity);
			if (integrity > 0f)
				return false;

			BreakNativeFissionReactor(v, part, module);
			return true;
		}

		private static void EnsureUnloadedFissionLoopSimulated(Vessel v, float elapsed_s)
		{
			TryRun(v, elapsed_s);
		}

		private static void SetProtoFissionRunning(Vessel v, ProtoPartSnapshot part, ProtoPartModuleSnapshot module, bool value)
		{
			if (Lib.Proto.GetBool(module, nameof(ProcessController.running)) == value)
				return;

			Lib.Proto.Set(module, nameof(ProcessController.running), value);
		}

		private static int GetNativeRadiatorLoopId(ProtoPartSnapshot part, Part prefab, ProtoPartModuleSnapshot nativeModule)
		{
			PartModule nativePrefab = FindMatchingPrefabModule(prefab, nativeModule, nativeModule.moduleName);
			string heatModuleId = nativePrefab != null
				? GetSystemHeatModuleId(nativePrefab)
				: Lib.Proto.GetString(nativeModule, "systemHeatModuleID");
			return GetLinkedLoopId(part, prefab, heatModuleId);
		}

		private static bool IsNativeRadiatorOperational(ProtoPartSnapshot part, Part prefab, ProtoPartModuleSnapshot nativeModule)
		{
			if (!Lib.Proto.GetBool(nativeModule, "IsCooling", true))
				return false;

			PartModule nativePrefab = FindMatchingPrefabModule(prefab, nativeModule, nativeModule.moduleName);
			if (nativePrefab != null && !IntegrationReflection.GetBool(nativePrefab, "IsCooling", true))
				return false;

			foreach (ProtoPartModuleSnapshot reliability in part.modules)
			{
				if (reliability.moduleName != "Reliability" || !Lib.Proto.GetBool(reliability, "broken"))
					continue;

				string type = Lib.Proto.GetString(reliability, "type");
				if (type == "SystemHeatRadiatorKerbalism"
					|| type == "ModuleSystemHeatRadiator"
					|| type == "ModuleActiveRadiator"
					|| type == "USRadiatorSwitch")
					return false;
			}

			return true;
		}

		private static int GetSpaceDustHarvesterLoopId(ProtoPartSnapshot part, Part prefab, ProtoPartModuleSnapshot harvesterModule)
		{
			PartModule harvesterPrefab = FindMatchingPrefabModule(prefab, harvesterModule, "ModuleSpaceDustHarvester")
				?? FindPrefabModule(prefab, "ModuleSpaceDustHarvester");
			if (harvesterPrefab == null)
				return -1;

			return GetLinkedLoopId(part, prefab, SpaceDust.Get(harvesterPrefab, "HeatModuleID", ""));
		}

		private static bool TryAddSpaceDustHarvesterHeat(
			ProtoPartSnapshot part,
			Part prefab,
			ProtoPartModuleSnapshot harvesterModule,
			Dictionary<int, LoopState> loops,
			HashSet<int> riskLoopIds,
			Vessel v,
			bool registerRiskLoop)
		{
			if (!PartHasModule(part, "SpaceDustHarvesterKerbalismUpdater") || !Lib.Proto.GetBool(harvesterModule, "Enabled"))
				return false;

			PartModule harvesterPrefab = FindMatchingPrefabModule(prefab, harvesterModule, "ModuleSpaceDustHarvester")
				?? FindPrefabModule(prefab, "ModuleSpaceDustHarvester");
			if (harvesterPrefab == null)
				return false;

			int loopId = GetSpaceDustHarvesterLoopId(part, prefab, harvesterModule);
			if (loopId < 0)
				return false;

			float systemPower = SpaceDust.Get(harvesterPrefab, "SystemPower", 0f);
			if (systemPower <= 0f)
				return false;

			float shutdown = SpaceDust.Get(harvesterPrefab, "ShutdownTemperature", float.MaxValue);
			if (registerRiskLoop && shutdown < float.MaxValue)
				riskLoopIds.Add(loopId);

			EnsureLoop(loops, loopId, v);
			LoopState loop = loops[loopId];
			loop.producerFluxKw += systemPower;
			loop.shutdownTemperature = Math.Min(loop.shutdownTemperature, shutdown);
			loop.heatProducers.Add(new HeatProducer { part = part, module = harvesterModule, shutdownTemperature = shutdown });
			MarkActiveProducer(loop, SpaceDust.Get(harvesterPrefab, "SystemOutletTemperature", 0f), systemPower);
			return true;
		}

		private static float GetLinkedLoopTemperature(ProtoPartSnapshot part, Part prefab, string moduleId, Vessel v)
		{
			ProtoPartModuleSnapshot heatModule = GetLinkedHeatModule(part, prefab, moduleId);
			if (heatModule == null)
				return GetFallbackLoopTemperature();

			float loopTemp = Lib.Proto.GetFloat(heatModule, "currentLoopTemperature");
			return loopTemp > 0f ? loopTemp : GetFallbackLoopTemperature();
		}

		/// <summary>
		/// After loading a vessel, snap the live loop temperature to the persisted anchor when background
		/// simulation drifted above the flight equilibrium (prevents immediate emergency shutdown).
		/// </summary>
		public static void RestoreLoadedFissionLoopTemperature(Part part, PartModule heatModule)
		{
			if (!Enabled || part == null || heatModule == null || part.protoPartSnapshot == null)
				return;

			ProtoPartModuleSnapshot protoHeat = FindMatchingLoadedHeatModuleSnapshot(part.protoPartSnapshot, heatModule);
			if (protoHeat == null)
				return;

			float temp = Lib.Proto.GetFloat(protoHeat, "currentLoopTemperature");
			if (Lib.Proto.GetBool(protoHeat, FluxAnchorValidField))
			{
				float anchorTemp = Lib.Proto.GetFloat(protoHeat, FluxAnchorTemperatureField);
				if (anchorTemp > 0f)
				{
					if (temp <= 0f)
						temp = anchorTemp;
					else if (temp < anchorTemp - TransientTemperatureTolerance)
						temp = anchorTemp;
					else if (temp > anchorTemp + TransientTemperatureTolerance * 4f)
						temp = anchorTemp;
				}
			}

			if (temp <= 0f)
				temp = GetFallbackLoopTemperature();

			if (temp > 0f)
			{
				Lib.Proto.Set(protoHeat, "currentLoopTemperature", temp);
				SystemHeat.Set(heatModule, "currentLoopTemperature", temp);
			}
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

		private static bool IsRadiatorOperational(ProtoPartSnapshot part, Part prefab, ProtoPartModuleSnapshot radiatorModule)
		{
			if (!Lib.Proto.GetBool(radiatorModule, "IsCooling", true))
				return false;

			if (TryGetUSRadiatorSelectedPower(part, prefab, radiatorModule, out float selectedPower) && selectedPower <= 0f)
				return false;

			foreach (ProtoPartModuleSnapshot module in part.modules)
			{
				if (module.moduleName != "Reliability" || !Lib.Proto.GetBool(module, "broken"))
					continue;

				string type = Lib.Proto.GetString(module, "type");
				if (type == "SystemHeatRadiatorKerbalism"
					|| type == "ModuleSystemHeatRadiator"
					|| type == "ModuleActiveRadiator"
					|| type == "USRadiatorSwitch")
					return false;
			}

			return true;
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
			ProtoPartResourceSnapshot res = part.resources.Find(k => k.resourceName == resource);
			if (res != null)
				res.flowState = false;

			foreach (ProtoPartModuleSnapshot reliability in part.modules)
			{
				if (reliability.moduleName != "Reliability")
					continue;

				string reliabilityType = Lib.Proto.GetString(reliability, "type");
				if (reliabilityType != "ProcessControllerSystemHeat"
					&& reliabilityType != "ProcessController")
					continue;

				Lib.Proto.Set(reliability, "broken", true);
				Lib.Proto.Set(reliability, "critical", true);
			}
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

			foreach (ProtoPartModuleSnapshot reliability in part.modules)
			{
				if (reliability.moduleName != "Reliability")
					continue;

				string reliabilityType = Lib.Proto.GetString(reliability, "type");
				if (reliabilityType != "ModuleSystemHeatFissionReactor"
					&& reliabilityType != "ModuleSystemHeatFissionEngine")
					continue;

				Lib.Proto.Set(reliability, "broken", true);
				Lib.Proto.Set(reliability, "critical", true);
			}
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

		private static float GetRadiatorRejectPower(ProtoPartSnapshot part, Part prefab, ProtoPartModuleSnapshot module, float loopTemperature)
		{
			float scale = Lib.Proto.GetFloat(module, "scale", 1f);
			if (scale <= 0f)
				scale = 1f;
			float scaleEmissionPower = Lib.Proto.GetFloat(module, "scaleEmissionPower", 2f);
			float scaleFactor = (float)Math.Pow(scale, scaleEmissionPower);

			if (TryGetUSRadiatorSelectedPower(part, prefab, module, out float selectedPower))
			{
				float temperatureFactor = Mathf.Clamp01(loopTemperature / StockRadiatorRatedTemperatureK);
				return selectedPower * temperatureFactor * scaleFactor;
			}

			float curvePower = EvaluateRadiatorCurvePower(prefab, module, loopTemperature, scaleFactor);
			if (curvePower > 0f)
				return curvePower;

			float inputPower = GetRadiatorInputResourcePower(prefab, module, scaleFactor);
			if (inputPower > 0f)
				return inputPower;

			string radiatorModuleName = Lib.Proto.GetString(module, "radiatorModuleName", "ModuleSystemHeatRadiator");
			PartModule nativeRadiator = FindPrefabModule(prefab, radiatorModuleName)
				?? FindPrefabModule(prefab, "ModuleSystemHeatRadiator")
				?? FindPrefabModule(prefab, "ModuleActiveRadiator");
			if (nativeRadiator != null)
			{
				float maxTransfer = IntegrationReflection.GetFloat(nativeRadiator, "maxEnergyTransfer", 0f);
				if (maxTransfer > 0f)
					return maxTransfer * scaleFactor;
			}

			return 100f * RadiatorCoefficient * scaleFactor;
		}

		private static float EvaluateRadiatorCurvePower(Part prefab, ProtoPartModuleSnapshot module, float loopTemperature, float scaleFactor)
		{
			if (loopTemperature <= 0f)
				return 0f;

			string radiatorModuleName = Lib.Proto.GetString(module, "radiatorModuleName", "ModuleSystemHeatRadiator");
			PartModule nativeRadiator = FindPrefabModule(prefab, radiatorModuleName)
				?? FindPrefabModule(prefab, "ModuleSystemHeatRadiator")
				?? FindPrefabModule(prefab, "ModuleActiveRadiator");
			if (nativeRadiator != null)
			{
				float power = SystemHeat.EvaluateFloatCurveField(nativeRadiator, "temperatureCurve", loopTemperature, 0f);
				if (power > 0f)
					return power * scaleFactor;
			}

			PartModule shRadiator = FindPrefabModule(prefab, "SystemHeatRadiatorKerbalism");
			if (shRadiator != null)
			{
				FloatCurve shCurve = IntegrationReflection.GetField<FloatCurve>(shRadiator, "temperatureCurve");
				if (shCurve != null && shCurve.Curve.length > 0)
				{
					float power = shCurve.Evaluate(loopTemperature);
					if (power > 0f)
						return power;
				}

				FloatCurve baseCurve = IntegrationReflection.GetField<FloatCurve>(shRadiator, "baseTemperatureCurve");
				if (baseCurve != null && baseCurve.Curve.length > 0)
				{
					float power = baseCurve.Evaluate(loopTemperature) * scaleFactor;
					if (power > 0f)
						return power;
				}
			}

			return 0f;
		}

		private static float GetRadiatorInputResourcePower(Part prefab, ProtoPartModuleSnapshot module, float scaleFactor)
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

			return power > 0f ? power * scaleFactor : 0f;
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
			foreach (PartModule engine in prefab.FindModulesImplementing<PartModule>())
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

		private static int GetFissionEngineLoopId(ProtoPartSnapshot part, Part prefab, PartModule enginePrefab)
		{
			if (enginePrefab != null)
			{
				int loopId = GetLinkedLoopId(part, prefab, GetSystemHeatModuleId(enginePrefab));
				if (loopId >= 0)
					return loopId;
			}

			foreach (PartModule heatEngine in prefab.FindModulesImplementing<PartModule>())
			{
				if (heatEngine.moduleName != "ModuleSystemHeatEngine")
					continue;
				int loopId = GetLinkedLoopId(part, prefab, GetSystemHeatModuleId(heatEngine));
				if (loopId >= 0)
					return loopId;
			}

			return GetUniqueHeatLoopId(part, prefab);
		}

		private static int GetUniqueHeatLoopId(ProtoPartSnapshot part, Part prefab)
		{
			int heatCount = 0;
			foreach (PartModule heat in prefab.FindModulesImplementing<PartModule>())
			{
				if (heat.moduleName != "ModuleSystemHeat")
					continue;
				heatCount++;
			}

			if (heatCount != 1)
				return -1;

			ProtoPartModuleSnapshot heatModule = IntegrationUtils.FindPartModuleSnapshot(part, "ModuleSystemHeat");
			return heatModule != null ? Lib.Proto.GetInt(heatModule, "currentLoopID") : -1;
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

			foreach (PartModule cryo in prefab.FindModulesImplementing<PartModule>())
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

			foreach (PartModule sink in prefab.FindModulesImplementing<PartModule>())
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

		private static int GetLinkedLoopId(ProtoPartSnapshot part, Part prefab, string moduleId)
		{
			ProtoPartModuleSnapshot heatModule = GetLinkedHeatModule(part, prefab, moduleId);
			return heatModule != null ? Lib.Proto.GetInt(heatModule, "currentLoopID") : -1;
		}

		private static ProtoPartModuleSnapshot GetLinkedHeatModule(ProtoPartSnapshot part, Part prefab, string moduleId)
		{
			foreach (PartModule heat in prefab.FindModulesImplementing<PartModule>())
			{
				if (heat.moduleName != "ModuleSystemHeat")
					continue;
				if (string.IsNullOrEmpty(moduleId) || GetModuleId(heat) == moduleId)
					return FindHeatModuleSnapshot(part, moduleId);
			}
			return null;
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
	}
}
