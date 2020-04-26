using System.Collections.Generic;
using UnityEngine;

namespace KERBALISM
{
	public class PartDataCollectionShip : PartDataCollectionBase
	{
		private readonly List<PartData> allParts = new List<PartData>();
		private readonly List<PartData> shipPartsCache = new List<PartData>();
		private readonly Dictionary<int, PartData> partDictionary = new Dictionary<int, PartData>();

		public override PartData this[Part part] => partDictionary[part.GetInstanceID()];
		public override bool Contains(PartData data) => partDictionary.ContainsKey(data.LoadedPart.GetInstanceID());
		public override bool Contains(Part part) => partDictionary.ContainsKey(part.GetInstanceID());
		public override bool TryGet(Part part, out PartData pd) => partDictionary.TryGetValue(part.GetInstanceID(), out pd);

		/// <summary> list of all instantiated parts, including editor parts that aren't attached to the ship </summary>
		public List<PartData> AllLoadedParts => allParts;

		public IEnumerable<int> AllInstanceIDs => partDictionary.Keys;

		private double lastFixedUpdateTimeStamp = -1.0;
		private int lastLoadedPartsCount = 0;

		protected override List<PartData> Parts
		{
			get
			{
				// optimization : don't rebuild shipPartsCache if this is called multiple times in the same
				// fixedupdate cycle and if total part count hasn't changed.
				if (Time.fixedTime != lastFixedUpdateTimeStamp || allParts.Count != lastLoadedPartsCount)
				{
					shipPartsCache.Clear();
					lastFixedUpdateTimeStamp = Time.fixedTime;
					lastLoadedPartsCount = allParts.Count;
					foreach (PartData partData in allParts)
					{
						if (!partData.LoadedPart.frozen)
						{
							shipPartsCache.Add(partData);
						}
					}
				}

				return shipPartsCache;
			}
		}

		

		public void Add(PartData partData)
		{
			int instanceID = partData.LoadedPart.GetInstanceID();
			if (partDictionary.ContainsKey(instanceID))
			{
				Lib.LogDebugStack($"PartData with key {instanceID} exists already ({partData.Title})", Lib.LogLevel.Warning);
				return;
			}

			partDictionary.Add(instanceID, partData);
			allParts.Add(partData);
		}

		public void Remove(int instanceID)
		{
			if (partDictionary.TryGetValue(instanceID, out PartData partData))
			{
				partDictionary.Remove(instanceID);
				allParts.Remove(partData);
			}
		}

		public void Remove(Part part)
		{
			Remove(part.GetInstanceID());
		}

		public void Clear()
		{
			partDictionary.Clear();
			allParts.Clear();
		}

		public override void Save(ConfigNode vesselDataNode)
		{
			ConfigNode partsNode = new ConfigNode(NODENAME_PARTS);
			foreach (PartData partData in Parts)
			{
				bool isPersistent = false;
				ConfigNode partNode = new ConfigNode(partData.LoadedPart.craftID.ToString());

				isPersistent |= PartResourceData.SavePartResources(partData, partNode);
				isPersistent |= PartRadiationData.SaveRadiationData(partData, partNode);

				if (isPersistent)
					partsNode.AddNode(partNode);
			}

			if (partsNode.CountNodes > 0)
				vesselDataNode.AddNode(partsNode);
		}

		public override void Load(ConfigNode vesselDataNode)
		{
			ConfigNode partsNode = vesselDataNode.GetNode(NODENAME_PARTS);
			if (partsNode == null)
				return;

			Dictionary<uint, ConfigNode> nodesByCraftID = new Dictionary<uint, ConfigNode>(Parts.Count);

			foreach (ConfigNode partNode in partsNode.nodes)
			{
				nodesByCraftID.Add(Lib.Parse.ToUInt(partNode.name), partNode);
			}

			foreach (PartData partData in allParts)
			{
				if (!nodesByCraftID.TryGetValue(partData.LoadedPart.craftID, out ConfigNode partNode))
					continue;

				PartResourceData.LoadPartResources(partData, partNode);
				PartRadiationData.LoadRadiationData(partData, partNode);
			}
		}
	}
}
