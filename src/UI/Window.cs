using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;


namespace KERBALISM {


// a window containing a panel
public sealed class Window
{
  // - width: window width in pixel
  // - left: initial window horizontal position
  // - right: initial window vertical position
  // - drag_height: height of the drag area on top of the window
  // - style: gui style to use for the window
  public Window(uint width, uint left, uint top)//, GUIStyle style)
  {
    // generate unique id
    win_id = Lib.RandomInt(int.MaxValue);

    // setup window geometry
    win_rect = new Rect((float)left, (float)top, (float)width, 0.0f);

    // setup dragbox geometry
    drag_rect = new Rect(0.0f, 0.0f, width, 20.0f);

    // initialize tooltip utility
    tooltip = new Tooltip();

    // initial title
    title = string.Empty;
  }

  public void open(float width, string title, Action<Panel> refresh)
  {
    win_rect.width = width;
    drag_rect.width = width;

    this.title = title;

    this.refresh = refresh;
  }

  public void close()
  {
    refresh = null;
    panel = null;
  }

  public void update()
  {
    if (refresh != null)
    {
      if (panel == null) panel = new Panel();
      else panel.clear();
      refresh(panel);
      if (panel.empty()) close();
    }
  }

  public void on_gui()
  {
    // window is considered closed if panel is null
    if (panel == null) return;

    // set automatic height
    win_rect.height = 20.0f + panel.height();

    // clamp the window to the screen, so it can't be dragged outside
    float offset_x = Math.Max(0.0f, -win_rect.xMin) + Math.Min(0.0f, Screen.width - win_rect.xMax);
    float offset_y = Math.Max(0.0f, -win_rect.yMin) + Math.Min(0.0f, Screen.height - win_rect.yMax);
    win_rect.xMin += offset_x;
    win_rect.xMax += offset_x;
    win_rect.yMin += offset_y;
    win_rect.yMax += offset_y;

    // draw the window
    win_rect = GUILayout.Window(win_id, win_rect, draw_window, "", Styles.win);
  }

  void draw_window(int _)
  {
    // render window title
    GUILayout.BeginHorizontal(Styles.title_container);
    GUILayout.Label(Icons.empty, Styles.left_icon);
    GUILayout.Label(title, Styles.title_text);
    GUILayout.Label(Icons.close, Styles.right_icon);
    bool b = Lib.IsClicked();
    GUILayout.EndHorizontal();
    if (b) { close(); return; }

    // render the window content
    panel.render();

    // draw tooltip
    tooltip.draw(win_rect);

    // enable dragging
    GUI.DragWindow(drag_rect);
  }

  public bool contains(Vector2 pos)
  {
    return win_rect.Contains(pos);
  }

  // store window id
  int win_id;

  // store window geometry
  Rect win_rect;

  // store dragbox geometry
  Rect drag_rect;

  // tooltip utility
  Tooltip tooltip;

  // panel
  Panel panel;

  // refresh function
  Action<Panel> refresh;

  // window title
  string title;
}


} // KERBALISM



