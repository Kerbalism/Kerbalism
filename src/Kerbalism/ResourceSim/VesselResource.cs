using System;
using System.Collections.Generic;
using System.Text;

namespace KERBALISM
{
	/// <summary>
	/// Interface for common interactions with VesselResource and VirtualResource.
	/// You can cast this to VesselResource to get the extra information it contains (rates...)
	/// </summary>
	public abstract class VesselResource
	{
		private static StringBuilder sb = new StringBuilder();

		/// <summary> Technical name</summary>
		public abstract string Name { get; }

		/// <summary> UI friendly name</summary>
		public abstract string Title { get; }

		/// <summary> Visibility of resource</summary>
		public abstract bool Visible { get; }

		/// <summary> Amount of resource</summary>
		public abstract double Amount { get; }

		/// <summary> Storage capacity of resource</summary>
		public abstract double Capacity { get; }

		public abstract bool NeedUpdate { get; }

		/// <summary> Rate of change in amount per-second, this is purely for visualization</summary>
		public double Rate => rate;
		protected double rate;

		/// <summary> Amount vs capacity, or 0 if there is no capacity</summary>
		public double Level => level;
		protected double level;

		/// <summary> last step consumption requests. For visualization only, can be greater than what was actually consumed </summary>
		public double ConsumeRequests => consumeRequests;
		protected double consumeRequests;

		/// <summary> last step production requests. For visualization only, can be greater than what was actually produced </summary>
		public double ProduceRequests => produceRequests;
		protected double produceRequests;

		/// <summary> true if all critical Consume() calls have been satisfied in the last sim step</summary>
		public bool CriticalConsumptionSatisfied => criticalConsumptionSatisfied;
		protected bool criticalConsumptionSatisfied;

		/// <summary> list of consumers and producers for this resource</summary>
		public List<ResourceBrokerRate> ResourceBrokers => resourceBrokers;
		protected List<ResourceBrokerRate> resourceBrokers;

		/// <summary> If enabled, the total resource amount will be redistributed evenly amongst all parts. Reset itself to "NotSet" after every ExecuteAndSyncToParts() call</summary>
		public EqualizeMode equalizeMode = EqualizeMode.NotSet;
		public enum EqualizeMode { NotSet, Enabled, Disabled }

		/// <summary> Not yet consumed or produced amount that will be synchronized to the vessel parts in Sync()</summary>
		public virtual double Deferred => deferred;
		protected double deferred;
		protected double deferredNonCriticalConsumers;

		/// <summary> [0 ; 1] availability factor that will be applied to every Consume() call in the next simulation step</summary>
		public virtual double AvailabilityFactor => availabilityFactor;
		protected double availabilityFactor = 0.0;

		protected double currentConsumeRequests;
		protected double currentConsumeCriticalRequests;
		protected double currentProduceRequests;

		public double UnknownBrokersRate => unknownBrokersRate;
		protected double unknownBrokersRate;

		/// <summary>Dictionary of all consumers and producers (key) and how much amount they did add/remove (value).</summary>
		protected Dictionary<ResourceBroker, double> brokersResourceAmounts;

		protected ResourceWrapper resourceWrapper;

		/// <summary> Called at the VesselResHandler instantiation, after the ResourceWrapper amount and capacity has been evaluated </summary>
		public virtual void Init()
		{
			deferred = 0.0;
			deferredNonCriticalConsumers = 0.0;
			currentConsumeRequests = 0.0;
			currentConsumeCriticalRequests = 0.0;
			currentProduceRequests = 0.0;
			criticalConsumptionSatisfied = true;

			// calculate level
			level = resourceWrapper.capacity > 0.0 ? resourceWrapper.amount / resourceWrapper.capacity : 0.0;
		}

		/// <summary> Called by the VesselResHandler, every update :
		/// <para/> - After Recipes have been processed
		/// <para/> - After the VesselResHandler has been updated with all part resources references, and after amount/oldAmount and capacity/oldCapacity have been set
		/// </summary>
		public virtual bool ExecuteAndSyncToParts(VesselDataBase vd, double elapsed_s)
		{
			// As we haven't yet synchronized anything, changes to amount can only come from non-Kerbalism producers or consumers
			unknownBrokersRate = resourceWrapper.amount - resourceWrapper.oldAmount;
			// Avoid false detection due to precision errors
			if (Math.Abs(unknownBrokersRate) < 1e-05) unknownBrokersRate = 0.0;

			// critical consumers are satisfied if there is enough produced + stored.
			// we are sure of that because Recipes are processed after critical Consume() calls,
			// and they will not underflow (consume more than what is available in amount + deferred)
			// and non critical consumers have been isolated in deferredNonCriticalConsumers
			criticalConsumptionSatisfied = resourceWrapper.amount + currentProduceRequests >= currentConsumeCriticalRequests;
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

			return false;
		}

		/// <summary>
		/// Record a consumption, it will be stored in "Deferred" and later synchronized to the vessel in ExecuteAndSyncToParts()
		/// <para/>IMPORTANT : quantity should NEVER be scaled by the AvailabilityFactor,
		/// and you should avoid to use a "if resource amount == 0, don't consume" logic.
		/// <para/> Instead, always consume the resource, and scale or disable the effect based on AvailabilityFactor.
		/// </summary>
		/// <param name="quantity">amount to consume. This should always be scaled by the timestep (elapsed_s)</param>
		/// <param name="broker">source of the consumption, for UI purposes</param>
		/// <param name="isCritical">
		/// if false, scale the consumption by the resource AvailabilityFactor. If false, don't scale it by AvailabilityFactor.
		/// <para/>You can know if all critical Consume() calls for this resource have been satified in last step by checking the CriticalConsumptionSatisfied property
		/// </param>
		public virtual void Consume(double quantity, ResourceBroker broker = null, bool isCritical = false)
		{
			if (quantity == 0.0)
				return;

			if (isCritical)
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

		/// <summary>Record a consumption, it will be stored in "Deferred" and later synchronized to the vessel in ExecuteAndSyncToParts()</summary>
		/// <param name="broker">origin of the consumption, will be available in the UI</param>
		public virtual void RecipeConsume(double quantity, ResourceBroker broker)
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

		/// <summary>Record a production, it will be stored in "Deferred" and later synchronized to the vessel in ExecuteAndSyncToParts()</summary>
		/// <param name="broker">origin of the production, will be available in the UI</param>
		public virtual void Produce(double quantity, ResourceBroker broker = null)
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

		/// <summary>estimate time until depletion</summary>
		public double Depletion => Amount <= 1e-10 ? 0.0 : Rate >= -1e-10 ? double.PositiveInfinity : Amount / -Rate;

		public string DepletionInfo => Amount <= 1e-10 ? Local.Monitor_depleted : Lib.HumanReadableDuration(Depletion);

		public override string ToString()
		{
			return $"{Name} : {Lib.HumanReadableStorage(Amount, Capacity)} ({Rate.ToString("+0.#######/s;-0.#######/s")})";
		}

		public string BrokersListTooltip(bool showSummary = true)
		{
			sb.Length = 0;

			sb.Append(Lib.Color(Title, Lib.Kolor.Yellow, true));
			sb.Append("<align=left />");
			if (showSummary)
			{
				sb.Append("\n");
				if (Rate != 0.0)
				{
					sb.Append(Lib.Color(Rate > 0.0,
						Lib.BuildString("+", Lib.HumanReadableRate(Math.Abs(Rate))), Lib.Kolor.PosRate,
						Lib.BuildString("-", Lib.HumanReadableRate(Math.Abs(Rate))), Lib.Kolor.NegRate,
						true));
				}
				else
				{
					sb.Append("<b>");
					sb.Append(Local.TELEMETRY_nochange);//no change
					sb.Append("</b>");
				}

				if (Rate < 0.0 && Level < 0.0001)
				{
					sb.Append(" <i>");
					sb.Append(Local.TELEMETRY_empty);//(empty)
					sb.Append("</i>");
				}
				else if (Rate > 0.0 && Level > 0.9999)
				{
					sb.Append(" <i>");
					sb.Append(Local.TELEMETRY_full);//(full)
					sb.Append("</i>");

				}
				else sb.Append("   "); // spaces to prevent alignement issues

				sb.Append("\t");
				sb.Append(Lib.HumanReadableStorage(Amount, Capacity));
				sb.Append(" (");
				sb.Append(Level.ToString("P0"));
				sb.Append(")");
			}


			if (ResourceBrokers.Count > 0)
			{
				if (showSummary)
				{
					sb.Append("\n<b>------------    \t------------</b>");
				}
				foreach (ResourceBrokerRate rb in ResourceBrokers)
				{
					// exclude very tiny rates to avoid the ui flickering
					if (rb.rate > -1e-09 && rb.rate < 1e-09)
						continue;

					sb.Append("\n");
					sb.Append(Lib.Color(rb.rate > 0.0,
						Lib.BuildString("+", Lib.HumanReadableRate(Math.Abs(rb.rate)), "   "), Lib.Kolor.PosRate, // spaces to mitigate alignement issues
						Lib.BuildString("-", Lib.HumanReadableRate(Math.Abs(rb.rate)), "   "), Lib.Kolor.NegRate, // spaces to mitigate alignement issues
						true));
					sb.Append("\t");
					sb.Append(rb.broker.Title);
				}
			}

			return sb.ToString();
		}

		public string BrokerListTooltipTMP(bool showSummary = true)
		{
			sb.Length = 0;

			sb.Append(Lib.Color(Title, Lib.Kolor.Yellow, true));
			sb.Append("<align=\"left\">");

			if (showSummary)
			{
				sb.Append("\n");
				if (Rate != 0.0)
				{
					sb.Append(Lib.Color(Rate > 0.0,
						Lib.HumanReadableRate(Rate, "F3", "", true), Lib.Kolor.PosRate,
						Lib.HumanReadableRate(Rate, "F3", "", true), Lib.Kolor.NegRate,
						true));
				}
				else
				{
					sb.Append("<b>");
					sb.Append(Local.TELEMETRY_nochange);//no change
					sb.Append("</b>");
				}

				sb.Append("<pos=80px>");
				sb.Append(Lib.HumanReadableStorage(Amount, Capacity));
				sb.Append(" (");
				sb.Append(Level.ToString("P0"));
				sb.Append(")");
			}

			if (ResourceBrokers.Count > 0)
			{
				if (showSummary)
				{
					sb.Append("\n<b>------------<pos=80px>------------</b>");
				}
				
				foreach (ResourceBrokerRate rb in ResourceBrokers)
				{
					// exclude very tiny rates to avoid the ui flickering
					if (rb.rate > -1e-09 && rb.rate < 1e-09)
						continue;

					sb.Append("\n");
					sb.Append(Lib.Color(rb.rate > 0.0,
						Lib.HumanReadableRate(rb.rate, "F3", "", true), Lib.Kolor.PosRate,
						Lib.HumanReadableRate(rb.rate, "F3", "", true), Lib.Kolor.NegRate,
						true));
					sb.Append("<pos=80px>");
					sb.Append(rb.broker.Title);
				}
			}

			return sb.ToString();
		}

	}
}
