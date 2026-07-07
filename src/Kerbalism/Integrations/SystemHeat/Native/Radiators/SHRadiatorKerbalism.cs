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

		public static string radiatorTitle = Localizer.Format("#KERBALISM_Brokers_Radiator");

		public FloatCurve temperatureCurve;
		List<InputResourceSnapshot> inputRateSnapshots;
		FloatCurve baseTemperatureCurve;

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
			CaptureInputRateSnapshots();
		}

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			SyncCoolingState();
			CaptureInputRateSnapshots();
			EnsureBaseTemperatureCurve();
			if (scale != 1f)
				RebuildTemperatureCurve();
		}

		public override void OnSave(ConfigNode node)
		{
			SyncCoolingState();
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

		void AppendInputResourceRequests(List<KeyValuePair<string, double>> resourceChangeRequest, float scaleFactor, float scaleEmissionPowerFactor)
		{
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

		public static string BackgroundUpdate(Vessel v, ProtoPartSnapshot part_snapshot, ProtoPartModuleSnapshot module_snapshot, PartModule proto_part_module, Part proto_part, Dictionary<string, double> availableResources, List<KeyValuePair<string, double>> resourceChangeRequest, double elapsed_s)
		{
			if (Lib.Proto.GetBool(module_snapshot, "IsCooling", true))
			{
				float scale = Lib.Proto.GetFloat(module_snapshot, "scale");
				float scaleEmissionPower = Lib.Proto.GetFloat(module_snapshot, "scaleEmissionPower");
				if (proto_part_module is SystemHeatRadiatorKerbalism radiator)
					radiator.AppendInputResourceRequests(resourceChangeRequest, scale, scaleEmissionPower);
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
			SyncCoolingState();
			if (IsCooling)
				AppendInputResourceRequests(resourceChangeRequest, scale, scaleEmissionPower);
			return radiatorTitle;
		}

		public void FixedUpdate()
		{
			SyncCoolingState();
		}
	}
}
