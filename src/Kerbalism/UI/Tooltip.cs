using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{


	public sealed class Tooltip
	{
		public Tooltip()
		{
			tooltip = string.Empty;
		}

		public void Draw(Rect screen_rect)
		{
			// draw tooltip, if there is one specified
			if (tooltip.Length > 0) Render_tooltip(screen_rect);

			// get tooltip
			// - need to be after the drawing, to play nice with unity layout engine
			Get_tooltip();
		}

		void Get_tooltip()
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


		void Render_tooltip(Rect screen_rect)
		{
			// get mouse pos
			Vector2 mouse_pos = Mouse.screenPos;

			// correct for non-origin screen rect
			mouse_pos -= new Vector2(screen_rect.xMin, screen_rect.yMin);

			// calculate tooltip size. CalcSize gives the natural width; cap to popup width
			// so long text wraps inside the window. If wrapping actually kicks in, widen to
			// the full popup width so the wrapped text uses available horizontal space
			// instead of stacking up vertically.
			GUIContent tooltip_content = new GUIContent(tooltip);
			Vector2 tooltip_size = Styles.tooltip.CalcSize(tooltip_content);
			float max_width = Math.Max(50f, screen_rect.width - 20f);
			if (tooltip_size.x > max_width) tooltip_size.x = max_width;
			tooltip_size.y = Styles.tooltip.CalcHeight(tooltip_content, tooltip_size.x);
			float single_line_h = Styles.tooltip.CalcHeight(new GUIContent("X"), max_width);
			if (tooltip_size.y > single_line_h * 1.25f && tooltip_size.x < max_width)
			{
				tooltip_size.x = max_width;
				tooltip_size.y = Styles.tooltip.CalcHeight(tooltip_content, tooltip_size.x);
			}

			// calculate tooltip position, default vertical position is above the cursor
			Rect tooltip_rect = new Rect(mouse_pos.x - Mathf.Floor(tooltip_size.x / 2.0f), mouse_pos.y - tooltip_size.y - 10.0f, tooltip_size.x, tooltip_size.y);

			// if vertical position above cursor goes outside the screen, change that to below the cursor
			if (tooltip_rect.yMin < 0f)
			{
				tooltip_rect.yMin = mouse_pos.y + 20f;
				tooltip_rect.yMax = tooltip_rect.yMin + tooltip_size.y;
			}

			// horizontal position : clamp to screen rect
			float offset_x = Math.Max(0.0f, -tooltip_rect.xMin) + Math.Min(0.0f, screen_rect.width - tooltip_rect.xMax);
			tooltip_rect.xMin += offset_x;
			tooltip_rect.xMax += offset_x;

			// finally, render the tooltip
			GUILayout.BeginArea(tooltip_rect, Styles.tooltip_container);
			GUILayout.Label(tooltip_content, Styles.tooltip);
			GUILayout.EndArea();
		}


		// tooltip text
		string tooltip;
	}


} // KERBALISM

