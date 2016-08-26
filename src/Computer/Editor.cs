// ====================================================================================================================
// edit text files on computers
// ====================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class Editor
{
  // ctor
  public Editor()
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
    win_style.normal.background = Lib.GetTexture("gray-background");
    top_style = new GUIStyle();
    top_style.fixedHeight = top_height;
    top_style.fontStyle = FontStyle.Bold;
    top_style.alignment = TextAnchor.MiddleCenter;
    top_style.normal.textColor = Color.black;
    top_style.stretchWidth = true;
    top_style.stretchHeight = true;
    txt_style = new GUIStyle();
    txt_style.fontSize = 12;
    txt_style.wordWrap = true;
    txt_style.normal.textColor = Color.black;
    txt_style.stretchWidth = true;
    txt_style.stretchHeight = true;
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
    // forget vessel doesn't exist anymore
    if (vessel_id != Guid.Empty && FlightGlobals.Vessels.Find(k => k.id == vessel_id) == null)
    {
      vessel_id = Guid.Empty;
    }

    // forget file if it doesn't exist anymore
    if (vessel_id != Guid.Empty && !DB.VesselData(vessel_id).computer.files.ContainsKey(filename))
    {
      filename = string.Empty;
    }

    // do nothing if there isn't a vessel or a filename specified
    if (vessel_id == Guid.Empty || filename.Length == 0) return;

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
    }
  }


  // draw the window
  void render(int id)
  {
    // draw pseudo-title
    GUILayout.BeginHorizontal();
    GUILayout.Label(close_left, icon_left_style);
    GUILayout.Label("EDITOR", top_style);
    GUILayout.Label(close_right, icon_right_style);
    if (Lib.IsClicked()) Close();
    GUILayout.EndHorizontal();

    // draw the text area
    // note: when user hit close, vessel/filename is not valid
    // note: file is guaranteed to exist at this point, if vessel/filename is valid
    if (vessel_id != Guid.Empty && filename.Length > 0)
    {
      File file = DB.VesselData(vessel_id).computer.files[filename];
      scroll_pos = GUILayout.BeginScrollView(scroll_pos, HighLogic.Skin.horizontalScrollbar, HighLogic.Skin.verticalScrollbar);
      file.content = GUILayout.TextArea(file.content, txt_style);
      GUILayout.EndScrollView();
    }

    // enable dragging
    GUI.DragWindow(drag_rect);
  }


  // show the console
  public static void Open(Vessel v, string filename)
  {
    // setting file show the window
    instance.vessel_id = v.id;
    instance.filename = filename;

    // create file if it doesn't exist
    var files = DB.VesselData(v.id).computer.files;
    File file;
    if (!files.TryGetValue(filename, out file))
    {
      file = new File();
      files.Add(filename, file);
    }
  }


  // close the console
  public static void Close()
  {
    // resetting file hide the window
    instance.vessel_id = Guid.Empty;
    instance.filename = string.Empty;
  }


  // toggle the console
  public static void Toggle(Vessel v, string filename)
  {
    // if file is different, show it
    // if file is the same, hide it
    if (instance.vessel_id == v.id && instance.filename == filename) Close();
    else Open(v, filename);
  }


  // constants
  const float width = 260.0f;
  const float height = 366.0f;
  const float top_height = 20.0f;
  const float margin = 10.0f;
  const float spacing = 10.0f;

  // styles
  GUIStyle win_style;
  GUIStyle top_style;
  GUIStyle txt_style;
  GUIStyle icon_left_style;
  GUIStyle icon_right_style;

  // textures
  Texture2D close_left = Lib.GetTexture("close-empty");
  Texture2D close_right = Lib.GetTexture("close-black");

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

  // store name of file being edited
  string filename;

  // permit global access
  static Editor instance;
}


} // KERBALISM