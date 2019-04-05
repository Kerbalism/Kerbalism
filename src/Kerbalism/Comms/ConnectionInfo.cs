using System;
using KSP.Localization;

namespace KERBALISM
{
	/// <summary> signal connection link status </summary>
	public enum LinkStatus
	{
		direct_link,
		indirect_link,	// relayed signal
		no_link,
		plasma,			// plasma blackout on reentry
		storm			// cme storm blackout
	};


	/// <summary> Stores a single vessels communication info</summary>
	public sealed class ConnectionInfo
	{
		/// <summary> true if there is a connection back to DSN </summary>
		public bool linked = false;

		/// <summary> status of the connection </summary>
		public LinkStatus status = LinkStatus.no_link;

		/// <summary> Controller Path </summary>
		public Guid[] controlPath;

		/// <summary> science data rate. note that internal transmitters can not transmit science data only telemetry data </summary>
		public double rate = 0.0;

		/// <summary> transmitter ec cost</summary>
		public double ec = 0.0;

		/// <summary> signal strength </summary>
		public double strength = 0.0;

		/// <summary> receiving node name </summary>
		public string target_name = "";

		// constructor
		/// <summary> Creates a <see cref="ConnectionInfo"/> object for the specified vessel from it's antenna modules</summary>
		public ConnectionInfo(Vessel v, bool powered, bool storm)
		{
			// set RemoteTech powered and storm state
			if (RemoteTech.Enabled)
			{
				RemoteTech.SetPoweredDown(v.id, !powered);
				RemoteTech.SetCommsBlackout(v.id, storm);
			}

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

			// if CommNet is enabled
			if (HighLogic.fetch.currentGame.Parameters.Difficulty.EnableCommNet)
			{
				AntennaInfoCommNet antennaInfo = new AntennaInfoCommNet(v);

				if (v.connection != null)
				{
					// force CommNet update of unloaded vessels
					if (!v.loaded)
						Lib.ReflectionValue(v.connection, "unloadedDoOnce", true);

					// are we connected to DSN
					if (v.connection.IsConnected)
					{
						ec = antennaInfo.ec;
						rate = antennaInfo.rate * PreferencesScience.Instance.transmitFactor;

						linked = true;
						status = v.connection.ControlPath.First.hopType == CommNet.HopType.Home ? LinkStatus.direct_link : LinkStatus.indirect_link;
						strength = v.connection.SignalStrength;
						rate *= strength;
						target_name = Lib.Ellipsis(Localizer.Format(v.connection.ControlPath.First.end.displayName).Replace("Kerbin", "DSN"), 20);

						if (status != LinkStatus.direct_link)
						{
							Vessel firstHop = Lib.CommNodeToVessel(v.Connection.ControlPath.First.end);
							// Get rate from the firstHop, each Hop will do the same logic, then we will have the min rate for whole path
							rate = Math.Min(Cache.VesselInfo(FlightGlobals.FindVessel(firstHop.id)).connection.rate, rate);
						}
					}
					// is loss of connection due to plasma blackout
					else if (Lib.ReflectionValue<bool>(v.connection, "inPlasma"))  // calling InPlasma causes a StackOverflow :(
					{
						status = LinkStatus.plasma;
					}
				}
				// if nothing has changed, no connection
				return;
			}
			// RemoteTech signal system
			else if (RemoteTech.Enabled)
			{
				AntennaInfoRT antennaInfo = new AntennaInfoRT(v);

				// are we connected
				if (RemoteTech.Connected(v.id))
				{
					ec = antennaInfo.ec;
					rate = antennaInfo.rate * PreferencesScience.Instance.transmitFactor;

					linked = RemoteTech.ConnectedToKSC(v.id);
					status = RemoteTech.TargetsKSC(v.id) ? LinkStatus.direct_link : LinkStatus.indirect_link;
					target_name = status == LinkStatus.direct_link ? Lib.Ellipsis("DSN: " + (RemoteTech.NameTargetsKSC(v.id) ?? ""), 20) :
						Lib.Ellipsis(RemoteTech.NameFirstHopToKSC(v.id) ?? "", 20);

					if (linked) controlPath = RemoteTech.GetCommsControlPath(v.id);

					// Get the lowest rate in ControlPath
					if (controlPath != null)
					{
						// Get rate from the firstHop, each Hop will do the same logic, then we will have the lowest rate for the path
						if (controlPath.Length > 0)
						{
							double dist = RemoteTech.GetCommsDistance(v.id, controlPath[0]);
							strength = 1 - (dist / Math.Max(RemoteTech.GetCommsMaxDistance(v.id, controlPath[0]), 1));

							// If using relay, get the lowest rate
							if (status != LinkStatus.direct_link)
							{
								Vessel target = FlightGlobals.FindVessel(controlPath[0]);
								strength *= Cache.VesselInfo(target).connection.strength;
								rate = Math.Min(Cache.VesselInfo(target).connection.rate, rate * strength);
							}
							else rate *= strength;
						}
					}
				}
				// is loss of connection due to a blackout
				else if (RemoteTech.GetCommsBlackout(v.id))
				{
					status = storm ? LinkStatus.storm : LinkStatus.plasma;
				}
				// if nothing has changed, no connection
				return;
			}
			// the simple stupid always connected signal system
			else
			{
				AntennaInfoCommNet antennaInfo = new AntennaInfoCommNet(v);
				ec = antennaInfo.ec * 0.16; // Consume 16% of the stock ec. Workaround for drain consumption with CommNet, ec consumption turns similar of RT
				rate = antennaInfo.rate * PreferencesScience.Instance.transmitFactor;

				linked = true;
				status = LinkStatus.direct_link;
				strength = 1;    // 100 %
				target_name = "DSN: KSC";
			}
		}
	}
} // KERBALISM
