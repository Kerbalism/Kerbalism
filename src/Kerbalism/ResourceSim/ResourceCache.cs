using System;
using System.Collections.Generic;

namespace KERBALISM
{
	/// <summary>Global cache for storing and accessing VesselResHandler (and each VesselResource) in all vessels, with shortcut for common methods</summary>
	public static class ResourceCache
	{
		// resource cache
		static Dictionary<Guid, VesselResHandler> entries;

		/// <summary> pseudo-ctor </summary>
		public static void Init()
		{
			entries = new Dictionary<Guid, VesselResHandler>();
		}

		/// <summary> clear all resource information for all vessels. Must only be called if all vessels are destroyed</summary>
		public static void Clear()
		{
			entries.Clear();
		}

		/// <summary> Reset the whole resource simulation for the vessel. Must only be called if the vessel is destroyed</summary>
		public static void Purge(Vessel v)
		{
			entries.Remove(v.id);
		}

		/// <summary> Reset the whole resource simulation for the vessel. Must only be called if the vessel is destroyed</summary>
		public static void Purge(ProtoVessel pv)
		{
			entries.Remove(pv.vesselID);
		}

		/// <summary> Return the VesselResHandler handler for this vessel </summary>
		public static VesselResHandler GetVesselHandler(Vessel v)
		{
			// try to get existing entry if any
			VesselResHandler entry;
			if (entries.TryGetValue(v.id, out entry)) return entry;

			// create new entry
			entry = new VesselResHandler();

			// remember new entry
			entries.Add(v.id, entry);

			// return new entry
			return entry;
		}

		public static IResource GetResource(Vessel v, string resource_name)
		{
			return GetVesselHandler(v).GetResource(v, resource_name);
		}

		/// <summary> record deferred production of a resource (shortcut) </summary>
		/// <param name="brokerName">short ui-friendly name for the producer</param>
		public static void Produce(Vessel v, string resource_name, double quantity, string brokerName)
		{
			GetResource(v, resource_name).Produce(quantity, brokerName);
		}

		/// <summary> record deferred consumption of a resource (shortcut) </summary>
		/// <param name="brokerName">short ui-friendly name for the consumer</param>
		public static void Consume(Vessel v, string resource_name, double quantity, string brokerName)
		{
			GetResource(v, resource_name).Consume(quantity, brokerName);
		}

		/// <summary> register deferred execution of a recipe (shortcut)</summary>
		public static void AddRecipe(Vessel v, Recipe recipe)
		{
			GetVesselHandler(v).AddRecipe(recipe);
		}
	}
}
