using System.Collections.Generic;
using System;
using KSP.Localization;
using CommNet;
using KSPAssets;

#if !KSP16 && !KSP15 && !KSP14
using Expansions.Serenity.DeployedScience.Runtime;

namespace KERBALISM
{
	internal class AntennaInfoSerenity: AntennaInfoCommNet
	{
		private DeployedScienceCluster cluster;

		public AntennaInfoSerenity(Vessel v, DeployedScienceCluster cluster, bool storm, bool transmitting)
			: base(v, cluster.IsPowered, storm, transmitting)
		{
			this.cluster = cluster;
			antennaInfo.ec = 0;
		}

		override protected List<KeyValuePair<ModuleDataTransmitter, ProtoPartSnapshot>> GetTransmittersUnloaded(Vessel v)
		{
			List<KeyValuePair<ModuleDataTransmitter, ProtoPartSnapshot>> transmitters;

			if (!Cache.HasVesselObjectsCache(v, "serenity_comm_bg"))
			{
				transmitters = new List<KeyValuePair<ModuleDataTransmitter, ProtoPartSnapshot>>();

				foreach(var antennaPart in cluster.AntennaParts)
				{
					ProtoPartSnapshot protoPart;
					if(FlightGlobals.FindUnloadedPart(antennaPart.PartId, out protoPart))
					{
						// find proto transmitters
						foreach (ProtoPartSnapshot p in protoPart.pVesselRef.protoPartSnapshots)
						{
							// get part prefab (required for module properties)
							Part part_prefab = PartLoader.getPartInfoByName(p.partName).partPrefab;

							foreach (ModuleDataTransmitter t in part_prefab.FindModulesImplementing<ModuleDataTransmitter>())
							{
								transmitters.Add(new KeyValuePair<ModuleDataTransmitter, ProtoPartSnapshot>(t, p));
							}
						}
					}
				}

				Cache.SetVesselObjectsCache(v, "serenity_comm_bg", transmitters);
			}
			else
			{
				// cache transmitters
				transmitters = Cache.VesselObjectsCache<List<KeyValuePair<ModuleDataTransmitter, ProtoPartSnapshot>>>(v, "serenity_comm_bg");
			}

			return transmitters;
		}

		override protected List<ModuleDataTransmitter> GetTransmittersLoaded(Vessel v)
		{
			List<ModuleDataTransmitter> transmitters;

			if (!Cache.HasVesselObjectsCache(v, "serenity_comm"))
			{
				// find transmitters
				transmitters = new List<ModuleDataTransmitter>();

				foreach (var antennaPart in cluster.AntennaParts)
				{
					Part part;
					if (FlightGlobals.FindLoadedPart(antennaPart.PartId, out part))
					{
						foreach (var t in v.FindPartModulesImplementing<ModuleDataTransmitter>())
							transmitters.Add(t);
					}
				}

				Cache.SetVesselObjectsCache(v, "serenity_comm", transmitters);
			}
			else
				transmitters = Cache.VesselObjectsCache<List<ModuleDataTransmitter>>(v, "serenity_comm");

			return transmitters;
		}

		override public AntennaInfo AntennaInfo()
		{
			AntennaInfo result = base.AntennaInfo();
			result.ec = 0;
			return result;
		}
	}
}
#endif
