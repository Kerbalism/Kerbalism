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
		public Sample(double size = 0.0)
		{
			this.size = size;
			analyze = false;
			science_cap = 1;
		}

		/// <summary>
		/// Creates a science sample from the specified config node
		/// </summary>
		public Sample(ConfigNode node)
		{
			size = Lib.ConfigValue(node, "size", 0.0);
			analyze = Lib.ConfigValue(node, "analyze", false);
			mass = Lib.ConfigValue(node, "mass", 0.0);
			science_cap = Lib.ConfigValue(node, "science_cap", 1.0);
		}

		/// <summary>
		/// Stores a science sample into the specified config node
		/// </summary>
		public void Save(ConfigNode node)
		{
			node.AddValue("size", size);
			node.AddValue("analyze", analyze);
			node.AddValue("mass", mass);
			node.AddValue("science_cap", science_cap);
		}

		/// <summary>
		/// data size in Mb
		/// </summary>
		public double size;

		public double mass;

		/// <summary>
		///	flagged for analysis in a laboratory
		/// </summary>
		public bool analyze;

		/// <summary>
		/// Factor for the max. available science value from underlying experiment
		/// </summary>
		public double science_cap;
	}


} // KERBALISM

