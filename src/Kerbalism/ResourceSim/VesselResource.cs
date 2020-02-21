using System;
using System.Collections.Generic;

namespace KERBALISM
{
	/// <summary>
	/// Handler for a single "real" resource on a vessel. Expose vessel-wide information about amounts, rates and brokers (consumers/producers).
	/// Responsible for synchronization between the resource simulator and the actual resources present on each part. 
	/// </summary>
	public class VesselResource : IResource
	{
		private ResourceWrapper resourceWrapper;

		/// <summary> Associated resource name</summary>
		public string Name => resourceWrapper.name;

		/// <summary> Rate of change in amount per-second, this is purely for visualization</summary>
		public double Rate => rate; double rate;

		/// <summary> Rate of change in amount per-second, including average rate for interval-based rules</summary>
		public double AverageRate => averageRate; double averageRate;

		/// <summary> Amount vs capacity, or 0 if there is no capacity</summary>
		public double Level => level; double level;

		/// <summary> True if an interval-based rule consumption/production was processed in the last simulation step</summary>
		public bool IntervalRuleHappened => intervalRuleHappened; bool intervalRuleHappened;

		/// <summary> Not yet consumed or produced amount that will be synchronized to the vessel parts in ExecuteAndSyncToParts()</summary>
		public double Deferred => deferred; double deferred;
		private double deferredNonCriticalConsumers;

		/// <summary> Amount of resource</summary>
		public double Amount => resourceWrapper.amount;

		/// <summary> Storage capacity of resource</summary>
		public double Capacity => resourceWrapper.capacity;

		/// <summary> If enabled, the total resource amount will be redistributed evenly amongst all parts. Reset itself to "NotSet" after every ExecuteAndSyncToParts() call</summary>
		public EqualizeMode equalizeMode = EqualizeMode.NotSet;
		public enum EqualizeMode { NotSet, Enabled, Disabled }

		/// <summary> Simulated average rate of interval-based rules in amount per-second. This is for information only, the resource is not consumed</summary>
		private double intervalRulesRate;

		/// <summary> Amount consumed/produced by interval-based rules in this simulation step</summary>
		private double intervalRuleAmount;

		/// <summary> [0 ; 1] availability factor that will be applied to every Consume() call in the next simulation step</summary>
		public double AvailabilityFactor => availabilityFactor;
		private double availabilityFactor = 1.0;

		/// <summary> true if all critical Consume() calls have been satisfied in the last sim step</summary>
		public bool CriticalConsumptionSatisfied { get; private set; }

		private double consumeRequests;
		private double consumeCriticalRequests;
		private double produceRequests;

		/// <summary>Dictionary of all consumers and producers (key) and how much amount they did add/remove (value).</summary>
		private Dictionary<ResourceBroker, double> brokersResourceAmounts;

		/// <summary>Dictionary of all interval-based rules (key) and their simulated average rate (value). This is for information only, the resource is not consumed</summary>
		private Dictionary<ResourceBroker, double> intervalRuleBrokersRates;

		public List<ResourceBrokerRate> ResourceBrokers => resourceBrokers;  List<ResourceBrokerRate>resourceBrokers;

		/// <summary>Ctor</summary>
		public VesselResource(ResourceWrapper resourceWrapper)
		{
			this.resourceWrapper = resourceWrapper;
			resourceBrokers = new List<ResourceBrokerRate>();
			brokersResourceAmounts = new Dictionary<ResourceBroker, double>();
			intervalRuleBrokersRates = new Dictionary<ResourceBroker, double>();
		}

		public void SetWrapper(ResourceWrapper resourceWrapper)
		{
			this.resourceWrapper = resourceWrapper;
		}

		public void Init()
		{
			deferred = 0.0;
			deferredNonCriticalConsumers = 0.0;
			consumeRequests = 0.0;
			consumeCriticalRequests = 0.0;
			produceRequests = 0.0;
			CriticalConsumptionSatisfied = true;

			// calculate level
			level = resourceWrapper.capacity > 0.0 ? resourceWrapper.amount / resourceWrapper.capacity : 0.0;
		}

		/// <summary>Record a production, it will be stored in "Deferred" and later synchronized to the vessel in Sync()</summary>
		/// <param name="broker">origin of the production, will be available in the UI</param>
		public void Produce(double quantity, ResourceBroker broker = null)
		{
			if (quantity == 0.0)
				return;

			produceRequests += quantity;
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
		public void Consume(double quantity, ResourceBroker broker = null, bool isCritial = false)
		{
			if (quantity == 0.0)
				return;

			if (isCritial)
			{
				consumeCriticalRequests += quantity;
				deferred -= quantity;
			}
			else
			{
				consumeRequests += quantity;
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

		public void RecipeConsume(double quantity, ResourceBroker broker)
		{
			if (quantity == 0.0)
				return;

			consumeCriticalRequests += quantity;
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
		public bool ExecuteAndSyncToParts(double elapsed_s)
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
			CriticalConsumptionSatisfied = resourceWrapper.amount + produceRequests >= consumeCriticalRequests;
			consumeRequests += consumeCriticalRequests;
			consumeCriticalRequests = 0.0;

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

			// calculate the resource "starvation" : how much of the consume requests can't be satisfied
			double starvation = Math.Abs(Math.Min(resourceWrapper.amount + produceRequests - consumeRequests, 0.0));
			availabilityFactor = consumeRequests > 0.0 ? Math.Max(1.0 - (starvation / consumeRequests), 0.0) : 1.0;
			produceRequests = 0.0;
			consumeRequests = 0.0;

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
			if (!Kerbalism.WarpBlending) rate = (resourceWrapper.amount - resourceWrapper.oldAmount - intervalRuleAmount) / elapsed_s;

			// calculate average rate of change per-second from interval-based rules
			intervalRulesRate = 0.0;
			foreach (var rb in intervalRuleBrokersRates)
			{
				intervalRulesRate += rb.Value;
			}

			// AverageRate is the exposed property that include simulated rate from interval-based rules.
			// For consistency with how "Rate" is calculated, we only add the simulated rate if there is some capacity or amount for it to have an effect
			averageRate = rate;
			if ((intervalRulesRate > 0.0 && level < 1.0) || (intervalRulesRate < 0.0 && level > 0.0)) averageRate += intervalRulesRate;

			// For visualization purpose, update the brokers list, merging all detected sources :
			// - normal brokers that use Consume() or Produce()
			// - "virtual" brokers from interval-based rules
			// - non-Kerbalism brokers (aggregated rate)
			UpdateResourceBrokers(brokersResourceAmounts, intervalRuleBrokersRates, unknownBrokersRate, elapsed_s);

			// reset amount added/removed from interval-based rules
			intervalRuleHappened = intervalRuleAmount > 0.0;
			intervalRuleAmount = 0.0;

			// reset brokers
			brokersResourceAmounts.Clear();
			intervalRuleBrokersRates.Clear();

			// reset amount added/removed from interval-based rules
			intervalRuleAmount = 0.0;

			// if incoherent producers are detected, do not allow high timewarp speed
			// - can be disabled in settings
			// - ignore incoherent consumers (no negative consequences for player)
			// - ignore flow state changes (avoid issue with process controllers and other things that alter resource capacities)
			return Settings.EnforceCoherency && TimeWarp.CurrentRate > 1000.0 && unknownBrokersRate > 0.0 && !flowStateChanged;
		}

		/// <summary>estimate time until depletion, including the simulated rate from interval-based rules</summary>
		public double DepletionTime()
		{
			// return depletion
			return resourceWrapper.amount <= 1e-10 ? 0.0 : averageRate >= -1e-10 ? double.PositiveInfinity : resourceWrapper.amount / -averageRate;
		}

		/// <summary>Inform that meal has happened in this simulation step</summary>
		/// <remarks>A simulation step can cover many physics ticks, especially for unloaded vessels</remarks>
		public void UpdateIntervalRule(double amount, double averageRate, ResourceBroker broker)
		{
			intervalRuleAmount += amount;
			intervalRulesRate += averageRate;

			if (intervalRuleBrokersRates.ContainsKey(broker))
				intervalRuleBrokersRates[broker] += averageRate;
			else
				intervalRuleBrokersRates.Add(broker, averageRate);
		}

		public void UpdateResourceBrokers(Dictionary<ResourceBroker, double> brokersResAmount, Dictionary<ResourceBroker, double> ruleBrokersRate, double unknownBrokersRate, double elapsedSeconds)
		{
			ResourceBrokers.Clear();

			foreach (KeyValuePair<ResourceBroker, double> p in ruleBrokersRate)
			{
				ResourceBroker broker = ResourceBroker.GetOrCreate(p.Key.Id + "Avg", p.Key.Category, Lib.BuildString(p.Key.Title, " (", Local.Generic_AVERAGE, ")"));
				ResourceBrokers.Add(new ResourceBrokerRate(broker, p.Value));
			}

			foreach (KeyValuePair<ResourceBroker, double> p in brokersResAmount)
			{
				ResourceBrokers.Add(new ResourceBrokerRate(p.Key, p.Value / elapsedSeconds));
			}

			if (unknownBrokersRate != 0.0)
			{
				ResourceBrokers.Add(new ResourceBrokerRate(ResourceBroker.Generic, unknownBrokersRate / elapsedSeconds));
			}
		}
	}
}
