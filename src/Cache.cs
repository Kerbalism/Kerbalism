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
  public double   food_depletion;     // time-to-depletion for food
  public double   oxygen_depletion;   // time-to-depletion for oxygen
  public double   food_consumption;   // food consumed per-second
  public double   oxygen_consumption; // oxygen consumed per-second
  public double   food_level;         // percentual of food
  public double   oxygen_level;       // percentual of oxygen
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
    info = new vessel_info();
    info.position = Lib.VesselPosition(v);
    info.sunlight = Sim.RaytraceBody(v, Sim.Sun(), out info.sun_dir, out info.sun_dist);
    info.temperature = Sim.Temperature(v, info.sunlight);
    info.cosmic_radiation = Radiation.CosmicRadiation(v);
    info.belt_radiation = Radiation.BeltRadiation(v);
    info.storm_radiation = Radiation.StormRadiation(v, info.sunlight);
    info.env_radiation = info.cosmic_radiation + info.belt_radiation + info.storm_radiation;
    info.food_depletion = LifeSupport.TimeToDepletionFood(v, out info.food_consumption, out info.food_level);
    info.oxygen_depletion = LifeSupport.TimeToDepletionOxygen(v, out info.oxygen_consumption, out info.oxygen_level);

    // store vessel info in the cache
    instance.vessels.Add(v.id, info);

    // return the vessel info
    return info;
  }
}


} // KERBALISM