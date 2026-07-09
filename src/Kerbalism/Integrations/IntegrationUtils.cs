using UnityEngine;

namespace KERBALISM
{
	internal static class IntegrationUtils
	{
		public static void Log(string message)
		{
			Debug.Log("[Kerbalism] [Integrations] " + message);
		}

		public static void LogError(string message)
		{
			Debug.LogError("[Kerbalism] [Integrations] " + message);
		}

		public static ProtoPartModuleSnapshot TryFindPartModuleSnapshot(ProtoPartSnapshot partSnapshot, string moduleName)
		{
			if (partSnapshot == null || string.IsNullOrEmpty(moduleName))
				return null;

			for (int i = 0; i < partSnapshot.modules.Count; i++)
			{
				if (partSnapshot.modules[i].moduleName == moduleName)
					return partSnapshot.modules[i];
			}

			return null;
		}

		public static ProtoPartModuleSnapshot FindPartModuleSnapshot(ProtoPartSnapshot partSnapshot, string moduleName)
		{
			ProtoPartModuleSnapshot snapshot = TryFindPartModuleSnapshot(partSnapshot, moduleName);
			if (snapshot == null && partSnapshot != null)
				LogError("Part [" + partSnapshot.partInfo.title + "] has no " + moduleName + " snapshot.");
			return snapshot;
		}

		public static ProtoPartResourceSnapshot TryFindPartResource(ProtoPartSnapshot partSnapshot, string resourceName)
		{
			if (partSnapshot == null || string.IsNullOrEmpty(resourceName))
				return null;

			for (int i = 0; i < partSnapshot.resources.Count; i++)
			{
				if (partSnapshot.resources[i].resourceName == resourceName)
					return partSnapshot.resources[i];
			}

			return null;
		}

		public static ConfigNode GetModuleConfigNode(Part part, string moduleName)
		{
			if (part?.partInfo == null || string.IsNullOrEmpty(moduleName))
				return null;

			AvailablePart availablePart = PartLoader.getPartInfoByName(part.partInfo.name);
			ConfigNode partConfig = availablePart?.partConfig;
			if (partConfig == null)
				return null;

			foreach (ConfigNode moduleNode in partConfig.GetNodes("MODULE"))
			{
				if (moduleNode.GetValue("name") == moduleName)
					return moduleNode;
			}

			return null;
		}

		public static PartModule FindModule(Part part, string moduleName)
		{
			if (part == null || string.IsNullOrEmpty(moduleName))
				return null;

			for (int i = 0; i < part.Modules.Count; i++)
			{
				PartModule module = part.Modules[i];
				if (module != null && module.moduleName == moduleName)
					return module;
			}

			return null;
		}

		public static double SampleResourceAbundance(Vessel v, ModuleResourceHarvester harvester)
		{
			if (v == null || harvester == null || ResourceMap.Instance == null)
				return 0.0;

			AbundanceRequest request = new AbundanceRequest
			{
				ResourceType = (HarvestTypes)harvester.HarvesterType,
				ResourceName = harvester.ResourceName,
				BodyId = v.mainBody.flightGlobalsIndex,
				Latitude = v.latitude,
				Longitude = v.longitude,
				Altitude = v.altitude,
				CheckForLock = false
			};
			return ResourceMap.Instance.GetAbundance(request);
		}
	}
}
