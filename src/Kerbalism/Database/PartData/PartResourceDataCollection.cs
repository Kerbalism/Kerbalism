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

		public IEnumerator<PartResourceData> GetEnumerator() => resources.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => resources.GetEnumerator();
		public bool Contains(string resourceName) => resources.Exists(p => p.ResourceName == resourceName);
		public int Count => resources.Count;

		public PartResourceData AddResource(string resourceName, double amount, double capacity, string containerId = null)
		{
			foreach (PartResourceData existingRes in resources)
			{
				if (existingRes.ResourceName == resourceName && existingRes.ContainerId == containerId)
				{
					existingRes.Capacity = capacity;
					existingRes.Amount = amount;
					return existingRes;
				}
			}

			PartResourceData res = new PartResourceData(resourceName, amount, capacity, containerId);
			resources.Add(res);
			return res;
		}

		/// <summary> remove all resources with the specified name and container id</summary>
		public void RemoveResource(string resourceName, string containerId = null)
		{
			resources.RemoveAll(p => p.ResourceName == resourceName && p.ContainerId == containerId);
		}

		/// <summary> remove all resources with the specified name, regardless of their container id</summary>
		public void RemoveResource(string resourceName)
		{
			resources.RemoveAll(p => p.ResourceName == resourceName);
		}
	}
}
