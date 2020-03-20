using System;
using Contracts;
using KSP.Localization;


namespace KERBALISM.CONTRACTS
{


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
			return Local.Contracts_sampleTitle;
		}

		protected override string GetDescription()
		{
			return Local.Contracts_sampleDesc;
		}

		protected override string MessageCompleted()
		{
			return Local.Contracts_sampleComplete;
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
				  && !DB.Landmarks.space_analysis;                                // never analyzed samples in space before
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
			return Local.Contracts_sampleTitle;
		}

		protected override void OnUpdate()
		{
			if (DB.Landmarks.space_analysis) SetComplete();
		}
	}


} // KERBALISM



