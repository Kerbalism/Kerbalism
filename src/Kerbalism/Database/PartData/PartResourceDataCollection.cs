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

		public PartResourceData AddResource(string resourceName, double amount, double capacity, bool asSeparateContainer = false)
		{
			int containerIndex = 0;

			foreach (PartResourceData existingRes in resources)
			{
				if (existingRes.ResourceName == resourceName)
				{
					if (asSeparateContainer)
					{
						containerIndex = Math.Max(containerIndex, existingRes.ContainerIndex) + 1;
					}
					else
					{
						existingRes.Capacity = capacity;
						existingRes.Amount = amount;
						return existingRes;
					}
				}
			}

			return AddResource(resourceName, amount, capacity, containerIndex);
		}

		public PartResourceData AddResource(string resourceName, double amount, double capacity, int containerIndex)
		{
			PartResourceData res = new PartResourceData(resourceName, containerIndex, amount, capacity);
			resources.Add(res);
			return res;
		}

		public PartResourceData GetResource(string resourceName)
		{
			return resources.Find(p => p.ResourceName == resourceName);
		}

		public PartResourceData GetResource(string resourceName, int containerIndex)
		{
			return resources.Find(p => p.ResourceName == resourceName && p.ContainerIndex == containerIndex);
		}

		/// <summary> remove all resources with the specified name and container index</summary>
		public void RemoveResource(string resourceName, int containerIndex)
		{
			resources.RemoveAll(p => p.ResourceName == resourceName && p.ContainerIndex == containerIndex);
		}

		/// <summary> remove all resources with the specified name, regardless of their container</summary>
		public void RemoveResource(string resourceName)
		{
			resources.RemoveAll(p => p.ResourceName == resourceName);
		}

		/// <summary> remove a resource</summary>
		public void RemoveResource(PartResourceData resource)
		{
			resources.RemoveAll(p => p == resource);
		}
	}
}
