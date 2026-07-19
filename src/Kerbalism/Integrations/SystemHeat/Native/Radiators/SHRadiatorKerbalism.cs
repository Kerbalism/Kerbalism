using System;
using System.Collections;
using System.Collections.Generic;
using KSP.Localization;
using UnityEngine;

namespace KERBALISM
{
	public class SystemHeatRadiatorKerbalism : PartModule, IKerbalismModule
	{
		struct InputResourceSnapshot
		{
			public readonly string name;
			public readonly double rate;

			public InputResourceSnapshot(string name, double rate)
			{
				this.name = name;
				this.rate = rate;
			}
		}

		[KSPField(isPersistant = true)]
		public float scale = 1f;

		[KSPField(isPersistant = true)]
		public float scaleEmissionPower = 2f;

		[KSPField(isPersistant = true)]
		public bool IsCooling = true;

		[KSPField(isPersistant = false)]
		public string radiatorModuleName = "ModuleSystemHeatRadiator";

		[KSPField(isPersistant = false)]
		public string radiatorModuleID = "";

		[KSPField(isPersistant = false)]
		public string systemHeatRadiatorModuleID = "";

		public static string radiatorTitle = Localizer.Format("#KERBALISM_Brokers_Radiator");

		public FloatCurve temperatureCurve;
		List<InputResourceSnapshot> inputRateSnapshots;
		FloatCurve baseTemperatureCurve;
		int lastUSRadiatorSelection = -1;
		float lastUSRadiatorScale = -1f;
		bool lastSyncedSystemHeatCooling;
		bool hasSyncedSystemHeatCooling;

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
			CaptureInputRateSnapshots();
		}

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			CaptureInputRateSnapshots();
			EnsureBaseTemperatureCurve();
			if (scale != 1f)
				RebuildTemperatureCurve();
			SyncRadiatorState();
		}

		public override void OnSave(ConfigNode node)
		{
			SyncRadiatorState();
			base.OnSave(node);
		}

		PartModule FindNativeRadiatorModule()
		{
			PartModule fallback = null;
			if (part == null)
				return null;

			for (int i = 0; i < part.Modules.Count; i++)
			{
				PartModule module = part.Modules[i];
				if (module == null || module == this)
					continue;

				bool nameMatches = module.moduleName == radiatorModuleName
					|| (string.IsNullOrEmpty(radiatorModuleName) && (module.moduleName == "ModuleSystemHeatRadiator" || module.moduleName == "ModuleActiveRadiator"));
				if (!nameMatches)
					continue;

				if (fallback == null)
					fallback = module;

				if (string.IsNullOrEmpty(radiatorModuleID) || SystemHeat.GetModuleId(module) == radiatorModuleID)
					return module;
			}

			return fallback;
		}

		/// <summary>Native radiator modules controlled by this sidecar (used by Reliability break/repair).</summary>
		public List<PartModule> FindNativeRadiatorsForReliability()
		{
			var radiators = new List<PartModule>();
			PartModule nativeRadiator = FindNativeRadiatorModule();
			if (nativeRadiator != null)
				radiators.Add(nativeRadiator);

			// USRadiatorSwitch must stay enabled for its mesh/function switching UI, so it
			// can't be replaced in-place. Its linked SystemHeat radiator is a second native
			// module and must follow the same Reliability state.
			if (nativeRadiator?.moduleName == "USRadiatorSwitch")
			{
				PartModule systemHeatRadiator = FindLinkedSystemHeatRadiator();
				if (systemHeatRadiator != null && !radiators.Contains(systemHeatRadiator))
					radiators.Add(systemHeatRadiator);
			}

			return radiators;
		}

		/// <summary>Remove the last registered rejection flux before a failed native radiator is disabled.</summary>
		public void ClearRadiatorFluxForReliability(PartModule radiator)
		{
			if (radiator == null || !SystemHeat.IsRadiator(radiator))
				return;

			string heatModuleId = SystemHeat.Get(radiator, "systemHeatModuleID", "");
			PartModule heatModule = SystemHeat.FindHeatModule(part, heatModuleId);
			SystemHeat.AddFlux(heatModule, SystemHeat.GetModuleId(radiator), 0f, 0f, false);
		}

		PartModule FindLinkedSystemHeatRadiator()
		{
			if (part == null)
				return null;

			PartModule fallback = null;
			for (int i = 0; i < part.Modules.Count; i++)
			{
				PartModule module = part.Modules[i];
				if (module == null || module.moduleName != "ModuleSystemHeatRadiator")
					continue;

				if (fallback == null)
					fallback = module;

				if (string.IsNullOrEmpty(systemHeatRadiatorModuleID)
					|| SystemHeat.GetModuleId(module) == systemHeatRadiatorModuleID)
					return module;
			}

			return fallback;
		}

		bool TryGetUSRadiatorSelection(PartModule radiator, out int selection, out float power)
		{
			selection = -1;
			power = 0f;
			if (radiator == null || radiator.moduleName != "USRadiatorSwitch")
				return false;

			string powersString = IntegrationReflection.GetString(radiator, "RadiatorPower");
			if (string.IsNullOrEmpty(powersString))
				return false;

			string[] powers = powersString.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
			selection = IntegrationReflection.GetInt(radiator, "CurrentSelection", -1);
			if (selection < 0 || selection >= powers.Length)
				return false;

			return float.TryParse(powers[selection].Trim(), System.Globalization.NumberStyles.Float,
				System.Globalization.CultureInfo.InvariantCulture, out power)
				&& !float.IsNaN(power) && !float.IsInfinity(power);
		}

		void SyncUSRadiatorSwitch()
		{
			PartModule radiator = FindNativeRadiatorModule();
			PartModule systemHeatRadiator = FindLinkedSystemHeatRadiator();
			if (radiator?.moduleName != "USRadiatorSwitch" || systemHeatRadiator == null
				|| !TryGetUSRadiatorSelection(radiator, out int selection, out float stockTransferPower))
				return;

			// ModuleActiveRadiator maxEnergyTransfer/RadiatorPower is fifty times the
			// equivalent SystemHeat temperature-curve output in kW.
			float scaleFactor = (float)Math.Pow(scale, scaleEmissionPower);
			float systemHeatPower = Math.Max(0f, stockTransferPower / 50f) * scaleFactor;
			if (selection != lastUSRadiatorSelection || !Mathf.Approximately(scaleFactor, lastUSRadiatorScale))
			{
				var curve = new FloatCurve();
				curve.Add(0f, 0f);
				curve.Add(400f, systemHeatPower);
				IntegrationReflection.SetField(systemHeatRadiator, "temperatureCurve", curve);
				lastUSRadiatorSelection = selection;
				lastUSRadiatorScale = scaleFactor;
			}

			// Vostok-style US radiators use AutoEnable=False and never call Enable(), so
			// IsCooling stays false even on the radiator mesh. ActiveCooling is the
			// persistent desired state; also honor ModuleSystemHeatRadiator PAW toggles.
			bool hasRadiatorPower = systemHeatPower > 0f && radiator.isEnabled;
			bool shCooling = IntegrationReflection.GetBool(systemHeatRadiator, "IsCooling", false);
			bool desiredCooling = hasRadiatorPower
				&& IntegrationReflection.GetBool(radiator, "ActiveCooling", true);

			if (hasRadiatorPower && hasSyncedSystemHeatCooling && shCooling != lastSyncedSystemHeatCooling)
			{
				desiredCooling = shCooling;
				IntegrationReflection.SetField(radiator, "ActiveCooling", desiredCooling);
			}

			IntegrationReflection.SetField(radiator, "IsCooling", desiredCooling);
			IntegrationReflection.SetField(systemHeatRadiator, "IsCooling", desiredCooling);
			IsCooling = desiredCooling;
			lastSyncedSystemHeatCooling = desiredCooling;
			hasSyncedSystemHeatCooling = true;
		}

		PartModule FindPrefabRadiatorModule(Part prefab)
		{
			if (prefab == null)
				return null;

			return IntegrationUtils.FindModule(prefab, radiatorModuleName)
				?? IntegrationUtils.FindModule(prefab, "ModuleSystemHeatRadiator")
				?? IntegrationUtils.FindModule(prefab, "ModuleActiveRadiator");
		}

		IList GetNativeInputResources()
		{
			return SystemHeat.GetResHandlerInputResources(FindNativeRadiatorModule());
		}

		IList GetPrefabInputResources()
		{
			return SystemHeat.GetResHandlerInputResources(FindPrefabRadiatorModule(part?.partInfo?.partPrefab));
		}

		bool NativeIsCooling()
		{
			PartModule radiator = FindNativeRadiatorModule();
			if (radiator == null)
				return IsCooling;

			return IntegrationReflection.GetBool(radiator, "IsCooling", IsCooling);
		}

		void SyncCoolingState()
		{
			IsCooling = NativeIsCooling();
		}

		void SyncRadiatorState()
		{
			PartModule radiator = FindNativeRadiatorModule();
			if (radiator?.moduleName == "USRadiatorSwitch" && FindLinkedSystemHeatRadiator() != null)
				SyncUSRadiatorSwitch();
			else
				SyncCoolingState();
		}

		void CaptureInputRateSnapshots()
		{
			IList inputResources = GetNativeInputResources();
			if (inputResources == null || inputResources.Count == 0)
				inputResources = GetPrefabInputResources();
			if (inputResources == null || inputResources.Count == 0)
				return;

			var snapshots = new List<InputResourceSnapshot>(inputResources.Count);
			for (int i = 0; i < inputResources.Count; i++)
			{
				if (inputResources[i] is ModuleResource resource)
					snapshots.Add(new InputResourceSnapshot(resource.name, resource.rate));
			}

			if (snapshots.Count > 0)
				inputRateSnapshots = snapshots;
		}

		void EnsureInputRateSnapshots()
		{
			if (inputRateSnapshots == null || inputRateSnapshots.Count == 0)
				CaptureInputRateSnapshots();
		}

		bool TryAppendUSRadiatorSwitchRequest(List<KeyValuePair<string, double>> resourceChangeRequest, float scaleFactor, float scaleEmissionPowerFactor, int? selectionOverride)
		{
			PartModule radiator = FindNativeRadiatorModule();
			if (radiator == null || radiator.moduleName != "USRadiatorSwitch")
				return false;

			string ratesString = IntegrationReflection.GetString(radiator, "RadiatorEnergy");
			if (string.IsNullOrEmpty(ratesString))
				return false;

			string[] rates = ratesString.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
			int selection = selectionOverride ?? IntegrationReflection.GetInt(radiator, "CurrentSelection", -1);
			if (selection < 0 || selection >= rates.Length)
				return false;

			if (!double.TryParse(rates[selection].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double rate)
				|| double.IsNaN(rate) || double.IsInfinity(rate))
				return false;

			if (rate > 0.0)
			{
				double emissionScale = Math.Pow(scaleFactor, scaleEmissionPowerFactor);
				resourceChangeRequest.Add(new KeyValuePair<string, double>("ElectricCharge", -rate * emissionScale));
			}
			return true;
		}

		void AppendInputResourceRequests(List<KeyValuePair<string, double>> resourceChangeRequest, float scaleFactor, float scaleEmissionPowerFactor, int? selectionOverride = null)
		{
			if (TryAppendUSRadiatorSwitchRequest(resourceChangeRequest, scaleFactor, scaleEmissionPowerFactor, selectionOverride))
				return;

			EnsureInputRateSnapshots();
			if (inputRateSnapshots == null)
				return;

			double emissionScale = Math.Pow(scaleFactor, scaleEmissionPowerFactor);
			for (int i = 0; i < inputRateSnapshots.Count; i++)
			{
				InputResourceSnapshot res = inputRateSnapshots[i];
				resourceChangeRequest.Add(new KeyValuePair<string, double>(res.name, -res.rate * emissionScale));
			}
		}

		void EnsureBaseTemperatureCurve()
		{
			if (baseTemperatureCurve != null && baseTemperatureCurve.Curve != null && baseTemperatureCurve.Curve.length > 0)
				return;

			FloatCurve source = temperatureCurve;
			Part prefab = part?.partInfo?.partPrefab;
			if (prefab != null)
			{
				PartModule prefabRadiator = FindPrefabRadiatorModule(prefab)
					?? prefab.FindModuleImplementing<SystemHeatRadiatorKerbalism>();
				FloatCurve prefabCurve = IntegrationReflection.GetField<FloatCurve>(prefabRadiator, "temperatureCurve");
				if (prefabCurve != null)
					source = prefabCurve;
			}
			baseTemperatureCurve = CloneCurve(source);
		}

		static FloatCurve CloneCurve(FloatCurve source)
		{
			FloatCurve clone = new FloatCurve();
			if (source == null)
				return clone;

			for (int i = 0; i < source.Curve.length; i++)
			{
				Keyframe key = source.Curve.keys[i];
				clone.Add(key.time, key.value);
			}
			return clone;
		}

		void RebuildTemperatureCurve()
		{
			EnsureBaseTemperatureCurve();
			if (baseTemperatureCurve == null || baseTemperatureCurve.Curve == null || baseTemperatureCurve.Curve.length == 0)
				return;

			temperatureCurve = new FloatCurve();
			float scaleFactor = (float)Math.Pow(scale, scaleEmissionPower);
			for (int i = 0; i < baseTemperatureCurve.Curve.length; i++)
			{
				Keyframe key = baseTemperatureCurve.Curve.keys[i];
				temperatureCurve.Add(key.time, key.value * scaleFactor);
			}
			IntegrationReflection.SetField(this, "temperatureCurve", temperatureCurve);
		}

		[KSPEvent]
		void OnPartScaleChanged(BaseEventDetails data)
		{
			scale = data.Get<float>("factorAbsolute");
			RebuildTemperatureCurve();
		}

		public string PlannerUpdate(List<KeyValuePair<string, double>> resourceChangeRequest, CelestialBody body, Dictionary<string, double> environment)
		{
			AppendInputResourceRequests(resourceChangeRequest, scale, scaleEmissionPower);
			return radiatorTitle;
		}

		static bool IsRadiatorReliabilityBroken(ProtoPartSnapshot part)
		{
			if (part == null)
				return false;

			for (int i = 0; i < part.modules.Count; i++)
			{
				ProtoPartModuleSnapshot module = part.modules[i];
				if (module.moduleName != "Reliability" || !Lib.Proto.GetBool(module, "broken"))
					continue;

				string type = Lib.Proto.GetString(module, "type");
				if (type == "SystemHeatRadiatorKerbalism"
					|| type == "ModuleSystemHeatRadiator"
					|| type == "ModuleActiveRadiator"
					|| type == "USRadiatorSwitch")
					return true;
			}
			return false;
		}

		public static string BackgroundUpdate(Vessel v, ProtoPartSnapshot part_snapshot, ProtoPartModuleSnapshot module_snapshot, PartModule proto_part_module, Part proto_part, Dictionary<string, double> availableResources, List<KeyValuePair<string, double>> resourceChangeRequest, double elapsed_s)
		{
			if (!IsRadiatorReliabilityBroken(part_snapshot) && Lib.Proto.GetBool(module_snapshot, "IsCooling", true))
			{
				float scale = Lib.Proto.GetFloat(module_snapshot, "scale");
				float scaleEmissionPower = Lib.Proto.GetFloat(module_snapshot, "scaleEmissionPower");
				if (proto_part_module is SystemHeatRadiatorKerbalism radiator)
				{
					ProtoPartModuleSnapshot nativeRadiator = IntegrationUtils.TryFindPartModuleSnapshot(part_snapshot, radiator.radiatorModuleName);
					int savedSelection = nativeRadiator == null ? -1 : Lib.Proto.GetInt(nativeRadiator, "CurrentSelection", -1);
					int? selection = savedSelection < 0 ? (int?)null : savedSelection;
					radiator.AppendInputResourceRequests(resourceChangeRequest, scale, scaleEmissionPower, selection);
				}
				else
				{
					IList inputResources = SystemHeat.GetResHandlerInputResources(proto_part_module);
					if (inputResources != null)
					{
						double emissionScale = Math.Pow(scale, scaleEmissionPower);
						for (int i = 0; i < inputResources.Count; i++)
						{
							if (inputResources[i] is ModuleResource res)
								resourceChangeRequest.Add(new KeyValuePair<string, double>(res.name, -res.rate * emissionScale));
						}
					}
				}
			}

			SystemHeatBackgroundThermal.TryRun(v, elapsed_s);
			return radiatorTitle;
		}

		public string ResourceUpdate(Dictionary<string, double> availableResources, List<KeyValuePair<string, double>> resourceChangeRequest)
		{
			SyncRadiatorState();
			if (IsCooling)
				AppendInputResourceRequests(resourceChangeRequest, scale, scaleEmissionPower);
			return radiatorTitle;
		}

		public void FixedUpdate()
		{
			SyncRadiatorState();
		}
	}
}
