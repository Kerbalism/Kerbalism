using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	public abstract class KsmPartModule : PartModule
	{
		public const string VALUENAME_SHIPID = "dataShipId";
		public const string VALUENAME_FLIGHTID = "dataFlightId";

		[KSPField(isPersistant = true)]
		public int dataShipId = 0;

		[KSPField(isPersistant = true)]
		public int dataFlightId = 0;

		public abstract ModuleData ModuleData { get; set; }

		public abstract Type ModuleDataType { get; }
	}

	public class KsmPartModule<TModule, TData> : KsmPartModule
		where TModule : KsmPartModule<TModule, TData>
		where TData : ModuleData<TModule, TData>
	{
		public TData moduleData;

		public override ModuleData ModuleData { get => moduleData; set => moduleData = (TData)value; }

		public override Type ModuleDataType => typeof(TData);

		public void OnDestroy()
		{
			// clear loaded module reference to avoid memory leaks
			if (moduleData != null)
			{
				moduleData.loadedModule = null;
			}

		}
	}
}
