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
				return;
			}

			completionPercent = stockSubject.science / stockSubject.scienceCap;

			double decimalPart = completionPercent - Math.Truncate(completionPercent);
			timesCompleted = (int)(completionPercent / 1.0) + (decimalPart < 1.0 - Science.scienceLeftForSubjectCompleted ? 0 : 1);
		}

		public SubjectData(ConfigNode node)
		{
			completionPercent = Lib.ConfigValue(node, "completionPercent", 0.0);

			double decimalPart = completionPercent - Math.Truncate(completionPercent);
			timesCompleted = (int)(completionPercent / 1.0) + (decimalPart < 1.0 - Science.scienceLeftForSubjectCompleted ? 0 : 1);
		}

		public void Save(ConfigNode node)
		{
			node.AddValue("completionPercent", completionPercent);
		}
	}
}
