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
  public double temperature         = 0.0;          // climate mechanic accumulator
  public double starved             = 0.0;          // food starving accumulator
  public double deprived            = 0.0;          // oxygen deprivation accumulator
  public double stressed            = 0.0;          // stress accumulator
  public double radiation           = 0.0;          // radiation accumulator
  public double time_since_food     = 0.0;          // keep track of last meal
  public uint   msg_freezing        = 0;            // message flag: freezing
  public uint   msg_burning         = 0;            // message flag: burning
  public uint   msg_starved         = 0;            // message flag: starving
  public uint   msg_deprived        = 0;            // message flag: suffocating
  public uint   msg_stressed        = 0;            // message flag: QoL
  public uint   msg_radiation       = 0;            // message flag: radiation poisoning
  public uint   resque              = 1;            // used to deal with resque mission kerbals
  public uint   disabled            = 0;            // a generic flag to disable resource consumption, for use by other mods
}


// store per-vessel data
public class vessel_data
{
  public uint   msg_ec              = 0;            // message flag: ec level
  public uint   msg_food            = 0;            // message flag: food level
  public uint   msg_oxygen          = 0;            // message flag: oxygen level
  public uint   msg_signal          = 0;            // message flag: link status
  public uint   msg_belt            = 0;            // message flag: crossing radiation belt
  public uint   cfg_ec              = 1;            // enable/disable message: ec level
  public uint   cfg_supply          = 1;            // enable/disable message: food/oxygen level
  public uint   cfg_malfunction     = 1;            // enable/disable message: malfunctions
  public uint   cfg_signal          = 1;            // enable/disable message: link status
  public string notes               = "";           // vessel notes
  public string group               = "NONE";       // vessel group
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
  private const string current_version = "0.9.9.3";

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


    kerbals.Clear();
    if (node.HasNode("kerbals"))
    {
      ConfigNode kerbals_node = node.GetNode("kerbals");
      foreach(ConfigNode kerbal_node in kerbals_node.GetNodes())
      {
        kerbal_data kd = new kerbal_data();
        kd.temperature     = Convert.ToDouble( kerbal_node.GetValue("temperature") );
        kd.starved         = Convert.ToDouble( kerbal_node.GetValue("starved") );
        kd.deprived        = Convert.ToDouble( kerbal_node.GetValue("deprived") );
        kd.stressed        = Convert.ToDouble( kerbal_node.GetValue("stressed") );
        kd.radiation       = Convert.ToDouble( kerbal_node.GetValue("radiation") );
        kd.time_since_food = Convert.ToDouble( kerbal_node.GetValue("time_since_food") );
        kd.msg_freezing    = Convert.ToUInt32( kerbal_node.GetValue("msg_freezing") );
        kd.msg_burning     = Convert.ToUInt32( kerbal_node.GetValue("msg_burning") );
        kd.msg_starved     = Convert.ToUInt32( kerbal_node.GetValue("msg_starved") );
        kd.msg_deprived    = Convert.ToUInt32( kerbal_node.GetValue("msg_deprived") );
        kd.msg_stressed    = Convert.ToUInt32( kerbal_node.GetValue("msg_stressed") );
        kd.msg_radiation   = Convert.ToUInt32( kerbal_node.GetValue("msg_radiation") );
        kd.resque          = Convert.ToUInt32( kerbal_node.GetValue("resque") );
        kd.disabled        = Convert.ToUInt32( kerbal_node.GetValue("disabled") );
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
        vd.msg_ec          = Convert.ToUInt32( vessel_node.GetValue("msg_ec") );
        vd.msg_food        = Convert.ToUInt32( vessel_node.GetValue("msg_food") );
        vd.msg_oxygen      = Convert.ToUInt32( vessel_node.GetValue("msg_oxygen") );
        vd.msg_signal      = Convert.ToUInt32( vessel_node.GetValue("msg_signal") );
        vd.msg_belt        = Convert.ToUInt32( vessel_node.GetValue("msg_belt") );
        vd.cfg_ec          = Convert.ToUInt32( vessel_node.GetValue("cfg_ec") );
        vd.cfg_supply      = Convert.ToUInt32( vessel_node.GetValue("cfg_supply") );
        vd.cfg_malfunction = Convert.ToUInt32( vessel_node.GetValue("cfg_malfunction") );
        vd.cfg_signal      = Convert.ToUInt32( vessel_node.GetValue("cfg_signal") );
        vd.notes           = vessel_node.GetValue("notes").Replace("$NEWLINE", "\n");
        vd.group           = string.CompareOrdinal(version, "0.9.9.0") > 0 ? vessel_node.GetValue("group") : "NONE";
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
        bd.storm_time  = Convert.ToDouble( body_node.GetValue("storm_time") );
        bd.storm_age   = Convert.ToDouble( body_node.GetValue("storm_age") );
        bd.storm_state = Convert.ToUInt32( body_node.GetValue("storm_state") );
        bd.msg_storm   = Convert.ToUInt32( body_node.GetValue("msg_storm") );
        bodies.Add(body_node.name.Replace("___", " "), bd);
      }
    }

    notifications = new notification_data();
    if (node.HasNode("notifications"))
    {
      ConfigNode notifications_node = node.GetNode("notifications");
      notifications.next_death_report   = Convert.ToUInt32( notifications_node.GetValue("next_death_report") );
      notifications.next_tutorial       = Convert.ToUInt32( notifications_node.GetValue("next_tutorial") );
      notifications.death_counter       = Convert.ToUInt32( notifications_node.GetValue("death_counter") );
      notifications.last_death_counter  = Convert.ToUInt32( notifications_node.GetValue("last_death_counter") );
      notifications.first_belt_crossing = Convert.ToUInt32( notifications_node.GetValue("first_belt_crossing") );
      notifications.first_signal_loss   = Convert.ToUInt32( notifications_node.GetValue("first_signal_loss") );
      notifications.first_malfunction   = Convert.ToUInt32( notifications_node.GetValue("first_malfunction") );
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
      kerbal_node.AddValue("temperature", kd.temperature);
      kerbal_node.AddValue("starved", kd.starved);
      kerbal_node.AddValue("deprived", kd.deprived);
      kerbal_node.AddValue("stressed", kd.stressed);
      kerbal_node.AddValue("radiation", kd.radiation);
      kerbal_node.AddValue("time_since_food", kd.time_since_food);
      kerbal_node.AddValue("msg_freezing", kd.msg_freezing);
      kerbal_node.AddValue("msg_burning", kd.msg_burning);
      kerbal_node.AddValue("msg_starved", kd.msg_starved);
      kerbal_node.AddValue("msg_deprived", kd.msg_deprived);
      kerbal_node.AddValue("msg_stressed", kd.msg_stressed);
      kerbal_node.AddValue("msg_radiation", kd.msg_radiation);
      kerbal_node.AddValue("resque", kd.resque);
      kerbal_node.AddValue("disabled", kd.disabled);
    }

    ConfigNode vessels_node = node.AddNode("vessels");
    foreach(var p in vessels)
    {
      vessel_data vd = p.Value;
      ConfigNode vessel_node = vessels_node.AddNode(p.Key.ToString());
      vessel_node.AddValue("msg_ec", vd.msg_ec);
      vessel_node.AddValue("msg_food", vd.msg_food);
      vessel_node.AddValue("msg_oxygen", vd.msg_oxygen);
      vessel_node.AddValue("msg_signal", vd.msg_signal);
      vessel_node.AddValue("msg_belt", vd.msg_belt);
      vessel_node.AddValue("cfg_ec", vd.cfg_ec);
      vessel_node.AddValue("cfg_supply", vd.cfg_supply);
      vessel_node.AddValue("cfg_malfunction", vd.cfg_malfunction);
      vessel_node.AddValue("cfg_signal", vd.cfg_signal);
      vessel_node.AddValue("notes", vd.notes.Replace("\n", "$NEWLINE"));
      vessel_node.AddValue("group", vd.group);
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