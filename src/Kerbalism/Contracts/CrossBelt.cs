using System;
using Contracts;
using KSP.Localization;


namespace KERBALISM.CONTRACTS
{


	// Cross radiation belt
	public sealed class CrossBelt : Contract
	{
		protected override bool Generate()
		{
			// never expire
			deadlineType = DeadlineType.None;
			expiryType = DeadlineType.None;

			// set reward
			SetScience(10.0f);
			SetReputation(10.0f, 5.0f);
			SetFunds(5000.0f, 25000.0f);

			// add parameters
			AddParameter(new CrossBeltCondition());
			return true;
		}

		protected override string GetHashString()
		{
			return "CrossBelt";
		}

		protected override string GetTitle()
		{
			return Local.Contracts_radTitle;
		}

		protected override string GetDescription()
		{
			return Local.Contracts_radDesc;
		}

		protected override string MessageCompleted()
		{
			return Local.Contracts_radComplete;
		}

		public override bool MeetRequirements()
		{
			// stop checking when requirements are met
			if (!meet_requirements)
			{
				ProgressTracking progress = ProgressTracking.Instance;

				meet_requirements =
				  Features.Radiation                                      // radiation is enabled
				  && progress != null && progress.reachSpace.IsComplete   // first suborbit flight completed
				  && !DB.landmarks.belt_crossing;                         // belt never crossed before
			}
			return meet_requirements;
		}

		bool meet_requirements;
	}


	public sealed class CrossBeltCondition : ContractParameter
	{
		protected override string GetHashString()
		{
			return "CrossBeltCondition";
		}

		protected override string GetTitle()
		{
			return Local.Contracts_radTitle;
		}

		protected override void OnUpdate()
		{
			if (DB.landmarks.belt_crossing) SetComplete();
		}
	}


} // KERBALISM.CONTRACTS

