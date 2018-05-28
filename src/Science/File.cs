using System;


namespace KERBALISM
{


	public sealed class File
	{
		public File(double amount = 0.0)
		{
			size = amount;
			buff = 0.0;
			send = false;
		}

		public File(ConfigNode node)
		{
			size = Lib.ConfigValue(node, "size", 0.0);
			buff = Lib.ConfigValue(node, "buff", 0.0);
			send = Lib.ConfigValue(node, "send", false);
		}

		public void Save(ConfigNode node)
		{
			node.AddValue("size", size);
			node.AddValue("buff", buff);
			node.AddValue("send", send);
		}

		public double size;   // data size in Mb
		public double buff;   // data transmitted but not credited
		public bool send;   // send-home flag
	}


} // KERBALISM