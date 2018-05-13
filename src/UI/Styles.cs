using System;
using UnityEngine;


namespace KERBALISM
{


	public static class Styles
	{
		static Styles()
		{
			// window container
			win = new GUIStyle(HighLogic.Skin.window);
			win.padding.left = ScaleInteger(6);
			win.padding.right = ScaleInteger(6);
			win.padding.top = 0;
			win.padding.bottom = 0;

			// window title container
			title_container = new GUIStyle();
			title_container.stretchWidth = true;
			title_container.fixedHeight = ScaleFloat(16.0f)
			title_container.margin.bottom = ScaleInteger(2);
			title_container.margin.top = ScaleInteger(2);

			// window title text
			title_text = new GUIStyle();
			title_text.fontSize = ScaleInteger(10);
			title_text.fixedHeight = ScaleFloat(16.0f);
			title_text.fontStyle = FontStyle.Bold;
			title_text.alignment = TextAnchor.MiddleCenter;

			// subsection title container
			section_container = new GUIStyle();
			section_container.stretchWidth = true;
			section_container.fixedHeight = ScaleFloat(16.0f);
			section_container.normal.background = Lib.GetTexture("black-background");
			section_container.margin.bottom = ScaleInteger(4);
			section_container.margin.top = ScaleInteger(4);

			// subsection title text
			section_text = new GUIStyle(HighLogic.Skin.label);
			section_text.fontSize = ScaleInteger(12);
			section_text.alignment = TextAnchor.MiddleCenter;
			section_text.normal.textColor = Color.white;
			section_text.stretchWidth = true;
			section_text.stretchHeight = true;

			// entry row container
			entry_container = new GUIStyle();
			entry_container.stretchWidth = true;
			entry_container.fixedHeight = ScaleFloat(16.0f);

			// entry label text
			entry_label = new GUIStyle(HighLogic.Skin.label);
			entry_label.richText = true;
			entry_label.normal.textColor = Color.white;
			entry_label.stretchWidth = true;
			entry_label.stretchHeight = true;
			entry_label.fontSize = ScaleInteger(12);
			entry_label.alignment = TextAnchor.MiddleLeft;

			entry_label_nowrap = new GUIStyle(HighLogic.Skin.label);
			entry_label_nowrap.richText = true;
			entry_label_nowrap.normal.textColor = Color.white;
			entry_label_nowrap.stretchWidth = true;
			entry_label_nowrap.stretchHeight = true;
			entry_label_nowrap.fontSize = ScaleInteger(12);
			entry_label_nowrap.alignment = TextAnchor.MiddleLeft;
			entry_label_nowrap.wordWrap = false;

			// entry value text
			entry_value = new GUIStyle(HighLogic.Skin.label);
			entry_value.richText = true;
			entry_value.normal.textColor = Color.white;
			entry_value.stretchWidth = true;
			entry_value.stretchHeight = true;
			entry_value.fontSize = ScaleInteger(12);
			entry_value.alignment = TextAnchor.MiddleRight;
			entry_value.fontStyle = FontStyle.Bold;

			// desc row container
			desc_container = new GUIStyle();
			desc_container.stretchWidth = true;
			desc_container.stretchHeight = true;

			// entry multi-line description
			desc = new GUIStyle(entry_label);
			desc.alignment = TextAnchor.UpperLeft;
			desc.margin.top = 0;
			desc.margin.bottom = 0;
			desc.padding.top = 0;
			desc.padding.bottom = ScaleInteger(10);
			desc.fontStyle = FontStyle.Italic;

			// left icon
			left_icon = new GUIStyle();
			left_icon.alignment = TextAnchor.MiddleLeft;
			left_icon.fixedWidth = ScaleFloat(16.0f);

			// right icon
			right_icon = new GUIStyle();
			right_icon.alignment = TextAnchor.MiddleRight;
			right_icon.fixedWidth = ScaleFloat(16.0f);
			right_icon.margin.left = ScaleInteger(8);

			// tooltip label style
			tooltip = new GUIStyle(HighLogic.Skin.label);
			tooltip.normal.background = Lib.GetTexture("black-background");
			tooltip.normal.textColor = Color.white;
			tooltip.stretchWidth = true;
			tooltip.stretchHeight = true;
			tooltip.fontSize = ScaleInteger(10);
			tooltip.border = new RectOffset(0, 0, 0, 0);
			tooltip.padding = new RectOffset(ScaleInteger(6), ScaleInteger(6), ScaleInteger(3), ScaleInteger(3));
			tooltip.margin = new RectOffset(0, 0, 0, 0);
			tooltip.alignment = TextAnchor.MiddleCenter;

			// tooltip container style
			tooltip_container = new GUIStyle();
			tooltip_container.stretchWidth = true;
			tooltip_container.stretchHeight = true;

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

		public static ScaleInteger(int val)
		{
			return (int) (val * Settings.UIScale);
		}

		public static ScaleFloat(float val)
		{
			return val * Settings.UIScale;
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

