using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public static class UI
{
  public static void init()
  {
    message  = new Message();
    launcher = new Launcher();
    window   = new Window(260u, 80u, 80u);
  }

  public static void update()
  {
    launcher.update();
    window.update();
  }

  public static void on_gui()
  {
    // re-enable camera mouse scrolling, as some of the following functions can
    // disable it on mouse-hover, but can't re-enable it again consistently
    // (eg: you mouse-hover and then close the window with the cursor still inside it)
    // note: we are ignoring user preference on mouse wheel
    GameSettings.AXIS_MOUSEWHEEL.primary.scale = 1.0f;

    // render the messages
    message.on_gui();

    // render the launcher
    launcher.on_gui();

    // render the window
    window.on_gui();
  }

  public static void open(float width, string title, Action<Panel> refresh)
  {
    window.open(width, title, refresh);
  }

  static Message  message;
  static Launcher launcher;
  static Window   window;
}


} // KERBALISM

