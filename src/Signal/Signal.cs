using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public static class Signal
{
  public static ConnectionInfo connection(Vessel v, Vector3d position, AntennaInfo antenna, bool blackout, HashSet<Guid> avoid_inf_recursion)
  {
    // if signal mechanic is disabled, use RemoteTech/CommNet/S4
    if (!Features.Signal) return OtherComms(v);

    // if it has no antenna
    if (antenna.no_antenna) return new ConnectionInfo(LinkStatus.no_antenna);

    // if there is a storm and the vessel is inside a magnetosphere
    if (blackout) return new ConnectionInfo(LinkStatus.blackout);

    // store raytracing data
    Vector3d dir;
    double dist;
    bool visible;

    // store other data
    double rate;
    List<ConnectionInfo> connections = new List<ConnectionInfo>();

    // raytrace home body
    visible = Sim.RaytraceBody(v, position, FlightGlobals.GetHomeBody(), out dir, out dist);

    // get rate
    rate = antenna.direct_rate(dist);

    // if directly linked
    if (visible && rate > 0.0)
    {
      ConnectionInfo conn = new ConnectionInfo(LinkStatus.direct_link, rate, antenna.direct_cost);
      connections.Add(conn);
    }

    // for each other vessel
    foreach(Vessel w in FlightGlobals.Vessels)
    {
      // do not test with itself
      if (v == w) continue;

      // skip vessels already in this chain
      if (avoid_inf_recursion.Contains(w.id)) continue;

      // get vessel from the cache
      // - when:
      //   . cache is empty (eg: new savegame loaded)
      // - we avoid single-tick wrong paths arising from this situation:
      //   . vessel A is directly linked
      //   . vessel B is indirectly linked through A
      //   . cache is cleared (after loading a savegame)
      //   . cache of A is computed
      //   . in turn, cache of B is computed ignoring A (and stored)
      //   . until cache of B is re-computed, B will result incorrectly not linked
      // - in this way:
      //   . cache of A is computed
      //   . in turn, cache of B is computed ignoring A (but not stored)
      //   . cache of B is then computed correctly
      //   . do not degenerate into O(N^3) by using non-optimal path
      vessel_info wi;
      if (!Cache.HasVesselInfo(w, out wi))
      {
        if (connections.Count > 0) continue;
        else wi = new vessel_info(w, Lib.VesselID(w), 0);
      }

      // skip invalid vessels
      if (!wi.is_valid) continue;

      // skip non-relays and non-linked relays
      if (!wi.antenna.is_relay || !wi.connection.linked) continue;

      // raytrace the other vessel
      visible = Sim.RaytraceVessel(v, w, position, Lib.VesselPosition(w), out dir, out dist);

      // get rate
      rate = antenna.indirect_rate(dist, wi.antenna);

      // if indirectly linked
      // - relays with no EC have zero relay_range
      // - avoid relay loops
      if (visible && rate > 0.0 && !wi.connection.path.Contains(v))
      {
        // create indirect link data
        ConnectionInfo conn = new ConnectionInfo(wi.connection);

        // update the link data and return it
        conn.status = LinkStatus.indirect_link;
        conn.rate = Math.Min(conn.rate, rate);
        conn.cost = antenna.indirect_cost;
        conn.path.Add(w);
        connections.Add(conn);
      }
    }

    // if at least a connection has been found
    if (connections.Count > 0)
    {
      // select the best connection
      double best_rate = 0.0;
      int best_index = 0;
      for(int i=0; i < connections.Count; ++i)
      {
        if (connections[i].rate > best_rate)
        {
          best_rate = connections[i].rate;
          best_index = i;
        }
      }

      // and return it
      return connections[best_index];
    }

    // no link
    return new ConnectionInfo(LinkStatus.no_link);
  }

  // return name of file being relayed, in case multiple ones are relayed the first one is returned
  public static string relaying(Vessel v, HashSet<Guid> avoid_inf_recursion)
  {
    foreach(Vessel w in FlightGlobals.Vessels)
    {
      // do not test with itself
      if (v == w) continue;

      // skip vessels already in this chain
      if (avoid_inf_recursion.Contains(w.id)) continue;

      // get vessel info from cache
      // - if vessel is not already in cache, just ignore it
      vessel_info wi;
      if (!Cache.HasVesselInfo(w, out wi)) continue;

      // skip invalid vessels
      if (!wi.is_valid) continue;

      // the vessel is relaying if at least another one that is using it as relay is transmitting data
      if (wi.transmitting.Length > 0 && wi.connection.path.Contains(v)) return wi.transmitting;
    }
    return string.Empty;
  }


  public static void update(Vessel v, vessel_info vi, VesselData vd, double elapsed_s)
  {
    // do nothing if signal mechanic is disabled
    if (!Features.Signal) return;

    // get connection info
    ConnectionInfo conn = vi.connection;

    // maintain and send messages
    // - do not send messages for vessels without an antenna
    // - do not send messages during/after solar storms
    // - do not send messages for EVA kerbals
    if (conn.status != LinkStatus.no_antenna && !v.isEVA && v.situation != Vessel.Situations.PRELAUNCH)
    {
      if (!vd.msg_signal && !conn.linked)
      {
        vd.msg_signal = true;
        if (vd.cfg_signal && conn.status != LinkStatus.blackout)
        {
          string subtext = "Data transmission disabled";
          if (vi.crew_count == 0)
          {
            switch(Settings.UnlinkedControl)
            {
              case UnlinkedCtrl.none: subtext = "Remote control disabled"; break;
              case UnlinkedCtrl.limited: subtext = "Limited control available"; break;
            }
          }
          Message.Post(Severity.warning, Lib.BuildString("Signal lost with <b>", v.vesselName, "</b>"), subtext);
        }
      }
      else if (vd.msg_signal && conn.linked)
      {
        vd.msg_signal = false;
        if (vd.cfg_signal && !Storm.JustEnded(v, elapsed_s))
        {
          var path = conn.path;
          Message.Post(Severity.relax, Lib.BuildString("<b>", v.vesselName, "</b> signal is back"),
            path.Count == 0 ? "We got a direct link with the space center" : Lib.BuildString("Relayed by <b>", path[path.Count - 1].vesselName, "</b>"));
        }
      }
    }
  }


  public static void render()
  {
    // do nothing if signal mechanic is disabled
    if (!Features.Signal) return;

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
      VesselData vd = DB.Vessel(v);

      // skip vessels with showlink disabled
      if (!vd.cfg_showlink) continue;

      // get connection info
      ConnectionInfo conn = vi.connection;

      // skip unlinked vessels
      // - we don't show the red line anymore
      if (!conn.linked) continue;

      // start of the line
      Vector3 a = ScaledSpace.LocalToScaledSpace(v.GetWorldPos3D());

      // determine end of line and color
      Vector3 b;
      Color color;
      if (conn.status == LinkStatus.direct_link)
      {
        b = home;
        color = Color.green;
      }
      else //< indirect link
      {
        // get link path
        var path = conn.path;

        // use relay position
        b = ScaledSpace.LocalToScaledSpace(path[path.Count - 1].GetWorldPos3D());
        color = Color.yellow;
      }

      // commit the line
      LineRenderer.commit(a, b, color);

      // if transmitting or relaying science data
      if (vi.transmitting.Length > 0 || vi.relaying.Length > 0)
      {
        // deduce number of particles and distance between them
        Vector3 dir = b - a;
        float len = dir.magnitude;
        int particle_count = Lib.Clamp((int)(len / 80.0f), 1, 256);
        dir /= (float)particle_count;

        // used for 'moving' effect
        float k = Time.realtimeSinceStartup / 3.0f;
        k -= Mathf.Floor(k);

        // particle color
        // - fade to avoid overlapping
        Color clr = Color.cyan;
        clr.a = Mathf.Min(Lib.Clamp(1.0f - 0.01f * PlanetariumCamera.fetch.Distance / dir.magnitude, 0.0f, 1.0f) * 2.0f, 1.0f);

        // for each particle
        for(int i=0; i < particle_count; ++i)
        {
          // commit particle
          ParticleRenderer.commit(a + dir * ((float)i + k), 8.0f, clr);
        }
      }
    }
  }


  static ConnectionInfo OtherComms(Vessel v)
  {
    // hard-coded transmission rate and cost
    const double ext_rate = 0.064;
    const double ext_cost = 0.1;

    // if RemoteTech is present and enabled
    if (RemoteTech.Enabled())
    {
      return RemoteTech.Connected(v.id)
        ? new ConnectionInfo(LinkStatus.direct_link, ext_rate, ext_cost)
        : new ConnectionInfo(LinkStatus.no_link);
    }
    // if CommNet is enabled
    else if (HighLogic.fetch.currentGame.Parameters.Difficulty.EnableCommNet)
    {
      return v.connection != null && v.connection.IsConnected
        ? new ConnectionInfo(LinkStatus.direct_link, ext_rate * v.connection.SignalStrength, ext_cost)
        : new ConnectionInfo(LinkStatus.no_link);
    }
    // the simple stupid signal system
    else
    {
      return new ConnectionInfo(LinkStatus.direct_link, ext_rate, ext_cost);
    }
  }
}


} // KERBALISM

