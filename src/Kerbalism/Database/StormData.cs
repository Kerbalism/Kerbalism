using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{

	public class StormData
	{
		public StormData()
		{
			storm_time = 0.0;
			storm_duration = 0.0;
			storm_generation = 0.0;
			storm_state = 0;
			msg_storm = 0;
		}

		public StormData(ConfigNode node)
		{
			storm_time = Lib.ConfigValue(node, "storm_time", 0.0);
			storm_duration = Lib.ConfigValue(node, "storm_duration", 0.0);
			storm_generation = Lib.ConfigValue(node, "storm_generation", 0.0);
			storm_state = Lib.ConfigValue(node, "storm_state", 0u);
			msg_storm = Lib.ConfigValue(node, "msg_storm", 0u);
		}

		public void Save(ConfigNode node)
		{
			node.AddValue("storm_time", storm_time);
			node.AddValue("storm_duration", storm_duration);
			node.AddValue("storm_generation", storm_generation);
			node.AddValue("storm_state", storm_state);
			node.AddValue("msg_storm", msg_storm);
		}

		public double storm_time;        // time of next storm
		public double storm_duration;    // duration of current/next storm
		public double storm_generation;  // time of next storm generation roll
		public uint storm_state;         // 0: none, 1: inbound, 2: inprogress
		public uint msg_storm;           // message flag
	}

} // KERBALISM
