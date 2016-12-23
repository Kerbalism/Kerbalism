using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public static class Telemetry
{
  public static void telemetry(this Panel p, Vessel v)
  {
    // if vessel doesn't exist anymore
    if (FlightGlobals.FindVessel(v.id) == null) return;

    // get info from the cache
    vessel_info vi = Cache.VesselInfo(v);

    // if not a valid vessel
    if (!vi.is_valid) return;

    // get vessel data
    VesselData vd = DB.Vessel(v);

    // get resources
    vessel_resources resources = ResourceCache.Get(v);

    // get crew
    var crew = Lib.CrewList(v);

    // draw the content
    render_environment(p, v, vi);
    render_habitat(p, v, vi);
    render_supplies(p, v, vi, resources);
    render_crew(p, crew);
    render_greenhouse(p, vi);
  }


  static void render_environment(Panel p, Vessel v, vessel_info vi)
  {
    // determine atmosphere
    string atmo_desc = "none";
    if (vi.underwater) atmo_desc = "ocean";
    else if (vi.atmo_factor < 1.0) //< inside an atmosphere
    {
      atmo_desc = vi.breathable ? "breathable" : "not breathable";
    }

    // flux tooltip
    string flux_tooltip = Lib.BuildString
    (
      "<align=left />",
      "solar flux\t<b>", Lib.HumanReadableFlux(vi.solar_flux), "</b>\n",
      "albedo flux\t<b>", Lib.HumanReadableFlux(vi.albedo_flux), "</b>\n",
      "body flux\t<b>", Lib.HumanReadableFlux(vi.body_flux), "</b>"
    );

    // render panel
    p.section("ENVIRONMENT");
    p.content("temperature", Lib.HumanReadableTemp(vi.temperature), flux_tooltip);
    if (Features.Radiation) p.content("radiation", Lib.HumanReadableRadiation(vi.radiation));
    p.content("atmosphere", atmo_desc);
  }

  static void render_habitat(Panel p, Vessel v, vessel_info vi)
  {
    // if habitat feature is disabled, do not show the panel
    if (!Features.Habitat) return;

    // if vessel is unmanned, do not show the panel
    if (vi.crew_count == 0) return;

    // determine some content, with colors
    string pressure_str = Lib.Color(Lib.HumanReadablePressure(vi.pressure * Sim.PressureAtSeaLevel()), vi.pressure < Settings.PressureThreshold, "yellow");
    string poisoning_str = Lib.Color(Lib.HumanReadablePerc(vi.poisoning, "F2"), vi.poisoning > Settings.PoisoningThreshold * 0.5, "yellow");

    // render panel, add some content based on enabled features
    if (!v.isEVA)
    {
      p.section("HABITAT");
      if (Features.Shielding) p.content("shielding", Habitat.shielding_to_string(vi.shielding));
      if (Features.LivingSpace) p.content("living space", Habitat.living_space_to_string(vi.living_space));
      if (Features.Comfort) p.content("comfort", vi.comforts.summary(), vi.comforts.tooltip());
      if (Features.Pressure) p.content("pressure", pressure_str);
      if (Features.Poisoning) p.content("co2 level", poisoning_str);
    }
    else
    {
      p.section("EVA SUIT");
      if (Features.Shielding) p.content("shielding", Habitat.shielding_to_string(vi.shielding));
      if (Features.LivingSpace) p.content("living space", "eva suit");
      if (Features.Poisoning) p.content("co2 level", poisoning_str);
    }
  }

  static void render_supplies(Panel p, Vessel v, vessel_info vi, vessel_resources resources)
  {
    // for each supply
    int supplies = 0;
    foreach(Supply supply in Profile.supplies)
    {
      // get resource info
      resource_info res = resources.Info(v, supply.resource);

      // only show estimate if the resource is present
      if (res.amount <= double.Epsilon) continue;

      // render panel title, if not done already
      if (supplies == 0) p.section("SUPPLIES");

      // rate tooltip
      string rate_tooltip = Math.Abs(res.rate) >= 0.0000001 ? Lib.BuildString
      (
        res.rate > 0.0 ? "<color=#00ff00><b>" : "<color=#ff0000><b>",
        Lib.HumanReadableRate(Math.Abs(res.rate)),
        "</b></color>"
      ) : string.Empty;

      // determine label
      string label = supply.resource == "ElectricCharge"
        ? "battery"
        : Lib.SpacesOnCaps(supply.resource).ToLower();

      // finally, render resource supply
      p.content(label, Lib.HumanReadableDuration(res.Depletion(vi.crew_count)), rate_tooltip);
      ++supplies;
    }
  }


  static void render_crew(Panel p, List<ProtoCrewMember> crew)
  {
    // do nothing if there isn't a crew, or if there are no rules
    if (crew.Count == 0 || Profile.rules.Count == 0) return;

    // panel section
    p.section("CREW");

    // for each crew
    foreach(ProtoCrewMember kerbal in crew)
    {
      // get kerbal data from DB
      KerbalData kd = DB.Kerbal(kerbal.name);

      // analyze issues
      UInt32 health_severity = 0;
      UInt32 stress_severity = 0;

      // for each rule
      List<string> tooltips = new List<string>();
      foreach(Rule r in Profile.rules)
      {
        // get rule data
        RuleData rd = kd.Rule(r.name);

        // add to the tooltip
        tooltips.Add(Lib.BuildString("<b>", Lib.HumanReadablePerc(rd.problem / r.fatal_threshold), "</b>\t", Lib.SpacesOnCaps(r.name).ToLower()));

        // analyze issue
        if (rd.problem > r.danger_threshold)
        {
          if (!r.breakdown) health_severity = Math.Max(health_severity, 2);
          else stress_severity = Math.Max(stress_severity, 2);
        }
        else if (rd.problem > r.warning_threshold)
        {
          if (!r.breakdown) health_severity = Math.Max(health_severity, 1);
          else stress_severity = Math.Max(stress_severity, 1);
        }
      }

      string tooltip = Lib.BuildString("<align=left />", String.Join("\n", tooltips.ToArray()));

      // render selectable title
      p.content(Lib.Ellipsis(kerbal.name.ToUpper(), 20), kd.disabled ? "<color=#00ffff>HYBERNATED</color>" : string.Empty, tooltip);
      p.icon(health_severity == 0 ? Icons.health_white : health_severity == 1 ? Icons.health_yellow : Icons.health_red, tooltip);
      p.icon(stress_severity == 0 ? Icons.brain_white : stress_severity == 1 ? Icons.brain_yellow : Icons.brain_red, tooltip);
    }
  }

  static void render_greenhouse(Panel p, vessel_info vi)
  {
    // do nothing without greenhouses
    if (vi.greenhouses.Count == 0) return;

    // panel section
    p.section("GREENHOUSE");

    // for each greenhouse
    for(int i = 0; i < vi.greenhouses.Count; ++i)
    {
      var greenhouse = vi.greenhouses[i];

      // state string
      string state = greenhouse.issue.Length > 0
        ? Lib.BuildString("<color=yellow>", greenhouse.issue, "</color>")
        : greenhouse.growth >= 0.99
        ? "<color=green>ready to harvest</color>"
        : "growing";

      // tooltip with summary
      string tooltip = Lib.BuildString
      (
        "<align=left />",
        "time to harvest\t<b>", Lib.HumanReadableDuration(greenhouse.tta), "</b>\n",
        "growth\t\t<b>", Lib.HumanReadablePerc(greenhouse.growth), "</b>\n",
        "natural lighting\t<b>", Lib.HumanReadableFlux(greenhouse.natural), "</b>\n",
        "artificial lighting\t<b>", Lib.HumanReadableFlux(greenhouse.artificial), "</b>"
      );

      // render it
      p.content(Lib.BuildString("crop #", (i + 1).ToString()), state, tooltip);

      // issues too, why not
      p.icon(greenhouse.issue.Length == 0 ? Icons.plant_white : Icons.plant_yellow, tooltip);
    }
  }
}


} // KERBALISM