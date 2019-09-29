using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{

	public class StormData
	{
		public StormData()
		{
			storm_generation = 0.0;
			Reset();
		}

		public void Reset()
		{
			storm_time = 0.0;
			storm_duration = 0.0;
			storm_state = 0;
			msg_storm = 0;
			displayed_duration = 0;
			display_warning = true;
		}

		public StormData(ConfigNode node)
		{
			storm_time = Lib.ConfigValue(node, "storm_time", 0.0);
			storm_duration = Lib.ConfigValue(node, "storm_duration", 0.0);
			storm_generation = Lib.ConfigValue(node, "storm_generation", 0.0);
			storm_state = Lib.ConfigValue(node, "storm_state", 0u);
			msg_storm = Lib.ConfigValue(node, "msg_storm", 0u);
			displayed_duration = Lib.ConfigValue(node, "displayed_duration", storm_duration);
			display_warning = Lib.ConfigValue(node, "display_warning", true);

			if(storm_time > 0 && storm_duration <= 0) // legacy save games did storms differently.
			{
				// storm_time used to be the point of time of the next storm, there was no storm_duration
				// as storms all had a fixed duration depending on the distance to the sun.
				// setting this to 0 will trigger a reroll of storm generation based on the new implementation.
				storm_time = 0;
				storm_generation = 0;
			}
		}

		public void Save(ConfigNode node)
		{
			node.AddValue("storm_time", storm_time);
			node.AddValue("storm_duration", storm_duration);
			node.AddValue("storm_generation", storm_generation);
			node.AddValue("storm_state", storm_state);
			node.AddValue("msg_storm", msg_storm);
			node.AddValue("displayed_duration", displayed_duration);
			node.AddValue("display_warning", display_warning);
		}

		public double storm_time;        // time of next storm
		public double storm_duration;    // duration of current/next storm
		public double storm_generation;  // time of next storm generation roll
		public uint storm_state;         // 0: none, 1: inbound, 2: inprogress
		public uint msg_storm;           // message flag

		public double displayed_duration;
		public bool display_warning;

	}

} // KERBALISM
