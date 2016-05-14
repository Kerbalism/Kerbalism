// ====================================================================================================================
// visualize informations about a vessel
// ====================================================================================================================


using System;
using System.Collections.Generic;
using HighlightingSystem;
using UnityEngine;


namespace KERBALISM {


[KSPAddon(KSPAddon.Startup.MainMenu, true)]
public class Info : MonoBehaviour
{
  // store data for the space scanning
  class space_details
  {
    public uint   crew_count = 0;
    public double living_space = 1.0;
    public double entertainment = 1.0;
    public double shielding = 0.0;
  }

  // constants
  const float width = 300.0f;
  const float height = 600.0f;
  const float top_height = 20.0f;
  const float bot_height = 20.0f;
  const float margin = 10.0f;
  const float spacing = 10.0f;

  // permit global access
  private static Info instance = null;

  // styles
  GUIStyle win_style;
  GUIStyle top_style;
  GUIStyle bot_style;
  GUIStyle txt_style;
  GUILayoutOption[] txt_options;
  GUIStyle row_style;
  GUIStyle title_style;
  GUIStyle content_style;

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

  // ctor
  Info()
  {
    // enable global access
    instance = this;

    // keep it alive
    DontDestroyOnLoad(this);

    // generate unique id, hopefully
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
  }


  // called every frame
  public void OnGUI()
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

    // get vessel name
    string vessel_name = FlightGlobals.Vessels.Find(k => k.id == vessel_id).vesselName;

    // draw pseudo-title
    GUILayout.BeginHorizontal();
    GUILayout.Label(vessel_name, top_style);
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


  void render_title(string title)
  {
    GUILayout.BeginHorizontal();
    GUILayout.Label(title, title_style);
    GUILayout.EndHorizontal();
  }


  void render_content(string desc, string value)
  {
    GUILayout.BeginHorizontal(row_style);
    GUILayout.Label(desc + "<b>" + value + "</b>", content_style);
    GUILayout.EndHorizontal();
  }


  void render_space()
  {
    GUILayout.Space(10.0f);
  }


  // temp, totally
  string fix_title(string title) { return title.Length < 9 ? title + "\t\t" : title + "\t"; }


  void render_info()
  {
    // find vessel
    Vessel v = FlightGlobals.Vessels.Find(k => k.id == vessel_id);

    // forget vessel if it doesn't exist anymore, or if its a dead eva kerbal
    if (v == null || EVA.IsDead(v)) { vessel_id = Guid.Empty; return; }

    // get info from the cache
    vessel_info vi = Cache.VesselInfo(v);

    render_title("ENVIRONMENT");
    render_content("Temperature:\t", Lib.HumanReadableTemp(vi.temperature));
    render_content("Radiation:\t", Lib.HumanReadableRadiationRate(vi.env_radiation));
    render_content("Atmosphere:\t", v.mainBody.atmosphere ? " yes" + (vi.breathable ? " <i>(breathable)</i>" : "") : "no");
    render_space();

    // render supplies
    if (Kerbalism.supply_rules.Count > 0 || Kerbalism.ec_rule != null)
    {
      render_title("SUPPLIES");
      if (Kerbalism.ec_rule != null)
      {
        var vmon = vi.vmon[Kerbalism.ec_rule.name];
        render_content(fix_title("Battery:"), vmon.level > double.Epsilon ? Lib.HumanReadableDuration(vmon.depletion) : "none");
      }
      if (Lib.CrewCapacity(v) > 0)
      {
        foreach(Rule r in Kerbalism.supply_rules)
        {
          var vmon = vi.vmon[r.resource_name];
          render_content(fix_title(r.resource_name + ":"), vmon.level > double.Epsilon ? Lib.HumanReadableDuration(vmon.depletion) : "none");
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
          ? " <i>(" + Lib.HumanReadableRadiationRate(vi.env_radiation * (1.0 - det.shielding)) + ")</i>"
          : "";

        render_title(space_name.Length > 0 ? space_name.ToUpper() : v.isEVA ? "EVA" : "VESSEL");
        render_content("Living space:\t", QualityOfLife.LivingSpaceToString(det.living_space));
        render_content("Entertainment:\t", QualityOfLife.EntertainmentToString(det.entertainment));
        render_content("Shielding:\t", Radiation.ShieldingToString(det.shielding) + radiation_txt);
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
        foreach(var q in Kerbalism.rules)
        {
          Rule r = q.Value;
          if (r.degeneration > double.Epsilon)
          {
            var kmon = DB.KmonData(c.name, r.name);
            var bar = Lib.ProgressBar(23, kmon.problem, r.warning_threshold, r.danger_threshold, r.fatal_threshold, kd.disabled > 0 ? "cyan" : "");
            render_content(fix_title(r.name + ":"), bar);
          }
        }
        if (kd.space_name.Length > 0 && !v.isEVA) render_content("Inside:\t\t", kd.space_name);
        if (kd.disabled > 0) render_content("Hibernated:\t", "yes");
        render_space();
      }
    }

    // for each greenhouse
    var greenhouses = Greenhouse.GetGreenhouses(v);
    foreach(var greenhouse in greenhouses)
    {
      render_title("GREENHOUSE");
      render_content("Lighting:\t\t", (greenhouse.lighting * 100.0).ToString("F0") + "%");
      render_content("Growth:\t\t", (greenhouse.growth * 100.0).ToString("F0") + "%");
      render_content("Harvest:\t\t", Lib.HumanReadableDuration(greenhouse.growing > double.Epsilon ? 1.0 / greenhouse.growing : 0.0));
      render_space();
    }
  }
}


} // KERBALISM