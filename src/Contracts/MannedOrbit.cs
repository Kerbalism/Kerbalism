using System;
using Contracts;


namespace KERBALISM.CONTRACTS {


// put a kerbal in orbit for 30 days
public sealed class MannedOrbit : Contract
{
  protected override bool Generate()
  {
    // never expire
    deadlineType = DeadlineType.None;
    expiryType = DeadlineType.None;

    // set reward
    SetScience(25.0f);
    SetReputation(30.0f, 10.0f);
    SetFunds(25000.0f, 100000.0f);

    // add parameters
    AddParameter(new MannedOrbitCondition());
    return true;
  }

  protected override string GetHashString()
  {
    return "MannedOrbit";
  }

  protected override string GetTitle()
  {
    return "Put a Kerbal in orbit for 30 days";
  }

  protected override string GetDescription()
  {
    return "Obtaining an orbit was easier than we expected. "
         + "Now it is time to keep a Kerbal alive in orbit for 30 days.";
  }

  protected override string MessageCompleted()
  {
    return "The mission was a success, albeit the Kerbal is a bit bored. "
         + "We have plenty of data about long-term permanence in space";
  }

  public override bool MeetRequirements()
  {
    // stop checking when requirements are met
    if (!meet_requirements)
    {
      ProgressTracking progress = ProgressTracking.Instance;

      meet_requirements =
        Profile.rules.Count > 0                                             // some rule is specified
        && progress != null && progress.celestialBodyHome.orbit.IsComplete  // first orbit completed
        && !DB.landmarks.manned_orbit;                                      // contract never completed
    }
    return meet_requirements;
  }

  bool meet_requirements;
}


public sealed class MannedOrbitCondition : ContractParameter
{
  protected override string GetHashString()
  {
    return "MannedOrbitCondition";
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
      bool manned = vi.crew_count > 0;
      bool in_orbit = Sim.Apoapsis(v) > v.mainBody.atmosphereDepth && Sim.Periapsis(v) > v.mainBody.atmosphereDepth;
      bool for_30days = v.missionTime > 60.0 * 60.0 * Lib.HoursInDay() * 30.0;
      if (manned && in_orbit && for_30days)
      {
        SetComplete();
        DB.landmarks.manned_orbit = true; //< remember that contract was completed
        break;
      }
    }
  }
}


} // KERBALISM.CONTRACTS



