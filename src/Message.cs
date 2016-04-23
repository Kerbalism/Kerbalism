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


public enum VesselEvent
{
  ec,       // about ec level in a vessel
  food,     // about food level in a vessel
  oxygen    // about oxygen level in a vessel
}


public enum KerbalEvent
{
  climate_low,  // about body temperature of a kerbal
  climate_high, // about body temperature of a kerbal
  food,         // about food intake of a kerbal
  oxygen,       // about oxygen intake of a kerbal
  radiation,    // about radiation dose of a kerbal
  stress        // about quality-of-life of a kerbal
}


public enum KerbalBreakdown
{
  mumbling,         // do nothing (in case all conditions fail)
  fat_finger,       // data has been cancelled
  rage,             // components have been damaged
  depressed,        // food has been lost
  wrong_valve,      // oxygen has been lost
  argument          // stress increased for all the crew
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


  // add a message related to vessel resources
  public static void Post(Severity severity, VesselEvent e, Vessel v)
  {
    bool is_eva = v.isEVA;
    bool is_probe = Lib.CrewCapacity(v) == 0;
    string text = "";
    string subtext = "";

    // vessel
    if (!v.isEVA)
    {
      switch(e)
      {
        // electric charge
        case VesselEvent.ec:

          // manned vessel
          if (Lib.CrewCapacity(v) > 0)
          {
            switch(severity)
            {
              case Severity.relax:    text = "$VESSEL batteries recharged"; subtext = "The crew is allowed music again"; break;
              case Severity.warning:  text = "On $VESSEL, batteries are almost empty"; subtext = "We are squeezing the last bit of juice"; break;
              case Severity.danger:   text = "There is no more electric charge on $VESSEL"; subtext = "Life support systems are off"; break;
            }
          }
          // probe
          else
          {
            switch(severity)
            {
              case Severity.relax:    text = "$VESSEL batteries recharged";  subtext = "Systems are back online"; break;
              case Severity.warning:  text = "On $VESSEL, batteries are almost empty"; subtext = "Shutting down non-essential systems"; break;
              case Severity.danger:   text = "There is no more electric charge on $VESSEL"; subtext = "We lost control"; break;
            }
          }
          break;

        // food
        case VesselEvent.food:

          switch(severity)
          {
            case Severity.relax:      text = "$VESSEL food reserves restored"; subtext = "Double snack rations for everybody"; break;
            case Severity.warning:    text = "On $VESSEL, food reserves are getting low"; subtext = "Anything edible is being scrutinized"; break;
            case Severity.danger:     text = "There is no more food on $VESSEL"; subtext = "The crew prepare to the inevitable"; break;
          }
          break;

        // oxygen
        case VesselEvent.oxygen:

          switch(severity)
          {
            case Severity.relax:      text = "$VESSEL oxygen reserves restored"; subtext = "The crew is taking a breather"; break;
            case Severity.warning:    text = "On $VESSEL, oxygen reserves are dangerously low"; subtext = "There is mildly panic among the crew"; break;
            case Severity.danger:     text = "There is no more oxygen on $VESSEL"; subtext = "Everybody stop breathing"; break;
          }
          break;
      }
    }
    // eva
    else
    {
      switch(e)
      {
        // electric charge
        case VesselEvent.ec:

          switch(severity)
          {
            case Severity.relax:      text = "$VESSEL recharged the battery"; break;
            case Severity.warning:    text = "$VESSEL is running out of power"; break;
            case Severity.danger:     text = "$VESSEL is out of power"; break;
          }
          break;

        // oxygen
        case VesselEvent.oxygen:

          switch(severity)
          {
            case Severity.relax:      text = "$VESSEL oxygen tank has been refilled"; break;
            case Severity.warning:    text = "$VESSEL is running out of oxygen"; break;
            case Severity.danger:     text = "$VESSEL is out of oxygen"; break;
          }
          break;
      }
    }

    text = text.Replace("$VESSEL", "<color=ffffff>" + v.vesselName + "</color>");

    Post(severity, text, subtext);
  }


  // add a message related to a kerbal
  public static void Post(Severity severity, KerbalEvent e, Vessel v, ProtoCrewMember c, KerbalBreakdown breakdown = KerbalBreakdown.mumbling)
  {
    string pretext = (!v.isActiveVessel ? ("On " + (v.isEVA ? "EVA" : v.vesselName) + ", ") : "") + "<color=#ffffff>" + c.name + "</color> ";
    string text = "";
    string subtext = "";

    switch(e)
    {
      // climate (cold)
      case KerbalEvent.climate_low:

        switch(severity)
        {
          case Severity.relax:      text = "hypothermia is under control"; break;
          case Severity.warning:    text = "feels cold"; break;
          case Severity.danger:     text = "is freezing to death"; break;
          case Severity.fatality:   text = "freezed to death"; break;
        }
        break;

      // climate (hot)
      case KerbalEvent.climate_high:

        switch(severity)
        {
          case Severity.relax:      text = "is hugging the climatizer"; break;
          case Severity.warning:    text = "is sweating"; break;
          case Severity.danger:     text = "is burning alive"; break;
          case Severity.fatality:   text = "burned alive"; break;
        }
        break;

      // food
      case KerbalEvent.food:

        switch(severity)
        {
          case Severity.relax:      text = "has got a mouthful of snacks now"; break;
          case Severity.warning:    text = "is hungry"; break;
          case Severity.danger:     text = "is starving"; break;
          case Severity.fatality:   text = "starved to death"; break;
        }
        break;

      // oxygen
      case KerbalEvent.oxygen:

        switch(severity)
        {
          case Severity.relax:      text = "is breathing again"; break;
          case Severity.warning:    text = "can't breath"; break;
          case Severity.danger:     text = "is suffocating"; break;
          case Severity.fatality:   text = "suffocated to death"; break;
        }
        break;

      // radiation
      case KerbalEvent.radiation:

        switch(severity)
        {
          // note: no recovery from radiation
          case Severity.warning:    text = "has been exposed to intense radiation"; break;
          case Severity.danger:     text = "is reporting symptoms of radiation poisoning"; break;
          case Severity.fatality:   text = "died after being exposed to extreme radiation"; break;
        }
        break;

      // quality-of-life
      case KerbalEvent.stress:

        switch(severity)
        {
          // note: no recovery from stress
          case Severity.warning:    text = "is losing $HIS_HER mind"; subtext = "Concentration is becoming a problem"; break;
          case Severity.danger:     text = "is about to breakdown"; subtext = "Starting to hear voices"; break;
          case Severity.breakdown:
            switch(breakdown)
            {
              case KerbalBreakdown.mumbling:      text = "has been in space for too long"; subtext = "Mumbling incoherently"; break;
              case KerbalBreakdown.argument:      text = "had an argument with the rest of the crew"; subtext = "Morale is degenerating at an alarming rate"; break;
              case KerbalBreakdown.fat_finger:    text = "is pressing buttons at random on the control panel"; subtext = "Science data has been lost"; break;
              case KerbalBreakdown.rage:          text = "is possessed by a blind rage"; subtext = "A component has been damaged"; break;
              case KerbalBreakdown.depressed:     text = "is not respecting the rationing guidelines"; subtext = "Food has been lost"; break;
              case KerbalBreakdown.wrong_valve:   text = "opened the wrong valve"; subtext = "Oxygen has been lost"; break;
            }
            break;
        }
        break;
    }

    text = text.Replace("$HIS_HER", c.gender == ProtoCrewMember.Gender.Male ? "his" : "her");
    text = text.Replace("$HIM_HER", c.gender == ProtoCrewMember.Gender.Male ? "him" : "her");

    Post(severity, pretext + text, subtext);
  }
}


} // KERBALISM