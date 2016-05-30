// ====================================================================================================================
// used to avoid computing vessel stuff multiple times per simulation step
// ====================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {



public class vessel_info
{
  public vessel_info(Vessel v)
  {
    // determine if this is a valid vessel
    is_vessel = Lib.IsVessel(v);
    if (!is_vessel) return;

    // determine if this is a resque mission vessel
    is_resque = Lib.IsResqueMission(v);
    if (is_resque) return;

    // determine if this is an EVA death kerbal
    is_eva_dead = EVA.IsDead(v);
    if (is_eva_dead) return;

    // calculate crew capacity for the vessel
    crew_capacity = Lib.CrewCapacity(v);

    // calculate vessel position
    position = Lib.VesselPosition(v);

    // determine if in sunlight, calculate sun direction and distance
    sunlight = Sim.RaytraceBody(v, Sim.Sun(), out sun_dir, out sun_dist) ? 1.0 : 0.0;

    // if the orbit length vs simulation step is lower than an acceptable threshold, use discrete sun visibility
    if (v.mainBody.flightGlobalsIndex != 0)
    {
      double orbit_period = Sim.OrbitalPeriod(v);
      if (orbit_period / TimeWarp.fixedDeltaTime < 16.0) sunlight = 1.0 - Sim.ShadowPeriod(v) / orbit_period;
    }

    // calculate temperature at vessel position
    temperature = Sim.Temperature(v, sunlight);

    // calculate radiation
    cosmic_radiation = Radiation.CosmicRadiation(v);
    belt_radiation = Radiation.BeltRadiation(v);
    storm_radiation = Radiation.StormRadiation(v, sunlight);
    env_radiation = cosmic_radiation + belt_radiation + storm_radiation;

    // calculate atmospheric parameters
    atmo_factor = Sim.AtmosphereFactor(v.mainBody, position, sun_dir);
    breathable = Sim.Breathable(v);
  }

  public bool     is_vessel;          // true if this is a valid vessel
  public bool     is_resque;          // true if this is a resque mission vessel
  public bool     is_eva_dead;        // true if this an EVA death
  public int      crew_capacity;      // crew capacity of the vessel
  public Vector3d position;           // vessel position
  public double   sunlight;           // if the vessel is in direct sunlight
  public Vector3d sun_dir;            // normalized vector from vessel to sun
  public double   sun_dist;           // distance from vessel to sun
  public double   temperature;        // vessel temperature
  public double   cosmic_radiation;   // cosmic radiation if outside magnetosphere
  public double   belt_radiation;     // radiation from belt if inside one
  public double   storm_radiation;    // radiation from coronal mass ejection
  public double   env_radiation;      // sun of all incoming radiation
  public double   atmo_factor;        // proportion of flux not blocked by atmosphere
  public bool     breathable;         // true if inside breathable atmosphere
}


public class resource_info
{
  public resource_info(Vessel v, string resource_name)
  {
    amount = Lib.Resource.Amount(v, resource_name);
    capacity = Lib.Resource.Capacity(v, resource_name);
    level = capacity > double.Epsilon ? amount / capacity : 0.0;
  }

  public double amount;               // amount of resource
  public double capacity;             // capacity of resource
  public double level;                // amount vs capacity, or 1 if there is no capacity
}


[KSPAddon(KSPAddon.Startup.MainMenu, true)]
public sealed class Cache : MonoBehaviour
{
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
    // clear the vessel and resource cache, that are recomputed every simulation step
    vessels.Clear();
    resources.Clear();
  }


  public static vessel_info VesselInfo(Vessel v)
  {
    // get vessel id
    UInt32 id = Lib.VesselID(v);

    // get the info from the cache, if it exist
    vessel_info info;
    if (instance.vessels.TryGetValue(id, out info)) return info;

    // compute vessel info
    info = new vessel_info(v);

    // store vessel info in the cache
    instance.vessels.Add(id, info);

    // return the vessel info
    return info;
  }


  public static resource_info ResourceInfo(Vessel v, string resource_name)
  {
    resource_info info;
    UInt32 id = Lib.CombinedID(Lib.VesselID(v), Lib.Hash32(resource_name));
    if (instance.resources.TryGetValue(id, out info)) return info;

    info = new resource_info(v, resource_name);

    instance.resources.Add(id, info);

    return info;
  }


  // permit global access
  private static Cache instance = null;

  // vessel cache
  private Dictionary<UInt32, vessel_info> vessels = new Dictionary<UInt32, vessel_info>();

  // resource cache
  private Dictionary<UInt32, resource_info> resources = new Dictionary<UInt32, resource_info>();
}



} // KERBALISM