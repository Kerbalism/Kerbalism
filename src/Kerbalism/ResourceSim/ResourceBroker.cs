using System;
using System.Collections.Generic;

namespace KERBALISM
{
	public class ResourceBroker : IEquatable<ResourceBroker>
	{
		public enum BrokerCategory
		{
			Unknown,
			Generator,
			Converter,
			SolarPanel,
			Harvester,
			RTG,
			FuelCell,
			ECLSS,
			VesselSystem,
			Kerbal,
			Comms,
			Science,
			Radiator
		}

		private static Dictionary<string, ResourceBroker> brokersDict = new Dictionary<string, ResourceBroker>();
		private static List<ResourceBroker> brokersList = new List<ResourceBroker>();

		public static ResourceBroker Generic = GetOrCreate("Others", BrokerCategory.Unknown, Local.Brokers_Others);
		public static ResourceBroker SolarPanel = GetOrCreate("SolarPanel", BrokerCategory.SolarPanel, Local.Brokers_SolarPanel);
		public static ResourceBroker KSPIEGenerator = GetOrCreate("KSPIEGenerator", BrokerCategory.Generator, Local.Brokers_KSPIEGenerator);
		public static ResourceBroker FissionReactor = GetOrCreate("FissionReactor", BrokerCategory.Converter, Local.Brokers_FissionReactor);
		public static ResourceBroker RTG = GetOrCreate("RTG", BrokerCategory.RTG, Local.Brokers_RTG);
		public static ResourceBroker ScienceLab = GetOrCreate("ScienceLab", BrokerCategory.Science, Local.Brokers_ScienceLab);
		public static ResourceBroker Light = GetOrCreate("Light", BrokerCategory.VesselSystem, Local.Brokers_Light);
		public static ResourceBroker Boiloff = GetOrCreate("Boiloff", BrokerCategory.VesselSystem, Local.Brokers_Boiloff);
		public static ResourceBroker Cryotank = GetOrCreate("Cryotank", BrokerCategory.VesselSystem, Local.Brokers_Cryotank);
		public static ResourceBroker Greenhouse = GetOrCreate("Greenhouse", BrokerCategory.VesselSystem, Local.Brokers_Greenhouse);
		public static ResourceBroker Experiment = GetOrCreate("ModuleKsmExperiment", BrokerCategory.Science, Local.Brokers_Experiment);
		public static ResourceBroker Command = GetOrCreate("Command", BrokerCategory.VesselSystem, Local.Brokers_Command);
		public static ResourceBroker GravityRing = GetOrCreate("GravityRing", BrokerCategory.RTG, Local.Brokers_GravityRing);
		public static ResourceBroker Scanner = GetOrCreate("Scanner", BrokerCategory.VesselSystem, Local.Brokers_Scanner);
		public static ResourceBroker Laboratory = GetOrCreate("Laboratory", BrokerCategory.Science, Local.Brokers_Laboratory);
		public static ResourceBroker CommsIdle = GetOrCreate("CommsIdle", BrokerCategory.Comms, Local.Brokers_CommsIdle);
		public static ResourceBroker CommsXmit = GetOrCreate("CommsXmit", BrokerCategory.Comms, Local.Brokers_CommsXmit);
		public static ResourceBroker StockConverter = GetOrCreate("StockConverter", BrokerCategory.Converter, Local.Brokers_StockConverter);
		public static ResourceBroker StockDrill = GetOrCreate("Converter", BrokerCategory.Harvester, Local.Brokers_StockDrill);
		public static ResourceBroker Harvester = GetOrCreate("Harvester", BrokerCategory.Harvester, Local.Brokers_Harvester);
		public static ResourceBroker Radiator = GetOrCreate("Radiator", BrokerCategory.Radiator, Local.Brokers_Radiator);
		public static ResourceBroker Habitat = GetOrCreate("Habitat", BrokerCategory.VesselSystem, Local.Habitat);
		public static ResourceBroker Environment = GetOrCreate("Environment", BrokerCategory.Unknown, Local.Sensor_environment);
		public static ResourceBroker Wheel = GetOrCreate("Wheel", BrokerCategory.VesselSystem, "wheel");
		public static ResourceBroker Engine = GetOrCreate("Engine", BrokerCategory.VesselSystem, "engine");
		public static ResourceBroker PassiveShield = GetOrCreate("PassiveShield", BrokerCategory.VesselSystem, "passive shield");

		public string Id { get; private set; }
		public BrokerCategory Category { get; private set; }
		public string Title { get; private set; }
		public string[] BrokerInfo { get; private set; }

		public override int GetHashCode() => hashcode;
		private int hashcode;

		private ResourceBroker(string id, BrokerCategory category = BrokerCategory.Unknown, string title = null)
		{
			Id = id;
			Category = category;

			if (string.IsNullOrEmpty(title))
				Title = id;
			else
				Title = title;

			BrokerInfo = new string[] { Category.ToString(), Id, Title };

			hashcode = id.GetHashCode();

			brokersDict.Add(id, this);
			brokersList.Add(this);
		}

		public static IEnumerator<ResourceBroker> List()
		{
			return brokersList.GetEnumerator();
		}

		public static ResourceBroker GetOrCreate(string id)
		{
			ResourceBroker rb;
			if (brokersDict.TryGetValue(id, out rb))
				return rb;

			return new ResourceBroker(id, BrokerCategory.Unknown, id);
		}

		public static ResourceBroker GetOrCreate(string id, BrokerCategory type, string title)
		{
			ResourceBroker rb;
			if (brokersDict.TryGetValue(id, out rb))
				return rb;

			return new ResourceBroker(id, type, title);
		}

		public static string GetTitle(string id)
		{
			ResourceBroker rb;
			if (brokersDict.TryGetValue(id, out rb))
				return rb.Title;
			return null;
		}

		public bool Equals(ResourceBroker other)
		{
			return other != null && Id == other.Id;
		}

		public override bool Equals(object obj) => Equals((ResourceBroker)obj);

		public override string ToString() => Title;
	}

	public class ResourceBrokerRate : IEquatable<ResourceBrokerRate>
	{
		public ResourceBroker broker;
		public double rate;
		public ResourceBrokerRate(ResourceBroker broker, double amount)
		{
			this.broker = broker;
			this.rate = amount;
		}

		public override int GetHashCode() => broker.GetHashCode();
		public bool Equals(ResourceBrokerRate other) => other != null && broker.Equals(other.broker);
		public override bool Equals(object obj) => Equals((ResourceBrokerRate)obj);
		public override string ToString() => broker.ToString() + ": " + rate.ToString("0.000000") + "/s";
	}
}
