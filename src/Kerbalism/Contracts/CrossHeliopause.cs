using System;
using System.Collections.Generic;
using Contracts;
using KSP.Localization;

namespace KERBALISM.CONTRACTS
{


	// Cross the heliopause
	public sealed class CrossHeliopause : Contract
	{
		protected override bool Generate()
		{
			// never expire
			deadlineType = DeadlineType.None;
			expiryType = DeadlineType.None;

			// set reward
			SetScience(100.0f);
			SetReputation(100.0f, 50.0f);
			SetFunds(100000.0f, 500000.0f);

			// add parameters
			AddParameter(new CrossHeliopauseCondition());
			return true;
		}

		protected override string GetHashString()
		{
			return "CrossHeliopause";
		}

		protected override string GetTitle()
		{
			return Local.Contracts_heliopauseTitle;
		}

		protected override string GetDescription()
		{
			return Local.Contracts_heliopauseDesc;
		}

		protected override string MessageCompleted()
		{
			return Local.Contracts_heliopauseComplete;
		}

		public override bool MeetRequirements()
		{
			// stop checking when requirements are met
			if (!meet_requirements)
			{
				ProgressTracking progress = ProgressTracking.Instance;
				if (progress == null) return false;
				int known = 0;
				foreach (var body_progress in progress.celestialBodyNodes)
				{ known += body_progress.flyBy != null && body_progress.flyBy.IsComplete ? 1 : 0; }
				bool end_game = known > FlightGlobals.Bodies.Count / 2;

				meet_requirements =
				  Features.Radiation                                          // radiation is enabled
				  && end_game                                                 // entered SOI of half the bodies
				  && Radiation.Info(FlightGlobals.Bodies[0]).model.has_pause  // there is an actual heliopause
				  && !DB.Landmarks.heliopause_crossing;                       // heliopause never crossed before
			}
			return meet_requirements;
		}

		bool meet_requirements;
	}


	// Cross radiation belt - condition
	public sealed class CrossHeliopauseCondition : ContractParameter
	{
		protected override string GetHashString()
		{
			return "CrossHeliopauseCondition";
		}

		protected override string GetTitle()
		{
			return Local.Contracts_heliopauseTitle;
		}

		protected override void OnUpdate()
		{
			if (DB.Landmarks.heliopause_crossing) SetComplete();
		}
	}


} // KERBALISM



