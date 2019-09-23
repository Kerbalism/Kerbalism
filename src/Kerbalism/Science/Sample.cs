using System;


namespace KERBALISM
{

	/// <summary>
	/// Stores information about a science sample
	/// </summary>
	public sealed class Sample
	{
		public string subject_id;

		public ExperimentInfo expInfo;

		/// <summary>data size in Mb </summary>
		public double size;

		public double mass;

		/// <summary>randomized result text</summary>
		public string resultText;

		/// <summary>flagged for analysis in a laboratory</summary>
		public bool analyze;

		public bool useStockCrediting;

		/// <summary>
		/// Creates a science sample with the specified size in Mb
		/// </summary>
		public Sample(string subject_id, double size = 0.0, bool useStockCrediting = true)
		{
			this.subject_id = subject_id;
			this.useStockCrediting = useStockCrediting;
			this.size = size;
			analyze = false;
			resultText = ResearchAndDevelopment.GetResults(subject_id);
			expInfo = Science.Experiment(subject_id);
		}

		/// <summary>
		/// Creates a science sample from the specified config node
		/// </summary>
		public Sample(string subject_id, ConfigNode node)
		{
			this.subject_id = subject_id;
			size = Lib.ConfigValue(node, "size", 0.0);
			analyze = Lib.ConfigValue(node, "analyze", false);
			mass = Lib.ConfigValue(node, "mass", 0.0);
			resultText = Lib.ConfigValue(node, "resultText", "");
			useStockCrediting = Lib.ConfigValue(node, "useStockCrediting", true);
			expInfo = Science.Experiment(subject_id);
			expInfo.AddDataCollectedInFlight(size);
		}

		/// <summary>
		/// Stores a science sample into the specified config node
		/// </summary>
		public void Save(ConfigNode node)
		{
			node.AddValue("size", size);
			node.AddValue("analyze", analyze);
			node.AddValue("mass", mass);
			node.AddValue("resultText", resultText);
			node.AddValue("useStockCrediting", useStockCrediting);
		}

		public ScienceData ConvertToStockData()
		{
			return new ScienceData((float)size, 1.0f, 1.0f, subject_id, expInfo.SubjectName);
		}
	}


} // KERBALISM

