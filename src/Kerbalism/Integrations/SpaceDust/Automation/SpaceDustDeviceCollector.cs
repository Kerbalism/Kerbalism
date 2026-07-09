using System.Collections.Generic;
using KSP.Localization;

namespace KERBALISM
{
	internal static class SpaceDustDeviceCollector
	{
		internal static void RemoveDevices(List<Device> devices)
		{
			for (int i = devices.Count - 1; i >= 0; i--)
			{
				Device device = devices[i];
				if (device is SpaceDustHarvesterDevice || device is ProtoSpaceDustHarvesterDevice)
					devices.RemoveAt(i);
			}
		}

		internal static void CollectLoaded(Vessel v, List<Device> devices)
		{
			foreach (Part part in v.parts)
			{
				SpaceDustHarvesterKerbalismUpdater updater = part.FindModuleImplementing<SpaceDustHarvesterKerbalismUpdater>();
				if (updater == null)
					continue;

				PartModule harvester = FindHarvesterModule(part, updater.harvesterModuleID);
				if (harvester != null)
					AddHarvesterDevice(devices, harvester, updater.harvesterModuleID);
			}
		}

		internal static void CollectProto(Vessel v, List<Device> devices)
		{
			var prefabData = new Dictionary<string, Lib.Module_prefab_data>();

			foreach (ProtoPartSnapshot partSnapshot in v.protoVessel.protoPartSnapshots)
			{
				ProtoPartModuleSnapshot updater = IntegrationUtils.TryFindPartModuleSnapshot(partSnapshot, "SpaceDustHarvesterKerbalismUpdater");
				if (updater == null)
					continue;

				string moduleId = Lib.Proto.GetString(updater, "harvesterModuleID");
				TryAddProtoDevice(devices, partSnapshot, prefabData, moduleId);
			}
		}

		private static PartModule FindHarvesterModule(Part part, string moduleId)
		{
			PartModule fallback = null;
			for (int i = 0; i < part.Modules.Count; i++)
			{
				PartModule module = part.Modules[i];
				if (!SpaceDust.IsHarvester(module) && module.moduleName != "ModuleSpaceDustHarvester")
					continue;

				if (fallback == null)
					fallback = module;

				string nativeId = SpaceDust.Get(module, "ModuleID", "");
				if (string.IsNullOrEmpty(moduleId) || nativeId == moduleId)
					return module;
			}

			return fallback;
		}

		private static void AddHarvesterDevice(List<Device> devices, PartModule harvester, string moduleId)
		{
			string resolvedId = SpaceDust.Get(harvester, "ModuleID", "");
			if (string.IsNullOrEmpty(resolvedId))
				resolvedId = moduleId;

			string deviceName = Lib.BuildString("spacedust harvester ", resolvedId);
			string displayName = harvester is IModuleInfo info
				? info.GetModuleTitle()
				: Localizer.Format("#LOC_SpaceDust_ModuleSpaceDustHarvester_DisplayName");
			devices.Add(new SpaceDustHarvesterDevice(harvester, deviceName, displayName));
		}

		private static void TryAddProtoDevice(
			List<Device> devices,
			ProtoPartSnapshot partSnapshot,
			Dictionary<string, Lib.Module_prefab_data> prefabData,
			string moduleId)
		{
			ProtoPartModuleSnapshot moduleSnapshot = FindHarvesterSnapshot(partSnapshot, moduleId);
			if (moduleSnapshot == null)
				return;

			Part partPrefab = PartLoader.getPartInfoByName(partSnapshot.partName).partPrefab;
			prefabData.Clear();
			PartModule modulePrefab = Lib.ModulePrefab(partPrefab.Modules, moduleSnapshot.moduleName, prefabData);
			if (modulePrefab == null)
				return;

			string resolvedId = Lib.Proto.GetString(moduleSnapshot, "ModuleID");
			if (string.IsNullOrEmpty(resolvedId))
				resolvedId = moduleId;

			string deviceName = Lib.BuildString("spacedust harvester ", resolvedId);
			string displayName = modulePrefab is IModuleInfo info
				? info.GetModuleTitle()
				: Localizer.Format("#LOC_SpaceDust_ModuleSpaceDustHarvester_DisplayName");
			devices.Add(new ProtoSpaceDustHarvesterDevice(modulePrefab, partSnapshot, moduleSnapshot, deviceName, displayName));
		}

		private static ProtoPartModuleSnapshot FindHarvesterSnapshot(ProtoPartSnapshot partSnapshot, string moduleId)
		{
			ProtoPartModuleSnapshot fallback = null;
			for (int i = 0; i < partSnapshot.modules.Count; i++)
			{
				ProtoPartModuleSnapshot moduleSnapshot = partSnapshot.modules[i];
				if (moduleSnapshot.moduleName != "ModuleSpaceDustHarvester")
					continue;

				if (fallback == null)
					fallback = moduleSnapshot;

				if (!string.IsNullOrEmpty(moduleId) && Lib.Proto.GetString(moduleSnapshot, "ModuleID") == moduleId)
					return moduleSnapshot;
			}

			return fallback;
		}
	}
}
