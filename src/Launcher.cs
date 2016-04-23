// ====================================================================================================================
// manage the toolbar button and the GUI
// ====================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.UI.Screens;


namespace KERBALISM {


[KSPAddon(KSPAddon.Startup.MainMenu, true)]
public class Launcher : MonoBehaviour
{
  // initialized flag
  bool ui_initialized;

  // store reference to applauncher button
  ApplicationLauncherButton launcher_btn;

  // applauncher button icons
  readonly Texture launcher_icon = Lib.GetTexture("applauncher");

  // used to remember previous visibility state in some scene changes
  bool last_show_window;

  // styles
  GUIStyle window_style;
  GUIStyle tooltip_style;

  // the vessel planner
  Planner planner = new Planner();

  // the vessel monitor
  Monitor monitor = new Monitor();

  // keep it alive
  Launcher() { DontDestroyOnLoad(this); }

  // called after resources are loaded
  public void Start()
  {
    // add toolbar-related callbacks
    GameEvents.onGUIApplicationLauncherReady.Add(this.init);

    // window style
    Color white = new Color(1.0f, 1.0f, 1.0f, 1.0f);
    window_style = new GUIStyle(HighLogic.Skin.window);
    window_style.normal.textColor = white;
    window_style.focused.textColor = white;
    window_style.richText = true;
    window_style.stretchWidth = true;
    window_style.stretchHeight = true;
    window_style.padding.top = 0;
    window_style.padding.bottom = 0;

    // tooltip style
    tooltip_style = new GUIStyle(HighLogic.Skin.label);
    tooltip_style.normal.background = Lib.GetTexture("black-background");
    tooltip_style.normal.textColor = white;
    tooltip_style.stretchWidth = true;
    tooltip_style.stretchHeight = true;
    tooltip_style.fontSize = 12;
    tooltip_style.border = new RectOffset(0, 0, 0, 0);
    tooltip_style.padding = new RectOffset(6, 6, 3, 3);
    tooltip_style.alignment = TextAnchor.MiddleCenter;
  }

  // called after applauncher is created
  public void init()
  {
    // do nothing if button already created
    if (ui_initialized) return;

    // create the button
    // note: for some weird reasons, the callbacks can be called BEFORE this function return
    launcher_btn = ApplicationLauncher.Instance.AddApplication(null, null, null, null, null, null, launcher_icon);
    ui_initialized = true;

    // enable the launcher button for some scenes
    launcher_btn.VisibleInScenes =
        ApplicationLauncher.AppScenes.SPACECENTER
      | ApplicationLauncher.AppScenes.FLIGHT
      | ApplicationLauncher.AppScenes.MAPVIEW
      | ApplicationLauncher.AppScenes.TRACKSTATION
      | ApplicationLauncher.AppScenes.VAB
      | ApplicationLauncher.AppScenes.SPH;

    // toggle off launcher button & hide window after scene changes
    GameEvents.onGameSceneSwitchRequested.Add((GameEvents.FromToAction<GameScenes, GameScenes> _) => hide());

    // hide on launch screen spawn to avoid window visible during vessel load
    GameEvents.onGUILaunchScreenSpawn.Add((GameEvents.VesselSpawnInfo _) => hide());

    // hide and remember state on 'minor scenes' spawn
    GameEvents.onGUIAdministrationFacilitySpawn.Add(hide_and_remember);
    GameEvents.onGUIAstronautComplexSpawn.Add(hide_and_remember);
    GameEvents.onGUIMissionControlSpawn.Add(hide_and_remember);
    GameEvents.onGUIRnDComplexSpawn.Add(hide_and_remember);
    GameEvents.onHideUI.Add(hide_and_remember);

    // restore previous window state on 'minor scenes' despawn
    GameEvents.onGUIAdministrationFacilityDespawn.Add(show_again);
    GameEvents.onGUIAstronautComplexDespawn.Add(show_again);
    GameEvents.onGUIMissionControlDespawn.Add(show_again);
    GameEvents.onGUIRnDComplexDespawn.Add(show_again);
    GameEvents.onShowUI.Add(show_again);
  }


  // called every frame
  public void OnGUI()
  {
    // do nothing if GUI has not been initialized
    if (!ui_initialized) return;

    // render the window
    if ((launcher_btn.toggleButton.Value || launcher_btn.IsHovering) && (Lib.SceneIsGame() || HighLogic.LoadedScene == GameScenes.EDITOR))
    {
      // hard-coded offsets
      const float at_top_offset_x = 40.0f;
      const float at_top_offset_y = 0.0f;
      const float at_bottom_offset_x = 0.0f;
      const float at_bottom_offset_y = 40.0f;
      const float at_bottom_editor_offset_x = 66.0f;

      // get screen size
      float screen_width = (float)Screen.width;
      float screen_height = (float)Screen.height;

      // determine app launcher position;
      bool is_at_top = ApplicationLauncher.Instance.IsPositionedAtTop;

      // get window size
      float width = HighLogic.LoadedSceneIsEditor ? planner.width() : monitor.width();
      float height = HighLogic.LoadedSceneIsEditor ? planner.height() : monitor.height();

      // calculate window position
      float left = screen_width - width;
      float top = is_at_top ? 0.0f : screen_height - height;
      if (is_at_top)
      {
        left -= at_top_offset_x;
        top += at_top_offset_y;
      }
      else
      {
        left -= !HighLogic.LoadedSceneIsEditor ? at_bottom_offset_x : at_bottom_editor_offset_x;
        top -= at_bottom_offset_y;
      }

      // store window geometry
      Rect win_rect = new Rect(left, top, width, height);

      // begin window area
      // note: we don't use GUILayout.Window, because it is evil
      GUILayout.BeginArea(win_rect, window_style);

      // a bit of spacing between title and content
      GUILayout.Space(10.0f);

      // draw planner in the editors, monitor everywhere else
      if (!HighLogic.LoadedSceneIsEditor) monitor.render();
      else planner.render();

      // end window area
      GUILayout.EndArea();

      // draw tooltips if any
      if (GUI.tooltip.Length > 0) draw_tooltip(GUI.tooltip, win_rect);
    }
  }


  // draw tooltip
  void draw_tooltip(string msg, Rect win_rect)
  {
    // set alignment
    if (msg.IndexOf("<align=left />", StringComparison.Ordinal) != -1)
    { tooltip_style.alignment = TextAnchor.MiddleLeft; msg = msg.Replace("<align=left />", ""); }
    else if (msg.IndexOf("<align=right />", StringComparison.Ordinal) != -1)
    { tooltip_style.alignment = TextAnchor.MiddleRight; msg = msg.Replace("<align=right />", ""); }
    else tooltip_style.alignment = TextAnchor.MiddleCenter;

    // get mouse pos
    Vector2 mouse_pos = Mouse.screenPos;

    // calculate tooltip size
    GUIContent tooltip_content = new GUIContent(msg);
    Vector2 tooltip_size = tooltip_style.CalcSize(tooltip_content);
    tooltip_size.y = tooltip_style.CalcHeight(tooltip_content, tooltip_size.x);

    // calculate tooltip position
    Rect tooltip_rect = new Rect(mouse_pos.x - tooltip_size.x / 2, mouse_pos.y - tooltip_size.y / 2, tooltip_size.x, tooltip_size.y);

    // get the mouse out of the way
    tooltip_rect.yMin -= tooltip_size.y / 2;
    tooltip_rect.yMax -= tooltip_size.y / 2;

    // clamp to screen
    float offset_x = Math.Max(0.0f, -tooltip_rect.xMin) + Math.Min(0.0f, Screen.width - tooltip_rect.xMax);
    float offset_y = Math.Max(0.0f, -tooltip_rect.yMin) + Math.Min(0.0f, Screen.height - tooltip_rect.yMax);
    tooltip_rect.xMin += offset_x;
    tooltip_rect.xMax += offset_x;
    tooltip_rect.yMin += offset_y;
    tooltip_rect.yMax += offset_y;

    // finally, render the tooltip
    var prev_style = GUI.skin.label;
    GUI.skin.label = tooltip_style;
    GUI.Label(tooltip_rect, msg);
    GUI.skin.label = prev_style;
  }


  // hide the window
  public void hide()
  {
    launcher_btn.toggleButton.Value = false;
  }


  // hide the window and remember last state
  public void hide_and_remember()
  {
    last_show_window = launcher_btn.toggleButton.Value;
    launcher_btn.toggleButton.Value = false;
  }


  // show the window if was visible at time of last call to hide_and_remember()
  public void show_again()
  {
    launcher_btn.toggleButton.Value = last_show_window;
  }
}


} // KERBALISM
