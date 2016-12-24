using System;
using UnityEngine;


namespace KERBALISM {


public static class Styles
{
  static Styles()
  {
    // window container
    win = new GUIStyle(HighLogic.Skin.window);
    win.padding.left = 6;
    win.padding.right = 6;
    win.padding.top = 0;
    win.padding.bottom = 0;

    // window title container
    title_container = new GUIStyle();
    title_container.stretchWidth = true;
    title_container.fixedHeight = 16.0f;
    title_container.margin.bottom = 2;
    title_container.margin.top = 2;

    // window title text
    title_text = new GUIStyle();
    title_text.fontSize = 10;
    title_text.fixedHeight = 16.0f;
    title_text.fontStyle = FontStyle.Bold;
    title_text.alignment = TextAnchor.MiddleCenter;

    // subsection title container
    section_container = new GUIStyle();
    section_container.stretchWidth = true;
    section_container.fixedHeight = 16.0f;
    section_container.normal.background = Lib.GetTexture("black-background");
    section_container.margin.bottom = 4;
    section_container.margin.top = 4;

    // subsection title text
    section_text = new GUIStyle(HighLogic.Skin.label);
    section_text.fontSize = 12;
    section_text.alignment = TextAnchor.MiddleCenter;
    section_text.normal.textColor = Color.white;
    section_text.stretchWidth = true;
    section_text.stretchHeight = true;

    // entry row container
    entry_container = new GUIStyle();
    entry_container.stretchWidth = true;
    entry_container.fixedHeight = 16.0f;

    // entry label text
    entry_label = new GUIStyle(HighLogic.Skin.label);
    entry_label.richText = true;
    entry_label.normal.textColor = Color.white;
    entry_label.stretchWidth = true;
    entry_label.stretchHeight = true;
    entry_label.fontSize = 12;
    entry_label.alignment = TextAnchor.MiddleLeft;

    // entry value text
    entry_value = new GUIStyle(HighLogic.Skin.label);
    entry_value.richText = true;
    entry_value.normal.textColor = Color.white;
    entry_value.stretchWidth = true;
    entry_value.stretchHeight = true;
    entry_value.fontSize = 12;
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
    desc.padding.bottom = 0;

    // left icon
    left_icon = new GUIStyle();
    left_icon.alignment = TextAnchor.MiddleLeft;
    left_icon.fixedWidth = 16.0f;

    // right icon
    right_icon = new GUIStyle();
    right_icon.alignment = TextAnchor.MiddleRight;
    right_icon.fixedWidth = 16.0f;
    right_icon.margin.left = 8;

    // tooltip label style
    tooltip = new GUIStyle(HighLogic.Skin.label);
    tooltip.normal.background = Lib.GetTexture("black-background");
    tooltip.normal.textColor = Color.white;
    tooltip.stretchWidth = true;
    tooltip.stretchHeight = true;
    tooltip.fontSize = 12;
    tooltip.border = new RectOffset(0, 0, 0, 0);
    tooltip.padding = new RectOffset(6, 6, 3, 3);
    tooltip.margin = new RectOffset(0,0,0,0);
    tooltip.alignment = TextAnchor.MiddleCenter;

    // tooltip container style
    tooltip_container = new GUIStyle();
    tooltip_container.stretchWidth = true;
    tooltip_container.stretchHeight = true;
  }


  // styles
  public static GUIStyle win;                       // window
  public static GUIStyle title_container;           // window title container
  public static GUIStyle title_text;                // window title text
  public static GUIStyle section_container;         // container for a section subtitle
  public static GUIStyle section_text;              // text for a section subtitle
  public static GUIStyle entry_container;           // container for a row
  public static GUIStyle entry_label;               // left content for a row
  public static GUIStyle entry_value;               // right content for a row
  public static GUIStyle desc_container;            // multi-line description container
  public static GUIStyle desc;                      // multi-line description content
  public static GUIStyle left_icon;                 // an icon on the left
  public static GUIStyle right_icon;                // an icon on the right
  public static GUIStyle tooltip;                   // tooltip label
  public static GUIStyle tooltip_container;         // tooltip label container
}


} // KERBALISM

