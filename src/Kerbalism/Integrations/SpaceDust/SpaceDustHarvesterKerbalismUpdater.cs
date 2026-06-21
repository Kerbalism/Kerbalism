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

			IList resources = SpaceDust.GetHarvestedResources(harvester);
			if (resources == null || resources.Count == 0)
				return;

			double intakeVolume = ComputeIntakeVolume(harvester);
			if (intakeVolume <= double.Epsilon)
				return;

			double altitude = vessel.altitude + vessel.mainBody.Radius;
			for (int i = 0; i < resources.Count; i++)
			{
				object res = resources[i];
				if (res == null)
					continue;

				string name = SpaceDust.GetHarvestedResourceName(res);
				if (string.IsNullOrEmpty(name))
					continue;

				double density = SpaceDust.GetHarvestedResourceDensity(res);
				double sample = SpaceDust.SampleResource(name, vessel.mainBody, altitude, vessel.latitude, vessel.longitude);
				double rate = sample * intakeVolume * SpaceDust.GetHarvestedResourceBaseEfficiency(res) * scale / density;
				if (rate <= SpaceDust.GetHarvestedResourceMinHarvestValue(res))
					continue;

				resourceChangeRequest.Add(new KeyValuePair<string, double>(name, rate));
			}
		}

		private static double ComputeIntakeVolume(PartModule harvester)
		{
			Vessel vessel = harvester.vessel;
			if (vessel == null)
				return 0d;

			string transformName = SpaceDust.Get(harvester, "HarvestIntakeTransformName", "");
			Transform intakeTransform = null;
			if (!string.IsNullOrEmpty(transformName))
				intakeTransform = harvester.part.FindModelTransform(transformName);
			if (intakeTransform == null)
				intakeTransform = harvester.part.transform;

			object harvestType = IntegrationReflection.GetField<object>(harvester, "HarvestType");
			string harvestTypeName = harvestType?.ToString() ?? "";

			if (harvestTypeName.Contains("Atmosphere"))
			{
				if (vessel.atmDensity <= 0d)
					return 0d;

				Vector3d worldVelocity = vessel.srf_velocity;
				double mach = vessel.mach;
				double dot = Vector3d.Dot(worldVelocity, intakeTransform.forward);
				object intakeVelocityScale = IntegrationReflection.GetField<object>(harvester, "IntakeVelocityScale");
				float intakeSpeedStatic = SpaceDust.Get(harvester, "IntakeSpeedStatic", 0f);
				float intakeArea = SpaceDust.Get(harvester, "IntakeArea", 0f);
				return (worldVelocity.magnitude * Math.Max(dot, 0d) * IntegrationReflection.EvaluateFloatCurve(intakeVelocityScale, (float)mach, 1f) + intakeSpeedStatic) * intakeArea;
			}

			if (harvestTypeName.Contains("Exosphere"))
			{
				if (vessel.atmDensity > 0d)
					return 0d;

				Vector3d worldVelocity = vessel.obt_velocity;
				double dot = Vector3d.Dot(worldVelocity.normalized, intakeTransform.forward.normalized);
				float intakeSpeedStatic = SpaceDust.Get(harvester, "IntakeSpeedStatic", 0f);
				float intakeArea = SpaceDust.Get(harvester, "IntakeArea", 0f);
				return (worldVelocity.magnitude * Math.Max(dot, 0d) + intakeSpeedStatic) * intakeArea;
			}

			return SpaceDust.Get(harvester, "IntakeSpeedStatic", 0f) * SpaceDust.Get(harvester, "IntakeArea", 0f);
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
			ProtoPartModuleSnapshot harvesterSnapshot = IntegrationUtils.TryFindPartModuleSnapshot(part_snapshot, "ModuleSpaceDustHarvester");
			if (harvesterSnapshot != null && Lib.Proto.GetBool(harvesterSnapshot, "Enabled"))
				Lib.Proto.Set(harvesterSnapshot, "Enabled", false);

			SystemHeatBackgroundThermal.TryRun(v, elapsed_s);
			return brokerTitle;
		}
	}
}
