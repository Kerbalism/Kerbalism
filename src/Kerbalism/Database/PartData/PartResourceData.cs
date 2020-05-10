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

		private VesselVirtualPartResource resource;
		public VesselVirtualPartResource Resource => resource;

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

		public double Level => capacity > 0.0 ? amount / capacity : 0.0;

		public PartResourceData(VesselVirtualPartResource vesselVirtualPartResource, double amount = 0.0, double capacity = 0.0)
		{
			resource = vesselVirtualPartResource;
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
				VesselVirtualPartResource res = pd.vesselData.ResHandler.CreateVirtualResource<VesselVirtualPartResource>(node.name);

				if (res == null)
					continue;

				pd.virtualResources.AddResource(res,
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
				ConfigNode resNode = resTopNode.AddNode(res.resource.Name);
				resNode.AddValue("amount", res.amount);
				resNode.AddValue("capacity", res.capacity);
			}
			return true;
		}
	}
}
