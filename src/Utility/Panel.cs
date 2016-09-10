// ===================================================================================================================
// a generic panel window renderer
// ===================================================================================================================


using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;


namespace KERBALISM {


public sealed class PanelWindow
{
  public PanelWindow(string title, uint left = 300u, uint top = 150u)
  {
    // set title
    this.title = title;

    // initialize sections storage
    sections = new List<PanelSection>(4);
    clicks = new List<Action<int>>(4);

    // generate unique id
    win_id = Lib.RandomInt(int.MaxValue);

    // setup window geometry
    win_rect = new Rect((float)left, (float)top, width, 0.0f);

    // setup dragbox geometry
    drag_rect = new Rect(0.0f, 0.0f, width, top_height);

    // setup styles
    win_style = new GUIStyle(HighLogic.Skin.window);
    win_style.padding.top = 0;
    win_style.padding.bottom = 0;
    top_style = new GUIStyle();
    top_style.fixedHeight = top_height;
    top_style.fontStyle = FontStyle.Bold;
    top_style.alignment = TextAnchor.MiddleCenter;

    // title container
    title_container_style = new GUIStyle();
    title_container_style.stretchWidth = true;
    title_container_style.fixedHeight = 16.0f;
    title_container_style.normal.background = Lib.GetTexture("black-background");
    title_container_style.margin.bottom = 4;
    title_container_style.margin.top = 4;

    // title label
    title_label_style = new GUIStyle(HighLogic.Skin.label);
    title_label_style.fontSize = 12;
    title_label_style.alignment = TextAnchor.MiddleCenter;
    title_label_style.normal.textColor = Color.white;
    title_label_style.stretchWidth = true;
    title_label_style.stretchHeight = true;

    // title icons
    title_icon_left_style = new GUIStyle();
    title_icon_left_style.alignment = TextAnchor.MiddleLeft;
    title_icon_right_style = new GUIStyle();
    title_icon_right_style.alignment = TextAnchor.MiddleRight;

    // row style
    row_style = new GUIStyle();
    row_style.stretchWidth = true;
    row_style.fixedHeight = 16.0f;

    // label style
    label_style = new GUIStyle(HighLogic.Skin.label);
    label_style.richText = true;
    label_style.normal.textColor = Color.white;
    label_style.stretchWidth = true;
    label_style.stretchHeight = true;
    label_style.fontSize = 12;
    label_style.alignment = TextAnchor.MiddleLeft;

    // value style
    value_style = new GUIStyle(HighLogic.Skin.label);
    value_style.richText = true;
    value_style.normal.textColor = Color.white;
    value_style.stretchWidth = true;
    value_style.stretchHeight = true;
    value_style.fontSize = 12;
    value_style.alignment = TextAnchor.MiddleRight;
    value_style.fontStyle = FontStyle.Bold;
  }


  public void add(PanelSection section, Action<int> click = null)
  {
    sections.Add(section);
    clicks.Add(click);
  }


  public void clear()
  {
    sections.Clear();
    clicks.Clear();
  }


  public void draw()
  {
    // set automatic height
    win_rect.height = height();

    // clamp the window to the screen, so it can't be dragged outside
    float offset_x = Math.Max(0.0f, -win_rect.xMin) + Math.Min(0.0f, Screen.width - win_rect.xMax);
    float offset_y = Math.Max(0.0f, -win_rect.yMin) + Math.Min(0.0f, Screen.height - win_rect.yMax);
    win_rect.xMin += offset_x;
    win_rect.xMax += offset_x;
    win_rect.yMin += offset_y;
    win_rect.yMax += offset_y;

    // draw the window
    win_rect = GUILayout.Window(win_id, win_rect, render, "", win_style);
  }


  void render(int _)
  {
    // draw pseudo-title
    GUILayout.BeginHorizontal();
    GUILayout.Label(title, top_style);
    GUILayout.EndHorizontal();

    // draw panels
    for(int i = 0; i < sections.Count; ++i)
    {
      render_section(i);
    }

    // enable dragging
    GUI.DragWindow(drag_rect);
  }


  void render_section(int i)
  {
    PanelSection section = sections[i];
    Action<int> click = clicks[i];

    GUILayout.BeginHorizontal(title_container_style);
    if (click == null)
    {
      GUILayout.Label(section.title, title_label_style);
    }
    else
    {
      GUILayout.Label(arrow_left, title_icon_left_style);
      if (Lib.IsClicked()) click(-1);
      GUILayout.Label(section.title, title_label_style);
      GUILayout.Label(arrow_right, title_icon_right_style);
      if (Lib.IsClicked()) click(1);
    }
    GUILayout.EndHorizontal();

    foreach(PanelEntry entry in section.entries)
    {
      GUILayout.BeginHorizontal(row_style);
      GUILayout.Label(entry.label, label_style);
      GUILayout.Label(entry.value, value_style);
      if (entry.func != null && Lib.IsClicked()) entry.func();
      GUILayout.EndHorizontal();
    }
    GUILayout.Space(10.0f);
  }


  float panel_height(int entries)
  {
    return 16.0f + (float)entries * 16.0f + 18.0f;
  }


  float height()
  {
    float h = top_height;
    foreach(var section in sections) h += panel_height(section.entries.Count);
    return h;
  }


  // constants
  const float width = 260.0f;
  const float top_height = 20.0f;
  const float margin = 10.0f;

  // arrow icons
  Texture arrow_left = Lib.GetTexture("left-white");
  Texture arrow_right = Lib.GetTexture("right-white");

  // styles
  GUIStyle win_style;
  GUIStyle top_style;
  GUIStyle title_container_style;
  GUIStyle title_label_style;
  GUIStyle title_icon_left_style;
  GUIStyle title_icon_right_style;
  GUIStyle row_style;
  GUIStyle label_style;
  GUIStyle value_style;

  // store window id
  int win_id;

  // store window geometry
  Rect win_rect;

  // store dragbox geometry
  Rect drag_rect;

  // store title
  string title;

  // store sections
  List<PanelSection> sections;

  // store flags for section selection
  List<Action<int>> clicks;
}


public class PanelEntry
{
  public PanelEntry(string label, string value = "", Action func = null)
  {
    this.label = label;
    this.value = value;
    this.func = func;
  }

  public string label;
  public string value;
  public Action func;
}


public class PanelSection
{
  public PanelSection(string title)
  {
    this.title = title;
    entries = new List<PanelEntry>();
  }

  public void add(string label, string value = "", Action func = null)
  {
    entries.Add(new PanelEntry(label, value, func));
  }

  public string title;
  public List<PanelEntry> entries;
}


} // KERBALISM

