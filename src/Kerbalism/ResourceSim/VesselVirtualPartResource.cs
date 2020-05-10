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
		public override string Name => name;
		private string name;

		public override string Title => name;

		public override bool Visible => false;

		// Note : managing "metadata" about the resource is problematic from a persistence POV
		// and since for now we don't really need it, I'm not gonna bother with that.
		//public override string Title => title;
		//private string title;

		//public override bool Visible => visible;
		//private bool visible;

		public override double Amount => resourceWrapper.amount;

		public override double Capacity => resourceWrapper.capacity;

		public override bool NeedUpdate => true;

		/// <summary> Don't use this directly, use the VesselResHandler.CreateVirtualPartResource() method </summary>
		public VesselVirtualPartResource(VirtualResourceWrapper resourceWrapper)
		{
			this.resourceWrapper = resourceWrapper;
			name = resourceWrapper.name;
			resourceBrokers = new List<ResourceBrokerRate>();
			brokersResourceAmounts = new Dictionary<ResourceBroker, double>();
			//this.title = title;
			//this.visible = visible;
		}
	}
}
