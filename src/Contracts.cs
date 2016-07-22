// ===================================================================================================================
// some landmark contracts related to Kerbalism mechanics
// ===================================================================================================================



using System;
using System.Collections.Generic;
using Contracts;
using UnityEngine;


namespace KERBALISM {


// Kerbal in orbit for 30 days - contract
public sealed class KerbalismMannedOrbit : Contract
{
  protected override bool Generate()
  {
    // never expire
    deadlineType = DeadlineType.None;
    expiryType = DeadlineType.None;

    // set reward
    base.SetScience(25.0f);
    base.SetReputation(30.0f, 10.0f);
    base.SetFunds(25000.0f, 100000.0f);

    // add parameters
    base.AddParameter(new KerbalismMannedOrbitCondition());
    return true;
  }

  protected override string GetHashString()
  {
    return "KerbalismMannedOrbit";
  }

  protected override string GetTitle()
  {
    return "Put a Kerbal in orbit for 30 days";
  }

  protected override string GetDescription()
  {
    return "Obtaining an orbit was easier than we expected. Now our eggheads want us to keep a Kerbal alive in orbit for 30 days.";
  }

  protected override string MessageCompleted()
  {
    return "The mission was a success, albeit the Kerbal is a bit bored. Now we have plenty of data about long-term permanence in space";
  }

  public override bool MeetRequirements()
  {
    // stop checking when requirements are met
    if (!meet_requirements)
    {
      meet_requirements =
        (Kerbalism.ec_rule != null || Kerbalism.supply_rules.Count > 0) // some resource-related life support rule is present
        && ProgressTracking.Instance != null && ProgressTracking.Instance.celestialBodyHome.orbit.IsComplete // first orbit completed
        && DB.Ready() && DB.NotificationData().manned_orbit_contract == 0; // contract never completed
    }
    return meet_requirements;
  }

  bool meet_requirements;
}


// Kerbal in orbit for 30 days - condition
public sealed class KerbalismMannedOrbitCondition : Contracts.ContractParameter
{
  protected override string GetHashString()
  {
    return "KerbalismMannedOrbitCondition";
  }

  protected override string GetTitle()
  {
    return "Put a Kerbal in orbit for 30 days";
  }

  protected override void OnUpdate()
  {
    foreach(Vessel v in FlightGlobals.Vessels)
    {
      vessel_info vi = Cache.VesselInfo(v);
      if (!vi.is_valid) continue;
      bool manned = v.loaded ? v.GetCrewCount() > 0 : v.protoVessel.GetVesselCrew().Count > 0;
      bool in_orbit = Sim.Apoapsis(v) > v.mainBody.atmosphereDepth && Sim.Periapsis(v) > v.mainBody.atmosphereDepth;
      bool for_30days = v.missionTime > 60.0 * 60.0 * Lib.HoursInDay() * 30.0;
      if (manned && in_orbit && for_30days && DB.Ready())
      {
        base.SetComplete();
        DB.NotificationData().manned_orbit_contract = 1; //< remember that contract was completed
        break;
      }
    }
  }
}


// Cross radiation belt - contract
public sealed class KerbalismCrossBelt : Contract
{
  protected override bool Generate()
  {
    // never expire
    deadlineType = DeadlineType.None;
    expiryType = DeadlineType.None;

    // set reward
    base.SetScience(10.0f);
    base.SetReputation(10.0f, 5.0f);
    base.SetFunds(5000.0f, 25000.0f);

    // add parameters
    base.AddParameter(new KerbalismCrossBeltCondition());
    return true;
  }

  protected override string GetHashString()
  {
    return "KerbalismCrossBelt";
  }

  protected override string GetTitle()
  {
    return "Cross the radiation belt";
  }

  protected override string GetDescription()
  {
    return "A brilliant scientist predicted a belt of super-charged particles surrounding the planet. "
      + "Now we need to confirm its existance and find out how deadly it is.";
  }

  protected override string MessageCompleted()
  {
    return "The mission confirmed the presence of a radiation belt around the planet. Early data suggest extreme levels of radiation: we should be careful in crossing it.";
  }

  public override bool MeetRequirements()
  {
    // stop checking when requirements are met
    if (!meet_requirements)
    {
      meet_requirements =
        Kerbalism.rad_rule != null // a radiation rule is present
        && ProgressTracking.Instance != null && ProgressTracking.Instance.reachSpace.IsComplete // first suborbit flight completed
        && DB.Ready() && DB.NotificationData().first_belt_crossing == 0; // belt never crossed before
    }
    return meet_requirements;
  }

  bool meet_requirements;
}


// Cross radiation belt - condition
public sealed class KerbalismCrossBeltCondition : Contracts.ContractParameter
{
  protected override string GetHashString()
  {
    return "KerbalismCrossBeltCondition";
  }

  protected override string GetTitle()
  {
    return "Cross the radiation belt";
  }

  protected override void OnUpdate()
  {
    if (DB.Ready() && DB.NotificationData().first_belt_crossing == 1) base.SetComplete();
  }
}


// First space harvest - contract
public sealed class KerbalismSpaceHarvest : Contract
{
  protected override bool Generate()
  {
    // never expire
    deadlineType = DeadlineType.None;
    expiryType = DeadlineType.None;

    // set reward
    base.SetScience(25.0f);
    base.SetReputation(30.0f, 10.0f);
    base.SetFunds(25000.0f, 100000.0f);

    // add parameters
    base.AddParameter(new KerbalismSpaceHarvestCondition());
    return true;
  }

  protected override string GetHashString()
  {
    return "KerbalismSpaceHarvest";
  }

  protected override string GetTitle()
  {
    return "Harvest food in space";
  }

  protected override string GetDescription()
  {
    return "Now that we got the technology to grow food in space, we should probably test it. Harvest food from a greenhouse in space.";
  }

  protected override string MessageCompleted()
  {
    return "We harvested food in space, and our scientists say it is actually delicious.";
  }

  public override bool MeetRequirements()
  {
    // stop checking when requirements are met
    if (!meet_requirements)
    {
      meet_requirements =
        PartLoader.getPartInfoByName("Greenhouse") != null // greenhouse part is present
        && Lib.CountTechs(new []{"scienceTech"}) > 0 // greenhouse unlocked
        && DB.Ready() && DB.NotificationData().first_space_harvest == 0; // greenhouse never harvested in space before
    }
    return meet_requirements;
  }

  bool meet_requirements;
}


// First space harvest - condition
public sealed class KerbalismSpaceHarvestCondition : Contracts.ContractParameter
{
  protected override string GetHashString()
  {
    return "KerbalismSpaceHarvestCondition";
  }

  protected override string GetTitle()
  {
    return "Harvest food in space";
  }

  protected override void OnUpdate()
  {
    if (DB.Ready() && DB.NotificationData().first_space_harvest == 1) base.SetComplete();
  }
}


} // KERBALISM