using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


// store and render a simple structured ui
public sealed class Panel
{
  public Panel()
  {
    sections = new List<Section>();
    titles = new List<Title>();
    callbacks = new List<Action>();
  }

  public void clear()
  {
    sections.Clear();
    titles.Clear();
  }

  public void title(string label, string tooltip="", Action click=null)
  {
    Title t = new Title();
    t.label = label;
    t.tooltip = tooltip;
    t.click = click;
    t.icons = new List<Icon>();
    titles.Add(t);
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
    else if (titles.Count > 0)
    {
      Title t = titles[titles.Count - 1];
      t.icons.Add(i);
    }
  }

  public void render()
  {
    // titles
    foreach(Title t in titles)
    {
      GUILayout.BeginHorizontal(Styles.entry_container);
      GUILayout.Label(new GUIContent(t.label, t.tooltip), Styles.entry_label);
      if (t.click != null && Lib.IsClicked()) callbacks.Add(t.click);
      foreach(Icon i in t.icons)
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

    h += (float)titles.Count * 26.0f;

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
    titles.AddRange(p.titles);
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
    return sections.Count == 0 && titles.Count == 0;
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

  sealed class Title
  {
    public string label;
    public string tooltip;
    public Action click;
    public List<Icon> icons;
  }

  List<Title>   titles;    // fat entries to show before the first section
  List<Section> sections;  // set of sections
  List<Action>  callbacks; // functions to call on input events
}


} // KERBALISM

