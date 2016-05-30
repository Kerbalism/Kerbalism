// ====================================================================================================================
// a simple frame-based profiler
// ====================================================================================================================


#if false

using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;


namespace KERBALISM {


[KSPAddon(KSPAddon.Startup.MainMenu, true)]
public sealed class Profiler : MonoBehaviour
{
  // constants
  const float width = 400.0f;
  const float height = 500.0f;
  const float top_height = 20.0f;
  const float bot_height = 20.0f;
  const float margin = 10.0f;
  const float spacing = 10.0f;

  // permit global access
  private static Profiler instance = null;

  // styles
  GUIStyle win_style;
  GUIStyle top_style;
  GUIStyle name_style;
  GUIStyle value_style;

  // store window id
  int win_id;

  // store window geometry
  Rect win_rect;

  // store dragbox geometry
  Rect drag_rect;

  // used by scroll window mechanics
  Vector2 scroll_pos;

  // visible flag
  bool visible;

  // an entry in the profiler
  class entry
  {
    public UInt64 calls;        // number of calls in last frame
    public UInt64 time;         // total time in last frame
    public UInt64 start;
  }

  // store all entries
  Dictionary<string, entry> entries = new Dictionary<string, entry>();
  Dictionary<string, entry> last_entries;


  // ctor
  Profiler()
  {
    // enable global access
    instance = this;

    // keep it alive
    DontDestroyOnLoad(this);

    // generate unique id, hopefully
    win_id = Lib.RandomInt(int.MaxValue);

    // setup window geometry
    win_rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

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
    name_style = new GUIStyle();
    name_style.fontSize = 10;
    name_style.fixedWidth = 150.0f;
    name_style.stretchWidth = false;
    value_style = new GUIStyle(name_style);
    value_style.fixedWidth = 75.0f;
    value_style.alignment = TextAnchor.MiddleRight;

  }


  // called every frame
  public void OnGUI()
  {
    if ((HighLogic.LoadedSceneIsEditor || Lib.SceneIsGame()) && visible)
    {
      // clamp the window to the screen, so it can't be dragged outside
      float offset_x = Math.Max(0.0f, -win_rect.xMin) + Math.Min(0.0f, Screen.width - win_rect.xMax);
      float offset_y = Math.Max(0.0f, -win_rect.yMin) + Math.Min(0.0f, Screen.height - win_rect.yMax);
      win_rect.xMin += offset_x;
      win_rect.xMax += offset_x;
      win_rect.yMin += offset_y;
      win_rect.yMax += offset_y;

      // draw the window
      win_rect = GUI.Window(win_id, win_rect, render, "", win_style);
    }
  }


  // draw the window
  void render(int id)
  {
    // draw pseudo-title
    GUILayout.BeginHorizontal();
    GUILayout.Label("Profiler", top_style);
    GUILayout.EndHorizontal();

    // draw top spacing
    GUILayout.Space(spacing);

    // draw entries
    scroll_pos = GUILayout.BeginScrollView(scroll_pos, HighLogic.Skin.horizontalScrollbar, HighLogic.Skin.verticalScrollbar);
    GUILayout.BeginHorizontal();
    GUILayout.Label("<b>NAME</b>", name_style);
    GUILayout.Label("<b>TIME</b>", value_style);
    GUILayout.Label("<b>AVG</b>", value_style);
    GUILayout.Label("<b>CALLS</b>", value_style);
    GUILayout.EndHorizontal();
    foreach(var pair in last_entries)
    {
      string e_name = pair.Key;
      entry e = pair.Value;
      GUILayout.BeginHorizontal();
      GUILayout.Label(e_name, name_style);
      GUILayout.Label(Microseconds(e.time).ToString("F2"), value_style);
      GUILayout.Label(e.calls > 0 ? Microseconds(e.time / e.calls).ToString("F2") : "", value_style);
      GUILayout.Label(e.calls.ToString(), value_style);
      GUILayout.EndHorizontal();
    }
    GUILayout.EndScrollView();

    // draw bottom spacing
    GUILayout.Space(spacing);

    // enable dragging
    GUI.DragWindow(drag_rect);
  }


  public void Update()
  {
    last_entries = entries;
    entries = new Dictionary<string, entry>();

    if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyUp(KeyCode.P))
    {
      visible = !visible;
    }
  }


  // start an entry
  public static void Start(string e_name)
  {
    if (instance == null) return;

    if (!instance.entries.ContainsKey(e_name)) instance.entries.Add(e_name, new entry());

    entry e = instance.entries[e_name];
    e.start = Clocks();
  }


  // stop an entry
  public static void Stop(string e_name)
  {
    if (instance == null) return;

    entry e = instance.entries[e_name];

    ++e.calls;
    e.time += Clocks() - e.start;
  }


  // get current time
  static UInt64 Clocks()
  {
    return (UInt64)Stopwatch.GetTimestamp();
  }


  // convert from clocks to microseconds
  static double Microseconds(UInt64 clocks)
  {
    return (double)clocks * 1000000.0 / (double)Stopwatch.Frequency;
  }
}


// profile a function scope
public class ProfileScope : IDisposable
{
  public ProfileScope(string name)
  {
    this.name = name;
    Profiler.Start(name);
  }

  public void Dispose()
  {
    Profiler.Stop(name);
  }


  private string name;
}


} // KERBALISM


#endif