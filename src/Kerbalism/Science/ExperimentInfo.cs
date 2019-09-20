using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{

	/// <summary>
	/// Stores information about an experiment
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

		private SubjectData subjectData;

		/// <summary> subject identifier </summary>
		public string SubjectId { get; private set; }

		/// <summary> full description of the subject </summary>
		public string SubjectName { get; private set; }

		/// <summary> subject situation </summary>
		public string SubjectSituation { get; private set; }

		/// <summary> has the subject been retrieved fully in RnD at least once ? </summary>
		public bool SubjectIsCompleted => subjectData == null ? false : subjectData.timesCompleted > 0;

		/// <summary> how many times the subject has been fully retrieved in RnD </summary>
		public int SubjectTimesCompleted => subjectData == null ? 0 : subjectData.timesCompleted;

		/// <summary> percentage [0;1] of science retrieved </summary>
		public double PercentRetrieved => subjectData == null ? 0.0 : subjectData.completionPercent;

		/// <summary> subject science points per MB of data </summary>
		public double SubjectSciencePerMB => ScienceValue / MaxAmount;

		/// <summary> science points collected in all vessels but not yet recovered or transmitted </summary>
		public double SubjectScienceCollectedInFlight { get; private set; }

		/// <summary> stock science subject definition, will be null before an experiment create it</summary>
		public ScienceSubject StockSubject
			=> ResearchAndDevelopment.GetSubjectByID(SubjectId);

		/// <summary> science points recovered or transmitted </summary>
		public float ScienceCollectedInKSC
			=> StockSubject == null ? 0f : StockSubject.science * HighLogic.CurrentGame.Parameters.Career.ScienceGainMultiplier;

		/// <summary> all science points recovered, transmitted or collected in flight </summary>
		public float ScienceCollectedTotal
			=> (float)SubjectScienceCollectedInFlight + ScienceCollectedInKSC;

		/// <summary> total science value of the subject. Will be PositiveInfinity until an experiment create the subject </summary>
		public float ScienceValue
			=> StockSubject == null ? float.PositiveInfinity : StockSubject.scienceCap * HighLogic.CurrentGame.Parameters.Career.ScienceGainMultiplier;

		/// <summary> science value remaining to collect. Will be PositiveInfinity until an experiment create the subject </summary>
		public float ScienceRemainingToCollect
			=> Math.Max(ScienceValue - ScienceCollectedTotal, 0f);

		/// <summary> science value remaining to retriecve. Will be PositiveInfinity until an experiment create the subject </summary>
		public float ScienceRemainingToRetrieve
			=> ScienceValue - ScienceCollectedInKSC;

		/// <summary> percentage [0;1] of science collected </summary>
		public float PercentCollectedTotal => ScienceCollectedTotal / ScienceValue;

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
			SubjectSituation = Situation(subject_id);
			SubjectName = Lib.BuildString(Name, " (", SubjectSituation, ")");

			if (IsSubject)
				subjectData = DB.Subject(SubjectId);

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

		/// <summary> add data to the in-flight collected science </summary>
		public void AddDataCollectedInFlight(double dataAmount)
		{
			SubjectScienceCollectedInFlight += dataAmount * SubjectSciencePerMB;
		}

		/// <summary> remove data from the in-flight collected science </summary>
		public void RemoveDataCollectedInFlight(double dataAmount)
		{
			SubjectScienceCollectedInFlight -= dataAmount * SubjectSciencePerMB;
			if (SubjectScienceCollectedInFlight < 0f) SubjectScienceCollectedInFlight = 0f;
		}

		/// <summary> remove science points from the in-flight collected science </summary>
		public void RemoveDataCollectedInFlight(float credits)
		{
			SubjectScienceCollectedInFlight -= credits;
			if (SubjectScienceCollectedInFlight < 0f) SubjectScienceCollectedInFlight = 0f;
		}

		/// <summary>
		/// update our subject completion database.
		/// if the subject was just completed, return the amount of times it has ever been completed.
		/// otherwise return -1
		/// </summary>
		public int UpdateSubjectCompletion(float scienceAdded)
		{
			subjectData.completionPercent = ((subjectData.completionPercent * ScienceValue) + scienceAdded) / ScienceValue;

			double decimalPart = subjectData.completionPercent - Math.Truncate(subjectData.completionPercent);
			int timesCompleted = (int)(subjectData.completionPercent / 1.0) + (decimalPart < 1.0 - Science.scienceLeftForSubjectCompleted ? 0 : 1);

			if (timesCompleted > subjectData.timesCompleted)
			{
				subjectData.timesCompleted = timesCompleted;
				return timesCompleted;
			}

			return -1;
		}

		/// <summary>
		/// returns  a pretty printed situation description for the UI
		/// </summary>
		private string Situation(string full_subject_id)
		{
			int i = full_subject_id.IndexOf('@');
			var situation = full_subject_id.Length < i + 2
				? Localizer.Format("#KERBALISM_ExperimentInfo_Unknown")
				: Lib.SpacesOnCaps(full_subject_id.Substring(i + 1));
			situation = situation.Replace("Srf ", string.Empty).Replace("In ", string.Empty);
			return situation;
		}

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

