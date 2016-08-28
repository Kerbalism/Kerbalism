// ====================================================================================================================
// visualize informations about a body, and some controls for things like fields rendering
// ====================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class BodyInfo
{
  // ctor
  public BodyInfo()
  {
    // enable global access
    instance = this;

    // generate unique id
    win_id = Lib.RandomInt(int.MaxValue);

    // setup window geometry
    win_rect = new Rect(245.0f, 41.0f, width, 0.0f); //< height set automatically later

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
    // only show in mapview
    if (!MapView.MapIsEnabled) return;
    
    // only show if there is a selected body and that body is not the sun
    CelestialBody body = Lib.SelectedBody();
    if (body == null || body.flightGlobalsIndex == 0) return;

    // if open
    if (open)
    {
      // set automatic height
      win_rect.height = height();

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
  }

  // draw the window
  void render(int _)
  {
    // shortcut
    CelestialBody sun = FlightGlobals.Bodies[0];

    // get selected body
    CelestialBody body = Lib.SelectedBody();

    // calculate simulation values
    double atmo_factor = Sim.AtmosphereFactor(body, 0.7071);
    double gamma_factor = Sim.GammaTransparency(body, 0.0);
    double sun_dist = Sim.Apoapsis(Lib.PlanetarySystem(body)) - sun.Radius - body.Radius;
    Vector3d sun_dir = (sun.position - body.position).normalized;
    double solar_flux = Sim.SolarFlux(sun_dist) * atmo_factor;
    double albedo_flux = Sim.AlbedoFlux(body, body.position + sun_dir * body.Radius);
    double body_flux = Sim.BodyFlux(body, 0.0);
    double total_flux = solar_flux + albedo_flux + body_flux + Sim.BackgroundFlux();
    double temperature = body.atmosphere ? body.GetTemperature(0.0) : Sim.BlackBodyTemperature(total_flux);

    // calculate night-side temperature
    double total_flux_min = Sim.AlbedoFlux(body, body.position - sun_dir * body.Radius) + body_flux + Sim.BackgroundFlux();
    double temperature_min = Sim.BlackBodyTemperature(total_flux_min);

    // calculate radiation at body surface
    double radiation = Radiation.ComputeSurface(body, gamma_factor);

    // draw pseudo-title
    GUILayout.BeginHorizontal();
    GUILayout.Label(body.bodyName.ToUpper(), top_style);
    GUILayout.EndHorizontal();

    // surface panel
    string temperature_str = body.atmosphere
      ? Lib.HumanReadableTemp(temperature)
      : Lib.BuildString(Lib.HumanReadableTemp(temperature_min), " / ", Lib.HumanReadableTemp(temperature));
    render_title("SURFACE");
    render_content("temperature", temperature_str);
    render_content("radiation", Lib.HumanReadableRadiationRate(radiation));
    render_content("solar flux", Lib.HumanReadableFlux(solar_flux));
    render_space();

    // atmosphere panel
    if (body.atmosphere)
    {
      render_title("ATMOSPHERE");
      render_content("breathable", body.atmosphereContainsOxygen ? "yes" : "no");
      render_content("visible absorption", Lib.HumanReadablePerc(1.0 - Sim.AtmosphereFactor(body, 0.7071)));
      render_content("gamma absorption", Lib.HumanReadablePerc(1.0 - Sim.GammaTransparency(body, 0.0)));
      render_space();
    }

    // rendering panel
    render_title("RENDERING");
    render_content("inner belt", ref Radiation.show_inner);
    render_content("outer belt", ref Radiation.show_outer);
    render_content("magnetopause", ref Radiation.show_pause);
    render_space();


    // draw footer
    GUILayout.BeginHorizontal();
    GUILayout.Label("(ALT+N to open and close)", bot_style);
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


  void render_content(string desc, string value)
  {
    GUILayout.BeginHorizontal(row_style);
    GUILayout.Label(desc, label_style);
    GUILayout.Label(value, value_style);
    GUILayout.EndHorizontal();
  }

  void render_content(string desc, ref bool b)
  {
    GUILayout.BeginHorizontal(row_style);
    GUILayout.Label(desc, label_style);
    GUILayout.Label(b ? "<color=green>show</color>" : "<color=red>hide</color>", value_style);
    if (Lib.IsClicked()) b = !b;
    GUILayout.EndHorizontal();
  }


  void render_space()
  {
    GUILayout.Space(10.0f);
  }


  float panel_height(int entries)
  {
    return 16.0f + (float)entries * 16.0f + 18.0f;
  }


  float height()
  {
    CelestialBody body = Lib.SelectedBody();
    return top_height + bot_height
      + panel_height(3)
      + (body.atmosphere ? panel_height(3) : 0.0f)
      + panel_height(3);
  }


  // show the window
  public static void Open()
  {
    instance.open = true;
  }


  // close the window
  public static void Close()
  {
    instance.open = false;
  }


  // toggle the window
  public static void Toggle()
  {
    instance.open = !instance.open;
  }


  // return true if the window is open
  public static bool IsOpen()
  {
    return instance.open;
  }


  // constants
  const float width = 260.0f;
  const float top_height = 20.0f;
  const float bot_height = 20.0f;
  const float margin = 10.0f;

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

  // open/close the window
  bool open = true;

  // permit global access
  static BodyInfo instance;
}


} // KERBALISM