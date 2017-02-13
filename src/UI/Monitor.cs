using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public enum MonitorPage
{
  telemetry,
  data,
  scripts,
  config
}


public sealed class Monitor
{
  // ctor
  public Monitor()
  {
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
    group_style.normal.textColor = Color.yellow;

    // initialize panel
    panel = new Panel();

    // auto-switch selected vessel on scene changes
    GameEvents.onVesselChange.Add((Vessel v) => { if (selected_id != Guid.Empty) selected_id = v.id; });
  }


  public void update()
  {
    // reset panel
    panel.clear();

    // get vessel
    selected_v = selected_id == Guid.Empty ? null : FlightGlobals.FindVessel(selected_id);

    // if nothing is selected, or if the selected vessel doesn't exist
    // anymore, or if it has become invalid for whatever reason
    if (selected_v == null || !Cache.VesselInfo(selected_v).is_valid)
    {
      // forget the selected vessel, if any
      selected_id = Guid.Empty;

      // filter flag is updated on render_vessel
      show_filter = false;

      // used to detect when no vessels are in list
      bool setup = false;

      // draw active vessel if any
      if (FlightGlobals.ActiveVessel != null)
      {
        setup |= render_vessel(panel, FlightGlobals.ActiveVessel);
      }

      // for each vessel
      foreach(Vessel v in FlightGlobals.Vessels)
      {
        // skip active vessel
        if (v == FlightGlobals.ActiveVessel) continue;

        // draw the vessel
        setup |= render_vessel(panel, v);
      }

      // empty vessel case
      if (!setup)
      {
        panel.header("<i>no vessels</i>");
      }
    }
    // if a vessel is selected
    else
    {
      // header act as title
      render_vessel(panel, selected_v);

      // update page content
      switch(page)
      {
        case MonitorPage.telemetry: panel.telemetry(selected_v); break;
        case MonitorPage.data: panel.fileman(selected_v); break;
        case MonitorPage.scripts: panel.devman(selected_v); break;
        case MonitorPage.config: panel.config(selected_v); break;
      }
    }
  }


  public void render()
  {
    // start scrolling view
    scroll_pos = GUILayout.BeginScrollView(scroll_pos, HighLogic.Skin.horizontalScrollbar, HighLogic.Skin.verticalScrollbar);

    // render panel content
    panel.render();

    // end scroll view
    GUILayout.EndScrollView();

    // if a vessel is selected, and exist
    if (selected_v != null)
    {
      render_menu(selected_v);
    }
    // if at least one vessel is assigned to a group
    else if (show_filter)
    {
      render_filter();
    }

    // right click goes back to list view
    if (Event.current.type == EventType.MouseDown
     && Event.current.button == 1)
    {
      selected_id = Guid.Empty;
    }
  }

  public float width()
  {
    return Math.Max(320.0f, panel.width());
  }

  public float height()
  {
    // top spacing
    float h = 10.0f;

    // panel height
    h += panel.height();

    // one is selected, or filter is required
    if (selected_id != Guid.Empty || show_filter)
    {
      h += 26.0f;
    }

    // clamp to screen height
    return Math.Min(h, Screen.height * 0.75f);
  }

  bool render_vessel(Panel p, Vessel v)
  {
    // get vessel info
    vessel_info vi = Cache.VesselInfo(v);

    // skip invalid vessels
    if (!vi.is_valid) return false;

    // get data from db
    VesselData vd = DB.Vessel(v);

    // determine if filter must be shown
    show_filter |= vd.group.Length > 0 && vd.group != "NONE";

    // skip filtered vessels
    if (filtered() && vd.group != filter) return false;

    // get resource handler
    vessel_resources resources = ResourceCache.Get(v);

    // get vessel crew
    List<ProtoCrewMember> crew = Lib.CrewList(v);

    // get vessel name
    string vessel_name = v.isEVA ? crew[0].name : v.vesselName;

    // get body name
    string body_name = v.mainBody.name.ToUpper();

    // render entry
    p.header
    (
      Lib.BuildString("<b>", Lib.Ellipsis(vessel_name, 20), "</b> <size=9><color=#cccccc>", Lib.Ellipsis(body_name, 8), "</color></size>"),
      string.Empty,
      () => { selected_id = selected_id != v.id ? v.id : Guid.Empty; }
    );

    // problem indicator
    indicator_problems(p, v, vi, crew);

    // battery indicator
    indicator_ec(p, v);

    // supply indicator
    if (Features.Supplies) indicator_supplies(p, v, vi);

    // reliability indicator
    if (Features.Reliability) indicator_reliability(p, v, vi);

    // signal indicator
    if (Features.Signal) indicator_signal(p, v, vi);

    // done
    return true;
  }


  void render_menu(Vessel v)
  {
    const string tooltip = "\n<i>(middle-click to popout in a window)</i>";
    VesselData vd = DB.Vessel(v);
    GUILayout.BeginHorizontal(Styles.entry_container);
    GUILayout.Label(new GUIContent(page == MonitorPage.telemetry ? " <color=#00ffff>INFO</color> " : " INFO ", Icons.small_info, "Telemetry readings" + tooltip), config_style);
    if (Lib.IsClicked()) page = MonitorPage.telemetry;
    else if (Lib.IsClicked(2)) UI.open((p) => p.telemetry(v));
    if (Features.Science)
    {
      GUILayout.Label(new GUIContent(page == MonitorPage.data ? " <color=#00ffff>DATA</color> " : " DATA " , Icons.small_folder, "Stored files and samples" + tooltip), config_style);
      if (Lib.IsClicked()) page = MonitorPage.data;
      else if (Lib.IsClicked(2)) UI.open((p) => p.fileman(v));
    }
    if (Features.Automation)
    {
      GUILayout.Label(new GUIContent(page == MonitorPage.scripts ? " <color=#00ffff>AUTO</color> " : " AUTO ", Icons.small_console, "Control and automate components" + tooltip), config_style);
      if (Lib.IsClicked()) page = MonitorPage.scripts;
      else if (Lib.IsClicked(2)) UI.open((p) => p.devman(v));
    }
    GUILayout.Label(new GUIContent(page == MonitorPage.config ? " <color=#00ffff>CFG</color> " : " CFG ", Icons.small_config, "Configure the vessel" + tooltip), config_style);
    if (Lib.IsClicked()) page = MonitorPage.config;
    else if (Lib.IsClicked(2)) UI.open((p) => p.config(v));
    GUILayout.Label(new GUIContent(" GROUP ", Icons.small_search, "Organize in groups"), config_style);
    vd.group = Lib.TextFieldPlaceholder("Kerbalism_group", vd.group, "NONE", group_style).ToUpper();
    GUILayout.EndHorizontal();
    GUILayout.Space(10.0f);
  }


  void render_filter()
  {
    // show the group filter
    GUILayout.BeginHorizontal(Styles.entry_container);
    filter = Lib.TextFieldPlaceholder("Kerbalism_filter", filter, filter_placeholder, filter_style).ToUpper();
    GUILayout.EndHorizontal();
    GUILayout.Space(10.0f);
  }


  void problem_sunlight(vessel_info info, ref List<Texture> icons, ref List<string> tooltips)
  {
    if (info.sunlight <= double.Epsilon)
    {
      icons.Add(Icons.sun_black);
      tooltips.Add("In shadow");
    }
  }

  void problem_greenhouses(Vessel v, List<Greenhouse.data> greenhouses, ref List<Texture> icons, ref List<string> tooltips)
  {
    if (greenhouses.Count == 0) return;

    foreach(Greenhouse.data greenhouse in greenhouses)
    {
      if (greenhouse.issue.Length > 0)
      {
        if (!icons.Contains(Icons.plant_yellow)) icons.Add(Icons.plant_yellow);
        tooltips.Add(Lib.BuildString("Greenhouse: <b>", greenhouse.issue, "</b>"));
      }
    }
  }

  void problem_kerbals(List<ProtoCrewMember> crew, ref List<Texture> icons, ref List<string> tooltips)
  {
    UInt32 health_severity = 0;
    UInt32 stress_severity = 0;
    foreach(ProtoCrewMember c in crew)
    {
      // get kerbal data
      KerbalData kd = DB.Kerbal(c.name);

      // skip disabled kerbals
      if (kd.disabled) continue;

      foreach(Rule r in Profile.rules)
      {
        RuleData rd = kd.Rule(r.name);
        if (rd.problem > r.danger_threshold)
        {
          if (!r.breakdown) health_severity = Math.Max(health_severity, 2);
          else stress_severity = Math.Max(stress_severity, 2);
          tooltips.Add(Lib.BuildString(c.name, ": <b>", r.name, "</b>"));
        }
        else if (rd.problem > r.warning_threshold)
        {
          if (!r.breakdown) health_severity = Math.Max(health_severity, 1);
          else stress_severity = Math.Max(stress_severity, 1);
          tooltips.Add(Lib.BuildString(c.name, ": <b>", r.name, "</b>"));
        }
      }

    }
    if (health_severity == 1) icons.Add(Icons.health_yellow);
    else if (health_severity == 2) icons.Add(Icons.health_red);
    if (stress_severity == 1) icons.Add(Icons.brain_yellow);
    else if (stress_severity == 2) icons.Add(Icons.brain_red);
  }

  void problem_radiation(vessel_info info, ref List<Texture> icons, ref List<string> tooltips)
  {
    string radiation_str = Lib.BuildString(" (<i>", (info.radiation * 60.0 * 60.0).ToString("F3"), " rad/h)</i>");
    if (info.radiation > 1.0 / 3600.0)
    {
      icons.Add(Icons.radiation_red);
      tooltips.Add(Lib.BuildString("Exposed to extreme radiation", radiation_str));
    }
    else if (info.radiation > 0.15 / 3600.0)
    {
      icons.Add(Icons.radiation_yellow);
      tooltips.Add(Lib.BuildString("Exposed to intense radiation", radiation_str));
    }
    else if (info.radiation > 0.0195 / 3600.0)
    {
      icons.Add(Icons.radiation_yellow);
      tooltips.Add(Lib.BuildString("Exposed to moderate radiation", radiation_str));
    }
  }

  void problem_poisoning(vessel_info info, ref List<Texture> icons, ref List<string> tooltips)
  {
    string poisoning_str = Lib.BuildString("CO2 level in internal atmosphere: <b>", Lib.HumanReadablePerc(info.poisoning), "</b>");
    if (info.poisoning >= 0.05)
    {
      icons.Add(Icons.recycle_red);
      tooltips.Add(poisoning_str);
    }
    else if (info.poisoning > 0.025)
    {
      icons.Add(Icons.recycle_yellow);
      tooltips.Add(poisoning_str);
    }
  }

  void problem_storm(Vessel v, ref List<Texture> icons, ref List<string> tooltips)
  {
    if (Storm.Incoming(v))
    {
      icons.Add(Icons.storm_yellow);
      tooltips.Add(Lib.BuildString("Coronal mass ejection incoming <i>(", Lib.HumanReadableDuration(Storm.TimeBeforeCME(v)), ")</i>"));
    }
    if (Storm.InProgress(v))
    {
      icons.Add(Icons.storm_red);
      tooltips.Add(Lib.BuildString("Solar storm in progress <i>(", Lib.HumanReadableDuration(Storm.TimeLeftCME(v)), ")</i>"));
    }
  }

  void indicator_problems(Panel p, Vessel v, vessel_info vi, List<ProtoCrewMember> crew)
  {
    // store problems icons & tooltips
    List<Texture> problem_icons = new List<Texture>();
    List<string> problem_tooltips = new List<string>();

    // detect problems
    problem_sunlight(vi, ref problem_icons, ref problem_tooltips);
    if (Features.SpaceWeather) problem_storm(v, ref problem_icons, ref problem_tooltips);
    if (crew.Count > 0 && Profile.rules.Count > 0) problem_kerbals(crew, ref problem_icons, ref problem_tooltips);
    if (crew.Count > 0 && Features.Radiation) problem_radiation(vi, ref problem_icons, ref problem_tooltips);
    problem_greenhouses(v, vi.greenhouses, ref problem_icons, ref problem_tooltips);
    if (Features.Poisoning) problem_poisoning(vi, ref problem_icons, ref problem_tooltips);

    // choose problem icon
    const UInt64 problem_icon_time = 3;
    Texture problem_icon = Icons.empty;
    if (problem_icons.Count > 0)
    {
      UInt64 problem_index = ((UInt64)Time.realtimeSinceStartup / problem_icon_time) % (UInt64)(problem_icons.Count);
      problem_icon = problem_icons[(int)problem_index];
    }

    // generate problem icon
    p.icon(problem_icon, String.Join("\n", problem_tooltips.ToArray()));
  }

  void indicator_ec(Panel p, Vessel v)
  {
    Texture image;
    string tooltip;

    resource_info ec = ResourceCache.Info(v, "ElectricCharge");

    tooltip = ec.capacity > 0.0 ? "EC: " + Lib.HumanReadablePerc(ec.level) : "";
    image = Icons.battery_white;

    Supply supply = Profile.supplies.Find(k => k.resource == "ElectricCharge");
    double low_threshold = supply != null ? supply.low_threshold : 0.15;

    if (ec.level <= 0.005) image = Icons.battery_red;
    else if (ec.level <= low_threshold) image = Icons.battery_yellow;


    p.icon(image, tooltip);
  }


  void indicator_supplies(Panel p, Vessel v, vessel_info vi)
  {
    List<string> tooltips = new List<string>();
    uint max_severity = 0;
    if (vi.crew_count > 0)
    {
      var supplies = Profile.supplies.FindAll(k => k.resource != "ElectricCharge");
      foreach(Supply supply in supplies)
      {
        resource_info res = ResourceCache.Info(v, supply.resource);
        if (res.capacity > double.Epsilon)
        {
          double depletion = res.Depletion(vi.crew_count);
          string deplete_str = depletion <= double.Epsilon
            ? ", depleted"
            : double.IsNaN(depletion)
            ? ""
            : Lib.BuildString(", deplete in <b>", Lib.HumanReadableDuration(depletion), "</b>");
          tooltips.Add(Lib.BuildString(supply.resource, ": <b>", Lib.HumanReadablePerc(res.level), "</b>", deplete_str));

          uint severity = res.level <= 0.005 ? 2u : res.level <= supply.low_threshold ? 1u : 0;
          max_severity = Math.Max(max_severity, severity);
        }
      }
    }

    Texture image = Icons.box_white;
    switch(max_severity)
    {
      case 0: image = Icons.box_white; break;
      case 1: image = Icons.box_yellow; break;
      case 2: image = Icons.box_red;  break;
    }
    string tooltip = string.Join("\n", tooltips.ToArray());

    p.icon(image, tooltip);
  }


  void indicator_reliability(Panel p, Vessel v, vessel_info vi)
  {
    Texture image;
    string tooltip;
    if (!vi.malfunction)
    {
      image = Icons.wrench_white;
      tooltip = string.Empty;
    }
    else if (!vi.critical)
    {
      image = Icons.wrench_yellow;
      tooltip = "Malfunctions";
    }
    else
    {
      image = Icons.wrench_red;
      tooltip = "Critical failures";
    }

    p.icon(image, tooltip);
  }


  void indicator_signal(Panel p, Vessel v, vessel_info vi)
  {
    ConnectionInfo conn = vi.connection;

    // target name
    string target_str = string.Empty;
    switch(vi.connection.status)
    {
      case LinkStatus.direct_link: target_str = "DSN"; break;
      case LinkStatus.indirect_link: target_str = vi.connection.path[vi.connection.path.Count - 1].vesselName; break;
      default: target_str = "none"; break;
    }

    // transmitted label, content and tooltip
    string comms_label = vi.relaying.Length == 0 ? "transmitting" : "relaying";
    string comms_str = vi.connection.linked ? "telemetry" : "nothing";
    string comms_tooltip = string.Empty;
    if (vi.relaying.Length > 0)
    {
      ExperimentInfo exp = Science.experiment(vi.relaying);
      comms_str = exp.name;
      comms_tooltip = exp.fullname;
    }
    else if (vi.transmitting.Length > 0)
    {
      ExperimentInfo exp = Science.experiment(vi.transmitting);
      comms_str = exp.name;
      comms_tooltip = exp.fullname;
    }

    string tooltip = Lib.BuildString
    (
      "<align=left />",
      "connected\t<b>", vi.connection.linked ? "yes" : "no", "</b>\n",
      "rate\t\t<b>", Lib.HumanReadableDataRate(vi.connection.rate), "</b>\n",
      "target\t\t<b>", target_str, "</b>\n",
      comms_label, "\t<b>", comms_str, "</b>"
    );

    Texture image = Icons.signal_red;
    switch(conn.status)
    {
      case LinkStatus.direct_link:
        image = vi.connection.rate > 0.005 ? Icons.signal_white : Icons.signal_yellow;
        break;

      case LinkStatus.indirect_link:
        image = vi.connection.rate > 0.005 ? Icons.signal_white : Icons.signal_yellow;
        tooltip += "\n\n<color=yellow>Signal relayed</color>";
        break;

      case LinkStatus.no_link:
        image = Icons.signal_red;
        break;

      case LinkStatus.no_antenna:
        image = Icons.signal_red;
        tooltip += "\n\n<color=red>No antenna</color>";
        break;

      case LinkStatus.blackout:
        image = Icons.signal_red;
        tooltip += "\n\n<color=red>Blackout</color>";
        break;
    }

    p.icon(image, tooltip);
  }

  // return true if the list of vessels is filtered
  bool filtered()
  {
    return filter.Length > 0 && filter != filter_placeholder;
  }

  // id of selected vessel
  Guid selected_id;

  // selected vessel
  Vessel selected_v;

  // group filter placeholder
  const string filter_placeholder = "FILTER BY GROUP";

  // store group filter, if any
  string filter = string.Empty;

  // determine if filter is shown
  bool show_filter;

  // used by scroll window mechanics
  Vector2 scroll_pos;

  // styles
  GUIStyle filter_style;            // vessel filter
  GUIStyle config_style;            // config entry label
  GUIStyle group_style;             // config group textfield

  // monitor page
  MonitorPage page = MonitorPage.telemetry;
  Panel panel;
}


} // KERBALISM