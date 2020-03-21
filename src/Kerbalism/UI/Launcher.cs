using KSP.UI.Screens;
using UnityEngine;


namespace KERBALISM
{

	public sealed class Launcher
	{
		// click through locks
		private bool clickThroughLocked = false;
		private const ControlTypes MainGUILockTypes = ControlTypes.MANNODE_ADDEDIT | ControlTypes.MANNODE_DELETE | ControlTypes.MAP_UI |
			ControlTypes.TARGETING | ControlTypes.VESSEL_SWITCHING | ControlTypes.TWEAKABLES | ControlTypes.EDITOR_UI | ControlTypes.EDITOR_SOFT_LOCK | ControlTypes.UI;

		public Launcher()
		{
			// initialize
			Planner.Planner.Initialize();
			monitor = new Monitor();
			tooltip = new Tooltip();

			GameEvents.onGUIApplicationLauncherReady.Add(Create);
		}

		public void Create()
		{
			// do nothing if button already created
			if (!ui_initialized)
			{
				ui_initialized = true;

				// create the button
				// note: for some weird reasons, the callbacks can be called BEFORE this function return
				vesselListLauncher = ApplicationLauncher.Instance.AddApplication(null, null, null, null, null, null, Textures.applauncher_vessels);

				// enable the launcher button for some scenes
				vesselListLauncher.VisibleInScenes =
					ApplicationLauncher.AppScenes.SPACECENTER
				  | ApplicationLauncher.AppScenes.FLIGHT
				  | ApplicationLauncher.AppScenes.MAPVIEW
				  | ApplicationLauncher.AppScenes.TRACKSTATION
				  | ApplicationLauncher.AppScenes.VAB
				  | ApplicationLauncher.AppScenes.SPH;
			}

			if (Features.Science)
			{
				if (generalMenuLauncher == null)
				{
					generalMenuLauncher = ApplicationLauncher.Instance.AddApplication(null, null, null, null, null, null, Textures.applauncher_database);
					generalMenuLauncher.VisibleInScenes =
						ApplicationLauncher.AppScenes.SPACECENTER
					  | ApplicationLauncher.AppScenes.FLIGHT
					  | ApplicationLauncher.AppScenes.MAPVIEW
					  | ApplicationLauncher.AppScenes.TRACKSTATION
					  | ApplicationLauncher.AppScenes.VAB
					  | ApplicationLauncher.AppScenes.SPH;

					generalMenuLauncher.onLeftClick = () => ScienceArchiveWindow.Toggle();
				}
			}
			else
			{
				if (generalMenuLauncher != null)
				{
					generalMenuLauncher.onLeftClick = null;
					ApplicationLauncher.Instance.RemoveApplication(generalMenuLauncher);
					generalMenuLauncher = null;
				}
			}
		}

		public void Update()
		{
			// do nothing if GUI has not been initialized
			if (!ui_initialized)
				return;

			// do nothing if the UI is not shown
			if (win_rect.width == 0f)
				return;

			// update planner/monitor content
			if (Lib.IsEditor)
			{
				Planner.Planner.Update();
			}
			else
			{
				monitor.Update();
			}
		}


		// called every frame
		public void On_gui()
		{
			// do nothing if GUI has not been initialized
			if (!ui_initialized)
				return;

			// render the window
			if (vesselListLauncher.toggleButton.Value || vesselListLauncher.IsHovering || (win_rect.width > 0f && win_rect.Contains(Mouse.screenPos)))
			{
				// hard-coded offsets
				// note: there is a bug in stock that only set appscale properly in non-flight-mode after you go in flight-mode at least once
				float at_top_offset_x = 40.0f * GameSettings.UI_SCALE * GameSettings.UI_SCALE_APPS;
				float at_top_offset_y = 0.0f * GameSettings.UI_SCALE * GameSettings.UI_SCALE_APPS;
				float at_bottom_offset_x = 0.0f * GameSettings.UI_SCALE * GameSettings.UI_SCALE_APPS;
				float at_bottom_offset_y = 40.0f * GameSettings.UI_SCALE * GameSettings.UI_SCALE_APPS;
				float at_bottom_editor_offset_x = 66.0f * GameSettings.UI_SCALE * GameSettings.UI_SCALE_APPS;

				// get screen size
				float screen_width = Screen.width;
				float screen_height = Screen.height;

				// determine app launcher position;
				bool is_at_top = ApplicationLauncher.Instance.IsPositionedAtTop;

				// get window size
				float width = Lib.IsEditor ? Planner.Planner.Width() : monitor.Width();
				float height = Lib.IsEditor ? Planner.Planner.Height() : monitor.Height();

				// calculate window position
				float left = screen_width - width;
				float top = is_at_top ? 0.0f : screen_height - height;
				if (is_at_top)
				{
					left -= at_top_offset_x;
					top += at_top_offset_y;
				}
				else
				{
					left -= !Lib.IsEditor ? at_bottom_offset_x : at_bottom_editor_offset_x;
					top -= at_bottom_offset_y;
				}

				// store window geometry
				win_rect = new Rect(left, top, width, height);

				// begin window area
				GUILayout.BeginArea(win_rect, Styles.win);

				// a bit of spacing between title and content
				GUILayout.Space(Styles.ScaleFloat(10.0f));

				// draw planner in the editors, monitor everywhere else
				if (!Lib.IsEditor)
					monitor.Render();
				else
					Planner.Planner.Render();

				// end window area
				GUILayout.EndArea();

				// draw tooltip
				tooltip.Draw(new Rect(0.0f, 0.0f, Screen.width, Screen.height));
			}
			else
			{
				// set zero area win_rect
				win_rect.width = 0f;
			}

			// get mouse over state
			// bool mouse_over = win_rect.Contains(Event.current.mousePosition);
			bool mouse_over = win_rect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y));

			// disable camera mouse scrolling on mouse over
			if (mouse_over)
			{
				GameSettings.AXIS_MOUSEWHEEL.primary.scale = 0.0f;
			}

			// Disable Click through
			if (mouse_over && !clickThroughLocked)
			{
				InputLockManager.SetControlLock(MainGUILockTypes, "KerbalismMainGUILock");
				clickThroughLocked = true;
			}
			if (!mouse_over && clickThroughLocked)
			{
				InputLockManager.RemoveControlLock("KerbalismMainGUILock");
				clickThroughLocked = false;
			}
		}


		// initialized flag
		bool ui_initialized;

		// store reference to applauncher button
		ApplicationLauncherButton vesselListLauncher;

		ApplicationLauncherButton generalMenuLauncher;

		// window geometry
		Rect win_rect;

		// the vessel monitor
		Monitor monitor;

		// tooltip utility
		Tooltip tooltip;
	}


} // KERBALISM
