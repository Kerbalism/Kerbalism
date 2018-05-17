using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;


namespace KERBALISM
{


	// a window containing a panel
	public sealed class Window
	{
		// - width: window width in pixel
		// - left: initial window horizontal position
		// - top: initial window vertical position
		public Window(uint width, uint left, uint top)
		{
			// generate unique id
			win_id = Lib.RandomInt(int.MaxValue);

			// setup window geometry
			win_rect = new Rect((float)left, (float)top, (float)width, 0.0f);

			// setup dragbox geometry
			drag_rect = new Rect(0.0f, 0.0f, (float)width, Styles.ScaleFloat(20.0f));

			// initialize tooltip utility
			tooltip = new Tooltip();
		}

		public void open(Action<Panel> refresh)
		{
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
				// initialize or clear panel
				if (panel == null) panel = new Panel();
				else panel.clear();

				// refresh panel content
				refresh(panel);

				// if panel is empty, close the window
				if (panel.empty())
				{
					close();
				}
			}
		}

		public void on_gui()
		{
			// window is considered closed if panel is null
			if (panel == null) return;

			// adapt window size to panel
			// - clamp to screen height
			win_rect.width = Math.Min(panel.width(), Screen.width * 0.8f);
			win_rect.height = Math.Min(Styles.ScaleFloat(20.0f) + panel.height(), Screen.height * 0.8f);

			// clamp the window to the screen, so it can't be dragged outside
			float offset_x = Math.Max(0.0f, -win_rect.xMin) + Math.Min(0.0f, Screen.width - win_rect.xMax);
			float offset_y = Math.Max(0.0f, -win_rect.yMin) + Math.Min(0.0f, Screen.height - win_rect.yMax);
			win_rect.xMin += offset_x;
			win_rect.xMax += offset_x;
			win_rect.yMin += offset_y;
			win_rect.yMax += offset_y;

			// draw the window
			win_rect = GUILayout.Window(win_id, win_rect, draw_window, "", Styles.win);

			// disable camera mouse scrolling on mouse over
			if (win_rect.Contains(Event.current.mousePosition))
			{
				GameSettings.AXIS_MOUSEWHEEL.primary.scale = 0.0f;
			}
		}

		void draw_window(int _)
		{
			// render window title
			GUILayout.BeginHorizontal(Styles.title_container);
			GUILayout.Label(Icons.empty, Styles.left_icon);
			GUILayout.Label(panel.title().ToUpper(), Styles.title_text);
			GUILayout.Label(Icons.close, Styles.right_icon);
			bool b = Lib.IsClicked();
			GUILayout.EndHorizontal();
			if (b) { close(); return; }

			// start scrolling view
			scroll_pos = GUILayout.BeginScrollView(scroll_pos, HighLogic.Skin.horizontalScrollbar, HighLogic.Skin.verticalScrollbar);

			// render panel content
			panel.render();

			// end scroll view
			GUILayout.EndScrollView();

			// draw tooltip
			tooltip.draw(win_rect);

			// right click close the window
			if (Event.current.type == EventType.MouseDown
			 && Event.current.button == 1)
			{
				close();
			}

			// enable dragging
			GUI.DragWindow(drag_rect);
		}

		public bool contains(Vector2 pos)
		{
			return win_rect.Contains(pos);
		}

		public void position(uint x, uint y)
		{
			win_rect.Set((float)x, (float)y, win_rect.width, win_rect.height);
		}

		public uint left()
		{
			return (uint)win_rect.xMin;
		}

		public uint top()
		{
			return (uint)win_rect.yMin;
		}

		public Panel.PanelType PanelType
		{
			get
			{
				if (panel == null)
					return Panel.PanelType.unknown;
				else
					return panel.paneltype;
			}
		}

		// store window id
		int win_id;

		// store window geometry
		Rect win_rect;

		// store dragbox geometry
		Rect drag_rect;

		// used by scroll window mechanics
		Vector2 scroll_pos;

		// tooltip utility
		Tooltip tooltip;

		// panel
		Panel panel;

		// refresh function
		Action<Panel> refresh;
	}


} // KERBALISM
