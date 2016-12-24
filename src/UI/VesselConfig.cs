using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public static class VesselConfig
{
  public static void config(this Panel p, Vessel v)
  {
    // if vessel doesn't exist anymore, leave the panel empty
    if (FlightGlobals.FindVessel(v.id) == null) return;

    // get info from the cache
    vessel_info vi = Cache.VesselInfo(v);

    // if not a valid vessel, leave the panel empty
    if (!vi.is_valid) return;

    // get data from db
    VesselData vd = DB.Vessel(v);

    // toggle rendering
    string tooltip;
    if (Features.Signal || Features.Reliability)
    {
      p.section("RENDERING");
    }
    if (Features.Signal)
    {
      tooltip = "Render the connection line\nin mapview and tracking station";
      p.content("show links", string.Empty, tooltip);
      p.icon(vd.cfg_showlink ? Icons.toggle_green : Icons.toggle_red, tooltip, () => p.toggle(ref vd.cfg_showlink));
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
    if (Features.Signal)
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

    // set metadata
    p.title(Lib.BuildString(Lib.Ellipsis(v.vesselName, 20), " <color=#cccccc>VESSEL CONFIG</color>"));
  }
}


} // KERBALISM

