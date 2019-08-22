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

		/// <summary> Simulated average rate of interval-based rules in amount per-second. This is for information only, the resource is not consumed</summary>
		private double intervalRulesRate;

		/// <summary> Amount consumed/produced by interval-based rules in this simulation step</summary>
		private double intervalRuleAmount;

		/// <summary>Dictionary of all consumers and producers (key) and how much amount they did add/remove (value).</summary>
		private Dictionary<string, double> brokersResourceAmounts;

		/// <summary>Dictionary of all interval-based rules (key) and their simulated average rate (value). This is for information only, the resource is not consumed</summary>
		private Dictionary<string, double> intervalRuleBrokersRates;

		/// <summary>Ctor</summary>
		public VesselResource(Vessel v, string res_name)
		{
			// remember resource name
			Name = res_name;

			Deferred = 0;
			Amount = 0;
			Capacity = 0;

			brokersResourceAmounts = new Dictionary<string, double>();
			intervalRuleBrokersRates = new Dictionary<string, double>();

			// get amount & capacity
			if (v.loaded)
			{
				foreach (Part p in v.Parts)
				{
					foreach (PartResource r in p.Resources)
					{
						if (r.resourceName == Name)
						{
							if (r.flowState) // has the user chosen to make a flowable resource flow
							{
								Amount += r.amount;
								Capacity += r.maxAmount;
							}
						}
#if DEBUG_RESOURCES
						// Force view all resource in Debug Mode
						r.isVisible = true;
#endif
					}
				}
			}
			else
			{
				foreach (ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
				{
					foreach (ProtoPartResourceSnapshot r in p.resources)
					{
						if (r.flowState && r.resourceName == Name)
						{
							if (r.flowState) // has the user chosen to make a flowable resource flow
							{
								Amount += r.amount;
								Capacity += r.maxAmount;
							}
						}
					}
				}
			}

			// calculate level
			Level = Capacity > 0.0 ? Amount / Capacity : 0.0;
		}

		/// <summary>Record a production, it will be stored in "Deferred" and later synchronized to the vessel in Sync()</summary>
		/// <param name="brokerName">origin of the production, will be available in the UI</param>
		public void Produce(double quantity, string brokerName)
		{
			Deferred += quantity;

			// keep track of every producer contribution for UI/debug purposes
			if (Math.Abs(quantity) < 1e-10) return;

			if (brokersResourceAmounts.ContainsKey(brokerName))
				brokersResourceAmounts[brokerName] += quantity;
			else
				brokersResourceAmounts.Add(brokerName, quantity);
		}

		/// <summary>Record a consumption, it will be stored in "Deferred" and later synchronized to the vessel in Sync()</summary>
		/// <param name="brokerName">origin of the consumption, will be available in the UI</param>
		public void Consume(double quantity, string brokerName)
		{
			Deferred -= quantity;

			// keep track of every consumer contribution for UI/debug purposes
			if (Math.Abs(quantity) < 1e-10) return;

			if (brokersResourceAmounts.ContainsKey(brokerName))
				brokersResourceAmounts[brokerName] -= quantity;
			else
				brokersResourceAmounts.Add(brokerName, -quantity);
		}

		/// <summary>synchronize resources from cache to vessel</summary>
		/// <remarks>
		/// this function will also sync from vessel to cache so you can always use the
		/// VesselResource properties to get information about resources
		/// </remarks>
		public void Sync(Vessel v, VesselData vd, double elapsed_s)
		{
			// # OVERVIEW
			// - consumption/production is accumulated in "Deferred", then this function called
			// - save previous step amount/capacity
			// - part loop 1 : detect new amount/capacity
			// - if amount has changed, this mean there is non-Kerbalism producers/consumers on the vessel
			// - if non-Kerbalism producers are detected on a loaded vessel, prevent high timewarp rates
			// - clamp "Deferred" to amount/capacity
			// - part loop 2 : apply "Deferred" to all parts
			// - apply "Deferred" to amount
			// - calculate change rate per-second
			// - calculate resource level
			// - reset deferred

			// # NOTE
			// It is impossible to guarantee coherency in resource simulation of loaded vessels,
			// if consumers/producers external to the resource cache exist in the vessel (#96).
			// Such is the case for example on loaded vessels with stock solar panels.
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

			if (v.loaded)
			{
				foreach (Part p in v.Parts)
				{
					foreach (PartResource r in p.Resources)
					{
						if (r.flowState && r.resourceName == Name)
						{
							Amount += r.amount;
							Capacity += r.maxAmount;
						}
					}
				}
			}
			else
			{
				foreach (ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
				{
					foreach (ProtoPartResourceSnapshot r in p.resources)
					{
						if (r.flowState && r.resourceName == Name)
						{
							Amount += r.amount;
							Capacity += r.maxAmount;
						}
					}
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

			// apply deferred consumption/production to all parts, simulating ALL_VESSEL_BALANCED
			// - iterating again is faster than using a temporary list of valid PartResources
			// - avoid very small values in deferred consumption/production
			if (Math.Abs(Deferred) > 1e-10)
			{
				if (v.loaded)
				{
					foreach (Part p in v.parts)
					{
						foreach (PartResource r in p.Resources)
						{
							if (r.flowState && r.resourceName == Name)
							{
								// calculate consumption/production coefficient for the part
								double k = Deferred < 0.0
								  ? r.amount / Amount
								  : (r.maxAmount - r.amount) / (Capacity - Amount);

								// apply deferred consumption/production
								r.amount += Deferred * k;
							}
						}
					}
				}
				else
				{
					foreach (ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
					{
						foreach (ProtoPartResourceSnapshot r in p.resources)
						{
							if (r.flowState && r.resourceName == Name)
							{
								// calculate consumption/production coefficient for the part
								double k = Deferred < 0.0
								  ? r.amount / Amount
								  : (r.maxAmount - r.amount) / (Capacity - Amount);

								// apply deferred consumption/production
								r.amount += Deferred * k;
							}
						}
					}
				}
			}

			// update amount, to get correct rate and levels at all times
			Amount += Deferred;

			// reset deferred production/consumption
			Deferred = 0.0;

			// recalculate level
			Level = Capacity > 0.0 ? Amount / Capacity : 0.0;

			// calculate rate of change per-second
			// - don't update rate during warp blending (stock modules have instabilities during warp blending) 
			// - ignore interval-based rules consumption/production
			if (!v.loaded || !Kerbalism.WarpBlending) Rate = (Amount - oldAmount - intervalRuleAmount) / elapsed_s;

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

			// For visualization purpose, update the VesselData.supplies brokers list, merging all detected sources :
			// - normal brokers that use Consume() or Produce()
			// - "virtual" brokers from interval-based rules
			// - non-Kerbalism brokers (aggregated rate)
			vd.Supply(Name).UpdateResourceBrokers(brokersResourceAmounts, intervalRuleBrokersRates, unsupportedBrokersRate, elapsed_s);

			if (PreferencesLifeSupport.Instance.resourceLogging)
			{
				Lib.Log("RESOURCE UPDATE : " + v);
				foreach (var rb in vd.Supply(Name).ResourceBrokers)
					Lib.Log(Lib.BuildString(Name, " : ", rb.rate.ToString("+0.000000;-0.000000;+0.000000"), "/s (", rb.name, ")"));
				Lib.Log("RESOURCE UPDATE END");
			}

			// reset amount added/removed from interval-based rules
			IntervalRuleHappened = intervalRuleAmount > 0.0;
			intervalRuleAmount = 0.0;

			// if incoherent producers are detected, do not allow high timewarp speed
			// - can be disabled in settings
			// - unloaded vessels can't be incoherent, we are in full control there
			// - ignore incoherent consumers (no negative consequences for player)
			// - ignore flow state changes (avoid issue with process controllers and other things) 
			if (Settings.EnforceCoherency && v.loaded && TimeWarp.CurrentRate > 1000.0 && unsupportedBrokersRate > 0.0 && !flowStateChanged)
			{
				Message.Post
				(
				  Severity.warning,
				  Lib.BuildString
				  (
					!v.isActiveVessel ? Lib.BuildString("On <b>", v.vesselName, "</b>\na ") : "A ",
					"producer of <b>", Name, "</b> has\n",
					"incoherent behavior at high warp speed.\n",
					"<i>Unload the vessel before warping</i>"
				  )
				);
				Lib.StopWarp(5);
			}

			// reset brokers
			brokersResourceAmounts.Clear();
			intervalRuleBrokersRates.Clear();

			// reset amount added/removed from interval-based rules
			intervalRuleAmount = 0.0;
		}

		/// <summary>estimate time until depletion, including the simulated rate from interval-based rules</summary>
		public double DepletionTime()
		{
			// return depletion
			return Amount <= 1e-10 ? 0.0 : AverageRate >= -1e-10 ? double.NaN : Amount / -AverageRate;
		}

		/// <summary>Inform that meal has happened in this simulation step</summary>
		/// <remarks>A simulation step can cover many physics ticks, especially for unloaded vessels</remarks>
		public void UpdateIntervalRule(double amount, double averageRate, string ruleName)
		{
			intervalRuleAmount += amount;
			intervalRulesRate += averageRate;

			if (intervalRuleBrokersRates.ContainsKey(ruleName))
				intervalRuleBrokersRates[ruleName] += averageRate;
			else
				intervalRuleBrokersRates.Add(ruleName, averageRate);
		}
	}
}
