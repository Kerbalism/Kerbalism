using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KERBALISM
{



	public class SubjectData
	{
		public ExperimentInfo ExpInfo { get; protected set; }

		public VesselSituation Situation { get; protected set; }

		/// <summary> [SERIALIZED] percentage [0;x] of science retrieved, can be > 1 if subject has been retrieved more than once</summary>
		public virtual double PercentRetrieved { get; protected set; }

		/// <summary> how many times the subject has been fully retrieved in RnD </summary>
		public virtual int TimesCompleted { get; protected set; }

		public bool ExistsInRnD { get; protected set; }

		public ScienceSubject RnDSubject { get; protected set; }

		/// <summary> our int-based identifier </summary>
		public virtual string Id { get; protected set; }

		/// <summary> stock subject identifier ("experimentId@situation") </summary>
		public virtual string StockSubjectId => Lib.BuildString(ExpInfo.ExperimentId, "@", Situation.ExperimentSituationId(ExpInfo));

		/// <summary> full description of the subject </summary>
		public virtual string FullTitle => Lib.BuildString(ExpInfo.Title, " (", SituationTitle, ")");

		public virtual string ExperimentTitle => ExpInfo.Title;

		public virtual string SituationTitle => Situation.ExperimentSituationName(ExpInfo);

		/// <summary> science points collected in all vessels but not yet recovered or transmitted </summary>
		public double ScienceCollectedInFlight { get; protected set; }

		/// <summary> total science value of the subject.  </summary>
		public double ScienceMaxValue => ExpInfo.ScienceCap * Situation.SituationMultiplier;

		public double SciencePerMB => ScienceMaxValue / ExpInfo.DataSize;

		/// <summary> science points recovered or transmitted </summary>
		public double ScienceRetrievedInKSC => ExistsInRnD ? RnDSubject.science * HighLogic.CurrentGame.Parameters.Career.ScienceGainMultiplier : 0.0;

		/// <summary> all science points recovered, transmitted or collected in flight </summary>
		public double ScienceCollectedTotal => ScienceCollectedInFlight + ScienceRetrievedInKSC;

		/// <summary> science value remaining to collect. </summary>
		public double ScienceRemainingToCollect => Math.Max(ScienceMaxValue - ScienceCollectedTotal, 0f);

		/// <summary> science value remaining to retriecve. </summary>
		public double ScienceRemainingToRetrieve => ScienceMaxValue - ScienceRetrievedInKSC;

		/// <summary> percentage [0;1] of science collected. </summary>
		public double PercentCollectedTotal => (ScienceCollectedInFlight / ScienceMaxValue) + PercentRetrieved;

		/// <summary> science value for the given data size </summary>
		public double ScienceValue(double dataSize, bool clampByScienceRetrieved = false, bool clampByScienceRetrievedAndCollected = false)
		{
			if (clampByScienceRetrievedAndCollected)
				return Math.Min(dataSize * SciencePerMB, ScienceRemainingToCollect);
			if (clampByScienceRetrieved)
				return Math.Min(dataSize * SciencePerMB, ScienceRemainingToRetrieve);

			return dataSize * SciencePerMB;
		}

		public SubjectData(ExperimentInfo expInfo, VesselSituation situation)
		{
			ExpInfo = expInfo;
			Situation = situation;
			Id = Lib.BuildString(ExpInfo.ExperimentId, "@", Situation.Id.ToString());
		}

		public void CheckRnD()
		{
			RnDSubject = ResearchAndDevelopment.GetSubjectByID(StockSubjectId);
			if (RnDSubject == null)
			{
				PercentRetrieved = 0.0;
				TimesCompleted = 0;
				ExistsInRnD = false;
			}
			else
			{
				PercentRetrieved = RnDSubject.science / RnDSubject.scienceCap;
				TimesCompleted = GetTimesCompleted(PercentRetrieved);
				ExistsInRnD = true;
				ScienceDB.persistedSubjects.Add(this);
			}
		}

		public void Load(ConfigNode node)
		{
			PercentRetrieved = Lib.ConfigValue(node, "percentRetrieved", 0.0);
			TimesCompleted = GetTimesCompleted(PercentRetrieved);
		}

		public void Save(ConfigNode node)
		{
			node.AddValue("percentRetrieved", PercentRetrieved);
		}

		private int GetTimesCompleted(double percentRetrieved)
		{
			double decimalPart = percentRetrieved - Math.Truncate(percentRetrieved);
			return (int)(percentRetrieved / 1.0) + (decimalPart < 1.0 - Science.scienceLeftForSubjectCompleted ? 0 : 1);
		}

		public void CreateSubjectInRnD()
		{
			if (ExistsInRnD || ResearchAndDevelopment.Instance == null)
				return;

			// try to get it, might be already created in some corner-case situation
			RnDSubject = ResearchAndDevelopment.GetSubjectByID(StockSubjectId);

			if (RnDSubject == null)
			{
				// get subjects container using reflection
				Dictionary<string, ScienceSubject> subjects = Lib.ReflectionValue<Dictionary<string, ScienceSubject>>
				(
					ResearchAndDevelopment.Instance,
					"scienceSubjects"
				);

				// create new subject
				RnDSubject = new ScienceSubject
				(
						  StockSubjectId,
						FullTitle,
						(float)ExpInfo.DataScale,
						  Situation.SituationMultiplier,
						(float)ScienceMaxValue
				);

				// add it to RnD
				subjects.Add(StockSubjectId, RnDSubject);
			}

			ExistsInRnD = true;
			SetAsPersistent();
		}

		/// <summary> add data to the in-flight collected science </summary>
		public void AddDataCollectedInFlight(double dataAmount)
		{
			ScienceCollectedInFlight += dataAmount * SciencePerMB;
		}

		/// <summary> remove data from the in-flight collected science </summary>
		public void RemoveDataCollectedInFlight(double dataAmount)
		{
			ScienceCollectedInFlight -= dataAmount * SciencePerMB;
			if (ScienceCollectedInFlight < 0.0) ScienceCollectedInFlight = 0.0;
		}

		/// <summary> remove science points from the in-flight collected science </summary>
		public void RemoveScienceCollectedInFlight(double credits)
		{
			ScienceCollectedInFlight -= credits;
			if (ScienceCollectedInFlight < 0.0) ScienceCollectedInFlight = 0.0;
		}

		public void ClearDataCollectedInFlight() => ScienceCollectedInFlight = 0.0;

		/// <summary>
		/// update our subject completion database.
		/// if the subject was just completed, return the amount of times it has ever been completed.
		/// otherwise return -1
		/// </summary>
		public int UpdateSubjectCompletion(double scienceAdded)
		{
			if (!Science.GameHasRnD) return -1;

			PercentRetrieved = ((PercentRetrieved * ScienceMaxValue) + scienceAdded) / ScienceMaxValue;
			int newTimesCompleted = GetTimesCompleted(PercentRetrieved);
			if (newTimesCompleted > TimesCompleted)
			{
				TimesCompleted = newTimesCompleted;
				return TimesCompleted;
			}
			return -1;
		}

		public void SetAsPersistent()
		{
			ScienceDB.persistedSubjects.Add(this);
		}

		public void AddScienceToRnDSubject(double scienceValue)
		{
			if (!Science.GameHasRnD)
				return;

			if (!ExistsInRnD)
				CreateSubjectInRnD();

			RnDSubject.science += (float)scienceValue;
			RnDSubject.scientificValue = ResearchAndDevelopment.GetSubjectValue(RnDSubject.science, RnDSubject);
		}
	}

	/// <summary>
	/// this is meant to handle subjects created by the stock system with the
	/// ResearchAndDevelopment.GetExperimentSubject overload that take a "sourceUId" string.
	/// In stock, it is only used by the asteroid samples, and I don't think there is any mod using that.
	/// </summary>
	public class MultiSubjectData : SubjectData
	{
		private string extraSituationInfo;
		private string subjectId;

		public MultiSubjectData(ExperimentInfo expInfo, VesselSituation situation, string subjectId, ScienceSubject stockSubject = null, string extraSituationInfo = "") : base(expInfo, situation)
		{
			this.subjectId = subjectId;
			this.extraSituationInfo = extraSituationInfo;
			ExpInfo = expInfo;
			Situation = situation;
			ExistsInRnD = stockSubject != null;
			RnDSubject = stockSubject;
			ScienceCollectedInFlight = 0.0;
		}

		public override string Id => subjectId;

		public override string StockSubjectId => subjectId;

		public override string FullTitle =>
			ExistsInRnD
			? RnDSubject.title
			: Lib.BuildString(ExpInfo.Title, " (", SituationTitle, ")");

		public override string SituationTitle =>
			string.IsNullOrEmpty(extraSituationInfo)
			? base.SituationTitle
			: Lib.BuildString(base.SituationTitle, " from ", extraSituationInfo);

		public override double PercentRetrieved
		{
			get
			{
				return ExistsInRnD ? (RnDSubject.science * HighLogic.CurrentGame.Parameters.Career.ScienceGainMultiplier) / ScienceMaxValue : 0.0;
			}
			protected set { }
		}
		public override int TimesCompleted
		{
			get
			{
				return ExistsInRnD ? (int)(RnDSubject.science / (RnDSubject.scienceCap - Science.scienceLeftForSubjectCompleted)) : 0;
			}
			protected set { }
		}
	}
}
