// ====================================================================================================================
// interact with computers
// ====================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class Console
{
  // ctor
  public Console()
  {
    // enable global access
    instance = this;

    // generate unique id, hopefully
    win_id = Lib.RandomInt(int.MaxValue);

    // setup window geometry
    win_rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

    // setup dragbox geometry
    drag_rect = new Rect(0.0f, 0.0f, width, top_height);

    // setup styles
    win_style = new GUIStyle();
    win_style.padding.left = 10;
    win_style.padding.right = 10;
    win_style.padding.top = 0;
    win_style.padding.bottom = 5;
    win_style.normal.background = Lib.GetTexture("black-background");
    top_style = new GUIStyle();
    top_style.fixedHeight = top_height;
    top_style.fontStyle = FontStyle.Bold;
    top_style.alignment = TextAnchor.MiddleCenter;
    top_style.normal.textColor = Color.white;
    top_style.stretchWidth = true;
    top_style.stretchHeight = true;
    txt_style = new GUIStyle();
    txt_style.fontSize = 12;
    txt_style.wordWrap = true;
    txt_style.normal.textColor = Color.white;
    desc_style = new GUIStyle(txt_style);
    desc_style.normal.textColor = Color.gray;
    desc_style.alignment = TextAnchor.MiddleRight;
    cmd_style = new GUIStyle();
    cmd_style.fixedHeight = cmd_height;
    cmd_style.fontSize = 12;
    cmd_style.normal.textColor = Color.white;
    cmd_style.padding.top = 5;
    cmd_style.richText = false;
    icon_left_style = new GUIStyle();
    icon_left_style.alignment = TextAnchor.MiddleLeft;
    icon_left_style.stretchWidth = false;
    icon_left_style.fixedWidth = 16;
    icon_right_style = new GUIStyle();
    icon_right_style.alignment = TextAnchor.MiddleRight;
    icon_right_style.stretchWidth = false;
    icon_right_style.fixedWidth = 16;
  }


  // called every frame
  public void on_gui()
  {
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
    win_rect = GUILayout.Window(win_id, win_rect, render, "", win_style);

    // disable camera mouse scrolling on mouse over
    if (win_rect.Contains(Event.current.mousePosition))
    {
      GameSettings.AXIS_MOUSEWHEEL.primary.scale = 0.0f;

      // auto focus on window click
      if (Input.GetMouseButtonDown(0) && GUIUtility.hotControl == 0)
      {
        GUI.FocusControl("console_prompt");
      }
    }
  }


  // draw the window
  void render(int id)
  {
    // draw pseudo-title
    GUILayout.BeginHorizontal();
    GUILayout.Label(close_left, icon_left_style);
    GUILayout.Label("CONSOLE", top_style);
    GUILayout.Label(close_right, icon_right_style);
    if (Lib.IsClicked()) Close();
    GUILayout.EndHorizontal();

    // trim the buffer if necessary
    if (buffer.Count > buffer_lines) buffer.RemoveRange(0, buffer_lines - terminal_lines);

    // draw buffer lines
    scroll_pos = GUILayout.BeginScrollView(scroll_pos, HighLogic.Skin.horizontalScrollbar, HighLogic.Skin.verticalScrollbar);
    for(int i = Math.Max(buffer.Count - terminal_lines, 0); i < buffer.Count; ++i)
    {
      var split = buffer[i].Split('#');
      GUILayout.BeginHorizontal();
      GUILayout.Label(split[0].TrimEnd(), txt_style);
      if (split.Length > 1) GUILayout.Label(split[1].Trim(), desc_style);
      GUILayout.EndHorizontal();
    }
    GUILayout.EndScrollView();

    // detect ENTER
    Event e = Event.current;
    if (e.type == EventType.keyDown && e.keyCode == KeyCode.Return)
    {
      exec();
    }

    // draw command prompt
    GUILayout.BeginHorizontal();
    GUI.SetNextControlName("console_prompt");
    prompt = GUILayout.TextField(prompt, cmd_style);
    GUILayout.EndHorizontal();

    // enable dragging
    GUI.DragWindow(drag_rect);
  }


  void exec()
  {
    // get the computer
    Computer cpu = DB.VesselData(vessel_id).computer;

    // get the environment
    Vessel environment = FlightGlobals.Vessels.Find(k => k.id == vessel_id);

    // execute current prompt
    cpu.execute(prompt, environment);

    // write the command string on the buffer
    buffer.Add(Lib.BuildString(">> ", prompt));

    // special outputs
    if (cpu.output.IndexOf('!') == 0)
    {
      var split = cpu.output.Substring(1).Split(' ');
      switch(split[0])
      {
        case "EDIT":
          Editor.Open(environment, split[1]);
          break;

        case "SWITCH":
          Open(FlightGlobals.Vessels.Find(k => k.id == new Guid(split[1])));
          break;

        case "CLEAR":
          buffer.Clear();
          break;

        case "EXIT":
          buffer.Clear();
          Close();
          break;
      }
    }
    // normal output
    else
    {
      // write the output on the buffer
      // note: we deal with multi-line success outputs
      var split = cpu.output.Split('\n');
      foreach(string s in split)
      {
        if (cpu.status) buffer.Add(s);
        else buffer.Add(Lib.BuildString("<color=red>", s, "</color>"));
      }
      if (cpu.output.Length > 0) buffer.Add(string.Empty);
    }

    // reset prompt
    prompt = string.Empty;

    // move the scroll bar at the bottom
    scroll_pos.y = 9999.0f;
  }


  void banner(Vessel v)
  {
    string vessel_name = Lib.BuildString("<b>", Lib.Epsilon(v.vesselName, 20), "</b>");
    string situation_name = Lib.BuildString(v.situation.ToString().ToLower(), " on <b>", Lib.Epsilon(v.mainBody.bodyName, 10), "</b>");

    // print banner
    buffer.Add(Lib.BuildString("  ╔═╗╔═╗   ", vessel_name));
    buffer.Add(Lib.BuildString("  ║<color=black>D</color>║╚═╗   ", situation_name));
    buffer.Add("  ╚═╝╚═╝   <b>WELCOME</b>");
    buffer.Add(string.Empty);
  }


  // show the console
  public static void Open(Vessel v)
  {
    // setting id show the window
    instance.vessel_id = v.id;

    // clear prompt
    instance.prompt = string.Empty;

    // clear buffer
    instance.buffer.Clear();

    // print banner
    instance.banner(v);
  }


  // close the console
  public static void Close()
  {
    // resetting id hide the window
    instance.vessel_id = Guid.Empty;
  }


  // toggle the console
  public static void Toggle(Vessel v)
  {
    // if id is different, show it
    // if id is the same, hide it
    instance.vessel_id = (instance.vessel_id == v.id ? Guid.Empty : v.id);

    // if the computer_id was changed
    if (instance.vessel_id == v.id)
    {
      // clear prompt
      instance.prompt = string.Empty;

      // clear buffer
      instance.buffer.Clear();

      // print banner
      instance.banner(v);
    }
  }


  // constants
  const float width = 260.0f;
  const float height = 366.0f;
  const float top_height = 20.0f;
  const float cmd_height = 20.0f;
  const float margin = 10.0f;
  const float spacing = 10.0f;

  // styles
  GUIStyle win_style;
  GUIStyle top_style;
  GUIStyle txt_style;
  GUIStyle desc_style;
  GUIStyle cmd_style;
  GUIStyle icon_left_style;
  GUIStyle icon_right_style;

  // textures
  Texture2D close_left = Lib.GetTexture("close-empty");
  Texture2D close_right = Lib.GetTexture("close-white");

  // store window id
  int win_id;

  // store window geometry
  Rect win_rect;

  // store dragbox geometry
  Rect drag_rect;

  // used by scroll area
  Vector2 scroll_pos;

  // store vessel id
  Guid vessel_id;

  // store the current prompt
  string prompt = "";

  // store the console buffer
  List<string> buffer = new List<string>();

  // permit global access
  static Console instance;

  // max number of lines displayed
  const int terminal_lines = 128;

  // max number of lines in the buffer
  const int buffer_lines = 256;
}


} // KERBALISM