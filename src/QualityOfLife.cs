// ====================================================================================================================
// quality-of-life mechanics
// ====================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {

public static class QualityOfLife
{
  // max entertainment value
  public static double MaxEntertainmnent = 5.0;

  // return quality-of-life bonus
  public static double Bonus(Vessel v, string k_name)
  {
    // get QoL data from db
    kerbal_data kd = DB.KerbalData(k_name);

    // calculate quality of life bonus
    return Bonus(kd.living_space, kd.entertainment, Lib.Landed(v), Signal.Link(v).linked, Lib.CrewCount(v) < 2u);
  }


  // return quality-of-life bonus
  public static double Bonus(double living_space, double entertainment, bool landed, bool linked, bool alone)
  {
    // sanitize input, can happen when loading vessel from old save
    living_space = Math.Max(living_space, 1.0);
    entertainment = Math.Max(entertainment, 1.0);

    // deduce firm ground bonus in [1..1+bonus] range
    double firm_ground = (landed ? Settings.QoL_FirmGround : 0.0) + 1.0;

    // deduce phone home bonus in [1..1+bonus] range
    double phone_home = (linked ? Settings.QoL_PhoneHome : 0.0) + 1.0;

    // deduce not alone bonus in [bonus..bonus*n] range
    double not_alone = (!alone ? Settings.QoL_NotAlone : 0.0) + 1.0;

    // finally, return quality of life bonus
    return living_space * entertainment * firm_ground * phone_home * not_alone;
  }


  // return living space
  public static double LivingSpace(uint crew_count, uint crew_capacity)
  {
    return crew_count == 0 ? 1.0 : ((double)crew_capacity / (double)crew_count);
  }


  // return living space inside a connected space
  public static double LivingSpace(ConnectedLivingSpace.ICLSSpace space)
  {
    return LivingSpace((uint)space.Crew.Count, (uint)space.MaxCrew);
  }


  // traduce living space value to string
  public static string LivingSpaceToString(double living_space)
  {
    if (living_space >= 3.5) return "good";
    else if (living_space >= 2.5) return "modest";
    else if (living_space >= 1.5) return "poor";
    else if (living_space > double.Epsilon) return "cramped";
    else return "none";
  }


  // return entertainment on a vessel
  public static double Entertainment(Vessel v)
  {
    // deduce entertainment bonus, multiplying all entertainment factors
    double entertainment = 1.0;
    if (v.loaded)
    {
      foreach(Entertainment m in v.FindPartModulesImplementing<Entertainment>())
      {
        entertainment *= m.rate;
      }
      foreach(GravityRing m in v.FindPartModulesImplementing<GravityRing>())
      {
        entertainment *= m.rate;
      }
    }
    else
    {
      foreach(ProtoPartSnapshot part in v.protoVessel.protoPartSnapshots)
      {
        foreach(ProtoPartModuleSnapshot m in part.modules)
        {
          if (m.moduleName == "Entertainment") entertainment *= Lib.Proto.GetDouble(m, "rate");
          else if (m.moduleName == "GravityRing") entertainment *= Lib.Proto.GetDouble(m, "rate");
        }
      }
    }
    return Math.Min(entertainment, MaxEntertainmnent);
  }


  public static double Entertainment(ConnectedLivingSpace.ICLSSpace space)
  {
    double entertainment = 1.0;
    foreach(var part in space.Parts)
    {
      foreach(Entertainment m in part.Part.FindModulesImplementing<Entertainment>())
      {
        entertainment *= m.rate;
      }
      foreach(GravityRing m in part.Part.FindModulesImplementing<GravityRing>())
      {
        entertainment *= m.rate;
      }
    }
    return Math.Min(entertainment, MaxEntertainmnent);
  }


  // traduce entertainment value to string
  public static string EntertainmentToString(double entertainment)
  {
    if (entertainment >= 4.5) return "good";
    else if (entertainment >= 2.5) return "tolerable";
    else if (entertainment >= 1.25) return "boring";
    else return "none";
  }
}


} // KERBALISM