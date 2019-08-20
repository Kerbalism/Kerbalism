using System;


namespace KERBALISM
{


	public sealed class File
	{
		public File(string subject_id, double amount = 0.0)
		{
			size = amount;
			buff = 0.0;
			ts = Planetarium.GetUniversalTime();
			resultText = ResearchAndDevelopment.GetResults(subject_id);
		}

		public File(ConfigNode node)
		{
			size = Lib.ConfigValue(node, "size", 0.0);
			buff = Lib.ConfigValue(node, "buff", 0.0);
			resultText = Lib.ConfigValue(node, "resultText", "");
		}

		public void Save(ConfigNode node)
		{
			node.AddValue("size", size);
			node.AddValue("buff", buff);
			node.AddValue("resultText", resultText);
		}

		/// <summary>data size in Mb</summary>
		public double size;
		/// <summary>data transmitted but not credited</summary>
		public double buff;
		/// <summary>randomized result text</summary>
		public string resultText; 
		/// <summary>last change time</summary>
		public double ts;
	}


} // KERBALISM
