using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {



public sealed class Info : Window
{
  public Info()
  : base(260u, 80u, 80u, 20u, Styles.win)
  {
    // enable global access
    instance = this;

    // detect if deep freeze is installed
    deep_freeze = Lib.HasAssembly("DeepFreeze");
  }

  public override bool prepare()
  {
    // if there is a vessel id specified
    if (vessel_id != Guid.Empty)
    {
      // try to get the vessel
      Vessel v = FlightGlobals.Vessels.Find(k => k.id == vessel_id);

      // if the vessel doesn't exist, forget it
      if (v == null)
      {
        vessel_id = Guid.Empty;
      }
      // if the vessel is not valid, forget it
      else if (!Cache.VesselInfo(v).is_valid)
      {
        vessel_id = Guid.Empty;
      }
    }

    // if there is no vessel selected, don't draw anything
    return vessel_id != Guid.Empty;
  }

  // draw the window
  public override void render()
  {
    // note: the id and the vessel are valid at this point

    // get vessel
    Vessel v = FlightGlobals.Vessels.Find(k => k.id == vessel_id);

    // get vessel data
    VesselData vd = DB.Vessel(v);

    // get info from the cache
    vessel_info vi = Cache.VesselInfo(v);

    // get resources
    vessel_resources resources = ResourceCache.Get(v);

    // get crew
    var crew = Lib.CrewList(v);

    // draw pseudo-title
    if (Panel.title(v.vesselName)) Close();

    // draw the content
    render_environment(v, vi);
    render_supplies(v, vi, resources);
    render_signal(v, vi);
    render_habitat(v, vi, resources);
    render_crew(crew);
    render_greenhouse(vi);
  }

  void render_environment(Vessel v, vessel_info vi)
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
    Panel.section("ENVIRONMENT");
    Panel.content("temperature", Lib.HumanReadableTemp(vi.temperature), flux_tooltip);
    if (Features.Radiation) Panel.content("radiation", Lib.HumanReadableRadiation(vi.radiation));
    Panel.content("atmosphere", atmo_desc);
    Panel.space();
  }

  void render_supplies(Vessel v, vessel_info vi, vessel_resources resources)
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
      if (supplies == 0) Panel.section("SUPPLIES");

      // rate tooltip
      string rate_tooltip = Math.Abs(res.rate) >= 0.000001 ? Lib.BuildString
      (
        res.rate > 0.0 ? "<color=#00ff00><b>" : "<color=#ff0000><b>",
        Lib.HumanReadableRate(Math.Abs(res.rate)),
        "</b></color>"
      ) : string.Empty;

      // determine label
      string label = supply.resource == "ElectricCharge"
        ? "battery"
        : Lib.SpacesOnCaps(supply.resource).ToLower();

      // determine content
      string content = Lib.HumanReadableDuration(res.Depletion(vi.crew_count));

      // finally, render resource supply
      Panel.content(label, content, rate_tooltip);
      ++supplies;
    }

    // spacing, if something was rendered
    if (supplies > 0) Panel.space();
  }

  void render_signal(Vessel v, vessel_info vi)
  {
    // if signal is disabled, don't show the panel
    if (!Features.Signal) return;

    // target name
    string target_str = string.Empty;
    switch(vi.connection.status)
    {
      case LinkStatus.direct_link: target_str = "DSN"; break;
      case LinkStatus.indirect_link: target_str = vi.connection.path[vi.connection.path.Count - 1].vesselName; break;
      default: target_str = "none"; break;
    }

    // transmitted name
    string transmission_str = vi.connection.linked ? "telemetry" : "nothing";
    if (vi.relaying.Length > 0) transmission_str = Science.experiment_name(vi.relaying);
    else if (vi.transmitting.Length > 0) transmission_str = Science.experiment_name(vi.transmitting);

    // render panel
    Panel.section("SIGNAL");
    Panel.content("connected", vi.connection.linked ? "yes" : "no");
    Panel.content("data rate", Lib.HumanReadableDataRate(vi.connection.rate));
    Panel.content("target", target_str);
    Panel.content(vi.relaying.Length == 0 ? "transmitting" : "relaying", transmission_str);
    Panel.space();
  }

  void render_habitat(Vessel v, vessel_info vi, vessel_resources resources)
  {
    // if habitat feature is disabled, do not show the panel
    if (!Features.Habitat) return;

    // if vessel is unmanned, do not show the panel
    if (vi.crew_count == 0) return;

    // get pseudo-resources info
    resource_info atmo_res = resources.Info(v, "Atmosphere");
    resource_info waste_res = resources.Info(v, "WasteAtmosphere");

    // determine some content, with colors
    string pressure_str = Lib.Color(Lib.HumanReadablePressure(vi.pressure * Sim.PressureAtSeaLevel()), vi.pressure < Settings.PressureThreshold, "yellow");
    string poisoning_str = Lib.Color(Lib.HumanReadablePerc(vi.poisoning, "F2"), vi.poisoning > Settings.PoisoningThreshold * 0.5, "yellow");

    // render panel, add some content based on enabled features
    if (!v.isEVA)
    {
      Panel.section("HABITAT");
      if (Features.Shielding) Panel.content("shielding", Habitat.shielding_to_string(vi.shielding));
      if (Features.LivingSpace) Panel.content("living space", Habitat.living_space_to_string(vi.living_space));
      if (Features.Comfort) Panel.content("comfort", vi.comforts.summary(), vi.comforts.tooltip());
      if (Features.Pressure) Panel.content("pressure", pressure_str);
      if (Features.Poisoning) Panel.content("co2 level", poisoning_str);
    }
    else
    {
      Panel.section("EVA SUIT");
      if (Features.Shielding) Panel.content("shielding", Habitat.shielding_to_string(vi.shielding));
      if (Features.LivingSpace) Panel.content("living space", "eva suit");
      if (Features.Poisoning) Panel.content("co2 level", poisoning_str);
    }
    Panel.space();
  }

  void render_crew(List<ProtoCrewMember> crew)
  {
    // do nothing if there isn't a crew, or if there are no rules
    if (crew.Count == 0 || Profile.rules.Count == 0) return;

    // select a kerbal
    ProtoCrewMember kerbal = crew[crew_index % crew.Count];

    // get kerbal data from DB
    KerbalData kd = DB.Kerbal(kerbal.name);

    // render selectable title
    Panel.section(Lib.Ellipsis(kerbal.name.ToUpper(), 20), ref crew_index, crew.Count);

    // for each rule
    foreach(Rule r in Profile.rules)
    {
      // get rule data
      RuleData rd = kd.Rule(r.name);

      // generate progress bar
      var bar = Lib.ProgressBar(20, rd.problem, r.warning_threshold, r.danger_threshold, r.fatal_threshold, kd.disabled ? "#00ffff" : "");

      // render it
      Panel.content(Lib.SpacesOnCaps(r.name).ToLower(), bar);
    }

    // always show kerbal specialization
    Panel.content("specialization", kerbal.trait);

    // if DeepFreeze is detected, also show if the kerbal is hibernated
    if (deep_freeze) Panel.content("hibernated", kd.disabled ? "yes" : "no");

    // done
    Panel.space();
  }

  void render_greenhouse(vessel_info vi)
  {
    // do nothing without greenhouses
    if (vi.greenhouses.Count == 0) return;

    // select a greenhouse
    var greenhouse = vi.greenhouses[greenhouse_index % vi.greenhouses.Count];

    // determine section label
    string title = vi.greenhouses.Count == 1 ? "GREENHOUSE" : Lib.BuildString("GREENHOUSE ", (greenhouse_index+1).ToString());

    // determine state string
    string state = greenhouse.issue.Length > 0
      ? Lib.BuildString("<color=yellow>", greenhouse.issue, "</color>")
      : greenhouse.growth >= 0.99
      ? "<color=green>ready to harvest</color>"
      : "growing";

    // render it
    Panel.section(title, ref greenhouse_index, vi.greenhouses.Count);
    Panel.content("growth", Lib.ProgressBar(20, greenhouse.growth, double.MaxValue, double.MaxValue, 1.0));
    Panel.content("state", state);
    Panel.content("natural lighting", Lib.HumanReadableFlux(greenhouse.natural));
    Panel.content("artificial lighting", Lib.HumanReadableFlux(greenhouse.artificial));
    Panel.content("time to harvest", Lib.HumanReadableDuration(greenhouse.tta));
    Panel.space();
  }

  public override float height()
  {
    // note: the id and the vessel are valid at this point, checked in on_gui()

    // get vessel
    Vessel v = FlightGlobals.Vessels.Find(k => k.id == vessel_id);

    // get info from the cache
    vessel_info vi = Cache.VesselInfo(v);

    // get crew
    var crew = Lib.CrewList(v);

    // store computed height
    float h = 20.0f;

    // environment panel
    h += Panel.height(2 + (Features.Radiation ? 1 : 0));

    // supply panel
    int supplies = 0;
    foreach(Supply supply in Profile.supplies)
    {
      resource_info res = ResourceCache.Info(v, supply.resource);
      if (res.amount <= double.Epsilon) continue;
      ++supplies;
    }
    if (supplies > 0) h += Panel.height(supplies);

    // signal panel
    if (Features.Signal)
    {
      h += Panel.height(4);
    }

    // habitat panel
    if (Features.Habitat && vi.crew_count > 0)
    {
      if (!v.isEVA)
      {
        h += Panel.height
        (
          (Features.Shielding ? 1 : 0)
        + (Features.LivingSpace ? 1 : 0)
        + (Features.Comfort ? 1 : 0)
        + (Features.Pressure ? 1 : 0)
        + (Features.Poisoning ? 1 : 0)
        );
      }
      else
      {
        h += Panel.height
        (
          (Features.Shielding ? 1 : 0)
        + (Features.LivingSpace ? 1 : 0)
        + (Features.Poisoning ? 1 : 0)
        );
      }
    }

    // crew panel
    if (crew.Count > 0 && Profile.rules.Count > 0)
    {
      h += Panel.height(Profile.rules.Count + 1 + (deep_freeze ? 1 : 0));
    }

    // greenhouse panel
    if (vi.greenhouses.Count > 0)
    {
      h += Panel.height(5);
    }

    // finally, return the height
    return h;
  }

  // show the window
  public static void Open(Vessel v)
  {
    // setting vessel id show the window
    instance.vessel_id = v.id;

    // reset indexes
    instance.crew_index = 0;
    instance.greenhouse_index = 0;
  }

  // close the window
  public static void Close()
  {
    // resetting vessel id hide the window
    instance.vessel_id = Guid.Empty;
  }

  // toggle the window
  public static void Toggle(Vessel v)
  {
    // if vessel is different, show it
    // if vessel is the same, hide it
    instance.vessel_id = (instance.vessel_id == v.id ? Guid.Empty : v.id);

    // reset indexes
    instance.crew_index = 0;
    instance.greenhouse_index = 0;
  }

  // return true if the window is open
  public static bool IsOpen()
  {
    return instance.vessel_id != Guid.Empty;
  }

  // store vessel id, if any
  Guid vessel_id;

  // space/crew/greenhouse indexes
  int crew_index;
  int greenhouse_index;

  // if deep freeze is present we show the hibernated state for the crew
  bool deep_freeze;

  // permit global access
  static Info instance;
}


} // KERBALISM