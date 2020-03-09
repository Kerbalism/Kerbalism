using Expansions.Serenity.DeployedScience.Runtime;

namespace KERBALISM
{

	public static class Serenity
	{
		/// <summary>
		/// Use this whenever possible. Note this cannot be used during deployment.
		/// </summary>
		internal static DeployedScienceCluster GetScienceCluster(Vessel v)
		{
			if (!Kerbalism.SerenityEnabled)
				return null;

			foreach (var cluster in DeployedScience.Instance.DeployedScienceClusters.Values)
			{
				if (v.loaded)
				{
					Part part = null;
					if (FlightGlobals.FindLoadedPart(cluster.ControlModulePartId, out part))
					{
						if (part.vessel == v)
							return cluster;
					}
				}
				else
				{
					ProtoPartSnapshot snapshot;
					if (FlightGlobals.FindUnloadedPart(cluster.ControlModulePartId, out snapshot))
					{
						if (snapshot.pVesselRef == v.protoVessel)
							return cluster;
					}
				}
			}

			return null;
		}

		internal static ModuleGroundExpControl GetModuleGroundExpControl(Vessel v)
		{
			if (!Kerbalism.SerenityEnabled)
				return null;

			if (v.loaded)
			{
				if (v.parts.Count > 1)
					return null; // deployables are 1-part vessels
				foreach (Part part in v.parts)
				{
					var result = part.FindModuleImplementing<ModuleGroundExpControl>();
					if (result != null)
						return result;
				}
			}
			else
			{
				foreach (ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
				{
					Part part_prefab = PartLoader.getPartInfoByName(p.partName).partPrefab;
					var result = part_prefab.FindModuleImplementing<ModuleGroundExpControl>();
					if (result != null)
						return result;
				}
			}
			return null;
		}
	}
}
