using System;


namespace KERBALISM
{


	public sealed class File
	{
		public File(double amount = 0.0)
		{
			size = amount;
			buff = 0.0;
			ts = Planetarium.GetUniversalTime();
			science_cap = 1;
		}

		public File(ConfigNode node)
		{
			size = Lib.ConfigValue(node, "size", 0.0);
			buff = Lib.ConfigValue(node, "buff", 0.0);
			science_cap = Lib.ConfigValue(node, "science_cap", 1.0);
		}

		public void Save(ConfigNode node)
		{
			node.AddValue("size", size);
			node.AddValue("buff", buff);
			node.AddValue("science_cap", science_cap);
		}

		public double size;   // data size in Mb
		public double buff;   // data transmitted but not credited
		public double ts; // last change time
		public double science_cap; // max. available science value factor
	}


} // KERBALISM