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

		/// <summary> Amount of resource</summary>
		public abstract double Amount { get; }

		/// <summary> Storage capacity of resource</summary>
		public abstract double Capacity { get; }

		/// <summary> Not yet consumed or produced amount that will be synchronized to the vessel parts in Sync()</summary>
		public abstract double Deferred { get; }

		/// <summary> Rate of change in amount per-second, this is purely for visualization</summary>
		public abstract double Rate { get; }

		/// <summary> Amount vs capacity, or 0 if there is no capacity</summary>
		public abstract double Level { get; }

		/// <summary> [0 ; 1] availability factor that will be applied to every Consume() call in the next simulation step</summary>
		public abstract double AvailabilityFactor { get; }

		/// <summary> last step consumption requests. For visualization only, can be greater than what was actually consumed </summary>
		public abstract double ConsumeRequests { get; }

		/// <summary> last step production requests. For visualization only, can be greater than what was actually produced </summary>
		public abstract double ProduceRequests { get; }

		/// <summary> list of consumers and producers for this resource</summary>
		public abstract List<ResourceBrokerRate> ResourceBrokers { get; }

		public abstract bool NeedUpdate { get; }

		/// <summary> Called at the VesselResHandler instantiation, after the ResourceWrapper amount and capacity has been evaluated </summary>
		public virtual void Init() { }

		/// <summary> Called by the VesselResHandler, every update :
		/// <para/> - After Recipes have been processed
		/// <para/> - After the VesselResHandler has been updated with all part resources references, and after amount/oldAmount and capacity/oldCapacity have been set
		/// </summary>
		public abstract bool ExecuteAndSyncToParts(double elapsed_s);

		/// <summary>Record a consumption, it will be stored in "Deferred" and later synchronized to the vessel in ExecuteAndSyncToParts()</summary>
		/// <param name="broker">origin of the consumption, will be available in the UI</param>
		public abstract void Consume(double quantity, ResourceBroker broker = null, bool isCritical = false);

		/// <summary>Record a consumption, it will be stored in "Deferred" and later synchronized to the vessel in ExecuteAndSyncToParts()</summary>
		/// <param name="broker">origin of the consumption, will be available in the UI</param>
		public abstract void RecipeConsume(double quantity, ResourceBroker broker);

		/// <summary>Record a production, it will be stored in "Deferred" and later synchronized to the vessel in ExecuteAndSyncToParts()</summary>
		/// <param name="broker">origin of the production, will be available in the UI</param>
		public abstract void Produce(double quantity, ResourceBroker broker = null);

		/// <summary>estimate time until depletion</summary>
		public double Depletion => Rate >= -1e-10 ? double.PositiveInfinity : Amount / -Rate;

		public string DepletionInfo => Amount <= 1e-10 ? Local.Monitor_depleted : Lib.HumanReadableDuration(Depletion);

		public override string ToString()
		{
			return $"{Name} : {Lib.HumanReadableStorage(Amount, Capacity)} ({Rate.ToString("+0.#######/s;-0.#######/s")})";
		}

		public string BrokersListTooltip()
		{
			sb.Length = 0;

			sb.Append(Lib.Color(Title, Lib.Kolor.Yellow, true));
			sb.Append("\n");
			sb.Append("<align=left />");
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

			if (ResourceBrokers.Count > 0)
			{
				sb.Append("\n<b>------------    \t------------</b>");
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

		public string BrokerListTooltipTMP()
		{
			sb.Length = 0;

			sb.Append(Lib.Color(Title, Lib.Kolor.Yellow, true));
			sb.Append("\n");
			sb.Append("<align=\"left\">");
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

			if (ResourceBrokers.Count > 0)
			{
				sb.Append("\n<b>------------<pos=80px>------------</b>");
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
