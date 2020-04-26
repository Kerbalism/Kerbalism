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

		private PartVirtualResource resource;
		public PartVirtualResource Resource => resource;

		private double amount;
		public double Amount
		{
			get => amount;
			set
			{
				double newAmount = Math.Min(value, capacity);
				double diff = newAmount - amount;
				amount = newAmount;
				resource.SetAmount(Resource.Amount + diff);
			}
		}

		private double capacity;
		public double Capacity
		{
			get => capacity;
			set
			{
				double diff = value - capacity;
				capacity = value;
				resource.SetCapacity(Resource.Capacity + diff);
				amount = Math.Min(amount, capacity);
			}
		}

		public void SetSyncedAmount(double amount)
		{
			this.amount = amount;
		}

		public PartResourceData(VesselDataBase vd, string name, double amount, double capacity)
		{
			if (!vd.ResHandler.TryGetPartVirtualResource(name, out resource))
			{
				resource = new PartVirtualResource(name);
				resource.SetCapacity(0.0);
				vd.ResHandler.AddPartVirtualResource(resource);
			}
				
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
				PartResourceData res = new PartResourceData(
					pd.vesselData,
					node.name,
					Lib.ConfigValue(node, "amount", 0.0),
					Lib.ConfigValue(node, "capacity", 0.0));

				pd.virtualResources.Add(res);
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

		public void Remove()
		{
			Resource.SetAmount(Resource.Amount - amount);
			Resource.SetCapacity(Resource.Capacity - capacity);
		}
	}
}
