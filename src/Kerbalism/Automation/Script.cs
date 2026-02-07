using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;


namespace KERBALISM
{


	public sealed class Script
	{
		public Script()
		{
			states = new Dictionary<uint, bool>();
			values = new Dictionary<uint, double>();
			prev = string.Empty;
		}

		public Script(ConfigNode node)
		{
			values = new Dictionary<uint, double>();
			states = new Dictionary<uint, bool>();
			foreach (string s in node.GetValues("state"))
			{
				var tokens = Lib.Tokenize(s, '@');
				if (tokens.Count != 2) continue;
				states.Add(Lib.Parse.ToUInt(tokens[0]), Lib.Parse.ToBool(tokens[1]));
			}
			foreach (string s in node.GetValues("value"))
			{
				var tokens = Lib.Tokenize(s, '@');
				if (tokens.Count != 2) continue;
				values.Add(Lib.Parse.ToUInt(tokens[0]), Lib.Parse.ToDouble(tokens[1]));
			}
			prev = Lib.ConfigValue(node, "prev", string.Empty);
		}

		public void Save(ConfigNode node)
		{
			foreach (var p in states)
			{
				node.AddValue("state", Lib.BuildString(p.Key.ToString(), "@", p.Value.ToString()));
			}
			foreach (var p in values)
			{
				node.AddValue("value", Lib.BuildString(p.Key.ToString(), "@", p.Value.ToString()));
			}
			node.AddValue("prev", prev);
		}

		public void SetState(Device dev, bool? state)
		{
			states.Remove(dev.Id);
			if (state != null)
			{
				states.Add(dev.Id, state == true);
			}
		}

		public void SetValue(uint id, double value)
		{
			values.Remove(id);
			values?.Add(id, value);
		}

		public void Execute(List<Device> devices)
		{
			foreach (var p in states)
			{
				foreach (Device device in devices)
				{
					if (device.Id == p.Key)
					{
						device.Ctrl(p.Value);
					}
				}
			}
		}


		public Dictionary<uint, bool> states;
		public Dictionary<uint, double> values;
		public string prev;
	}



} // KERBALISM

