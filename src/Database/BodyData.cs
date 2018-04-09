using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{


	public class BodyData
	{
		public BodyData()
		{
			storm_time = 0.0;
			storm_age = 0.0;
			storm_state = 0;
			msg_storm = 0;
		}

		public BodyData(ConfigNode node)
		{
			storm_time = Lib.ConfigValue(node, "storm_time", 0.0);
			storm_age = Lib.ConfigValue(node, "storm_age", 0.0);
			storm_state = Lib.ConfigValue(node, "storm_state", 0u);
			msg_storm = Lib.ConfigValue(node, "msg_storm", 0u);
		}

		public void save(ConfigNode node)
		{
			node.AddValue("storm_time", storm_time);
			node.AddValue("storm_age", storm_age);
			node.AddValue("storm_state", storm_state);
			node.AddValue("msg_storm", msg_storm);
		}

		public double storm_time;   // time of next storm
		public double storm_age;    // time since last storm
		public uint storm_state;  // 0: none, 1: inbound, 2: inprogress
		public uint msg_storm;    // message flag
	}


} // KERBALISM



