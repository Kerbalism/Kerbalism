using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{
	public class RuleData
	{
		public RuleData()
		{
			problem = 0.0;
			message = 0;
			lifetime = false;
		}

		public RuleData(ConfigNode node)
		{
			problem = Lib.ConfigValue(node, "problem", 0.0);
			message = Lib.ConfigValue(node, "message", 0u);
			lifetime = Lib.ConfigValue(node, "lifetime", false);
		}

		public void Save(ConfigNode node)
		{
			node.AddValue("problem", problem);
			node.AddValue("message", message);
			node.AddValue("lifetime", lifetime);
		}

		/// <summary>
		/// Reset process value, except lifetime values
		/// </summary>
		public void Reset()
		{
			message = 0;
			if (!lifetime) problem = 0.0;
		}

		public double problem;      // accumulator for the rule
		public uint message;        // used to avoid sending messages multiple times
		public bool lifetime;       // is this a life time value or not
	}


} // KERBALISM

