using System;
using System.Text;
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

		/// <summary>Start a tooltip scope: call before drawing the controls whose tooltip Capture() should pick up.</summary>
		public void Begin()
		{
			// Unity 6's IMGUI keeps GUI.tooltip across repaints and windows (Unity 2019 reset it), so once set a
			// tooltip never went away and leaked into other windows. Clear it here; a hovered control sets it again.
			if (Event.current.type == EventType.Repaint) GUI.tooltip = string.Empty;
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

					tooltip = Expand_tabs(tooltip);
				}
			}
		}

		// Unity 6's IMGUI uses other tab stops than Unity 2019 and wraps tab-aligned lines it measured as fitting, so
		// tooltip columns came out misaligned and clipped. Replace each tab with spaces up to the next Unity 2019 tab
		// stop: 16 space widths, with the space width rounded as 2019's text layout rounded it.
		string Expand_tabs(string text)
		{
			if (text.IndexOf('\t') < 0) return text;
			if (text == expanded_source) return expanded_text;

			GUIStyle style = Styles.tooltip;
			float space = Text_width(style, "x x") - Text_width(style, "xx");
			float stop = 16.0f * Mathf.Floor(space + 0.5f);
			StringBuilder result = new StringBuilder(text.Length + 64);
			string[] lines = text.Split('\n');
			for (int l = 0; l < lines.Length; l++)
			{
				string[] columns = lines[l].Split('\t');
				StringBuilder line = new StringBuilder(columns[0]);
				for (int c = 1; c < columns.Length; c++)
				{
					float width = Text_width(style, line.ToString());
					float next = (Mathf.Floor(width / stop) + 1.0f) * stop;
					int start = line.Length;
					int count = Math.Max(1, Mathf.RoundToInt((next - width) / space));
					line.Append(' ', count);

					// spaces inside <b> are wider than plain ones: redo the count with the width they have here
					float each = (Text_width(style, line.ToString()) - width) / count;
					int better = each > 0.0f ? Math.Max(1, Mathf.RoundToInt((next - width) / each)) : count;
					if (better != count)
					{
						line.Length = start;
						line.Append(' ', better);
					}
					line.Append(columns[c]);
				}
				if (l > 0) result.Append('\n');
				result.Append(line);
			}
			expanded_source = text;
			expanded_text = result.ToString();
			return expanded_text;
		}

		static float Text_width(GUIStyle style, string text)
		{
			return text.Length == 0 ? 0.0f : style.CalcSize(new GUIContent(text)).x - style.padding.horizontal;
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
			float maxHeight = Screen.height - margin * 2.0f;

			// Unity 6's IMGUI can wrap a line at the very width it measured it as fitting, which clipped tooltips, so
			// only word-wrap the ones wider than the cap and draw the rest unwrapped.
			if (draw_style == null) draw_style = new GUIStyle(Styles.tooltip);
			draw_style.alignment = Styles.tooltip.alignment;
			draw_style.wordWrap = false;
			Vector2 naturalSize = draw_style.CalcSize(tooltip_content);
			float tooltipWidth;
			float tooltipHeight;
			if (naturalSize.x <= preferredMaxWidth)
			{
				tooltipWidth = Mathf.Max(naturalSize.x, Styles.ScaleWidthFloat(80.0f));
				tooltipHeight = naturalSize.y;
			}
			else
			{
				draw_style.wordWrap = true;
				tooltipWidth = preferredMaxWidth;
				tooltipHeight = draw_style.CalcHeight(tooltip_content, tooltipWidth);

				// Only widen beyond the preferred cap when that is necessary to keep all
				// wrapped text visible vertically.
				while (tooltipHeight > maxHeight && tooltipWidth < screenWidth)
				{
					tooltipWidth = Mathf.Min(tooltipWidth * 1.25f, screenWidth);
					tooltipHeight = draw_style.CalcHeight(tooltip_content, tooltipWidth);
				}
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
			GUI.Label(new Rect(0.0f, 0.0f, tooltip_rect.width, tooltip_rect.height), tooltip, draw_style);
		}

		// tooltip text
		string tooltip;
		readonly int window_id;
		Rect tooltip_rect;

		// copy of Styles.tooltip with this tooltip's word wrap (the Launcher's and Window's tooltips share Styles.tooltip)
		GUIStyle draw_style;

		// last tab expansion
		string expanded_source;
		string expanded_text;
	}


} // KERBALISM
