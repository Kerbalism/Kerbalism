using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	public interface IVolumeAndSurfaceModule
	{
		/// <summary>
		/// If the part is deployable / animated, set it to the deployed state in this method
		/// in order for the volume/surface evaluation to use the correct model state.
		/// </summary>
		void SetupPrefabPartModel();

		/// <summary>
		/// Called on the prefab, just after the part has been compiled
		/// </summary>
		void GetVolumeAndSurfaceResults(PartVolumeAndSurface.Info result);
	}
}
