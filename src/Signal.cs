// ====================================================================================================================
// the signal system
// ====================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public class antenna_data
{
  public antenna_data(Vessel v)
  {
    // [disabled] EVA kerbals have an implicit antenna
    //if (v.isEVA)
    //{
    //  range = 1000.0; //< 1Km
    //  return;
    //}

    // get error-correcting code factor
    double ecc = Signal.ECC();

    // get ec available
    // note: this is the amount available at previous simulation step
    double ec_amount = ResourceCache.Info(v, "ElectricCharge").amount;

    // if the vessel is loaded
    if (v.loaded)
    {
      // choose the best antenna
      foreach(Antenna a in v.FindPartModulesImplementing<Antenna>())
      {
        // calculate real range
        double real_range = Signal.Range(a.scope, a.penalty, ecc);

        // maintain best range
        range = Math.Max(range, real_range);

        // maintain best relay
        if (a.relay && real_range > relay_range && ec_amount >= a.relay_cost)
        {
          relay_range = real_range;
          relay_cost = a.relay_cost;
        }
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
          // early exit
          if (m.moduleName != "Antenna") continue;

          // get the antenna module prefab
          Part part_prefab = PartLoader.getPartInfoByName(p.partName).partPrefab;
          Antenna a = Lib.FindModuleAs<Antenna>(part_prefab, "Antenna");
          if (!a) continue;

          // calculate real range
          double real_range = Signal.Range(a.scope, Malfunction.Penalty(p, 0.7071), ecc);

          // maintain best range
          range = Math.Max(range, real_range);

          // maintain best relay
          if (Lib.Proto.GetBool(m, "relay") && real_range > relay_range && ec_amount >= a.relay_cost)
          {
            relay_range = real_range;
            relay_cost = a.relay_cost;
          }
        }
      }
    }
  }


  public double range;       // best range of all antennas
  public double relay_range; // best range of all relay antennas
  public double relay_cost;  // the EC/s relay cost of the best relay antenna
}


// types of link status
public enum link_status { no_antenna, no_link, indirect_link, direct_link };


// runtime link data
public class link_data
{
  public link_data(bool linked, link_status status, double distance)
  {
    this.linked = linked;
    this.status = status;
    this.distance = distance;
    this.path = new List<Vessel>();
  }

  public link_data(link_data other)
  {
    this.linked = other.linked;
    this.status = other.status;
    this.distance = other.distance;
    this.path = new List<Vessel>();
    foreach(Vessel v in other.path) this.path.Add(v);
  }


  public bool linked;             // true if linked, false otherwise
  public link_status status;      // kind of link
  public double distance;         // distance to first relay or home planet
  public List<Vessel> path;       // list of relays in reverse order
}


public sealed class Signal
{
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


  // ctor
  public Signal()
  {
    // enable global access
    instance = this;

    // determine nearest and furthest planets from home body
    CelestialBody sun = FlightGlobals.Bodies[0];
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


  public static link_data Link(Vessel v, Vector3d position, antenna_data antenna, bool blackout, HashSet<Guid> avoid_inf_recursion)
  {
    // assume linked if signal mechanic is disabled
    if (!Kerbalism.features.signal) return new link_data(true, link_status.direct_link, double.MaxValue);

    // if it has no antenna
    if (antenna.range <= double.Epsilon) return new link_data(false, link_status.no_antenna, 0.0);

    // if there is a storm and the vessel is inside a magnetosphere
    if (blackout) return new link_data(false, link_status.no_link, 0.0);

    // store raytracing data
    Vector3d dir;
    double dist;
    bool visible;

    // raytrace home body
    visible = Sim.RaytraceBody(v, position, FlightGlobals.GetHomeBody(), out dir, out dist);
    dist = visible && antenna.range > dist ? dist : double.MaxValue;

    // if directly linked
    if (antenna.range > dist) return new link_data(true, link_status.direct_link, dist);

    // for each other vessel
    foreach(Vessel w in FlightGlobals.Vessels)
    {
      // do not test with itself
      if (v == w) continue;

      // skip vessels already in this chain
      if (avoid_inf_recursion.Contains(w.id)) continue;

      // get vessel from the cache
      // note: safe because we are avoiding infinite recursion
      vessel_info wi = Cache.VesselInfo(w);

      // skip invalid vessels
      if (!wi.is_valid) continue;

      // skip non-relays and non-linked relays
      if (wi.antenna.relay_range <= double.Epsilon || !wi.link.linked) continue;

      // raytrace the other vessel
      visible = Sim.RaytraceVessel(v, w, position, wi.position, out dir, out dist);
      dist = visible && antenna.range > dist ? dist : double.MaxValue;

      // if indirectly linked
      // note: relays with no EC have zero relay_range
      // note: avoid relay loops
      if (antenna.range > dist && wi.antenna.relay_range > dist && !wi.link.path.Contains(v))
      {
        // create indirect link data
        link_data link = new link_data(wi.link);

        // update the link data and return it
        link.status = link_status.indirect_link;
        link.distance = dist; //< store distance of last link
        link.path.Add(w);
        return link;
      }
    }

    // no link
    return new link_data(false, link_status.no_link, 0.0);
  }


  public void update(Vessel v, vessel_info vi, vessel_data vd, vessel_resources resources, double elapsed_s)
  {
    // do nothing if signal mechanic is disabled
    if (!Kerbalism.features.signal) return;

    // get link data
    link_data link = vi.link;

    // consume relay ec
    // note: this is the only way to do it with new signal and resource systems
    if (vi.antenna.relay_range > 0.0)
    {
      foreach(Vessel w in FlightGlobals.Vessels)
      {
        vessel_info wi = Cache.VesselInfo(w);
        if (wi.is_valid)
        {
          if (wi.link.path.Contains(v))
          {
            resources.Consume(v, "ElectricCharge", vi.antenna.relay_cost * elapsed_s);
            break;
          }
        }
      }
    }

    // maintain and send messages
    // - do nothing if db isn't ready
    // - do not send messages for vessels without an antenna
    if (link.status != link_status.no_antenna)
    {
      if (vd.msg_signal < 1 && !link.linked)
      {
        vd.msg_signal = 1;
        DB.NotificationData().first_signal_loss = 1; //< record first signal loss
        if (vd.cfg_signal == 1 && !vi.blackout) //< do not send message during storms
        {
          Message.Post(Severity.warning, Lib.BuildString("Signal lost with <b>", v.vesselName, "</b>"),
            vi.crew_count == 0 && Settings.RemoteControlLink ? "Remote control disabled" : "Data transmission disabled");
        }
      }
      else if (vd.msg_signal > 0 && link.linked)
      {
        vd.msg_signal = 0;
        if (vd.cfg_signal == 1 && !Storm.JustEnded(v, elapsed_s)) //< do not send messages after a storm
        {
          var path = link.path;
          Message.Post(Severity.relax, Lib.BuildString("<b>", v.vesselName, "</b> signal is back"),
            path.Count == 0 ? "We got a direct link with the space center" : Lib.BuildString("Relayed by <b>", path[path.Count - 1].vesselName, "</b>"));
        }
      }
    }
  }


  public static void render()
  {
    // get home body position
    Vector3 home = ScaledSpace.LocalToScaledSpace(FlightGlobals.GetHomeBody().position);

    // for each vessel
    foreach(Vessel v in FlightGlobals.Vessels)
    {
      // get info from the cache
      vessel_info vi = Cache.VesselInfo(v);

      // skip invalid vessels
      if (!vi.is_valid) continue;

      // get data from db
      vessel_data vd = DB.VesselData(v.id);

      // skip vessels with showlink disabled
      if (vd.cfg_showlink == 0) continue;

      // get link data
      link_data link = vi.link;

      // skip vessels with no antenna
      if (link.status == link_status.no_antenna) continue;

      // start of the line
      Vector3 a = ScaledSpace.LocalToScaledSpace(v.GetWorldPos3D());

      // determine end of line and color
      Vector3 b;
      Color color;
      if (link.status == link_status.no_link)
      {
        b = home;
        color = Color.red;
      }
      else if (link.status == link_status.direct_link)
      {
        b = home;
        color = Color.green;
      }
      else //< indirect link
      {
        // get link path
        var path = link.path;

        // use relay position
        b = ScaledSpace.LocalToScaledSpace(path[path.Count - 1].GetWorldPos3D());
        color = Color.yellow;
      }

      // commit the line
      LineRenderer.commit(a, b, color);
    }
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


  // store range scope values
  Dictionary<string, double> range_values = new Dictionary<string, double>(32);

  // permit global access
  static Signal instance;
}




} // KERBALISM
