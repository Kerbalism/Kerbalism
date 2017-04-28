using System;
using System.Collections.Generic;
using CommNet;
using UnityEngine;
using KSP.UI.Screens;


namespace KERBALISM {


[KSPScenario(ScenarioCreationOptions.AddToAllGames, new []{GameScenes.SPACECENTER, GameScenes.TRACKSTATION, GameScenes.FLIGHT, GameScenes.EDITOR})]
public sealed class Kerbalism : ScenarioModule
{
  public override void OnLoad(ConfigNode node)
  {
    // initialize everything just once
    if (!initialized)
    {
      // add supply resources to pods
      Profile.SetupPods();

      // initialize subsystems
      Cache.init();
      ResourceCache.init();
      Radiation.init();
      Science.init();
      LineRenderer.init();
      ParticleRenderer.init();
      Highlighter.init();
      UI.init();

      // prepare storm data
      foreach(CelestialBody body in FlightGlobals.Bodies)
      {
        if (Storm.skip_body(body)) continue;
        storm_data sd = new storm_data();
        sd.body = body;
        storm_bodies.Add(sd);
      }

      // setup callbacks
      callbacks = new Callbacks();

      // everything was initialized
      initialized = true;
    }

    // deserialize data
    DB.load(node);

    // detect if this is a different savegame
    if (DB.uid != savegame_uid)
    {
      // clear caches
      Cache.clear();
      ResourceCache.clear();

      // remember savegame id
      savegame_uid = DB.uid;
    }

    // force CommNet off when signal is enabled
    HighLogic.fetch.currentGame.Parameters.Difficulty.EnableCommNet &= !Features.Signal;
  }


  public override void OnSave(ConfigNode node)
  {
    // serialize data
    DB.save(node);
  }


  void FixedUpdate()
  {
    // remove control locks in any case
    Misc.clearLocks();

    // do nothing if paused
    if (Lib.IsPaused()) return;

    // maintain elapsed_s, converting to double only once
    // and detect warp blending
    double fixedDeltaTime = TimeWarp.fixedDeltaTime;
    if (Math.Abs(fixedDeltaTime - elapsed_s) > double.Epsilon) warp_blending = 0;
    else ++warp_blending;
    elapsed_s = fixedDeltaTime;

    // evict oldest entry from vessel cache
    Cache.update();

    // store info for oldest unloaded vessel
    double last_time = 0.0;
    Vessel last_v = null;
    vessel_info last_vi = null;
    VesselData last_vd = null;
    vessel_resources last_resources = null;

    // for each vessel
    foreach(Vessel v in FlightGlobals.Vessels)
    {
      // get vessel info from the cache
      vessel_info vi = Cache.VesselInfo(v);

      // set locks for active vessel
      if (v.isActiveVessel)
      {
        Misc.setLocks(v, vi);
      }

      // maintain eva dead animation and helmet state
      if (v.loaded && v.isEVA)
      {
        EVA.update(v);
      }

      // keep track of rescue mission kerbals, and gift resources to their vessels on discovery
      if (v.loaded && vi.is_vessel)
      {
        // manage rescue mission mechanics
        Misc.manageRescueMission(v);
      }

      // do nothing else for invalid vessels
      if (!vi.is_valid) continue;

      // get vessel data from db
      VesselData vd = DB.Vessel(v);

      // get resource cache
      vessel_resources resources = ResourceCache.Get(v);

      // if loaded
      if (v.loaded)
      {
        // show belt warnings
        Radiation.beltWarnings(v, vi, vd);

        // update storm data
        Storm.update(v, vi, vd, elapsed_s);

        // show signal warnings
        Signal.update(v, vi, vd, elapsed_s);

        // consume ec for transmission, and transmit science data
        Science.update(v, vi, vd, resources, elapsed_s);

        // apply rules
        Profile.Execute(v, vi, vd, resources, elapsed_s);

        // apply deferred requests
        resources.Sync(v, elapsed_s);

        // call automation scripts
        vd.computer.automate(v, vi, resources);

        // remove from unloaded data container
        unloaded.Remove(vi.id);
      }
      // if unloaded
      else
      {
        // get unloaded data, or create an empty one
        unloaded_data ud;
        if (!unloaded.TryGetValue(vi.id, out ud))
        {
          ud = new unloaded_data();
          unloaded.Add(vi.id, ud);
        }

        // accumulate time
        ud.time += elapsed_s;

        // maintain oldest entry
        if (ud.time > last_time)
        {
          last_time = ud.time;
          last_v = v;
          last_vi = vi;
          last_vd = vd;
          last_resources = resources;
        }
      }
    }


    // if the oldest unloaded vessel was selected
    if (last_v != null)
    {
      // show belt warnings
      Radiation.beltWarnings(last_v, last_vi, last_vd);

      // update storm data
      Storm.update(last_v, last_vi, last_vd, last_time);

      // show signal warnings
      Signal.update(last_v, last_vi, last_vd, last_time);

      // consume ec for transmission, and transmit science
      Science.update(last_v, last_vi, last_vd, last_resources, last_time);

      // apply rules
      Profile.Execute(last_v, last_vi, last_vd, last_resources, last_time);

      // simulate modules in background
      Background.update(last_v, last_vi, last_vd, last_resources, last_time);

      // apply deferred requests
      last_resources.Sync(last_v, last_time);

      // call automation scripts
      last_vd.computer.automate(last_v, last_vi, last_resources);

      // remove from unloaded data container
      unloaded.Remove(last_vi.id);
    }

    // update storm data for one body per-step
    if (storm_bodies.Count > 0)
    {
      storm_bodies.ForEach(k => k.time += elapsed_s);
      storm_data sd = storm_bodies[storm_index];
      Storm.update(sd.body, sd.time);
      sd.time = 0.0;
      storm_index = (storm_index + 1) % storm_bodies.Count;
    }
  }


  void Update()
  {
    // attach map renderer to planetarium camera once
    if (MapView.MapIsEnabled && map_camera_script == null)
      map_camera_script = PlanetariumCamera.Camera.gameObject.AddComponent<MapCameraScript>();

    // process keyboard input
    Misc.KeyboardInput();

    // add description to techs
    Misc.techDescriptions();

    // set part highlight colors
    Highlighter.update();

    // prepare gui content
    UI.update(callbacks.visible);
  }


  void OnGUI()
  {
    UI.on_gui(callbacks.visible);
  }


  // used to setup KSP callbacks
  static Callbacks callbacks;

  // the rendering script attached to map camera
  static MapCameraScript map_camera_script;

  // store time until last update for unloaded vessels
  // note: not using reference_wrapper<T> to increase readability
  sealed class unloaded_data { public double time; }; //< reference wrapper
  static Dictionary<UInt32, unloaded_data> unloaded = new Dictionary<uint, unloaded_data>();

  // used to update storm data on one body per step
  static int storm_index;
  class storm_data { public double time; public CelestialBody body; };
  static List<storm_data> storm_bodies = new List<storm_data>();

  // used to initialize everything just once
  static bool initialized;

  // equivalent to TimeWarp.fixedDeltaTime
  // note: stored here to avoid converting it to double every time
  public static double elapsed_s;

  // number of steps from last warp blending
  public static uint warp_blending;

  // last savegame unique id
  static int savegame_uid;
}


public sealed class MapCameraScript : MonoBehaviour
{
  void OnPostRender()
  {
    // do nothing when not in map view
    // - avoid weird situation when in some user installation MapIsEnabled is true in the space center
    if (!MapView.MapIsEnabled || HighLogic.LoadedScene == GameScenes.SPACECENTER) return;

    // commit all geometry
    Signal.render();
    Radiation.render();

    // render all committed geometry
    LineRenderer.render();
    ParticleRenderer.render();
  }
}


// misc functions
public static class Misc
{
  public static void clearLocks()
  {
    // remove control locks
    InputLockManager.RemoveControlLock("eva_dead_lock");
    InputLockManager.RemoveControlLock("no_signal_lock");
  }


  public static void setLocks(Vessel v, vessel_info vi)
  {
    // lock controls for EVA death
    if (EVA.IsDead(v))
    {
      InputLockManager.SetControlLock(ControlTypes.EVA_INPUT, "eva_dead_lock");
    }

    // lock controls for probes without signal
    if (vi.is_valid && !vi.connection.linked && vi.crew_count == 0 && Settings.UnlinkedControl != UnlinkedCtrl.full)
    {
      // choose no controls, or only full/zero throttle and staging
      ControlTypes ctrl = Settings.UnlinkedControl == UnlinkedCtrl.none
        ? ControlTypes.ALL_SHIP_CONTROLS
        : ControlTypes.PARTIAL_SHIP_CONTROLS;

      InputLockManager.SetControlLock(ctrl, "no_signal_lock");
      FlightInputHandler.state.mainThrottle = 0.0f;
    }
  }


  public static void manageRescueMission(Vessel v)
  {
    // true if we detected this was a rescue mission vessel
    bool detected = false;

    // deal with rescue missions
    foreach(ProtoCrewMember c in Lib.CrewList(v))
    {
      // get kerbal data
      KerbalData kd = DB.Kerbal(c.name);

      // flag the kerbal as not rescue at prelaunch
      if (v.situation == Vessel.Situations.PRELAUNCH) kd.rescue = false;

      // if the kerbal belong to a rescue mission
      if (kd.rescue)
      {
        // remember it
        detected = true;

        // flag the kerbal as non-rescue
        // note: enable life support mechanics for the kerbal
        kd.rescue = false;

        // show a message
        Message.Post(Lib.BuildString("We found <b>", c.name, "</b>"), Lib.BuildString((c.gender == ProtoCrewMember.Gender.Male ? "He" : "She"), "'s still alive!"));
      }
    }

    // gift resources
    if (detected)
    {
      var reslib = PartResourceLibrary.Instance.resourceDefinitions;
      var parts = Lib.GetPartsRecursively(v.rootPart);

      // give the vessel some propellant usable on eva
      string monoprop_name = Lib.EvaPropellantName();
      double monoprop_amount = Lib.EvaPropellantCapacity();
      foreach(var part in parts)
      {
        if (part.CrewCapacity > 0 || part.FindModuleImplementing<KerbalEVA>() != null)
        {
          if (Lib.Capacity(part, monoprop_name) <= double.Epsilon)
          {
            Lib.AddResource(part, monoprop_name, 0.0, monoprop_amount);
          }
          break;
        }
      }
      ResourceCache.Produce(v, monoprop_name, monoprop_amount);

      // give the vessel some supplies
      Profile.SetupRescue(v);
    }
  }


  public static void techDescriptions()
  {
    var rnd = RDController.Instance;
    if (rnd == null) return;
    var selected = RDController.Instance.node_selected;
    if (selected == null) return;
    var techID = selected.tech.techID;
    if (rnd.node_description.text.IndexOf("<i></i>\n", StringComparison.Ordinal) == -1) //< check for state in the string
    {
      rnd.node_description.text += "<i></i>\n"; //< store state in the string

      // collect unique configure-related unlocks
      HashSet<string> labels = new HashSet<string>();
      foreach(AvailablePart p in PartLoader.LoadedPartsList)
      {
        foreach(Configure cfg in p.partPrefab.FindModulesImplementing<Configure>())
        {
          foreach(ConfigureSetup setup in cfg.Setups())
          {
            if (setup.tech == selected.tech.techID)
            {
              labels.Add(Lib.BuildString(setup.name, " in ", cfg.title));
            }
          }
        }
      }

      // add unique configure-related unlocks
      foreach(string label in labels)
      {
        rnd.node_description.text += Lib.BuildString("\n• <color=#00ffff>", label, "</color>");
      }
    }
  }


  public static void KeyboardInput()
  {
    // mute/unmute messages with keyboard
    if (Input.GetKeyDown(KeyCode.Pause))
    {
      if (!Message.IsMuted())
      {
        Message.Post("Messages muted", "Be careful out there");
        Message.Mute();
      }
      else
      {
        Message.Unmute();
        Message.Post("Messages unmuted");
      }
    }

    // toggle body info window with keyboard
    if (MapView.MapIsEnabled && Input.GetKeyDown(KeyCode.B))
    {
      UI.open(BodyInfo.body_info);
    }

    // call action scripts
    // - avoid creating vessel data for invalid vessels
    Vessel v = FlightGlobals.ActiveVessel;
    if (v != null && DB.vessels.ContainsKey(Lib.RootID(v)))
    {
      // get computer
      Computer computer = DB.Vessel(v).computer;

      // call scripts with 1-5 key
      if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) { computer.execute(v, ScriptType.action1); }
      if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) { computer.execute(v, ScriptType.action2); }
      if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) { computer.execute(v, ScriptType.action3); }
      if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) { computer.execute(v, ScriptType.action4); }
      if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) { computer.execute(v, ScriptType.action5); }
    }
  }


  // return true if the vessel is a rescue mission
  public static bool IsRescueMission(Vessel v)
  {
    // if at least one of the crew is flagged as rescue, consider it a rescue mission
    foreach(var c in Lib.CrewList(v))
    {
      if (DB.Kerbal(c.name).rescue) return true;
    }


    // not a rescue mission
    return false;
  }

  // kill a kerbal
  // note: you can't kill a kerbal while iterating over vessel crew list, do it outside the loop
  public static void Kill(Vessel v, ProtoCrewMember c)
  {
    // if on pod
    if (!v.isEVA)
    {
      // forget kerbal data
      DB.kerbals.Remove(c.name);

      // if vessel is loaded
      if (v.loaded)
      {
        // find part
        Part part = null;
        foreach(Part p in v.parts)
        {
          if (p.protoModuleCrew.Find(k => k.name == c.name) != null) { part = p; break; }
        }

        // remove kerbal from part
        part.RemoveCrewmember(c);

        // and from vessel
        v.RemoveCrew(c);

        // then kill it
        c.Die();
      }
      // if vessel is not loaded
      else
      {
        // find proto part
        ProtoPartSnapshot part = null;
        foreach(ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
        {
          if (p.HasCrew(c.name)) { part = p; break; }
        }

        // remove from part
        part.RemoveCrew(c.name);

        // and from vessel
        v.protoVessel.RemoveCrew(c);

        // flag as dead
        c.rosterStatus = ProtoCrewMember.RosterStatus.Dead;
      }
    }
    // else it must be an eva death
    else
    {
      // flag as eva death
      DB.Kerbal(c.name).eva_dead = true;

      // rename vessel
      v.vesselName = c.name + "'s body";
    }

    // remove reputation
    if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
    {
      Reputation.Instance.AddReputation(-Settings.DeathReputation, TransactionReasons.Any);
    }
  }


  // trigger a random breakdown event
  public static void Breakdown(Vessel v, ProtoCrewMember c)
  {
    // constants
    const double res_penalty = 0.1;        // proportion of food lost on 'depressed' and 'wrong_valve'

    // get a supply resource at random
    resource_info res = null;
    if (Profile.supplies.Count > 0)
    {
      Supply supply = Profile.supplies[Lib.RandomInt(Profile.supplies.Count)];
      res = ResourceCache.Info(v, supply.resource);
    }

    // compile list of events with condition satisfied
    List<KerbalBreakdown> events = new List<KerbalBreakdown>();
    events.Add(KerbalBreakdown.mumbling); //< do nothing, here so there is always something that can happen
    if (Lib.HasData(v)) events.Add(KerbalBreakdown.fat_finger);
    if (Reliability.CanMalfunction(v)) events.Add(KerbalBreakdown.rage);
    if (res != null && res.amount > double.Epsilon) events.Add(KerbalBreakdown.wrong_valve);

    // choose a breakdown event
    KerbalBreakdown breakdown = events[Lib.RandomInt(events.Count)];

    // generate message
    string text = "";
    string subtext = "";
    switch(breakdown)
    {
      case KerbalBreakdown.mumbling:    text = "$ON_VESSEL$KERBAL has been in space for too long"; subtext = "Mumbling incoherently"; break;
      case KerbalBreakdown.fat_finger:  text = "$ON_VESSEL$KERBAL is pressing buttons at random on the control panel"; subtext = "Science data has been lost"; break;
      case KerbalBreakdown.rage:        text = "$ON_VESSEL$KERBAL is possessed by a blind rage"; subtext = "A component has been damaged"; break;
      case KerbalBreakdown.wrong_valve: text = "$ON_VESSEL$KERBAL opened the wrong valve"; subtext = res.resource_name + " has been lost"; break;
    }

    // post message first so this one is shown before malfunction message
    Message.Post(Severity.breakdown, Lib.ExpandMsg(text, v, c), subtext);

    // trigger the event
    switch(breakdown)
    {
      case KerbalBreakdown.mumbling: break; // do nothing
      case KerbalBreakdown.fat_finger: Lib.RemoveData(v); break;
      case KerbalBreakdown.rage: Reliability.CauseMalfunction(v); break;
      case KerbalBreakdown.wrong_valve: res.Consume(res.amount * res_penalty); break;
    }

    // remove reputation
    if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
    {
      Reputation.Instance.AddReputation(-Settings.BreakdownReputation, TransactionReasons.Any);
    }
  }

  // breakdown events
  enum KerbalBreakdown
  {
    mumbling,         // do nothing (in case all conditions fail)
    fat_finger,       // data has been cancelled
    rage,             // components have been damaged
    wrong_valve       // supply resource has been lost
  }
}


} // KERBALISM
