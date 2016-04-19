// ====================================================================================================================
// monitor vessel resources and show warning messages
// ====================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {
  
  
// runtime antenna data
public struct antenna_data
{  
  public antenna_data(Vector3d pos, double range, double relay_range, double relay_cost)
  { this.pos = pos; this.range = range; this.relay_range = relay_range; this.relay_cost = relay_cost; }
  
  public Vector3d pos;           // antenna position
  public double range;           // best range of all antennas
  public double relay_range;     // best range of all relay antennas
  public double relay_cost;      // the EC/s relay cost of the best relay antenna
}


// types of link status
public enum link_status { no_antenna, no_link, indirect_link, direct_link };
    
    
// runtime link data
public struct link_data
{  
  public link_data(bool linked, link_status status, double distance)
  { this.linked = linked; this.status = status; this.distance = distance; path = new List<string>(); }
  
  public bool linked;             // true if linked, false otherwise
  public link_status status;      // kind of link
  public double distance;         // distance to first relay or home planet
  public List<string> path;       // list of relay names in reverse order
}
    
  
[KSPAddon(KSPAddon.Startup.MainMenu, true)] 
public class Signal : MonoBehaviour
{   
  // note: complexity
  // the original problem was O(N+N^N), N being the number of vessels
  // we reduce it to O(N+N^2) by building a visibility cache
  // we further reduce it to O(N+(N^2)/2) by storing indirect visibility per-pair and dealing with the relay asymmetry ad-hoc
  // finally, we do not compute indirect visibility in the case both vessels are directly visible, leading to
  // the final complexity O(N+N*M/2), M being the number of non-directly-visible vessels, always sensibly less than N
  
  // store ranges
  Dictionary<string, double> range_values = new Dictionary<string, double>();
  
  // store data about antennas
  Dictionary<Guid, antenna_data> antennas = new Dictionary<Guid, antenna_data>();
  
  // store visibility cache between vessel and homebody
  // note: value store distance, visible if value > 0.0
  Dictionary<Guid, double> direct_visibility_cache = new Dictionary<Guid, double>();
  
  // store visibility cache between vessels
  // note: value store distance, visible if value > 0.0
  // note: the visibility stored is the relay-agnostic, symmetric one
  Dictionary<Guid, double> indirect_visibility_cache = new Dictionary<Guid, double>();
  
  // store link status
  Dictionary<Guid, link_data> links = new Dictionary<Guid, link_data>();
  
  // store list of active relays, and their relay cost
  Dictionary<Guid, double> active_relays = new Dictionary<Guid, double>();
  
  // permit global access
  private static Signal instance = null;
  
  
  Signal()
  {
    // enable global access
    instance = this;
    
    // keep it alive
    DontDestroyOnLoad(this);
    
    // determine nearest and furthest planets from home body
    CelestialBody sun = Sim.Sun();
    CelestialBody home = Lib.PlanetarySystem(FlightGlobals.GetHomeBody());
    CelestialBody near = null;
    CelestialBody far = null;
    double min_dist = double.MaxValue;
    double max_dist = double.MinValue;
    foreach(CelestialBody body in FlightGlobals.Bodies)
    {
      if (body == sun || body == home) continue;
      if (body.referenceBody != sun) continue;
      double dist = Math.Abs(home.orbit.semiMajorAxis - body.orbit.semiMajorAxis);
      if (dist < min_dist) { min_dist = dist; near = body; }
      if (dist > max_dist) { max_dist = dist; far = body; }
    }
    range_values.Add("orbit", home.sphereOfInfluence * 1.05);
    range_values.Add("home", home.sphereOfInfluence * 4.0 * 1.05);
    range_values.Add("near", (Sim.Apoapsis(home) + Sim.Apoapsis(near)) * 1.6);
    range_values.Add("far", (Sim.Apoapsis(home) + Sim.Apoapsis(far)) * 1.1);
    range_values.Add("extreme", (Sim.Apoapsis(home) + Sim.Apoapsis(far)) * 4.0);
  }
  
  
  // fill antennas data
  void BuildAntennas()
  {
    // get error-correcting code factor
    double ecc = ECC();
    
    // forget previous antennas
    antennas.Clear();
    
    // for each vessel
    foreach(Vessel v in FlightGlobals.Vessels)
    {     
      // skip invalid vessels
      if (!Lib.IsVessel(v)) continue;
      
      // store best antenna values
      double best_range = 0.0;
      double best_relay_range = 0.0;
      double best_relay_cost = 0.0;
      
      // get ec available
      double ec_amount = Lib.GetResourceAmount(v, "ElectricCharge");
      
      // if the vessel is loaded
      if (v.loaded)
      {
        // choose the best antenna
        foreach(Antenna a in v.FindPartModulesImplementing<Antenna>())
        {
          double range = Range(a.scope, a.penalty, ecc);
          best_range = Math.Max(best_range, range);
          if (a.relay && range > best_relay_range && ec_amount >= a.relay_cost * TimeWarp.deltaTime)
          { best_relay_range = range; best_relay_cost = a.relay_cost; }
        }
      }
      // if the vessel isn't loaded
      else
      { 
        // choose the best antenna
        foreach(ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
        {
          foreach(ProtoPartModuleSnapshot m in p.modules)
          {
            if (m.moduleName == "Antenna")
            {
              double range = Range(m.moduleValues.GetValue("scope"), Malfunction.Penalty(p), ecc);
              double relay_cost = Lib.GetProtoValue<double>(m, "relay_cost");
              bool relay = Lib.GetProtoValue<bool>(m, "relay");
              best_range = Math.Max(best_range, range);
              if (relay && range > best_relay_range && ec_amount >= relay_cost * TimeWarp.deltaTime)
              { best_relay_range = range; best_relay_cost = relay_cost; }
            }
          }
        }
      }
      
      // add antenna data
      antennas.Add(v.id, new antenna_data(Lib.VesselPosition(v), best_range, best_relay_range, best_relay_cost));
    }
  }
  
  
  // build the visibility caches
  void BuildVisibility()
  {
    // get home body
    CelestialBody home = FlightGlobals.GetHomeBody();
    
    // build direct visibility cache
    direct_visibility_cache.Clear();    
    foreach(Vessel v in FlightGlobals.Vessels)
    {
      // skip invalid vessels
      if (!Lib.IsVessel(v)) continue;
      
      // get antenna data
      antenna_data ad = antennas[v.id];
      
      // raytrace home body
      Vector3d dir;
      double dist = 0.0;
      bool visible = Sim.RaytraceBody(v, home, out dir, out dist);
      dist = Math.Abs(dist); //< avoid problem below water level
      
      // store in visibility cache
      // note: we store distance & visibility flag at the same time
      direct_visibility_cache.Add(v.id, visible && dist < ad.range ? dist : 0.0);
    }
    
    
    // build indirect visibility cache
    indirect_visibility_cache.Clear();
    foreach(Vessel v in FlightGlobals.Vessels)
    {
      // skip invalid vessels
      if (!Lib.IsVessel(v)) continue;
      
      // get antenna data
      antenna_data v_ad = antennas[v.id];
      
      // for each vessel
      foreach(Vessel w in FlightGlobals.Vessels)
      {
        // skip invalid vessels
        if (!Lib.IsVessel(w)) continue;
      
        // do not test with itself
        if (v == w) continue;
        
        // do not compute visibility when both vessels have a direct link
        // rationale: optimization, the indirect visibility it never used in this case
        if (direct_visibility_cache[v.id] > double.Epsilon && direct_visibility_cache[w.id] > double.Epsilon) continue;
        
        // generate ordered merged guid
        Guid id = Lib.CombineGuid(v.id, w.id);
        
        // avoid raycasting the same pair twice
        if (indirect_visibility_cache.ContainsKey(id)) continue;
        
        // get antenna data
        antenna_data w_ad = antennas[w.id];
        
        // raytrace the vessel
        Vector3d dir;
        double dist = 0.0;
        bool visible = Sim.RaytraceVessel(v, w, out dir, out dist);
        
        // store visibility in cache
        // note: we store distance & visibility flag at the same time
        // note: relay visibility is asymmetric, done at link build time
        indirect_visibility_cache.Add(id, visible && dist < Math.Min(v_ad.range, w_ad.range) ? dist : 0.0);
      }
    }
  } 
  
  
  public link_data ComputeLink(Vessel v, HashSet<Guid> avoid_inf_recursion)
  { 
    // if it has no antenna
    if (antennas[v.id].range <= double.Epsilon) return new link_data(false, link_status.no_antenna, 0.0);
    
    // if there is a storm and the vessel is inside a magnetosphere
    if (Blackout(v)) return new link_data(false, link_status.no_link, 0.0);
    
    // check for direct link
    // note: we also get distance from the cache and store it in the link
    double direct_visible_dist = direct_visibility_cache[v.id];
    bool direct_visible = direct_visible_dist > 0.0;
    if (direct_visible) return new link_data(true, link_status.direct_link, direct_visible_dist);
    
    // avoid infinite recursion
    avoid_inf_recursion.Add(v.id);
    
    // get antenna data
    antenna_data v_ad = antennas[v.id];
    
    // check for indirect link
    foreach(Vessel w in FlightGlobals.Vessels)
    {
      // skip invalid vessels
      if (!Lib.IsVessel(w)) continue;
      
      // avoid infinite recursion
      if (avoid_inf_recursion.Contains(w.id)) continue;
      
      // avoid testing against itself
      if (v == w) continue;
      
      // get antenna data
      antenna_data w_ad = antennas[w.id];
     
      // check for indirect link to home body
      // note: we also get distance from the cache
      // note: we check the relay range, that is asymmetric
      double indirect_visible_dist = indirect_visibility_cache[Lib.CombineGuid(v.id, w.id)];
      bool indirect_visible = indirect_visible_dist > 0.0 && indirect_visible_dist < Math.Min(v_ad.range, w_ad.relay_range);
      if (indirect_visible)
      {
        // check link to home body, recursively
        link_data next_link = ComputeLink(w, avoid_inf_recursion);
        
        // if indirectly linked
        if (next_link.linked)
        {
          // flag the relay for ec consumption, but only once
          if (!active_relays.ContainsKey(w.id)) active_relays.Add(w.id, w_ad.relay_cost);
          
          // update the link data and return it
          next_link.status = link_status.indirect_link;
          next_link.distance = indirect_visible_dist; //< store distance of last link
          next_link.path.Add(w.vesselName);
          return next_link;
        }
      }     
    }
    
    // no link
    return new link_data(false, link_status.no_link, 0.0);
  }
  
  
  public void BuildLinks()
  {
    // clear links container
    links.Clear();
    
    // clear active relays container
    active_relays.Clear();
    
    // iterate over all vessels
    foreach(Vessel v in FlightGlobals.Vessels)
    {
      // skip invalid vessels
      if (!Lib.IsVessel(v)) continue;
      
      // generate and store link status
      link_data ld = ComputeLink(v, new HashSet<Guid>());
      links.Add(v.id, ld);
      
      // maintain and send messages
      // - do nothing if db isn't ready
      // - do not send messages for vessels without an antenna  
      if (DB.Ready() && ld.status != link_status.no_antenna)
      {       
        vessel_data vd = DB.VesselData(v.id);       
        if (vd.msg_signal < 1 && !ld.linked)
        {
          vd.msg_signal = 1;
          if (DB.Ready()) DB.NotificationData().first_signal_loss = 1; //< record first signal loss
          if (vd.cfg_signal == 1 && !Blackout(v)) //< do not send message during storms
          {
            bool is_probe = (v.loaded ? v.GetCrewCount() == 0 : v.protoVessel.GetVesselCrew().Count == 0);
            Message.Post(Severity.warning, "Signal lost with <b>" + v.vesselName + "</b>", is_probe ? "Remote control disabled" : "Data transmission disabled");
          }
        }
        else if (vd.msg_signal > 0 && ld.linked)
        {
          vd.msg_signal = 0;
          if (vd.cfg_signal == 1 && !Storm.JustEnded(v.mainBody, TimeWarp.deltaTime)) //< do not send messages after a storm
          {
            Message.Post(Severity.relax, "<b>" + v.vesselName + "</b> signal is back",
              ld.path.Count == 0 ? "We got a direct link with the space center" : "Relayed by <b>" + ld.path[ld.path.Count - 1] + "</b>");
          }
        }
      }
    }
  }
  
  
  // update every frame
  // note: we don't do it every simulation step because:
  // - performance
  // - during scene changes the vessel list change asynchronously, but is synched every frame, apparently
  public void Update()
  {         
    // get existing antennas
    BuildAntennas();
      
    // build visibility cache
    BuildVisibility();
      
    // build the links cache
    BuildLinks();
    
    // if there is an active vessel, and is valid
    Vessel v = FlightGlobals.ActiveVessel;
    if (v != null && Lib.IsVessel(v))
    { 
      // get link state
      link_data ld = links[v.id];
      
      // for each antenna
      foreach(Antenna m in v.FindPartModulesImplementing<Antenna>())
      {       
        // remove incomplete data toggle
        m.Events["TransmitIncompleteToggle"].active = false;
        
        // enable/disable science transmission
        m.can_transmit = ld.linked;
        
        // store transmission distance in the antenna
        m.transmission_distance = ld.distance;
      } 
    }
  }
  
  
  public void FixedUpdate()
  {
    // do nothing if paused
    if (Lib.IsPaused()) return;
    
    // consume relay EC
    foreach(var p in active_relays)
    {
      // shortcuts
      Guid id = p.Key;
      double relay_cost = p.Value;
      
      // find the vessel
      Vessel v = FlightGlobals.Vessels.Find(k => k.id == id);
      
      // consume the relay ec
      if (v != null)
      {
        Lib.RequestResource(v, "ElectricCharge", relay_cost * TimeWarp.fixedDeltaTime);
      }
    }
  }
  
  
  // return link status of a vessel
  public static link_data Link(Vessel v)
  {       
    // if, for some reasons, there isn't link data for the vessel, return 'no antenna'
    // note: this for example may happen when a resque mission vessel get enabled
    link_data ld;
    if (!instance.links.TryGetValue(v.id, out ld)) ld.status = link_status.no_antenna;
    
    // return link status from the cache
    return ld;    
  }
  
  
  // return range of an antenna
  static public double Range(string scope, double penalty, double ecc)
  {
    return instance.range_values[scope] *  penalty * ecc;
  }
  
  
  // get ecc level from tech
  static public double ECC()
  {
    if (ResearchAndDevelopment.GetTechnologyState("experimentalElectrics") == RDTech.State.Available) return 1.0;
    else if (ResearchAndDevelopment.GetTechnologyState("largeElectrics") == RDTech.State.Available) return 0.66;
    else if (ResearchAndDevelopment.GetTechnologyState("advElectrics") == RDTech.State.Available) return 0.33;
    else return 0.15; // "start"
  }
  
  
  // return true if vessel is inside a magnetosphere and there is a storm in progress
  public static bool Blackout(Vessel v)
  {
    return Storm.InProgress(v.mainBody) && Radiation.InsideMagnetosphere(v);
  }
}


} // KERBALISM