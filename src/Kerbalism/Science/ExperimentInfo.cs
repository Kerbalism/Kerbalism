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
		/// <summary>
		/// Creates information for an experiment with the specified identifier
		/// </summary>
		public ExperimentInfo(string subject_id)
		{
			// get experiment id out of subject id
			int i = subject_id.IndexOf('@');
			id = i > 0 ? subject_id.Substring(0, i) : subject_id;

			// get experiment definition
			// - available even in sandbox
			try {
				expdef = ResearchAndDevelopment.GetExperiment(id);
			} catch(Exception e) {
				Lib.Log("ERROR: failed to load experiment " + subject_id + ": " + e.Message);
				throw e;
			}

			// deduce short name for the subject
			name = expdef != null ? expdef.experimentTitle : Lib.UppercaseFirst(id);

			// deduce max data amount
			max_amount = expdef != null ? expdef.baseValue * expdef.dataScale : double.MaxValue;
		}

		/// <summary>
		/// returns  a pretty printed situation description for the UI
		/// </summary>
		public static string Situation(string full_subject_id)
		{
			int i = full_subject_id.IndexOf('@');
			var situation = full_subject_id.Length < i + 2
				? Localizer.Format("#KERBALISM_ExperimentInfo_Unknown")
				: Lib.SpacesOnCaps(full_subject_id.Substring(i + 1));
			situation = situation.Replace("Srf ", string.Empty).Replace("In ", string.Empty);
			return situation;
		}

		public string FullName(string full_subject_id)
		{
			return Lib.BuildString(name, " (", Situation(full_subject_id), ")");
		}

		public List<string> Situations()
		{
			List<string> result = new List<string>();
			string s;
			s = MaskToString("Landed", 1, expdef.situationMask, expdef.biomeMask); if (!string.IsNullOrEmpty(s)) result.Add(s);
			s = MaskToString("Splashed", 2, expdef.situationMask, expdef.biomeMask); if (!string.IsNullOrEmpty(s)) result.Add(s);
			s = MaskToString("Flying Low", 4, expdef.situationMask, expdef.biomeMask); if (!string.IsNullOrEmpty(s)) result.Add(s);
			s = MaskToString("Flying High", 8, expdef.situationMask, expdef.biomeMask); if (!string.IsNullOrEmpty(s)) result.Add(s);
			s = MaskToString("In Space Low", 16, expdef.situationMask, expdef.biomeMask); if (!string.IsNullOrEmpty(s)) result.Add(s);
			s = MaskToString("In Space High", 32, expdef.situationMask, expdef.biomeMask); if (!string.IsNullOrEmpty(s)) result.Add(s);
			return result;
		}

		private static string MaskToString(string text, uint flag, uint situationMask, uint biomeMask)
		{
			string result = string.Empty;
			if ((flag & situationMask) == 0) return result;
			result = text;
			if ((flag & biomeMask) != 0) result += " (Biomes)";
			return result;
		}

		/// <summary>
		/// experiment identifier
		/// </summary>
		public string id;

		/// <summary>
		/// experiment definition
		/// </summary>
		private ScienceExperiment expdef;

		/// <summary>
		/// short description of the experiment
		/// </summary>
		public string name;

		/// <summary>
		/// max data amount for the experiment
		/// </summary>
		public double max_amount;
	}


} // KERBALISM

