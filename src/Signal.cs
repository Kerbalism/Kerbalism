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
  { this.linked = linked; this.status = status; this.distance = distance;
    this.path = new List<string>(); this.path_id = new List<UInt32>(); }

  public bool linked;             // true if linked, false otherwise
  public link_status status;      // kind of link
  public double distance;         // distance to first relay or home planet
  public List<string> path;       // list of relay names in reverse order
  public List<UInt32> path_id;    // list of relay ids in reverse order
}


[KSPAddon(KSPAddon.Startup.MainMenu, true)]
public sealed class Signal : MonoBehaviour
{
  // note: complexity
  // the original problem was O(N+N^N), N being the number of vessels
  // we reduce it to O(N+N^2) by building a visibility cache
  // we further reduce it to O(N+(N^2)/2) by storing indirect visibility per-pair and dealing with the relay asymmetry ad-hoc
  // finally, we do not compute indirect visibility in the case both vessels are directly visible, leading to
  // the final complexity O(N+N*M/2), M being the number of non-directly-visible vessels, always sensibly less than N

  // signal processing technologies
  public class SignalProcessing
  {
    public SignalProcessing()
    {
      var cfg = Lib.ParseConfig("Kerbalism/Patches/System/SignalProcessing");
      this.techs[0] = Lib.ConfigValue(cfg, "tech0", "advElectrics");
      this.techs[1] = Lib.ConfigValue(cfg, "tech1", "largeElectrics");
      this.techs[2] = Lib.ConfigValue(cfg, "tech2", "experimentalElectrics");
    }
    public string[] techs = {"", "", ""};
  }
  public static SignalProcessing signal_processing = new SignalProcessing();

  // store ranges
  Dictionary<string, double> range_values = new Dictionary<string, double>();

  // store data about antennas
  Dictionary<UInt32, antenna_data> antennas = new Dictionary<UInt32, antenna_data>();

  // store visibility cache between vessel and homebody
  // note: value store distance, visible if value > 0.0
  Dictionary<UInt32, double> direct_visibility_cache = new Dictionary<UInt32, double>();

  // store visibility cache between vessels
  // note: value store distance, visible if value > 0.0
  // note: the visibility stored is the relay-agnostic, symmetric one
  Dictionary<UInt32, double> indirect_visibility_cache = new Dictionary<UInt32, double>();

  // store link status
  Dictionary<UInt32, link_data> links = new Dictionary<UInt32, link_data>();

  // store list of active relays, and their relay cost
  Dictionary<UInt32, double> active_relays = new Dictionary<UInt32, double>();

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

    // generate default antenna scopes
    range_values.Add("orbit", home.sphereOfInfluence * 1.05);
    range_values.Add("home", home.sphereOfInfluence * 4.0 * 1.05);
    range_values.Add("near", (Sim.Apoapsis(home) + Sim.Apoapsis(near)) * 1.6);
    range_values.Add("far", (Sim.Apoapsis(home) + Sim.Apoapsis(far)) * 1.1);
    range_values.Add("extreme", (Sim.Apoapsis(home) + Sim.Apoapsis(far)) * 4.0);
    range_values.Add("medium", (range_values["near"] + range_values["far"]) * 0.5);

    // parse user-defined antenna scopes
    var user_scopes = Lib.ParseConfigs("AntennaScope");
    foreach(var scope in user_scopes)
    {
      string scope_name = Lib.ConfigValue(scope, "name", "").Trim();
      double scope_range = Lib.ConfigValue(scope, "range", 0.0);

      if (scope_name.Length > 0 && scope_range > double.Epsilon)
      {
        if (!range_values.ContainsKey(scope_name))
        {
          range_values.Add(scope_name, scope_range);
          Lib.Log("Added user-defined antenna scope '" + scope_name + "' with range " + Lib.HumanReadableRange(scope_range));
        }
        else
        {
          range_values[scope_name] = scope_range;
          Lib.Log("Using user-defined range " + Lib.HumanReadableRange(scope_range) + " for antenna scope '" + scope_name + "'");
        }
      }
    }
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
      // get info from the cache
      vessel_info vi = Cache.VesselInfo(v);

      // skip invalid vessels
      if (!vi.is_vessel) continue;

      // store best antenna values
      double best_range = 0.0;
      double best_relay_range = 0.0;
      double best_relay_cost = 0.0;

      // get ec available
      double ec_amount = Cache.ResourceInfo(v, "ElectricCharge").amount;

      // if the vessel is loaded
      if (v.loaded)
      {
        // choose the best antenna
        foreach(Antenna a in v.FindPartModulesImplementing<Antenna>())
        {
          double range = Range(a.scope, a.penalty, ecc);
          best_range = Math.Max(best_range, range);
          if (a.relay && range > best_relay_range && ec_amount >= a.relay_cost * TimeWarp.fixedDeltaTime)
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
              double range = Range(m.moduleValues.GetValue("scope"), Malfunction.Penalty(p, 0.7071), ecc);
              double relay_cost = Lib.Proto.GetDouble(m, "relay_cost");
              bool relay = Lib.Proto.GetBool(m, "relay");
              best_range = Math.Max(best_range, range);
              if (relay && range > best_relay_range && ec_amount >= relay_cost * TimeWarp.fixedDeltaTime)
              { best_relay_range = range; best_relay_cost = relay_cost; }
            }
          }
        }
      }

      // add antenna data
      antennas.Add(Lib.VesselID(v), new antenna_data(vi.position, best_range, best_relay_range, best_relay_cost));
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
      // get info from the cache
      vessel_info vi = Cache.VesselInfo(v);

      // skip invalid vessels
      if (!vi.is_vessel) continue;

      // get antenna data
      antenna_data ad = antennas[Lib.VesselID(v)];

      // raytrace home body
      Vector3d dir;
      double dist = 0.0;
      bool visible = Sim.RaytraceBody(v, home, out dir, out dist);
      dist = Math.Abs(dist); //< avoid problem below water level

      // store in visibility cache
      // note: we store distance & visibility flag at the same time
      direct_visibility_cache.Add(Lib.VesselID(v), visible && dist < ad.range ? dist : 0.0);
    }


    // build indirect visibility cache
    indirect_visibility_cache.Clear();
    foreach(Vessel v in FlightGlobals.Vessels)
    {
      // skip invalid vessels
      if (!Cache.VesselInfo(v).is_vessel) continue;

      // get id of first vessel
      UInt32 v_id = Lib.VesselID(v);

      // get antenna data
      antenna_data v_ad = antennas[v_id];

      // for each vessel
      foreach(Vessel w in FlightGlobals.Vessels)
      {
        // skip invalid vessels
        if (!Cache.VesselInfo(w).is_vessel) continue;

        // do not test with itself
        if (v == w) continue;

        // get id of second vessel
        UInt32 w_id = Lib.VesselID(w);

        // do not compute visibility when both vessels have a direct link
        // rationale: optimization, the indirect visibility it never used in this case
        if (direct_visibility_cache[v_id] > double.Epsilon && direct_visibility_cache[w_id] > double.Epsilon) continue;

        // generate combined id
        UInt32 id = Lib.CombinedID(v_id, w_id);

        // avoid raycasting the same pair twice
        if (indirect_visibility_cache.ContainsKey(id)) continue;

        // get antenna data
        antenna_data w_ad = antennas[w_id];

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


  public link_data ComputeLink(Vessel v, HashSet<UInt32> avoid_inf_recursion)
  {
    // get id of first vessel
    UInt32 v_id = Lib.VesselID(v);

    // if it has no antenna
    if (antennas[v_id].range <= double.Epsilon) return new link_data(false, link_status.no_antenna, 0.0);

    // if there is a storm and the vessel is inside a magnetosphere
    if (Blackout(v)) return new link_data(false, link_status.no_link, 0.0);

    // check for direct link
    // note: we also get distance from the cache and store it in the link
    double direct_visible_dist = direct_visibility_cache[v_id];
    bool direct_visible = direct_visible_dist > 0.0;
    if (direct_visible) return new link_data(true, link_status.direct_link, direct_visible_dist);

    // avoid infinite recursion
    avoid_inf_recursion.Add(v_id);

    // get antenna data
    antenna_data v_ad = antennas[v_id];

    // check for indirect link
    foreach(Vessel w in FlightGlobals.Vessels)
    {
      // skip invalid vessels
      if (!Cache.VesselInfo(w).is_vessel) continue;

      // get id of second vessel
      UInt32 w_id = Lib.VesselID(w);

      // avoid infinite recursion
      if (avoid_inf_recursion.Contains(w_id)) continue;

      // avoid testing against itself
      if (v_id == w_id) continue;

      // get antenna data
      antenna_data w_ad = antennas[w_id];

      // check for indirect link to home body
      // note: we also get distance from the cache
      // note: we check the relay range, that is asymmetric
      double indirect_visible_dist = indirect_visibility_cache[Lib.CombinedID(v_id, w_id)];
      bool indirect_visible = indirect_visible_dist > 0.0 && indirect_visible_dist < Math.Min(v_ad.range, w_ad.relay_range);
      if (indirect_visible)
      {
        // check link to home body, recursively
        link_data next_link = ComputeLink(w, avoid_inf_recursion);

        // if indirectly linked
        if (next_link.linked)
        {
          // flag the relay for ec consumption, but only once
          if (!active_relays.ContainsKey(w_id)) active_relays.Add(w_id, w_ad.relay_cost);

          // update the link data and return it
          next_link.status = link_status.indirect_link;
          next_link.distance = indirect_visible_dist; //< store distance of last link
          next_link.path.Add(w.vesselName);
          next_link.path_id.Add(w_id);
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
      if (!Cache.VesselInfo(v).is_vessel) continue;

      // generate and store link status
      link_data ld = ComputeLink(v, new HashSet<UInt32>());
      links.Add(Lib.VesselID(v), ld);

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
            Message.Post(Severity.warning, Lib.BuildString("Signal lost with <b>", v.vesselName, "</b>"),
              is_probe && Settings.RemoteControlLink ? "Remote control disabled" : "Data transmission disabled");
          }
        }
        else if (vd.msg_signal > 0 && ld.linked)
        {
          vd.msg_signal = 0;
          if (vd.cfg_signal == 1 && !Storm.JustEnded(v.mainBody, TimeWarp.deltaTime)) //< do not send messages after a storm
          {
            Message.Post(Severity.relax, Lib.BuildString("<b>", v.vesselName, "</b> signal is back"),
              ld.path.Count == 0 ? "We got a direct link with the space center" : Lib.BuildString("Relayed by <b>", ld.path[ld.path.Count - 1], "</b>"));
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
    // do nothing if signal mechanic is disabled
    if (!Kerbalism.features.signal) return;

    // get existing antennas
    BuildAntennas();

    // build visibility cache
    BuildVisibility();

    // build the links cache
    BuildLinks();

    // if there is an active vessel, and is valid
    Vessel v = FlightGlobals.ActiveVessel;
    if (v != null && Cache.VesselInfo(v).is_vessel)
    {
      // get link state
      link_data ld = links[Lib.VesselID(v)];

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
    // play nice with RemoteTech and AntennaRange
    if (!Kerbalism.features.signal) return;

    // do nothing if paused
    if (Lib.IsPaused()) return;

    // consume relay EC
    foreach(var p in active_relays)
    {
      // shortcuts
      UInt32 id = p.Key;
      double relay_cost = p.Value;

      // find the vessel
      Vessel v = FlightGlobals.Vessels.Find(k => Lib.VesselID(k) == id);

      // consume the relay ec
      if (v != null)
      {
        Lib.Resource.Request(v, "ElectricCharge", relay_cost * TimeWarp.fixedDeltaTime);
      }
    }
  }


  // return link status of a vessel
  public static link_data Link(Vessel v)
  {
    // assume linked if signal mechanic is disabled
    if (!Kerbalism.features.signal) return new link_data(true, link_status.direct_link, double.MaxValue);

    // if there isn't link data for the vessel, return 'no antenna'
    // this for example may happen when a resque mission vessel get enabled
    // this also can happen when Link() is called with a non-valid vessel (eg: debris)
    link_data ld;
    if (!instance.links.TryGetValue(Lib.VesselID(v), out ld)) ld.status = link_status.no_antenna;

    // return link status from the cache
    return ld;
  }


  // return range of an antenna
  static public double Range(string scope, double penalty, double ecc)
  {
    double range;
    return instance.range_values.TryGetValue(scope, out range) ? range * penalty * ecc : 0.0;
  }


  // get ecc level from tech
  static public double ECC()
  {
    double[] value = {0.15, 0.33, 0.66, 1.0};
    return value[Lib.CountTechs(signal_processing.techs)];
  }


  // return true if vessel is inside a magnetosphere and there is a storm in progress
  public static bool Blackout(Vessel v)
  {
    return Storm.InProgress(v.mainBody) && Radiation.InsideMagnetosphere(v);
  }
}


// manage a line object
public class Line : MonoBehaviour
{
  public Line()
  {
    lr = gameObject.AddComponent<LineRenderer>();
    lr.SetVertexCount(2);
    lr.material = MapView.OrbitLinesMaterial;
    gameObject.layer = 31;
    DontDestroyOnLoad(this);
  }

  public void OnDestroy()
  {
    Show(false);
    Destroy(lr);
  }

  public void Show(bool b)
  {
    lr.enabled = b;
    gameObject.SetActive(b);
  }

  public void UpdatePoints(Vector3d a, Vector3d b, Color clr)
  {
    var cam = PlanetariumCamera.Camera;
    a = cam.WorldToScreenPoint(ScaledSpace.LocalToScaledSpace(a));
    b = cam.WorldToScreenPoint(ScaledSpace.LocalToScaledSpace(b));

    if (a.z <= 0.0 || b.z <= 0.0) a = b; //< hack away

    if (!MapView.Draw3DLines)
    {
      a.z = b.z = 0.0f;
    }
    else
    {
      a = cam.ScreenToWorldPoint(a);
      b = cam.ScreenToWorldPoint(b);
    }

    float width = MapView.Draw3DLines ? MapView.MapCamera.Distance * 0.01f : 3.6f;
    lr.SetWidth(width, width);
    lr.SetPosition(0, a);
    lr.SetPosition(1, b);
    lr.SetColors(clr, clr);
  }


  public static Line Create()
  {
    return new GameObject(Lib.RandomInt(int.MaxValue).ToString(), typeof(Line)).GetComponent<Line>();
  }


  LineRenderer lr;
}



[KSPAddon(KSPAddon.Startup.MainMenu, true)]
public class LinkRenderer : MonoBehaviour
{
  // keep it alive
  LinkRenderer() { DontDestroyOnLoad(this); }

  // called at every frame
  public void Update()
  {
    // hide all lines
    foreach(var l in lines) l.Value.Show(false);

    // do nothing if db isn't ready
    if (!DB.Ready()) return;

    // do nothing if signal is disabled
    if (!Kerbalism.features.signal) return;

    // do nothing if not in map view or tracking station
    if (!MapView.MapIsEnabled) return;

    // get homebody position
    Vector3d home = FlightGlobals.GetHomeBody().position;

    // iterate all vessels
    foreach(Vessel v in FlightGlobals.Vessels)
    {
      // get info from the cache
      vessel_info vi = Cache.VesselInfo(v);

      // skip invalid vessels
      if (!vi.is_vessel) continue;

      // skip resque missions
      if (vi.is_resque) continue;

      // skip EVA kerbals
      if (v.isEVA) continue;

      // get vessel data
      vessel_data vd = DB.VesselData(v.id);

      // do nothing if showlink is disabled
      if (vd.cfg_showlink == 0) continue;

      // get link status
      link_data ld = Signal.Link(v);

      // if there is an antenna
      if (ld.status != link_status.no_antenna)
      {
        // get line renderer from the cache
        Line line = getLine(Lib.VesselID(v));

        // start of the line
        Vector3d a = vi.position;

        // determine end of line and color
        Vector3d b;
        Color clr;
        if (ld.status == link_status.direct_link)
        {
          b = home;
          clr = Color.green;
        }
        else if (ld.status == link_status.indirect_link)
        {
          Vessel relay = FlightGlobals.Vessels.Find(k => Lib.VesselID(k) == ld.path_id[ld.path.Count - 1]);
          if (relay == null) { line.Show(false); continue; } //< skip if it doesn't exist anymore
          b = Cache.VesselInfo(relay).position;
          clr = Color.yellow;
        }
        else // no link
        {
          b = home;
          clr = Color.red;
        }

        // setup the line and show it
        line.UpdatePoints(a, b, clr);
        line.Show(true);
      }
    }
  }

  // create a line renderer or return it from the cache
  Line getLine(UInt32 id)
  {
    Line line;
    if (!lines.TryGetValue(id, out line))
    {
      line = Line.Create();
      lines.Add(id, line);
    }
    return line;
  }

  // store unity line renderers per-vessel
  Dictionary<UInt32, Line> lines = new Dictionary<UInt32, Line>();
}


} // KERBALISM
