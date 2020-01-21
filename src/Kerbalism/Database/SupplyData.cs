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
		public List<ResourceBrokerRate> ResourceBrokers { get; private set; } = new List<ResourceBrokerRate>();
		public static string LocAveragecache;

		public class ResourceBrokerRate
		{
			public ResourceBroker broker;
			public double rate;
			public ResourceBrokerRate(ResourceBroker broker, double amount)
			{
				this.broker = broker;
				this.rate = amount;
			}
		}

		public void UpdateResourceBrokers(Dictionary<ResourceBroker, double> brokersResAmount, Dictionary<ResourceBroker, double> ruleBrokersRate, double unsupportedBrokersRate, double elapsedSeconds)
		{
			ResourceBrokers.Clear();

			foreach (KeyValuePair<ResourceBroker, double> p in ruleBrokersRate)
			{
				ResourceBroker broker = ResourceBroker.GetOrCreate(p.Key.Id + "Avg", p.Key.Category, Lib.BuildString(p.Key.Title, " (", Local.Generic_AVERAGE, ")"));
				ResourceBrokers.Add(new ResourceBrokerRate(broker, p.Value));
			}
			foreach (KeyValuePair<ResourceBroker, double> p in brokersResAmount)
			{
				ResourceBrokers.Add(new ResourceBrokerRate(p.Key, p.Value / elapsedSeconds));
			}
			if (unsupportedBrokersRate != 0.0)
			{
				ResourceBrokers.Add(new ResourceBrokerRate(ResourceBroker.Generic, unsupportedBrokersRate)); 
			}
		}
	}



} // KERBALISM
