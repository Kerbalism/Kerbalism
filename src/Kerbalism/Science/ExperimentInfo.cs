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
			expdef = ResearchAndDevelopment.GetExperiment(id);

			// deduce short name for the subject
			name = expdef != null ? expdef.experimentTitle : Lib.UppercaseFirst(id);

			// deduce situation for the subject
			situation = subject_id.Length < i + 2 ? Localizer.Format("#KERBALISM_ExperimentInfo_Unknown") : Lib.SpacesOnCaps(subject_id.Substring(i + 1));
			situation = situation.Replace("Srf ", string.Empty).Replace("In ", string.Empty);

			// provide a full name
			fullname = Lib.BuildString(name, " (", situation, ")");

			// deduce max data amount
			max_amount = expdef != null ? expdef.baseValue * expdef.dataScale : double.MaxValue;
		}


		/// <summary>
		/// experiment identifier
		/// </summary>
		public string id;

		/// <summary>
		/// experiment definition
		/// </summary>
		public ScienceExperiment expdef;

		/// <summary>
		/// short description of the experiment
		/// </summary>
		public string name;

		/// <summary>
		/// full description of the experiment
		/// </summary>
		public string fullname;

		/// <summary>
		/// description of the situation
		/// </summary>
		public string situation;

		/// <summary>
		/// max data amount for the experiment
		/// </summary>
		public double max_amount;
	}


} // KERBALISM

