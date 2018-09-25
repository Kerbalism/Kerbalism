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
			lifetime = false;
			rate = 0.0;
		}

		public RuleData(ConfigNode node)
		{
			problem = Lib.ConfigValue(node, "problem", 0.0);
			message = Lib.ConfigValue(node, "message", 0u);
			time_since = Lib.ConfigValue(node, "time_since", 0.0);
			lifetime = Lib.ConfigValue(node, "lifetime", false);
			rate = Lib.ConfigValue(node, "rate", 0.0);
		}

		public void Save(ConfigNode node)
		{
			node.AddValue("problem", problem);
			node.AddValue("message", message);
			node.AddValue("time_since", time_since);
			node.AddValue("lifetime", lifetime);
			if(Math.Abs(rate) > Double.Epsilon)
				node.AddValue("rate", rate);
		}

		/// <summary>
		/// Reset process value, except lifetime values
		/// </summary>
		public void Reset()
		{
			message = 0;
			time_since = 0;
			if (!lifetime) problem = 0.0;
		}

		public double problem;      // accumulator for the rule
		public uint message;        // used to avoid sending messages multiple times
		public double time_since;   // time since last execution, if interval > 0
		public bool lifetime;       // is this a life time value or not
		public double rate;			// process rate override (set in Sickbay)
	}


} // KERBALISM

