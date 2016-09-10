// ====================================================================================================================
// the vessel monitor
// ====================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class Monitor
{
  // ctor
  public Monitor()
  {
    // style for vessel row
    row_style = new GUIStyle();
    row_style.stretchWidth = true;
    row_style.fixedHeight = 16.0f; //< required for icon vertical alignment

    // style for vessel name
    name_style = new GUIStyle(HighLogic.Skin.label);
    name_style.richText = true;
    name_style.normal.textColor = Color.white;
    name_style.fixedWidth = 120.0f;
    name_style.stretchHeight = true;
    name_style.fontSize = 12;
    name_style.alignment = TextAnchor.MiddleLeft;

    // style for body name
    body_style = new GUIStyle(HighLogic.Skin.label);
    body_style.richText = true;
    body_style.normal.textColor = new Color(0.75f, 0.75f, 0.75f, 1.0f);
    body_style.fixedWidth = 36.0f;
    body_style.stretchHeight = true;
    body_style.fontSize = 8;
    body_style.alignment = TextAnchor.MiddleRight;

    // icon style
    icon_style = new GUIStyle();
    icon_style.alignment = TextAnchor.MiddleRight;

    // filter style
    filter_style = new GUIStyle(HighLogic.Skin.label);
    filter_style.normal.textColor = new Color(0.66f, 0.66f, 0.66f, 1.0f);
    filter_style.stretchWidth = true;
    filter_style.fontSize = 12;
    filter_style.alignment = TextAnchor.MiddleCenter;
    filter_style.fixedHeight = 16.0f;
    filter_style.border = new RectOffset(0, 0, 0, 0);

    // vessel config style
    config_style = new GUIStyle(HighLogic.Skin.label);
    config_style.normal.textColor = Color.white;
    config_style.padding = new RectOffset(0, 0, 0, 0);
    config_style.alignment = TextAnchor.MiddleLeft;
    config_style.imagePosition = ImagePosition.ImageLeft;
    config_style.fontSize = 9;

    // group texfield style
    group_style = new GUIStyle(config_style);
    group_style.imagePosition = ImagePosition.TextOnly;
    group_style.stretchWidth = true;
    group_style.fixedHeight = 11.0f;
    group_style.normal.textColor = Color.cyan;
  }


  GUIContent indicator_ec(Vessel v)
  {
    resource_info ec = ResourceCache.Info(v, "ElectricCharge");

    GUIContent state = new GUIContent();
    state.tooltip = ec.capacity > 0.0 ? "EC: " + Lib.HumanReadablePerc(ec.level) : "";
    state.image = icon_battery_nominal;

    double low_threshold = 0.15;
    if (Kerbalism.ec_rule != null) low_threshold = Kerbalism.ec_rule.low_threshold;

    if (ec.level <= 0.005) state.image = icon_battery_danger;
    else if (ec.level <= low_threshold) state.image = icon_battery_warning;
    return state;
  }


  GUIContent indicator_supplies(Vessel v, vessel_info vi)
  {
    GUIContent state = new GUIContent();
    List<string> tooltips = new List<string>();
    uint max_severity = 0;
    if (vi.crew_count > 0)
    {
      foreach(Rule r in Kerbalism.supply_rules)
      {
        resource_info res = ResourceCache.Info(v, r.resource_name);
        if (res.capacity > double.Epsilon)
        {
          double depletion = r.Depletion(v, res);
          string deplete_str = depletion <= double.Epsilon
            ? ", depleted"
            : double.IsNaN(depletion)
            ? ""
            : Lib.BuildString(", deplete in <b>", Lib.HumanReadableDuration(depletion), "</b>");
          tooltips.Add(Lib.BuildString(r.resource_name, ": <b>", Lib.HumanReadablePerc(res.level), "</b>", deplete_str));
          uint severity = res.level <= 0.005 ? 2u : res.level <= r.low_threshold ? 1u : 0;
          max_severity = Math.Max(max_severity, severity);
        }
      }
    }
    switch(max_severity)
    {
      case 0: state.image = icon_supplies_nominal; break;
      case 1: state.image = icon_supplies_warning; break;
      case 2: state.image = icon_supplies_danger;  break;
    }
    state.tooltip = string.Join("\n", tooltips.ToArray());
    return state;
  }


  GUIContent indicator_reliability(Vessel v, vessel_info vi)
  {
    GUIContent state = new GUIContent();
    uint max_malfunctions = vi.max_malfunction;
    if (max_malfunctions == 0)
    {
      state.image = icon_malfunction_nominal;
      state.tooltip = "No malfunctions";
    }
    else if (max_malfunctions == 1)
    {
      state.image = icon_malfunction_warning;
      state.tooltip = "Minor malfunctions";
    }
    else
    {
      state.image = icon_malfunction_danger;
      state.tooltip = "Major malfunctions";
    }
    if (vi.avg_quality > 0.0) state.tooltip += Lib.BuildString("\n<i>Quality: ", Reliability.QualityToString(vi.avg_quality), "</i>");
    return state;
  }


  GUIContent indicator_signal(Vessel v, vessel_info vi)
  {
    GUIContent state = new GUIContent();
    link_data ld = vi.link;
    switch(ld.status)
    {
      case link_status.direct_link:
        state.image = icon_signal_direct;
        state.tooltip = "Direct link";
        break;

      case link_status.indirect_link:
        state.image = icon_signal_relay;
        if (ld.path.Count == 1)
        {
          state.tooltip = Lib.BuildString("Signal relayed by <b>", ld.path[ld.path.Count - 1].vesselName, "</b>");
        }
        else
        {
          state.tooltip = "Signal relayed by:";
          for(int i=ld.path.Count-1; i>0; --i) state.tooltip += Lib.BuildString("\n<b>", ld.path[i].vesselName, "</b>");
        }
        break;

      case link_status.no_link:
        state.image = icon_signal_none;
        state.tooltip = !vi.blackout ? "No signal" : "Blackout";
        break;

      case link_status.no_antenna:
        state.image = icon_signal_none;
        state.tooltip = "No antenna";
        break;
    }
    return state;
  }


  void problem_sunlight(vessel_info info, ref List<Texture> icons, ref List<string> tooltips)
  {
    if (info.sunlight <= double.Epsilon)
    {
      icons.Add(icon_sun_shadow);
      tooltips.Add("In shadow");
    }
  }


  void problem_scrubbers(Vessel v, List<Scrubber.partial_data> scrubbers, ref List<Texture> icons, ref List<string> tooltips)
  {
    if (scrubbers.Count == 0) return;

    bool no_ec_left = ResourceCache.Info(v, "ElectricCharge").amount <= double.Epsilon;
    bool disabled = false;
    foreach(var scrubber in scrubbers)
    {
      disabled |= !scrubber.is_enabled;
    }
    if (no_ec_left)
    {
      icons.Add(icon_scrubber_danger);
      tooltips.Add("Scrubber has no power");
    }
    else if (disabled)
    {
      icons.Add(icon_scrubber_warning);
      tooltips.Add("Scrubber disabled");
    }
  }


  void problem_recyclers(Vessel v, List<Recycler.partial_data> recyclers, ref List<Texture> icons, ref List<string> tooltips)
  {
    if (recyclers.Count == 0) return;

    bool no_ec_left = ResourceCache.Info(v, "ElectricCharge").amount <= double.Epsilon;
    bool disabled = false;
    foreach(var recycler in recyclers)
    {
      disabled |= !recycler.is_enabled;
    }
    if (no_ec_left)
    {
      icons.Add(icon_scrubber_danger);
      tooltips.Add("Recycler has no power");
    }
    else if (disabled)
    {
      icons.Add(icon_scrubber_warning);
      tooltips.Add("Recycler disabled");
    }
  }


  void problem_greenhouses(Vessel v, List<Greenhouse.partial_data> greenhouses, ref List<Texture> icons, ref List<string> tooltips)
  {
    if (greenhouses.Count == 0) return;

    double min_growing = double.MaxValue;
    foreach(var greenhouse in greenhouses) min_growing = Math.Min(min_growing, greenhouse.growing);

    if (min_growing <= double.Epsilon)
    {
      icons.Add(icon_greenhouse_danger);
      tooltips.Add("Greenhouse not growing");
    }
    else if (min_growing < 0.00000005)
    {
      icons.Add(icon_greenhouse_warning);
      tooltips.Add("Greenhouse growing slowly");
    }
  }


  void problem_kerbals(List<ProtoCrewMember> crew, ref List<Texture> icons, ref List<string> tooltips)
  {
    UInt32 health_severity = 0;
    UInt32 stress_severity = 0;
    foreach(ProtoCrewMember c in crew)
    {
      // get kerbal data
      kerbal_data kd = DB.KerbalData(c.name);

      // skip disabled kerbals
      if (kd.disabled == 1) continue;

      foreach(Rule r in Kerbalism.rules)
      {
        kmon_data kmon = DB.KmonData(c.name, r.name);
        if (kmon.problem > r.danger_threshold)
        {
          if (!r.breakdown) health_severity = Math.Max(health_severity, 2);
          else stress_severity = Math.Max(stress_severity, 2);
          tooltips.Add(Lib.BuildString(c.name, " (", r.name, ")"));
        }
        else if (kmon.problem > r.warning_threshold)
        {
          if (!r.breakdown) health_severity = Math.Max(health_severity, 1);
          else stress_severity = Math.Max(stress_severity, 1);
          tooltips.Add(Lib.BuildString(c.name, " (", r.name, ")"));
        }
      }

    }
    if (health_severity == 1) icons.Add(icon_health_warning);
    else if (health_severity == 2) icons.Add(icon_health_danger);
    if (stress_severity == 1) icons.Add(icon_stress_warning);
    else if (stress_severity == 2) icons.Add(icon_stress_danger);
  }


  void problem_radiation(vessel_info info, ref List<Texture> icons, ref List<string> tooltips)
  {
    string radiation_str = Lib.BuildString(" (<i>", (info.radiation * 60.0 * 60.0).ToString("F3"), " rad/h)</i>");
    if (info.radiation > 1.0 / 3600.0)
    {
      icons.Add(icon_radiation_danger);
      tooltips.Add(Lib.BuildString("Exposed to extreme radiation", radiation_str));
    }
    else if (info.radiation > 0.15 / 3600.0)
    {
      icons.Add(icon_radiation_warning);
      tooltips.Add(Lib.BuildString("Exposed to intense radiation", radiation_str));
    }
    else if (info.radiation > 0.0195 / 3600.0)
    {
      icons.Add(icon_radiation_warning);
      tooltips.Add(Lib.BuildString("Exposed to moderate radiation", radiation_str));
    }
  }


  void problem_storm(Vessel v, ref List<Texture> icons, ref List<string> tooltips)
  {
    if (Storm.Incoming(v))
    {
      icons.Add(icon_storm_warning);
      tooltips.Add(Lib.BuildString("Coronal mass ejection incoming <i>(", Lib.HumanReadableDuration(Storm.TimeBeforeCME(v)), ")</i>"));
    }
    if (Storm.InProgress(v))
    {
      icons.Add(icon_storm_danger);
      tooltips.Add(Lib.BuildString("Solar storm in progress <i>(", Lib.HumanReadableDuration(Storm.TimeLeftCME(v)), ")</i>"));
    }
  }


  // draw a vessel in the monitor
  // - return: 1 if vessel wasn't skipped
  uint render_vessel(Vessel v)
  {
    // get vessel info from cache
    vessel_info vi = Cache.VesselInfo(v);

    // skip invalid vessels
    if (!vi.is_valid) return 0;

    // get vessel data from the db
    vessel_data vd = DB.VesselData(v.id);

    // skip filtered vessels
    if (filtered() && vd.group != filter) return 0;

    // get vessel crew
    List<ProtoCrewMember> crew = v.loaded ? v.GetVesselCrew() : v.protoVessel.GetVesselCrew();

    // get vessel name
    string vessel_name = v.isEVA ? crew[0].name : v.vesselName;

    // get body name
    string body_name = v.mainBody.name.ToUpper();

    // store problems icons & tooltips
    List<Texture> problem_icons = new List<Texture>();
    List<string> problem_tooltips = new List<string>();

    // detect problems
    problem_sunlight(vi, ref problem_icons, ref problem_tooltips);
    problem_storm(v, ref problem_icons, ref problem_tooltips);
    if (crew.Count > 0)
    {
      problem_kerbals(crew, ref problem_icons, ref problem_tooltips);
      problem_radiation(vi, ref problem_icons, ref problem_tooltips);
      problem_scrubbers(v, vi.scrubbers, ref problem_icons, ref problem_tooltips);
      problem_recyclers(v, vi.recyclers, ref problem_icons, ref problem_tooltips);
    }
    problem_greenhouses(v, vi.greenhouses, ref problem_icons, ref problem_tooltips);

    // choose problem icon
    const UInt64 problem_icon_time = 3;
    Texture problem_icon = icon_empty;
    if (problem_icons.Count > 0)
    {
      UInt64 problem_index = ((UInt64)Time.realtimeSinceStartup / problem_icon_time) % (UInt64)(problem_icons.Count);
      problem_icon = problem_icons[(int)problem_index];
    }

    // generate problem tooltips
    string problem_tooltip = String.Join("\n", problem_tooltips.ToArray());

    // render vessel name & icons
    GUILayout.BeginHorizontal(row_style);
    GUILayout.Label(new GUIContent(Lib.BuildString("<b>", Lib.Epsilon(vessel_name, 18), "</b>"), vessel_name.Length > 18 ? vessel_name : ""), name_style);
    GUILayout.Label(new GUIContent(Lib.Epsilon(body_name, 8), body_name.Length > 8 ? body_name : ""), body_style);
    GUILayout.Label(new GUIContent(problem_icon, problem_tooltip), icon_style);
    GUILayout.Label(indicator_ec(v), icon_style);
    if (Kerbalism.supply_rules.Count > 0) GUILayout.Label(indicator_supplies(v, vi), icon_style);
    if (Kerbalism.features.reliability) GUILayout.Label(indicator_reliability(v, vi), icon_style);
    if (Kerbalism.features.signal) GUILayout.Label(indicator_signal(v, vi), icon_style);
    GUILayout.EndHorizontal();
    if (Lib.IsClicked(1)) Info.Toggle(v);
    else if (Lib.IsClicked(2) && !v.isEVA) Console.Toggle(v);

    // remember last vessel clicked
    if (Lib.IsClicked()) last_clicked_id = v.id;

    // render vessel config
    if (configured_id == v.id) render_config(v);

    // spacing between vessels
    GUILayout.Space(10.0f);

    // signal that the vessel wasn't skipped for whatever reason
    return 1;
  }


  // draw vessel config
  void render_config(Vessel v)
  {
    // get vessel data
    vessel_data vd = DB.VesselData(v.id);

    // draw the config
    if (Kerbalism.ec_rule != null)
    {
      GUILayout.BeginHorizontal(row_style);
      GUILayout.Label(new GUIContent(" EC MESSAGES", icon_toggle[vd.cfg_ec]), config_style);
      if (Lib.IsClicked()) vd.cfg_ec = (vd.cfg_ec == 0 ? 1u : 0);
      GUILayout.EndHorizontal();
    }
    if (Kerbalism.supply_rules.Count > 0)
    {
      GUILayout.BeginHorizontal(row_style);
      GUILayout.Label(new GUIContent(" SUPPLY MESSAGES", icon_toggle[vd.cfg_supply]), config_style);
      if (Lib.IsClicked()) vd.cfg_supply = (vd.cfg_supply == 0 ? 1u : 0);
      GUILayout.EndHorizontal();
    }
    if (Kerbalism.features.signal)
    {
      GUILayout.BeginHorizontal(row_style);
      GUILayout.Label(new GUIContent(" SIGNAL MESSAGES", icon_toggle[vd.cfg_signal]), config_style);
      if (Lib.IsClicked()) vd.cfg_signal = (vd.cfg_signal == 0 ? 1u : 0);
      GUILayout.EndHorizontal();
    }
    if (Settings.StormDuration > double.Epsilon)
    {
      GUILayout.BeginHorizontal(row_style);
      GUILayout.Label(new GUIContent(" STORM MESSAGES", icon_toggle[vd.cfg_storm]), config_style);
      if (Lib.IsClicked()) vd.cfg_storm = (vd.cfg_storm == 0 ? 1u : 0);
      GUILayout.EndHorizontal();
    }
    if (Kerbalism.features.reliability)
    {
      GUILayout.BeginHorizontal(row_style);
      GUILayout.Label(new GUIContent(" RELIABILITY MESSAGES", icon_toggle[vd.cfg_malfunction]), config_style);
      if (Lib.IsClicked()) vd.cfg_malfunction = (vd.cfg_malfunction == 0 ? 1u : 0);
      GUILayout.EndHorizontal();
      GUILayout.BeginHorizontal(row_style);
      GUILayout.Label(new GUIContent(" HIGHLIGHT MALFUNCTIONS", icon_toggle[vd.cfg_highlights]), config_style);
      if (Lib.IsClicked()) vd.cfg_highlights = (vd.cfg_highlights == 0 ? 1u : 0);
      GUILayout.EndHorizontal();
    }
    if (Kerbalism.features.signal)
    {
      GUILayout.BeginHorizontal(row_style);
      GUILayout.Label(new GUIContent(" SHOW LINK", icon_toggle[vd.cfg_showlink]), config_style);
      if (Lib.IsClicked()) vd.cfg_showlink = (vd.cfg_showlink == 0 ? 1u : 0);
      GUILayout.EndHorizontal();
    }
    if (!filtered())
    {
      GUILayout.BeginHorizontal(row_style);
      GUILayout.Label(new GUIContent(" GROUP: ", icon_group), config_style, new[]{GUILayout.Width(52.0f)});
      vd.group = Lib.TextFieldPlaceholder("Kerbalism_group", vd.group, "NONE", group_style).ToUpper();
      GUILayout.EndHorizontal();
    }
    if (!v.isEVA)
    {
      GUILayout.BeginHorizontal(row_style);
      GUILayout.Label(new GUIContent(" CONSOLE", icon_console), config_style);
      if (Lib.IsClicked()) Console.Toggle(v);
      GUILayout.EndHorizontal();
      GUILayout.BeginHorizontal(row_style);
      GUILayout.Label(new GUIContent(" NOTES", icon_notes), config_style);
      if (Lib.IsClicked()) Editor.Toggle(v, "doc/notes");
      GUILayout.EndHorizontal();
    }
    GUILayout.BeginHorizontal(row_style);
    GUILayout.Label(new GUIContent(" DETAILS", icon_info), config_style);
    if (Lib.IsClicked()) Info.Toggle(v);
    GUILayout.EndHorizontal();
  }


  void render_filter()
  {
    // show the group filter
    GUILayout.BeginHorizontal(row_style);
    filter = Lib.TextFieldPlaceholder("Kerbalism_filter", filter, filter_placeholder, filter_style).ToUpper();
    GUILayout.EndHorizontal();
    GUILayout.Space(10.0f);

    // if the filter is focused, forget config id
    if (GUI.GetNameOfFocusedControl() == "Kerbalism_filter") configured_id = Guid.Empty;
  }


  public float width()
  {
    return 300.0f
      - (!Kerbalism.features.reliability ? 20.0f : 0.0f)
      - (!Kerbalism.features.signal ? 20.0f : 0.0f)
      - (Kerbalism.supply_rules.Count == 0 ? 20.0f : 0.0f);
  }


  public float height()
  {
    // note: this function is abused to determine if the filter must be shown

    // guess vessel count
    uint count = 0;
    show_filter = false;
    foreach(Vessel v in FlightGlobals.Vessels)
    {
      // get info from the cache
      vessel_info vi = Cache.VesselInfo(v);

      // skip invalid vessels
      if (!vi.is_valid) continue;

      // get vessel data
      vessel_data vd = DB.VesselData(v.id);

      // determine if filter must be shown
      show_filter |= vd.group.Length > 0 && vd.group != "NONE";

      // if the panel is filtered, skip filtered vessels
      if (filtered() && vd.group != filter) continue;

      // the vessel will be rendered
      ++count;
    }

    // deal with no vessels case
    count = Math.Max(1u, count);

    // calculate height
    float vessels_height = 10.0f + (float)count * (16.0f + 10.0f);
    uint config_entries = 0;
    if (configured_id != Guid.Empty)
    {
      config_entries = 4u; // group, info, console, notes
      if (Kerbalism.ec_rule != null) ++config_entries;
      if (Kerbalism.supply_rules.Count > 0) ++config_entries;
      if (Kerbalism.features.signal) config_entries += 2u;
      if (Kerbalism.features.reliability) config_entries += 2u;
      if (Settings.StormDuration > double.Epsilon) ++config_entries;
      if (filtered()) ++config_entries;
    }
    float config_height = (float)config_entries * 16.0f;
    float filter_height = show_filter ? 16.0f + 10.0f : 0.0f;
    return Math.Min(vessels_height + config_height + filter_height, Screen.height * 0.5f);
  }


  public void render()
  {
    // reset last clicked vessel
    last_clicked_id = Guid.Empty;

    // forget edited vessel if it doesn't exist anymore
    if (FlightGlobals.Vessels.Find(k => k.id == configured_id) == null) configured_id = Guid.Empty;

    // store number of vessels rendered
    uint vessels_rendered = 0;

    // start scrolling view
    scroll_pos = GUILayout.BeginScrollView(scroll_pos, HighLogic.Skin.horizontalScrollbar, HighLogic.Skin.verticalScrollbar);

    // draw active vessel if any
    if (FlightGlobals.ActiveVessel != null)
    {
      vessels_rendered += render_vessel(FlightGlobals.ActiveVessel);
    }

    // for each vessel
    foreach(Vessel v in FlightGlobals.Vessels)
    {
      // skip active vessel
      if (v == FlightGlobals.ActiveVessel) continue;

      // draw the vessel
      vessels_rendered += render_vessel(v);
    }

    // if user clicked on a vessel
    if (last_clicked_id != Guid.Empty)
    {
      // if user clicked on configured vessel hide config, if user clicked on another vessel show its config
      configured_id = (last_clicked_id == configured_id ? Guid.Empty : last_clicked_id);
    }

    // end scroll view
    GUILayout.EndScrollView();

    // no-vessels case
    if (vessels_rendered == 0)
    {
      GUILayout.BeginHorizontal(row_style);
      GUILayout.Label("<i>No vessels</i>", name_style);
      GUILayout.EndHorizontal();
      GUILayout.Space(10.0f);
    }

    // if at least one vessel is assigned to a group, render the filter
    if (show_filter) render_filter();
  }


  // return true if the list of vessels is filtered
  bool filtered()
  {
    return filter.Length > 0 && filter != filter_placeholder;
  }


  // store last vessel clicked in the monitor ui, if any
  Guid last_clicked_id;

  // store vessel whose configs are being edited, if any
  Guid configured_id;

  // group filter placeholder
  const string filter_placeholder = "FILTER BY GROUP";

  // store group filter, if any
  string filter = "";

  // determine if filter is shown
  bool show_filter;

  // used by scroll window mechanics
  Vector2 scroll_pos;

  // styles
  GUIStyle row_style;               // all monitor rows
  GUIStyle name_style;              // vessel name
  GUIStyle body_style;              // vessel body
  GUIStyle icon_style;              // vessel icon
  GUIStyle filter_style;            // vessel filter
  GUIStyle config_style;            // config entry label
  GUIStyle group_style;             // config group textfield

  // icons
  readonly Texture icon_battery_danger      = Lib.GetTexture("battery-red");
  readonly Texture icon_battery_warning     = Lib.GetTexture("battery-yellow");
  readonly Texture icon_battery_nominal     = Lib.GetTexture("battery-white");
  readonly Texture icon_supplies_danger     = Lib.GetTexture("box-red");
  readonly Texture icon_supplies_warning    = Lib.GetTexture("box-yellow");
  readonly Texture icon_supplies_nominal    = Lib.GetTexture("box-white");
  readonly Texture icon_malfunction_danger  = Lib.GetTexture("wrench-red");
  readonly Texture icon_malfunction_warning = Lib.GetTexture("wrench-yellow");
  readonly Texture icon_malfunction_nominal = Lib.GetTexture("wrench-white");
  readonly Texture icon_sun_shadow          = Lib.GetTexture("sun-black");
  readonly Texture icon_signal_none         = Lib.GetTexture("signal-red");
  readonly Texture icon_signal_relay        = Lib.GetTexture("signal-yellow");
  readonly Texture icon_signal_direct       = Lib.GetTexture("signal-white");
  readonly Texture icon_scrubber_danger     = Lib.GetTexture("recycle-red");
  readonly Texture icon_scrubber_warning    = Lib.GetTexture("recycle-yellow");
  readonly Texture icon_greenhouse_danger   = Lib.GetTexture("plant-red");
  readonly Texture icon_greenhouse_warning  = Lib.GetTexture("plant-yellow");
  readonly Texture icon_health_danger       = Lib.GetTexture("health-red");
  readonly Texture icon_health_warning      = Lib.GetTexture("health-yellow");
  readonly Texture icon_stress_danger       = Lib.GetTexture("brain-red");
  readonly Texture icon_stress_warning      = Lib.GetTexture("brain-yellow");
  readonly Texture icon_storm_danger        = Lib.GetTexture("storm-red");
  readonly Texture icon_storm_warning       = Lib.GetTexture("storm-yellow");
  readonly Texture icon_radiation_danger    = Lib.GetTexture("radiation-red");
  readonly Texture icon_radiation_warning   = Lib.GetTexture("radiation-yellow");
  readonly Texture icon_radiation_nominal   = Lib.GetTexture("radiation-white");
  readonly Texture icon_empty               = Lib.GetTexture("empty");
  readonly Texture icon_notes               = Lib.GetTexture("notes");
  readonly Texture icon_console             = Lib.GetTexture("console");
  readonly Texture icon_group               = Lib.GetTexture("search");
  readonly Texture icon_info                = Lib.GetTexture("info");
  readonly Texture[] icon_toggle            ={Lib.GetTexture("toggle-disabled"),
                                              Lib.GetTexture("toggle-enabled")};
}


} // KERBALISM