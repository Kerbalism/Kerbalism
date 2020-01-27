using System.Collections.Generic;

namespace KERBALISM
{
	/// <summary> signal connection link status </summary>
	public enum LinkStatus
	{
		direct_link = 0,
		indirect_link = 1,	// relayed signal
		no_link = 2,
		plasma = 3,			// plasma blackout on reentry
		storm = 4			// cme storm blackout
	}

	/// <summary> Stores a single vessels communication info</summary>
	public sealed class ConnectionInfo
	{
		/// <summary> true if there is a connection back to DSN </summary>
		public bool linked = false;

		/// <summary> status of the connection </summary>
		public LinkStatus status = LinkStatus.no_link;

		/// <summary> science data rate. note that internal transmitters can not transmit science data only telemetry data </summary>
		public double rate = 0.0;

		/// <summary> transmitter ec cost while transmitting</summary>
		public double ec = 0.0;

		/// <summary> transmitter ec cost while idle</summary>
		public double ec_idle = 0.0;

		/// <summary> signal strength </summary>
		public double strength = 0.0;

		/// <summary> receiving node name </summary>
		public string target_name = "";

		public List<string[]> control_path = null;

		public static ConnectionInfo Update(Vessel v, bool powered, bool storm)
		{
			return new ConnectionInfo(v, powered, storm);
		}

		private static double SanityCheck(double value, string name, Vessel v)
		{
			if (double.IsNaN(value) || double.IsInfinity(value))
			{
				Lib.LogDebug("ERROR: Comms: invalid value: " + name + " on " + v + " (" + value + ")");
				value = 0;
			}
			return value;
		}

		/// <summary> Creates a <see cref="ConnectionInfo"/> object for the specified vessel from it's antenna modules</summary>
		private ConnectionInfo(Vessel v, bool powered, bool storm)
		{
			// return no connection if there is no ec left
			if (!powered)
				return;

			// wait until network is initialized (2 seconds after load)
			if (!Communications.NetworkInitialized)
				return;

			AntennaInfo ai = GetAntennaInfo(v, powered, storm);

			ec = SanityCheck(ai.ec, "ec", v);
			ec_idle = SanityCheck(ai.ec_idle, "ec_idle", v);
			rate = SanityCheck(ai.rate, "rate", v) * PreferencesScience.Instance.transmitFactor;
			linked = ai.linked;
			strength = SanityCheck(ai.strength, "strength", v);
			target_name = ai.target_name;
			control_path = ai.control_path;

			switch(ai.status)
			{
				case 0: status = LinkStatus.direct_link; break;
				case 1: status = LinkStatus.indirect_link; break;
				case 2: status = LinkStatus.no_link; break;
				case 3: status = LinkStatus.plasma; break;
				case 4: status = LinkStatus.storm; break;
				default: status = LinkStatus.no_link; break;
			}
		}

		private static AntennaInfo GetAntennaInfo(Vessel v, bool powered, bool storm)
		{
			AntennaInfo ai = new AntennaInfo();
			ai.powered = powered;
			ai.storm = storm;
			ai.transmitting = v.KerbalismData().filesTransmitted.Count > 0;

			API.Comm.Init(ai, v);
			if (ai.strength > -1)
				return ai;

#if !KSP15_16
			var cluster = Serenity.GetScienceCluster(v);
			if (cluster != null)
				return new AntennaInfoSerenity(v, cluster, storm, ai.transmitting).AntennaInfo();
#endif
			// if CommNet is enabled
			if (HighLogic.fetch.currentGame.Parameters.Difficulty.EnableCommNet)
				return new AntennaInfoCommNet(v, powered, storm, ai.transmitting).AntennaInfo();

			// default: the simple stupid always connected signal system
			AntennaInfo antennaInfo = new AntennaInfoCommNet(v, powered, storm, ai.transmitting).AntennaInfo();

			antennaInfo.ec *= 0.16;
			antennaInfo.linked = true;
			antennaInfo.status = (int)LinkStatus.direct_link;
			antennaInfo.strength = 1;    // 100 %
			antennaInfo.target_name = "DSN: KSC";

			return antennaInfo;
		}
	}
} // KERBALISM
