using System;
using System.Collections.Generic;
using KSP.Localization;
using UnityEngine;

namespace KERBALISM
{
	public class HarvesterSystemHeat : Harvester, IConfigurable, IKerbalismModule
	{
		private static readonly CrewSpecs engineerSpecs = new CrewSpecs("Engineer@0");

		[KSPField] public string systemHeatModuleID = "";
		[KSPField] public float shutdownTemperature = 1000f;
		[KSPField] public float systemOutletTemperature = 1000f;
		[KSPField] public float systemPower = 0f;
		[KSPField] public FloatCurve systemEfficiency = new FloatCurve();
		[KSPField] public bool AutoShutdown = true;
		[KSPField] public bool GeneratesHeat = false;

		private PartModule heatModule;
		private double lastPlannerThermalEff = -1.0;

		public void ModuleIsConfigured() { }

		public void Start()
		{
			heatModule = SystemHeat.FindHeatModule(part, systemHeatModuleID);
		}

		public override string GetInfo()
		{
			string baseInfo = base.GetInfo();
			if (systemPower <= 0f)
				return baseInfo;

			string info = SystemHeat.FormatPartInfoAdd(systemPower, systemOutletTemperature, shutdownTemperature);
			int pos = baseInfo.IndexOf("\n\n");
			return pos < 0 ? baseInfo + "\n\n" + info : baseInfo.Substring(0, pos) + info + baseInfo.Substring(pos);
		}

		public void Configure(bool enable, int multiplier)
		{
			if (!enable)
			{
				DisableModule();
				SystemHeat.AddFlux(heatModule, resource, 0f, 0f, false);
			}
		}

		public new void FixedUpdate()
		{
			base.FixedUpdate();

			if (heatModule == null)
				heatModule = SystemHeat.FindHeatModule(part, systemHeatModuleID);
			if (heatModule == null)
				return;

			ApplyFlux();
			if (Lib.IsFlight())
				CheckThermalShutdown();
			else if (Lib.IsEditor())
				RefreshPlannerIfThermalEfficiencyChanged();
		}

		public string ResourceUpdate(Dictionary<string, double> availableResources, List<KeyValuePair<string, double>> resourceChangeRequest)
		{
			return title;
		}

		public string PlannerUpdate(List<KeyValuePair<string, double>> resourceChangeRequest, CelestialBody body, Dictionary<string, double> environment)
		{
			if (!running || simulated_abundance <= min_abundance)
				return title;

			double thermalEff = GetThermalEfficiencyScale();
			if (ec_rate > double.Epsilon)
				resourceChangeRequest.Add(new KeyValuePair<string, double>("ElectricCharge", -ec_rate * thermalEff));

			resourceChangeRequest.Add(new KeyValuePair<string, double>(
				resource,
				Harvester.AdjustedRate(this, engineerSpecs, GetEditorCrew(), simulated_abundance) * thermalEff));

			return title;
		}

		public static string BackgroundUpdate(
			Vessel v,
			ProtoPartSnapshot partSnapshot,
			ProtoPartModuleSnapshot moduleSnapshot,
			PartModule protoPartModule,
			Part protoPart,
			Dictionary<string, double> availableResources,
			List<KeyValuePair<string, double>> resourceChangeRequest,
			double elapsed_s)
		{
			Harvester.BackgroundUpdate(v, moduleSnapshot, protoPartModule as Harvester, elapsed_s);
			SystemHeatBackgroundThermal.TryRun(v, elapsed_s);
			return protoPartModule == null ? "" : protoPartModule.GetModuleDisplayName();
		}

		private void ApplyFlux()
		{
			bool active = ModuleIsActive();
			SystemHeat.AddFlux(heatModule, resource, active ? systemOutletTemperature : 0f, active ? systemPower : 0f, active);
		}

		private void CheckThermalShutdown()
		{
			if (!AutoShutdown || !ModuleIsActive())
				return;

			if (SystemHeat.CurrentLoopTemperature(heatModule) <= shutdownTemperature)
				return;

			ScreenMessages.PostScreenMessage(new ScreenMessage(
				Localizer.Format("#LOC_SystemHeat_ModuleSystemHeatHarvester_Message_Shutdown", part.partInfo.title),
				3.0f,
				ScreenMessageStyle.UPPER_CENTER));
			DisableModule();
			ApplyFlux();
		}

		private double GetThermalEfficiencyScale()
		{
			return SystemHeatEditorSimulation.EvaluateEfficiency(systemEfficiency, SystemHeat.CurrentLoopTemperature(heatModule));
		}

		private void RefreshPlannerIfThermalEfficiencyChanged()
		{
			double thermalEff = GetThermalEfficiencyScale();
			if (Math.Abs(thermalEff - lastPlannerThermalEff) <= SystemHeatEditorSimulation.HystFrac)
				return;
			lastPlannerThermalEff = thermalEff;
			Lib.RefreshPlanner();
		}

		private static List<ProtoCrewMember> GetEditorCrew()
		{
			if (KSP.UI.CrewAssignmentDialog.Instance == null)
				return new List<ProtoCrewMember>();
			VesselCrewManifest manifest = KSP.UI.CrewAssignmentDialog.Instance.GetManifest();
			return manifest != null ? manifest.GetAllCrew(false).FindAll(k => k != null) : new List<ProtoCrewMember>();
		}
	}
}
