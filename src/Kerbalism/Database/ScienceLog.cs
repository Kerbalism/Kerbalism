using System;

namespace KERBALISM
{
	public class ScienceLog
	{
		public double SumTotal { get; private set; }
		public string LastSubjectTitle { get; private set; }
		public double LastTransmissionTime { get; private set; }

		internal void AddCredits(float credits, ScienceSubject subject)
		{
			SumTotal += credits;
			LastSubjectTitle = subject.title;
			LastTransmissionTime = Planetarium.GetUniversalTime();
		}

		internal void Load(ConfigNode node)
		{
			SumTotal = Lib.ConfigValue(node, "SumTotal", 0.0);
			LastSubjectTitle = Lib.ConfigValue(node, "LastSubjectTitle", string.Empty);
			LastTransmissionTime = Lib.ConfigValue(node, "LastTransmissionTime", 0.0);
		}

		internal void Save(ConfigNode node)
		{
			node.AddValue("SumTotal", SumTotal);
			node.AddValue("LastSubjectTitle", LastSubjectTitle);
			node.AddValue("LastTransmissionTime", LastTransmissionTime);
		}
	}
}
