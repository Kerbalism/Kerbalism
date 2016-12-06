using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.UI.Screens;


namespace KERBALISM {


public sealed class Launcher
{
  // ctor
  public Launcher()
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
    GameEvents.onGameSceneSwitchRequested.Add((_) => hide());

    // hide on launch screen spawn to avoid window visible during vessel load
    GameEvents.onGUILaunchScreenSpawn.Add((_) => hide());

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
  public void on_gui()
  {
    // do nothing if GUI has not been initialized
    if (!ui_initialized) return;

    // render the window
    if (launcher_btn.toggleButton.Value || launcher_btn.IsHovering || (win_rect.width > 0 && win_rect.Contains(Mouse.screenPos)))
    {
      // hard-coded offsets
      // note: there is a bug in stock that only set appscale properly in non-flight-mode after you go in flight-mode at least once
      float at_top_offset_x = 40.0f * GameSettings.UI_SCALE_APPS;
      float at_top_offset_y = 0.0f * GameSettings.UI_SCALE_APPS;
      float at_bottom_offset_x = 0.0f * GameSettings.UI_SCALE_APPS;
      float at_bottom_offset_y = 40.0f * GameSettings.UI_SCALE_APPS;
      float at_bottom_editor_offset_x = 66.0f * GameSettings.UI_SCALE_APPS;

      // get screen size
      float screen_width = (float)Screen.width;
      float screen_height = (float)Screen.height;

      // determine app launcher position;
      bool is_at_top = ApplicationLauncher.Instance.IsPositionedAtTop;

      // get window size
      float width = Lib.IsEditor() ? planner.width() : monitor.width();
      float height = Lib.IsEditor() ? planner.height() : monitor.height();

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
        left -= !Lib.IsEditor() ? at_bottom_offset_x : at_bottom_editor_offset_x;
        top -= at_bottom_offset_y;
      }

      // store window geometry
      win_rect = new Rect(left, top, width, height);

      // begin window area
      // note: we don't use GUILayout.Window, because it is evil
      GUILayout.BeginArea(win_rect, window_style);

      // a bit of spacing between title and content
      GUILayout.Space(10.0f);

      // draw planner in the editors, monitor everywhere else
      if (!Lib.IsEditor()) monitor.render();
      else planner.render();

      // end window area
      GUILayout.EndArea();

      // draw tooltip
      tooltip.draw(new Rect(0.0f, 0.0f, Screen.width, Screen.height));

      // disable camera mouse scrolling on mouse over
      if (win_rect.Contains(Event.current.mousePosition))
      {
        GameSettings.AXIS_MOUSEWHEEL.primary.scale = 0.0f;
      }
    }
    else
    {
      // set zero area win_rect
      win_rect.width = 0;
    }
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

    must_hide_ui = true;
  }


  // show the window if was visible at time of last call to hide_and_remember()
  public void show_again()
  {
    launcher_btn.toggleButton.Value = last_show_window;

    must_hide_ui = false;
  }


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

  // window geometry
  Rect win_rect;

  // the vessel planner
  Planner planner = new Planner();

  // the vessel monitor
  Monitor monitor = new Monitor();

  // tooltip utility
  Tooltip tooltip = new Tooltip();

  // used by engine to hide other windows when appropriate
  public bool must_hide_ui;
}


} // KERBALISM
