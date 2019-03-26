using System;


namespace KERBALISM
{


	public sealed class File
	{
		public File(double amount = 0.0)
		{
			size = amount;
			buff = 0.0;
			send = PreferencesBasic.Instance.transmitScience;
			silentTransmission = false;
		}

		public File(ConfigNode node)
		{
			size = Lib.ConfigValue(node, "size", 0.0);
			buff = Lib.ConfigValue(node, "buff", 0.0);
			send = Lib.ConfigValue(node, "send", PreferencesBasic.Instance.transmitScience);
			silentTransmission = Lib.ConfigValue(node, "silentTransmission", false);
		}

		public void Save(ConfigNode node)
		{
			node.AddValue("size", size);
			node.AddValue("buff", buff);
			node.AddValue("send", send);
			node.AddValue("silentTransmission", silentTransmission);
		}

		public double size;   // data size in Mb
		public double buff;   // data transmitted but not credited
		public bool send;     // send-home flag
		public bool silentTransmission; // don't show a message when transmitted
	}


} // KERBALISM