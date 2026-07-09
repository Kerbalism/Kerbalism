using System.Collections.Generic;

namespace KERBALISM
{
	public class DynamicRadiationController : PartModule
	{
		[KSPField(isPersistant = true)] public string powerModuleName = "";
		[KSPField(isPersistant = true)] public string powerModuleId = "";
		[KSPField(isPersistant = true)] public string powerActiveMode = "enabled";
		[KSPField(isPersistant = true)] public double minEmissionPercent = 25.0;
		[KSPField(isPersistant = true)] public double emissionDecayRate = 3600.0;
		[KSPField(isPersistant = true)] public bool reactorHasStarted = false;
		[KSPField(isPersistant = true)] public double reactorStoppedAt = 0.0;
		[KSPField(isPersistant = true)] public double emitterMaxRadiation = 0.0;
		[KSPField(isPersistant = true)] public int emitterIndex = -1;
		[KSPField(isPersistant = true)] public bool initialized = false;

		private Emitter emitter;

		public override void OnStart(StartState state)
		{
			if (Lib.DisableScenario(this))
				return;

			base.OnStart(state);
			TryInitialize();
		}

		private void TryInitialize()
		{
			if (initialized)
				return;

			emitter = DynamicRadiationLogic.FindPrimaryEmitter(part, ref emitterIndex);
			emitterMaxRadiation = DynamicRadiationLogic.ResolvePeakRadiation(part, emitter, minEmissionPercent, emitterMaxRadiation);

			if (emitter != null && !reactorHasStarted)
			{
				emitter.running = false;
				emitter.radiation = emitterMaxRadiation * minEmissionPercent / 100.0;
			}

			initialized = emitter != null && emitterMaxRadiation > 0.0;
		}

		public void Update()
		{
			if (!Features.Radiation)
				return;

			if (!initialized)
				TryInitialize();

			if (!initialized || emitter == null)
				return;

			if (Lib.IsEditor())
			{
				emitter.running = true;
				emitter.radiation = emitterMaxRadiation;
			}
		}

		public void FixedUpdate()
		{
			if (!Lib.IsFlight() || !Features.Radiation)
				return;

			if (!initialized)
				TryInitialize();

			if (!initialized || emitter == null)
				return;

			bool enabled = DynamicRadiationLogic.GetPowerEnabled(part, powerModuleName, powerModuleId, powerActiveMode);
			DynamicRadiationLogic.UpdateFlight(
				emitter,
				enabled,
				ref reactorHasStarted,
				ref reactorStoppedAt,
				emitterMaxRadiation,
				minEmissionPercent,
				emissionDecayRate);
		}

		public static string BackgroundUpdate(
			Vessel vessel,
			ProtoPartSnapshot partSnapshot,
			ProtoPartModuleSnapshot moduleSnapshot,
			PartModule modulePrefab,
			Part partPrefab,
			Dictionary<string, double> availableResources,
			List<KeyValuePair<string, double>> resourceChangeRequest,
			double elapsed_s)
		{
			if (!Features.Radiation)
				return string.Empty;

			string powerModuleName = Lib.Proto.GetString(moduleSnapshot, "powerModuleName");
			string powerModuleId = Lib.Proto.GetString(moduleSnapshot, "powerModuleId");
			string powerActiveMode = Lib.Proto.GetString(moduleSnapshot, "powerActiveMode");
			if (string.IsNullOrEmpty(powerActiveMode))
				powerActiveMode = "enabled";

			double minEmissionPercent = Lib.Proto.GetDouble(moduleSnapshot, "minEmissionPercent");
			if (minEmissionPercent <= 0.0)
				minEmissionPercent = 25.0;

			double emissionDecayRate = Lib.Proto.GetDouble(moduleSnapshot, "emissionDecayRate");
			if (emissionDecayRate <= 0.0)
				emissionDecayRate = 3600.0;

			int emitterIndex = (int)Lib.Proto.GetDouble(moduleSnapshot, "emitterIndex");
			double peakRadiation = Lib.Proto.GetDouble(moduleSnapshot, "emitterMaxRadiation");
			if (peakRadiation <= 0.0)
			{
				peakRadiation = DynamicRadiationLogic.ResolvePeakRadiation(partPrefab, null, minEmissionPercent, 0.0);
				if (peakRadiation <= 0.0)
					peakRadiation = DynamicRadiationLogic.FindPeakEmitterRadiation(partSnapshot);

				if (peakRadiation <= 0.0)
					return string.Empty;

				if (minEmissionPercent > 0.0 && minEmissionPercent < 100.0)
				{
					double inferred = peakRadiation * 100.0 / minEmissionPercent;
					if (inferred > peakRadiation * 1.01)
						peakRadiation = inferred;
				}

				Lib.Proto.Set(moduleSnapshot, "emitterMaxRadiation", peakRadiation);
			}

			ProtoPartModuleSnapshot emitterSnapshot = DynamicRadiationLogic.FindEmitterSnapshot(partSnapshot, emitterIndex, peakRadiation);
			if (emitterSnapshot == null)
				return string.Empty;

			bool enabled = DynamicRadiationLogic.GetPowerEnabledProto(partSnapshot, powerModuleName, powerModuleId, powerActiveMode);
			bool started = Lib.Proto.GetBool(moduleSnapshot, "reactorHasStarted");
			double stoppedAt = Lib.Proto.GetDouble(moduleSnapshot, "reactorStoppedAt");

			DynamicRadiationLogic.UpdateBackground(
				emitterSnapshot,
				enabled,
				ref started,
				ref stoppedAt,
				peakRadiation,
				minEmissionPercent,
				emissionDecayRate,
				elapsed_s);

			Lib.Proto.Set(moduleSnapshot, "reactorHasStarted", started);
			Lib.Proto.Set(moduleSnapshot, "reactorStoppedAt", stoppedAt);

			return string.Empty;
		}
	}
}
