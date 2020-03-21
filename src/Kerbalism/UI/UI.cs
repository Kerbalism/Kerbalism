using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{


	public static class UI
	{
		public static void Init()
		{
			// create subsystems
			message = new Message();
			launcher = new Launcher();
			window = new Window((uint)Styles.ScaleWidthFloat(300), 0, 0);
		}

		public static void Sync()
		{
			window.Position(DB.UiData.win_left, DB.UiData.win_top);
		}

		public static void Update(bool show_window)
		{
			// if gui should be shown
			if (show_window)
			{
				// as a special case, the first time the user enter
				// map-view/tracking-station we open the body info window
				if (MapView.MapIsEnabled && !DB.UiData.map_viewed)
				{
					Open(BodyInfo.Body_info);
					DB.UiData.map_viewed = true;
				}

				// update subsystems
				launcher.Update();
				window.Update();

				// remember main window position
				DB.UiData.win_left = window.Left();
				DB.UiData.win_top = window.Top();
			}

			// re-enable camera mouse scrolling, as some of the on_gui functions can
			// disable it on mouse-hover, but can't re-enable it again consistently
			// (eg: you mouse-hover and then close the window with the cursor still inside it)
			// - we are ignoring user preference on mouse wheel
			GameSettings.AXIS_MOUSEWHEEL.primary.scale = 1.0f;
		}

		public static void On_gui(bool show_window)
		{
			// render subsystems
			message.On_gui();
			if (show_window)
			{
				launcher.On_gui();
				window.On_gui();
			}
		}

		public static void Open(Action<Panel> refresh)
		{
			window.Open(refresh);
		}

		static Message message;
		static Launcher launcher;
		public static Window window;
	}


} // KERBALISM

