using System.Collections.Generic;
using KSP.Localization;

namespace KERBALISM
{
	/// <summary>
	/// Routes ModuleSystemHeatConverter resource IO through Kerbalism while keeping native SystemHeat behaviour.
	/// </summary>
	public class SystemHeatConverterKerbalismUpdater : PartModule, IKerbalismModule
	{
		public static string brokerName = "SHNativeConverter";
		public static string brokerTitle = Localizer.Format("#KERBALISM_Brokers_Converter");

		[KSPField(isPersistant = true)]
		public string converterModuleID = "converter";

		protected PartModule converterModule;

		internal bool OwnsConverter(PartModule converter)
		{
			return converter != null
				&& converterModuleID == SystemHeat.GetModuleId(converter)
				&& converter.part == part;
		}

		internal PartModule FindConverterModule()
		{
			if (converterModule != null)
				return converterModule;

			converterModule = FindConverterPrefab(part, converterModuleID);
			return converterModule;
		}

		public string ResourceUpdate(Dictionary<string, double> availableResources, List<KeyValuePair<string, double>> resourceChangeRequest)
		{
			return SHNativeConverterResourceSim.AddLoadedConverterRates(
				FindConverterModule(),
				brokerTitle,
				resourceChangeRequest);
		}

		public string PlannerUpdate(List<KeyValuePair<string, double>> resourceChangeRequest, CelestialBody body, Dictionary<string, double> environment)
		{
			PartModule converter = FindConverterModule();
			if (converter != null && SystemHeat.IsActivated(converter))
				return SHNativeConverterResourceSim.AddPlannerConverterRates(converter, resourceChangeRequest, brokerTitle);
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
			string moduleId = Lib.Proto.GetString(module_snapshot, "converterModuleID");
			ProtoPartModuleSnapshot converterSnapshot = FindConverterSnapshot(part_snapshot, moduleId);
			PartModule converterPrefab = FindConverterPrefab(proto_part, moduleId);

			if (converterSnapshot != null && converterPrefab != null)
			{
				SHNativeConverterResourceSim.BackgroundUpdateConverter(
					v,
					converterSnapshot,
					converterPrefab,
					brokerName,
					brokerTitle,
					elapsed_s);
			}

			SystemHeatBackgroundThermal.TryRun(v, elapsed_s);
			return brokerTitle;
		}

		private static PartModule FindConverterPrefab(Part part, string moduleId)
		{
			return SystemHeat.FindConverter(part, moduleId);
		}

		private static ProtoPartModuleSnapshot FindConverterSnapshot(ProtoPartSnapshot partSnapshot, string moduleId)
		{
			ProtoPartModuleSnapshot firstConverter = null;
			for (int i = 0; i < partSnapshot.modules.Count; i++)
			{
				ProtoPartModuleSnapshot module = partSnapshot.modules[i];
				if (module.moduleName != "ModuleSystemHeatConverter")
					continue;

				if (firstConverter == null)
					firstConverter = module;

				if (Lib.Proto.GetString(module, "moduleID") == moduleId)
					return module;
			}

			return firstConverter;
		}
	}
}
