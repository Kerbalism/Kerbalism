using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{


	public static class VesselConfig
	{
		public static void config(this Panel p, Vessel v)
		{
			// avoid corner-case when this is called in a lambda after scene changes
			v = FlightGlobals.FindVessel(v.id);

			// if vessel doesn't exist anymore, leave the panel empty
			if (v == null) return;

			// get info from the cache
			vessel_info vi = Cache.VesselInfo(v);

			// if not a valid vessel, leave the panel empty
			if (!vi.is_valid) return;

			// set metadata
			p.title(Lib.BuildString(Lib.Ellipsis(v.vesselName, 20), " <color=#cccccc>VESSEL CONFIG</color>"));

			// time-out simulation
			if (p.timeout(vi)) return;

			// get data from db
			VesselData vd = DB.Vessel(v);

			// toggle rendering
			string tooltip;
			if (Features.Reliability)
			{
				p.section("RENDERING");
			}
			if (Features.Reliability)
			{
				tooltip = "Highlight failed components";
				p.content("highlight malfunctions", string.Empty, tooltip);
				p.icon(vd.cfg_highlights ? Icons.toggle_green : Icons.toggle_red, tooltip, () => p.toggle(ref vd.cfg_highlights));
			}

			// toggle messages
			p.section("MESSAGES");
			tooltip = "Receive a message when\nElectricCharge level is low";
			p.content("battery", string.Empty, tooltip);
			p.icon(vd.cfg_ec ? Icons.toggle_green : Icons.toggle_red, tooltip, () => p.toggle(ref vd.cfg_ec));
			if (Features.Supplies)
			{
				tooltip = "Receive a message when\nsupply resources level is low";
				p.content("supply", string.Empty, tooltip);
				p.icon(vd.cfg_supply ? Icons.toggle_green : Icons.toggle_red, tooltip, () => p.toggle(ref vd.cfg_supply));
			}
			if (RemoteTech.Enabled() || HighLogic.fetch.currentGame.Parameters.Difficulty.EnableCommNet)
			{
				tooltip = "Receive a message when signal is lost or obtained";
				p.content("signal", string.Empty, tooltip);
				p.icon(vd.cfg_signal ? Icons.toggle_green : Icons.toggle_red, tooltip, () => p.toggle(ref vd.cfg_signal));
			}
			if (Features.Reliability)
			{
				tooltip = "Receive a message\nwhen a component fail";
				p.content("reliability", string.Empty, tooltip);
				p.icon(vd.cfg_malfunction ? Icons.toggle_green : Icons.toggle_red, tooltip, () => p.toggle(ref vd.cfg_malfunction));
			}
			if (Features.SpaceWeather)
			{
				tooltip = "Receive a message\nduring CME events";
				p.content("storm", string.Empty, tooltip);
				p.icon(vd.cfg_storm ? Icons.toggle_green : Icons.toggle_red, tooltip, () => p.toggle(ref vd.cfg_storm));
			}
			if (Features.Automation)
			{
				tooltip = "Receive a message when\nscripts are executed";
				p.content("script", string.Empty, tooltip);
				p.icon(vd.cfg_script ? Icons.toggle_green : Icons.toggle_red, tooltip, () => p.toggle(ref vd.cfg_script));
			}
		}
	}


} // KERBALISM

