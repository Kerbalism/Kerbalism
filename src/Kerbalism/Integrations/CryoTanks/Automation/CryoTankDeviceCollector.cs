using System.Collections;
using System.Collections.Generic;

namespace KERBALISM
{
	internal static class CryoTankDeviceCollector
	{
		internal static void RemoveDevices(List<Device> devices)
		{
			for (int i = devices.Count - 1; i >= 0; i--)
			{
				Device device = devices[i];
				if (device is CryoTankCoolingDevice || device is ProtoCryoTankCoolingDevice)
					devices.RemoveAt(i);
			}
		}

		internal static void CollectLoaded(Vessel v, List<Device> devices)
		{
			if (!CryoTanks.Installed || !CryoSettings.Enabled)
				return;

			foreach (Part part in v.parts)
			{
				if (part.FindModuleImplementing<CryoTankKerbalismUpdater>() == null)
					continue;

				PartModule cryo = CryoTanks.FindCryoTankModule(part);
				if (cryo == null || !IsCoolable(cryo, part))
					continue;

				devices.Add(new CryoTankCoolingDevice(cryo));
			}
		}

		internal static void CollectProto(Vessel v, List<Device> devices)
		{
			if (!CryoTanks.Installed || !CryoSettings.Enabled || v?.protoVessel == null)
				return;

			var prefabData = new Dictionary<string, Lib.Module_prefab_data>();

			foreach (ProtoPartSnapshot partSnapshot in v.protoVessel.protoPartSnapshots)
			{
				if (IntegrationUtils.TryFindPartModuleSnapshot(partSnapshot, "CryoTankKerbalismUpdater") == null)
					continue;

				Part partPrefab = PartLoader.getPartInfoByName(partSnapshot.partName).partPrefab;
				prefabData.Clear();

				foreach (ProtoPartModuleSnapshot moduleSnapshot in partSnapshot.modules)
				{
					if (moduleSnapshot.moduleName != "ModuleCryoTank")
						continue;

					PartModule modulePrefab = Lib.ModulePrefab(partPrefab.Modules, moduleSnapshot.moduleName, prefabData);
					if (modulePrefab == null || !IsCoolable(modulePrefab, partSnapshot))
						continue;

					devices.Add(new ProtoCryoTankCoolingDevice(modulePrefab, partSnapshot, moduleSnapshot));
				}
			}
		}

		internal static bool IsCoolable(PartModule cryoModule, Part part)
		{
			if (cryoModule == null || part == null)
				return false;
			if (!CryoTanks.Get(cryoModule, "CoolingAllowed", true))
				return false;

			if (CryoTanks.GetCoolingCost(cryoModule) > 0f)
				return HasAnyBoiloffResource(cryoModule, part);

			IList fuels = CryoTankAccess.GetFuels(cryoModule);
			if (fuels == null)
				return false;

			foreach (object fuel in fuels)
			{
				string fuelName = CryoTankAccess.GetFuelName(fuel);
				if (string.IsNullOrEmpty(fuelName))
					continue;
				if (part.Resources.Get(fuelName) == null)
					continue;
				if (CryoTankAccess.GetFuelCoolingCost(fuel) > 0f)
					return true;
			}

			return false;
		}

		internal static bool IsCoolable(PartModule cryoPrefab, ProtoPartSnapshot part)
		{
			if (cryoPrefab == null || part == null)
				return false;
			if (!CryoTanks.Get(cryoPrefab, "CoolingAllowed", true))
				return false;

			if (CryoTanks.GetCoolingCost(cryoPrefab) > 0f)
				return HasAnyBoiloffResource(cryoPrefab, part);

			IList fuels = CryoTankAccess.GetFuels(cryoPrefab);
			if (fuels == null)
				return false;

			foreach (object fuel in fuels)
			{
				string fuelName = CryoTankAccess.GetFuelName(fuel);
				if (string.IsNullOrEmpty(fuelName))
					continue;
				if (CryoUtils.FindPartResource(part, fuelName) == null)
					continue;
				if (CryoTankAccess.GetFuelCoolingCost(fuel) > 0f)
					return true;
			}

			return false;
		}

		static bool HasAnyBoiloffResource(PartModule cryoModule, Part part)
		{
			IList fuels = CryoTankAccess.GetFuels(cryoModule);
			if (fuels == null)
				return false;

			foreach (object fuel in fuels)
			{
				string fuelName = CryoTankAccess.GetFuelName(fuel);
				if (!string.IsNullOrEmpty(fuelName) && part.Resources.Get(fuelName) != null)
					return true;
			}

			return false;
		}

		static bool HasAnyBoiloffResource(PartModule cryoPrefab, ProtoPartSnapshot part)
		{
			IList fuels = CryoTankAccess.GetFuels(cryoPrefab);
			if (fuels == null)
				return false;

			foreach (object fuel in fuels)
			{
				string fuelName = CryoTankAccess.GetFuelName(fuel);
				if (!string.IsNullOrEmpty(fuelName) && CryoUtils.FindPartResource(part, fuelName) != null)
					return true;
			}

			return false;
		}
	}
}
