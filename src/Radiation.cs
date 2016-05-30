// ====================================================================================================================
// implement magnetosphere and radiation mechanics
// ====================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public static class Radiation
{
  class body_info
  {
    public double dynamo;               // used to determine magnetosphere & belt properties
    public double magn_altitude;        // magnetosphere boundary altitude, if any
    public double belt_altitude;        // radiation belt altitude, if any
  }

  // cache of body info, computed once
  static Dictionary<int, body_info> bodies = new Dictionary<int, body_info>();

  // ctor
  static Radiation()
  {
    // compute magnetosphere of bodies
    CelestialBody home = FlightGlobals.GetHomeBody();
    double home_surfspeed = Sim.SurfaceSpeed(home);
    double home_surfgrav = Sim.SurfaceGravity(home);
    foreach(CelestialBody body in FlightGlobals.Bodies)
    {
      // skip the sun
      if (body.flightGlobalsIndex == 0) continue;

      // get body parameters and normalize them against home body
      double surfspeed = Sim.SurfaceSpeed(body);
      double surfgrav = Sim.SurfaceGravity(body);
      double norm_radius = body.Radius / home.Radius;
      double norm_surfspeed = surfspeed / home_surfspeed;
      double norm_surfgrav = surfgrav / home_surfgrav;

      // store magnetosphere info
      body_info info = new body_info();

      // deduce magnetic strength from body parameters
      info.dynamo = norm_radius * norm_surfspeed * norm_surfgrav / (Math.Min(norm_radius, Math.Min(norm_surfspeed, norm_surfgrav)));

      // deduce magnetopause from body parameters
      // - if magnetic strength is below a threshold, there is no magnetosphere
      // - magnetopause has to be higher than double the atmosphere (if any)
      // - magnetopause has to be higher than 1/2 radii
      info.magn_altitude = info.dynamo > 0.0666 ? Math.Max(surfspeed * 33.33 * norm_surfgrav * 1000.0, Math.Max(body.atmosphereDepth * 2.0, body.Radius * 0.5)) : 0.0;

      // deduce radiation belt
      // - if magnetic strength is below a threshold, there is no belt
      // - if magnetopause is lower than 2 radii, there is no belt
      info.belt_altitude = info.dynamo > 0.1888 && info.magn_altitude > body.Radius * 2.0 ? body.Radius : 0.0;

      // add magnetosphere info to the cache
      bodies.Add(body.flightGlobalsIndex, info);
    }
  }


  // return magnetism strength for a body
  public static double Dynamo(CelestialBody body)
  {
    if (body.flightGlobalsIndex == 0) return 0.0;
    return bodies[body.flightGlobalsIndex].dynamo;
  }


  // return altitude of magnetosphere boundary for a body, if any
  public static double MagnAltitude(CelestialBody body)
  {
     if (body.flightGlobalsIndex == 0) return 0.0;
     return bodies[body.flightGlobalsIndex].magn_altitude;
  }


  // return altitude of radiation belt for a body, if any
  public static double BeltAltitude(CelestialBody body)
  {
     if (body.flightGlobalsIndex == 0) return 0.0;
     return bodies[body.flightGlobalsIndex].belt_altitude;
  }


  // return true if the body has a magnetosphere
  public static bool HasMagnetosphere(CelestialBody body)
  {
    if (body.flightGlobalsIndex == 0) return false;
    return bodies[body.flightGlobalsIndex].magn_altitude > double.Epsilon;
  }


  // return true if the body has a radiation belt
  public static bool HasBelt(CelestialBody body)
  {
    if (body.flightGlobalsIndex == 0) return false;
    return bodies[body.flightGlobalsIndex].belt_altitude > double.Epsilon;
  }


  // return true if a vessel is inside a magnetosphere
  public static bool InsideMagnetosphere(Vessel v)
  {
    if (v.mainBody.flightGlobalsIndex == 0) return false;
    return v.altitude < bodies[v.mainBody.flightGlobalsIndex].magn_altitude;
  }


  // return true if a vessel is inside a radiation belt
  public static bool InsideBelt(Vessel v)
  {
    if (v.mainBody.flightGlobalsIndex == 0) return false;
    double belt_altitude = bodies[v.mainBody.flightGlobalsIndex].belt_altitude;
    return Math.Abs(v.altitude - belt_altitude) < belt_altitude * Settings.BeltFalloff;
  }


  // return cosmic radiation hitting the vessel, in rad/s
  public static double CosmicRadiation(Vessel v)
  {
    if (v.mainBody.flightGlobalsIndex == 0) return Settings.CosmicRadiation;
    double magn_altitude = bodies[v.mainBody.flightGlobalsIndex].magn_altitude;
    double magn_k = magn_altitude > double.Epsilon ? Lib.Clamp((v.altitude - magn_altitude) / (magn_altitude * Settings.MagnetosphereFalloff), 0.0, 1.0) : 1.0;
    return Settings.CosmicRadiation * magn_k;
  }


  // return belt radiation hitting the vessel, in rad/s
  public static double BeltRadiation(Vessel v)
  {
    if (v.mainBody.flightGlobalsIndex == 0) return 0.0;
    body_info info = bodies[v.mainBody.flightGlobalsIndex];
    double belt_altitude = info.belt_altitude;
    double dynamo = info.dynamo;
    double belt_k = belt_altitude > double.Epsilon ? 1.0 - Math.Min(Math.Abs(v.altitude - belt_altitude) / (belt_altitude * Settings.BeltFalloff), 1.0) : 0.0;
    return Settings.BeltRadiation * dynamo * belt_k;
  }


  // return solar storm radiation hitting the vessel, in rad/s
  public static double StormRadiation(Vessel v, double sunlight)
  {
    double storm_k = (Storm.InProgress(v.mainBody) && !InsideMagnetosphere(v) ? 1.0 : 0.0) * sunlight;
    return Settings.StormRadiation * storm_k;
  }


  // return percentual of radiations blocked by shielding
  public static double Shielding(double amount, double capacity)
  {
    return capacity > double.Epsilon ? Settings.ShieldingEfficiency * amount / capacity : 0.0;
  }


  // return percentage of radiations blocked by shielding
  public static double Shielding(Vessel v)
  {
    return Shielding(Lib.Resource.Amount(v, "Shielding"), Lib.Resource.Capacity(v, "Shielding"));
  }


  public static double Shielding(ConnectedLivingSpace.ICLSSpace space)
  {
    double amount = 0.0;
    double capacity = 0.0;
    foreach(var part in space.Parts)
    {
      amount += Lib.Resource.Amount(part.Part, "Shielding");
      capacity += Lib.Resource.Capacity(part.Part, "Shielding");
    }
    return Shielding(amount, capacity);
  }


  // return a verbose description of shielding capability
  public static string ShieldingToString(double shielding_factor)
  {
    if (shielding_factor <= double.Epsilon) return "none";
    if (shielding_factor <= 0.25) return "poor";
    if (shielding_factor <= 0.50) return "moderate";
    if (shielding_factor <= 0.75) return "decent";
    return "hardened";
  }


  // return a verbose description of shielding capability
  public static string ShieldingToString(double amount, double capacity)
  {
    double level = capacity > double.Epsilon ? amount / capacity : 0.0;
    return ShieldingToString(level);
  }
}


} // KERBALISM
