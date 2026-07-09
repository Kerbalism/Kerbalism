using System.Collections.Generic;
using KSP.Localization;

namespace KERBALISM
{
	/// <summary>
	/// Routes ModuleSystemHeatHarvester resource IO through Kerbalism while keeping native SystemHeat behaviour.
	/// </summary>
	public class SystemHeatHarvesterKerbalismUpdater : PartModule, IKerbalismModule
	{
		public static string brokerName = "SHNativeHarvester";
		public static string brokerTitle = Localizer.Format("#KERBALISM_Brokers_Harvester");

		[KSPField(isPersistant = true)]
		public string harvesterModuleID = "harvester";

		protected PartModule harvesterModule;

		internal bool OwnsHarvester(PartModule harvester)
		{
			return harvester != null
				&& harvesterModuleID == SystemHeat.GetModuleId(harvester)
				&& harvester.part == part;
		}

		internal PartModule FindHarvesterModule()
		{
			if (harvesterModule != null)
				return harvesterModule;

			harvesterModule = FindHarvesterPrefab(part, harvesterModuleID);
			return harvesterModule;
		}

		public string ResourceUpdate(Dictionary<string, double> availableResources, List<KeyValuePair<string, double>> resourceChangeRequest)
		{
			return SHNativeConverterResourceSim.AddLoadedHarvesterRates(
				FindHarvesterModule(),
				brokerTitle,
				resourceChangeRequest);
		}

		public string PlannerUpdate(List<KeyValuePair<string, double>> resourceChangeRequest, CelestialBody body, Dictionary<string, double> environment)
		{
			PartModule harvester = FindHarvesterModule();
			if (harvester != null && SystemHeat.IsActivated(harvester))
				return SHNativeConverterResourceSim.AddPlannerHarvesterRates(harvester, resourceChangeRequest, brokerTitle);
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
			string moduleId = Lib.Proto.GetString(module_snapshot, "harvesterModuleID");
			ProtoPartModuleSnapshot harvesterSnapshot = FindHarvesterSnapshot(part_snapshot, moduleId);
			PartModule harvesterPrefab = FindHarvesterPrefab(proto_part, moduleId);

			if (harvesterSnapshot != null && harvesterPrefab != null)
			{
				SHNativeConverterResourceSim.BackgroundUpdateHarvester(
					v,
					harvesterSnapshot,
					harvesterPrefab,
					brokerName,
					brokerTitle,
					elapsed_s);
			}

			SystemHeatBackgroundThermal.TryRun(v, elapsed_s);
			return brokerTitle;
		}

		private static PartModule FindHarvesterPrefab(Part part, string moduleId)
		{
			return SystemHeat.FindHarvester(part, moduleId);
		}

		private static ProtoPartModuleSnapshot FindHarvesterSnapshot(ProtoPartSnapshot partSnapshot, string moduleId)
		{
			ProtoPartModuleSnapshot firstHarvester = null;
			for (int i = 0; i < partSnapshot.modules.Count; i++)
			{
				ProtoPartModuleSnapshot module = partSnapshot.modules[i];
				if (module.moduleName != "ModuleSystemHeatHarvester")
					continue;

				if (firstHarvester == null)
					firstHarvester = module;

				if (Lib.Proto.GetString(module, "moduleID") == moduleId)
					return module;
			}

			return firstHarvester;
		}
	}
}
