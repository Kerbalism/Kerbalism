using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	public class PartDataCollectionShip : IEnumerable<PartDataShip>
	{
		private List<PartDataShip> partList = new List<PartDataShip>();
		private Dictionary<int, PartDataShip> partDictionary = new Dictionary<int, PartDataShip>();

		public IEnumerator<PartDataShip> GetEnumerator() => partList.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => partList.GetEnumerator();

		public int Count => partList.Count;
		public PartDataShip this[int index] => partList[index];
		public PartDataShip this[Part part] => partDictionary[part.GetInstanceID()];
		public PartDataShip this[PartDataShip data] => partDictionary[data.LoadedPart.GetInstanceID()];
		public bool Contains(PartDataShip data) => partDictionary.ContainsKey(data.LoadedPart.GetInstanceID());
		public bool Contains(Part part) => partDictionary.ContainsKey(part.GetInstanceID());
		public bool TryGet(int instanceID, out PartDataShip pd) => partDictionary.TryGetValue(instanceID, out pd);

		public IEnumerable<int> AllInstanceIDs => partDictionary.Keys;

		public void Add(PartDataShip partData)
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
			if (partDictionary.TryGetValue(instanceID, out PartDataShip partData))
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
