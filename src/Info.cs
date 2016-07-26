// ====================================================================================================================
// visualize informations about a vessel
// ====================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class Info
{
  // store data for the space scanning
  class space_details
  {
    public uint   crew_count = 0;
    public double living_space = 1.0;
    public double entertainment = 1.0;
    public double shielding = 0.0;
  }


  // ctor
  public Info()
  {
    // enable global access
    instance = this;

    // generate unique id
    win_id = Lib.RandomInt(int.MaxValue);

    // setup window geometry
    win_rect = new Rect(80.0f, (Screen.height - height) * 0.5f, width, height);

    // setup dragbox geometry
    drag_rect = new Rect(0.0f, 0.0f, width, top_height);

    // setup styles
    win_style = new GUIStyle(HighLogic.Skin.window);
    win_style.padding.top = 0;
    win_style.padding.bottom = 0;
    top_style = new GUIStyle();
    top_style.fixedHeight = top_height;
    top_style.fontStyle = FontStyle.Bold;
    top_style.alignment = TextAnchor.MiddleCenter;
    bot_style = new GUIStyle();
    bot_style.fixedHeight = bot_height;
    bot_style.fontSize = 11;
    bot_style.fontStyle = FontStyle.Italic;
    bot_style.alignment = TextAnchor.MiddleRight;
    bot_style.normal.textColor = new Color(0.5f, 0.5f, 0.5f, 1.0f);
    txt_style = new GUIStyle();
    txt_style.fontSize = 11;
    txt_style.wordWrap = true;
    txt_options = new []{GUILayout.MaxWidth(width - margin - spacing), GUILayout.MinHeight(height - top_height - bot_height - margin - spacing)};

    // row style
    row_style = new GUIStyle();
    row_style.stretchWidth = true;
    row_style.fixedHeight = 16.0f;

    // title style
    title_style = new GUIStyle(HighLogic.Skin.label);
    title_style.normal.background = Lib.GetTexture("black-background");
    title_style.normal.textColor = Color.white;
    title_style.stretchWidth = true;
    title_style.stretchHeight = false;
    title_style.fixedHeight = 16.0f;
    title_style.fontSize = 12;
    title_style.border = new RectOffset(0, 0, 0, 0);
    title_style.padding = new RectOffset(3, 4, 3, 4);
    title_style.alignment = TextAnchor.MiddleCenter;

    // content style
    content_style = new GUIStyle(HighLogic.Skin.label);
    content_style.richText = true;
    content_style.normal.textColor = Color.white;
    content_style.stretchWidth = true;
    content_style.stretchHeight = true;
    content_style.fontSize = 12;
    content_style.alignment = TextAnchor.MiddleLeft;

    // rate style
    rate_style = new GUIStyle(HighLogic.Skin.label);
    content_style.richText = true;
    rate_style.fixedWidth = 100.0f;
    rate_style.stretchHeight = true;
    rate_style.fontSize = 12;
    rate_style.alignment = TextAnchor.MiddleRight;
    rate_style.fontStyle = FontStyle.Bold;
  }


  // called every frame
  public void on_gui()
  {
    // do nothing if db isn't ready
    if (!DB.Ready()) return;

    // forget vessel if it doesn't exist anymore
    if (vessel_id != Guid.Empty && FlightGlobals.Vessels.Find(k => k.id == vessel_id) == null) vessel_id = Guid.Empty;

    // do nothing if there isn't a vessel specified
    if (vessel_id == Guid.Empty) return;

    // clamp the window to the screen, so it can't be dragged outside
    float offset_x = Math.Max(0.0f, -win_rect.xMin) + Math.Min(0.0f, Screen.width - win_rect.xMax);
    float offset_y = Math.Max(0.0f, -win_rect.yMin) + Math.Min(0.0f, Screen.height - win_rect.yMax);
    win_rect.xMin += offset_x;
    win_rect.xMax += offset_x;
    win_rect.yMin += offset_y;
    win_rect.yMax += offset_y;

    // draw the window
    win_rect = GUI.Window(win_id, win_rect, render, "", win_style);
  }


  // draw the window
  void render(int id)
  {
    // get vessel data
    vessel_data vd = DB.VesselData(vessel_id);

    // get vessel
    Vessel v = FlightGlobals.Vessels.Find(k => k.id == vessel_id);

    // ignore this frame if vessel isn't there for whatever reason
    if (v == null) return;

    // draw pseudo-title
    GUILayout.BeginHorizontal();
    GUILayout.Label(v.vesselName, top_style);
    GUILayout.EndHorizontal();

    // draw top spacing
    GUILayout.Space(spacing);

    // draw text area
    scroll_pos = GUILayout.BeginScrollView(scroll_pos, HighLogic.Skin.horizontalScrollbar, HighLogic.Skin.verticalScrollbar);
    render_info();
    GUILayout.EndScrollView();

    // draw bottom spacing
    GUILayout.Space(spacing);

    // draw footer
    GUILayout.BeginHorizontal();
    GUILayout.Label("close", bot_style);
    if (Lib.IsClicked()) Close();
    GUILayout.EndHorizontal();

    // enable dragging
    GUI.DragWindow(drag_rect);
  }


  void render_title(string title)
  {
    GUILayout.BeginHorizontal();
    GUILayout.Label(title, title_style);
    GUILayout.EndHorizontal();
  }


  void render_content(string desc, string value)
  {
    GUILayout.BeginHorizontal(row_style);
    GUILayout.Label(Lib.BuildString(desc, "<b>", value, "</b>"), content_style);
    GUILayout.EndHorizontal();
  }


  void render_content(string desc, string value, double rate)
  {
    string clr = rate > 0.0 ? "<color=#00ff00>" : "<color=#ff0000>";
    GUILayout.BeginHorizontal(row_style);
    GUILayout.Label(Lib.BuildString(desc, "<b>", value, "</b>"), content_style);
    GUILayout.Label(Math.Abs(rate) >= 0.0001 ? Lib.BuildString(clr, Math.Abs(rate).ToString("F3"), "/s</color>") : string.Empty, rate_style);
    GUILayout.EndHorizontal();
  }


  void render_space()
  {
    GUILayout.Space(10.0f);
  }


  string fix_title(string title)
  {
    return Lib.BuildString(title, ":", title.Length < 8 ? "\t\t" : "\t");
  }


  void render_info()
  {
    // find vessel
    Vessel v = FlightGlobals.Vessels.Find(k => k.id == vessel_id);

    // forget vessel if it doesn't exist anymore
    if (v == null) { vessel_id = Guid.Empty; return; }

    // get info from the cache
    vessel_info vi = Cache.VesselInfo(v);

    // forget vessel that was invalid (eg: when re-entering from eva on a debris)
    // or that has just become invalid (eg: the user changed vessel type to debris)
    // or that has just died (eg: the vessel is now eva dead)
    if (!vi.is_valid) { vessel_id = Guid.Empty; return; }

    // determine atmosphere
    string atmo_desc = "none";
    if (Sim.Underwater(v)) atmo_desc = "ocean";
    else if (vi.atmo_factor < 1.0) //< inside an atmosphere
    {
      atmo_desc = vi.breathable ? "breathable" : "not breathable";
    }

    render_title("ENVIRONMENT");
    render_content("Temperature:\t", Lib.HumanReadableTemp(vi.temperature));
    render_content("Radiation:\t", Lib.HumanReadableRadiationRate(vi.env_radiation));
    render_content("Atmosphere:\t", atmo_desc);
    if (Settings.ShowFlux)
    {
      render_content("Solar flux:\t", Lib.HumanReadableFlux(vi.solar_flux));
      if (v.mainBody.flightGlobalsIndex != 0)
      {
        render_content("Albedo flux:\t", Lib.HumanReadableFlux(vi.albedo_flux));
        render_content("Body flux:\t", Lib.HumanReadableFlux(vi.body_flux));
      }
    }
    if (Settings.RelativisticTime)
    {
      render_content("Time dilation:\t", Lib.HumanReadablePerc(1.0 / vi.time_dilation));
    }
    render_space();

    // render supplies
    if (Kerbalism.supply_rules.Count > 0 || Kerbalism.ec_rule != null)
    {
      render_title("SUPPLIES");
      if (Kerbalism.ec_rule != null)
      {
        resource_info res = ResourceCache.Info(v, "ElectricCharge");
        render_content(fix_title("Battery"), res.level > double.Epsilon ? Lib.HumanReadableDuration(Kerbalism.ec_rule.Depletion(v, res)) : "none", res.rate);
      }
      if (vi.crew_capacity > 0)
      {
        foreach(Rule r in Kerbalism.supply_rules)
        {
          resource_info res = ResourceCache.Info(v, r.resource_name);
          render_content(fix_title(r.resource_name), res.level > double.Epsilon ? Lib.HumanReadableDuration(r.Depletion(v, res)) : "none", res.rate);
        }
      }
      render_space();
    }


    // get crew
    var crew = v.loaded ? v.GetVesselCrew() : v.protoVessel.GetVesselCrew();

    // do not render internal spaces info for eva vessels
    if (!v.isEVA)
    {
      // collect set of spaces
      Dictionary<string, space_details> spaces = new Dictionary<string, space_details>();
      foreach(var c in crew)
      {
        kerbal_data kd = DB.KerbalData(c.name);
        if (!spaces.ContainsKey(kd.space_name))
        {
          space_details sd = new space_details();
          sd.living_space = kd.living_space;
          sd.entertainment = kd.entertainment;
          sd.shielding = kd.shielding;
          spaces.Add(kd.space_name, sd);
        }
        ++(spaces[kd.space_name].crew_count);
      }

      // for each space
      foreach(var space in spaces)
      {
        string space_name = space.Key;
        space_details det = space.Value;

        string radiation_txt = vi.env_radiation > double.Epsilon
          ? Lib.BuildString(" <i>(", Lib.HumanReadableRadiationRate(vi.env_radiation * (1.0 - det.shielding)), ")</i>")
          : "";

        render_title(space_name.Length > 0 ? Lib.Epsilon(space_name.ToUpper(), 26) : v.isEVA ? "EVA" : "VESSEL");
        render_content("Living space:\t", QualityOfLife.LivingSpaceToString(det.living_space));
        render_content("Entertainment:\t", QualityOfLife.EntertainmentToString(det.entertainment));
        render_content("Shielding:\t", Lib.BuildString(Radiation.ShieldingToString(det.shielding), radiation_txt));
        render_space();
      }
    }

    // for each kerbal
    if (Kerbalism.rules.Count > 0)
    {
      foreach(var c in crew)
      {
        kerbal_data kd = DB.KerbalData(c.name);
        render_title(c.name.ToUpper());
        foreach(Rule r in Kerbalism.rules)
        {
          if (r.degeneration > double.Epsilon)
          {
            var kmon = DB.KmonData(c.name, r.name);
            var bar = Lib.ProgressBar(23, kmon.problem, r.warning_threshold, r.danger_threshold, r.fatal_threshold, kd.disabled > 0 ? "cyan" : "");
            render_content(fix_title(r.name), bar);
          }
        }
        if (kd.space_name.Length > 0 && !v.isEVA) render_content("Inside:\t\t", Lib.Epsilon(kd.space_name, 18));
        if (kd.disabled > 0) render_content("Hibernated:\t", "yes");
        render_space();
      }
    }

    // for each greenhouse
    foreach(var greenhouse in vi.greenhouses)
    {
      render_title("GREENHOUSE");
      render_content("Lighting:\t\t", Lib.HumanReadablePerc(greenhouse.lighting));
      render_content("Growth:\t\t", Lib.HumanReadablePerc(greenhouse.growth));
      render_content("Harvest:\t\t", Lib.HumanReadableDuration(greenhouse.growing > double.Epsilon ? (1.0 - greenhouse.growth) / greenhouse.growing : 0.0));
      render_space();
    }
  }


  // show the window
  public static void Open(Vessel v)
  {
    // setting vessel id show the window
    instance.vessel_id = v.id;
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
  }


  // return true if the window is open
  public static bool IsOpen()
  {
    return instance.vessel_id != Guid.Empty;
  }


  // constants
  const float width = 300.0f;
  const float height = 600.0f;
  const float top_height = 20.0f;
  const float bot_height = 20.0f;
  const float margin = 10.0f;
  const float spacing = 10.0f;

  // styles
  GUIStyle win_style;
  GUIStyle top_style;
  GUIStyle bot_style;
  GUIStyle txt_style;
  GUILayoutOption[] txt_options;
  GUIStyle row_style;
  GUIStyle title_style;
  GUIStyle content_style;
  GUIStyle rate_style;

  // store window id
  int win_id;

  // store window geometry
  Rect win_rect;

  // store dragbox geometry
  Rect drag_rect;

  // used by scroll window mechanics
  Vector2 scroll_pos;

  // store vessel id, if any
  Guid vessel_id;

  // permit global access
  static Info instance;
}


} // KERBALISM