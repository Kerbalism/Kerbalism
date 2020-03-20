using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	public class PartDataCollectionShip : IEnumerable<PartData>
	{
		private List<PartData> partList = new List<PartData>();
		private Dictionary<int, PartData> partDictionary = new Dictionary<int, PartData>();

		public IEnumerator<PartData> GetEnumerator() => partList.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => partList.GetEnumerator();

		public int Count => partList.Count;
		public PartData this[int index] => partList[index];
		public PartData this[Part part] => partDictionary[part.GetInstanceID()];
		public PartData this[PartData data] => partDictionary[data.LoadedPart.GetInstanceID()];
		public bool Contains(PartData data) => partDictionary.ContainsKey(data.LoadedPart.GetInstanceID());
		public bool Contains(Part part) => partDictionary.ContainsKey(part.GetInstanceID());
		public bool TryGet(int instanceID, out PartData pd) => partDictionary.TryGetValue(instanceID, out pd);

		public IEnumerable<int> AllInstanceIDs => partDictionary.Keys;

		public void Add(PartData partData)
		{
			int instanceID = partData.LoadedPart.GetInstanceID();
			if (partDictionary.ContainsKey(instanceID))
			{
				Lib.LogDebugStack($"PartData with key {instanceID} exists already ({partData.Title})", Lib.LogLevel.Warning);
				return;
			}

			partDictionary.Add(instanceID, partData);
			partList.Add(partData);
		}

		public void Remove(int instanceID)
		{
			if (partDictionary.TryGetValue(instanceID, out PartData partData))
			{
				partDictionary.Remove(instanceID);
				partList.Remove(partData);
			}
		}

		public void Remove(Part part)
		{
			Remove(part.GetInstanceID());
		}

		public void Clear()
		{
			partDictionary.Clear();
			partList.Clear();
		}
	}
}
