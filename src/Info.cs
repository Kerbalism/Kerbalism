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
    public string name;
    public double living_space = 1.0;
    public double entertainment = 1.0;
    public double shielding = 0.0;
    public uint   crew_count = 0;
  }


  // ctor
  public Info()
  {
    // enable global access
    instance = this;

    // generate unique id
    win_id = Lib.RandomInt(int.MaxValue);

    // setup window geometry
    win_rect = new Rect(80.0f, 80.0f, width, 0.0f);

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

    // title container
    title_container_style = new GUIStyle();
    title_container_style.stretchWidth = true;
    title_container_style.fixedHeight = 16.0f;
    title_container_style.normal.background = Lib.GetTexture("black-background");
    title_container_style.margin.bottom = 4;
    title_container_style.margin.top = 4;

    // title label
    title_label_style = new GUIStyle(HighLogic.Skin.label);
    title_label_style.fontSize = 12;
    title_label_style.alignment = TextAnchor.MiddleCenter;
    title_label_style.normal.textColor = Color.white;
    title_label_style.stretchWidth = true;
    title_label_style.stretchHeight = true;

    // title icons
    title_icon_left_style = new GUIStyle();
    title_icon_left_style.alignment = TextAnchor.MiddleLeft;
    title_icon_right_style = new GUIStyle();
    title_icon_right_style.alignment = TextAnchor.MiddleRight;

    // row style
    row_style = new GUIStyle();
    row_style.stretchWidth = true;
    row_style.fixedHeight = 16.0f;

    // label style
    label_style = new GUIStyle(HighLogic.Skin.label);
    label_style.richText = true;
    label_style.normal.textColor = Color.white;
    label_style.stretchWidth = true;
    label_style.stretchHeight = true;
    label_style.fontSize = 12;
    label_style.alignment = TextAnchor.MiddleLeft;

    // value style
    value_style = new GUIStyle(HighLogic.Skin.label);
    value_style.richText = true;
    value_style.normal.textColor = Color.white;
    value_style.stretchWidth = true;
    value_style.stretchHeight = true;
    value_style.fontSize = 12;
    value_style.alignment = TextAnchor.MiddleRight;
    value_style.fontStyle = FontStyle.Bold;
  }


  // called every frame
  public void on_gui()
  {
    // if there is a vessel id specified
    if (vessel_id != Guid.Empty)
    {
      // try to get the vessel
      Vessel v = FlightGlobals.Vessels.Find(k => k.id == vessel_id);

      // if the vessel still exist
      if (v != null)
      {
        // get info from cache
        vessel_info vi = Cache.VesselInfo(v);

        // if vessel is valid
        if (vi.is_valid)
        {
          // set automatic height
          win_rect.height = height(v, vi);

          // clamp the window to the screen, so it can't be dragged outside
          float offset_x = Math.Max(0.0f, -win_rect.xMin) + Math.Min(0.0f, Screen.width - win_rect.xMax);
          float offset_y = Math.Max(0.0f, -win_rect.yMin) + Math.Min(0.0f, Screen.height - win_rect.yMax);
          win_rect.xMin += offset_x;
          win_rect.xMax += offset_x;
          win_rect.yMin += offset_y;
          win_rect.yMax += offset_y;

          // draw the window
          win_rect = GUILayout.Window(win_id, win_rect, render, "", win_style);
        }
        // if the vessel is invalid
        else
        {
          // forget it
          vessel_id = Guid.Empty;
        }
      }
      // if the vessel doesn't exist anymore
      else
      {
        // forget it
        vessel_id = Guid.Empty;
      }
    }
  }


  // draw the window
  void render(int _)
  {
    // note: the id and the vessel are valid at this point, checked in on_gui()

    // get vessel data
    vessel_data vd = DB.VesselData(vessel_id);

    // get vessel
    Vessel v = FlightGlobals.Vessels.Find(k => k.id == vessel_id);

    // get info from the cache
    vessel_info vi = Cache.VesselInfo(v);

    // get crew
    var crew = v.loaded ? v.GetVesselCrew() : v.protoVessel.GetVesselCrew();

    // draw pseudo-title
    GUILayout.BeginHorizontal();
    GUILayout.Label(v.vesselName, top_style);
    GUILayout.EndHorizontal();

    // draw the content
    render_environment(v, vi);
    render_supplies(v, vi);
    render_internal_space(v, vi, crew);
    render_crew(v, vi, crew);
    render_greenhouse(vi);

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
    GUILayout.BeginHorizontal(title_container_style);
    GUILayout.Label(title, title_label_style);
    GUILayout.EndHorizontal();
  }


  void render_title(string title, ref int index, int count)
  {
    GUILayout.BeginHorizontal(title_container_style);
    if (count > 1)
    {
      GUILayout.Label(arrow_left, title_icon_left_style);
      if (Lib.IsClicked()) { index = (index == 0 ? count : index) - 1; }
    }
    GUILayout.Label(title, title_label_style);
    if (count > 1)
    {
      GUILayout.Label(arrow_right, title_icon_right_style);
      if (Lib.IsClicked()) { index = (index + 1) % count; }
    }
    GUILayout.EndHorizontal();
    if (count == 0) index = 0;
  }


  void render_content(string desc, string value)
  {
    GUILayout.BeginHorizontal(row_style);
    GUILayout.Label(desc, label_style);
    GUILayout.Label(value, value_style);
    GUILayout.EndHorizontal();
  }


  void render_content(string desc, string value, double rate)
  {
    GUILayout.BeginHorizontal(row_style);
    GUILayout.Label(desc, label_style);
    if (!Settings.ShowRates || Math.Abs(rate) < 0.0001 || (((UInt64)Time.realtimeSinceStartup / 3) % 2ul) == 0) GUILayout.Label(value, value_style);
    else GUILayout.Label(Lib.BuildString(rate > 0.0 ? "<color=#00ff00>" : "<color=#ff0000>", Math.Abs(rate).ToString("F3"), "/s</color>"), value_style);
    GUILayout.EndHorizontal();
  }


  void render_space()
  {
    GUILayout.Space(10.0f);
  }


  void render_environment(Vessel v, vessel_info vi)
  {
    // determine atmosphere
    string atmo_desc = "none";
    if (Sim.Underwater(v)) atmo_desc = "ocean";
    else if (vi.atmo_factor < 1.0) //< inside an atmosphere
    {
      atmo_desc = vi.breathable ? "breathable" : "not breathable";
    }

    render_title("ENVIRONMENT");
    render_content("temperature", Lib.HumanReadableTemp(vi.temperature));
    render_content("radiation", Lib.HumanReadableRadiationRate(vi.radiation));
    render_content("atmosphere", atmo_desc);
    if (Settings.ShowFlux)
    {
      render_content("solar flux", Lib.HumanReadableFlux(vi.solar_flux));
      render_content("albedo flux", Lib.HumanReadableFlux(vi.albedo_flux));
      render_content("body flux", Lib.HumanReadableFlux(vi.body_flux));
    }
    if (Settings.RelativisticTime)
    {
      render_content("time dilation", Lib.HumanReadablePerc(1.0 / vi.time_dilation));
    }
    render_space();
  }


  void render_supplies(Vessel v, vessel_info vi)
  {
    if (Kerbalism.supply_rules.Count > 0 || Kerbalism.ec_rule != null)
    {
      render_title("SUPPLIES");
      if (Kerbalism.ec_rule != null)
      {
        resource_info res = ResourceCache.Info(v, "ElectricCharge");
        render_content("battery", res.level > double.Epsilon ? Lib.HumanReadableDuration(Kerbalism.ec_rule.Depletion(v, res)) : "none", res.rate);
      }
      if (vi.crew_capacity > 0)
      {
        foreach(Rule r in Kerbalism.supply_rules)
        {
          resource_info res = ResourceCache.Info(v, r.resource_name);
          render_content(r.resource_name.AddSpacesOnCaps().ToLower(), res.level > double.Epsilon ? Lib.HumanReadableDuration(r.Depletion(v, res)) : "none", res.rate);
        }
      }
      render_space();
    }
  }


  void render_internal_space(Vessel v, vessel_info vi, List<ProtoCrewMember> crew)
  {
    // do not render internal space info for eva vessels
    if (v.isEVA) return;

    // if there is no crew, no space will be found, so do nothing in that case
    if (crew.Count == 0) return;

    // collect set of spaces
    // note: this is guaranteed to get at least a space (because there is at least one crew member)
    List<space_details> spaces = new List<space_details>();
    foreach(var c in crew)
    {
      kerbal_data kd = DB.KerbalData(c.name);
      space_details sd = spaces.Find(k => k.name == kd.space_name);
      if (sd == null)
      {
        sd = new space_details();
        sd.name = kd.space_name;
        sd.living_space = kd.living_space;
        sd.entertainment = kd.entertainment;
        sd.shielding = kd.shielding;
        spaces.Add(sd);
      }
      ++sd.crew_count;
    }

    // select a space
    space_details space = spaces[space_index % spaces.Count];

    // render it
    string radiation_txt = vi.radiation > double.Epsilon
      ? Lib.BuildString(" <i>(", Lib.HumanReadableRadiationRate(vi.radiation * (1.0 - space.shielding)), ")</i>")
      : "";
    render_title(space.name.Length > 0 && spaces.Count > 1 ? Lib.Epsilon(space.name.ToUpper(), 20) : "VESSEL", ref space_index, spaces.Count);
    render_content("living space", QualityOfLife.LivingSpaceToString(space.living_space));
    render_content("entertainment", QualityOfLife.EntertainmentToString(space.entertainment));
    render_content("shielding", Lib.BuildString(Radiation.ShieldingToString(space.shielding), radiation_txt));
    render_space();
  }


  void render_crew(Vessel v, vessel_info vi, List<ProtoCrewMember> crew)
  {
    // get degenerative rules
    List<Rule> degen_rules = Kerbalism.rules.FindAll(k => k.degeneration > 0.0);

    // do nothing if there are no degenerative rules
    if (degen_rules.Count == 0) return;

    // do nothing if there isn't a crew
    if (crew.Count == 0) return;

    // select a kerbal
    ProtoCrewMember kerbal = crew[crew_index % crew.Count];

    // render it
    kerbal_data kd = DB.KerbalData(kerbal.name);
    render_title(Lib.Epsilon(kerbal.name.ToUpper(), 20), ref crew_index, crew.Count);
    foreach(Rule r in degen_rules)
    {
      var kmon = DB.KmonData(kerbal.name, r.name);
      var bar = Lib.ProgressBar(20, kmon.problem, r.warning_threshold, r.danger_threshold, r.fatal_threshold, kd.disabled > 0 ? "cyan" : "");
      render_content(r.name.AddSpacesOnCaps().ToLower(), bar);
    }
    render_content("specialization", kerbal.trait);
    if (Kerbalism.detected_mods.DeepFreeze) render_content("hibernated", kd.disabled > 0 ? "yes" : "no");
    if (Kerbalism.detected_mods.CLS) render_content("inside", v.isEVA ? "EVA" : Lib.Epsilon(kd.space_name.Length == 0 ? v.vesselName : kd.space_name, 24));
    render_space();
  }


  void render_greenhouse(vessel_info vi)
  {
    // do nothing without greenhouses
    if (vi.greenhouses.Count == 0) return;

    // select a greenhouse
    var greenhouse = vi.greenhouses[greenhouse_index % vi.greenhouses.Count];

    // render it
    render_title("GREENHOUSE", ref greenhouse_index, vi.greenhouses.Count);
    render_content("lighting", Lib.HumanReadablePerc(greenhouse.lighting));
    render_content("growth", Lib.HumanReadablePerc(greenhouse.growth));
    render_content("harvest", Lib.HumanReadableDuration(greenhouse.growing > double.Epsilon ? (1.0 - greenhouse.growth) / greenhouse.growing : 0.0));
    render_space();
  }


  float panel_height(int entries)
  {
    return 16.0f + (float)entries * 16.0f + 18.0f;
  }


  float height(Vessel v, vessel_info vi)
  {
    // get degenerative rules
    List<Rule> degen_rules = Kerbalism.rules.FindAll(k => k.degeneration > 0.0);

    // get crew
    var crew = v.loaded ? v.GetVesselCrew() : v.protoVessel.GetVesselCrew();

    // store computed height
    float h = top_height + bot_height;

    // environment panel
    h += panel_height(3 + (Settings.ShowFlux ? 3 : 0) + (Settings.RelativisticTime ? 1 : 0));

    // supply panel
    if ((vi.crew_capacity > 0 && Kerbalism.supply_rules.Count > 0) || Kerbalism.ec_rule != null)
    {
      h += panel_height((Kerbalism.ec_rule != null ? 1 : 0) + (vi.crew_capacity > 0 ? Kerbalism.supply_rules.Count : 0));
    }

    // internal space panel
    if (!v.isEVA && crew.Count > 0)
    {
      h += panel_height(3);
    }

    // crew panel
    if (degen_rules.Count > 0 && crew.Count > 0)
    {
      h += panel_height(degen_rules.Count + 1 + (Kerbalism.detected_mods.CLS ? 1 : 0) + (Kerbalism.detected_mods.DeepFreeze ? 1 : 0));
    }

    // greenhouse panel
    if (vi.greenhouses.Count > 0)
    {
      h += panel_height(3);
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
    instance.space_index = 0;
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
    instance.space_index = 0;
    instance.crew_index = 0;
    instance.greenhouse_index = 0;
  }


  // return true if the window is open
  public static bool IsOpen()
  {
    return instance.vessel_id != Guid.Empty;
  }


  // constants
  const float width = 260.0f;
  const float top_height = 20.0f;
  const float bot_height = 20.0f;
  const float margin = 10.0f;

  // arrow icons
  Texture arrow_left = Lib.GetTexture("left-white");
  Texture arrow_right = Lib.GetTexture("right-white");

  // styles
  GUIStyle win_style;
  GUIStyle top_style;
  GUIStyle bot_style;
  GUIStyle title_container_style;
  GUIStyle title_label_style;
  GUIStyle title_icon_left_style;
  GUIStyle title_icon_right_style;
  GUIStyle row_style;
  GUIStyle label_style;
  GUIStyle value_style;

  // store window id
  int win_id;

  // store window geometry
  Rect win_rect;

  // store dragbox geometry
  Rect drag_rect;

  // store vessel id, if any
  Guid vessel_id;

  // space/crew/greenhouse indexes
  int space_index;
  int crew_index;
  int greenhouse_index;

  // permit global access
  static Info instance;
}


} // KERBALISM