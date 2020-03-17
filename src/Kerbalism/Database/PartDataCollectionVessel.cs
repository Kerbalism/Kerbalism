using System.Collections;
using System.Collections.Generic;

namespace KERBALISM
{
	public class PartDataCollectionVessel : IEnumerable<PartData>
	{
		private static Dictionary<int, ConfigNode> moduleDataNodes = new Dictionary<int, ConfigNode>();
		private Dictionary<uint, PartData> partDictionary = new Dictionary<uint, PartData>();
		private List<PartData> partList = new List<PartData>();


		public PartDataCollectionVessel(PartDataCollectionShip shipPartData)
		{
			Lib.LogDebug($"Transferring PartData from ship to vessel for launch");

			foreach (PartData partData in shipPartData)
			{
				partData.flightId = partData.LoadedPart.flightID;
				Add(partData);

				foreach (ModuleData moduleData in partData.modules)
				{
					ModuleData.AssignNewFlightId(moduleData);
				}
			}
		}

		public PartDataCollectionVessel(Vessel vessel)
		{
			Lib.LogDebug($"Creating partdatas for new loaded vessel {vessel.vesselName}");

			foreach (Part part in vessel.parts)
			{
				PartData partData = Add(part);

				for (int i = 0; i < part.Modules.Count; i++)
				{
					if (part.Modules[i] is KsmPartModule ksmPM)
					{
						ModuleData.New(ksmPM, partData, true);
					}
				}
			}
				
		}
		public PartDataCollectionVessel(ProtoVessel protoVessel, ConfigNode vesselDataNode)
		{
			Lib.LogDebug($"Loading partdatas for existing vessel {protoVessel.vesselName}");

			moduleDataNodes.Clear();

			ConfigNode modulesNode = vesselDataNode?.GetNode(VesselDataBase.NODENAME_MODULE);

			if (modulesNode != null)
			{
				foreach (ConfigNode moduleNode in modulesNode.GetNodes())
				{
					int flightId = Lib.ConfigValue(moduleNode, ModuleData.VALUENAME_FLIGHTID, 0);
					if (flightId != 0)
						moduleDataNodes.Add(flightId, moduleNode);
				}
			}

			foreach (ProtoPartSnapshot protopart in protoVessel.protoPartSnapshots)
			{
				PartData partData = Add(protopart);

				foreach (ProtoPartModuleSnapshot protoModule in protopart.modules)
				{
					if (ModuleData.IsKsmPartModule(protoModule))
					{
						int flightId = Lib.Proto.GetInt(protoModule, KsmPartModule.VALUENAME_FLIGHTID, 0);

						if (modulesNode != null && flightId != 0 && moduleDataNodes.TryGetValue(flightId, out ConfigNode moduleNode))
						{
							ModuleData.NewFromNode(protoModule, partData, moduleNode, flightId);
						}
						else
						{
							ModuleData.New(protoModule, partData);
						}
					}
				}
			}
		}

		// List / dictionary alike implementation
		public IEnumerator<PartData> GetEnumerator() => partList.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => partList.GetEnumerator();
		public int Count => partList.Count;
		public PartData this[int index] => partList[index];
		public PartData this[uint flightId] => partDictionary[flightId];
		public bool Contains(PartData data) => partDictionary.ContainsKey(data.flightId);
		public bool Contains(uint flightId) => partDictionary.ContainsKey(flightId);

		public bool TryGet(uint flightId, out PartData pd) => partDictionary.TryGetValue(flightId, out pd);

		public void Add(PartData partData)
		{
			if (partDictionary.ContainsKey(partData.flightId))
			{
				Lib.LogDebugStack($"PartData with key {partData.flightId} exists already ({partData.Title})", Lib.LogLevel.Warning);
				return;
			}

			partDictionary.Add(partData.flightId, partData);
			partList.Add(partData);
		}

		public PartData Add(Part part)
		{
			uint id = Lib.IsEditor() ? part.persistentId : part.flightID;

			if (partDictionary.ContainsKey(id))
			{
				Lib.LogDebugStack($"PartData with key {id} exists already ({part.partInfo.title})", Lib.LogLevel.Warning);
				return null;
			}

			PartData pd = new PartData(part);
			partDictionary.Add(id, pd);
			partList.Add(pd);
			return pd;
		}

		public PartData Add(ProtoPartSnapshot protoPart)
		{
			if (partDictionary.ContainsKey(protoPart.flightID))
			{
				Lib.LogDebugStack($"PartData with key {protoPart.flightID} exists already ({protoPart.partInfo.title})", Lib.LogLevel.Warning);
				return null;
			}

			PartData pd = new PartData(protoPart);
			partDictionary.Add(protoPart.flightID, pd);
			partList.Add(pd);
			return pd;
		}

		public void Remove(PartData partdata)
		{
			if (partDictionary.TryGetValue(partdata.flightId, out PartData partData))
			{
				partDictionary.Remove(partdata.flightId);
				partList.Remove(partData);
			}
		}

		public void Remove(uint flightID)
		{
			if (partDictionary.TryGetValue(flightID, out PartData partData))
			{
				partDictionary.Remove(flightID);
				partList.Remove(partData);
			}
		}

		public void Clear()
		{
			partDictionary.Clear();
			partList.Clear();
		}

		public void TransferFrom(PartDataCollectionVessel other)
		{
			foreach (PartData partData in other)
			{
				Add(partData);
			}

			other.Clear();
		}
	}
}
