﻿using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;

namespace KERBALISM
{


	public static class Communications
	{
		public static bool NetworkInitialized = false;	// True if CommNet is initialized

		public static void Update(Vessel v, Vessel_info vi, VesselData vd, Vessel_resources resources, double elapsed_s)
		{
			// consume ec for internal transmitters (control and telemetry)
			Resource_info ec = resources.Info(v, "ElectricCharge");
			ec.Consume(vi.connection.internal_cost * elapsed_s);

			// do nothing if signal mechanic is disabled or CommNet is not ready
			if (!(HighLogic.fetch.currentGame.Parameters.Difficulty.EnableCommNet && NetworkInitialized) && !RemoteTech.Enabled())
				return;

			// maintain and send messages
			// - do not send messages during/after solar storms
			// - do not send messages for EVA kerbals
			if (!v.isEVA && v.situation != Vessel.Situations.PRELAUNCH)
			{
				if (!vd.msg_signal && !vi.connection.linked)
				{
					vd.msg_signal = true;
					if (vd.cfg_signal)
					{
						string subtext = Localizer.Format("#KERBALISM_UI_transmissiondisabled");

						switch (vi.connection.status)
						{

							case LinkStatus.plasma:
								subtext = Localizer.Format("#KERBALISM_UI_Plasmablackout");
								break;
							case LinkStatus.storm:
								subtext = Localizer.Format("#KERBALISM_UI_Stormblackout");
								break;
							default:
								if (vi.crew_count == 0)
								{
									switch (Settings.UnlinkedControl)
									{
										case UnlinkedCtrl.none:
											subtext = Localizer.Format("#KERBALISM_UI_noctrl");
											break;
										case UnlinkedCtrl.limited:
											subtext = Localizer.Format("#KERBALISM_UI_limitedcontrol");
											break;
									}
								}
								break;
						}

						Message.Post(Severity.warning, Lib.BuildString(Localizer.Format("#KERBALISM_UI_signallost"), " <b>", v.vesselName, "</b>"), subtext);
					}
				}
				else if (vd.msg_signal && vi.connection.linked)
				{
					vd.msg_signal = false;
					if (vd.cfg_signal)
					{
						Message.Post(Severity.relax, Lib.BuildString("<b>", v.vesselName, "</b> ", Localizer.Format("#KERBALISM_UI_signalback")),
						  vi.connection.status == LinkStatus.direct_link ? Localizer.Format("#KERBALISM_UI_directlink") :
							Lib.BuildString(Localizer.Format("#KERBALISM_UI_relayby"), " <b>", vi.connection.target_name, "</b>"));
					}
				}
			}
		}
	}


} // KERBALISM

