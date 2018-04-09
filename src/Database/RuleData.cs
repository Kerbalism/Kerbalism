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
			time_since = 0.0;
		}

		public RuleData(ConfigNode node)
		{
			problem = Lib.ConfigValue(node, "problem", 0.0);
			message = Lib.ConfigValue(node, "message", 0u);
			time_since = Lib.ConfigValue(node, "time_since", 0.0);
		}

		public void save(ConfigNode node)
		{
			node.AddValue("problem", problem);
			node.AddValue("message", message);
			node.AddValue("time_since", time_since);
		}

		public double problem;      // accumulator for the rule
		public uint message;      // used to avoid sending messages multiple times
		public double time_since;   // time since last execution, if interval > 0
	}


} // KERBALISM

