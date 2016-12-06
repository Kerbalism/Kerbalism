using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;


namespace KERBALISM {


public abstract class Window
{
  // - width: window width in pixel
  // - left: initial window horizontal position
  // - right: initial window vertical position
  // - drag_height: height of the drag area on top of the window
  // - style: gui style to use for the window
  protected Window(uint width, uint left, uint top, uint drag_height, GUIStyle style)
  {
    // generate unique id
    win_id = Lib.RandomInt(int.MaxValue);

    // setup window geometry
    win_rect = new Rect((float)left, (float)top, (float)width, 0.0f);

    // setup dragbox geometry
    drag_rect = new Rect(0.0f, 0.0f, width, drag_height);

    // remember style
    win_style = style;

    // initialize tooltip utility
    tooltip = new Tooltip();
  }

  public void on_gui()
  {
    // prepare the window if necessary, do not render it if specified
    if (!prepare()) return;

    // set automatic height
    win_rect.height = height();

    // clamp the window to the screen, so it can't be dragged outside
    float offset_x = Math.Max(0.0f, -win_rect.xMin) + Math.Min(0.0f, Screen.width - win_rect.xMax);
    float offset_y = Math.Max(0.0f, -win_rect.yMin) + Math.Min(0.0f, Screen.height - win_rect.yMax);
    win_rect.xMin += offset_x;
    win_rect.xMax += offset_x;
    win_rect.yMin += offset_y;
    win_rect.yMax += offset_y;

    // draw the window
    win_rect = GUILayout.Window(win_id, win_rect, draw_window, "", win_style);
  }

  void draw_window(int _)
  {
    // render the window content
    render();

    // draw tooltip
    tooltip.draw(win_rect);

    // enable dragging
    GUI.DragWindow(drag_rect);
  }

  public bool contains(Vector2 pos)
  {
    return win_rect.Contains(pos);
  }

  // prepare the window before rendering, return false to prevent it
  public abstract bool prepare();

  // draw the window content
  public abstract void render();

  // return window height
  public abstract float height();

  // store window id
  int win_id;

  // store window geometry
  Rect win_rect;

  // store dragbox geometry
  Rect drag_rect;

  // window style
  GUIStyle win_style;

  // tooltip utility
  Tooltip tooltip;
}


} // KERBALISM



