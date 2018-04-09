using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{


	public sealed class ExperimentInfo
	{
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
			situation = subject_id.Length < i + 2 ? "Unknown" : Lib.SpacesOnCaps(subject_id.Substring(i + 1));
			situation = situation.Replace("Srf ", string.Empty).Replace("In ", string.Empty);

			// provide a full name
			fullname = Lib.BuildString(name, " (", situation, ")");

			// deduce max data amount
			max_amount = expdef != null ? expdef.scienceCap * expdef.dataScale : double.MaxValue;
		}


		public string id;                 // experiment identifier
		public ScienceExperiment expdef;  // experiment definition
		public string name;               // short description of the experiment
		public string fullname;           // full description of the experiment
		public string situation;          // description of the situation
		public double max_amount;         // max data amount for the experiment
	}


} // KERBALISM

