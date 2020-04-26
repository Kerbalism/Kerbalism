using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KERBALISM
{
	/// <summary>
	/// Handler for a single "real" resource on a vessel. Expose vessel-wide information about amounts, rates and brokers (consumers/producers).
	/// Responsible for synchronization between the resource simulator and the actual resources present on each part. 
	/// </summary>
	public class VesselKSPResource : VesselResource
	{
		private ResourceWrapper resourceWrapper;

		/// <summary> Associated resource name</summary>
		public override string Name => resourceWrapper.name;

		/// <summary> Shortcut to the resource definition "displayName" </summary>
		public override string Title => PartResourceLibrary.Instance.resourceDefinitions[resourceWrapper.name].displayName;

		/// <summary> Shortcut to the resource definition "isVisible" </summary>
		public override bool Visible => PartResourceLibrary.Instance.resourceDefinitions[resourceWrapper.name].isVisible;

		/// <summary> Shortcut to the resource definition "abbreviation" </summary>
		public string Abbreviation => PartResourceLibrary.Instance.resourceDefinitions[resourceWrapper.name].abbreviation;

		/// <summary> Shortcut to the resource definition "density" </summary>
		public float Density => PartResourceLibrary.Instance.resourceDefinitions[resourceWrapper.name].density;

		/// <summary> Shortcut to the resource definition "unitCost" </summary>
		public float UnitCost => PartResourceLibrary.Instance.resourceDefinitions[resourceWrapper.name].unitCost;

		/// <summary> Rate of change in amount per-second, this is purely for visualization</summary>
		public override double Rate => rate; double rate;

		/// <summary> Amount vs capacity, or 0 if there is no capacity</summary>
		public override double Level => level; double level;

		/// <summary> Not yet consumed or produced amount that will be synchronized to the vessel parts in ExecuteAndSyncToParts()</summary>
		public override double Deferred => deferred; double deferred;
		private double deferredNonCriticalConsumers;

		/// <summary> Amount of resource</summary>
		public override double Amount => resourceWrapper.amount;

		/// <summary> Storage capacity of resource</summary>
		public override double Capacity => resourceWrapper.capacity;

		/// <summary> If enabled, the total resource amount will be redistributed evenly amongst all parts. Reset itself to "NotSet" after every ExecuteAndSyncToParts() call</summary>
		public EqualizeMode equalizeMode = EqualizeMode.NotSet;
		public enum EqualizeMode { NotSet, Enabled, Disabled }

		/// <summary> [0 ; 1] availability factor that will be applied to every Consume() call in the next simulation step</summary>
		public override double AvailabilityFactor => availabilityFactor;
		private double availabilityFactor = 0.0;

		/// <summary> true if all critical Consume() calls have been satisfied in the last sim step</summary>
		public bool CriticalConsumptionSatisfied { get; private set; }

		/// <summary> last step consumption requests. For visualization only, can be greater than what was actually consumed </summary>
		public override double ConsumeRequests => consumeRequests; double consumeRequests;

		/// <summary> last step production requests. For visualization only, can be greater than what was actually produced </summary>
		public override double ProduceRequests => produceRequests; double produceRequests;

		private double currentConsumeRequests;
		private double currentConsumeCriticalRequests;
		private double currentProduceRequests;

		/// <summary>Dictionary of all consumers and producers (key) and how much amount they did add/remove (value).</summary>
		private Dictionary<ResourceBroker, double> brokersResourceAmounts;

		public override List<ResourceBrokerRate> ResourceBrokers => resourceBrokers;  List<ResourceBrokerRate>resourceBrokers;

		public override bool NeedUpdate => availabilityFactor != 0.0 || deferred != 0.0 || Capacity != 0.0 || resourceBrokers.Count != 0;

		/// <summary>Ctor</summary>
		public VesselKSPResource(ResourceWrapper resourceWrapper)
		{
			this.resourceWrapper = resourceWrapper;
			resourceBrokers = new List<ResourceBrokerRate>();
			brokersResourceAmounts = new Dictionary<ResourceBroker, double>();
		}

		public void SetWrapper(ResourceWrapper resourceWrapper)
		{
			this.resourceWrapper = resourceWrapper;
		}

		public override void Init()
		{
			deferred = 0.0;
			deferredNonCriticalConsumers = 0.0;
			currentConsumeRequests = 0.0;
			currentConsumeCriticalRequests = 0.0;
			currentProduceRequests = 0.0;
			CriticalConsumptionSatisfied = true;

			// calculate level
			level = resourceWrapper.capacity > 0.0 ? resourceWrapper.amount / resourceWrapper.capacity : 0.0;
		}

		/// <summary>Record a production, it will be stored in "Deferred" and later synchronized to the vessel in Sync()</summary>
		/// <param name="broker">origin of the production, will be available in the UI</param>
		public override void Produce(double quantity, ResourceBroker broker = null)
		{
			if (quantity == 0.0)
				return;

			currentProduceRequests += quantity;
			deferred += quantity;

			if (broker == null)
				broker = ResourceBroker.Generic;

			if (brokersResourceAmounts.ContainsKey(broker))
				brokersResourceAmounts[broker] += quantity;
			else
				brokersResourceAmounts.Add(broker, quantity);
		}

		/// <summary>
		/// Record a consumption, it will be stored in "Deferred" and later synchronized to the vessel in ExecuteAndSyncToParts()
		/// <para/>IMPORTANT : quantity should NEVER be scaled by the AvailabilityFactor,
		/// and you should avoid to use a "if resource amount == 0, don't consume" logic.
		/// <para/> Instead, always consume the resource, and scale or disable the effect based on AvailabilityFactor.
		/// </summary>
		/// <param name="quantity">amount to consume. This should always be scaled by the timestep (elapsed_s)</param>
		/// <param name="broker">source of the consumption, for UI purposes</param>
		/// <param name="isCritial">
		/// if false, scale the consumption by the resource AvailabilityFactor. If false, don't scale it by AvailabilityFactor.
		/// <para/>You can know if all critical Consume() calls for this resource have been satified in last step by checking the CriticalConsumptionSatisfied property
		/// </param>
		public override void Consume(double quantity, ResourceBroker broker = null, bool isCritial = false)
		{
			if (quantity == 0.0)
				return;

			if (isCritial)
			{
				currentConsumeCriticalRequests += quantity;
				deferred -= quantity;
			}
			else
			{
				currentConsumeRequests += quantity;
				quantity *= availabilityFactor;
				deferredNonCriticalConsumers -= quantity;
			}

			if (broker == null)
				broker = ResourceBroker.Generic;

			if (brokersResourceAmounts.ContainsKey(broker))
				brokersResourceAmounts[broker] -= quantity;
			else
				brokersResourceAmounts.Add(broker, -quantity);
		}

		public override void RecipeConsume(double quantity, ResourceBroker broker)
		{
			if (quantity == 0.0)
				return;

			currentConsumeCriticalRequests += quantity;
			deferred -= quantity;

			if (brokersResourceAmounts.ContainsKey(broker))
				brokersResourceAmounts[broker] -= quantity;
			else
				brokersResourceAmounts.Add(broker, -quantity);
		}

		/// <summary>synchronize resources from cache to vessel</summary>
		/// <remarks>
		/// this function will also sync from vessel to cache so you can always use the
		/// VesselResource properties to get information about resources
		/// </remarks>
		public override bool ExecuteAndSyncToParts(VesselDataBase vd, double elapsed_s)
		{
			// detect flow state changes
			bool flowStateChanged = resourceWrapper.capacity - resourceWrapper.oldCapacity > 1e-05;

			// As we haven't yet synchronized anything, changes to amount can only come from non-Kerbalism producers or consumers
			double unknownBrokersRate = resourceWrapper.amount - resourceWrapper.oldAmount;
			// Avoid false detection due to precision errors
			if (Math.Abs(unknownBrokersRate) < 1e-05) unknownBrokersRate = 0.0;

			// critical consumers are satisfied if there is enough produced + stored.
			// we are sure of that because Recipes are processed after critical Consume() calls,
			// and they will not underflow (consume more than what is available in amount + deferred)
			// and non critical consumers have been isolated in deferredNonCriticalConsumers
			CriticalConsumptionSatisfied = resourceWrapper.amount + currentProduceRequests >= currentConsumeCriticalRequests;
			currentConsumeRequests += currentConsumeCriticalRequests;
			currentConsumeCriticalRequests = 0.0;

			// untested  and not sure this is a good idea
			// we need to test the behaviour of availabilityFactor when non kerbalism consumers are involved
			//if (unsupportedBrokersRate < 0.0)
			//	consumeRequests += Math.Abs(unsupportedBrokersRate);

			// deduce the [0 ; 1] availability factor that will be applied to every Consume() call in the next simulation step
			// The purpose of this is
			// - To stabilize the resource sim when consumption > production instead of having in a perpetual "0 / max / 0 / max" cycle
			// - To be able to scale the output of whatever the consumption request is for : comms data rate, experiment data rate,
			//   anything that is doing some action based on a resource consumption.
			// See excel simulation in misc/ResourceSim-AvailabilityFactor.xlsx


			// if there are consumers, get the availability factor from the consume/produce ratio
			if (currentConsumeRequests > 0.0)
			{
				// calculate the resource "starvation" : how much of the consume requests can't be satisfied
				double starvation = Math.Abs(Math.Min(resourceWrapper.amount + currentProduceRequests - currentConsumeRequests, 0.0));
				availabilityFactor = Math.Max(1.0 - (starvation / currentConsumeRequests), 0.0); 
			}
			// otherwise, just check the resource amount
			else
			{
				availabilityFactor = resourceWrapper.amount == 0.0 ? 0.0 : 1.0;
			}

			produceRequests = currentProduceRequests;
			consumeRequests = currentConsumeRequests;
			currentProduceRequests = 0.0;
			currentConsumeRequests = 0.0;

			// deferred currently is the result of :
			// - all Produce() calls
			// - all Recipes execution (which do not underfow the available quantity + production)
			// - critical Consume() calls (wich can underflow the available quantity + production)
			// To give priority to critical consumers, we kept non critical ones isolated, 
			// but now is time to finally synchronise everyone.
			deferred += deferredNonCriticalConsumers;
			deferredNonCriticalConsumers = 0.0;

			// clamp consumption/production to vessel amount/capacity
			// - if deferred is negative, then amount is guaranteed to be greater than zero
			// - if deferred is positive, then capacity - amount is guaranteed to be greater than zero
			deferred = Lib.Clamp(deferred, -resourceWrapper.amount, resourceWrapper.capacity - resourceWrapper.amount);

			resourceWrapper.SyncToPartResources(deferred, equalizeMode == EqualizeMode.Enabled);

			equalizeMode = EqualizeMode.NotSet;

			// update amount, to get correct rate and levels at all times
			resourceWrapper.amount += deferred;

			// reset deferred production/consumption
			deferred = 0.0;

			// recalculate level
			level = resourceWrapper.capacity > 0.0 ? resourceWrapper.amount / resourceWrapper.capacity : 0.0;

			// calculate rate of change per-second
			// - don't update rate during warp blending (stock modules have instabilities during warp blending) 
			// - ignore interval-based rules consumption/production
			if (!Kerbalism.WarpBlending)
				rate = (resourceWrapper.amount - resourceWrapper.oldAmount) / elapsed_s;

			// For visualization purpose, update the brokers list, merging all detected sources :
			// - normal brokers that use Consume() or Produce()
			// - non-Kerbalism brokers (aggregated rate)
			resourceBrokers.Clear();

			foreach (KeyValuePair<ResourceBroker, double> broker in brokersResourceAmounts)
				if (broker.Value != 0.0)
					resourceBrokers.Add(new ResourceBrokerRate(broker.Key, broker.Value / elapsed_s));

			foreach (ResourceBrokerRate brokerRate in resourceBrokers)
				brokersResourceAmounts[brokerRate.broker] = 0.0;

			if (unknownBrokersRate != 0.0)
			{
				resourceBrokers.Add(new ResourceBrokerRate(ResourceBroker.Generic, unknownBrokersRate / elapsed_s));
			}

			// if incoherent producers are detected, do not allow high timewarp speed
			// - can be disabled in settings
			// - ignore incoherent consumers (no negative consequences for player)
			// - ignore flow state changes (avoid issue with process controllers and other things that alter resource capacities)
			return Settings.EnforceCoherency && TimeWarp.CurrentRate > 1000.0 && unknownBrokersRate > 0.0 && !flowStateChanged;
		}
	}
}
