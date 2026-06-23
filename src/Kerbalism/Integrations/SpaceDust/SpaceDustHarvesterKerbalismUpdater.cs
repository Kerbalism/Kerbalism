using System;
using System.Collections;
using System.Collections.Generic;
using KSP.Localization;
using UnityEngine;

namespace KERBALISM
{
	/// <summary>
	/// Kerbalism resource routing for SpaceDust harvesters; native module keeps intake physics, heat, and UI.
	/// </summary>
	public class SpaceDustHarvesterKerbalismUpdater : PartModule, IKerbalismModule
	{
		public static string brokerName = "SpaceDustHarvester";
		public static string brokerTitle = Localizer.Format("#LOC_SpaceDust_ModuleSpaceDustHarvester_DisplayName");

		[KSPField(isPersistant = true)]
		public string harvesterModuleID = "harvester";

		private PartModule nativeHarvester;
		private bool nativeResolved;

		private PartModule NativeHarvester
		{
			get
			{
				if (!nativeResolved)
				{
					nativeResolved = true;
					nativeHarvester = SpaceDust.FindHarvesterModule(part);
				}

				return nativeHarvester;
			}
		}

		private bool IsEnabled()
		{
			PartModule harvester = NativeHarvester;
			return harvester != null && SpaceDust.Get(harvester, "Enabled", false);
		}

		private float GetPowerCost()
		{
			PartModule harvester = NativeHarvester;
			return harvester != null ? SpaceDust.Get(harvester, "PowerCost", 0f) : 0f;
		}

		private static double GetThermalScale(PartModule harvester, Part part)
		{
			if (harvester == null || part == null)
				return 1d;

			PartModule heatModule = FindLinkedHeatModule(harvester, part);
			if (heatModule == null)
				return 1d;

			float loopTemp = IntegrationReflection.GetFloat(heatModule, "currentLoopTemperature");
			object efficiencyCurve = IntegrationReflection.GetField<object>(harvester, "SystemEfficiency");
			return EvaluateThermalScale(efficiencyCurve, loopTemp);
		}

		private static PartModule FindLinkedHeatModule(PartModule harvester, Part part)
		{
			string heatModuleId = SpaceDust.Get(harvester, "HeatModuleID", "");
			foreach (PartModule module in part.Modules)
			{
				if (module.moduleName != "ModuleSystemHeat")
					continue;
				if (IntegrationReflection.GetString(module, "moduleID") == heatModuleId)
					return module;
			}

			return null;
		}

		/// <summary>SystemEfficiency curves are 0–1; clamp so Kerbalism rates never exceed nominal cfg.</summary>
		private static double EvaluateThermalScale(object efficiencyCurve, float loopTemperatureK)
		{
			float thermal = IntegrationReflection.EvaluateFloatCurve(efficiencyCurve, loopTemperatureK, 1f);
			return Mathf.Clamp(thermal, 0f, 1f);
		}

		/// <summary>Native FixedUpdate prepays EC for the whole physics step; use ~1s for Kerbalism UI sync.</summary>
		internal static bool HasOperatingPower(PartModule harvester, Vessel v)
		{
			if (harvester == null || v == null || !SpaceDust.Get(harvester, "Enabled", false))
				return false;

			float powerCost = SpaceDust.Get(harvester, "PowerCost", 0f);
			if (powerCost <= 0f)
				return true;

			float minResToLeave = SpaceDust.Get(harvester, "minResToLeave", 0.1f);
			ResourceInfo ec = KERBALISM.ResourceCache.GetResource(v, "ElectricCharge");
			return ec.Amount >= powerCost + minResToLeave;
		}

		internal static bool IsThermallyShutdown(PartModule harvester, Part part)
		{
			PartModule heatModule = FindLinkedHeatModule(harvester, part);
			if (heatModule == null)
				return false;

			float loopTemp = IntegrationReflection.GetFloat(heatModule, "currentLoopTemperature");
			float shutdown = SpaceDust.Get(harvester, "ShutdownTemperature", float.MaxValue);
			return loopTemp > shutdown;
		}

		/// <summary>
		/// Native FixedUpdate still runs scoop animations while Kerbalism blocks RequestResource.
		/// Reconcile UI/VFX with actual harvester state after each physics step.
		/// </summary>
		internal static void SyncNativeUiAfterFixedUpdate(PartModule harvester)
		{
			if (harvester == null || harvester.vessel == null)
				return;

			if (!SpaceDust.Get(harvester, "Enabled", false))
				return;

			if (IsThermallyShutdown(harvester, harvester.part))
				return;

			if (!HasOperatingPower(harvester, harvester.vessel))
				return;

			double scale = GetThermalScale(harvester, harvester.part);
			IntegrationReflection.Call(harvester, "DoFocusedHarvesting", new object[] { scale }, new[] { typeof(double) });

			SpaceDust.Set(harvester, "ScannerUI", Localizer.Format("#LOC_SpaceDust_ModuleSpaceDustHarvester_Field_Resources_Harvesting"));
			harvester.Fields["IntakeSpeed"].guiActive = true;
			harvester.Fields["ScoopUI"].guiActive = true;

			PartModule heatModule = FindLinkedHeatModule(harvester, harvester.part);
			if (heatModule != null)
			{
				float loopTemp = IntegrationReflection.GetFloat(heatModule, "currentLoopTemperature");
				object efficiencyCurve = IntegrationReflection.GetField<object>(harvester, "SystemEfficiency");
				float efficiencyPct = IntegrationReflection.EvaluateFloatCurve(efficiencyCurve, loopTemp, 1f) * 100f;
				harvester.Fields["ThermalUI"].guiActive = true;
				SpaceDust.Set(
					harvester,
					"ThermalUI",
					Localizer.Format("#LOC_SpaceDust_ModuleSpaceDustHarvester_Field_Thermal_Running", efficiencyPct.ToString("F1")));
			}
		}

		private double GetThermalScale()
		{
			PartModule harvester = NativeHarvester;
			return harvester != null ? GetThermalScale(harvester, part) : 1d;
		}

		private PartModule FindLinkedHeatModule(PartModule harvester)
		{
			return FindLinkedHeatModule(harvester, part);
		}

		public string ResourceUpdate(Dictionary<string, double> availableResources, List<KeyValuePair<string, double>> resourceChangeRequest)
		{
			SyncProtoState();
			if (!IsEnabled())
				return brokerTitle;

			double scale = GetThermalScale();
			float powerCost = GetPowerCost();
			if (powerCost > 0f)
				resourceChangeRequest.Add(new KeyValuePair<string, double>("ElectricCharge", -powerCost * scale));

			AddHarvestRates(resourceChangeRequest, scale);
			return brokerTitle;
		}

		public string PlannerUpdate(List<KeyValuePair<string, double>> resourceChangeRequest, CelestialBody body, Dictionary<string, double> environment)
		{
			if (!IsEnabled())
				return brokerTitle;

			float powerCost = GetPowerCost();
			if (powerCost > 0f)
				resourceChangeRequest.Add(new KeyValuePair<string, double>("ElectricCharge", -powerCost));

			AddHarvestRates(resourceChangeRequest, 1d);
			return brokerTitle;
		}

		private void AddHarvestRates(List<KeyValuePair<string, double>> resourceChangeRequest, double scale)
		{
			PartModule harvester = NativeHarvester;
			if (harvester == null || vessel == null)
				return;

			AddHarvestRatesFromModule(harvester, vessel, resourceChangeRequest, scale);
		}

		private void SyncProtoState()
		{
			PartModule harvester = NativeHarvester;
			if (harvester == null)
				return;

			ProtoPartSnapshot partSnapshot = part.protoPartSnapshot;
			if (partSnapshot == null)
				return;

			ProtoPartModuleSnapshot harvesterSnapshot = FindHarvesterSnapshot(partSnapshot, part.partInfo.partPrefab, harvesterModuleID);
			if (harvesterSnapshot != null)
				Lib.Proto.Set(harvesterSnapshot, "Enabled", SpaceDust.Get(harvester, "Enabled", false));
		}

		internal static void AddBackgroundHarvestRates(
			Vessel v,
			PartModule harvesterPrefab,
			List<KeyValuePair<string, double>> resourceChangeRequest,
			ProtoPartSnapshot partSnapshot,
			Part partPrefab,
			string harvesterModuleId)
		{
			if (v == null || harvesterPrefab == null || partSnapshot == null)
				return;

			ProtoPartModuleSnapshot harvesterSnapshot = FindHarvesterSnapshot(partSnapshot, partPrefab, harvesterModuleId);
			if (harvesterSnapshot == null || !IsHarvesterEnabledInProto(harvesterSnapshot))
				return;

			// Atmospheric ram scoops need loaded flight physics; on-rails srf_velocity/mach are unreliable.
			if (IsAtmosphereHarvester(harvesterPrefab))
				return;

			if (!HasBackgroundOperatingPower(v, harvesterPrefab))
				return;

			double scale = GetBackgroundThermalScale(partSnapshot, harvesterPrefab, harvesterSnapshot);
			float powerCost = SpaceDust.Get(harvesterPrefab, "PowerCost", 0f);
			if (powerCost > 0f)
				resourceChangeRequest.Add(new KeyValuePair<string, double>("ElectricCharge", -powerCost * scale));

			// Exosphere (PK-EXO): background cannot resolve intake orientation; assume ideal alignment.
			AddHarvestRatesFromModule(harvesterPrefab, v, resourceChangeRequest, scale, intakeAlignment: 1d);
		}

		private static bool IsHarvesterEnabledInProto(ProtoPartModuleSnapshot snapshot)
		{
			if (snapshot == null)
				return false;

			string raw = Lib.Proto.GetString(snapshot, "Enabled");
			if (string.IsNullOrEmpty(raw))
				return Lib.Proto.GetBool(snapshot, "Enabled");

			if (bool.TryParse(raw, out bool enabled))
				return enabled;

			return raw == "1";
		}

		private static bool HasBackgroundOperatingPower(Vessel v, PartModule harvesterPrefab)
		{
			float powerCost = SpaceDust.Get(harvesterPrefab, "PowerCost", 0f);
			if (powerCost <= 0f)
				return true;

			float minResToLeave = SpaceDust.Get(harvesterPrefab, "minResToLeave", 0.1f);
			ResourceInfo ec = KERBALISM.ResourceCache.GetResource(v, "ElectricCharge");
			return ec.Amount >= powerCost + minResToLeave;
		}

		private static ProtoPartModuleSnapshot FindHarvesterSnapshot(ProtoPartSnapshot part, Part partPrefab, string harvesterModuleId)
		{
			if (part == null)
				return null;

			var prefabData = new Dictionary<string, Lib.Module_prefab_data>();
			ProtoPartModuleSnapshot fallback = null;
			foreach (ProtoPartModuleSnapshot module in part.modules)
			{
				PartModule prefabModule = partPrefab != null
					? Lib.ModulePrefab(partPrefab.Modules, module.moduleName, prefabData)
					: null;
				bool isHarvester = module.moduleName == "ModuleSpaceDustHarvester"
					|| (prefabModule != null && (SpaceDust.IsHarvester(prefabModule) || prefabModule.moduleName == "ModuleSpaceDustHarvester"));
				if (!isHarvester)
					continue;

				if (fallback == null)
					fallback = module;

				string moduleId = Lib.Proto.GetString(module, "ModuleID");
				if (string.IsNullOrEmpty(harvesterModuleId) || moduleId == harvesterModuleId)
					return module;
			}

			return fallback;
		}

		private static double GetBackgroundThermalScale(ProtoPartSnapshot part, PartModule harvesterPrefab, ProtoPartModuleSnapshot harvesterSnapshot)
		{
			string heatModuleId = SpaceDust.Get(harvesterPrefab, "HeatModuleID", "");
			if (string.IsNullOrEmpty(heatModuleId))
				return 1d;

			foreach (ProtoPartModuleSnapshot module in part.modules)
			{
				if (module.moduleName != "ModuleSystemHeat")
					continue;
				if (Lib.Proto.GetString(module, "moduleID") != heatModuleId)
					continue;

				float loopTemp = Lib.Proto.GetFloat(module, "currentLoopTemperature");
				if (loopTemp <= 0f)
					return 1d;

				object efficiencyCurve = IntegrationReflection.GetField<object>(harvesterPrefab, "SystemEfficiency");
				return EvaluateThermalScale(efficiencyCurve, loopTemp);
			}

			return 1d;
		}

		private static void AddHarvestRatesFromModule(
			PartModule harvester,
			Vessel v,
			List<KeyValuePair<string, double>> resourceChangeRequest,
			double scale,
			double intakeAlignment = double.NaN)
		{
			IList resources = SpaceDust.GetHarvestedResources(harvester);
			if (resources == null || resources.Count == 0)
				return;

			double intakeVolume = ComputeIntakeVolume(harvester, v, intakeAlignment);
			if (intakeVolume <= double.Epsilon)
				return;

			double altitude = v.altitude + v.mainBody.Radius;
			for (int i = 0; i < resources.Count; i++)
			{
				object res = resources[i];
				if (res == null)
					continue;

				string name = SpaceDust.GetHarvestedResourceName(res);
				if (string.IsNullOrEmpty(name))
					continue;

				double density = SpaceDust.GetHarvestedResourceDensity(res);
				double sample = SpaceDust.SampleResource(name, v.mainBody, altitude, v.latitude, v.longitude);
				double rate = sample * intakeVolume * SpaceDust.GetHarvestedResourceBaseEfficiency(res) * scale / density;
				if (rate <= SpaceDust.GetHarvestedResourceMinHarvestValue(res))
					continue;

				resourceChangeRequest.Add(new KeyValuePair<string, double>(name, rate));
			}
		}

		private static bool IsAtmosphereHarvester(PartModule harvester)
		{
			object harvestType = IntegrationReflection.GetField<object>(harvester, "HarvestType");
			string harvestTypeName = harvestType?.ToString() ?? "";
			return harvestTypeName.Contains("Atmosphere");
		}

		private static Vector3d GetExosphereOrbitalVelocity(Vessel v)
		{
			if (v == null)
				return Vector3d.zero;

			Vector3d velocity = v.obt_velocity;
			if (velocity.sqrMagnitude > 1e-6)
				return velocity;

			if (v.orbit != null && v.orbit.vel.sqrMagnitude > 1e-6)
				return v.orbit.vel;

			return Vector3d.zero;
		}

		private static Transform FindIntakeTransform(PartModule harvester)
		{
			if (harvester?.part == null)
				return null;

			string transformName = SpaceDust.Get(harvester, "HarvestIntakeTransformName", "");
			if (!string.IsNullOrEmpty(transformName))
			{
				Transform intakeTransform = harvester.part.FindModelTransform(transformName);
				if (intakeTransform != null)
					return intakeTransform;
			}

			return harvester.part.transform;
		}

		private static double ComputeIntakeVolume(PartModule harvester, Vessel v, double intakeAlignment = double.NaN)
		{
			if (harvester == null || v == null || v.mainBody == null)
				return 0d;

			bool useCachedAlignment = !double.IsNaN(intakeAlignment);
			Transform intakeTransform = useCachedAlignment ? null : FindIntakeTransform(harvester);
			object harvestType = IntegrationReflection.GetField<object>(harvester, "HarvestType");
			string harvestTypeName = harvestType?.ToString() ?? "";
			float intakeSpeedStatic = SpaceDust.Get(harvester, "IntakeSpeedStatic", 0f);
			float intakeArea = SpaceDust.Get(harvester, "IntakeArea", 0f);

			if (harvestTypeName.Contains("Atmosphere"))
			{
				if (v.atmDensity <= 0d)
					return 0d;

				Vector3d worldVelocity = v.srf_velocity;
				double mach = v.mach;
				double alignment = useCachedAlignment
					? intakeAlignment
					: (intakeTransform != null
						? Math.Max(Vector3d.Dot(worldVelocity, intakeTransform.forward), 0d)
						: Math.Max(worldVelocity.magnitude, 0d));
				object intakeVelocityScale = IntegrationReflection.GetField<object>(harvester, "IntakeVelocityScale");
				return (worldVelocity.magnitude * alignment * IntegrationReflection.EvaluateFloatCurve(intakeVelocityScale, (float)mach, 1f) + intakeSpeedStatic) * intakeArea;
			}

			if (harvestTypeName.Contains("Exosphere"))
			{
				if (v.atmDensity > 0d)
					return 0d;

				Vector3d worldVelocity = GetExosphereOrbitalVelocity(v);
				double alignment = useCachedAlignment
					? intakeAlignment
					: (intakeTransform != null
						? Math.Max(Vector3d.Dot(worldVelocity.normalized, intakeTransform.forward.normalized), 0d)
						: 1d);
				return (worldVelocity.magnitude * alignment + intakeSpeedStatic) * intakeArea;
			}

			return intakeSpeedStatic * intakeArea;
		}

		public static string BackgroundUpdate(
			Vessel v,
			ProtoPartSnapshot part_snapshot,
			ProtoPartModuleSnapshot module_snapshot,
			PartModule proto_part_module,
			Part proto_part,
			Dictionary<string, double> availableResources,
			List<KeyValuePair<string, double>> resourceChangeRequest,
			double elapsed_s)
		{
			string harvesterModuleId = Lib.Proto.GetString(module_snapshot, "harvesterModuleID", "harvester");
			PartModule harvesterPrefab = FindHarvesterPrefab(proto_part, harvesterModuleId);
			AddBackgroundHarvestRates(v, harvesterPrefab, resourceChangeRequest, part_snapshot, proto_part, harvesterModuleId);
			SystemHeatBackgroundThermal.TryRun(v, elapsed_s);
			return brokerTitle;
		}

		private static PartModule FindHarvesterPrefab(Part protoPart, string harvesterModuleId)
		{
			if (protoPart == null)
				return null;

			PartModule fallback = null;
			for (int i = 0; i < protoPart.Modules.Count; i++)
			{
				PartModule module = protoPart.Modules[i];
				if (!SpaceDust.IsHarvester(module) && module.moduleName != "ModuleSpaceDustHarvester")
					continue;

				if (fallback == null)
					fallback = module;

				string nativeId = SpaceDust.Get(module, "ModuleID", "");
				if (string.IsNullOrEmpty(harvesterModuleId) || nativeId == harvesterModuleId)
					return module;
			}

			return fallback;
		}
	}
}
