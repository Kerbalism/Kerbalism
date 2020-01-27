using KSP.Localization;
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

		public Situation Situation { get; protected set; }

		/// <summary> [SERIALIZED] percentage [0;x] of science retrieved, can be > 1 if subject has been retrieved more than once</summary>
		public virtual double PercentRetrieved { get; protected set; }

		/// <summary> how many times the subject has been fully retrieved in RnD </summary>
		public virtual int TimesCompleted { get; protected set; }

		public bool ExistsInRnD => RnDSubject != null;

		public ScienceSubject RnDSubject { get; protected set; }

		/// <summary> our int-based identifier </summary>
		public virtual string Id { get; protected set; }

		/// <summary> stock subject identifier ("experimentId@situation") </summary>
		public virtual string StockSubjectId => Lib.BuildString(ExpInfo.ExperimentId, "@", Situation.GetStockIdForExperiment(ExpInfo));

		/// <summary> full description of the subject </summary>
		public virtual string FullTitle => Lib.BuildString(ExpInfo.Title, " (", SituationTitle, ")");

		public virtual string ExperimentTitle => ExpInfo.Title;

		public virtual string SituationTitle => Situation.GetTitleForExperiment(ExpInfo);

		public virtual string BiomeTitle => Situation.BiomeTitle;

		/// <summary> science points collected in all vessels but not yet recovered or transmitted </summary>
		public double ScienceCollectedInFlight { get; protected set; }

		/// <summary> total science value of the subject.  </summary>
		public double ScienceMaxValue => ExpInfo.ScienceCap * Situation.SituationMultiplier; 

		public double SciencePerMB => ScienceMaxValue / ExpInfo.DataSize;

		/// <summary> science points recovered or transmitted </summary>
		// Note : this code is a bit convoluted to avoid "never completed" issues due to float <> double conversions
		public double ScienceRetrievedInKSC => ExistsInRnD ? (RnDSubject.scienceCap - RnDSubject.science <= 0f) ? ScienceMaxValue : RnDSubject.science : 0.0;

		/// <summary> all science points recovered, transmitted or collected in flight </summary>
		public double ScienceCollectedTotal => ScienceCollectedInFlight + ScienceRetrievedInKSC;

		/// <summary> science value remaining to collect. </summary>
		public double ScienceRemainingToCollect => Math.Max(ScienceMaxValue - ScienceCollectedTotal, 0.0);

		/// <summary> science value remaining to retrieve. </summary>
		public double ScienceRemainingToRetrieve => Math.Max(ScienceMaxValue - ScienceRetrievedInKSC, 0.0);

		/// <summary> science value remaining (accounting for retrieved in KSC and collected in flight) </summary>
		public double ScienceRemainingTotal => Math.Max(ScienceMaxValue - ScienceCollectedTotal, 0.0);

		/// <summary> percentage [0;1] of science collected. </summary>
		public double PercentCollectedTotal => ScienceMaxValue == 0.0 ? 0.0 : (ScienceCollectedInFlight / ScienceMaxValue) + PercentRetrieved;

		/// <summary> science value for the given data size </summary>
		public double ScienceValue(double dataSize, bool clampByScienceRetrieved = false, bool clampByScienceRetrievedAndCollected = false)
		{
			if (clampByScienceRetrievedAndCollected)
				return Math.Min(dataSize * SciencePerMB, ScienceRemainingToCollect);
			if (clampByScienceRetrieved)
				return Math.Min(dataSize * SciencePerMB, ScienceRemainingToRetrieve);

			return dataSize * SciencePerMB;
		}

		public SubjectData(ExperimentInfo expInfo, Situation situation)
		{
			ExpInfo = expInfo;
			Situation = situation;
			Id = Lib.BuildString(ExpInfo.ExperimentId, "@", Situation.Id.ToString());
		}

		public void CheckRnD()
		{
			if (Science.GameHasRnD)
			{
				RnDSubject = ResearchAndDevelopment.GetSubjectByID(StockSubjectId);
			}
			else
			{
				ScienceSubject savedSubject;
				if (ScienceDB.sandboxSubjects.TryGetValue(StockSubjectId, out savedSubject))
					RnDSubject = savedSubject;
			}

			if (RnDSubject == null)
			{
				PercentRetrieved = 0.0;
				TimesCompleted = 0;
			}
			else
			{
				PercentRetrieved = RnDSubject.science / RnDSubject.scienceCap;
				TimesCompleted = GetTimesCompleted(PercentRetrieved);
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
			return ((int)percentRetrieved) + (decimalPart < 1.0 - (Science.scienceLeftForSubjectCompleted / ScienceMaxValue) ? 0 : 1);
		}

		public void CreateSubjectInRnD()
		{
			if (ExistsInRnD)
				return;

			Dictionary<string, ScienceSubject> subjectsDB;

			if (Science.GameHasRnD)
			{
				if (ResearchAndDevelopment.Instance == null)
					return;

				// get subjects dictionary using reflection
				subjectsDB = Lib.ReflectionValue<Dictionary<string, ScienceSubject>>
				(
					ResearchAndDevelopment.Instance,
					"scienceSubjects"
				);

				// try to get the subject, might be already created in some corner-case situations
				RnDSubject = ResearchAndDevelopment.GetSubjectByID(StockSubjectId);
			}
			else
			{
				subjectsDB = ScienceDB.sandboxSubjects;

				// try to get the subject, might be already created in some corner-case situations
				ScienceSubject savedSubject;
				if (subjectsDB.TryGetValue(StockSubjectId, out savedSubject))
					RnDSubject = savedSubject;
			}

			if (RnDSubject != null)
			{
				Lib.Log("CreateSubjectInRnD : ScienceSubject " + StockSubjectId + "exists already, this should not be happening !");
			}
			else
			{
				// create new subject
				RnDSubject = new ScienceSubject
				(
					StockSubjectId,
					FullTitle,
					(float)ExpInfo.DataScale,
					(float)Situation.SituationMultiplier,
					(float)ScienceMaxValue
				);

				// add it to RnD or sandbox DB
				subjectsDB.Add(StockSubjectId, RnDSubject);
			}

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
			PercentRetrieved = ((PercentRetrieved * ScienceMaxValue) + scienceAdded) / ScienceMaxValue;
			int newTimesCompleted = GetTimesCompleted(PercentRetrieved);
			if (newTimesCompleted > TimesCompleted)
			{
				TimesCompleted = newTimesCompleted;
				OnSubjectCompleted();
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
			if (!ExistsInRnD)
				CreateSubjectInRnD();

			RnDSubject.science = Math.Min((float)(RnDSubject.science + scienceValue), RnDSubject.scienceCap);
			RnDSubject.scientificValue = ResearchAndDevelopment.GetSubjectValue(RnDSubject.science, RnDSubject);
		}

		public void OnSubjectCompleted()
		{
			if (ExpInfo.UnlockResourceSurvey)
			{
				ResourceMap.Instance.UnlockPlanet(Situation.Body.flightGlobalsIndex);
				Message.Post(Localizer.Format("#autoLOC_259361", Situation.BodyTitle) + "</color>");
			}
		}
	}

	/// <summary>
	/// this is meant to handle subjects created by the stock system with the
	/// ResearchAndDevelopment.GetExperimentSubject overload that take a "sourceUId" string.
	/// In stock, it is only used by the asteroid samples, and I don't think there is any mod using that.
	/// </summary>
	public class UnknownSubjectData : SubjectData
	{
		private string extraSituationInfo;
		private string subjectId;

		public UnknownSubjectData(ExperimentInfo expInfo, Situation situation, string subjectId, ScienceSubject stockSubject = null, string extraSituationInfo = "") : base(expInfo, situation)
		{
			this.subjectId = subjectId;
			this.extraSituationInfo = extraSituationInfo;
			ExpInfo = expInfo;
			Situation = situation;
			RnDSubject = stockSubject;
			ScienceCollectedInFlight = 0.0;

			TimesCompleted = ExistsInRnD ? (int)(RnDSubject.science / (RnDSubject.scienceCap - Science.scienceLeftForSubjectCompleted)) : 0;
			PercentRetrieved = ExistsInRnD ? RnDSubject.science / ScienceMaxValue : 0.0;
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

		public override string BiomeTitle =>
			string.IsNullOrEmpty(Situation.BiomeTitle)
			? extraSituationInfo
			: string.IsNullOrEmpty(extraSituationInfo)
			? Situation.BiomeTitle
			: Lib.BuildString(Situation.BiomeTitle, " - ", extraSituationInfo);
	}
}
