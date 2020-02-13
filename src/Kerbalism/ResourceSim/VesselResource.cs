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
		/// <summary> Associated resource name</summary>
		public string Name => name; string name;

		/// <summary> Rate of change in amount per-second, this is purely for visualization</summary>
		public double Rate => rate; double rate;

		/// <summary> Rate of change in amount per-second, including average rate for interval-based rules</summary>
		public double AverageRate => averageRate; double averageRate;

		/// <summary> Amount vs capacity, or 0 if there is no capacity</summary>
		public double Level => level; double level;

		/// <summary> True if an interval-based rule consumption/production was processed in the last simulation step</summary>
		public bool IntervalRuleHappened => intervalRuleHappened; bool intervalRuleHappened;

		/// <summary> Not yet consumed or produced amount that will be synchronized to the vessel parts in Sync()</summary>
		public double Deferred => deferred; double deferred;
		private double deferredNonCriticalConsumers;

		/// <summary> Amount of resource</summary>
		public double Amount => amount; double amount;

		/// <summary> Storage capacity of resource</summary>
		public double Capacity => capacity; double capacity;

		/// <summary> If enabled, the total resource amount will be redistributed evenly amongst all parts. Reset itself to "NotSet" after every ExecuteAndSyncToParts() call</summary>
		public EqualizeMode equalizeMode = EqualizeMode.NotSet;
		public enum EqualizeMode { NotSet, Enabled, Disabled }

		/// <summary> Simulated average rate of interval-based rules in amount per-second. This is for information only, the resource is not consumed</summary>
		private double intervalRulesRate;

		/// <summary> Amount consumed/produced by interval-based rules in this simulation step</summary>
		private double intervalRuleAmount;

		public double AvailabilityFactor => availabilityFactor;
		private double availabilityFactor = 1.0;

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
		public VesselResource(string res_name)
		{
			name = res_name;
			resourceBrokers = new List<ResourceBrokerRate>();
			brokersResourceAmounts = new Dictionary<ResourceBroker, double>();
			intervalRuleBrokersRates = new Dictionary<ResourceBroker, double>();
		}

		public void InitAmounts(List<ResourceWrapper> partResources)
		{
			amount = 0.0;
			capacity = 0.0;
			deferred = 0.0;
			deferredNonCriticalConsumers = 0.0;
			consumeRequests = 0.0;
			consumeCriticalRequests = 0.0;
			produceRequests = 0.0;
			CriticalConsumptionSatisfied = true;

			// get amount & capacity
			foreach (ResourceWrapper partResource in partResources)
			{
				if (partResource.FlowState) // has the user chosen to make a flowable resource flow
				{
					amount += partResource.Amount;
					capacity += partResource.Capacity;
				}
			}

			// calculate level
			level = capacity > 0.0 ? amount / capacity : 0.0;
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
		/// Record a consumption, it will be stored in "Deferred" and later synchronized to the vessel in Sync()
		/// <para/>IMPORTANT : quantity should NOT be scaled to the resource amount / availability when considerAvailablility is true
		/// </summary>
		/// <param name="quantity">amount to consume. This should always be scaled by the timestep (elapsed_s)</param>
		/// <param name="broker">source of the consumption, for UI purposes</param>
		/// <param name="considerAvailablility">
		/// if true, scale the consumption by the resource AvailabilityFactor, and consider it to determine AvailabilityFactor
		/// this should always be true, excepted when called from a Recipe, as recipe inputs are already scaled by resource availability
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
		public bool ExecuteAndSyncToParts(double elapsed_s, List<ResourceWrapper> partResources = null)
		{
			// # OVERVIEW
			// - consumption/production is accumulated in "Deferred" from partmodules and other parts of Kerblism
			// - then this is called last
			// - save previous step amount/capacity
			// - part loop 1 : detect new amount/capacity
			// - if amount has changed, this mean there is non-Kerbalism producers/consumers on the vessel
			// - if non-Kerbalism producers are detected on a loaded vessel, prevent high timewarp rates
			// - clamp "Deferred" to amount/capacity
			// - part loop 2 : apply "Deferred" to all parts
			// - apply "Deferred" to amount
			// - calculate rate of change per-second
			// - calculate resource level
			// - reset deferred

			// # NOTE
			// It is impossible to guarantee coherency in resource simulation of loaded vessels,
			// if consumers/producers external to the resource cache exist in the vessel (#96).
			// The effect is that the whole resource simulation become dependent on timestep again.
			// From the user point-of-view, there are two cases:
			// - (A) the timestep-dependent error is smaller than capacity
			// - (B) the timestep-dependent error is bigger than capacity
			// In case [A], there are no consequences except a slightly wrong computed level and rate.
			// In case [B], the simulation became incoherent and from that point anything can happen,
			// like for example insta-death by co2 poisoning or climatization.
			// To avoid the consequences of [B]:
			// - we hacked the solar panels to use the resource cache (SolarPanelFixer)
			// - we detect incoherency on loaded vessels, and forbid the two highest warp speeds

			// remember vessel-wide amount currently known, to calculate rate and detect non-Kerbalism brokers
			double oldAmount = Amount;

			// remember vessel-wide capacity currently known, to detect flow state changes
			double oldCapacity = Capacity;

			// iterate over all enabled resource containers and detect amount/capacity again
			// - this detect production/consumption from stock and third-party mods
			//   that by-pass the resource cache, and flow state changes in general
			amount = 0.0;
			capacity = 0.0;

			foreach (ResourceWrapper partResource in partResources)
			{
				if (partResource.FlowState) // has the user chosen to make a flowable resource flow
				{
					amount += partResource.Amount;
					capacity += partResource.Capacity;
				}
			}

			CriticalConsumptionSatisfied = amount < consumeCriticalRequests ? amount + produceRequests >= consumeCriticalRequests : true;
			consumeRequests += consumeCriticalRequests;
			consumeCriticalRequests = 0.0;

			// deduce the [0 ; 1] availability factor that will be applied to every Consume() call in the next simulation step
			// The purpose of this is
			// - To stabilize the resource sim when consumption > production instead of having in a perpetual "0 / max / 0 / max" cycle
			// - To be able to scale the output of whatever the consumption request is for : comms data rate, experiment data rate,
			//   anything that is doing some action based on a resource consumption.

			// calculate the resource "starvation" : how much of the consume requests can't be satisfied
			double starvation = Math.Abs(Math.Min(amount + produceRequests - consumeRequests, 0.0));
			availabilityFactor = consumeRequests > 0.0 ? Math.Max(1.0 - (starvation / consumeRequests), 0.0) : 1.0;

			produceRequests = 0.0;
			consumeRequests = 0.0;

			// As we haven't yet synchronized anything, changes to amount can only come from non-Kerbalism producers or consumers
			double unsupportedBrokersRate = amount - oldAmount;
			// Avoid false detection due to precision errors
			if (Math.Abs(unsupportedBrokersRate) < 1e-05) unsupportedBrokersRate = 0.0;
			// Calculate the resulting rate
			unsupportedBrokersRate /= elapsed_s;

			// Detect flow state changes
			bool flowStateChanged = capacity - oldCapacity > 1e-05;

			deferred += deferredNonCriticalConsumers;
			deferredNonCriticalConsumers = 0.0;

			// clamp consumption/production to vessel amount/capacity
			// - if deferred is negative, then amount is guaranteed to be greater than zero
			// - if deferred is positive, then capacity - amount is guaranteed to be greater than zero
			deferred = Lib.Clamp(deferred, -amount, capacity - amount);

			if (capacity > 0.0)
			{
				if (equalizeMode == EqualizeMode.Enabled)
				{
					// apply deferred consumption/production to all parts, equally balancing the total amount amongst all parts
					foreach (ResourceWrapper partResource in partResources)
					{
						if (partResource.FlowState) // has the user chosen to make a flowable resource flow
						{
							// apply deferred consumption/production
							partResource.Amount = (amount + deferred) * (partResource.Capacity / capacity);
						}
					}
				}
				else
				{
					// apply deferred consumption/production to all parts, simulating ALL_VESSEL_BALANCED
					// avoid very small values in deferred consumption/production
					if (Math.Abs(deferred) > 1e-16)
					{
						foreach (ResourceWrapper partResource in partResources)
						{
							if (partResource.FlowState) // has the user chosen to make a flowable resource flow
							{
								// calculate consumption/production coefficient for the part
								double k;
								if (deferred < 0.0)
									k = partResource.Amount / amount;
								else
									k = (partResource.Capacity - partResource.Amount) / (capacity - amount);

								// apply deferred consumption/production
								partResource.Amount += deferred * k;
							}
						}
					}
				}
			}

			equalizeMode = EqualizeMode.NotSet;

			// update amount, to get correct rate and levels at all times
			amount += deferred;

			// reset deferred production/consumption
			deferred = 0.0;

			// recalculate level
			level = capacity > 0.0 ? amount / capacity : 0.0;

			// calculate rate of change per-second
			// - don't update rate during warp blending (stock modules have instabilities during warp blending) 
			// - ignore interval-based rules consumption/production
			if (!Kerbalism.WarpBlending) rate = (amount - oldAmount - intervalRuleAmount) / elapsed_s;

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
			UpdateResourceBrokers(brokersResourceAmounts, intervalRuleBrokersRates, unsupportedBrokersRate, elapsed_s);

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
			return Settings.EnforceCoherency && TimeWarp.CurrentRate > 1000.0 && unsupportedBrokersRate > 0.0 && !flowStateChanged;
		}

		/// <summary>estimate time until depletion, including the simulated rate from interval-based rules</summary>
		public double DepletionTime()
		{
			// return depletion
			return amount <= 1e-10 ? 0.0 : averageRate >= -1e-10 ? double.NaN : amount / -averageRate;
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

		public void UpdateResourceBrokers(Dictionary<ResourceBroker, double> brokersResAmount, Dictionary<ResourceBroker, double> ruleBrokersRate, double unsupportedBrokersRate, double elapsedSeconds)
		{
			ResourceBrokers.Clear();

			foreach (KeyValuePair<ResourceBroker, double> p in ruleBrokersRate)
			{
				ResourceBroker broker = ResourceBroker.GetOrCreate(p.Key.Id + "Avg", p.Key.Category, Lib.BuildString(p.Key.Title, " (", Local.Generic_AVERAGE, ")"));
				ResourceBrokers.Add(new ResourceBrokerRate(broker, p.Value));
			}

			foreach (KeyValuePair<ResourceBroker, double> p in brokersResAmount)
			{
				// note : we that here and not directly in the produce/consume methods because
				// recipes do a lot of tiny increments for the the same broker
				if (p.Value > -1e-07 && p.Value < 1e-07)
					continue;

				ResourceBrokers.Add(new ResourceBrokerRate(p.Key, p.Value / elapsedSeconds));
			}

			if (unsupportedBrokersRate != 0.0)
			{
				ResourceBrokers.Add(new ResourceBrokerRate(ResourceBroker.Generic, unsupportedBrokersRate));
			}
		}
	}
}
