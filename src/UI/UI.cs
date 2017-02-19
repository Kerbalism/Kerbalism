using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public static class UI
{
  public static void init()
  {
    // create subsystems
    message  = new Message();
    launcher = new Launcher();
    window   = new Window(260u, 280u, 100u);
  }

  public static void update(bool show_window)
  {
    // if gui should be shown
    if (show_window)
    {
      // as a special case, the first time the user enter
      // map-view/tracking-station we open the body info window
      if (MapView.MapIsEnabled && !first_time_mapview)
      {
        open(BodyInfo.body_info);
        first_time_mapview = true;
      }

      // update subsystems
      launcher.update();
      window.update();
    }

    // re-enable camera mouse scrolling, as some of the on_gui functions can
    // disable it on mouse-hover, but can't re-enable it again consistently
    // (eg: you mouse-hover and then close the window with the cursor still inside it)
    // - we are ignoring user preference on mouse wheel
    GameSettings.AXIS_MOUSEWHEEL.primary.scale = 1.0f;
  }

  public static void on_gui(bool show_window)
  {
    // render subsystems
    message.on_gui();
    if (show_window)
    {
      launcher.on_gui();
      window.on_gui();
    }
  }

  public static void open(Action<Panel> refresh)
  {
    window.open(refresh);
  }

  static Message  message;
  static Launcher launcher;
  static Window   window;
  static bool     first_time_mapview;
}


} // KERBALISM

