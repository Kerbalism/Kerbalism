using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	public class PartVirtualResource : VesselVirtualResource
	{
		public PartVirtualResource(string name) : base(name) { }

		// note : it would be more efficient to sync the new amount from VesselData,
		// doing a single loop over all parts...
		public override bool ExecuteAndSyncToParts(VesselDataBase vd, double elapsed_s)
		{
			base.ExecuteAndSyncToParts(vd, elapsed_s);

			foreach (PartData pd in vd.Parts)
			{
				foreach (PartResourceData partResource in pd.virtualResources)
				{
					if (partResource.Resource == this)
					{
						partResource.SetSyncedAmount(amount * (partResource.Capacity / capacity));
					}
				}
			}

			return false;
		}
	}
}
