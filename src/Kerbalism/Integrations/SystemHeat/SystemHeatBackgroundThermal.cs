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
		private static readonly Dictionary<Guid, double> lastReactorLoopSimTime = new Dictionary<Guid, double>();

		private static readonly string[] FusionReactorModuleNames = { "FusionReactor", "ModuleFusionEngine" };

		internal static bool Enabled = true;
		internal static float RadiatorCoefficient = 0.05f;
		private const float TransientTemperatureTolerance = 5f;
		private const float FluxEpsilonKw = 0.01f;
		private const float CoolantDensity = 1f;
		private const float CoolantHeatCapacity = 4.18f;
		private const float HeatLoopDecayCoefficient = 0.01f;

		public static void CaptureLoadedTemperatures(Vessel v)
		{
			if (!Enabled || v == null || !v.loaded)
				return;

			foreach (Part part in v.parts)
			{
				if (part == null || part.protoPartSnapshot == null)
					continue;

				foreach (PartModule module in part.Modules)
				{
					if (module == null || !IsLoadedHeatLoopModule(module))
						continue;

					ProtoPartModuleSnapshot protoModule = FindMatchingLoadedHeatModuleSnapshot(part.protoPartSnapshot, module);
					if (protoModule == null)
						continue;

					float temperature = SystemHeat.CurrentLoopTemperature(module, 0f);
					if (temperature > 0f)
						Lib.Proto.Set(protoModule, "currentLoopTemperature", temperature);

					float flux = SystemHeat.Get(module, "currentLoopFlux", Lib.Proto.GetFloat(protoModule, "currentLoopFlux"));
					Lib.Proto.Set(protoModule, "currentLoopFlux", flux);
				}
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

			TryRun(v, elapsed_s);

			ProtoPartModuleSnapshot heatModule = GetLinkedHeatModule(part, partPrefab, Lib.Proto.GetString(module, "systemHeatModuleID"));
			float loopTemperature = heatModule != null ? Lib.Proto.GetFloat(heatModule, "currentLoopTemperature") : GetEnvironmentTemperature(v);
			if (loopTemperature <= 0f)
				loopTemperature = GetEnvironmentTemperature(v);

			ApplyFrozenCoreDamage(v, part, module, partPrefab, processPrefab, loopTemperature, (float)elapsed_s);
			if (Lib.Proto.GetBool(module, "broken"))
			{
				pseudoResource.flowState = false;
				return;
			}

			bool running = Lib.Proto.GetBool(module, "running");
			float safetyOverride = GetFissionSafetyOverride(partPrefab, module, processPrefab);
			bool autoShutdown = IntegrationReflection.GetBool(processPrefab, "AutoShutdown", true);
			if (running && autoShutdown && loopTemperature > safetyOverride)
			{
				Lib.Proto.Set(module, "running", false);
				running = false;
			}

			if (!running)
			{
				pseudoResource.flowState = false;
				return;
			}

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

		private class LoopState
		{
			internal float volume;
			internal float temperature;
			internal float previousTemperature;
			internal float netFluxKw;
			internal float outletTemperature;
			internal float shutdownTemperature = float.MaxValue;
			internal bool hasActiveProducer;
			internal bool hasRadiator;
			internal readonly List<ProtoPartModuleSnapshot> heatModules = new List<ProtoPartModuleSnapshot>();
			internal readonly List<HeatProducer> heatProducers = new List<HeatProducer>();
			internal readonly List<HeatSink> heatSinks = new List<HeatSink>();
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
							loop = new LoopState { temperature = loopTemp > 0f ? loopTemp : GetEnvironmentTemperature(v) };
							loops[loopId] = loop;
						}

						loop.volume += volume;
						if (loopTemp > 0f)
							loop.temperature = loopTemp;
						loop.heatModules.Add(module);
					}
					else if (module.moduleName == "ProcessControllerSystemHeat")
					{
						PartModule processPrefab = FindMatchingPrefabModule(prefab, module, "ProcessControllerSystemHeat");
						int loopId = GetLinkedLoopId(part, prefab, Lib.Proto.GetString(module, "systemHeatModuleID"));
						if (loopId < 0)
							continue;

						bool isFission = Lib.Proto.GetString(module, "resource") == "_Nukereactor";
						float meltdown = GetProcessField(prefab, module, "meltdownTemperature", 0f);
						float maximum = GetProcessField(prefab, module, "MaximumTemperature", 0f);
						if (isFission || (meltdown > 0f && maximum > meltdown))
							riskLoopIds.Add(loopId);

						float shutdown = isFission
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
						loop.netFluxKw += power;
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
						loop.netFluxKw += power;
						loop.shutdownTemperature = Math.Min(loop.shutdownTemperature, shutdown);
						loop.heatProducers.Add(new HeatProducer { part = part, module = module, shutdownTemperature = shutdown });
						MarkActiveProducer(loop, GetHarvesterField(prefab, module, "systemOutletTemperature", 0f), power);
					}
					else if (module.moduleName == "SystemHeatRadiatorKerbalism")
					{
						if (!IsRadiatorOperational(part, module))
							continue;

						int loopId = GetRadiatorLoopId(part, prefab, module);
						if (loopId < 0)
							continue;

						EnsureLoop(loops, loopId, v);
						LoopState loop = loops[loopId];
						loop.netFluxKw -= GetRadiatorRejectPower(prefab, module, loop.temperature);
						loop.hasRadiator = true;
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
						loop.netFluxKw += IntegrationReflection.GetFloat(converterPrefab, "systemPower");
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
						loop.netFluxKw += IntegrationReflection.GetFloat(harvesterPrefab, "systemPower");
						loop.shutdownTemperature = Math.Min(loop.shutdownTemperature, IntegrationReflection.GetFloat(harvesterPrefab, "shutdownTemperature", float.MaxValue));
						loop.heatProducers.Add(new HeatProducer { part = part, module = harvester, shutdownTemperature = IntegrationReflection.GetFloat(harvesterPrefab, "shutdownTemperature", float.MaxValue) });
						MarkActiveProducer(loop, IntegrationReflection.GetFloat(harvesterPrefab, "systemOutletTemperature"), IntegrationReflection.GetFloat(harvesterPrefab, "systemPower"));
					}
					else if (module.moduleName == "SpaceDustHarvesterKerbalismUpdater")
					{
						ProtoPartModuleSnapshot harvester = IntegrationUtils.TryFindPartModuleSnapshot(part, "ModuleSpaceDustHarvester");
						if (harvester == null)
							continue;

						if (Lib.Proto.GetBool(harvester, "Enabled"))
							Lib.Proto.Set(harvester, "Enabled", false);
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
							loop.netFluxKw += heat;
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
							loop.netFluxKw += heat;
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
						loops[loopId].netFluxKw += heat;
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
						loops[loopId].netFluxKw += systemPower;
					}
				}
			}

			ApplyHeatSinkStorage(loops, elapsed_s);

			if (riskLoopIds.Count == 0 && temperatureSensitiveLoopIds.Count == 0)
			{
				AdvanceOrdinaryTransientLoops(v, loops, riskLoopIds, temperatureSensitiveLoopIds, elapsed_s);
				SyncFrozenShutdowns(v);
				return;
			}

			// Ordinary loops only advance while warming/cooling toward steady state.
			// Risk loops keep advancing because their temperature drives damage/meltdown.
			// Temperature-sensitive loops keep advancing because their temperature drives state such as cryo boiloff.
			AdvanceOrdinaryTransientLoops(v, loops, riskLoopIds, temperatureSensitiveLoopIds, elapsed_s);

			float envTemp = GetEnvironmentTemperature(v);
			foreach (KeyValuePair<int, LoopState> entry in loops)
			{
				bool isRiskLoop = riskLoopIds.Contains(entry.Key);
				bool isTemperatureSensitiveLoop = temperatureSensitiveLoopIds.Contains(entry.Key);
				if (!isRiskLoop && !isTemperatureSensitiveLoop)
					continue;

				LoopState loop = entry.Value;
				if (loop.volume <= 0f)
					loop.volume = 1f;

				float thermalMass = loop.volume * CoolantDensity * CoolantHeatCapacity;
				if (thermalMass <= 0f)
					continue;

				loop.previousTemperature = loop.temperature;
				AdvanceLoopTemperature(loop, thermalMass, envTemp, elapsed_s);

				foreach (ProtoPartModuleSnapshot heatModule in loop.heatModules)
				{
					Lib.Proto.Set(heatModule, "currentLoopTemperature", loop.temperature);
					Lib.Proto.Set(heatModule, "currentLoopFlux", loop.netFluxKw);
				}

				if (!isRiskLoop)
					continue;

				foreach (HeatProducer producer in loop.heatProducers)
					ApplyCoreDamage(v, producer, loop, elapsed_s);

				if (loop.temperature >= loop.shutdownTemperature)
				{
					foreach (HeatProducer producer in loop.heatProducers)
					{
						if (loop.temperature < producer.shutdownTemperature)
							continue;

						switch (producer.module.moduleName)
						{
							case "ProcessControllerSystemHeat":
							case "HarvesterSystemHeat":
								Lib.Proto.Set(producer.module, "running", false);
								break;
							case "ModuleSystemHeatConverter":
							case "ModuleSystemHeatHarvester":
								Lib.Proto.Set(producer.module, "IsActivated", false);
								break;
							case "ModuleSystemHeatFissionReactor":
							case "ModuleSystemHeatFissionEngine":
								Lib.Proto.Set(producer.module, "Enabled", false);
								break;
							case "ModuleSpaceDustHarvester":
								Lib.Proto.Set(producer.module, "Enabled", false);
								break;
						}
					}
				}
			}

			SyncFrozenShutdowns(v);
		}

		private static void EnsureLoop(Dictionary<int, LoopState> loops, int loopId, Vessel v)
		{
			if (!loops.ContainsKey(loopId))
				loops[loopId] = new LoopState { temperature = GetEnvironmentTemperature(v) };
		}

		private static void MarkActiveProducer(LoopState loop, float outletTemperature, float power)
		{
			if (power <= 0f)
				return;

			loop.hasActiveProducer = true;
			if (outletTemperature > loop.outletTemperature)
				loop.outletTemperature = outletTemperature;
		}

		private static void AdvanceOrdinaryTransientLoops(
			Vessel v,
			Dictionary<int, LoopState> loops,
			HashSet<int> riskLoopIds,
			HashSet<int> temperatureSensitiveLoopIds,
			float elapsed_s)
		{
			float envTemp = GetEnvironmentTemperature(v);
			foreach (KeyValuePair<int, LoopState> entry in loops)
			{
				if (riskLoopIds.Contains(entry.Key) || temperatureSensitiveLoopIds.Contains(entry.Key))
					continue;

				LoopState loop = entry.Value;
				if (!IsOrdinaryLoopTransient(loop, envTemp))
					continue;

				if (loop.volume <= 0f)
					loop.volume = 1f;

				float thermalMass = loop.volume * CoolantDensity * CoolantHeatCapacity;
				if (thermalMass <= 0f)
					continue;

				loop.previousTemperature = loop.temperature;
				AdvanceLoopTemperature(loop, thermalMass, envTemp, elapsed_s);

				// Ordinary producer loops are treated as warming/cooling toward their outlet
				// setpoint, then frozen again instead of continuously integrating forever.
				if (loop.hasActiveProducer && loop.outletTemperature > 0f)
				{
					if (loop.previousTemperature < loop.outletTemperature && loop.temperature > loop.outletTemperature)
						loop.temperature = loop.outletTemperature;
					else if (loop.previousTemperature > loop.outletTemperature && loop.temperature < loop.outletTemperature)
						loop.temperature = loop.outletTemperature;
				}

				foreach (ProtoPartModuleSnapshot heatModule in loop.heatModules)
				{
					Lib.Proto.Set(heatModule, "currentLoopTemperature", loop.temperature);
					Lib.Proto.Set(heatModule, "currentLoopFlux", loop.netFluxKw);
				}
			}
		}

		private static bool IsOrdinaryLoopTransient(LoopState loop, float envTemp)
		{
			if (loop.hasActiveProducer && loop.outletTemperature > 0f)
			{
				if (loop.netFluxKw > FluxEpsilonKw && loop.temperature < loop.outletTemperature - TransientTemperatureTolerance)
					return true;
				if (loop.netFluxKw < -FluxEpsilonKw && loop.temperature > loop.outletTemperature + TransientTemperatureTolerance)
					return true;
			}

			return !loop.hasActiveProducer
				&& loop.hasRadiator
				&& loop.netFluxKw < -FluxEpsilonKw
				&& loop.temperature > envTemp + TransientTemperatureTolerance;
		}

		private static void ApplyHeatSinkStorage(Dictionary<int, LoopState> loops, float elapsed_s)
		{
			if (elapsed_s <= 0f)
				return;

			foreach (LoopState loop in loops.Values)
			{
				if (loop.netFluxKw <= FluxEpsilonKw || loop.heatSinks.Count == 0)
					continue;

				for (int i = 0; i < loop.heatSinks.Count && loop.netFluxKw > FluxEpsilonKw; i++)
				{
					HeatSink sink = loop.heatSinks[i];
					float storedEnergy = StoreHeatInSink(sink, loop.netFluxKw, elapsed_s);
					if (storedEnergy <= 0f)
						continue;

					loop.netFluxKw -= storedEnergy / elapsed_s;
					if (loop.netFluxKw < 0f)
						loop.netFluxKw = 0f;
				}
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

		private static void AdvanceLoopTemperature(LoopState loop, float thermalMass, float envTemp, float elapsed_s)
		{
			float deltaT = loop.netFluxKw * 1000f / thermalMass * elapsed_s;
			loop.temperature = Mathf.Clamp(loop.temperature + deltaT, envTemp, 5000f);

			if (loop.netFluxKw <= 0f && loop.temperature > envTemp)
			{
				float decay = (loop.temperature - envTemp) * HeatLoopDecayCoefficient;
				loop.temperature -= decay * 1000f / thermalMass * elapsed_s;
				loop.temperature = Mathf.Max(loop.temperature, envTemp);
			}
		}

		private static void CollectRiskLoopFlux(Vessel v, Dictionary<int, LoopState> loops, HashSet<int> riskLoopIds)
		{
			foreach (ProtoPartSnapshot part in v.protoVessel.protoPartSnapshots)
			{
				Part prefab = PartLoader.getPartInfoByName(part.partName).partPrefab;

				foreach (ProtoPartModuleSnapshot module in part.modules)
				{
					if (module.moduleName == "ProcessControllerSystemHeat")
					{
						int loopId = GetLinkedLoopId(part, prefab, Lib.Proto.GetString(module, "systemHeatModuleID"));
						if (loopId < 0 || !riskLoopIds.Contains(loopId))
							continue;

						PartModule processPrefab = FindMatchingPrefabModule(prefab, module, "ProcessControllerSystemHeat");
						float shutdown = GetProcessField(prefab, module, "shutdownTemperature", float.MaxValue);
						if (Lib.Proto.GetString(module, "resource") == "_Nukereactor")
							shutdown = GetFissionSafetyOverride(prefab, module, processPrefab);

						EnsureLoop(loops, loopId, v);
						LoopState loop = loops[loopId];
						loop.shutdownTemperature = Math.Min(loop.shutdownTemperature, shutdown);
						loop.heatProducers.Add(new HeatProducer
						{
							part = part,
							module = module,
							shutdownTemperature = shutdown,
							meltdownTemperature = GetProcessField(prefab, module, "meltdownTemperature", 0f),
							maximumTemperature = GetProcessField(prefab, module, "MaximumTemperature", 2000f),
							coreDamageRate = GetProcessField(prefab, module, "CoreDamageRate", 0f),
							coreDamageCurve = IntegrationReflection.GetField(processPrefab, "coreDamageCurve", new FloatCurve())
						});

						if (!IsProcessOperational(part, prefab, module, processPrefab))
							continue;

						loop.netFluxKw += GetProcessHeatPower(part, prefab, module, processPrefab) * GetProcessThrottle(module);
					}
					else if (module.moduleName == "HarvesterSystemHeat")
					{
						if (!Lib.Proto.GetBool(module, "deployed") || !Lib.Proto.GetBool(module, "running") || Lib.Proto.GetString(module, "issue").Length > 0)
							continue;

						int loopId = GetLinkedLoopId(part, prefab, Lib.Proto.GetString(module, "systemHeatModuleID"));
						if (loopId < 0 || !riskLoopIds.Contains(loopId))
							continue;

						float shutdown = GetHarvesterField(prefab, module, "shutdownTemperature", float.MaxValue);
						EnsureLoop(loops, loopId, v);
						loops[loopId].netFluxKw += GetHarvesterHeatPower(prefab, module);
						loops[loopId].shutdownTemperature = Math.Min(loops[loopId].shutdownTemperature, shutdown);
						loops[loopId].heatProducers.Add(new HeatProducer { part = part, module = module, shutdownTemperature = shutdown });
					}
					else if (module.moduleName == "SystemHeatRadiatorKerbalism")
					{
						if (!IsRadiatorOperational(part, module))
							continue;

						int loopId = GetRadiatorLoopId(part, prefab, module);
						if (loopId < 0 || !riskLoopIds.Contains(loopId))
							continue;

						EnsureLoop(loops, loopId, v);
						LoopState loop = loops[loopId];
						loop.netFluxKw -= GetRadiatorRejectPower(prefab, module, loop.temperature);
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
						if (loopId < 0 || !riskLoopIds.Contains(loopId))
							continue;

						EnsureLoop(loops, loopId, v);
						loops[loopId].netFluxKw += IntegrationReflection.GetFloat(converterPrefab, "systemPower");
						loops[loopId].shutdownTemperature = Math.Min(loops[loopId].shutdownTemperature, IntegrationReflection.GetFloat(converterPrefab, "shutdownTemperature", float.MaxValue));
						loops[loopId].heatProducers.Add(new HeatProducer { part = part, module = converter, shutdownTemperature = IntegrationReflection.GetFloat(converterPrefab, "shutdownTemperature", float.MaxValue) });
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
						if (loopId < 0 || !riskLoopIds.Contains(loopId))
							continue;

						EnsureLoop(loops, loopId, v);
						loops[loopId].netFluxKw += IntegrationReflection.GetFloat(harvesterPrefab, "systemPower");
						loops[loopId].shutdownTemperature = Math.Min(loops[loopId].shutdownTemperature, IntegrationReflection.GetFloat(harvesterPrefab, "shutdownTemperature", float.MaxValue));
						loops[loopId].heatProducers.Add(new HeatProducer { part = part, module = harvester, shutdownTemperature = IntegrationReflection.GetFloat(harvesterPrefab, "shutdownTemperature", float.MaxValue) });
					}
					else if (module.moduleName == "SpaceDustHarvesterKerbalismUpdater")
					{
						ProtoPartModuleSnapshot harvester = IntegrationUtils.FindPartModuleSnapshot(part, "ModuleSpaceDustHarvester");
						if (harvester == null)
							continue;

						if (Lib.Proto.GetBool(harvester, "Enabled"))
							Lib.Proto.Set(harvester, "Enabled", false);
					}
					else if (module.moduleName == "SystemHeatFissionReactorKerbalismUpdater")
					{
						ProtoPartModuleSnapshot reactor = IntegrationUtils.FindPartModuleSnapshot(part, "ModuleSystemHeatFissionReactor");
						if (reactor == null || !Lib.Proto.GetBool(reactor, "Enabled"))
							continue;

						PartModule reactorPrefab = FindPrefabModule(prefab, "ModuleSystemHeatFissionReactor");
						string heatModuleId = reactorPrefab != null ? GetSystemHeatModuleId(reactorPrefab) : "reactor";
						int loopId = GetLinkedLoopId(part, prefab, heatModuleId);
						if (loopId < 0 || !riskLoopIds.Contains(loopId))
							continue;

						EnsureLoop(loops, loopId, v);
						float throttle = Lib.Proto.GetFloat(reactor, "CurrentReactorThrottle");
						loops[loopId].netFluxKw += GetReactorWasteHeat(reactorPrefab, throttle);
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
						if (loopId < 0 || !riskLoopIds.Contains(loopId))
							continue;

						EnsureLoop(loops, loopId, v);
						loops[loopId].netFluxKw += systemPower;
					}
				}
			}
		}

		private static void SyncFrozenShutdowns(Vessel v)
		{
			foreach (ProtoPartSnapshot part in v.protoVessel.protoPartSnapshots)
			{
				Part prefab = PartLoader.getPartInfoByName(part.partName).partPrefab;

				foreach (ProtoPartModuleSnapshot module in part.modules)
				{
					if (module.moduleName == "ProcessControllerSystemHeat")
					{
						if (!Lib.Proto.GetBool(module, "running") || Lib.Proto.GetBool(module, "broken"))
							continue;

						if (Lib.Proto.GetString(module, "resource") == "_Nukereactor")
							continue;

						float meltdown = GetProcessField(prefab, module, "meltdownTemperature", 0f);
						float maximum = GetProcessField(prefab, module, "MaximumTemperature", 0f);
						if (meltdown > 0f && maximum > meltdown)
							continue;

						float loopTemperature = GetLinkedLoopTemperature(part, prefab, Lib.Proto.GetString(module, "systemHeatModuleID"), v);
						float shutdown = GetProcessField(prefab, module, "shutdownTemperature", float.MaxValue);
						if (loopTemperature > shutdown)
						{
							Lib.Proto.Set(module, "running", false);
							SetPseudoResourceFlow(part, module, FindMatchingPrefabModule(prefab, module, "ProcessControllerSystemHeat"), false);
						}
					}
					else if (module.moduleName == "HarvesterSystemHeat")
					{
						if (!Lib.Proto.GetBool(module, "running"))
							continue;

						float loopTemperature = GetLinkedLoopTemperature(part, prefab, Lib.Proto.GetString(module, "systemHeatModuleID"), v);
						float shutdown = GetHarvesterField(prefab, module, "shutdownTemperature", float.MaxValue);
						if (loopTemperature > shutdown)
							Lib.Proto.Set(module, "running", false);
					}
					else if (module.moduleName == "SystemHeatConverterKerbalismUpdater")
					{
						ProtoPartModuleSnapshot converter = IntegrationUtils.TryFindPartModuleSnapshot(part, "ModuleSystemHeatConverter");
						if (converter == null || !Lib.Proto.GetBool(converter, "IsActivated"))
							continue;

						PartModule converterPrefab = FindPrefabModule(prefab, "ModuleSystemHeatConverter");
						if (converterPrefab == null)
							continue;

						float loopTemperature = GetLinkedLoopTemperature(part, prefab, GetSystemHeatModuleId(converterPrefab), v);
						if (loopTemperature > IntegrationReflection.GetFloat(converterPrefab, "shutdownTemperature", float.MaxValue))
							Lib.Proto.Set(converter, "IsActivated", false);
					}
					else if (module.moduleName == "SystemHeatHarvesterKerbalismUpdater")
					{
						ProtoPartModuleSnapshot harvester = IntegrationUtils.TryFindPartModuleSnapshot(part, "ModuleSystemHeatHarvester");
						if (harvester == null || !Lib.Proto.GetBool(harvester, "IsActivated"))
							continue;

						PartModule harvesterPrefab = FindPrefabModule(prefab, "ModuleSystemHeatHarvester");
						if (harvesterPrefab == null)
							continue;

						float loopTemperature = GetLinkedLoopTemperature(part, prefab, GetSystemHeatModuleId(harvesterPrefab), v);
						if (loopTemperature > IntegrationReflection.GetFloat(harvesterPrefab, "shutdownTemperature", float.MaxValue))
							Lib.Proto.Set(harvester, "IsActivated", false);
					}
				}
			}
		}

		private static float GetEnvironmentTemperature(Vessel v)
		{
			if (v.mainBody != null && v.altitude < 50000d)
				return Mathf.Clamp((float)v.mainBody.GetTemperature(v.altitude), 4f, 50000f);
			return 4f;
		}

		private static float GetModuleVolume(Part prefab, ProtoPartModuleSnapshot module)
		{
			PartModule heat = FindPrefabModule(prefab, "ModuleSystemHeat");
			if (heat != null)
				return IntegrationReflection.GetFloat(heat, "volume", 1f);
			return 1f;
		}

		private static float GetProcessHeatPower(ProtoPartSnapshot part, Part prefab, ProtoPartModuleSnapshot module, PartModule processPrefab)
		{
			if (HasNoWasteHeatSubtype(part))
				return 0f;

			string resource = Lib.Proto.GetString(module, "resource");
			if (processPrefab != null)
				return IntegrationReflection.GetFloat(processPrefab, "systemPower");

			foreach (PartModule pm in prefab.Modules)
			{
				if (pm.moduleName != "ProcessControllerSystemHeat")
					continue;
				if (string.IsNullOrEmpty(resource) || IntegrationReflection.GetString(pm, "resource") == resource)
					return IntegrationReflection.GetFloat(pm, "systemPower");
			}
			return Lib.Proto.GetFloat(module, "systemPower");
		}

		private static float GetProcessThrottle(ProtoPartModuleSnapshot module)
		{
			float percent = Lib.Proto.GetFloat(module, "CurrentPowerPercent", 100f);
			return Mathf.Clamp(percent, 0f, 100f) / 100f;
		}

		private static void SetPseudoResourceFlow(ProtoPartSnapshot part, ProtoPartModuleSnapshot module, PartModule processPrefab, bool flowState)
		{
			string resource = processPrefab != null
				? IntegrationReflection.GetString(processPrefab, "resource", Lib.Proto.GetString(module, "resource"))
				: Lib.Proto.GetString(module, "resource");
			ProtoPartResourceSnapshot pseudoResource = part.resources.Find(k => k.resourceName == resource);
			if (pseudoResource != null)
				pseudoResource.flowState = flowState;
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
					ApplyCoreDamageAtTemperature(v, producer.part, producer.module, averageTemperature, producer.meltdownTemperature, producer.maximumTemperature);
					break;
				case "ModuleSystemHeatFissionReactor":
				case "ModuleSystemHeatFissionEngine":
					ApplyNativeCoreDamageAtTemperature(v, producer.part, producer.module, averageTemperature, producer.meltdownTemperature, producer.maximumTemperature);
					break;
			}
		}

		private static void ApplyFrozenCoreDamage(Vessel v, ProtoPartSnapshot part, ProtoPartModuleSnapshot module, Part prefab, PartModule processPrefab, float loopTemperature, float elapsed_s)
		{
			float damageStart = GetProcessField(prefab, module, "meltdownTemperature", 0f);
			float maximumTemperature = GetProcessField(prefab, module, "MaximumTemperature", 2000f);
			ApplyCoreDamageAtTemperature(v, part, module, loopTemperature, damageStart, maximumTemperature);
		}

		private static bool ApplyCoreDamageAtTemperature(Vessel v, ProtoPartSnapshot part, ProtoPartModuleSnapshot module, float loopTemperature, float damageStart, float maximumTemperature)
		{
			if (damageStart <= 0f || maximumTemperature <= damageStart)
				return false;

			float damage = SystemHeatEditorSimulation.SyncCoreDamageFromTemperature(
				loopTemperature, damageStart, maximumTemperature, Lib.Proto.GetFloat(module, "CoreDamage"));
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
			if (!Enabled || v == null || elapsed_s <= 0f || v.loaded)
				return;

			double now = Planetarium.GetUniversalTime();
			if (lastReactorLoopSimTime.TryGetValue(v.id, out double last) && last == now)
				return;
			lastReactorLoopSimTime[v.id] = now;

			SimulateUnloadedFissionLoops(v, elapsed_s);
		}

		private static void SimulateUnloadedFissionLoops(Vessel v, float elapsed_s)
		{
			var loops = new Dictionary<int, LoopState>();
			var fissionLoopIds = new HashSet<int>();
			float envTemp = GetEnvironmentTemperature(v);

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
							loop = new LoopState { temperature = loopTemp > 0f ? loopTemp : envTemp };
							loops[loopId] = loop;
						}

						loop.volume += volume;
						if (loopTemp > 0f)
							loop.temperature = loopTemp;
						loop.heatModules.Add(module);
					}
					else if (module.moduleName == "ProcessControllerSystemHeat" && Lib.Proto.GetString(module, "resource") == "_Nukereactor")
					{
						int loopId = GetLinkedLoopId(part, prefab, Lib.Proto.GetString(module, "systemHeatModuleID"));
						if (loopId >= 0)
							fissionLoopIds.Add(loopId);
					}
				}
			}

			if (fissionLoopIds.Count == 0)
				return;

			foreach (ProtoPartSnapshot part in v.protoVessel.protoPartSnapshots)
			{
				Part prefab = PartLoader.getPartInfoByName(part.partName).partPrefab;

				foreach (ProtoPartModuleSnapshot module in part.modules)
				{
					if (module.moduleName == "ProcessControllerSystemHeat" && Lib.Proto.GetString(module, "resource") == "_Nukereactor")
					{
						if (Lib.Proto.GetBool(module, "broken") || !Lib.Proto.GetBool(module, "running"))
							continue;

						PartModule processPrefab = FindMatchingPrefabModule(prefab, module, "ProcessControllerSystemHeat");

						float power = GetProcessHeatPower(part, prefab, module, processPrefab) * GetProcessThrottle(module);
						int loopId = GetLinkedLoopId(part, prefab, Lib.Proto.GetString(module, "systemHeatModuleID"));
						if (loopId < 0 || !fissionLoopIds.Contains(loopId))
							continue;

						EnsureLoop(loops, loopId, v);
						loops[loopId].netFluxKw += power;
					}
					else if (module.moduleName == "SystemHeatRadiatorKerbalism")
					{
						if (!IsRadiatorOperational(part, module))
							continue;

						int loopId = GetRadiatorLoopId(part, prefab, module);
						if (loopId < 0 || !fissionLoopIds.Contains(loopId))
							continue;

						EnsureLoop(loops, loopId, v);
						LoopState loop = loops[loopId];
						loop.netFluxKw -= GetRadiatorRejectPower(prefab, module, loop.temperature);
					}
				}
			}

			foreach (int loopId in fissionLoopIds)
			{
				if (!loops.TryGetValue(loopId, out LoopState loop))
					continue;

				if (loop.volume <= 0f)
					loop.volume = 1f;

				float thermalMass = loop.volume * CoolantDensity * CoolantHeatCapacity;
				if (thermalMass <= 0f)
					continue;

				float deltaT = loop.netFluxKw * 1000f / thermalMass * elapsed_s;
				loop.temperature = Mathf.Clamp(loop.temperature + deltaT, envTemp, 5000f);

				if (loop.netFluxKw <= 0f && loop.temperature > envTemp)
				{
					float decay = (loop.temperature - envTemp) * HeatLoopDecayCoefficient;
					loop.temperature -= decay * 1000f / thermalMass * elapsed_s;
					loop.temperature = Mathf.Max(loop.temperature, envTemp);
				}

				foreach (ProtoPartModuleSnapshot heatModule in loop.heatModules)
					Lib.Proto.Set(heatModule, "currentLoopTemperature", loop.temperature);
			}
		}

		private static float GetLinkedLoopTemperature(ProtoPartSnapshot part, Part prefab, string moduleId, Vessel v)
		{
			ProtoPartModuleSnapshot heatModule = GetLinkedHeatModule(part, prefab, moduleId);
			if (heatModule == null)
				return GetEnvironmentTemperature(v);

			float loopTemp = Lib.Proto.GetFloat(heatModule, "currentLoopTemperature");
			return loopTemp > 0f ? loopTemp : GetEnvironmentTemperature(v);
		}

		private static bool IsRadiatorOperational(ProtoPartSnapshot part, ProtoPartModuleSnapshot radiatorModule)
		{
			if (!Lib.Proto.GetBool(radiatorModule, "IsCooling", true))
				return false;

			foreach (ProtoPartModuleSnapshot module in part.modules)
			{
				if (module.moduleName != "Reliability" || !Lib.Proto.GetBool(module, "broken"))
					continue;

				string type = Lib.Proto.GetString(module, "type");
				if (type == "SystemHeatRadiatorKerbalism"
					|| type == "ModuleSystemHeatRadiator"
					|| type == "ModuleActiveRadiator")
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
			float meltdown = GetProcessField(prefab, module, "meltdownTemperature", 1300f);
			float protoOverride = Lib.Proto.GetFloat(module, "CurrentSafetyOverride", 0f);
			if (protoOverride > 0f)
				return protoOverride;

			return meltdown > 0f ? meltdown : IntegrationReflection.GetFloat(processPrefab, "CurrentSafetyOverride", 1000f);
		}

		private static void BreakProcessReactor(Vessel v, ProtoPartSnapshot part, ProtoPartModuleSnapshot module)
		{
			v.KerbalismData().ResetReliabilityStatus();
			Lib.Proto.Set(module, "running", false);
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

		private static float GetRadiatorRejectPower(Part prefab, ProtoPartModuleSnapshot module, float loopTemperature)
		{
			string radiatorModuleName = Lib.Proto.GetString(module, "radiatorModuleName", "ModuleSystemHeatRadiator");
			PartModule radiator = FindPrefabModule(prefab, radiatorModuleName)
				?? FindPrefabModule(prefab, "ModuleSystemHeatRadiator")
				?? FindPrefabModule(prefab, "ModuleActiveRadiator")
				?? FindPrefabModule(prefab, "SystemHeatRadiatorKerbalism");

			float scale = Lib.Proto.GetFloat(module, "scale", 1f);
			if (scale <= 0f)
				scale = 1f;
			float scaleEmissionPower = Lib.Proto.GetFloat(module, "scaleEmissionPower", 2f);
			float scaleFactor = (float)Math.Pow(scale, scaleEmissionPower);

			if (radiator != null && loopTemperature > 0f)
			{
				float curvePower = SystemHeat.EvaluateFloatCurveField(radiator, "temperatureCurve", loopTemperature, 0f);
				if (curvePower > 0f)
					return curvePower * scaleFactor;
			}

			float power = 0f;
			IList inputResources = radiator != null ? SystemHeat.GetResHandlerInputResources(radiator) : null;
			if (inputResources != null)
			{
				for (int i = 0; i < inputResources.Count; i++)
				{
					if (inputResources[i] is ModuleResource res)
						power += (float)res.rate;
				}
			}
			return (power > 0f ? power : 10f) * SystemHeatBackgroundThermal.RadiatorCoefficient * scaleFactor;
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
