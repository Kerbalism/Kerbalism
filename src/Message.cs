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


[KSPAddon(KSPAddon.Startup.MainMenu, true)]
public class Message : MonoBehaviour
{
  // constants
  const float offset = 266.0f;

  // represent an entry in the message list
  public class Entry
  {
    public string msg;
    public float first_seen;
  }

  // store entries
  private static Queue<Entry> entries = new Queue<Entry>();

  // disable message rendering
  static bool muted = false;
  static bool muted_internal = false;

  // styles
  GUIStyle style;


  // keep it alive
  Message() { DontDestroyOnLoad(this); }


  // pseudo-ctor
  public void Start()
  {
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
  public void OnGUI()
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
    if (muted || muted_internal) return;

    // avoid adding the same message if already present in the queue
    foreach(Entry e in entries) { if (e.msg == msg) return; }

    // compile entry
    Entry entry = new Entry();
    entry.msg = msg;
    entry.first_seen = 0;

    // add entry
    entries.Enqueue(entry);
  }


  // add a message
  public static void Post(string text, string subtext)
  {
    Post(text + "\n<i>" + subtext + "</i>");
  }


  // add a message
  public static void Post(Severity severity, string text, string subtext="")
  {
    string title = "";
    switch(severity)
    {
      case Severity.relax:      title = "<color=#00BB00><b>RELAX</b></color>\n"; break;
      case Severity.warning:    title = "<color=#BBBB00><b>WARNING</b></color>\n"; Lib.StopWarp(); break;
      case Severity.danger:     title = "<color=#BB0000><b>DANGER</b></color>\n"; Lib.StopWarp(); break;
      case Severity.fatality:   title = "<color=#BB0000><b>FATALITY</b></color>\n"; Lib.StopWarp(); break;
      case Severity.breakdown:  title = "<color=#BB0000><b>BREAKDOWN</b></color>\n"; Lib.StopWarp(); break;
    }
    Post(title + text + (subtext.Length > 0 ? "\n<i>" + subtext + "</i>" : ""));
  }

  // disable rendering of messages
  public static void Mute()
  {
    muted = true;
  }


  // re-enable rendering of messages
  public static void Unmute()
  {
    muted = false;
  }


  // return true if user channel is muted
  public static bool IsMuted()
  {
    return muted;
  }


  // disable rendering of messages
  // this one mute the internal channel, instead of the user one
  public static void MuteInternal()
  {
    muted_internal = true;
  }


  // re-enable rendering of messages
  // this one unmute the internal channel, instead of the user one
  public static void UnmuteInternal()
  {
    muted_internal = false;
  }


  // return true if internal channel is muted
  public static bool IsMutedInternal()
  {
    return muted_internal;
  }
}


} // KERBALISM