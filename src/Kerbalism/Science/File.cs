using System;

namespace KERBALISM
{
	public sealed class File
	{
		/// <summary>data size in Mb</summary>
		public double size;
		/// <summary>randomized result text</summary>
		public string resultText;

		public string subject_id;

		public ExperimentInfo expInfo;

		public double transmitRate = 0.0;

		public bool useStockCrediting;

		public File(string subject_id, double amount = 0.0, bool useStockCrediting = true)
		{
			this.subject_id = subject_id;
			this.useStockCrediting = useStockCrediting;
			size = amount;
			resultText = ResearchAndDevelopment.GetResults(subject_id);
			expInfo = Science.Experiment(subject_id);
		}

		public File(string subject_id, ConfigNode node)
		{
			this.subject_id = subject_id;
			size = Lib.ConfigValue(node, "size", 0.0);
			resultText = Lib.ConfigValue(node, "resultText", "");
			useStockCrediting = Lib.ConfigValue(node, "useStockCrediting", true);
			expInfo = Science.Experiment(subject_id);
			expInfo.AddDataCollectedInFlight(size);
		}

		public void Save(ConfigNode node)
		{
			node.AddValue("size", size);
			node.AddValue("resultText", resultText);
			node.AddValue("useStockCrediting", useStockCrediting);
		}

		public ScienceData ConvertToStockData()
		{
			return new ScienceData((float)size, 1.0f, 1.0f, subject_id, expInfo.SubjectName);
		}
	}


} // KERBALISM
