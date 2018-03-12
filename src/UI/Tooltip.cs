using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class Tooltip
{
  public Tooltip()
  {
    tooltip = string.Empty;
  }

  public void draw(Rect screen_rect)
  {
    // draw tooltip, if there is one specified
    if (tooltip.Length > 0) render_tooltip(screen_rect);

    // get tooltip
    // - need to be after the drawing, to play nice with unity layout engine
    get_tooltip();
  }

  void get_tooltip()
  {
    // get current tooltip
    if (Event.current.type == EventType.Repaint)
    {
      tooltip = GUI.tooltip;

      // set alignment
      if (tooltip.Length > 0)
      {
        if (tooltip.IndexOf("<align=left />", StringComparison.Ordinal) != -1)
        {
          Styles.tooltip.alignment = TextAnchor.MiddleLeft;
          tooltip = tooltip.Replace("<align=left />", "");
        }
        else if (tooltip.IndexOf("<align=right />", StringComparison.Ordinal) != -1)
        {
          Styles.tooltip.alignment = TextAnchor.MiddleRight;
          tooltip = tooltip.Replace("<align=right />", "");
        }
        else Styles.tooltip.alignment = TextAnchor.MiddleCenter;
      }
    }
  }


  void render_tooltip(Rect screen_rect)
  {
    // get mouse pos
    Vector2 mouse_pos = Mouse.screenPos;

    // correct for non-origin screen rect
    mouse_pos -= new Vector2(screen_rect.xMin, screen_rect.yMin);

    // calculate tooltip size
    GUIContent tooltip_content = new GUIContent(tooltip);
    Vector2 tooltip_size = Styles.tooltip.CalcSize(tooltip_content);
    tooltip_size.y = Styles.tooltip.CalcHeight(tooltip_content, tooltip_size.x);

    // calculate tooltip position
    Rect tooltip_rect = new Rect(mouse_pos.x - Mathf.Floor(tooltip_size.x / 2.0f), mouse_pos.y - Mathf.Floor(tooltip_size.y / 2.0f), tooltip_size.x, tooltip_size.y);

    // get the mouse out of the way
    tooltip_rect.yMin -= Mathf.Floor(tooltip_size.y / 2.0f);
    tooltip_rect.yMax -= Mathf.Floor(tooltip_size.y / 2.0f);

    // clamp to screen rect
    float offset_x = Math.Max(0.0f, -tooltip_rect.xMin) + Math.Min(0.0f, screen_rect.width - tooltip_rect.xMax);
    float offset_y = Math.Max(0.0f, -tooltip_rect.yMin) + Math.Min(0.0f, screen_rect.height - tooltip_rect.yMax);
    tooltip_rect.xMin += offset_x;
    tooltip_rect.xMax += offset_x;
    tooltip_rect.yMin += offset_y;
    tooltip_rect.yMax += offset_y;

    // finally, render the tooltip
    GUILayout.BeginArea(tooltip_rect, Styles.tooltip_container);
    GUILayout.Label(tooltip_content, Styles.tooltip);
    GUILayout.EndArea();
  }


  // tooltip text
  string tooltip;
}


} // KERBALISM

