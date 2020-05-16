using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	public class PartResourceDataCollection : IEnumerable<PartResourceData>
	{
		private List<PartResourceData> resources = new List<PartResourceData>();
		private Dictionary<string, PartResourceData> resourcesDict = new Dictionary<string, PartResourceData>();

		public IEnumerator<PartResourceData> GetEnumerator() => resources.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => resources.GetEnumerator();

		public bool Contains(VesselVirtualPartResource resource) => resourcesDict.ContainsKey(resource.Name);
		public bool Contains(string resourceName) => resourcesDict.ContainsKey(resourceName);
		public bool TryGet(VesselVirtualPartResource resource, out PartResourceData res) => resourcesDict.TryGetValue(resource.Name, out res);
		public bool TryGet(string resourceName, out PartResourceData res) => resourcesDict.TryGetValue(resourceName, out res);
		public int Count => resources.Count;

		public PartResourceData AddResource(string resourceName, double amount, double capacity)
		{
			if (resourcesDict.TryGetValue(resourceName, out PartResourceData partRes))
			{
				partRes.Capacity = capacity;
				partRes.Amount = amount;
				return partRes;
			}

			partRes = new PartResourceData(resourceName, amount, capacity);
			resources.Add(partRes);
			resourcesDict.Add(resourceName, partRes);
			return partRes;
		}

		public void RemoveResource(VesselVirtualPartResource resource)
		{
			if (resourcesDict.TryGetValue(resource.Name, out PartResourceData partRes))
			{
				resources.Remove(partRes);
				resourcesDict.Remove(resource.Name);
			}
		}

		public void RemoveResource(string resourceName)
		{
			if (resourcesDict.TryGetValue(resourceName, out PartResourceData partRes))
			{
				resources.Remove(partRes);
				resourcesDict.Remove(resourceName);
			}
		}
	}
}
