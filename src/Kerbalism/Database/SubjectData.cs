using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KERBALISM
{
	public class SubjectData
	{
		public double completionPercent;
		public int timesCompleted;

		public SubjectData(string subject_id)
		{
			ScienceSubject stockSubject = ResearchAndDevelopment.GetSubjectByID(subject_id);
			if (stockSubject == null)
			{
				completionPercent = 0.0;
				timesCompleted = 0;
				Message.Post("SUBJECTDATA CTOR - EXP NULL");
				Lib.Log("SUBJECTDATA CTOR - SUBJECT NULL , " + (ResearchAndDevelopment.Instance == null ? "RND NULL - " : "RND NOT NULL - ") + subject_id);
				return;
			}

			completionPercent = stockSubject.science / stockSubject.scienceCap;

			double decimalPart = completionPercent - Math.Truncate(completionPercent);
			timesCompleted = (int)(completionPercent / 1.0) + (decimalPart < 1.0 - Science.scienceLeftForSubjectCompleted ? 0 : 1);

			Lib.Log("SUBJECTDATA CTOR : " + completionPercent.ToString("P0") + " - " + subject_id);
		}

		public SubjectData(ConfigNode node, string subject_id)
		{
			completionPercent = Lib.ConfigValue(node, "completionPercent", 0.0);

			double decimalPart = completionPercent - Math.Truncate(completionPercent);
			timesCompleted = (int)(completionPercent / 1.0) + (decimalPart < 1.0 - Science.scienceLeftForSubjectCompleted ? 0 : 1);

			Lib.Log("SUBJECTDATA LOAD : " + completionPercent.ToString("P0") + " - " + subject_id);
		}

		public void Save(ConfigNode node, string subject_id)
		{
			node.AddValue("completionPercent", completionPercent);
			Lib.Log("SUBJECTDATA SAVE : " + completionPercent.ToString("P0") + " - " + subject_id);
		}
	}
}
