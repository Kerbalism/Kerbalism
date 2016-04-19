// ====================================================================================================================
// a simple notepad to take vessel notes
// ====================================================================================================================


using System;
using System.Collections.Generic;
using HighlightingSystem;
using UnityEngine;


namespace KERBALISM {


[KSPAddon(KSPAddon.Startup.MainMenu, true)]
public class Notepad : MonoBehaviour
{
  // constants
  const float width = 300.0f;
  const float height = 200.0f;
  const float top_height = 20.0f;
  const float bot_height = 20.0f;
  const float margin = 10.0f;
  const float spacing = 10.0f;

  // permit global access
  private static Notepad instance = null;

  // styles
  GUIStyle win_style;
  GUIStyle top_style;
  GUIStyle bot_style;
  GUIStyle txt_style;
  GUILayoutOption[] txt_options;

  // store window geometry
  Rect win_rect;

  // store dragbox geometry
  Rect drag_rect;

  // used by scroll window mechanics
  Vector2 scroll_pos;

  // store vessel id, if any
  Guid vessel_id;

  // ctor
  Notepad()
  {
    // enable global access
    instance = this;

    // keep it alive
    DontDestroyOnLoad(this);

    // setup window geometry
    win_rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

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
    win_rect = GUI.Window(66666666, win_rect, render, "", win_style);
  }


  // draw the window
  void render(int win_id)
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
    vd.notes = GUILayout.TextArea(vd.notes, txt_style, txt_options);
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


  // show the notepad
  public static void Open(Vessel v)
  {
    // setting vessel id show the window
    instance.vessel_id = v.id;
  }


  // close the notepad
  public static void Close()
  {
    // resetting vessel id hide the window
    instance.vessel_id = Guid.Empty;
  }


  // toggle the notepad
  public static void Toggle(Vessel v)
  {
    // if vessel is different or hidden, show it
    // if vessel is the same, hide it
    instance.vessel_id = (instance.vessel_id == v.id ? Guid.Empty : v.id);
  }

}


} // KERBALISM