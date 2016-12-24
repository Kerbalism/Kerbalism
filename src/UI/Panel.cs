using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


// store and render a simple structured ui
public sealed class Panel
{
  public Panel()
  {
    headers = new List<Header>();
    sections = new List<Section>();
    callbacks = new List<Action>();
    win_title = string.Empty;
    min_width = 260.0f;
  }

  public void clear()
  {
    headers.Clear();
    sections.Clear();
    win_title = string.Empty;
    min_width = 260.0f;
  }

  public void header(string label, string tooltip="", Action click=null)
  {
    Header h = new Header();
    h.label = label;
    h.tooltip = tooltip;
    h.click = click;
    h.icons = new List<Icon>();
    headers.Add(h);
  }

  public void section(string title, string desc="", Action left=null, Action right=null)
  {
    Section p = new Section();
    p.title = title;
    p.desc = desc;
    p.left = left;
    p.right = right;
    p.entries = new List<Entry>();
    sections.Add(p);
  }

  public void content(string label, string value="", string tooltip="", Action click=null, Action hover=null)
  {
    Entry e = new Entry();
    e.label = label;
    e.value = value;
    e.tooltip = tooltip;
    e.click = click;
    e.hover = hover;
    e.icons = new List<Icon>();
    if (sections.Count > 0) sections[sections.Count - 1].entries.Add(e);
  }

  public void icon(Texture texture, string tooltip="", Action click=null)
  {
    Icon i = new Icon();
    i.texture = texture;
    i.tooltip = tooltip;
    i.click = click;
    if (sections.Count > 0)
    {
      Section p = sections[sections.Count - 1];
      p.entries[p.entries.Count - 1].icons.Add(i);
    }
    else if (headers.Count > 0)
    {
      Header h = headers[headers.Count - 1];
      h.icons.Add(i);
    }
  }


  public void render()
  {
    // headers
    foreach(Header h in headers)
    {
      GUILayout.BeginHorizontal(Styles.entry_container);
      GUILayout.Label(new GUIContent(h.label, h.tooltip), Styles.entry_label);
      if (h.click != null && Lib.IsClicked()) callbacks.Add(h.click);
      foreach(Icon i in h.icons)
      {
        GUILayout.Label(new GUIContent(i.texture, i.tooltip), Styles.right_icon);
        if (i.click != null && Lib.IsClicked()) callbacks.Add(i.click);
      }
      GUILayout.EndHorizontal();
      GUILayout.Space(10.0f);
    }

    // sections
    foreach(Section p in sections)
    {
      // section title
      GUILayout.BeginHorizontal(Styles.section_container);
      if (p.left != null)
      {
        GUILayout.Label(Icons.left_arrow, Styles.left_icon);
        if (Lib.IsClicked()) callbacks.Add(p.left);
      }
      GUILayout.Label(p.title, Styles.section_text);
      if (p.right != null)
      {
        GUILayout.Label(Icons.right_arrow, Styles.right_icon);
        if (Lib.IsClicked()) callbacks.Add(p.right);
      }
      GUILayout.EndHorizontal();

      // description
      if (p.desc.Length > 0)
      {
        GUILayout.BeginHorizontal(Styles.desc_container);
        GUILayout.Label(p.desc, Styles.desc);
        GUILayout.EndHorizontal();
      }

      // entries
      foreach(Entry e in p.entries)
      {
        GUILayout.BeginHorizontal(Styles.entry_container);
        GUILayout.Label(new GUIContent(e.label, e.tooltip), Styles.entry_label);
        if (e.hover != null && Lib.IsHover()) callbacks.Add(e.hover);
        GUILayout.Label(new GUIContent(e.value, e.tooltip), Styles.entry_value);
        if (e.click != null && Lib.IsClicked()) callbacks.Add(e.click);
        if (e.hover != null && Lib.IsHover()) callbacks.Add(e.hover);
        foreach(Icon i in e.icons)
        {
          GUILayout.Label(new GUIContent(i.texture, i.tooltip), Styles.right_icon);
          if (i.click != null && Lib.IsClicked()) callbacks.Add(i.click);
        }
        GUILayout.EndHorizontal();
      }

      // spacing
      GUILayout.Space(10.0f);
    }

    // call callbacks
    if (Event.current.type == EventType.Repaint)
    {
      foreach(Action func in callbacks) func();
      callbacks.Clear();
    }
  }

  public float height()
  {
    float h = 0.0f;

    h += (float)headers.Count * 26.0f;

    foreach(Section p in sections)
    {
      h += 18.0f + (float)p.entries.Count * 16.0f + 16.0f;
      if (p.desc.Length > 0)
      {
        h += Styles.entry_label.CalcHeight(new GUIContent(p.desc), 240.0f) - 4.0f; //< note: width is hard-coded
      }
    }

    return h;
  }

  // utility: decrement an index, warping around 0
  public void prev(ref int index, int count)
  {
    index = (index == 0 ? count : index) - 1;
  }

  // utility: increment an index, warping around a max
  public void next(ref int index, int count)
  {
    index = (index + 1) % count;
  }

  // utility: toggle a flag
  public void toggle(ref bool b)
  {
    b = !b;
  }

  // merge another panel with this one
  public void add(Panel p)
  {
    headers.AddRange(p.headers);
    sections.AddRange(p.sections);
  }

  // collapse all sections into one
  public void collapse(string title)
  {
    if (sections.Count > 0)
    {
      sections[0].title = title;
      for(int i=1; i<sections.Count; ++i) sections[0].entries.AddRange(sections[i].entries);
    }
    while(sections.Count > 1) sections.RemoveAt(sections.Count - 1);
  }

  // return true if panel has no sections or titles
  public bool empty()
  {
    return sections.Count == 0 && headers.Count == 0;
  }

  // set title metadata
  public void title(string s)
  {
    win_title = s;
  }

  // set width metadata
  // - width never shrink
  public void width(float w)
  {
    min_width = Math.Max(w, min_width);
  }

  // get medata
  public string title() { return win_title; }
  public float  width() { return min_width; }


  sealed class Header
  {
    public string label;
    public string tooltip;
    public Action click;
    public List<Icon> icons;
  }

  sealed class Section
  {
    public string title;
    public string desc;
    public Action left;
    public Action right;
    public List<Entry> entries;
  }

  sealed class Entry
  {
    public string label;
    public string value;
    public string tooltip;
    public Action click;
    public Action hover;
    public List<Icon> icons;
  }

  sealed class Icon
  {
    public Texture texture;
    public string tooltip;
    public Action click;
  }

  List<Header>  headers;    // fat entries to show before the first section
  List<Section> sections;   // set of sections
  List<Action>  callbacks;  // functions to call on input events
  string        win_title;  // metadata stored in panel
  float         min_width;  // metadata stored in panel
}


} // KERBALISM

