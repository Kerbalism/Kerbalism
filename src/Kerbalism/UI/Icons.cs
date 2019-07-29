using UnityEngine;

namespace KERBALISM
{

	///<summary> Kerbalism's Icons </summary>
	internal static class Icons
	{
		///<summary> Path to Kerbalism's textures </summary>
		internal static string TexturePath;

		internal static Texture2D empty;
		internal static Texture2D close;
		internal static Texture2D left_arrow;
		internal static Texture2D right_arrow;
		internal static Texture2D toggle_green;
		internal static Texture2D toggle_red;

		internal static Texture2D send_black;
		internal static Texture2D send_cyan;
		internal static Texture2D lab_black;
		internal static Texture2D lab_cyan;

		internal static Texture2D applauncher;

		internal static Texture2D small_info;
		internal static Texture2D small_folder;
		internal static Texture2D small_console;
		internal static Texture2D small_config;
		internal static Texture2D small_search;
		internal static Texture2D small_notes;

		internal static Texture2D category_normal;
		internal static Texture2D category_selected;

		internal static Texture2D sun_black;
		internal static Texture2D sun_white;
		internal static Texture2D solar_panel;

		internal static Texture2D battery_white;
		internal static Texture2D battery_yellow;
		internal static Texture2D battery_red;

		internal static Texture2D box_white;
		internal static Texture2D box_yellow;
		internal static Texture2D box_red;

		internal static Texture2D wrench_white;
		internal static Texture2D wrench_yellow;
		internal static Texture2D wrench_red;

		internal static Texture2D signal_white;
		internal static Texture2D signal_yellow;
		internal static Texture2D signal_red;

		internal static Texture2D recycle_yellow;
		internal static Texture2D recycle_red;

		internal static Texture2D radiation_yellow;
		internal static Texture2D radiation_red;

		internal static Texture2D health_white;
		internal static Texture2D health_yellow;
		internal static Texture2D health_red;

		internal static Texture2D brain_white;
		internal static Texture2D brain_yellow;
		internal static Texture2D brain_red;

		internal static Texture2D storm_yellow;
		internal static Texture2D storm_red;

		internal static Texture2D plant_white;
		internal static Texture2D plant_yellow;

		internal static Texture2D station_black;
		internal static Texture2D station_white;

		internal static Texture2D base_black;
		internal static Texture2D base_white;

		internal static Texture2D ship_black;
		internal static Texture2D ship_white;

		internal static Texture2D probe_black;
		internal static Texture2D probe_white;

		internal static Texture2D relay_black;
		internal static Texture2D relay_white;

		internal static Texture2D rover_black;
		internal static Texture2D rover_white;

		internal static Texture2D lander_black;
		internal static Texture2D lander_white;

		internal static Texture2D eva_black;
		internal static Texture2D eva_white;

		internal static Texture2D plane_black;
		internal static Texture2D plane_white;

		internal static Texture2D controller_black;
		internal static Texture2D controller_white;

		// timer controller
		internal static float nextFlashing = Time.time;
		internal static bool lastIcon = false;

		internal static Texture2D GetTexture(string name) {
			return Styles.GetUIScaledTexture(name);
		}

		internal static Texture2D GetTexture(string name, int width = 16, int height = 16, float prescalar = 1.0f) {
			return Styles.GetUIScaledTexture(name, width, height, prescalar);
		}

		///<summary> Initializes the icons </summary>
		internal static void Initialize()
		{
			TexturePath = KSPUtil.ApplicationRootPath + "GameData/Kerbalism/Textures/";

			empty = GetTexture("empty");                      // an empty icon to maintain alignment
			close = GetTexture("close");                      // black close icon
			left_arrow = GetTexture("left-arrow");            // white left arrow
			right_arrow = GetTexture("right-arrow");          // white right arrow
			toggle_green = GetTexture("toggle-green");        // green check mark
			toggle_red = GetTexture("toggle-red");            // red check mark

			send_black = GetTexture("send-black");            // used by file man
			send_cyan = GetTexture("send-cyan");
			lab_black = GetTexture("lab-black");
			lab_cyan = GetTexture("lab-cyan");

			applauncher = GetTexture("applauncher", 38, 38);

			small_info = GetTexture("small-info");
			small_folder = GetTexture("small-folder");
			small_console = GetTexture("small-console");
			small_config = GetTexture("small-config");
			small_search = GetTexture("small-search");
			small_notes = GetTexture("small-notes");

			category_normal = GetTexture("category-normal");
			category_selected = GetTexture("category-selected");

			sun_black = GetTexture("sun-black");
			sun_white = GetTexture("sun-white");
			solar_panel = GetTexture("solar-panel");

			battery_white = GetTexture("battery-white");
			battery_yellow = GetTexture("battery-yellow");
			battery_red = GetTexture("battery-red");

			box_white = GetTexture("box-white");
			box_yellow = GetTexture("box-yellow");
			box_red = GetTexture("box-red");

			wrench_white = GetTexture("wrench-white");
			wrench_yellow = GetTexture("wrench-yellow");
			wrench_red = GetTexture("wrench-red");

			signal_white = GetTexture("signal-white");
			signal_yellow = GetTexture("signal-yellow");
			signal_red = GetTexture("signal-red");

			recycle_yellow = GetTexture("recycle-yellow");
			recycle_red = GetTexture("recycle-red");

			radiation_yellow = GetTexture("radiation-yellow");
			radiation_red = GetTexture("radiation-red");

			health_white = GetTexture("health-white");
			health_yellow = GetTexture("health-yellow");
			health_red = GetTexture("health-red");

			brain_white = GetTexture("brain-white");
			brain_yellow = GetTexture("brain-yellow");
			brain_red = GetTexture("brain-red");

			storm_yellow = GetTexture("storm-yellow");
			storm_red = GetTexture("storm-red");

			plant_white = GetTexture("plant-white");
			plant_yellow = GetTexture("plant-yellow");

			station_black = GetTexture("vessels/station-black", 80, 80, 4);
			station_white = GetTexture("vessels/station-white", 80, 80, 4);

			base_black = GetTexture("vessels/base-black", 80, 80, 4);
			base_white = GetTexture("vessels/base-white", 80, 80, 4);

			ship_black = GetTexture("vessels/ship-black", 80, 80, 4);
			ship_white = GetTexture("vessels/ship-white", 80, 80, 4);

			probe_black = GetTexture("vessels/probe-black", 80, 80, 4);
			probe_white = GetTexture("vessels/probe-white", 80, 80, 4);

			relay_black = GetTexture("vessels/relay-black", 40, 40, 1.75f);
			relay_white = GetTexture("vessels/relay-white", 40, 40, 1.75f);

			rover_black = GetTexture("vessels/rover-black", 80, 80, 4);
			rover_white = GetTexture("vessels/rover-white", 80, 80, 4);

			lander_black = GetTexture("vessels/lander-black", 80, 80, 4);
			lander_white = GetTexture("vessels/lander-white", 80, 80, 4);

			eva_black = GetTexture("vessels/eva-black", 80, 80, 4);
			eva_white = GetTexture("vessels/eva-white", 80, 80, 4);

			plane_black = GetTexture("vessels/plane-black", 40, 40, 2.25f);
			plane_white = GetTexture("vessels/plane-white", 40, 40, 2.25f);

			controller_black = GetTexture("vessels/controller-black", 40, 40, 2.25f);
			controller_white = GetTexture("vessels/controller-white", 40, 40, 2.25f);
		}

		/// <summary>Switch icons based on time </summary>
		/// <param name="icon1">First Texture2D</param>
		/// <param name="icon2">Second Texture2D</param>
		/// <param name="interval">interval in sec</param>
		/// <returns></returns>
		internal static Texture2D iconSwitch(Texture2D icon1, Texture2D icon2, float interval = 0.3f)
		{
			if (Time.time > nextFlashing)
			{
				nextFlashing += interval;
				lastIcon ^= true;
			}
			if (lastIcon) return icon1;
			else return icon2;
		}
	}
} // KERBALISM
