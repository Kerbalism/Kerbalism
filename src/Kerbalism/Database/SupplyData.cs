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
		public List<ResourceBroker> ResourceBrokers { get; private set; } = new List<ResourceBroker>();

		public struct ResourceBroker
		{
			public string name;
			public double rate;
			public ResourceBroker(string name, double amount)
			{
				this.name = name;
				this.rate = amount;
			}
		}

		public void UpdateResourceBrokers(Dictionary<string, double> brokers, Dictionary<string, double> ruleBrokers, double unsupportedBrokersRate, double elapsedSeconds)
		{
			ResourceBrokers.Clear();

			foreach (KeyValuePair<string, double> p in ruleBrokers)
			{
				ResourceBrokers.Add(new ResourceBroker(Lib.BuildString(p.Key, " (avg.)"), p.Value));
			}

			foreach (KeyValuePair<string, double> p in brokers)
			{
				ResourceBrokers.Add(new ResourceBroker(p.Key, p.Value / elapsedSeconds));
			}
			if (unsupportedBrokersRate != 0.0)
			{
				ResourceBrokers.Add(new ResourceBroker("unknown", unsupportedBrokersRate)); 
			}
		}
	}



} // KERBALISM
