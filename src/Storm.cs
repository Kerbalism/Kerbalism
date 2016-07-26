// ====================================================================================================================
// implement space weather mechanics
// ====================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {



public sealed class Storm
{
  public void update(CelestialBody body, double elapsed_s)
  {
    // do nothing if storms are disabled
    if (Settings.StormDuration <= double.Epsilon) return;

    // skip the sun
    if (body.flightGlobalsIndex == 0) return;

    // skip moons
    // note: referenceBody is never null here
    if (body.referenceBody.flightGlobalsIndex != 0) return;

    // get body data
    body_data bd = DB.BodyData(body.name);

    // generate storm time if necessary
    if (bd.storm_time <= double.Epsilon)
    {
      bd.storm_time = Settings.StormMinTime + (Settings.StormMaxTime - Settings.StormMinTime) * Lib.RandomDouble();
    }

    // accumulate age
    bd.storm_age += elapsed_s * storm_frequency(body.orbit.semiMajorAxis);

    // if storm is over
    if (bd.storm_age > bd.storm_time)
    {
      bd.storm_age = 0.0;
      bd.storm_time = 0.0;
      bd.storm_state = 0;
    }
    // if storm is in progress
    else if (bd.storm_age > bd.storm_time - Settings.StormDuration)
    {
      bd.storm_state = 2;
    }
    // if storm is incoming
    else if (bd.storm_age > bd.storm_time - Settings.StormDuration  - time_to_impact(body.orbit.semiMajorAxis))
    {
      bd.storm_state = 1;
    }

    // send messages
    // note: separed from state management to support the case when the user enter the SOI of a body under storm or about to be hit
    if (bd.msg_storm < 2 && bd.storm_state == 2)
    {
      if (body_is_relevant(body))
      {
        Message.Post(Severity.danger, Lib.BuildString("The coronal mass ejection hit <b>", body.name, "</b> system"),
          Lib.BuildString("Storm duration: ", Lib.HumanReadableDuration(TimeLeftCME(bd.storm_time, bd.storm_age))));
      }
      bd.msg_storm = 2;
    }
    else if (bd.msg_storm < 1 && bd.storm_state == 1)
    {
      if (body_is_relevant(body))
      {
        Message.Post(Severity.warning, Lib.BuildString("Our observatories report a coronal mass ejection directed toward <b>", body.name, "</b> system"),
          Lib.BuildString("Time to impact: ", Lib.HumanReadableDuration(TimeBeforeCME(bd.storm_time, bd.storm_age))));
      }
      bd.msg_storm = 1;
    }
    else if (bd.msg_storm > 1 && bd.storm_state == 0)
    {
      if (body_is_relevant(body))
      {
        Message.Post(Severity.relax, Lib.BuildString("The solar storm at <b>", body.name, "</b> system is over"));
      }
      bd.msg_storm = 0;
    }
  }


  public void update(Vessel v, vessel_info vi, vessel_data vd, double elapsed_s)
  {
    // do nothing if storms are disabled
    if (Settings.StormDuration <= double.Epsilon) return;

    // only consider vessels in interplanetary space
    if (v.mainBody.flightGlobalsIndex != 0) return;

    // skip unmanned vessels
    if (vi.crew_count == 0) return;

    // generate storm time if necessary
    if (vd.storm_time <= double.Epsilon)
    {
      vd.storm_time = Settings.StormMinTime + (Settings.StormMaxTime - Settings.StormMinTime) * Lib.RandomDouble();
    }

    // accumulate age
    vd.storm_age += elapsed_s * storm_frequency(vi.sun_dist);

    // if storm is over
    if (vd.storm_age > vd.storm_time && vd.storm_state == 2)
    {
      vd.storm_age = 0.0;
      vd.storm_time = 0.0;
      vd.storm_state = 0;

      // send message
      Message.Post(Severity.relax, Lib.BuildString("The solar storm around <b>", v.vesselName, "</b> is over"));
    }
    // if storm is in progress
    else if (vd.storm_age > vd.storm_time - Settings.StormDuration && vd.storm_state == 1)
    {
      vd.storm_state = 2;

      // send message
      Message.Post(Severity.danger, Lib.BuildString("The coronal mass ejection hit <b>", v.vesselName, "</b>"),
      Lib.BuildString("Storm duration: ", Lib.HumanReadableDuration(TimeLeftCME(vd.storm_time, vd.storm_age))));
    }
    // if storm is incoming
    else if (vd.storm_age > vd.storm_time - Settings.StormDuration - time_to_impact(vi.sun_dist) && vd.storm_state == 0)
    {
      vd.storm_state = 1;

      // send message
      Message.Post(Severity.warning, Lib.BuildString("Our observatories report a coronal mass ejection directed toward <b>", v.vesselName, "</b>"),
      Lib.BuildString("Time to impact: ", Lib.HumanReadableDuration(TimeBeforeCME(vd.storm_time, vd.storm_age))));
    }
  }


  // return storm frequency factor by distance from sun
  static double storm_frequency(double dist)
  {
    return Kerbalism.AU / dist;
  }


  // return time to impact from CME event, in seconds
  static double time_to_impact(double dist)
  {
    return dist / Settings.StormEjectionSpeed;
  }


  // return true if body is relevant to the player
  // - body: reference body of the planetary system
  static bool body_is_relevant(CelestialBody body)
  {
    // special case: home system is always relevant
    // note: we deal with the case of a planet mod setting homebody as a moon
    if (body == Lib.PlanetarySystem(FlightGlobals.GetHomeBody())) return true;

    // for each vessel
    foreach(Vessel v in FlightGlobals.Vessels)
    {
      // if inside the system
      if (Lib.PlanetarySystem(v.mainBody) == body)
      {
        // get info from the cache
        vessel_info vi = Cache.VesselInfo(v);

        // skip invalid vessels
        if (!vi.is_valid) continue;

        // obey message config
        if (DB.VesselData(v.id).cfg_storm == 0) continue;

        // body is relevant
        return true;
      }
    }
    return false;
  }


  // used by the engine to update one body per-step
  public static bool skip_body(CelestialBody body)
  {
    // skip all bodies if storms are disabled
    if (Settings.StormDuration <= double.Epsilon) return true;

    // skip the sun
    if (body.flightGlobalsIndex == 0) return true;

    // skip moons
    // note: referenceBody is never null here
    if (body.referenceBody.flightGlobalsIndex != 0) return true;

    // do not skip the body
    return false;
  }


  // return true if a storm is incoming
  public static bool Incoming(Vessel v)
  {
    if (!DB.Ready()) return false;

    // if in interplanetary space
    if (v.mainBody.flightGlobalsIndex == 0)
    {
      return DB.VesselData(v.id).storm_state == 1;
    }
    // if inside a planetary system
    else
    {
      return DB.BodyData(Lib.PlanetarySystem(v.mainBody).name).storm_state == 1;
    }
  }


  // return true if a storm is in progress
  public static bool InProgress(Vessel v)
  {
    if (!DB.Ready()) return false;

    // if in interplanetary space
    if (v.mainBody.flightGlobalsIndex == 0)
    {
      return DB.VesselData(v.id).storm_state == 2;
    }
    // if inside a planetary system
    else
    {
      return DB.BodyData(Lib.PlanetarySystem(v.mainBody).name).storm_state == 2;
    }
  }


  // return true if a storm just ended
  // used to avoid sending 'signal is back' messages en-masse after the storm is over
  // - delta_time: time between calls to this function
  public static bool JustEnded(Vessel v, double delta_time)
  {
    if (!DB.Ready()) return false;

    // if in interplanetary space
    if (v.mainBody.flightGlobalsIndex == 0)
    {
      return DB.VesselData(v.id).storm_age < delta_time * 2.0;
    }
    // if inside a planetary system
    else
    {
      return DB.BodyData(Lib.PlanetarySystem(v.mainBody).name).storm_age < delta_time * 2.0;
    }
  }


  // return time left until CME impact
  static double TimeBeforeCME(double storm_time, double storm_age)
  {
    return Math.Max(0.0, storm_time - storm_age - Settings.StormDuration);
  }


  // return time left until CME impact
  public static double TimeBeforeCME(Vessel v)
  {
    if (!DB.Ready()) return 0.0;

    // if in interplanetary space
    if (v.mainBody.flightGlobalsIndex == 0)
    {
      vessel_data vd = DB.VesselData(v.id);
      return TimeBeforeCME(vd.storm_time, vd.storm_age);
    }
    // if inside a planetary system
    else
    {
      body_data bd = DB.BodyData(Lib.PlanetarySystem(v.mainBody).name);
      return TimeBeforeCME(bd.storm_time, bd.storm_age);
    }
  }


  // return time left until CME is over
  static double TimeLeftCME(double storm_time, double storm_age)
  {
    return Math.Max(0.0, storm_time - storm_age);
  }


  // return time left until CME is over
  public static double TimeLeftCME(Vessel v)
  {
    if (!DB.Ready()) return 0.0;

    // if in interplanetary space
    if (v.mainBody.flightGlobalsIndex == 0)
    {
      vessel_data vd = DB.VesselData(v.id);
      return TimeLeftCME(vd.storm_time, vd.storm_age);
    }
    // if inside a planetary system
    else
    {
      body_data bd = DB.BodyData(Lib.PlanetarySystem(v.mainBody).name);
      return TimeLeftCME(bd.storm_time, bd.storm_age);
    }
  }
}


} // KERBALISM