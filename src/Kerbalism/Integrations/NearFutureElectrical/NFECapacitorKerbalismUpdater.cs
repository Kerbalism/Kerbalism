using System.Collections.Generic;
using KSP.Localization;

namespace KERBALISM
{
	public class NFECapacitorKerbalismUpdater : PartModule, IKerbalismModule
	{
		public static string brokerName = "NFECapacitor";
		public static string brokerTitle = Localizer.Format("#KERBALISM_Brokers_Capacitor");

		protected PartModule capacitorModule;
		private bool lastPlannerDischarging;
		private bool lastPlannerCharging;
		private bool lastPlannerRechargeEnabled;

		internal PartModule CapacitorModule => capacitorModule;

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			if (capacitorModule == null)
				capacitorModule = FindCapacitorModule(part);
		}

		public void FixedUpdate()
		{
			if (capacitorModule == null)
				capacitorModule = FindCapacitorModule(part);
			if (capacitorModule == null)
				return;

			if (Lib.IsFlight())
				RefreshPlannerIfStateChanged();
		}

		public void Update()
		{
			if (capacitorModule == null)
				capacitorModule = FindCapacitorModule(part);
			if (capacitorModule == null)
				return;

			if (Lib.IsEditor())
			{
				CapacitorResourceSim.SyncCapacitorVisuals(capacitorModule);
				RefreshPlannerIfStateChanged();
			}
		}

		public string ResourceUpdate(Dictionary<string, double> availableResources, List<KeyValuePair<string, double>> resourceChangeRequest)
		{
			if (capacitorModule == null)
				capacitorModule = FindCapacitorModule(part);
			return CapacitorResourceSim.UpdateLoaded(capacitorModule, vessel, brokerName, brokerTitle);
		}

		public string PlannerUpdate(List<KeyValuePair<string, double>> resourceChangeRequest, CelestialBody body, Dictionary<string, double> environment)
		{
			if (capacitorModule == null)
				capacitorModule = FindCapacitorModule(part);
			if (capacitorModule != null)
				CapacitorResourceSim.AddPlannerRates(capacitorModule, resourceChangeRequest);
			return brokerTitle;
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
			ProtoPartModuleSnapshot capacitor = IntegrationUtils.TryFindPartModuleSnapshot(part_snapshot, "DischargeCapacitor");
			if (capacitor == null)
				return "ERR: no capacitor";

			return CapacitorResourceSim.BackgroundUpdate(v, part_snapshot, capacitor, proto_part, resourceChangeRequest, elapsed_s);
		}

		private void RefreshPlannerIfStateChanged()
		{
			if (capacitorModule == null)
				return;

			bool plannerDischarging = CapacitorResourceSim.IsDischarging(capacitorModule);
			bool plannerCharging = CapacitorResourceSim.ShouldPlannerReportCharging(capacitorModule);
			bool plannerRechargeEnabled = NearFutureElectrical.Get(capacitorModule, "Enabled", false);
			if (plannerDischarging == lastPlannerDischarging
				&& plannerCharging == lastPlannerCharging
				&& plannerRechargeEnabled == lastPlannerRechargeEnabled)
				return;

			lastPlannerDischarging = plannerDischarging;
			lastPlannerCharging = plannerCharging;
			lastPlannerRechargeEnabled = plannerRechargeEnabled;
			Lib.RefreshPlanner();
		}

		internal static PartModule FindCapacitorModule(Part part)
		{
			return NearFutureElectrical.FindCapacitorModule(part);
		}
	}
}
