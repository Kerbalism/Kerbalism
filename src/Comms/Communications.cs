using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{


	public static class Communications
	{

		public static void update(Vessel v, vessel_info vi, VesselData vd, double elapsed_s)
		{
			// do nothing if signal mechanic is disabled
			if (!HighLogic.fetch.currentGame.Parameters.Difficulty.EnableCommNet && !RemoteTech.Enabled()) return;

			// get connection info
			ConnectionInfo conn = vi.connection;

			// maintain and send messages
			// - do not send messages for vessels without an antenna
			// - do not send messages during/after solar storms
			// - do not send messages for EVA kerbals
			if (conn.status != LinkStatus.no_antenna && !v.isEVA && v.situation != Vessel.Situations.PRELAUNCH)
			{
				if (!vd.msg_signal && !conn.linked)
				{
					vd.msg_signal = true;
					if (vd.cfg_signal && conn.status != LinkStatus.blackout)
					{
						string subtext = "Data transmission disabled";
						if (vi.crew_count == 0)
						{
							switch (Settings.UnlinkedControl)
							{
								case UnlinkedCtrl.none: subtext = "Remote control disabled"; break;
								case UnlinkedCtrl.limited: subtext = "Limited control available"; break;
							}
						}
						Message.Post(Severity.warning, Lib.BuildString("Signal lost with <b>", v.vesselName, "</b>"), subtext);
					}
				}
				else if (vd.msg_signal && conn.linked)
				{
					vd.msg_signal = false;
					if (vd.cfg_signal && !Storm.JustEnded(v, elapsed_s))
					{
						var path = conn.path;
						Message.Post(Severity.relax, Lib.BuildString("<b>", v.vesselName, "</b> signal is back"),
						  path.Count == 0 ? "We got a direct link with the space center" : Lib.BuildString("Relayed by <b>", path[path.Count - 1].vesselName, "</b>"));
					}
				}
			}
		}




		public static ConnectionInfo connection(Vessel v)
		{
			// hard-coded transmission rate and cost
			const double ext_rate = 0.064;
			const double ext_cost = 0.1;

			// if RemoteTech is present and enabled
			if (RemoteTech.Enabled())
			{
				if (RemoteTech.Connected(v.id) && !RemoteTech.ConnectedToKSC(v.id))
				{
					return new ConnectionInfo(LinkStatus.indirect_link, ext_rate, ext_cost);
				}
				else if (RemoteTech.Connected(v.id) && RemoteTech.ConnectedToKSC(v.id))
				{
					return new ConnectionInfo(LinkStatus.direct_link, ext_rate, ext_cost);
				}
				else
				{
					return new ConnectionInfo(LinkStatus.no_link);
				}
			}
			// if CommNet is enabled
			else if (HighLogic.fetch.currentGame.Parameters.Difficulty.EnableCommNet)
			{
				return v.connection != null && v.connection.IsConnected
				  ? new ConnectionInfo(LinkStatus.direct_link, ext_rate * v.connection.SignalStrength, ext_cost)
				  : new ConnectionInfo(LinkStatus.no_link);
			}
			// the simple stupid signal system
			else
			{
				return new ConnectionInfo(LinkStatus.direct_link, ext_rate, ext_cost);
			}
		}
	}


} // KERBALISM

