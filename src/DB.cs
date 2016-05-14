// ====================================================================================================================
// store globally accessible data
// ====================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


// store per-kerbal data
public class kerbal_data
{
  public uint   resque              = 1;            // used to deal with resque mission kerbals
  public uint   disabled            = 0;            // a generic flag to disable resource consumption, for use by other mods
  public double living_space        = 1.0;          // living space factor of the connected-space or whole-space contaning this kerbal
  public double entertainment       = 1.0;          // entertainment factor of the connected-space or whole-space contaning this kerbal
  public double shielding           = 0.0;          // shielding factor of the connected-space or whole-space contaning this kerbal
  public string space_name          = "";           // a name for the space where the kerbal is, or empty for the whole-vessel space
  public Dictionary<string, kmon_data> kmon = new Dictionary<string, kmon_data>(); // rule data
}


// store per-vessel data
public class vessel_data
{
  public uint   msg_signal          = 0;            // message flag: link status
  public uint   msg_belt            = 0;            // message flag: crossing radiation belt
  public uint   cfg_ec              = 1;            // enable/disable message: ec level
  public uint   cfg_supply          = 1;            // enable/disable message: supplies level
  public uint   cfg_signal          = 1;            // enable/disable message: link status
  public uint   cfg_malfunction     = 1;            // enable/disable message: malfunctions
  public uint   cfg_storm           = 1;            // enable/disable message: storms
  public uint   cfg_highlights      = 1;            // show/hide malfunction highlights
  public uint   cfg_showlink        = 0;            // show/hide link line

  public string notes               = "";           // vessel notes
  public string group               = "NONE";       // vessel group
  public Dictionary<string, vmon_data> vmon = new Dictionary<string, vmon_data>(); // rule data
  public List<uint> scansat_id = new List<uint>();  // used to remember scansat sensors that were disabled
}


// store per-body data
public class body_data
{
  public double storm_time          = 0.0;          // time of next storm
  public double storm_age           = 0.0;          // time since last storm
  public uint   storm_state         = 0;            // 0: none, 1: inbound, 2: inprogress
  public uint   msg_storm           = 0;            // message flag
}


// store notifications data
public class notification_data
{
  public uint   next_death_report   = 0;            // index of next death report notification
  public uint   next_tutorial       = 0;            // index of next tutorial notification
  public uint   death_counter       = 0;            // number of death events, including multiple kerbals at once
  public uint   last_death_counter  = 0;            // previous number of death events, to detect new ones
  public uint   first_belt_crossing = 0;            // record first time the radiation belt is crossed, to show tutorial notification
  public uint   first_signal_loss   = 0;            // record first signal loss, to show tutorial notification
  public uint   first_malfunction   = 0;            // record first malfunction, to show tutorial notification
}


// store, serialize and make globally accessible data
[KSPScenario(ScenarioCreationOptions.AddToAllGames, new GameScenes[]{GameScenes.SPACECENTER, GameScenes.TRACKSTATION, GameScenes.FLIGHT})]
public class DB : ScenarioModule
{
  // store data per-kerbal
  private Dictionary<string, kerbal_data> kerbals = new Dictionary<string, kerbal_data>();

  // store data per-vessel
  private Dictionary<Guid, vessel_data> vessels = new Dictionary<Guid, vessel_data>();

  // store data per-body
  private Dictionary<string, body_data> bodies = new Dictionary<string, body_data>();

  // store data for the notifications system
  private notification_data notifications = new notification_data();

  // current savegame version
  private const string current_version = "0.9.9.8";

  // allow global access
  private static DB instance = null;


  public DB()
  {
    instance = this;
  }


  public override void OnLoad(ConfigNode node)
  {
    // get version of the savegame
    // note: if there isn't a version this is either a new game, or the first public release (that didn't have versioning)
    string version = node.HasValue("version") ? node.GetValue("version") : node.HasNode("kerbals") ? "0.9.9.0" : current_version;

    // this is an unsupported version, attempt a total clean up and pray
    // note: currently unused
    if (string.CompareOrdinal(version, "0.9.9.0") < 0)
    {
      Lib.Log("loading save from unsupported version " + version);
      kerbals.Clear();
      vessels.Clear();
      bodies.Clear();
      notifications = new notification_data();
      return;
    }


    kerbals.Clear();
    if (node.HasNode("kerbals"))
    {
      ConfigNode kerbals_node = node.GetNode("kerbals");
      foreach(ConfigNode kerbal_node in kerbals_node.GetNodes())
      {
        kerbal_data kd = new kerbal_data();
        kd.resque          = Lib.ConfigValue(kerbal_node, "resque", 1u);
        kd.disabled        = Lib.ConfigValue(kerbal_node, "disabled", 0u);
        kd.living_space    = Lib.ConfigValue(kerbal_node, "living_space", 1.0); // since 0.9.9.4
        kd.entertainment   = Lib.ConfigValue(kerbal_node, "entertainment", 1.0); // since 0.9.9.4
        kd.shielding       = Lib.ConfigValue(kerbal_node, "shielding", 0.0); // since 0.9.9.4
        kd.space_name      = Lib.ConfigValue(kerbal_node, "space_name", ""); // since 0.9.9.4
        kd.kmon = new Dictionary<string, kmon_data>();
        if (kerbal_node.HasNode("kmon")) // since 0.9.9.5
        {
          foreach(var cfg in kerbal_node.GetNode("kmon").GetNodes())
          {
            kmon_data kmon = new kmon_data();
            kmon.problem = Lib.ConfigValue(cfg, "problem", 0.0);
            kmon.message = Lib.ConfigValue(cfg, "message", 0u);
            kmon.time_since = Lib.ConfigValue(cfg, "time_since", 0.0);
            kd.kmon.Add(cfg.name, kmon);
          }
        }
        kerbals.Add(kerbal_node.name.Replace("___", " "), kd);
      }
    }

    vessels.Clear();
    if (node.HasNode("vessels"))
    {
      ConfigNode vessels_node = node.GetNode("vessels");
      foreach(ConfigNode vessel_node in vessels_node.GetNodes())
      {
        vessel_data vd = new vessel_data();
        vd.msg_signal      = Lib.ConfigValue(vessel_node, "msg_signal", 0u);
        vd.msg_belt        = Lib.ConfigValue(vessel_node, "msg_belt", 0u);
        vd.cfg_ec          = Lib.ConfigValue(vessel_node, "cfg_ec", 1u);
        vd.cfg_supply      = Lib.ConfigValue(vessel_node, "cfg_supply", 1u);
        vd.cfg_signal      = Lib.ConfigValue(vessel_node, "cfg_signal", 1u);
        vd.cfg_malfunction = Lib.ConfigValue(vessel_node, "cfg_malfunction", 1u);
        vd.cfg_storm       = Lib.ConfigValue(vessel_node, "cfg_storm", 1u); // since 0.9.9.5
        vd.cfg_highlights  = Lib.ConfigValue(vessel_node, "cfg_highlights", 1u); // since 0.9.9.5
        vd.cfg_showlink    = Lib.ConfigValue(vessel_node, "cfg_showlink", 0u); // since 0.9.9.8
        vd.notes           = Lib.ConfigValue(vessel_node, "notes", "").Replace("$NEWLINE", "\n"); // since 0.9.9.1
        vd.group           = Lib.ConfigValue(vessel_node, "group", "NONE"); // since 0.9.9.1
        vd.vmon = new Dictionary<string, vmon_data>();
        if (vessel_node.HasNode("vmon")) // since 0.9.9.5
        {
          foreach(var cfg in vessel_node.GetNode("vmon").GetNodes())
          {
            vmon_data vmon = new vmon_data();
            vmon.message = Lib.ConfigValue(cfg, "message", 0u);
            vd.vmon.Add(cfg.name, vmon);
          }
        }
        vd.scansat_id = new List<uint>();
        foreach(string s in vessel_node.GetValues("scansat_id")) // since 0.9.9.5
        {
          vd.scansat_id.Add(Convert.ToUInt32(s));
        }
        vessels.Add(new Guid(vessel_node.name), vd);
      }
    }

    bodies.Clear();
    if (node.HasNode("bodies"))
    {
      ConfigNode bodies_node = node.GetNode("bodies");
      foreach(ConfigNode body_node in bodies_node.GetNodes())
      {
        body_data bd = new body_data();
        bd.storm_time  = Lib.ConfigValue(body_node, "storm_time", 0.0);
        bd.storm_age   = Lib.ConfigValue(body_node, "storm_age", 0.0);
        bd.storm_state = Lib.ConfigValue(body_node, "storm_state", 0u);
        bd.msg_storm   = Lib.ConfigValue(body_node, "msg_storm", 0u);
        bodies.Add(body_node.name.Replace("___", " "), bd);
      }
    }

    notifications = new notification_data();
    if (node.HasNode("notifications"))
    {
      ConfigNode n_node = node.GetNode("notifications");
      notifications.next_death_report   = Lib.ConfigValue(n_node, "next_death_report", 0u);
      notifications.next_tutorial       = Lib.ConfigValue(n_node, "next_tutorial", 0u);
      notifications.death_counter       = Lib.ConfigValue(n_node, "death_counter", 0u);
      notifications.last_death_counter  = Lib.ConfigValue(n_node, "last_death_counter", 0u);
      notifications.first_belt_crossing = Lib.ConfigValue(n_node, "first_belt_crossing", 0u);
      notifications.first_signal_loss   = Lib.ConfigValue(n_node, "first_signal_loss", 0u);
      notifications.first_malfunction   = Lib.ConfigValue(n_node, "first_malfunction", 0u);
    }


    // versions before 0.9.9.5 used a different structure to remember message sent
    // mute the message system for a few seconds to avoid the user being bombarded by messages
    if (string.CompareOrdinal(version, "0.9.9.5") < 0)
    {
      Message.MuteInternal();
      base.StartCoroutine(CallbackUtil.DelayedCallback(10.0f, Message.UnmuteInternal));
    }


    // versions before 0.9.9.5 didn't have profiles, and didn't use CRP food/oxygen values
    // scale all amounts of them in existing vessels, to not break savegames
    if (string.CompareOrdinal(version, "0.9.9.5") < 0)
    {
      foreach(Vessel v in FlightGlobals.Vessels.FindAll(k => !k.loaded))
      {
        foreach(var part in v.protoVessel.protoPartSnapshots)
        {
          var food = part.resources.Find(k => k.resourceName == "Food");
          if (food != null)
          {
            food.resourceValues.SetValue("amount", (Lib.ConfigValue(food.resourceValues, "amount", 0.0f) * 10.0f).ToString());
            food.resourceValues.SetValue("maxAmount", (Lib.ConfigValue(food.resourceValues, "maxAmount", 0.0f) * 10.0f).ToString());
          }

          var oxygen = part.resources.Find(k => k.resourceName == "Oxygen");
          if (oxygen != null)
          {
            oxygen.resourceValues.SetValue("amount", (Lib.ConfigValue(oxygen.resourceValues, "amount", 0.0f) * 1000.0f).ToString());
            oxygen.resourceValues.SetValue("maxAmount", (Lib.ConfigValue(oxygen.resourceValues, "maxAmount", 0.0f) * 1000.0f).ToString());
          }
        }
      }
    }


    // if an old savegame was imported, log some debug info
    if (version != current_version) Lib.Log("savegame converted from version " + version);
  }


  public override void OnSave(ConfigNode node)
  {
    // save current version
    node.AddValue("version", current_version);

    ConfigNode kerbals_node = node.AddNode("kerbals");
    foreach(var p in kerbals)
    {
      kerbal_data kd = p.Value;
      ConfigNode kerbal_node = kerbals_node.AddNode(p.Key.Replace(" ", "___"));
      kerbal_node.AddValue("resque", kd.resque);
      kerbal_node.AddValue("disabled", kd.disabled);
      kerbal_node.AddValue("living_space", kd.living_space);
      kerbal_node.AddValue("entertainment", kd.entertainment);
      kerbal_node.AddValue("shielding", kd.shielding);
      kerbal_node.AddValue("space_name", kd.space_name);
      var kmon_node = kerbal_node.AddNode("kmon");
      foreach(var q in kd.kmon)
      {
        var kmon_subnode = kmon_node.AddNode(q.Key);
        kmon_subnode.AddValue("problem", q.Value.problem);
        kmon_subnode.AddValue("message", q.Value.message);
        kmon_subnode.AddValue("time_since", q.Value.time_since);
      }
    }

    ConfigNode vessels_node = node.AddNode("vessels");
    foreach(var p in vessels)
    {
      vessel_data vd = p.Value;
      ConfigNode vessel_node = vessels_node.AddNode(p.Key.ToString());
      vessel_node.AddValue("msg_signal", vd.msg_signal);
      vessel_node.AddValue("msg_belt", vd.msg_belt);
      vessel_node.AddValue("cfg_ec", vd.cfg_ec);
      vessel_node.AddValue("cfg_supply", vd.cfg_supply);
      vessel_node.AddValue("cfg_signal", vd.cfg_signal);
      vessel_node.AddValue("cfg_malfunction", vd.cfg_malfunction);
      vessel_node.AddValue("cfg_storm", vd.cfg_storm);
      vessel_node.AddValue("cfg_highlights", vd.cfg_highlights);
      vessel_node.AddValue("cfg_showlink", vd.cfg_showlink);
      vessel_node.AddValue("notes", vd.notes.Replace("\n", "$NEWLINE"));
      vessel_node.AddValue("group", vd.group);
      var vmon_node = vessel_node.AddNode("vmon");
      foreach(var q in vd.vmon)
      {
        var vmon_subnode = vmon_node.AddNode(q.Key);
        vmon_subnode.AddValue("message", q.Value.message);
      }
      foreach(uint id in vd.scansat_id)
      {
        vessel_node.AddValue("scansat_id", id.ToString());
      }
    }

    ConfigNode bodies_node = node.AddNode("bodies");
    foreach(var p in bodies)
    {
      body_data bd = p.Value;
      ConfigNode body_node = bodies_node.AddNode(p.Key.Replace(" ", "___"));
      body_node.AddValue("storm_time", bd.storm_time);
      body_node.AddValue("storm_age", bd.storm_age);
      body_node.AddValue("storm_state", bd.storm_state);
      body_node.AddValue("msg_storm", bd.msg_storm);
    }

    ConfigNode notifications_node = node.AddNode("notifications");
    notifications_node.AddValue("next_death_report", notifications.next_death_report.ToString());
    notifications_node.AddValue("next_tutorial", notifications.next_tutorial.ToString());
    notifications_node.AddValue("death_counter", notifications.death_counter.ToString());
    notifications_node.AddValue("last_death_counter", notifications.last_death_counter.ToString());
    notifications_node.AddValue("first_belt_crossing", notifications.first_belt_crossing.ToString());
    notifications_node.AddValue("first_signal_loss", notifications.first_signal_loss.ToString());
    notifications_node.AddValue("first_malfunction", notifications.first_malfunction.ToString());
  }


  public static bool Ready()
  {
    return instance != null;
  }


  public static kerbal_data KerbalData(string k_name)
  {
    if (!instance.kerbals.ContainsKey(k_name)) instance.kerbals.Add(k_name, new kerbal_data());
    return instance.kerbals[k_name];
  }


  public static vessel_data VesselData(Guid v_id)
  {
    if (!instance.vessels.ContainsKey(v_id)) instance.vessels.Add(v_id, new vessel_data());
    return instance.vessels[v_id];
  }


  public static body_data BodyData(string b_name)
  {
    if (!instance.bodies.ContainsKey(b_name)) instance.bodies.Add(b_name, new body_data());
    return instance.bodies[b_name];
  }


  public static vmon_data VmonData(Guid v_id, string rule_name)
  {
    var vd = VesselData(v_id);
    if (!vd.vmon.ContainsKey(rule_name)) vd.vmon.Add(rule_name, new vmon_data());
    return vd.vmon[rule_name];
  }


  public static kmon_data KmonData(string k_name, string rule_name)
  {
    var kd = KerbalData(k_name);
    if (!kd.kmon.ContainsKey(rule_name)) kd.kmon.Add(rule_name, new kmon_data());
    return kd.kmon[rule_name];
  }


  public static notification_data NotificationData()
  {
    return instance.notifications;
  }


  public static void ForgetKerbal(string k_name)
  {
    instance.kerbals.Remove(k_name);
  }


  public static void ForgetVessel(Guid v_id)
  {
    instance.vessels.Remove(v_id);
  }

  public static void ForgetBody(string b_name)
  {
    instance.bodies.Remove(b_name);
  }


  public static Dictionary<string, kerbal_data> Kerbals()
  {
    return instance.kerbals;
  }


  public static Dictionary<Guid, vessel_data> Vessels()
  {
    return instance.vessels;
  }


  public static Dictionary<string, body_data> Bodies()
  {
    return instance.bodies;
  }
}


} // KERBALISM