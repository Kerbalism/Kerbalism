using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{
	public class SupplyData
	{
		public SupplyData()
		{
			message = 0;
		}

		public SupplyData(ConfigNode node)
		{
			message = Lib.ConfigValue(node, "message", 0u);
		}

		public void Save(ConfigNode node)
		{
			node.AddValue("message", message);
		}

		public uint message;  // used to avoid sending messages multiple times
	}



} // KERBALISM
