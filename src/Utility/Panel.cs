using System;
using UnityEngine;


namespace KERBALISM {


public static class Panel
{
  // render window pseudo-title
  // - return true if close button is clicked
  public static bool title(string txt)
  {
    GUILayout.BeginHorizontal(Styles.title_container);
    GUILayout.Label(Styles.empty, Styles.left_icon);
    GUILayout.Label(txt, Styles.title_text);
    GUILayout.Label(Styles.close, Styles.right_icon);
    bool b = Lib.IsClicked();
    GUILayout.EndHorizontal();
    return b;
  }

  // render section title
  public static void section(string txt)
  {
    GUILayout.BeginHorizontal(Styles.section_container);
    GUILayout.Label(txt, Styles.section_text);
    GUILayout.EndHorizontal();
  }

  // render selectable section title with arrows
  // - index: this value can be changed if arrows are clicked
  // - count: used to cycle index inside [0,count-1] range
  public static void section(string txt, ref int index, int count)
  {
    GUILayout.BeginHorizontal(Styles.section_container);
    if (count > 1)
    {
      GUILayout.Label(Styles.left_arrow, Styles.left_icon);
      if (Lib.IsClicked()) { index = (index == 0 ? count : index) - 1; }
    }
    GUILayout.Label(txt, Styles.section_text);
    if (count > 1)
    {
      GUILayout.Label(Styles.right_arrow, Styles.right_icon);
      if (Lib.IsClicked()) { index = (index + 1) % count; }
    }
    GUILayout.EndHorizontal();
  }

  // render selectable section title with arrows
  // - change: will contain -1 or 1 if the arrows are clicked
  public static void section(string txt, ref int change)
  {
    GUILayout.BeginHorizontal(Styles.section_container);
    GUILayout.Label(Styles.left_arrow, Styles.left_icon);
    if (Lib.IsClicked()) change = -1;
    GUILayout.Label(txt, Styles.section_text);
    GUILayout.Label(Styles.right_arrow, Styles.right_icon);
    if (Lib.IsClicked()) change = 1;
    GUILayout.EndHorizontal();
  }

  // render multi-line description
  public static void description(string txt)
  {
    GUILayout.BeginHorizontal(Styles.desc_container);
    GUILayout.Label(txt, Styles.desc);
    GUILayout.EndHorizontal();
  }

  // render description with value
  public static void content(string desc, string value)
  {
    GUILayout.BeginHorizontal(Styles.entry_container);
    GUILayout.Label(desc, Styles.entry_label);
    GUILayout.Label(value, Styles.entry_value);
    GUILayout.EndHorizontal();
  }

  // render description with value and a tooltip
  // note: for various reasons, the tooltips only work from planner/monitor ui
  public static void content(string desc, string value, string tooltip)
  {
    GUILayout.BeginHorizontal(Styles.entry_container);
    GUILayout.Label(desc, Styles.entry_label);
    GUILayout.Label(new GUIContent(value, tooltip), Styles.entry_value);
    GUILayout.EndHorizontal();
  }

  // render a description with a clickable value
  // - b: will be inverted if clicked
  public static void content(string desc, string value, ref bool b)
  {
    GUILayout.BeginHorizontal(Styles.entry_container);
    GUILayout.Label(desc, Styles.entry_label);
    GUILayout.Label(value, Styles.entry_value);
    if (Lib.IsClicked()) b = !b;
    GUILayout.EndHorizontal();
  }

  // render a description with a clickable value
  // - func: will be called if clicked
  public static void content(string desc, string value, Callback func)
  {
    GUILayout.BeginHorizontal(Styles.entry_container);
    GUILayout.Label(desc, Styles.entry_label);
    GUILayout.Label(value, Styles.entry_value);
    if (Lib.IsClicked()) func();
    GUILayout.EndHorizontal();
  }

  // render some spacing
  public static void space()
  {
    GUILayout.Space(10.0f);
  }

  // return height of a panel section with the specified number of entries
  public static float height(int entries)
  {
    return 18.0f + (float)entries * 16.0f + 16.0f;
  }

  // return height of a multi-line description
  public static float description_height(string txt)
  {
    return Styles.entry_label.CalcHeight(new GUIContent(txt), 240.0f) - 4.0f;
  }
}


} // KERBALISM

