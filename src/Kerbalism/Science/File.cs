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
		}

		public File(ConfigNode node)
		{
			size = Lib.ConfigValue(node, "size", 0.0);
			buff = Lib.ConfigValue(node, "buff", 0.0);
		}

		public void Save(ConfigNode node)
		{
			node.AddValue("size", size);
			node.AddValue("buff", buff);
		}

		public double size;   // data size in Mb
		public double buff;   // data transmitted but not credited
		public double ts; // last change time
	}


} // KERBALISM