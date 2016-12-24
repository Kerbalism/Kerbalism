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
    window   = new Window(260u, 80u, 80u);
  }

  public static void update()
  {
    // update subsystems
    launcher.update();
    window.update();

    // re-enable camera mouse scrolling, as some of the on_gui functions can
    // disable it on mouse-hover, but can't re-enable it again consistently
    // (eg: you mouse-hover and then close the window with the cursor still inside it)
    // - we are ignoring user preference on mouse wheel
    GameSettings.AXIS_MOUSEWHEEL.primary.scale = 1.0f;
  }

  public static void on_gui()
  {
    // render subsystems
    message.on_gui();
    launcher.on_gui();
    window.on_gui();
  }

  public static void open(Action<Panel> refresh)
  {
    window.open(refresh);
  }

  static Message  message;
  static Launcher launcher;
  static Window   window;
}


} // KERBALISM

