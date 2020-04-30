using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


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
			v.TryGetVesselDataTemp(out VesselData vd);

			// if not a valid vessel, leave the panel empty
			if (!vd.IsSimulated) return;

			// set metadata
			p.Title(Lib.BuildString(Lib.Ellipsis(v.vesselName, Styles.ScaleStringLength(20)), " ", Lib.Color(Local.VESSELCONFIG_title, Lib.Kolor.LightGrey)));//"VESSEL CONFIG"
			p.Width(Styles.ScaleWidthFloat(355.0f));
			p.paneltype = Panel.PanelType.config;

			p.AddSection(Local.VESSELCONFIG_RENDERING);//"RENDERING"

			p.AddCheckbox(vd.cfg_show, Local.VESSELCONFIG_ShowVessel, Local.VESSELCONFIG_ShowVessel_desc, click: () => p.Toggle(ref vd.cfg_show));
			if (Features.Failures)
				p.AddCheckbox(vd.cfg_highlights, Local.VESSELCONFIG_Highlightfailed, Local.VESSELCONFIG_Highlightfailed_desc, click: () => p.Toggle(ref vd.cfg_highlights));

			// toggle messages
			p.AddSection(Local.VESSELCONFIG_MESSAGES);//"MESSAGES"

			p.AddCheckbox(vd.cfg_ec, Local.VESSELCONFIG_battery, Local.VESSELCONFIG_EClow, click: () => p.Toggle(ref vd.cfg_ec));

			if (Features.LifeSupport)
				p.AddCheckbox(vd.cfg_supply, Local.VESSELCONFIG_supply, Local.VESSELCONFIG_Supplylow, click: () => p.Toggle(ref vd.cfg_supply));

			if (API.Comm.handlers.Count > 0 || HighLogic.fetch.currentGame.Parameters.Difficulty.EnableCommNet)
				p.AddCheckbox(vd.cfg_signal, Local.VESSELCONFIG_signal, Local.VESSELCONFIG_Signallost, click: () => p.Toggle(ref vd.cfg_signal));

			if (Features.Failures)
				p.AddCheckbox(vd.cfg_malfunction, Local.VESSELCONFIG_reliability, Local.VESSELCONFIG_Componentfail, click: () => p.Toggle(ref vd.cfg_malfunction));

			if (Features.Radiation)
				p.AddCheckbox(vd.cfg_storm, Local.VESSELCONFIG_storm, Local.VESSELCONFIG_CMEevent, click: () => p.Toggle(ref vd.cfg_storm));

			p.AddCheckbox(vd.cfg_script, Local.VESSELCONFIG_script, Local.VESSELCONFIG_ScriptExe, click: () => p.Toggle(ref vd.cfg_script));
		}
	}


} // KERBALISM

