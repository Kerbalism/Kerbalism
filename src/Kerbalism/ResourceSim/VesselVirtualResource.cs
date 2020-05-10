using System;
using System.Collections.Generic;

namespace KERBALISM
{
	/// <summary>
	/// VesselVirtualResource is a vessel wide, non persisted resource.<br/>
	/// It behave like any other resource in regard to Recipes and produce/consume calls, at the exception that it doesn't handle "critical" consumptions<br/>
	/// It's amount and capacity isn't held in any part, and can be set directy using the relevant methods.<br/>
	/// Since it isn't persisted, it has to be re-instantiatiated after loads by whatever component is using it.
	/// </summary>
	public class VesselVirtualResource : VesselResource
	{
		/// <summary> Virtual resource name. Use an unique name to avoid it being shared. </summary>
		public override string Name => name;
		protected string name;

		public override string Title => title;
		protected string title;

		public override bool Visible => false;

		/// <summary> Amount of virtual resource. This can be set directly if needed.</summary>
		public override double Amount => amount;
		protected double amount;

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
		/// </summary>
		public override double Capacity => capacity;
		protected double capacity;

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

		public override bool NeedUpdate => true;

		/// <summary>Don't use this to create a virtual resource, use the VesselResHandler.CreateVirtualResource() method</summary>
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

		public override void Init()
		{
			return;
		}

		public override bool ExecuteAndSyncToParts(VesselDataBase vd, double elapsed_s)
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
	}
}
