using System;
using System.Collections.Generic;

namespace KERBALISM
{
	/// <summary> signal connection link status </summary>
	public enum LinkStatus
	{
		direct_link = 0,
		indirect_link = 1,  // relayed signal
		no_link = 2,
		plasma = 3,         // plasma blackout on reentry
		storm = 4           // cme storm blackout
	}

	public sealed class ConnectionInfo : IConnectionInfo
	{
		// Note : Do not change this class, it is used for the API handlers (as of 02-2020, RealAntenna is using it)
		// That's also why it doesn't use the above enum directly

		public LinkStatus Status
		{
			get => (LinkStatus)status;
			set => status = (int)value;
		}

		public bool HasActiveAntenna => hasActiveAntenna;

		public bool Linked => linked;

		public double Ec => ec;

		public double EcIdle => ec_idle;

		public double DataRate => rate;

		public double Strength => strength;


		// ====================================================================
		// VALUES SET BY KERBALISM (CommInfo API )
		// ====================================================================

		/// <summary>
		/// This will be set to true if the vessel currently is transmitting data.
		/// </summary>
		public bool transmitting = false;

		/// <summary>
		/// Set to true if the vessel is currently subjected to a CME storm
		/// </summary>
		public bool storm = false;

		/// <summary>
		/// Set to true if the vessel has enough EC to operate
		/// </summary>
		public bool powered = true;


		// ====================================================================
		// VALUES TO SET FOR KERBALISM (CommInfo API)
		// ====================================================================

		public bool hasActiveAntenna = false;

		/// <summary>
		/// science data rate, in MB/s. note that internal transmitters can not transmit science data only telemetry data
		/// </summary>
		public double rate = 0.0;

		/// <summary>
		/// ec cost while transmitting at the above rate
		/// <para/> Note: ec_idle is substracted from ec in Science.Update(), it's silly but don't change it as this is what is expected from the RealAntenna API handler
		/// </summary>
		public double ec = 0.0;

		/// <summary> ec cost while not transmitting </summary>
		public double ec_idle = 0.0;

		/// <summary> link quality indicator for the UI, any value from 0-1.
		/// you MUST set this to >= 0 in your mod, otherwise the comm status
		/// will either be handled by an other mod or by the stock implementation.
		/// </summary>
		public double strength = -1;

		/// <summary>
		/// direct_link = 0, indirect_link = 1 (relayed signal), no_link = 2, plasma = 3 (plasma blackout on reentry), storm = 4 (cme storm blackout)
		/// </summary>
		public int status = 2;

		/// <summary>
		/// true if communication is established. if false, vessels can't transmit data and might be uncontrollable.
		/// </summary>
		public bool linked;

		/// <summary>
		/// The name of the thing at the other end of your radio beam (KSC, name of the relay, ...)
		/// </summary>
		public string target_name;

		/// <summary>
		/// The next hop in the control path. (Guid.Empty if none or KSC)
		/// </summary>
		public Guid next_hop = Guid.Empty;

		/// <summary>
		/// The distance to the next hop in meters.
		/// </summary>
		public double hop_distance;

		/// <summary>
		/// The maximum distance to the next hop in meters.
		/// </summary>
		public double hop_max_distance;

		/// <summary>
		/// The data rate to the next hop, contrary to rate which include the min along the path logic,
		/// hop_datarate is only the data rate of this specific hop.
		/// </summary>
		public double hop_datarate;
	}
}
