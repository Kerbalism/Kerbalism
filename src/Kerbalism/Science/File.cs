using System;


namespace KERBALISM
{


	public sealed class File
	{
		public File(string subject_id, double amount = 0.0)
		{
			this.subject_id = subject_id;
			size = amount;
			resultText = ResearchAndDevelopment.GetResults(subject_id);
			expInfo = Science.Experiment(subject_id);
		}

		public File(string subject_id, ConfigNode node)
		{
			this.subject_id = subject_id;
			size = Lib.ConfigValue(node, "size", 0.0);
			resultText = Lib.ConfigValue(node, "resultText", "");
			expInfo = Science.Experiment(subject_id);
		}

		public void Save(ConfigNode node)
		{
			node.AddValue("size", size);
			node.AddValue("resultText", resultText);
		}

		public string subject_id;

		public ExperimentInfo expInfo;

		/// <summary>data size in Mb</summary>
		public double size;
		/// <summary>randomized result text</summary>
		public string resultText;

		public double transmitRate = 0.0;
	}


} // KERBALISM
