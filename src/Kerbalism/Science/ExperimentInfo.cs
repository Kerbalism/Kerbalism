using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{

	/// <summary>
	/// Stores information about an experiment_id or a subject_id
	/// Beware that subject information will be incomplete until the stock `ScienceSubject` is created in RnD
	/// </summary>
	public sealed class ExperimentInfo
	{
		/// <summary> experiment definition </summary>
		private ScienceExperiment expdef;

		/// <summary> experiment identifier </summary>
		public string ExperimentId { get; private set; }

		/// <summary> UI friendly name of the experiment </summary>
		public string Name { get; private set; }

		/// <summary> max data amount for the experiment </summary>
		public double MaxAmount { get; private set; }

		/// <summary> experiment situation mask </summary>
		public uint SituationMask { get; private set; }

		/// <summary> experiment biome mask </summary>
		public uint BiomeMask { get; private set; }

		/// <summary> if true, this ExperimentInfo is about a specific subject, and all Subject* properties can be used </summary>
		public bool IsSubject { get; private set; }

		/// <summary>
		/// false if the stock ScienceSubject hasn't been created yet in the RnD instance. 
		/// while false, SubjectScienceMaxValue and all derived properties will return invalid values
		/// </summary>
		public bool SubjectExistsInRnD => StockSubject != null;

		private SubjectData SubjectData => DB.Subject(SubjectId);

		/// <summary> subject identifier </summary>
		public string SubjectId { get; private set; }

		/// <summary> full description of the subject </summary>
		public string SubjectName { get; private set; }

		/// <summary> UI friendly subject situation </summary>
		public string SubjectSituation { get; private set; }

		/// <summary> has the subject been retrieved fully in RnD at least once ? </summary>
		public bool SubjectIsCompleted => SubjectData.timesCompleted > 0;

		/// <summary> how many times the subject has been fully retrieved in RnD </summary>
		public int SubjectTimesCompleted => SubjectData.timesCompleted;

		/// <summary> percentage [0;x] of science retrieved, can be > 1 if subject has been retrieved more than once</summary>
		public double SubjectPercentRetrieved => SubjectData.completionPercent;

		/// <summary> subject science points per MB of data. Will be PositiveInfinity while SubjectExistsInRnD is false</summary>
		public double SubjectSciencePerMB => SubjectScienceMaxValue / MaxAmount;

		/// <summary> science points collected in all vessels but not yet recovered or transmitted </summary>
		public double SubjectScienceCollectedInFlight { get; private set; }

		/// <summary> stock science subject definition, will be null before an experiment create it</summary>
		public ScienceSubject StockSubject
			=> ResearchAndDevelopment.GetSubjectByID(SubjectId);

		/// <summary> science points recovered or transmitted </summary>
		public float SubjectScienceRetrievedInKSC
			=> SubjectExistsInRnD ? StockSubject.science * HighLogic.CurrentGame.Parameters.Career.ScienceGainMultiplier : 0f;

		/// <summary> all science points recovered, transmitted or collected in flight </summary>
		public float SubjectScienceCollectedTotal
			=> (float)SubjectScienceCollectedInFlight + SubjectScienceRetrievedInKSC;

		/// <summary> total science value of the subject. Will be PositiveInfinity while SubjectExistsInRnD is false </summary>
		public float SubjectScienceMaxValue
			=> SubjectExistsInRnD ? StockSubject.scienceCap * HighLogic.CurrentGame.Parameters.Career.ScienceGainMultiplier : float.PositiveInfinity;

		/// <summary> science value remaining to collect. Will be PositiveInfinity while SubjectExistsInRnD is false </summary>
		public float SubjectScienceRemainingToCollect
			=> Math.Max(SubjectScienceMaxValue - SubjectScienceCollectedTotal, 0f);

		/// <summary> science value remaining to retriecve. Will be PositiveInfinity while SubjectExistsInRnD is false</summary>
		public float SubjectScienceRemainingToRetrieve
			=> SubjectScienceMaxValue - SubjectScienceRetrievedInKSC;

		/// <summary> percentage [0;1] of science collected. Will be PositiveInfinity while SubjectExistsInRnD is false</summary>
		public float SubjectPercentCollectedTotal
			=> SubjectScienceCollectedTotal / SubjectScienceMaxValue;

		/// <summary> science value for the given data size </summary>
		public double ScienceValue(double dataSize, bool clampByScienceRetrieved = false, bool clampByScienceRetrievedAndCollected = false)
		{
			if (clampByScienceRetrievedAndCollected)
				return Math.Min(dataSize * SubjectSciencePerMB, SubjectScienceRemainingToCollect);
			if (clampByScienceRetrieved)
				return Math.Min(dataSize * SubjectSciencePerMB, SubjectScienceRemainingToRetrieve);

			return dataSize * SubjectSciencePerMB;
		}

		/// <summary>
		/// Creates information for an experiment or a subject with the specified identifier
		/// </summary>
		public ExperimentInfo(string subject_id)
		{
			SubjectId = subject_id;

			// get experiment id out of subject id
			int i = subject_id.IndexOf('@');
			IsSubject = i > 0;

			ExperimentId = IsSubject ? subject_id.Substring(0, i) : subject_id;

			// get experiment definition
			// - available even in sandbox
			try {
				expdef = ResearchAndDevelopment.GetExperiment(ExperimentId);
			} catch(Exception e) {
				Lib.Log("ERROR: failed to load experiment " + subject_id + ": " + e.Message);
				throw e;
			}

			// deduce short name for the experiment
			Name = expdef != null ? expdef.experimentTitle : Lib.UppercaseFirst(ExperimentId);

			// deduce max data amount
			// use scienceCap here, not baseValue. this is because of Serenity,
			// for deployed experiments the baseValue has the hourly rate and scienceCap the
			// total value. This relies on a config patch that sets scienceCap = baseValue
			// for all non-Serenity experiments.
			MaxAmount = expdef != null ? expdef.scienceCap * expdef.dataScale : float.MaxValue;

			SituationMask = expdef.situationMask;
			BiomeMask = expdef.biomeMask;

			SubjectScienceCollectedInFlight = 0f;
			SubjectSituation = ParseSubjectSituation(subject_id);
			SubjectName = Lib.BuildString(Name, " (", SubjectSituation, ")");

			if (IsSubject)
			{
				// TODO : remove this for release
				if (StockSubject == null)
				{
					// Message.Post("DEBUG : Experiment info created but subject '" + SubjectId + "' doesn't exists in RnD\nRnD instance : " + (ResearchAndDevelopment.Instance == null ? "null" : "not null"));
					Lib.Log("Experiment info created but subject '" + SubjectId + "' doesn't exists in RnD - RnD instance : " + (ResearchAndDevelopment.Instance == null ? "null" : "not null"));
				}

				// we collect data only if the subject exists
				if (StockSubject != null)
				{
					foreach (Drive drive in DB.drives.Values)
					{
						if (drive.files.ContainsKey(subject_id))
							SubjectScienceCollectedInFlight += (float)(drive.files[subject_id].size * SubjectSciencePerMB);

						if (drive.samples.ContainsKey(subject_id))
							SubjectScienceCollectedInFlight += (float)(drive.samples[subject_id].size * SubjectSciencePerMB);
					}
				}
			}
		}

		public void CreateSubjectInRnD(Vessel v, ExperimentSituation sit)
		{
			// in sandbox, do nothing else
			if (ResearchAndDevelopment.Instance == null)
				return;

			if (!IsSubject || SubjectExistsInRnD)
				return;

			// get subjects container using reflection
			// - we tried just changing the subject.id instead, and
			//   it worked but the new id was obviously used only after
			//   putting RnD through a serialization->deserialization cycle
			Dictionary<string, ScienceSubject> subjects = Lib.ReflectionValue<Dictionary<string, ScienceSubject>>
			(
				ResearchAndDevelopment.Instance,
				"scienceSubjects"
			);

			float multiplier = sit.Multiplier(this);
			float cap = multiplier * expdef.baseValue;

			// create new subject
			ScienceSubject subject = new ScienceSubject
			(
				  	SubjectId,
					SubjectName,
					expdef.dataScale,
				  	multiplier,
					cap
			);

			// add it to RnD
			subjects.Add(SubjectId, subject);

			return;
		}

		/// <summary> add data to the in-flight collected science </summary>
		public void AddDataCollectedInFlight(double dataAmount)
		{
			SubjectScienceCollectedInFlight += dataAmount * SubjectSciencePerMB;
		}

		/// <summary> remove data from the in-flight collected science </summary>
		public void RemoveDataCollectedInFlight(double dataAmount)
		{
			SubjectScienceCollectedInFlight -= dataAmount * SubjectSciencePerMB;
			if (SubjectScienceCollectedInFlight < 0.0) SubjectScienceCollectedInFlight = 0.0;
		}

		/// <summary> remove science points from the in-flight collected science </summary>
		public void RemoveScienceCollectedInFlight(double credits)
		{
			SubjectScienceCollectedInFlight -= credits;
			if (SubjectScienceCollectedInFlight < 0.0) SubjectScienceCollectedInFlight = 0.0;
		}

		/// <summary>
		/// update our subject completion database.
		/// if the subject was just completed, return the amount of times it has ever been completed.
		/// otherwise return -1
		/// </summary>
		public int UpdateSubjectCompletion(double scienceAdded)
		{
			SubjectData.completionPercent = ((SubjectData.completionPercent * SubjectScienceMaxValue) + scienceAdded) / SubjectScienceMaxValue;

			double decimalPart = SubjectData.completionPercent - Math.Truncate(SubjectData.completionPercent);
			int timesCompleted = (int)(SubjectData.completionPercent / 1.0) + (decimalPart < 1.0 - Science.scienceLeftForSubjectCompleted ? 0 : 1);

			if (timesCompleted > SubjectData.timesCompleted)
			{
				SubjectData.timesCompleted = timesCompleted;
				return timesCompleted;
			}

			return -1;
		}

		/// <summary> UI friendly situation description for the subject (slow, use the non-static SubjectSituation property is possible)</summary>
		public static string ParseSubjectSituation(string full_subject_id)
		{
			int i = full_subject_id.IndexOf('@');
			string situation = full_subject_id.Length < i + 2
				? Localizer.Format("#KERBALISM_ExperimentInfo_Unknown")
				: ParseSituationSubstring(full_subject_id.Substring(i + 1));
			return situation;
		}

		/// <summary> UI friendly description for the situation (as formatted after the "@" of a subject_id)</summary>
		public static string ParseSituationSubstring(string situationSubstring)
		{
			return Lib.SpacesOnCaps(situationSubstring.Replace("Srf", string.Empty).Replace("In", string.Empty));
		}

		/// <summary> UI friendly list of situations available for the experiment</summary>
		public List<string> Situations()
		{
			List<string> result = new List<string>();

			string s;

			s = MaskToString(KerbalismSituations.SrfLanded, expdef.situationMask, expdef.biomeMask); if (!string.IsNullOrEmpty(s)) result.Add(s);
			s = MaskToString(KerbalismSituations.SrfSplashed, expdef.situationMask, expdef.biomeMask); if (!string.IsNullOrEmpty(s)) result.Add(s);
			s = MaskToString(KerbalismSituations.FlyingLow, expdef.situationMask, expdef.biomeMask); if (!string.IsNullOrEmpty(s)) result.Add(s);
			s = MaskToString(KerbalismSituations.FlyingHigh, expdef.situationMask, expdef.biomeMask); if (!string.IsNullOrEmpty(s)) result.Add(s);
			s = MaskToString(KerbalismSituations.InSpaceLow, expdef.situationMask, expdef.biomeMask); if (!string.IsNullOrEmpty(s)) result.Add(s);
			s = MaskToString(KerbalismSituations.InSpaceHigh, expdef.situationMask, expdef.biomeMask); if (!string.IsNullOrEmpty(s)) result.Add(s);

			s = MaskToString(KerbalismSituations.InnerBelt, expdef.situationMask, expdef.biomeMask); if (!string.IsNullOrEmpty(s)) result.Add(s);
			s = MaskToString(KerbalismSituations.OuterBelt, expdef.situationMask, expdef.biomeMask); if (!string.IsNullOrEmpty(s)) result.Add(s);
			s = MaskToString(KerbalismSituations.Magnetosphere, expdef.situationMask, expdef.biomeMask); if (!string.IsNullOrEmpty(s)) result.Add(s);
			s = MaskToString(KerbalismSituations.Reentry, expdef.situationMask, expdef.biomeMask); if (!string.IsNullOrEmpty(s)) result.Add(s);
			s = MaskToString(KerbalismSituations.Interstellar, expdef.situationMask, expdef.biomeMask); if (!string.IsNullOrEmpty(s)) result.Add(s);

			return result;
		}

		private static string MaskToString(KerbalismSituations sit, uint situationMask, uint biomeMask)
		{
			string result = string.Empty;
			if (((int)sit & situationMask) == 0) return result;
			result = Lib.SpacesOnCaps(sit.ToString().Replace("Srf", ""));
			if (((int)sit & biomeMask) != 0) result += " (Biomes)";
			return result;
		}


	}


} // KERBALISM

