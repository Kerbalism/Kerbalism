using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public static class UI
{
  public static void init()
  {
    launcher   = new Launcher();
    info       = new Info();
    body_info  = new BodyInfo();
    message    = new Message();
    notepad    = new Notepad();
    fileman    = new FileManager();
    devman     = new DevManager();
  }


  public static void on_gui()
  {
    // re-enable camera mouse scrolling, as some of the following functions can
    // disable it on mouse-hover, but can't re-enable it again consistently
    // (eg: you mouse-hover and then close the window with the cursor still inside it)
    // note: we are ignoring user preference on mouse wheel
    GameSettings.AXIS_MOUSEWHEEL.primary.scale = 1.0f;

    // always render the launcher
    launcher.on_gui();

    // always render the messages
    message.on_gui();

    // outside the editor
    if (!Lib.IsEditor())
    {
      // do nothing if GUI should be hidden
      if (!launcher.must_hide_ui)
      {
        // render subsystems
        info.on_gui();
        body_info.on_gui();
        notepad.on_gui();
        fileman.on_gui();
        devman.on_gui();
      }
    }
  }


  static Launcher     launcher;
  static Info         info;
  static BodyInfo     body_info;
  static Message      message;
  static Notepad      notepad;
  static FileManager  fileman;
  static DevManager   devman;
}


} // KERBALISM

