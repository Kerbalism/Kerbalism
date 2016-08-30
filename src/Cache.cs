// ====================================================================================================================
// cache for vessel-related data, using a smart eviction strategy to decouple computation time per-step from number of vessels
// ====================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {



public class vessel_info
{
  public vessel_info(Vessel v, uint vessel_id, UInt64 inc)
  {
    // NOTE: anything used here can't in turn use cache, unless you know what you are doing

    // associate with an unique incremental id
    this.inc = inc;

    // determine if this is a valid vessel
    is_vessel = Lib.IsVessel(v);
    if (!is_vessel) return;

    // determine if this is a resque mission vessel
    is_resque = Lib.IsResqueMission(v);
    if (is_resque) return;

    // dead EVA are not valid vessels
    if (v.isEVA && EVA.KerbalData(v).eva_dead) return;

    // shortcut for common tests
    is_valid = true;

    // generate id once
    id = vessel_id;

    // calculate crew info for the vessel
    crew_count = Lib.CrewCount(v);
    crew_capacity = Lib.CrewCapacity(v);

    // get vessel position once
    position = Lib.VesselPosition(v);

    // determine if in sunlight, calculate sun direction and distance
    sunlight = Sim.RaytraceBody(v, position, FlightGlobals.Bodies[0], out sun_dir, out sun_dist) ? 1.0 : 0.0;

    // if the orbit length vs simulation step is lower than an acceptable threshold, use discrete sun visibility
    if (v.mainBody.flightGlobalsIndex != 0)
    {
      double orbit_period = Sim.OrbitalPeriod(v);
      if (orbit_period / Kerbalism.elapsed_s < 16.0) sunlight = 1.0 - Sim.ShadowPeriod(v) / orbit_period;
    }

    // calculate environment stuff
    atmo_factor = Sim.AtmosphereFactor(v.mainBody, position, sun_dir);
    gamma_transparency = Sim.GammaTransparency(v.mainBody, v.altitude);
    breathable = Sim.Breathable(v);
    landed = Lib.Landed(v);

    // calculate temperature at vessel position
    temperature = Sim.Temperature(v, position, sunlight, atmo_factor, out solar_flux, out albedo_flux, out body_flux, out total_flux);

    // calculate radiation
    radiation = Radiation.Compute(v, position, gamma_transparency, sunlight, out blackout, out inside_pause, out inside_belt);

    // calculate malfunction stuff
    max_malfunction = Malfunction.MaxMalfunction(v);
    avg_quality = Malfunction.AverageQuality(v);

    // calculate signal info
    antenna = new antenna_data(v);
    avoid_inf_recursion.Add(v.id);
    link = Signal.Link(v, position, antenna, blackout, avoid_inf_recursion);
    avoid_inf_recursion.Remove(v.id);

    // partial data about modules, used by vessel info/monitor
    scrubbers = Scrubber.PartialData(v);
    recyclers = Recycler.PartialData(v);
    greenhouses = Greenhouse.PartialData(v);

    // woot relativity
    time_dilation = Sim.TimeDilation(v);

  }


  public UInt64   inc;                // unique incremental id for the entry
  public bool     is_vessel;          // true if this is a valid vessel
  public bool     is_resque;          // true if this is a resque mission vessel
  public bool     is_valid;           // equivalent to (is_vessel && !is_resque && !eva_dead)
  public UInt32   id;                 // generate the id once
  public int      crew_count;         // number of crew on the vessel
  public int      crew_capacity;      // crew capacity of the vessel
  public Vector3d position;           // vessel position
  public double   sunlight;           // if the vessel is in direct sunlight
  public Vector3d sun_dir;            // normalized vector from vessel to sun
  public double   sun_dist;           // distance from vessel to sun
  public double   solar_flux;         // solar flux at vessel position
  public double   albedo_flux;        // solar flux reflected from the nearest body
  public double   body_flux;          // infrared radiative flux from the nearest body
  public double   total_flux;         // total flux at vessel position
  public double   temperature;        // vessel temperature
  public double   radiation;          // environment radiation at vessel position
  public bool     inside_pause;       // true if vessel is inside a magnetopause (except the heliosphere)
  public bool     inside_belt;        // true if vessel is inside a radiation belt
  public bool     blackout;           // true if the vessel is inside a magnetopause (except the sun) and under storm
  public double   atmo_factor;        // proportion of flux not blocked by atmosphere
  public double   gamma_transparency; // proportion of ionizing radiation not blocked by atmosphere
  public bool     breathable;         // true if inside breathable atmosphere
  public bool     landed;             // true if on the surface of a body
  public uint     max_malfunction;    // max malfunction level among all vessel modules
  public double   avg_quality;        // average manufacturing quality among all vessel modules
  public antenna_data antenna;        // best antenna/relay data
  public link_data link;              // link data
  static HashSet<Guid> avoid_inf_recursion = new HashSet<Guid>(); // used to avoid infinite recursion while calculating link data
  public List<Scrubber.partial_data> scrubbers;       // partial module data
  public List<Recycler.partial_data> recyclers;       // ..
  public List<Greenhouse.partial_data> greenhouses;   // ..
  public double   time_dilation;      // time dilation effect according to special relativity


}


public sealed class Cache
{
  // ctor
  public Cache()
  {
    // enable global access
    instance = this;
  }


  public void update()
  {
    // purge the oldest entry from the vessel cache
    UInt64 oldest_inc = UInt64.MaxValue;
    UInt32 oldest_id = 0;
    foreach(KeyValuePair<UInt32, vessel_info> pair in vessels)
    {
      if (pair.Value.inc < oldest_inc)
      {
        oldest_inc = pair.Value.inc;
        oldest_id = pair.Key;
      }
    }
    if (oldest_id > 0) vessels.Remove(oldest_id);
  }


  public static vessel_info VesselInfo(Vessel v)
  {
    // get vessel id
    UInt32 id = Lib.VesselID(v);

    // get the info from the cache, if it exist
    vessel_info info;
    if (instance.vessels.TryGetValue(id, out info)) return info;

    // compute vessel info
    info = new vessel_info(v, id, instance.next_inc++);

    // store vessel info in the cache
    instance.vessels.Add(id, info);

    // return the vessel info
    return info;
  }


  public static vessel_info TryGetVesselInfo(Vessel v)
  {
    // get vessel id
    UInt32 id = Lib.VesselID(v);

    // get the info from the cache, if it exist
    // if it doesn't, don't create it and return null
    vessel_info info;
    return instance.vessels.TryGetValue(id, out info) ? info : null;
  }


  // vessel cache
  Dictionary<UInt32, vessel_info> vessels = new Dictionary<UInt32, vessel_info>(512);

  // used to generate unique id
  UInt64 next_inc;

  // permit global access
  static Cache instance;
}


} // KERBALISM