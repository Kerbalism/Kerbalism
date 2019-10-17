using UnityEngine;

namespace KERBALISM
{
	public static class Styles
	{
		static Styles()
		{
			// window container
			win = new GUIStyle(HighLogic.Skin.window)
			{
				padding =
				{
					left = ScaleInteger(6),
					right = ScaleInteger(6),
					top = 0,
					bottom = 0
				}
			};

			// window title container
			title_container = new GUIStyle
			{
				stretchWidth = true,
				fixedHeight = ScaleFloat(16.0f),
				margin =
				{
					bottom = ScaleInteger(2),
					top = ScaleInteger(2)
				}
			};

			// window title text
			title_text = new GUIStyle
			{
				fontStyle = FontStyle.Bold,
				fontSize = ScaleInteger(10),
				fixedHeight = ScaleFloat(16.0f),
				alignment = TextAnchor.MiddleCenter
			};

			// subsection title container
			section_container = new GUIStyle
			{
				stretchWidth = true,
				fixedHeight = ScaleFloat(16.0f),
				normal = { background = Lib.GetTexture("black-background") },
				margin =
				{
					bottom = ScaleInteger(4),
					top = ScaleInteger(4)
				}
			};

			// subsection title text
			section_text = new GUIStyle(HighLogic.Skin.label)
			{
				stretchWidth = true,
				stretchHeight = true,
				fontSize = ScaleInteger(12),
				alignment = TextAnchor.MiddleCenter,
				normal = { textColor = Color.white }
			};

			// entry row container
			entry_container = new GUIStyle
			{
				stretchWidth = true,
				fixedHeight = ScaleFloat(16.0f)
			};

			// entry label text
			entry_label = new GUIStyle(HighLogic.Skin.label)
			{
				richText = true,
				stretchWidth = true,
				stretchHeight = true,
				fontSize = ScaleInteger(12),
				alignment = TextAnchor.MiddleLeft,
				normal = { textColor = Color.white }
			};

			entry_label_nowrap = new GUIStyle(HighLogic.Skin.label)
			{
				richText = true,
				wordWrap = false,
				stretchWidth = true,
				stretchHeight = true,
				fontSize = ScaleInteger(12),
				alignment = TextAnchor.MiddleLeft,
				normal = { textColor = Color.white }
			};

			// entry value text
			entry_value = new GUIStyle(HighLogic.Skin.label)
			{
				richText = true,
				stretchWidth = true,
				stretchHeight = true,
				fontStyle = FontStyle.Bold,
				fontSize = ScaleInteger(12),
				alignment = TextAnchor.MiddleRight,
				normal = { textColor = Color.white }
			};

			// desc row container
			desc_container = new GUIStyle
			{
				stretchWidth = true,
				stretchHeight = true
			};

			// entry multi-line description
			desc = new GUIStyle(entry_label)
			{
				fontStyle = FontStyle.Italic,
				alignment = TextAnchor.UpperLeft,
				margin =
				{
					top = 0,
					bottom = 0
				},
				padding =
				{
					top = 0,
					bottom = ScaleInteger(10)
				}
			};

			// left icon
			left_icon = new GUIStyle
			{
				stretchWidth = true,
				stretchHeight = true,
				fixedWidth = ScaleFloat(16.0f),
				alignment = TextAnchor.MiddleLeft
			};

			// right icon
			right_icon = new GUIStyle
			{
				stretchWidth = true,
				stretchHeight = true,
				margin = { left = ScaleInteger(8) },
				fixedWidth = ScaleFloat(16.0f),
				alignment = TextAnchor.MiddleRight
			};

			// tooltip label style
			tooltip = new GUIStyle(HighLogic.Skin.label)
			{
				stretchWidth = true,
				stretchHeight = true,
				fontSize = ScaleInteger(12),
				alignment = TextAnchor.MiddleCenter,
				border = new RectOffset(0, 0, 0, 0),
				normal =
				{
					textColor = Color.white, 
					background = Lib.GetTexture("black-background")
				},
				margin = new RectOffset(0, 0, 0, 0),
				padding = new RectOffset(ScaleInteger(6), ScaleInteger(6), ScaleInteger(3), ScaleInteger(3))
			};

			tooltip.normal.background.wrapMode = TextureWrapMode.Repeat;

			// tooltip container style
			tooltip_container = new GUIStyle
			{
				stretchWidth = true,
				stretchHeight = true
			};

			smallStationHead = new GUIStyle(HighLogic.Skin.label)
			{
				fontSize = ScaleInteger(12)
			};

			smallStationText = new GUIStyle(HighLogic.Skin.label)
			{
				fontSize = ScaleInteger(12),
				normal = { textColor = Color.white }
			};
		}

		public static int ScaleInteger(int val)
		{
			return (int)(val * Settings.UIScale * GameSettings.UI_SCALE * GameSettings.UI_SCALE_APPS);
		}

		public static float ScaleFloat(float val)
		{
			return val * Settings.UIScale * GameSettings.UI_SCALE * GameSettings.UI_SCALE_APPS;
		}

		// Increasing font size does not affect width as much as height. To avoid excessively wide UIs we scale with UIPanelWidthSScale rather than	UIScale
		public static float ScaleWidthFloat(float val)
		{
			return val * Settings.UIPanelWidthScale * GameSettings.UI_SCALE * GameSettings.UI_SCALE_APPS;
		}

		public static uint ScaleStringLength(int val)
		{
			return (uint)ScaleWidthFloat(val / (Settings.UIScale * Settings.UIScale * GameSettings.UI_SCALE * GameSettings.UI_SCALE_APPS));
		}

		public static Texture2D GetUIScaledTexture(string name, int width = 16, int height =16, float prescalar = 1.0f)
		{
			Texture2D texture = Lib.GetTexture(name, width, height);

			Lib.ScaleTexture(texture, ScaleInteger((int)(texture.width / prescalar)), ScaleInteger((int)(texture.height / prescalar)));

			return texture;
		}

		// styles
		public static GUIStyle win;                       // window
		public static GUIStyle title_container;           // window title container
		public static GUIStyle title_text;                // window title text
		public static GUIStyle section_container;         // container for a section subtitle
		public static GUIStyle section_text;              // text for a section subtitle
		public static GUIStyle entry_container;           // container for a row
		public static GUIStyle entry_label;               // left content for a row
		public static GUIStyle entry_label_nowrap;        // left content for a row that doesn't wrap
		public static GUIStyle entry_value;               // right content for a row
		public static GUIStyle desc_container;            // multi-line description container
		public static GUIStyle desc;                      // multi-line description content
		public static GUIStyle left_icon;                 // an icon on the left
		public static GUIStyle right_icon;                // an icon on the right
		public static GUIStyle tooltip;                   // tooltip label
		public static GUIStyle tooltip_container;         // tooltip label container
		public static GUIStyle smallStationHead;
		public static GUIStyle smallStationText;
	}
} // KERBALISM
