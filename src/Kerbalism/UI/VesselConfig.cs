﻿using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{
	public static class VesselConfig
	{
		public static void Config(this Panel p, Vessel v)
		{
			// avoid corner-case when this is called in a lambda after scene changes
			v = FlightGlobals.FindVessel(v.id);

			// if vessel doesn't exist anymore, leave the panel empty
			if (v == null) return;

			// get vessel data
			VesselData vd = v.KerbalismData();

			// if not a valid vessel, leave the panel empty
			if (!vd.IsSimulated) return;

			// set metadata
			p.Title(Lib.BuildString(Lib.Ellipsis(v.vesselName, Styles.ScaleStringLength(20)), " ", Lib.Color("VESSEL CONFIG", Lib.Kolor.LightGrey)));
			p.Width(Styles.ScaleWidthFloat(355.0f));
			p.paneltype = Panel.PanelType.config;

			// toggle rendering
			string tooltip;
			if (Features.Reliability)
			{
				p.AddSection("RENDERING");
			}
			if (Features.Reliability)
			{
				tooltip = "Highlight failed components";
				p.AddContent("highlight malfunctions", string.Empty, tooltip);
				p.AddRightIcon(vd.cfg_highlights ? Textures.toggle_green : Textures.toggle_red, tooltip, () => p.Toggle(ref vd.cfg_highlights));
			}

			// toggle messages
			p.AddSection("MESSAGES");
			tooltip = "Receive a message when\nElectricCharge level is low";
			p.AddContent("battery", string.Empty, tooltip);
			p.AddRightIcon(vd.cfg_ec ? Textures.toggle_green : Textures.toggle_red, tooltip, () => p.Toggle(ref vd.cfg_ec));
			if (Features.Supplies)
			{
				tooltip = "Receive a message when\nsupply resources level is low";
				p.AddContent("supply", string.Empty, tooltip);
				p.AddRightIcon(vd.cfg_supply ? Textures.toggle_green : Textures.toggle_red, tooltip, () => p.Toggle(ref vd.cfg_supply));
			}
			if (API.Comm.handlers.Count > 0 || HighLogic.fetch.currentGame.Parameters.Difficulty.EnableCommNet)
			{
				tooltip = "Receive a message when signal is lost or obtained";
				p.AddContent("signal", string.Empty, tooltip);
				p.AddRightIcon(vd.cfg_signal ? Textures.toggle_green : Textures.toggle_red, tooltip, () => p.Toggle(ref vd.cfg_signal));
			}
			if (Features.Reliability)
			{
				tooltip = "Receive a message\nwhen a component fail";
				p.AddContent("reliability", string.Empty, tooltip);
				p.AddRightIcon(vd.cfg_malfunction ? Textures.toggle_green : Textures.toggle_red, tooltip, () => p.Toggle(ref vd.cfg_malfunction));
			}
			if (Features.SpaceWeather)
			{
				tooltip = "Receive a message\nduring CME events";
				p.AddContent("storm", string.Empty, tooltip);
				p.AddRightIcon(vd.cfg_storm ? Textures.toggle_green : Textures.toggle_red, tooltip, () => p.Toggle(ref vd.cfg_storm));
			}
			if (Features.Automation)
			{
				tooltip = "Receive a message when\nscripts are executed";
				p.AddContent("script", string.Empty, tooltip);
				p.AddRightIcon(vd.cfg_script ? Textures.toggle_green : Textures.toggle_red, tooltip, () => p.Toggle(ref vd.cfg_script));
			}
		}
	}


} // KERBALISM

