using System.Collections.Generic;
using KSP.Localization;

namespace KERBALISM
{
	internal static class FissionDeviceCollector
	{
		internal static void RemoveDevices(List<Device> devices)
		{
			for (int i = devices.Count - 1; i >= 0; i--)
			{
				Device device = devices[i];
				if (device is FissionReactorDevice || device is ProtoFissionReactorDevice)
					devices.RemoveAt(i);
			}
		}

		internal static void CollectLoaded(Vessel v, List<Device> devices)
		{
			foreach (Part part in v.parts)
			{
				SystemHeatFissionEngineKerbalismUpdater engineUpdater = part.FindModuleImplementing<SystemHeatFissionEngineKerbalismUpdater>();
				if (engineUpdater != null)
				{
					PartModule engine = engineUpdater.FindEngineModule(part, engineUpdater.engineModuleID);
					if (engine != null)
						AddLoadedDevice(devices, engine, true);
					continue;
				}

				SystemHeatFissionReactorKerbalismUpdater reactorUpdater = part.FindModuleImplementing<SystemHeatFissionReactorKerbalismUpdater>();
				if (reactorUpdater == null)
					continue;

				PartModule reactor = reactorUpdater.FindReactorModule(part, reactorUpdater.reactorModuleID);
				if (reactor != null)
					AddLoadedDevice(devices, reactor, false);
			}
		}

		internal static void CollectProto(Vessel v, List<Device> devices)
		{
			var prefabData = new Dictionary<string, Lib.Module_prefab_data>();

			foreach (ProtoPartSnapshot partSnapshot in v.protoVessel.protoPartSnapshots)
			{
				ProtoPartModuleSnapshot engineUpdater = IntegrationUtils.TryFindPartModuleSnapshot(partSnapshot, "SystemHeatFissionEngineKerbalismUpdater");
				if (engineUpdater != null)
				{
					string moduleId = Lib.Proto.GetString(engineUpdater, "engineModuleID");
					TryAddProtoDevice(devices, partSnapshot, prefabData, "ModuleSystemHeatFissionEngine", "moduleID", moduleId, true);
					continue;
				}

				ProtoPartModuleSnapshot reactorUpdater = IntegrationUtils.TryFindPartModuleSnapshot(partSnapshot, "SystemHeatFissionReactorKerbalismUpdater");
				if (reactorUpdater == null)
					continue;

				string reactorModuleId = Lib.Proto.GetString(reactorUpdater, "reactorModuleID");
				TryAddProtoDevice(devices, partSnapshot, prefabData, "ModuleSystemHeatFissionReactor", "moduleID", reactorModuleId, false);
			}
		}

		private static void AddLoadedDevice(List<Device> devices, PartModule reactor, bool isEngine)
		{
			string moduleId = SystemHeat.GetModuleId(reactor);
			string deviceName = isEngine
				? "SH fission engine reactor " + moduleId
				: "SH fission reactor " + moduleId;
			string displayName = isEngine
				? Localizer.Format("#KERBALISM_Device_FissionEngine")
				: Localizer.Format("#KERBALISM_Device_FissionReactor");

			devices.Add(new FissionReactorDevice(reactor, deviceName, displayName));
		}

		private static void TryAddProtoDevice(
			List<Device> devices,
			ProtoPartSnapshot partSnapshot,
			Dictionary<string, Lib.Module_prefab_data> prefabData,
			string moduleName,
			string idFieldName,
			string moduleId,
			bool isEngine)
		{
			ProtoPartModuleSnapshot moduleSnapshot = FindModuleSnapshot(partSnapshot, moduleName, idFieldName, moduleId);
			if (moduleSnapshot == null)
				return;

			Part partPrefab = PartLoader.getPartInfoByName(partSnapshot.partName).partPrefab;
			prefabData.Clear();
			PartModule modulePrefab = Lib.ModulePrefab(partPrefab.Modules, moduleSnapshot.moduleName, prefabData);
			if (modulePrefab == null)
				return;

			string resolvedId = Lib.Proto.GetString(moduleSnapshot, idFieldName);
			if (string.IsNullOrEmpty(resolvedId))
				resolvedId = moduleId;

			string deviceName = isEngine
				? "SH fission engine reactor " + resolvedId
				: "SH fission reactor " + resolvedId;
			string displayName = isEngine
				? Localizer.Format("#KERBALISM_Device_FissionEngine")
				: Localizer.Format("#KERBALISM_Device_FissionReactor");

			devices.Add(new ProtoFissionReactorDevice(modulePrefab, partSnapshot, moduleSnapshot, deviceName, displayName));
		}

		private static ProtoPartModuleSnapshot FindModuleSnapshot(
			ProtoPartSnapshot partSnapshot,
			string moduleName,
			string idFieldName,
			string moduleId)
		{
			ProtoPartModuleSnapshot fallback = null;
			for (int i = 0; i < partSnapshot.modules.Count; i++)
			{
				ProtoPartModuleSnapshot moduleSnapshot = partSnapshot.modules[i];
				if (moduleSnapshot.moduleName != moduleName)
					continue;

				if (fallback == null)
					fallback = moduleSnapshot;

				if (!string.IsNullOrEmpty(moduleId) && Lib.Proto.GetString(moduleSnapshot, idFieldName) == moduleId)
					return moduleSnapshot;
			}

			return fallback;
		}
	}
}
