using System;
using UnityEngine;


namespace KERBALISM
{


	public sealed class Tooltip
	{
		public Tooltip()
		{
			tooltip = string.Empty;
			window_id = Lib.RandomInt(int.MaxValue);
		}

		/// <summary>Capture the tooltip selected by controls in the current GUI window.</summary>
		public void Capture()
		{
			Get_tooltip();
		}

		/// <summary>Draw the captured tooltip in screen space, anchored to the mouse.</summary>
		public void Draw()
		{
			if (tooltip.Length > 0) Render_tooltip();
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


		void Render_tooltip()
		{
			// Input.mousePosition is bottom-left based, while IMGUI screen coordinates
			// are top-left based. Convert explicitly instead of relying on Mouse.screenPos,
			// whose coordinate space differs between GUI.Window and screen-level drawing.
			Vector2 mouse_pos = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);

			GUIContent tooltip_content = new GUIContent(tooltip);
			float margin = Styles.ScaleFloat(8.0f);
			float screenWidth = Screen.width - margin * 2.0f;
			float preferredMaxWidth = Mathf.Min(Styles.ScaleWidthFloat(420.0f), screenWidth);
			float naturalWidth = Styles.tooltip.CalcSize(tooltip_content).x;
			float tooltipWidth = Mathf.Clamp(naturalWidth, Styles.ScaleWidthFloat(80.0f), preferredMaxWidth);
			float tooltipHeight = Styles.tooltip.CalcHeight(tooltip_content, tooltipWidth);
			float maxHeight = Screen.height - margin * 2.0f;

			// Only widen beyond the preferred cap when that is necessary to keep all
			// wrapped text visible vertically.
			while (tooltipHeight > maxHeight && tooltipWidth < screenWidth)
			{
				tooltipWidth = Mathf.Min(tooltipWidth * 1.25f, screenWidth);
				tooltipHeight = Styles.tooltip.CalcHeight(tooltip_content, tooltipWidth);
			}
			tooltipHeight = Mathf.Min(tooltipHeight, maxHeight);

			// Prefer the lower-right of the cursor so the hovered control stays visible.
			// Flip independently on each axis when the preferred side doesn't fit.
			float cursorGap = Styles.ScaleFloat(16.0f);
			float right = mouse_pos.x + cursorGap;
			float left = mouse_pos.x - tooltipWidth - cursorGap;
			float below = mouse_pos.y + cursorGap;
			float above = mouse_pos.y - tooltipHeight - cursorGap;
			float x = right + tooltipWidth <= Screen.width - margin ? right : left;
			float y = below + tooltipHeight <= Screen.height - margin ? below : above;

			// Keep the final rect fully on-screen on both axes.
			x = Mathf.Clamp(x, margin, Screen.width - tooltipWidth - margin);
			y = Mathf.Clamp(y, margin, Screen.height - tooltipHeight - margin);

			tooltip_rect = new Rect(x, y, tooltipWidth, tooltipHeight);
			GUI.Window(window_id, tooltip_rect, DrawTooltipWindow, string.Empty, GUIStyle.none);
			GUI.BringWindowToFront(window_id);
		}

		void DrawTooltipWindow(int _)
		{
			GUI.Label(new Rect(0.0f, 0.0f, tooltip_rect.width, tooltip_rect.height), tooltip, Styles.tooltip);
		}

		// tooltip text
		string tooltip;
		readonly int window_id;
		Rect tooltip_rect;
	}


} // KERBALISM
