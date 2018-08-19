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

		///<summary> Initializes the icons </summary>
		internal static void Initialize()
		{
			TexturePath = KSPUtil.ApplicationRootPath + "GameData/Kerbalism/Textures/";

			empty = Styles.GetUIScaledTexture("empty");                      // an empty icon to maintain alignment
			close = Styles.GetUIScaledTexture("close");                      // black close icon
			left_arrow = Styles.GetUIScaledTexture("left-arrow");            // white left arrow
			right_arrow = Styles.GetUIScaledTexture("right-arrow");          // white right arrow
			toggle_green = Styles.GetUIScaledTexture("toggle-green");        // green check mark
			toggle_red = Styles.GetUIScaledTexture("toggle-red");            // red check mark

			send_black = Styles.GetUIScaledTexture("send-black");            // used by file man
			send_cyan = Styles.GetUIScaledTexture("send-cyan");
			lab_black = Styles.GetUIScaledTexture("lab-black");
			lab_cyan = Styles.GetUIScaledTexture("lab-cyan");

			applauncher = Styles.GetUIScaledTexture("applauncher");

			small_info = Styles.GetUIScaledTexture("small-info");
			small_folder = Styles.GetUIScaledTexture("small-folder");
			small_console = Styles.GetUIScaledTexture("small-console");
			small_config = Styles.GetUIScaledTexture("small-config");
			small_search = Styles.GetUIScaledTexture("small-search");
			small_notes = Styles.GetUIScaledTexture("small-notes");

			category_normal = Styles.GetUIScaledTexture("category-normal");
			category_selected = Styles.GetUIScaledTexture("category-selected");

			sun_black = Styles.GetUIScaledTexture("sun-black");
			sun_white = Styles.GetUIScaledTexture("sun-white");

			battery_white = Styles.GetUIScaledTexture("battery-white");
			battery_yellow = Styles.GetUIScaledTexture("battery-yellow");
			battery_red = Styles.GetUIScaledTexture("battery-red");

			box_white = Styles.GetUIScaledTexture("box-white");
			box_yellow = Styles.GetUIScaledTexture("box-yellow");
			box_red = Styles.GetUIScaledTexture("box-red");

			wrench_white = Styles.GetUIScaledTexture("wrench-white");
			wrench_yellow = Styles.GetUIScaledTexture("wrench-yellow");
			wrench_red = Styles.GetUIScaledTexture("wrench-red");

			signal_white = Styles.GetUIScaledTexture("signal-white");
			signal_yellow = Styles.GetUIScaledTexture("signal-yellow");
			signal_red = Styles.GetUIScaledTexture("signal-red");

			recycle_yellow = Styles.GetUIScaledTexture("recycle-yellow");
			recycle_red = Styles.GetUIScaledTexture("recycle-red");

			radiation_yellow = Styles.GetUIScaledTexture("radiation-yellow");
			radiation_red = Styles.GetUIScaledTexture("radiation-red");

			health_white = Styles.GetUIScaledTexture("health-white");
			health_yellow = Styles.GetUIScaledTexture("health-yellow");
			health_red = Styles.GetUIScaledTexture("health-red");

			brain_white = Styles.GetUIScaledTexture("brain-white");
			brain_yellow = Styles.GetUIScaledTexture("brain-yellow");
			brain_red = Styles.GetUIScaledTexture("brain-red");

			storm_yellow = Styles.GetUIScaledTexture("storm-yellow");
			storm_red = Styles.GetUIScaledTexture("storm-red");

			plant_white = Styles.GetUIScaledTexture("plant-white");
			plant_yellow = Styles.GetUIScaledTexture("plant-yellow");
		}
	}

} // KERBALISM