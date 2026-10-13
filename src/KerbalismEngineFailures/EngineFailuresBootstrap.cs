using System.Collections.Generic;
using UnityEngine;

namespace KERBALISM.EngineFailures
{
	[KSPAddon(KSPAddon.Startup.MainMenu, true)]
	public sealed class EngineFailuresBootstrap : MonoBehaviour
	{
		void Awake()
		{
			ReliabilityInfo.ExtraProviders.Add(Collect);
		}

		static void Collect(Vessel vessel, List<ReliabilityInfo> result)
		{
			if (vessel == null) return;

			if (vessel.loaded)
			{
				foreach (EngineFailures module in PartModuleCache.GetModules<EngineFailures>(vessel))
				{
					if (!module.isEnabled) continue;
					module.EnsureRatings();
					result.Add(BuildInfo(module));
				}
				return;
			}

			var prefabData = new Dictionary<string, Lib.Module_prefab_data>();
			foreach (ProtoPartSnapshot partSnapshot in vessel.protoVessel.protoPartSnapshots)
			{
				AvailablePart partInfo = PartLoader.getPartInfoByName(partSnapshot.partName);
				if (partInfo == null || partInfo.partPrefab == null) continue;

				prefabData.Clear();
				foreach (ProtoPartModuleSnapshot moduleSnapshot in partSnapshot.modules)
				{
					if (moduleSnapshot.moduleName != nameof(EngineFailures)) continue;

					EngineFailures modulePrefab = Lib.ModulePrefab(
						partInfo.partPrefab.Modules,
						moduleSnapshot.moduleName,
						prefabData) as EngineFailures;
					if (modulePrefab == null) continue;
					if (!Lib.Proto.GetBool(moduleSnapshot, "isEnabled")) continue;

					modulePrefab.EnsureRatings();
					result.Add(BuildInfo(partSnapshot, moduleSnapshot, modulePrefab));
				}
			}
		}

		static ReliabilityInfo BuildInfo(EngineFailures module)
		{
			string title = Lib.BuildString(module.part.partInfo.title, Lib.Color(" " + Reliability.LocalizeTitle(module.title), Lib.Kolor.LightGrey));
			double rel_duration = 0.0;
			double rel_ignitions = 0.0;

			if (module.rated_operation_duration > 0)
			{
				rel_duration = module.operation_duration / Reliability.EffectiveDuration(module.quality, module.rated_operation_duration);
				rel_duration = Lib.Clamp(rel_duration, 0, 1);
			}

			if (module.rated_ignitions > 0)
			{
				rel_ignitions = (double)module.ignitions / Reliability.EffectiveIgnitions(module.quality, module.rated_ignitions);
				rel_ignitions = Lib.Clamp(rel_ignitions, 0, 1);
			}

			return new ReliabilityInfo(
				title,
				module.redundancy,
				module.broken,
				module.critical,
				module.part.flightID,
				module.needMaintenance,
				rel_duration,
				rel_ignitions,
				0.0);
		}

		static ReliabilityInfo BuildInfo(
			ProtoPartSnapshot partSnapshot,
			ProtoPartModuleSnapshot moduleSnapshot,
			EngineFailures modulePrefab)
		{
			bool quality = Lib.Proto.GetBool(moduleSnapshot, nameof(EngineFailures.quality), false);
			double relDuration = 0.0;
			double relIgnitions = 0.0;

			if (modulePrefab.rated_operation_duration > 0)
			{
				double operationDuration = Lib.Proto.GetDouble(
					moduleSnapshot,
					nameof(EngineFailures.operation_duration),
					0.0);
				relDuration = operationDuration
					/ Reliability.EffectiveDuration(quality, modulePrefab.rated_operation_duration);
				relDuration = Lib.Clamp(relDuration, 0, 1);
			}

			if (modulePrefab.rated_ignitions > 0)
			{
				int ignitions = Lib.Proto.GetInt(
					moduleSnapshot,
					nameof(EngineFailures.ignitions),
					0);
				relIgnitions = (double)ignitions
					/ Reliability.EffectiveIgnitions(quality, modulePrefab.rated_ignitions);
				relIgnitions = Lib.Clamp(relIgnitions, 0, 1);
			}

			string title = Lib.BuildString(
				partSnapshot.partInfo.title,
				Lib.Color(" " + Reliability.LocalizeTitle(modulePrefab.title), Lib.Kolor.LightGrey));

			return new ReliabilityInfo(
				title,
				modulePrefab.redundancy,
				Lib.Proto.GetBool(moduleSnapshot, nameof(EngineFailures.broken), false),
				Lib.Proto.GetBool(moduleSnapshot, nameof(EngineFailures.critical), false),
				0,
				Lib.Proto.GetBool(moduleSnapshot, nameof(EngineFailures.needMaintenance), false),
				relDuration,
				relIgnitions,
				0.0);
		}
	}
}
