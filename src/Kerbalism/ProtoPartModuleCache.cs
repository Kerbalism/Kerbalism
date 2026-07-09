using System;
using System.Collections.Generic;

namespace KERBALISM
{
	///<summary>
	/// Per-vessel cache of ProtoPartModuleSnapshots, bucketed by queried module name and built
	/// lazily in a single parts/modules pass per queried name. The returned lists are cached and
	/// shared :
	/// - never modify them
	/// - disabled modules are not included
	/// <para/>
	/// Validity is self-checked against the ProtoVessel instance : KSP rebuilds the whole proto
	/// snapshot hierarchy of loaded vessels on every backup (Vessel.BackupVessel, called on game
	/// saves) without firing any vessel-modified event, so a new ProtoVessel instance can't serve
	/// stale module references. Structural changes are caught trough Cache.PurgeVesselCaches()
	/// which is called on all the vessel-modified game events.
	///</summary>
	public static class ProtoPartModuleCache
	{
		private sealed class Entry
		{
			public ProtoVessel protoRef;	// ProtoVessel instance at build time
			public readonly Dictionary<string, List<ProtoPartModuleSnapshot>> byName = new Dictionary<string, List<ProtoPartModuleSnapshot>>();
		}

		// vessel ID -> cache entry
		private static readonly Dictionary<Guid, Entry> entries = new Dictionary<Guid, Entry>();

		///<summary>
		/// return all proto modules with a specified name on a vessel.
		/// note: disabled modules are not returned.
		/// note: the returned list is cached and shared, do not modify it.
		///</summary>
		public static List<ProtoPartModuleSnapshot> GetModules(ProtoVessel v, string module_name)
		{
			Guid id = Lib.VesselID(v);

			Entry e;
			if (!entries.TryGetValue(id, out e) || !ReferenceEquals(e.protoRef, v))
			{
				e = new Entry { protoRef = v };
				entries[id] = e;
			}

			List<ProtoPartModuleSnapshot> result;
			if (e.byName.TryGetValue(module_name, out result))
				return result;

			result = new List<ProtoPartModuleSnapshot>(8);
			for (int i = 0; i < v.protoPartSnapshots.Count; ++i)
			{
				ProtoPartSnapshot p = v.protoPartSnapshots[i];
				for (int j = 0; j < p.modules.Count; ++j)
				{
					ProtoPartModuleSnapshot m = p.modules[j];
					if (m.moduleName == module_name && Lib.Proto.GetBool(m, "isEnabled"))
						result.Add(m);
				}
			}
			e.byName.Add(module_name, result);
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
