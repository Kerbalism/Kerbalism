// ====================================================================================================================
// show messages on screen
// ====================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public enum Severity
{
  relax,    // something went back to nominal
  warning,  // the user should start being worried about something
  danger,   // the user should start panicking about something
  fatality, // somebody died
  breakdown // somebody is breaking down
}


public sealed class Message
{
  // represent an entry in the message list
  class Entry
  {
    public string msg;
    public float first_seen;
  }


  // ctor
  public Message()
  {
    // enable global access
    instance = this;

    // setup style
    style = new GUIStyle();
    style.normal.background = Lib.GetTexture("black-background");
    style.normal.textColor = new Color(0.66f, 0.66f, 0.66f, 1.0f);
    style.richText = true;
    style.stretchWidth = true;
    style.stretchHeight = true;
    style.fixedWidth = 0;
    style.fixedHeight = 0;
    style.fontSize = 12;
    style.alignment = TextAnchor.MiddleCenter;
    style.border = new RectOffset(0, 0, 0, 0);
    style.padding = new RectOffset(2, 2, 2, 2);
  }


  // called every frame
  public void on_gui()
  {
    // do nothing in the main menu
    if (!(Lib.SceneIsGame() || HighLogic.LoadedScene == GameScenes.EDITOR)) return;

    // if queue is empty, do nothing
    if (entries.Count == 0) return;

    // get current time
    float time = Time.realtimeSinceStartup;

    // get first entry in the queue
    Entry e = entries.Peek();

    // if never visualized, remember first time shown
    if (e.first_seen <= float.Epsilon) e.first_seen = time;

    // if visualized for too long, remove from the queue and skip this update
    if (e.first_seen + Settings.MessageLength < time) { entries.Dequeue(); return; }

    // calculate content size
    GUIContent content = new GUIContent(e.msg);
    Vector2 size = style.CalcSize(content);
    size = style.CalcScreenSize(size);
    size.x += style.padding.left + style.padding.right;
    size.y += style.padding.bottom + style.padding.top;

    // calculate position
    Rect rect = new Rect((Screen.width - size.x) * 0.5f, (Screen.height - size.y - offset), size.x, size.y);

    // render the message
    var prev_style = GUI.skin.label;
    GUI.skin.label = style;
    GUI.Label(rect, e.msg);
    GUI.skin.label = prev_style;
  }


  // add a plain message
  public static void Post(string msg)
  {
    // ignore the message if muted
    if (instance.muted || instance.muted_internal) return;

    // avoid adding the same message if already present in the queue
    foreach(Entry e in instance.entries) { if (e.msg == msg) return; }

    // compile entry
    Entry entry = new Entry();
    entry.msg = msg;
    entry.first_seen = 0;

    // add entry
    instance.entries.Enqueue(entry);
  }


  // add a message
  public static void Post(string text, string subtext)
  {
    // ignore the message if muted
    if (instance.muted || instance.muted_internal) return;

    Post(Lib.BuildString(text, "\n<i>", subtext, "</i>"));
  }


  // add a message
  public static void Post(Severity severity, string text, string subtext="")
  {
    // ignore the message if muted
    if (instance.muted || instance.muted_internal) return;

    string title = "";
    switch(severity)
    {
      case Severity.relax:      title = "<color=#00BB00><b>RELAX</b></color>\n"; break;
      case Severity.warning:    title = "<color=#BBBB00><b>WARNING</b></color>\n"; Lib.StopWarp(); break;
      case Severity.danger:     title = "<color=#BB0000><b>DANGER</b></color>\n"; Lib.StopWarp(); break;
      case Severity.fatality:   title = "<color=#BB0000><b>FATALITY</b></color>\n"; Lib.StopWarp(); break;
      case Severity.breakdown:  title = "<color=#BB0000><b>BREAKDOWN</b></color>\n"; Lib.StopWarp(); break;
    }
    if (subtext.Length == 0) Post(Lib.BuildString(title, text));
    else Post(Lib.BuildString(title, text, "\n<i>", subtext, "</i>"));
  }


  // disable rendering of messages
  public static void Mute()
  {
    instance.muted = true;
  }


  // re-enable rendering of messages
  public static void Unmute()
  {
    instance.muted = false;
  }


  // return true if user channel is muted
  public static bool IsMuted()
  {
    return instance.muted;
  }


  // disable rendering of messages
  // this one mute the internal channel, instead of the user one
  public static void MuteInternal()
  {
    instance.muted_internal = true;
  }


  // re-enable rendering of messages
  // this one unmute the internal channel, instead of the user one
  public static void UnmuteInternal()
  {
    instance.muted_internal = false;
  }


  // return true if internal channel is muted
  public static bool IsMutedInternal()
  {
    return instance.muted_internal;
  }


  // constants
  const float offset = 266.0f;

  // store entries
  Queue<Entry> entries = new Queue<Entry>();

  // disable message rendering
  bool muted;
  bool muted_internal;

  // styles
  GUIStyle style;

  // permit global access
  static Message instance;
}


} // KERBALISM