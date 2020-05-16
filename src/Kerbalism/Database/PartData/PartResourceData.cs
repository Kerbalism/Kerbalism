using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	public class PartResourceData
	{
		public const string NODENAME_RESOURCES = "RESOURCES";

		public string ResourceName { get; private set; }

		public int? resourceId = null;

		private double amount;
		public double Amount
		{
			get => amount;
			set
			{
				amount = value < 0.0 ? 0.0 : value > capacity ? capacity : value;
			}
		}

		private double capacity;
		public double Capacity
		{
			get => capacity;
			set
			{
				if (value < 0.0)
					value = 0.0;

				if (value > amount)
					amount = value;

				capacity = value;
			}
		}

		public bool flowState = true;

		public double Level => capacity > 0.0 ? amount / capacity : 0.0;

		public PartResourceData(string resourceName, double amount = 0.0, double capacity = 0.0)
		{
			ResourceName = resourceName;
			Capacity = capacity;
			Amount = amount;
		}

		public static void LoadPartResources(PartData pd, ConfigNode partDataNode)
		{
			ConfigNode resTopNode = partDataNode.GetNode(NODENAME_RESOURCES);
			if (resTopNode == null)
				return;

			foreach (ConfigNode node in resTopNode.nodes)
			{
				pd.virtualResources.AddResource(node.name,
					Lib.ConfigValue(node, "amount", 0.0),
					Lib.ConfigValue(node, "capacity", 0.0));
			}
		}

		public static bool SavePartResources(PartData pd, ConfigNode partDataNode)
		{
			if (pd.virtualResources.Count == 0)
				return false;

			ConfigNode resTopNode = partDataNode.AddNode(NODENAME_RESOURCES);

			foreach (PartResourceData res in pd.virtualResources)
			{
				ConfigNode resNode = resTopNode.AddNode(res.ResourceName);
				resNode.AddValue("amount", res.amount);
				resNode.AddValue("capacity", res.capacity);
			}
			return true;
		}
	}
}
