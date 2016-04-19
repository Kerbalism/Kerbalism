// ====================================================================================================================
// implement magnetosphere and radiation mechanics
// ====================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


[KSPAddon(KSPAddon.Startup.MainMenu, true)]
public class Radiation : MonoBehaviour
{
  class body_info
  {
    public double dynamo;               // used to determine magnetosphere & belt properties
    public double magn_altitude;        // magnetosphere boundary altitude, if any
    public double belt_altitude;        // radiation belt altitude, if any
  }

  // cache of body info, computed once
  Dictionary<int, body_info> bodies = new Dictionary<int, body_info>();

  // permit global access
  private static Radiation instance = null;

  // ctor
  Radiation()
  {
    // enable global access
    instance = this;

    // keep it alive
    DontDestroyOnLoad(this);

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

      // store magnetosphere info
      bodies.Add(body.flightGlobalsIndex, info);
    }
  }


  // implement radiation mechanics
  public void FixedUpdate()
  {
    // avoid case when DB isn't ready for whatever reason
    if (!DB.Ready()) return;

    // do nothing in the editors and the menus
    if (!Lib.SceneIsGame()) return;

    // do nothing if paused
    if (Lib.IsPaused()) return;

    // get time elapsed from last update
    double elapsed_s = TimeWarp.fixedDeltaTime;

    // for each vessel
    foreach(Vessel v in FlightGlobals.Vessels)
    {
      // skip invalid vessels
      if (!Lib.IsVessel(v)) continue;

      // skip dead eva kerbals
      if (EVA.IsDead(v)) continue;

      // get crew
      List<ProtoCrewMember> crew = v.loaded ? v.GetVesselCrew() : v.protoVessel.GetVesselCrew();

      // get crew count
      int crew_count = Lib.CrewCount(v);

      // get vessel info from the cache
      vessel_info info = Cache.VesselInfo(v);

      // get vessel data
      vessel_data vd = DB.VesselData(v.id);


      // belt warnings
      // note: we only show it for manned vesssels, but the first time we also show it for probes
      if (crew_count > 0 || DB.NotificationData().first_belt_crossing == 0)
      {
        if (InsideBelt(v) && vd.msg_belt < 1)
        {
          Message.Post("<b>" + v.vesselName + "</b> is crossing <i>" + v.mainBody.bodyName + " radiation belt</i>", "Exposed to extreme radiation");
          vd.msg_belt = 1;
          DB.NotificationData().first_belt_crossing = 1; //< record first belt crossing
        }
        else if (!InsideBelt(v) && vd.msg_belt > 0)
        {
          // no message after crossing the belt
          vd.msg_belt = 0;
        }
      }


      // for each crew
      foreach(ProtoCrewMember c in crew)
      {
        // get kerbal data
        kerbal_data kd = DB.KerbalData(c.name);

        // skip resque kerbals
        if (kd.resque == 1) continue;

        // skip disabled kerbals
        if (kd.disabled == 1) continue;

        // accumulate radiation
        kd.radiation += info.radiation * elapsed_s;

        // kill kerbal if necessary
        if (kd.radiation >= Settings.RadiationFatalThreshold)
        {
          Message.Post(Severity.fatality, KerbalEvent.radiation, v, c);
          Kerbalism.Kill(v, c);
        }
        // show warnings
        else if (kd.radiation >= Settings.RadiationDangerThreshold && kd.msg_radiation < 2)
        {
          Message.Post(Severity.danger, KerbalEvent.radiation, v, c);
          kd.msg_radiation = 2;
        }
        else if (kd.radiation >= Settings.RadiationWarningThreshold && kd.msg_radiation < 1)
        {
          Message.Post(Severity.danger, KerbalEvent.radiation, v, c);
          kd.msg_radiation = 1;
        }
        // note: no recovery from radiations
      }
    }
  }


  // return magnetism strength for a body
  public static double Dynamo(CelestialBody body)
  {
    if (body.flightGlobalsIndex == 0) return 0.0;
    return instance.bodies[body.flightGlobalsIndex].dynamo;
  }


  // return altitude of magnetosphere boundary for a body, if any
  public static double MagnAltitude(CelestialBody body)
  {
     if (body.flightGlobalsIndex == 0) return 0.0;
     return instance.bodies[body.flightGlobalsIndex].magn_altitude;
  }


  // return altitude of radiation belt for a body, if any
  public static double BeltAltitude(CelestialBody body)
  {
     if (body.flightGlobalsIndex == 0) return 0.0;
     return instance.bodies[body.flightGlobalsIndex].belt_altitude;
  }


  // return true if the body has a magnetosphere
  public static bool HasMagnetosphere(CelestialBody body)
  {
    if (body.flightGlobalsIndex == 0) return false;
    return instance.bodies[body.flightGlobalsIndex].magn_altitude > double.Epsilon;
  }


  // return true if the body has a radiation belt
  public static bool HasBelt(CelestialBody body)
  {
    if (body.flightGlobalsIndex == 0) return false;
    return instance.bodies[body.flightGlobalsIndex].belt_altitude > double.Epsilon;
  }


  // return true if a vessel is inside a magnetosphere
  public static bool InsideMagnetosphere(Vessel v)
  {
    if (v.mainBody.flightGlobalsIndex == 0) return false;
    return v.altitude < instance.bodies[v.mainBody.flightGlobalsIndex].magn_altitude;
  }


  // return true if a vessel is inside a radiation belt
  public static bool InsideBelt(Vessel v)
  {
    if (v.mainBody.flightGlobalsIndex == 0) return false;
    double belt_altitude = instance.bodies[v.mainBody.flightGlobalsIndex].belt_altitude;
    return Math.Abs(v.altitude - belt_altitude) < belt_altitude * Settings.BeltFalloff;
  }


  // return cosmic radiation hitting the vessel, in rad/s
  public static double CosmicRadiation(Vessel v)
  {
    if (v.mainBody.flightGlobalsIndex == 0) return Settings.CosmicRadiation;
    double magn_altitude = instance.bodies[v.mainBody.flightGlobalsIndex].magn_altitude;
    double magn_k = magn_altitude > double.Epsilon ? Lib.Clamp((v.altitude - magn_altitude) / (magn_altitude * Settings.MagnetosphereFalloff), 0.0, 1.0) : 1.0;
    return Settings.CosmicRadiation * magn_k;
  }


  // return belt radiation hitting the vessel, in rad/s
  public static double BeltRadiation(Vessel v)
  {
    if (v.mainBody.flightGlobalsIndex == 0) return 0.0;
    body_info info = instance.bodies[v.mainBody.flightGlobalsIndex];
    double belt_altitude = info.belt_altitude;
    double dynamo = info.dynamo;
    double belt_k = belt_altitude > double.Epsilon ? 1.0 - Math.Min(Math.Abs(v.altitude - belt_altitude) / (belt_altitude * Settings.BeltFalloff), 1.0) : 0.0;
    return Settings.BeltRadiation * dynamo * belt_k;
  }


  // return solar storm radiation hitting the vessel, in rad/s
  public static double StormRadiation(Vessel v, bool sunlight)
  {
    double storm_k = Storm.InProgress(v.mainBody) && !InsideMagnetosphere(v) && sunlight ? 1.0 : 0.0;
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
    return Shielding(Lib.GetResourceAmount(v, "Shielding"), Lib.GetResourceCapacity(v, "Shielding"));
  }


  // return a verbose description of shielding capability
  public static string ShieldingToString(double amount, double capacity)
  {
    double level = capacity > double.Epsilon ? amount / capacity : 0.0;
    if (level <= double.Epsilon) return "none";
    if (level <= 0.33) return "poor";
    if (level <= 0.66) return "moderate";
    if (level <= 0.99) return "decent";
    return "hardened";
  }
}


} // KERBALISM
