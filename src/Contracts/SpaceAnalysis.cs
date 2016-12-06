using System;
using Contracts;


namespace KERBALISM.CONTRACTS {


// First sample analysis
public sealed class SpaceAnalysis : Contract
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
    AddParameter(new SpaceAnalysisCondition());
    return true;
  }

  protected override string GetHashString()
  {
    return "SpaceAnalysis";
  }

  protected override string GetTitle()
  {
    return "Analyze samples in space";
  }

  protected override string GetDescription()
  {
    return "The Laboratory can analyze samples in space, in theory. "
         + "We should check if this actually work by and analyzing some samples in space.";
  }

  protected override string MessageCompleted()
  {
    return "Our Laboratory analysis was good, perhaps even better than the "
         + "ones done usually by our scientists at mission control. But don't tell'em.";
  }

  public override bool MeetRequirements()
  {
    // stop checking when requirements are met
    if (!meet_requirements)
    {
      var lab = PartLoader.getPartInfoByName("Large_Crewed_Lab");

      meet_requirements =
           Features.Science                                             // science is enabled
        && lab != null                                                  // lab part is present
        && ResearchAndDevelopment.PartTechAvailable(lab)                // lab part is unlocked
        && !DB.landmarks.space_analysis;                                // never analyzed samples in space before
    }
    return meet_requirements;
  }

  bool meet_requirements;
}


public sealed class SpaceAnalysisCondition : ContractParameter
{
  protected override string GetHashString()
  {
    return "SpaceAnalysisCondition";
  }

  protected override string GetTitle()
  {
    return "Analyze samples in space";
  }

  protected override void OnUpdate()
  {
    if (DB.landmarks.space_analysis) SetComplete();
  }
}


} // KERBALISM



