// ====================================================================================================================
// used to avoid computing vessel stuff multiple times per simulation step
// ====================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public class vessel_info
{
  public Vector3d position;           // vessel position
  public bool     sunlight;           // if the vessel is in direct sunlight
  public Vector3d sun_dir;            // normalized vector from vessel to sun
  public double   sun_dist;           // distance from vessel to sun
  public double   temperature;        // vessel temperature
  public double   cosmic_radiation;   // cosmic radiation if outside magnetosphere
  public double   belt_radiation;     // radiation from belt if inside one
  public double   storm_radiation;    // radiation from coronal mass ejection
  public double   env_radiation;      // sun of all incoming radiation
  public bool     breathable;         // true if inside breathable atmosphere
  public Dictionary<string, vmon_cache> vmon = new Dictionary<string, vmon_cache>();
}


[KSPAddon(KSPAddon.Startup.MainMenu, true)]
public class Cache : MonoBehaviour
{
  // cache of vessel info, recomputed every simulation step
  Dictionary<Guid, vessel_info> vessels = new Dictionary<Guid, vessel_info>();

  // permit global access
  private static Cache instance = null;

  // ctor
  Cache()
  {
    // enable global access
    instance = this;

    // keep it alive
    DontDestroyOnLoad(this);
  }

  void FixedUpdate()
  {
    // clear the cache, that is recomputed every simulation step
    vessels.Clear();
  }

  // get vessel info from the cache, or compute a new one and add it to the cache
  public static vessel_info VesselInfo(Vessel v)
  {
    // get the info from the cache, if it exist
    vessel_info info;
    if (instance.vessels.TryGetValue(v.id, out info)) return info;

    // compute vessel info
    info = Compute(v);

    // store vessel info in the cache
    instance.vessels.Add(v.id, info);

    // return the vessel info
    return info;
  }


  static vessel_info Compute(Vessel v)
  {
    vessel_info info = new vessel_info();
    info.position = Lib.VesselPosition(v);
    info.sunlight = Sim.RaytraceBody(v, Sim.Sun(), out info.sun_dir, out info.sun_dist);
    info.temperature = Sim.Temperature(v, info.sunlight);
    info.cosmic_radiation = Radiation.CosmicRadiation(v);
    info.belt_radiation = Radiation.BeltRadiation(v);
    info.storm_radiation = Radiation.StormRadiation(v, info.sunlight);
    info.env_radiation = info.cosmic_radiation + info.belt_radiation + info.storm_radiation;
    info.breathable = Sim.Breathable(v);
    foreach(var p in Kerbalism.rules)
    {
      Rule r = p.Value;
      if (r.resource_name.Length > 0)
      {
        var vmon = new vmon_cache();
        vmon.depletion = r.EstimateLifetime(v);
        double amount = Lib.GetResourceAmount(v, r.resource_name);
        double capacity = Lib.GetResourceCapacity(v, r.resource_name);
        vmon.level = capacity > double.Epsilon ? amount / capacity : 1.0; //< level is 1 with no capacity
        info.vmon.Add(p.Value.name, vmon);
      }
    }
    return info;
  }
}


} // KERBALISM