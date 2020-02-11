using System;
using System.Collections.Generic;

namespace KERBALISM
{
	/// <summary>
	/// VirtualResource is meant to be used in the following cases :
	/// <para/>- replacement for processes pseudoresources (note : this will require migrating the planner resource sim before that can happen)
	/// <para/>- checking the output of a resource consumer, by creating a Recipe with the consumed resource as input and the virtual resource as output
	/// <para/>- eventually they could be used to replace the habitat pseudo resources but well... don't touch habitat.
	/// </summary>
	public class VirtualResource : IResource
	{
		/// <summary>
		/// Virtual resource name. Use an unique name to avoid it being shared.
		/// Examples :
		/// <para/>- "myResource" + "myModule" will make the resource shared with all "myModule" partmodules
		/// <para/>- "myResource" + "myModule" + part.flightID will make that resource local to a partmodule and a part
		/// </summary>
		public string Name { get; private set; }

		/// <summary> Amount of virtual resource. This can be set directly if needed.</summary>
		public double Amount
		{
			get
			{
				return amount;
			}
			set
			{
				if (value < 0.0)
					amount = 0.0;
				else if (value > capacity)
					amount = capacity;
				else
					amount = value;
			}
		}
		private double amount;

		/// <summary>
		/// Storage capacity of the virtual resource. Will default to double.MaxValue unless explicitely defined
		/// <para/>Note that a virtual resource used as output in a Recipe will follow the same rules as a regular resource regarding the "dump" behvior specified in the Recipe.
		/// <para/>In the current state of things, if you intent to use Capacity in a VirtualResource it must be set manually 
		/// from OnLoad or OnStart as there is no persistence for it.
		/// </summary>
		public double Capacity
		{
			get
			{
				return capacity;
			}
			set
			{
				if (value < 0.0)
					capacity = 0.0;
				else
					capacity = value;

				if (amount > capacity)
					amount = capacity;
			}
		}
		private double capacity;

		/// <summary> Not yet consumed or produced amount, will be synchronized to Amount in Sync()</summary>
		public double Deferred { get; private set; }

		/// <summary> Amount vs capacity, or 0 if there is no capacity</summary>
		public double Level { get; private set; }

		public bool IsNewInstance { get; private set; }

		public VirtualResource(string virtualName)
		{
			Name = virtualName;
			Amount = 0.0;
			Deferred = 0.0;
			Capacity = double.MaxValue;
			Level = 0.0;
			IsNewInstance = true;
		}

		public bool ExecuteAndSyncToParts(double elapsed_s, List<ResourceWrapper> partResources = null)
		{
			IsNewInstance = false;
			Amount += Deferred;
			Deferred = 0.0;
			Level = Capacity > 0.0 ? Amount / Capacity : 0.0;
			return false;
		}

		/// <summary>Record a consumption, it will be stored in 'Deferred' until the Sync() method synchronize it to 'Amount'</summary>
		/// <param name="brokerName">origin of the consumption, will be available in the UI</param>
		public void Produce(double quantity, ResourceBroker broker)
		{
			Deferred += quantity;
		}

		/// <summary>Record a production, it will be stored in 'Deferred' until the Sync() method synchronize it to 'Amount'</summary>
		/// <param name="brokerName">origin of the production, will be available in the UI</param>
		public void Consume(double quantity, ResourceBroker broker)
		{
			Deferred -= quantity;
		}

		public static void Load(Vessel v, ConfigNode vesselDataNode)
		{
			foreach (ConfigNode node in vesselDataNode.GetNodes("VirtualResources"))
			{
				string resName = Lib.ConfigValue(node, "Name", string.Empty);
				if (string.IsNullOrEmpty(resName)) return;
				VirtualResource res = (VirtualResource)ResourceCache.GetResource(v, resName);
				res.Amount = Lib.ConfigValue(node, "Amount", 0.0);
				res.Capacity = Lib.ConfigValue(node, "Capacity", 0.0);
				res.Level = res.Capacity > 0.0 ? res.Amount / res.Capacity : 0.0;
				res.IsNewInstance = false;
			}
		}

		public static void Save(Vessel v, ConfigNode node)
		{
			if (v == null) return;
			foreach (VirtualResource vRes in ResourceCache.GetVesselHandler(v).GetVirtualResources())
			{
				if (vRes.Amount <= 0.0 && vRes.Capacity <= 0.0) continue;
				ConfigNode vResNode = node.AddNode("VirtualResources");
				node.AddValue("Name", vRes.Name);
				node.AddValue("Amount", vRes.Amount);
				node.AddValue("Capacity", vRes.Capacity);
			}
		}
	}
}
