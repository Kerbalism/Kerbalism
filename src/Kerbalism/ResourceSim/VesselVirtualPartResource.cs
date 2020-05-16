using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	/// <summary>
	/// A VesselVirtualPartResource is functionaly equivalent to a massless and costless KSP resource. <br/>
	/// Per-part amount/capacity is stored and persisted in PartResourceData objects, and synchronization <br/>
	/// is done in the same way as for a normal KSP resource (reads : parts -> handler, writes : handler -> parts) <br/>
	/// For specific purposes, the per-part amount/capacity can be set directly. <br/>
	/// Currently doesn't support per-part flow locking, but can be implemented if necessary
	/// </summary>
	public class VesselVirtualPartResource : VesselResource
	{
		public override string Name => Definition.name;

		public override string Title => Definition.title;

		public override bool Visible => Definition.isVisible;

		public override double Amount => resourceWrapper.amount;

		public override double Capacity => resourceWrapper.capacity;

		public override bool NeedUpdate => true;

		public bool IsValid => Definition != null;

		public VirtualResourceDefinition Definition { get; private set; }

		/// <summary> Don't use this directly, use the VesselResHandler.CreateVirtualPartResource() method </summary>
		public VesselVirtualPartResource(VirtualResourceWrapper resourceWrapper, VirtualResourceDefinition definition)
		{
			this.Definition = definition;
			this.resourceWrapper = resourceWrapper;
			resourceBrokers = new List<ResourceBrokerRate>();
			brokersResourceAmounts = new Dictionary<ResourceBroker, double>();
		}

		/// <summary> Don't use this directly, use the VesselResHandler.CreateVirtualPartResource() method </summary>
		public VesselVirtualPartResource(VirtualResourceWrapper resourceWrapper, string name, bool isVisible = false, string title = null)
		{
			Definition = VirtualResourceDefinition.GetOrCreateDefinition(name, isVisible, VirtualResourceDefinition.ResType.PartResource, title);
			this.resourceWrapper = resourceWrapper;
			resourceBrokers = new List<ResourceBrokerRate>();
			brokersResourceAmounts = new Dictionary<ResourceBroker, double>();
		}
	}
}
