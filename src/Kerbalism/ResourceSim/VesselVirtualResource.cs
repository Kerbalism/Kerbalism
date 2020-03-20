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
	public class VesselVirtualResource : VesselResource
	{
		/// <summary>
		/// Virtual resource name. Use an unique name to avoid it being shared.
		/// Examples :
		/// <para/>- "myResource" + "myModule" will make the resource shared with all "myModule" partmodules
		/// <para/>- "myResource" + "myModule" + part.flightID will make that resource local to a partmodule and a part
		/// </summary>
		public override string Name => name; string name;

		public override string Title => title; string title;

		public override bool Visible => false;

		/// <summary> Amount of virtual resource. This can be set directly if needed.</summary>
		public override double Amount => amount; double amount;
		public void SetAmount(double amount)
		{
			if (amount < 0.0)
				this.amount = 0.0;
			else if (amount > capacity)
				this.amount = capacity;
			else
				this.amount = amount;

			level = capacity > 0.0 ? amount / capacity : 0.0;
		}

		/// <summary>
		/// Storage capacity of the virtual resource. Will default to double.MaxValue unless explicitely defined
		/// <para/>Note that a virtual resource used as output in a Recipe will follow the same rules as a regular resource regarding the "dump" behvior specified in the Recipe.
		/// <para/>In the current state of things, if you intent to use Capacity in a VirtualResource it must be set manually 
		/// from OnLoad or OnStart as there is no persistence for it.
		/// </summary>
		public override double Capacity => capacity; double capacity;
		public void SetCapacity(double capacity)
		{
			if (capacity < 0.0)
				this.capacity = 0.0;
			else
				this.capacity = capacity;

			if (amount > capacity)
				amount = capacity;

			level = capacity > 0.0 ? amount / capacity : 0.0;
		}

		/// <summary> Not yet consumed or produced amount, will be synchronized to Amount in Sync()</summary>
		public override double Deferred => deferred; double deferred;

		/// <summary> Amount vs capacity, or 0 if there is no capacity</summary>
		public override double Rate => rate; double rate;

		/// <summary> Amount vs capacity, or 0 if there is no capacity</summary>
		public override double Level => level; double level;

		/// <summary> [0 ; 1] availability factor that will be applied to every Consume() call in the next simulation step</summary>
		public override double AvailabilityFactor => availabilityFactor; double availabilityFactor;

		/// <summary> last step consumption requests. For visualization only, can be greater than what was actually consumed </summary>
		public override double ConsumeRequests => consumeRequests; double consumeRequests;

		/// <summary> last step production requests. For visualization only, can be greater than what was actually produced </summary>
		public override double ProduceRequests => produceRequests; double produceRequests;

		private double currentConsumeRequests;
		private double currentProduceRequests;

		/// <summary> list of consumers and producers for this resource</summary>
		public override List<ResourceBrokerRate> ResourceBrokers => resourceBrokers; List<ResourceBrokerRate> resourceBrokers;

		/// <summary>Dictionary of all consumers and producers (key) and how much amount they did add/remove (value).</summary>
		private Dictionary<ResourceBroker, double> brokersResourceAmounts;

		public override bool NeedUpdate => true;

		/// <summary>Don't use this to create a virtual resource, use the VesselResHandler.SetupOrCreateVirtualResource() method</summary>
		public VesselVirtualResource(string name)
		{
			this.name = name;
			title = name;
			amount = 0.0;
			capacity = double.MaxValue;
			deferred = 0.0;
			level = 0.0;
			resourceBrokers = new List<ResourceBrokerRate>();
			brokersResourceAmounts = new Dictionary<ResourceBroker, double>();
		}

		public void Setup(string title, double amount = 0.0, double capacity = double.MaxValue)
		{
			this.title = title;
			this.amount = amount;
			this.capacity = capacity;
			deferred = 0.0;
			level = capacity > 0.0 ? amount / capacity : 0.0;
		}

		public override bool ExecuteAndSyncToParts(double elapsed_s)
		{
			// if there are consumers, get the availability factor from the consume/produce ratio
			if (currentConsumeRequests > 0.0)
			{
				// calculate the resource "starvation" : how much of the consume requests can't be satisfied
				double starvation = Math.Abs(Math.Min(amount + currentProduceRequests - currentConsumeRequests, 0.0));
				availabilityFactor = Math.Max(1.0 - (starvation / currentConsumeRequests), 0.0);
			}
			// otherwise, just check the resource amount
			else
			{
				availabilityFactor = amount == 0.0 ? 0.0 : 1.0;
			}

			produceRequests = currentProduceRequests;
			consumeRequests = currentConsumeRequests;
			currentProduceRequests = 0.0;
			currentConsumeRequests = 0.0;

			double newAmount = Lib.Clamp(amount + deferred, 0.0, capacity);
			deferred = 0.0;

			// note : VesselResources return zero Rate when there is no actual change in amount, so we try to be consistent
			// and reproduce the same logic here
			rate = (newAmount - amount) / elapsed_s;
			amount = newAmount;
			level = Capacity > 0.0 ? amount / capacity : 0.0;

			resourceBrokers.Clear();

			foreach (KeyValuePair<ResourceBroker, double> broker in brokersResourceAmounts)
				if (broker.Value != 0.0)
					resourceBrokers.Add(new ResourceBrokerRate(broker.Key, broker.Value / elapsed_s));

			foreach (ResourceBrokerRate brokerRate in resourceBrokers)
				brokersResourceAmounts[brokerRate.broker] = 0.0;

			return false;
		}

		/// <summary>Record a consumption, it will be stored in 'Deferred' until the Sync() method synchronize it to 'Amount'</summary>
		/// <param name="brokerName">origin of the consumption, will be available in the UI</param>
		public override void Produce(double quantity, ResourceBroker broker)
		{
			produceRequests += quantity;
			deferred += quantity;

			if (broker == null)
				broker = ResourceBroker.Generic;

			if (brokersResourceAmounts.ContainsKey(broker))
				brokersResourceAmounts[broker] += quantity;
			else
				brokersResourceAmounts.Add(broker, quantity);
		}

		/// <summary>Record a production, it will be stored in 'Deferred' until the Sync() method synchronize it to 'Amount'</summary>
		/// <param name="brokerName">origin of the production, will be available in the UI</param>
		public override void Consume(double quantity, ResourceBroker broker, bool isCritical = false)
		{
			consumeRequests += quantity;
			quantity *= availabilityFactor;
			deferred -= quantity;

			if (broker == null)
				broker = ResourceBroker.Generic;

			if (brokersResourceAmounts.ContainsKey(broker))
				brokersResourceAmounts[broker] -= quantity;
			else
				brokersResourceAmounts.Add(broker, -quantity);
		}

		public override void RecipeConsume(double quantity, ResourceBroker broker)
		{
			consumeRequests += quantity;
			deferred -= quantity;

			if (brokersResourceAmounts.ContainsKey(broker))
				brokersResourceAmounts[broker] -= quantity;
			else
				brokersResourceAmounts.Add(broker, -quantity);
		}

		public static void LoadAll(VesselDataBase vd, ConfigNode vesselDataNode)
		{
			VesselResHandler resHandler = vd.ResHandler;
			foreach (ConfigNode node in vesselDataNode.GetNodes("VIRTUALRESOURCE"))
			{
				string resName = Lib.ConfigValue(node, "Name", string.Empty);
				if (string.IsNullOrEmpty(resName)) return;

				resHandler.SetupOrCreateVirtualResource(
					resName,
					Lib.ConfigValue(node, "Title", resName),
					Lib.ConfigValue(node, "Amount", 0.0),
					Lib.ConfigValue(node, "Capacity", 0.0));
			}
		}

		public static void SaveAll(VesselDataBase vd, ConfigNode vesselDataNode)
		{
			foreach (VesselVirtualResource vRes in vd.ResHandler.GetVirtualResources())
			{
				if (vRes.Amount == 0.0 && vRes.Capacity == 0.0)
					continue;

				ConfigNode vResNode = vesselDataNode.AddNode("VIRTUALRESOURCE");
				vResNode.AddValue("Name", vRes.Name);
				vResNode.AddValue("Title", vRes.Name);
				vResNode.AddValue("Amount", vRes.Amount);
				vResNode.AddValue("Capacity", vRes.Capacity);
			}
		}
	}
}
