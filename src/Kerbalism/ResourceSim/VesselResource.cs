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
		public string Name { get; private set; }

		/// <summary> Rate of change in amount per-second, this is purely for visualization</summary>
		public double Rate { get; private set; }

		/// <summary> Rate of change in amount per-second, including average rate for interval-based rules</summary>
		public double AverageRate { get; private set; }

		/// <summary> Amount vs capacity, or 0 if there is no capacity</summary>
		public double Level { get; private set; }

		/// <summary> True if an interval-based rule consumption/production was processed in the last simulation step</summary>
		public bool IntervalRuleHappened { get; private set; }

		/// <summary> Not yet consumed or produced amount that will be synchronized to the vessel parts in Sync()</summary>
		public double Deferred { get; private set; }

		/// <summary> Amount of resource</summary>
		public double Amount { get; private set; }

		/// <summary> Storage capacity of resource</summary>
		public double Capacity { get; private set; }

		/// <summary> If enabled, the total resource amount will be redistributed evenly amongst all parts. Reset itself to "NotSet" after every ExecuteAndSyncToParts() call</summary>
		public EqualizeMode equalizeMode = EqualizeMode.NotSet;
		public enum EqualizeMode { NotSet, Enabled, Disabled }

		/// <summary> Simulated average rate of interval-based rules in amount per-second. This is for information only, the resource is not consumed</summary>
		private double intervalRulesRate;

		/// <summary> Amount consumed/produced by interval-based rules in this simulation step</summary>
		private double intervalRuleAmount;

		/// <summary>Dictionary of all consumers and producers (key) and how much amount they did add/remove (value).</summary>
		private Dictionary<ResourceBroker, double> brokersResourceAmounts;

		/// <summary>Dictionary of all interval-based rules (key) and their simulated average rate (value). This is for information only, the resource is not consumed</summary>
		private Dictionary<ResourceBroker, double> intervalRuleBrokersRates;

		public List<ResourceBrokerRate> ResourceBrokers { get; private set; }

		/// <summary>Ctor</summary>
		public VesselResource(string res_name)
		{
			Name = res_name;
			ResourceBrokers = new List<ResourceBrokerRate>();
			brokersResourceAmounts = new Dictionary<ResourceBroker, double>();
			intervalRuleBrokersRates = new Dictionary<ResourceBroker, double>();
		}

		public void InitAmounts(List<ResourceWrapper> partResources)
		{
			Amount = 0;
			Capacity = 0;
			Deferred = 0;

			// get amount & capacity
			foreach (ResourceWrapper partResource in partResources)
			{
				if (partResource.FlowState) // has the user chosen to make a flowable resource flow
				{
					Amount += partResource.Amount;
					Capacity += partResource.Capacity;
				}
			}

			// calculate level
			Level = Capacity > 0.0 ? Amount / Capacity : 0.0;
		}

		/// <summary>Record a production, it will be stored in "Deferred" and later synchronized to the vessel in Sync()</summary>
		/// <param name="broker">origin of the production, will be available in the UI</param>
		public void Produce(double quantity, ResourceBroker broker = null)
		{
			Deferred += quantity;

			// keep track of every producer contribution for UI/debug purposes
			if (Math.Abs(quantity) < 1e-10) return;

			if (broker == null)
				broker = ResourceBroker.Generic;

			if (brokersResourceAmounts.ContainsKey(broker))
				brokersResourceAmounts[broker] += quantity;
			else
				brokersResourceAmounts.Add(broker, quantity);
		}

		/// <summary>Record a consumption, it will be stored in "Deferred" and later synchronized to the vessel in Sync()</summary>
		/// <param name="broker">origin of the consumption, will be available in the UI</param>
		public void Consume(double quantity, ResourceBroker broker = null)
		{
			Deferred -= quantity;

			// keep track of every consumer contribution for UI/debug purposes
			if (Math.Abs(quantity) < 1e-10) return;

			if (broker == null)
				broker = ResourceBroker.Generic;

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
			Amount = 0.0;
			Capacity = 0.0;

			foreach (ResourceWrapper partResource in partResources)
			{
				if (partResource.FlowState) // has the user chosen to make a flowable resource flow
				{
					Amount += partResource.Amount;
					Capacity += partResource.Capacity;
				}
			}

			// As we haven't yet synchronized anything, changes to amount can only come from non-Kerbalism producers or consumers
			double unsupportedBrokersRate = Amount - oldAmount;
			// Avoid false detection due to precision errors
			if (Math.Abs(unsupportedBrokersRate) < 1e-05) unsupportedBrokersRate = 0.0;
			// Calculate the resulting rate
			unsupportedBrokersRate /= elapsed_s;

			// Detect flow state changes
			bool flowStateChanged = Capacity - oldCapacity > 1e-05;

			// clamp consumption/production to vessel amount/capacity
			// - if deferred is negative, then amount is guaranteed to be greater than zero
			// - if deferred is positive, then capacity - amount is guaranteed to be greater than zero
			Deferred = Lib.Clamp(Deferred, -Amount, Capacity - Amount);



			if (equalizeMode == EqualizeMode.Enabled)
			{
				// apply deferred consumption/production to all parts, equally balancing the total amount amongst all parts
				foreach (ResourceWrapper partResource in partResources)
				{
					if (partResource.FlowState) // has the user chosen to make a flowable resource flow
					{
						// apply deferred consumption/production
						partResource.Amount = (Amount + Deferred) * (partResource.Capacity / Capacity);
					}
				}
			}
			else
			{
				// apply deferred consumption/production to all parts, simulating ALL_VESSEL_BALANCED
				// avoid very small values in deferred consumption/production
				if (Math.Abs(Deferred) > 1e-10)
				{
					foreach (ResourceWrapper partResource in partResources)
					{
						if (partResource.FlowState) // has the user chosen to make a flowable resource flow
						{
							// calculate consumption/production coefficient for the part
							double k;
							if (Deferred < 0.0)
								k = partResource.Amount / Amount;
							else
								k = (partResource.Capacity - partResource.Amount) / (Capacity - Amount);

							// apply deferred consumption/production
							partResource.Amount += Deferred * k;
						}
					}
				}
			}

			equalizeMode = EqualizeMode.NotSet;

			// update amount, to get correct rate and levels at all times
			Amount += Deferred;

			// reset deferred production/consumption
			Deferred = 0.0;

			// recalculate level
			Level = Capacity > 0.0 ? Amount / Capacity : 0.0;

			// calculate rate of change per-second
			// - don't update rate during warp blending (stock modules have instabilities during warp blending) 
			// - ignore interval-based rules consumption/production
			if (!Kerbalism.WarpBlending) Rate = (Amount - oldAmount - intervalRuleAmount) / elapsed_s;

			// calculate average rate of change per-second from interval-based rules
			intervalRulesRate = 0.0;
			foreach (var rb in intervalRuleBrokersRates)
			{
				intervalRulesRate += rb.Value;
			}

			// AverageRate is the exposed property that include simulated rate from interval-based rules.
			// For consistency with how "Rate" is calculated, we only add the simulated rate if there is some capacity or amount for it to have an effect
			AverageRate = Rate;
			if ((intervalRulesRate > 0.0 && Level < 1.0) || (intervalRulesRate < 0.0 && Level > 0.0)) AverageRate += intervalRulesRate;

			// For visualization purpose, update the brokers list, merging all detected sources :
			// - normal brokers that use Consume() or Produce()
			// - "virtual" brokers from interval-based rules
			// - non-Kerbalism brokers (aggregated rate)
			UpdateResourceBrokers(brokersResourceAmounts, intervalRuleBrokersRates, unsupportedBrokersRate, elapsed_s);

			// reset amount added/removed from interval-based rules
			IntervalRuleHappened = intervalRuleAmount > 0.0;
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
			return Amount <= 1e-10 ? 0.0 : AverageRate >= -1e-10 ? double.NaN : Amount / -AverageRate;
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
				ResourceBrokers.Add(new ResourceBrokerRate(p.Key, p.Value / elapsedSeconds));
			}
			if (unsupportedBrokersRate != 0.0)
			{
				ResourceBrokers.Add(new ResourceBrokerRate(ResourceBroker.Generic, unsupportedBrokersRate));
			}
		}
	}
}
