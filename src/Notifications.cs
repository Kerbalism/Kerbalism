// ====================================================================================================================
// show notifications after certain events
// ====================================================================================================================


using System;
using System.Collections.Generic;
using Contracts.Parameters;
using KSP.UI.Screens;
using UnityEngine;


namespace KERBALISM {


[KSPAddon(KSPAddon.Startup.MainMenu, true)]
public class Notifications : MonoBehaviour
{
  // store a notification
  public class Entry
  {
    public Entry(string title, string msg) { this.title = title; this.msg = msg; }

    public string title;
    public string msg;
  }

  // time of last conditions check
  private float last_check = 0.0f;

  // time interval between checks
  private const float check_interval = 30.0f;

  // world of encouragements for the death reports
  private readonly static Entry[] death_report =
  {
    new Entry("In memoriam", "Every time we'll look at the star, we'll see him. His pale green face, his preposterous big eyes, his dumb smile that never failed to raise our own."),
    new Entry("The revenge of reality", "All our dreams have been met with the harsh reality of space travel. I wonder if, perhaps, we weren't meant to survive at all."),
    new Entry("He's gone, Jeb, HE IS GONE", "We knew space was hard. But sometime it feels like it is actually fighting against us."),
    new Entry("We are losing count", "The gravestones in the Space Memorial keeps popping up like skyscrapers in the 20s.")
  };

  // tutorial notifications
  private readonly static Entry[] tutorials =
  {
    new Entry("Food and Oxygen", "All Kerbals need a constant supply of Food and Oxygen to survive. Plan accordingly.\n\n"
            + "<b>Scrubbers</b>\n"
            + "CO2 scrubbers are embedded in all command pods. They reclaim some of the Oxygen, "
            + "at the expense of extra Electric Charge consumption. Their efficiency depend on the technological level of your space agency. Take a look in the Tech Tree.\n\n"
            + "<b>Greenhouses</b>\n"
            + "Greenhouses can grow food on the surface of planets and in space, even far from the Sun. But their mass makes them impractical for short-term missions.\n"),

    new Entry("Electric Charge", "As you have probably figured out already, Electric Charge is a precious resource in space.\n\n"
            + "<b>Climate Control</b>\n"
            + "We are in a constant battle against the hostile environment of space, where temperatures range from "
            + "extremely low to ridiculously high. All our manned pods are equipped with a climatizer that consume Electric Charge in proportion "
            + "to the external temperature. Use the thermometer to get some insight on it.\n\n"
            + "<b>Tracking panels</b>\n"
            + "Tracking panels are invaluable to guarantee a stable Electric Charge generation rate over long periods of time.\n"),

    new Entry("Radiation", "Just when you through that space may not be that hard after all, turns out it is filled with deadly radiation.\n\n"
            + "<b>Magnetospheres</b>\n"
            + "Some celestial bodies have a magnetosphere that extend far into space, protecting anything inside it.\n\n"
            + "<b>Radiation Belts</b>\n"
            + "Magnetospheres may have a region populated by extremely charged particles. "
            + "Unfortunately we have to cross it if we want to explore other celestial bodies.\n\n"
            + "<b>Cosmic Radiation</b>\n"
            + "The vast expanse of space seems to be filled with radiation coming from the galaxy. "
            + "We didn't notice it before because our magnetosphere protect us from it.\n\n"
            + "<b>Space Weather</b>\n"
            + "Coronal mass ejections from the Sun will hit planetary systems from time to time, causing short but intense radiation to all vessels caught outside "
            + "a magnetosphere and in direct line of sight with the Sun.\n\n"
            + "<b>Shielding</b>\n"
            + "We have the means to protect us from radiation. Well, not exactly, but at least we can delay their effects by equipping our vessels with Shielding.\n"),

    new Entry("Quality of Life", "Kerbals were susceptible to mental instability even on the surface of Kerbin, let alone in the deeps of space.\n\n"
            + "<b>Living Space</b>\n"
            + "Do not underestimate the consequences of living in extremely close quarters for extremely long times.\n"
            + "Give your Kerbals some space to stretch the legs.\n\n"
            + "<b>Entertainment</b>\n"
            + "Kerbals work hard and ask only for modest entertainment. Not that we could give them much more than a "
            + "window to watch the panorama, under the constrains of volume and mass we actually got.\n\n"
            + "<b>Company</b>\n"
            + "Nobody likes to be alone out there. Send out your Kerbals in groups and give their vessels an antenna so they can chat with mission control.\n\n"
            + "<b>Breakdown</b>\n"
            + "When Kerbals reach their limits, things could get a bit unpredictable. "
            + "Resources and data may be lost, and some components can become the target of your Kerbals rage.\n\n"
            + "<b>Crew Rotations</b>\n"
            + "Ultimately, Kerbals will break after some time. Rotate the crew on your space stations and bases, from time to time.\n"),

    new Entry("Signals", "Transmitting science data and controlling probes remotely require a link with the space center.\n\n"
            + "<b>Ranges and Transmission Costs</b>\n"
            + "Choose the right antenna for the job as these have wildly different ranges and transmission costs. Bigger doesn't always mean better.\n\n"
            + "<b>Line of Sight & Relays</b>\n"
            + "The signal can't go through celestial bodies, but it can be relayed by other vessels. Relaying must be enabled per-antenna, and cost some Electric Charge.\n\n"
            + "<b>Signal processing</b>\n"
            + "The error-correcting code used in our communication protocol can be further developed by researching specific technologies, leading to improved ranges.\n\n"
            + "<b>Blackouts</b>\n"
            + "When a coronal mass ejections from the Sun hit a magnetosphere, the signal-to-noise ratio drop to zero. Communications will blackout until the solar storm is over.\n"),

    new Entry("Malfunctions", "Components fail and usually they do it just when you need them. Their specs get reduced sensibly in that case. Plan for redundancy.\n\n"
            + "<b>Engineers</b>\n"
            + "Our Engineers are the only ones capable of repairing malfunctioned components. This mean they need to be out there, fixing things, and not sit "
            + "at the space center all day long thinking about spherical cows.\n\n"
            + "<b>Manufacturing Quality</b>\n"
            + "Some technologies increase the quality of your components, making them last longer in the extreme conditions of space. Look in the tech tree.\n")
  };

  // tutorial conditions
  static bool tutorial_condition(uint i)
  {
    if (ProgressTracking.Instance == null) return false;
    switch(i)
    {
      case 0: return ProgressTracking.Instance.reachSpace.IsComplete;                               // 'food & oxygen'
      case 1: return ProgressTracking.Instance.celestialBodyHome.orbit.IsComplete;                  // 'electric charge'
      case 2: return DB.NotificationData().first_belt_crossing > 0;                                 // 'radiation'
      case 3:                                                                                       // 'quality of life'
        foreach(var b in ProgressTracking.Instance.celestialBodyNodes)
        {
          if (b != ProgressTracking.Instance.celestialBodyHome && b.flyBy.IsComplete) return true;
        }
        return false;
      case 4: return DB.NotificationData().first_signal_loss > 0;                                   // 'signals'
      case 5: return DB.NotificationData().first_malfunction > 0;                                   // 'malfunctions'
    }
    return false;
  }

  // return true if the relative feature is enabled
  static bool tutorial_feature(uint i)
  {
    if (ProgressTracking.Instance == null) return false;
    switch(i)
    {
      case 0: // 'food & oxygen'
        return Kerbalism.supply_rules.Find(k => k.resource_name == "Food") != null
            && Kerbalism.supply_rules.Find(k => k.resource_name == "Oxygen") != null
            && Kerbalism.features.scrubber;

      case 1: // 'electric charge'
        foreach(var p in Kerbalism.rules)
        { if (p.Value.modifier == "temperature" && p.Value.resource_name == "ElectricCharge") return true; }
        return false;

      case 2: // 'radiation'
        foreach(var p in Kerbalism.rules)
        { if (p.Value.modifier == "radiation") return true; }
        return false;

      case 3: // 'quality of life'
        foreach(var p in Kerbalism.rules)
        { if (p.Value.modifier == "qol") return true; }
        return false;

      case 4: // 'signals'
        return Kerbalism.features.signal;

      case 5: // 'malfunctions'
        return Kerbalism.features.malfunction;
    }
    return false;
  }

  // keep it alive
  Notifications() { DontDestroyOnLoad(this); }

  // called after resources are loaded
  public void Start()
  {
    // register callback for kerbal death
    GameEvents.onCrewKilled.Add(RegisterDeathEvent);
  }

  // called by the engine when a kerbal die
  public void RegisterDeathEvent(EventReport e)
  {
    ++DB.NotificationData().death_counter;
  }

  // called manually to register a death (used for eva death)
  public static void RegisterDeath()
  {
    ++DB.NotificationData().death_counter;
  }

  // called every frame
  public void OnGUI()
  {
    // avoid case when DB isn't ready for whatever reason
    if (!DB.Ready()) return;

    // check only when at the space center
    if (HighLogic.LoadedScene != GameScenes.SPACECENTER) return;

    // check once in a while
    float time = Time.realtimeSinceStartup;
    if (last_check + check_interval > time) return;
    last_check = time;

    // get notification data
    notification_data nd = DB.NotificationData();

    // if there are tutorials left to show
    if (nd.next_tutorial < tutorials.Length)
    {
      // check tutorial condition
      if (tutorial_condition(nd.next_tutorial))
      {
        // check if the relative feature is enabled
        if (tutorial_feature(nd.next_tutorial))
        {
          // show notification
          Entry e = tutorials[nd.next_tutorial];
          Notification(e.title, e.msg, "INFO");
        }

        // move to next tutorial
        ++nd.next_tutorial;
      }
    }

    // if there is one or more new deaths
    if (nd.death_counter > nd.last_death_counter)
    {
      // show notification
      Entry e = death_report[nd.next_death_report];
      Notification(e.title, e.msg, "ALERT");

      // move to next tutorial, cycle throwgh all of them repetatedly
      // note: done this way because modulo didn't work...
      ++nd.next_death_report;
      if (nd.next_death_report >= death_report.Length) nd.next_death_report -= (uint)death_report.Length;
    }

    // remember number of death kerbals
    nd.last_death_counter = nd.death_counter;
  }

  // show a generic notification
  public static void Notification(string title, string msg, string type)
  {
    MessageSystemButton.MessageButtonColor msg_clr = MessageSystemButton.MessageButtonColor.BLUE;
    MessageSystemButton.ButtonIcons msg_icon = MessageSystemButton.ButtonIcons.MESSAGE;
    if (type == "ALERT")
    {
      msg_clr = MessageSystemButton.MessageButtonColor.RED;
      msg_icon = MessageSystemButton.ButtonIcons.ALERT;
    }
    MessageSystem.Instance.AddMessage(new MessageSystem.Message(title, msg, msg_clr, msg_icon));
  }
}


} // KERBALISM