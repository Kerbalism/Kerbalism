// ====================================================================================================================
// run the show
// ====================================================================================================================


using System;
using System.Collections.Generic;
using System.Diagnostics;
using KSP.UI.Screens;
using UnityEngine;


namespace KERBALISM {


[KSPAddon(KSPAddon.Startup.MainMenu, true)]
public sealed class Engine : MonoBehaviour
{
  // ctor
  Engine()
  {
    // enable global access
    instance = this;

    // keep it alive
    DontDestroyOnLoad(this);
  }


  void Start()
  {
    // create subsystems
    cache           = new Cache();
    resource_cache  = new ResourceCache();
    background      = new Background();
    signal          = new Signal();
    storm           = new Storm();
    launcher        = new Launcher();
    info            = new Info();
    body_info       = new BodyInfo();
    message         = new Message();
    console         = new Console();
    editor          = new Editor();
    Radiation.init();
    LineRenderer.init();
    ParticleRenderer.init();

    // prepare storm data
    foreach(CelestialBody body in FlightGlobals.Bodies)
    {
      if (Storm.skip_body(body)) continue;
      storm_data sd = new storm_data();
      sd.body = body;
      storm_bodies.Add(sd);
    }
  }


  public static void ResetCache()
  {
    instance.cache = new Cache();
    instance.resource_cache = new ResourceCache();
  }


  // called every simulation step
  void FixedUpdate()
  {
    // do nothing if paused
    if (Lib.IsPaused()) return;

    // do nothing in the editors and the menus
    if (!Lib.SceneIsGame()) return;

    // do nothing if db isn't ready
    if (!DB.Ready()) return;

    // get elapsed time
    double elapsed_s = Kerbalism.elapsed_s;

    // evict oldest entry from vessel cache
    cache.update();

    // store info for oldest unloaded vessel
    double last_time = 0.0;
    Vessel last_v = null;
    vessel_info last_vi = null;
    vessel_data last_vd = null;
    vessel_resources last_resources = null;

    // for each vessel
    foreach(Vessel v in FlightGlobals.Vessels)
    {
      // get vessel info from the cache
      vessel_info vi = Cache.VesselInfo(v);

      // skip invalid vessels
      if (!vi.is_valid) continue;

      // get vessel data from db
      vessel_data vd = DB.VesselData(v.id);

      // get resource cache
      vessel_resources resources = ResourceCache.Get(v);

      // if loaded
      if (v.loaded)
      {
        // show belt warnings
        Radiation.beltWarnings(v, vi, vd);

        // update storm data
        storm.update(v, vi, vd, elapsed_s);

        // consume relay EC and show signal warnings
        signal.update(v, vi, vd, resources, elapsed_s * vi.time_dilation);

        // apply rules
        Rule.applyRules(v, vi, vd, resources, elapsed_s * vi.time_dilation);

        // apply deferred requests
        resources.Sync(v, elapsed_s);

        // update computer
        vd.computer.update(v, elapsed_s);

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

      // decay unloaded vessels inside atmosphere
      Kerbalism.atmosphereDecay(last_v, last_vi, last_time);

      // update storm data
      storm.update(last_v, last_vi, last_vd, last_time);

      // consume relay EC and show signal warnings
      signal.update(last_v, last_vi, last_vd, last_resources, last_time * last_vi.time_dilation);

      // apply rules
      Rule.applyRules(last_v, last_vi, last_vd, last_resources, last_time * last_vi.time_dilation);

      // simulate modules in background
      Background.update(last_v, last_vi, last_vd, last_resources, last_time * last_vi.time_dilation);

      // apply deferred requests
      last_resources.Sync(last_v, last_time);

      // update computer
      last_vd.computer.update(last_v, last_time);

      // remove from unloaded data container
      unloaded.Remove(last_vi.id);
    }


    // update storm data for one body per-step
    storm_bodies.ForEach(k => k.time += elapsed_s);
    storm_data sd = storm_bodies[storm_index];
    storm.update(sd.body, sd.time);
    sd.time = 0.0;
    storm_index = (storm_index + 1) % storm_bodies.Count;
  }


  // called every frame twice, first to compute ui layout and then to draw the ui
  void OnGUI()
  {
    // re-enable camera mouse scrolling, as some of the following functions can
    // disable it on mouse-hover, but can't re-enable it again consistently
    // (eg: you mouse-hover and then close the window with the cursor still inside it)
    // note: we are ignoring user preference on mouse wheel
    GameSettings.AXIS_MOUSEWHEEL.primary.scale = 1.0f;

    // always render the launcher
    launcher.on_gui();

    // always render the messages
    message.on_gui();

    // do nothing else if DB isn't ready or if we are in the editor
    if (!DB.Ready() || !Lib.SceneIsGame()) return;

    // do nothing if GUI should be hidden
    if (launcher.must_hide_ui) return;

    // render subsystems
    info.on_gui();
    body_info.on_gui();
    console.on_gui();
    editor.on_gui();
  }

  void Update()
  {
    // attach map renderer to planetarium camera once
    if (MapView.MapIsEnabled && map_camera_script == null)
      map_camera_script = PlanetariumCamera.Camera.gameObject.AddComponent<MapCameraScript>();
  }


  // subsystems
  Cache             cache;
  ResourceCache     resource_cache;
  Background        background;
  Signal            signal;
  Storm             storm;
  Launcher          launcher;
  Info              info;
  BodyInfo          body_info;
  Message           message;
  MapCameraScript   map_camera_script;
  Console           console;
  Editor            editor;

  // store time until last update for unloaded vessels
  public class unloaded_data { public double time; }; //< reference wrapper
  Dictionary<UInt32, unloaded_data> unloaded = new Dictionary<uint, unloaded_data>();

  // used to update storm data on one body per step
  int storm_index;
  public class storm_data { public double time; public CelestialBody body; };
  List<storm_data> storm_bodies = new List<storm_data>();

  // permit global access
  static Engine instance;
}


public sealed class MapCameraScript : MonoBehaviour
{
  void OnPostRender()
  {
    // do nothing if DB isn't ready for whatever reason
    if (!DB.Ready()) return;

    // do nothing when not in map view
    if (!MapView.MapIsEnabled) return;

    // commit all geometry
    Signal.render();
    Radiation.render();

    // render all committed geometry
    LineRenderer.render();
    ParticleRenderer.render();
  }
}


} // KERBALISM