using System.Collections.Generic;

namespace KERBALISM
{
	/// <summary>
	/// Kerbalism bridge for Planetside Exploration Technologies wind turbines.
	/// Stock EC output is suppressed via Harmony; this module feeds Kerbalism's resource system
	/// (loaded ResourceUpdate + unloaded BackgroundUpdate) and exposes Auto controls.
	/// </summary>
	public class PETTurbineFixer : PartModule, IKerbalismModule
	{
		private PartModule turbineModule;

		internal PartModule TurbineModule
		{
			get
			{
				if (turbineModule == null)
					turbineModule = PlanetsideExplorationTechnologies.FindTurbineModule(part);
				return turbineModule;
			}
		}

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			turbineModule = PlanetsideExplorationTechnologies.FindTurbineModule(part);
		}

		public string ResourceUpdate(Dictionary<string, double> availableResources, List<KeyValuePair<string, double>> resourceChangeRequest)
		{
			// Loaded production is injected at PET's UpdateResourceHandler Harmony hook,
			// after PET has calculated the wind efficiency used by its PAW.
			return PETTurbineResourceSim.BrokerId;
		}

		public string PlannerUpdate(List<KeyValuePair<string, double>> resourceChangeRequest, CelestialBody body, Dictionary<string, double> environment)
		{
			// Planner wind estimation is out of scope for this pass.
			return PETTurbineResourceSim.BrokerId;
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
			ProtoPartModuleSnapshot turbineProto = PETTurbineResourceSim.FindTurbineProto(part_snapshot);
			PartModule turbinePrefab = PETTurbineResourceSim.FindTurbinePrefab(proto_part);
			if (turbineProto == null || turbinePrefab == null)
				return PETTurbineResourceSim.BrokerId;

			double rate = PETTurbineResourceSim.GetBackgroundRate(v, turbineProto, turbinePrefab);
			PETTurbineResourceSim.AddRate(resourceChangeRequest, rate);
			return PETTurbineResourceSim.BrokerId;
		}

		internal bool IsDeployable => PETTurbineResourceSim.IsDeployable(TurbineModule);

		internal bool IsBroken => PETTurbineResourceSim.IsBroken(TurbineModule);

		internal string DeployState => PETTurbineResourceSim.GetDeployState(TurbineModule);

		internal bool IsActive => PlanetsideExplorationTechnologies.Get(TurbineModule, "isActive", false);

		internal void Toggle()
		{
			PartModule turbine = TurbineModule;
			if (turbine == null || IsBroken)
				return;

			if (IsDeployable)
			{
				string state = DeployState;
				if (state == "RETRACTED")
					PETTurbineResourceSim.Extend(turbine);
				else if (state == "EXTENDED")
					PETTurbineResourceSim.Retract(turbine);
				return;
			}

			PETTurbineResourceSim.SetActive(turbine, !IsActive);
		}

		internal void Ctrl(bool value)
		{
			PartModule turbine = TurbineModule;
			if (turbine == null || IsBroken)
				return;

			if (IsDeployable)
			{
				string state = DeployState;
				if (value && state == "RETRACTED")
					PETTurbineResourceSim.Extend(turbine);
				else if (!value && state == "EXTENDED")
					PETTurbineResourceSim.Retract(turbine);
				return;
			}

			PETTurbineResourceSim.SetActive(turbine, value);
		}
	}
}
