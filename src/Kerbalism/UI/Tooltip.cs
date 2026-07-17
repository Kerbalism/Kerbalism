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
			window_id = Lib.RandomInt(int.MaxValue);
		}

		/// <summary>Capture the tooltip selected by controls in the current GUI window.</summary>
		public void Capture()
		{
			Get_tooltip();
		}

		/// <summary>Draw the captured tooltip in screen space.</summary>
		public void Draw(Rect parentRect, bool outsideParent = false)
		{
			if (tooltip.Length > 0) Render_tooltip(parentRect, outsideParent);
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


		void Render_tooltip(Rect parentRect, bool outsideParent)
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

			float x = mouse_pos.x - Mathf.Floor(tooltipWidth / 2.0f);
			float y;
			if (outsideParent)
			{
				// Configure dropdowns keep tooltips outside the window so they don't
				// cover nearby options.
				float gap = Styles.ScaleFloat(8.0f);
				float yAbove = parentRect.yMin - tooltipHeight - gap;
				float yBelow = parentRect.yMax + gap;
				y = yAbove >= margin ? yAbove : yBelow;
			}
			else
			{
				// Other windows retain the familiar cursor-relative placement.
				y = mouse_pos.y - tooltipHeight - Styles.ScaleFloat(10.0f);
				if (y < margin)
					y = mouse_pos.y + Styles.ScaleFloat(20.0f);
			}

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

