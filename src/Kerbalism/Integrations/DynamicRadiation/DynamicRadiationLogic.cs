using System;
using System.Reflection;

namespace KERBALISM
{
	internal static class DynamicRadiationLogic
	{
		private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		public static void UpdateFlight(Emitter emitter, bool powerEnabled, ref bool reactorHasStarted, ref double reactorStoppedAt, double emitterMaxRadiation, double minEmissionPercent, double emissionDecayRate)
		{
			if (emitter == null || emitterMaxRadiation <= 0.0)
				return;

			double minRadiation = emitterMaxRadiation * minEmissionPercent / 100.0;
			double now = Planetarium.GetUniversalTime();

			if (powerEnabled)
			{
				reactorHasStarted = true;
				reactorStoppedAt = 0.0;
				emitter.running = true;
				emitter.radiation = emitterMaxRadiation;
				return;
			}

			if (!reactorHasStarted)
			{
				emitter.running = false;
				emitter.radiation = minRadiation;
				return;
			}

			if (reactorStoppedAt <= 0.0)
				reactorStoppedAt = now;

			double elapsed = Math.Max(0.0, now - reactorStoppedAt);
			double decayed = minRadiation + (emitterMaxRadiation - minRadiation) * Math.Exp(-elapsed / Math.Max(emissionDecayRate, 1.0));
			emitter.radiation = decayed;
			emitter.running = decayed > minRadiation * 1.001;
		}

		public static void UpdateBackground(ProtoPartModuleSnapshot emitterSnapshot, bool powerEnabled, ref bool reactorHasStarted, ref double reactorStoppedAt, double emitterMaxRadiation, double minEmissionPercent, double emissionDecayRate, double elapsed_s)
		{
			if (emitterSnapshot == null || emitterMaxRadiation <= 0.0)
				return;

			double minRadiation = emitterMaxRadiation * minEmissionPercent / 100.0;

			if (powerEnabled)
			{
				reactorHasStarted = true;
				reactorStoppedAt = 0.0;
				Lib.Proto.Set(emitterSnapshot, "running", true);
				Lib.Proto.Set(emitterSnapshot, "radiation", emitterMaxRadiation);
				return;
			}

			if (!reactorHasStarted)
			{
				Lib.Proto.Set(emitterSnapshot, "running", false);
				Lib.Proto.Set(emitterSnapshot, "radiation", minRadiation);
				return;
			}

			if (reactorStoppedAt <= 0.0)
				reactorStoppedAt = Planetarium.GetUniversalTime();

			double current = Lib.Proto.GetDouble(emitterSnapshot, "radiation");
			if (current <= 0.0)
				current = emitterMaxRadiation;

			double target = minRadiation + (current - minRadiation) * Math.Exp(-elapsed_s / Math.Max(emissionDecayRate, 1.0));
			if (target < minRadiation)
				target = minRadiation;

			Lib.Proto.Set(emitterSnapshot, "radiation", target);
			Lib.Proto.Set(emitterSnapshot, "running", target > minRadiation * 1.001);
		}

		public static double ResolvePeakRadiation(Part part, Emitter emitter, double minEmissionPercent, double persistedPeak)
		{
			if (persistedPeak > 0.0)
				return persistedPeak;

			double prefabPeak = FindPeakEmitterRadiation(part == null ? null : part.partInfo == null ? null : part.partInfo.partPrefab);
			if (prefabPeak > 0.0)
				return prefabPeak;

			if (emitter != null && emitter.radiation > 0.0)
			{
				if (minEmissionPercent > 0.0 && minEmissionPercent < 100.0)
				{
					double inferred = emitter.radiation * 100.0 / minEmissionPercent;
					if (inferred > emitter.radiation * 1.01)
						return inferred;
				}

				return emitter.radiation;
			}

			return 0.0;
		}

		public static double FindPeakEmitterRadiation(ProtoPartSnapshot protoPart)
		{
			if (protoPart == null)
				return 0.0;

			double best = 0.0;
			for (int i = 0; i < protoPart.modules.Count; i++)
			{
				ProtoPartModuleSnapshot module = protoPart.modules[i];
				if (module.moduleName != "Emitter")
					continue;

				double radiation = Lib.Proto.GetDouble(module, "radiation");
				if (radiation > best)
					best = radiation;
			}
			return best;
		}

		public static Emitter FindPrimaryEmitter(Part part, ref int emitterIndex)
		{
			if (part == null)
				return null;

			Emitter best = null;
			int bestIndex = -1;
			double bestRadiation = 0.0;
			for (int i = 0; i < part.Modules.Count; i++)
			{
				Emitter emitter = part.Modules[i] as Emitter;
				if (emitter == null || emitter.radiation <= 0.0)
					continue;

				if (emitter.radiation > bestRadiation)
				{
					bestRadiation = emitter.radiation;
					best = emitter;
					bestIndex = i;
				}
			}

			emitterIndex = bestIndex;
			return best;
		}

		public static ProtoPartModuleSnapshot FindEmitterSnapshot(ProtoPartSnapshot protoPart, int emitterIndex, double emitterMaxRadiation)
		{
			if (protoPart == null)
				return null;

			ProtoPartModuleSnapshot byIndex = null;
			ProtoPartModuleSnapshot best = null;
			double bestRadiation = 0.0;
			for (int i = 0; i < protoPart.modules.Count; i++)
			{
				ProtoPartModuleSnapshot module = protoPart.modules[i];
				if (module.moduleName != "Emitter")
					continue;

				if (i == emitterIndex)
					byIndex = module;

				double radiation = Lib.Proto.GetDouble(module, "radiation");
				if (radiation > bestRadiation)
				{
					bestRadiation = radiation;
					best = module;
				}
			}

			if (byIndex != null)
				return byIndex;
			if (best != null)
				return best;

			for (int i = 0; i < protoPart.modules.Count; i++)
			{
				ProtoPartModuleSnapshot module = protoPart.modules[i];
				if (module.moduleName == "Emitter" && Math.Abs(Lib.Proto.GetDouble(module, "radiation") - emitterMaxRadiation) < emitterMaxRadiation * 0.01)
					return module;
			}

			return null;
		}

		public static bool GetPowerEnabled(Part part, string powerModuleName, string powerModuleId, string powerActiveMode)
		{
			if (powerActiveMode == "any_running")
				return AnyPowerModuleRunning(part, powerModuleName, powerModuleId);

			return IsPowerActive(FindPowerModule(part, powerModuleName, powerModuleId), powerActiveMode);
		}

		public static bool GetPowerEnabledProto(ProtoPartSnapshot protoPart, string powerModuleName, string powerModuleId, string powerActiveMode)
		{
			if (powerActiveMode == "any_running")
				return AnyPowerModuleRunningProto(protoPart, powerModuleName, powerModuleId);

			return IsPowerActiveProto(FindPowerModuleProto(protoPart, powerModuleName, powerModuleId), powerActiveMode);
		}

		private static bool AnyPowerModuleRunning(Part part, string powerModuleName, string powerModuleId)
		{
			if (part == null || string.IsNullOrEmpty(powerModuleName))
				return false;

			for (int i = 0; i < part.Modules.Count; i++)
			{
				PartModule module = part.Modules[i];
				if (ModuleNameMatches(module.moduleName, powerModuleName)
					&& (string.IsNullOrEmpty(powerModuleId) || GetModuleId(module) == powerModuleId)
					&& IsPowerActive(module, "running"))
					return true;
			}

			return false;
		}

		private static bool AnyPowerModuleRunningProto(ProtoPartSnapshot protoPart, string powerModuleName, string powerModuleId)
		{
			if (protoPart == null || string.IsNullOrEmpty(powerModuleName))
				return false;

			for (int i = 0; i < protoPart.modules.Count; i++)
			{
				ProtoPartModuleSnapshot module = protoPart.modules[i];
				if (ModuleNameMatches(module.moduleName, powerModuleName)
					&& (string.IsNullOrEmpty(powerModuleId) || GetModuleId(module) == powerModuleId)
					&& Lib.Proto.GetBool(module, "running"))
					return true;
			}

			return false;
		}

		private static PartModule FindPowerModule(Part part, string powerModuleName, string powerModuleId)
		{
			if (part == null || string.IsNullOrEmpty(powerModuleName))
				return null;

			PartModule first = null;
			for (int i = 0; i < part.Modules.Count; i++)
			{
				PartModule module = part.Modules[i];
				if (!ModuleNameMatches(module.moduleName, powerModuleName))
					continue;
				if (first == null)
					first = module;
				if (string.IsNullOrEmpty(powerModuleId) || GetModuleId(module) == powerModuleId)
					return module;
			}

			return first;
		}

		private static ProtoPartModuleSnapshot FindPowerModuleProto(ProtoPartSnapshot protoPart, string powerModuleName, string powerModuleId)
		{
			if (protoPart == null || string.IsNullOrEmpty(powerModuleName))
				return null;

			ProtoPartModuleSnapshot first = null;
			for (int i = 0; i < protoPart.modules.Count; i++)
			{
				ProtoPartModuleSnapshot module = protoPart.modules[i];
				if (!ModuleNameMatches(module.moduleName, powerModuleName))
					continue;
				if (first == null)
					first = module;
				if (string.IsNullOrEmpty(powerModuleId) || GetModuleId(module) == powerModuleId)
					return module;
			}

			return first;
		}

		private static bool ModuleNameMatches(string moduleName, string powerModuleName)
		{
			return moduleName == powerModuleName || (powerModuleName == "ModuleEngines" && moduleName != null && moduleName.StartsWith("ModuleEngines"));
		}

		private static bool IsPowerActive(PartModule module, string powerActiveMode)
		{
			if (module == null)
				return false;
			if (powerActiveMode == "thrust")
				return IntegrationReflection.GetBool(module, "flameout", false) == false
					&& (IntegrationReflection.GetFloat(module, "throttle", 0f) > 0.01f || IntegrationReflection.GetFloat(module, "currentThrust", 0f) > 0.01f);
			if (powerActiveMode == "running")
				return IntegrationReflection.GetBool(module, "running", false);
			if (powerActiveMode == "converter")
				return !IntegrationReflection.GetBool(module, "DisabledByEngineer", false)
					&& (IntegrationReflection.GetBool(module, "IsEnabled", false) || IntegrationReflection.GetBool(module, "Enabled", false) || IntegrationReflection.GetBool(module, "IsActivated", false));

			return IntegrationReflection.GetBool(module, "Enabled", false);
		}

		private static bool IsPowerActiveProto(ProtoPartModuleSnapshot module, string powerActiveMode)
		{
			if (module == null)
				return false;
			if (powerActiveMode == "thrust")
				return Lib.Proto.GetFloat(module, "throttle") > 0.01f && !Lib.Proto.GetBool(module, "flameout");
			if (powerActiveMode == "running")
				return Lib.Proto.GetBool(module, "running");
			if (powerActiveMode == "converter")
				return !Lib.Proto.GetBool(module, "DisabledByEngineer") && (Lib.Proto.GetBool(module, "IsEnabled") || Lib.Proto.GetBool(module, "Enabled"));

			return Lib.Proto.GetBool(module, "Enabled");
		}

		private static string GetModuleId(PartModule module)
		{
			if (module == null)
				return string.Empty;
			string id = IntegrationReflection.GetString(module, "moduleID");
			if (!string.IsNullOrEmpty(id))
				return id;
			id = IntegrationReflection.GetString(module, "ModuleID");
			return string.IsNullOrEmpty(id) ? IntegrationReflection.GetString(module, "resource") : id;
		}

		private static string GetModuleId(ProtoPartModuleSnapshot module)
		{
			string id = Lib.Proto.GetString(module, "moduleID");
			if (!string.IsNullOrEmpty(id))
				return id;
			id = Lib.Proto.GetString(module, "ModuleID");
			return string.IsNullOrEmpty(id) ? Lib.Proto.GetString(module, "resource") : id;
		}

		private static double FindPeakEmitterRadiation(Part prefab)
		{
			if (prefab == null)
				return 0.0;

			double best = 0.0;
			for (int i = 0; i < prefab.Modules.Count; i++)
			{
				Emitter emitter = prefab.Modules[i] as Emitter;
				if (emitter != null && emitter.radiation > best)
					best = emitter.radiation;
			}
			return best;
		}
	}
}
