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

		/// <summary> transmitter ec cost</summary>
		public double ec = 0.0;

		/// <summary> signal strength </summary>
		public double strength = 0.0;

		/// <summary> receiving node name </summary>
		public string target_name = "";

		public List<string[]> control_path = null;

		// constructor
		/// <summary> Creates a <see cref="ConnectionInfo"/> object for the specified vessel from it's antenna modules</summary>
		public ConnectionInfo(Vessel v, bool powered, bool storm)
		{
			// return no connection if there is no ec left
			if (!powered)
			{
				// hysteresis delay
				if (DB.Vessel(v).hyspos_signal >= 5.0)
				{
					DB.Vessel(v).hyspos_signal = 5.0;
					DB.Vessel(v).hysneg_signal = 0.0;
					return;
				}
				DB.Vessel(v).hyspos_signal += 0.1;
			}
			else
			{
				// hysteresis delay
				DB.Vessel(v).hysneg_signal += 0.1;
				if (DB.Vessel(v).hysneg_signal < 5.0)
					return;
				DB.Vessel(v).hysneg_signal = 5.0;
				DB.Vessel(v).hyspos_signal = 0.0;
			}

			AntennaInfo ai = GetAntennaInfo(v, powered, storm);
			ec = ai.ec;
			rate = ai.rate * PreferencesScience.Instance.transmitFactor;
			linked = ai.linked;
			strength = ai.strength;
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
			API.Comm.Init(ai, v, powered, storm);
			if (ai.strength > -1)
				return ai;

			// if CommNet is enabled
			if (HighLogic.fetch.currentGame.Parameters.Difficulty.EnableCommNet)
				return new AntennaInfoCommNet(v, powered, storm);

			// default: the simple stupid always connected signal system
			AntennaInfoCommNet antennaInfo = new AntennaInfoCommNet(v, powered, storm);

			antennaInfo.ec *= 0.16;
			antennaInfo.linked = true;
			antennaInfo.status = (int)LinkStatus.direct_link;
			antennaInfo.strength = 1;    // 100 %
			antennaInfo.target_name = "DSN: KSC";

			return antennaInfo;
		}
	}
} // KERBALISM
