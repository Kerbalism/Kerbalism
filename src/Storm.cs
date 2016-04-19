// ====================================================================================================================
// implement solar storm mechanic
// ====================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {
  
  
[KSPAddon(KSPAddon.Startup.MainMenu, true)] 
public class Storm : MonoBehaviour
{ 
  // keep it alive
  Storm() { DontDestroyOnLoad(this); }
  
  // called every simulation tick
  public void FixedUpdate()
  {
    // do nothing if paused
    if (Lib.IsPaused()) return;
    
    // avoid case when DB isn't ready for whatever reason
    if (!DB.Ready()) return;
    
    // do nothing in the editors and the menus    
    if (!Lib.SceneIsGame()) return;
    
    // for each celestial body
    foreach(CelestialBody body in FlightGlobals.Bodies)
    {
      // skip the sun
      if (body.flightGlobalsIndex == 0) continue;
      
      // skip moons
      if (body.referenceBody.flightGlobalsIndex != 0) continue;
      
      // get body data
      body_data bd = DB.BodyData(body.name);
      
      // generate storm time if necessary
      if (bd.storm_time <= double.Epsilon)
      {
        bd.storm_time = Settings.StormMinTime + (Settings.StormMaxTime - Settings.StormMinTime) * Lib.RandomDouble();
      }
    
      // accumulate age
      bd.storm_age += TimeWarp.fixedDeltaTime * storm_frequency(body);
    
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
      else if (bd.storm_age > bd.storm_time - Settings.StormDuration  - time_to_impact(body))
      {
        bd.storm_state = 1;
      }
      
      // send messages
      // note: separed from state management to support the case when the user enter the SOI of a body under storm or about to be hit
      if (bd.msg_storm < 2 && bd.storm_state == 2)
      {
        if (body_is_relevant(body))
        {
          Message.Post(Severity.danger, "The coronal mass ejection hit <b>" + body.name + "</b> system",
            "Storm duration: " + Lib.HumanReadableDuration(TimeLeftCME(body)));
        }
        bd.msg_storm = 2;
      }
      else if (bd.msg_storm < 1 && bd.storm_state == 1)
      {
        if (body_is_relevant(body))
        {
          Message.Post(Severity.warning, "Our observatories report a coronal mass ejection directed toward <b>" + body.name + "</b> system",
            "Time to impact: " + Lib.HumanReadableDuration(TimeBeforeCME(body)));
        }
        bd.msg_storm = 1;
      }
      else if (bd.msg_storm > 1 && bd.storm_state == 0)
      {
        if (body_is_relevant(body))
        {
          Message.Post(Severity.relax, "The solar storm at <b>" + body.name + "</b> system is over");
        }
        bd.msg_storm = 0;
      }
    }
  }
  
  
  // influence the frequency of solar storms
  // - body: reference body of the planetary system
  double storm_frequency(CelestialBody body)
  {
    // note: we deal with the case of a planet mod setting homebody as a moon
    CelestialBody home = Lib.PlanetarySystem(FlightGlobals.GetHomeBody());
    return home.orbit.semiMajorAxis / body.orbit.semiMajorAxis;
  }
  
  
  // return time to impact from CME event, in seconds
  // - body: reference body of the planetary system
  double time_to_impact(CelestialBody body)
  {
    return body.orbit.semiMajorAxis / Settings.StormEjectionSpeed;
  }
  
  
  // return true if body is relevant to the player
  // - body: reference body of the planetary system
  bool body_is_relevant(CelestialBody body)
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
        // skip invalid vessels
        if (!Lib.IsVessel(v)) continue;
        
        // skip resque missions
        if (Lib.IsResqueMission(v)) continue;
        
        // skip dead eva kerbal
        if (EVA.IsDead(v)) continue;
        
        // body is relevant
        return true;
      }
    }
    return false;
  }
  
  
  // return true if a storm is incoming
  public static bool Incoming(CelestialBody body)
  {
    if (body.flightGlobalsIndex == 0) return false;
    return DB.Ready() && DB.BodyData(Lib.PlanetarySystem(body).name).storm_state == 1;
  }
  
  
  // return true if a storm is in progress
  public static bool InProgress(CelestialBody body)
  {
    if (body.flightGlobalsIndex == 0) return false;
    return DB.Ready() && DB.BodyData(Lib.PlanetarySystem(body).name).storm_state == 2;
  }
  
  
  // return true if a storm just ended
  // used to avoid sending 'signal is back' messages en-masse after the storm is over
  // - delta_time: time between calls to this function
  public static bool JustEnded(CelestialBody body, double delta_time)
  {
    if (body.flightGlobalsIndex == 0) return false;
    return DB.Ready() && DB.BodyData(Lib.PlanetarySystem(body).name).storm_age < TimeWarp.deltaTime * 2.0;
  }
  
  
  // return time left until CME impact
  public static double TimeBeforeCME(CelestialBody body)
  {
    if (body.flightGlobalsIndex == 0) return 0.0;
    if (!DB.Ready()) return 0.0;
    body_data bd = DB.BodyData(Lib.PlanetarySystem(body).name);
    return Math.Max(0.0, bd.storm_time - bd.storm_age - Settings.StormDuration);
  }
  
  
  // return time left until CME is over
  public static double TimeLeftCME(CelestialBody body)
  {
    if (body.flightGlobalsIndex == 0) return 0.0;
    if (!DB.Ready()) return 0.0;
    body_data bd = DB.BodyData(Lib.PlanetarySystem(body).name);
    return Math.Max(0.0, bd.storm_time - bd.storm_age);
  }
}


} // KERBALISM