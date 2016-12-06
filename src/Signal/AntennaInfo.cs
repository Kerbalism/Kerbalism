using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class AntennaInfo
{
  public AntennaInfo(Vessel v)
  {
    // initialize data
    type = new List<AntennaType>();
    cost = new List<double>();
    rate = new List<double>();
    dist = new List<double>();
    relay = new List<bool>();
    no_antenna = true;

    // get ec available
    // - this is the amount available at previous simulation step
    bool ec_available = ResourceCache.Info(v, "ElectricCharge").amount > double.Epsilon;

    // if the vessel is loaded
    if (v.loaded)
    {
      // get all antennas data
      foreach(Antenna a in Lib.FindModules<Antenna>(v))
      {
        if (!Settings.ExtendedAntenna || a.extended)
        {
          type.Add(a.type);
          cost.Add(a.cost);
          rate.Add(a.rate);
          dist.Add(a.dist);
          relay.Add(ec_available && a.relay);
          is_relay |= ec_available && a.relay;
          direct_cost += a.cost;
          if (a.type == AntennaType.low_gain) indirect_cost += a.cost;
        }
        no_antenna = false;
      }
    }
    // if the vessel isn't loaded
    // - we don't support multiple antenna modules per-part
    else
    {
      // for each part
      foreach(ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
      {
        // get part prefab (required for module properties)
        Part part_prefab = PartLoader.getPartInfoByName(p.partName).partPrefab;

        // get module prefab
        Antenna a = part_prefab.FindModuleImplementing<Antenna>();

        // if there is none, skip the part
        if (a == null) continue;

        // for each module
        foreach(ProtoPartModuleSnapshot m in p.modules)
        {
          // we are only interested in antennas
          if (m.moduleName != "Antenna") continue;

          // if the module is disabled, skip it
          if (!Lib.Proto.GetBool(m, "isEnabled")) continue;

          // get antenna data
          if (!Settings.ExtendedAntenna || Lib.Proto.GetBool(m, "extended"))
          {
            bool antenna_is_relay = Lib.Proto.GetBool(m, "relay");
            type.Add(a.type);
            cost.Add(a.cost);
            rate.Add(a.rate);
            dist.Add(a.dist);
            relay.Add(ec_available && antenna_is_relay);
            is_relay |= ec_available && antenna_is_relay;
            direct_cost += a.cost;
            if (a.type == AntennaType.low_gain) indirect_cost += a.cost;
          }
          no_antenna = false;
        }
      }
    }
  }


  public double direct_rate(double d)
  {
    double r = 0.0;
    for(int i=0; i < type.Count; ++i)
    {
      r += Antenna.calculate_rate(d, dist[i], rate[i]);
    }
    return r;
  }

  public double indirect_rate(double d, AntennaInfo relay_antenna)
  {
    double r = 0.0;
    for(int i=0; i < type.Count; ++i)
    {
      if (type[i] == AntennaType.low_gain)
      {
        r += Antenna.calculate_rate(d, dist[i], rate[i]);
      }
    }

    double indirect_r = 0.0;
    for(int i=0; i < relay_antenna.type.Count; ++i)
    {
      if (relay_antenna.type[i] == AntennaType.low_gain && relay_antenna.relay[i])
      {
        indirect_r += Antenna.calculate_rate(d, relay_antenna.dist[i], relay_antenna.rate[i]);
      }
    }

    return Math.Min(r, indirect_r);
  }

  public double relay_rate(double d)
  {
    double r = 0.0;
    for(int i=0; i < type.Count; ++i)
    {
      if (type[i] == AntennaType.low_gain && relay[i])
      {
        r += Antenna.calculate_rate(d, dist[i], rate[i]);
      }
    }
    return r;
  }


  List<AntennaType> type;
  List<double> cost;
  List<double> rate;
  List<double> dist;
  List<bool> relay;

  public bool no_antenna;
  public bool is_relay;
  public double direct_cost;
  public double indirect_cost;
}


} // KERBALISM

