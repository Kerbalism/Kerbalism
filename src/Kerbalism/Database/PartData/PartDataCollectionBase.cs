using System;
using System.Collections;
using System.Collections.Generic;

namespace KERBALISM
{
	public abstract class PartDataCollectionBase : IEnumerable<PartData>
	{
		public const string NODENAME_PARTS = "PARTS";

		protected abstract List<PartData> Parts { get; }

		public int Count => Parts.Count;
		public PartData this[int index] => Parts[index];

		public abstract PartData this[Part part] { get; }
		public abstract bool Contains(PartData data);
		public abstract bool Contains(Part part);
		public abstract bool TryGet(Part part, out PartData pd);

		public IEnumerable<T> AllModulesOfType<T>()
		{
			foreach (PartData partData in Parts)
			{
				for (int i = 0; i < partData.modules.Count; i++)
				{
					if (partData.modules[i] is T moduleData)
					{
						yield return moduleData;
					}
				}
			}
		}

		public IEnumerable<T> AllModulesOfType<T>(Predicate<T> predicate)
		{
			foreach (PartData partData in Parts)
			{
				for (int i = 0; i < partData.modules.Count; i++)
				{
					if (partData.modules[i] is T moduleData && predicate(moduleData))
					{
						yield return moduleData;
					}
				}
			}
		}

		public IEnumerator<PartData> GetEnumerator()
		{
			return Parts.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return Parts.GetEnumerator();
		}

		public abstract void Save(ConfigNode VesselDataNode);
		public abstract void Load(ConfigNode VesselDataNode);
	}


}
