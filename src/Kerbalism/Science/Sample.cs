using System;


namespace KERBALISM
{

	/// <summary>
	/// Stores information about a science sample
	/// </summary>
	public sealed class Sample
	{
		/// <summary>
		/// Creates a science sample with the specified size in Mb
		/// </summary>
		public Sample(string subject_id, double size = 0.0)
		{
			this.size = size;
			analyze = false;
			resultText = ResearchAndDevelopment.GetResults(subject_id);
		}

		/// <summary>
		/// Creates a science sample from the specified config node
		/// </summary>
		public Sample(ConfigNode node)
		{
			size = Lib.ConfigValue(node, "size", 0.0);
			analyze = Lib.ConfigValue(node, "analyze", false);
			mass = Lib.ConfigValue(node, "mass", 0.0);
			resultText = Lib.ConfigValue(node, "resultText", "");
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
		}

		/// <summary>data size in Mb </summary>
		public double size;

		public double mass;

		/// <summary>randomized result text</summary>
		public string resultText; // 

		/// <summary>flagged for analysis in a laboratory</summary>
		public bool analyze;
	}


} // KERBALISM

