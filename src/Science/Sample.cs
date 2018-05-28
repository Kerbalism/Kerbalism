using System;


namespace KERBALISM
{


	public sealed class Sample
	{
		public Sample(double amount = 0.0)
		{
			size = amount;
			analyze = false;
		}

		public Sample(ConfigNode node)
		{
			size = Lib.ConfigValue(node, "size", 0.0);
			analyze = Lib.ConfigValue(node, "analyze", false);
		}

		public void Save(ConfigNode node)
		{
			node.AddValue("size", size);
			node.AddValue("analyze", analyze);
		}

		public double size;     // data size in Mb
		public bool analyze;  // analyze-in-lab flag
	}


} // KERBALISM

