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

		private double GetThermalScale()
		{
			PartModule harvester = NativeHarvester;
			if (harvester == null)
				return 1d;

			PartModule heatModule = FindLinkedHeatModule(harvester);
			if (heatModule == null)
				return 1d;

			float loopTemp = IntegrationReflection.GetFloat(heatModule, "currentLoopTemperature");
			object efficiencyCurve = IntegrationReflection.GetField<object>(harvester, "SystemEfficiency");
			return IntegrationReflection.EvaluateFloatCurve(efficiencyCurve, loopTemp, 1f);
		}

		private PartModule FindLinkedHeatModule(PartModule harvester)
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

		public string ResourceUpdate(Dictionary<string, double> availableResources, List<KeyValuePair<string, double>> resourceChangeRequest)
		{
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

		internal static void AddBackgroundHarvestRates(
			Vessel v,
			PartModule harvesterPrefab,
			List<KeyValuePair<string, double>> resourceChangeRequest,
			ProtoPartSnapshot partSnapshot,
			string harvesterModuleId)
		{
			if (v == null || harvesterPrefab == null || partSnapshot == null)
				return;

			ProtoPartModuleSnapshot harvesterSnapshot = FindHarvesterSnapshot(partSnapshot, harvesterModuleId);
			if (harvesterSnapshot == null || !Lib.Proto.GetBool(harvesterSnapshot, "Enabled"))
				return;

			double scale = GetBackgroundThermalScale(partSnapshot, harvesterPrefab, harvesterSnapshot);
			float powerCost = SpaceDust.Get(harvesterPrefab, "PowerCost", 0f);
			if (powerCost > 0f)
				resourceChangeRequest.Add(new KeyValuePair<string, double>("ElectricCharge", -powerCost * scale));

			AddHarvestRatesFromModule(harvesterPrefab, v, resourceChangeRequest, scale);
		}

		private static ProtoPartModuleSnapshot FindHarvesterSnapshot(ProtoPartSnapshot part, string harvesterModuleId)
		{
			ProtoPartModuleSnapshot fallback = null;
			foreach (ProtoPartModuleSnapshot module in part.modules)
			{
				if (module.moduleName != "ModuleSpaceDustHarvester")
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
				return IntegrationReflection.EvaluateFloatCurve(efficiencyCurve, loopTemp, 1f);
			}

			return 1d;
		}

		private static void AddHarvestRatesFromModule(
			PartModule harvester,
			Vessel v,
			List<KeyValuePair<string, double>> resourceChangeRequest,
			double scale)
		{
			IList resources = SpaceDust.GetHarvestedResources(harvester);
			if (resources == null || resources.Count == 0)
				return;

			double intakeVolume = ComputeIntakeVolume(harvester, v);
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

		private static double ComputeIntakeVolume(PartModule harvester, Vessel v)
		{
			if (harvester == null || v == null || v.mainBody == null)
				return 0d;

			Transform intakeTransform = null;
			if (harvester.part != null)
			{
				string transformName = SpaceDust.Get(harvester, "HarvestIntakeTransformName", "");
				if (!string.IsNullOrEmpty(transformName))
					intakeTransform = harvester.part.FindModelTransform(transformName);
				if (intakeTransform == null)
					intakeTransform = harvester.part.transform;
			}

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
				double dot = intakeTransform != null
					? Vector3d.Dot(worldVelocity, intakeTransform.forward)
					: worldVelocity.magnitude;
				object intakeVelocityScale = IntegrationReflection.GetField<object>(harvester, "IntakeVelocityScale");
				return (worldVelocity.magnitude * Math.Max(dot, 0d) * IntegrationReflection.EvaluateFloatCurve(intakeVelocityScale, (float)mach, 1f) + intakeSpeedStatic) * intakeArea;
			}

			if (harvestTypeName.Contains("Exosphere"))
			{
				if (v.atmDensity > 0d)
					return 0d;

				Vector3d worldVelocity = v.obt_velocity;
				double dot = intakeTransform != null
					? Vector3d.Dot(worldVelocity.normalized, intakeTransform.forward.normalized)
					: 1d;
				return (worldVelocity.magnitude * Math.Max(dot, 0d) + intakeSpeedStatic) * intakeArea;
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
			AddBackgroundHarvestRates(v, proto_part_module, resourceChangeRequest, part_snapshot, harvesterModuleId);
			SystemHeatBackgroundThermal.TryRun(v, elapsed_s);
			return brokerTitle;
		}
	}
}
