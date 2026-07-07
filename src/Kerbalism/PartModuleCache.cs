using System;
using System.Collections;
using System.Collections.Generic;

namespace KERBALISM
{
	///<summary>
	/// Per-vessel cache of loaded PartModules, bucketed by queried type and built lazily
	/// in a single parts/modules pass per queried type. The returned lists are cached and shared :
	/// - never modify them
	/// - they include disabled modules : check isEnabled at use time (Configure toggles
	///   isEnabled at runtime without firing any vessel-modified event)
	/// <para/>
	/// Validity is self-checked against the vessel part list : KSP creates a new parts list
	/// instance on vessel load (Vessel.StartFromBackup), so stale module references can't leak
	/// across an unload/reload cycle even if no purge event fired. Structural changes while
	/// loaded are caught by the part count check, and trough Cache.PurgeVesselCaches() which
	/// is called on all the vessel-modified game events.
	///</summary>
	public static class PartModuleCache
	{
		private sealed class Entry
		{
			public List<Part> partsRef;	// v.parts instance at build time
			public int partCount;		// v.parts.Count at build time
			public readonly Dictionary<Type, IList> byType = new Dictionary<Type, IList>();
		}

		private static readonly Dictionary<Guid, Entry> entries = new Dictionary<Guid, Entry>();

		///<summary>
		/// return all partmodules implementing a specific type on a loaded vessel.
		/// note: disabled modules are returned, check isEnabled on each module.
		/// note: the returned list is cached and shared, do not modify it.
		///</summary>
		public static List<T> GetModules<T>(Vessel v) where T : class
		{
			Guid id = Lib.VesselID(v);

			Entry e;
			if (!entries.TryGetValue(id, out e)
				|| !ReferenceEquals(e.partsRef, v.parts)
				|| e.partCount != v.parts.Count)
			{
				e = new Entry { partsRef = v.parts, partCount = v.parts.Count };
				entries[id] = e;
			}

			IList cached;
			if (e.byType.TryGetValue(typeof(T), out cached))
				return (List<T>)cached;

			List<T> result = new List<T>();
			for (int i = 0; i < v.parts.Count; ++i)
			{
				Part p = v.parts[i];
				for (int j = 0; j < p.Modules.Count; ++j)
				{
					if (p.Modules[j] is T t)
						result.Add(t);
				}
			}
			e.byType.Add(typeof(T), result);
			return result;
		}

		///<summary>forget the cached module lists of a vessel</summary>
		public static void Purge(Guid id)
		{
			entries.Remove(id);
		}

		///<summary>forget the cached module lists of all vessels</summary>
		public static void Clear()
		{
			entries.Clear();
		}
	}
}
