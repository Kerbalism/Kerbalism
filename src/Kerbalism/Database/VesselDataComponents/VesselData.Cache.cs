using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
    public partial class VesselDataBase
    {
		public PartsCache PartCache { get; private set; } = new PartsCache();

        public class PartsCache
		{
			public List<PartRadiationData> RadiationOccluders { get; private set; } = new List<PartRadiationData>();

			public List<RadiationCoilData> RadiationArrays { get; private set; } = new List<RadiationCoilData>();

            public List<IRadiationEmitter> RadiationEmitters { get; private set; } = new List<IRadiationEmitter>();

            public void Update(VesselDataBase vd)
			{
				RadiationOccluders.Clear();
				RadiationArrays.Clear();
				RadiationEmitters.Clear();

				foreach (PartData partData in vd.Parts)
				{
					if (partData.radiationData.IsOccluder)
						RadiationOccluders.Add(partData.radiationData);

					foreach (ModuleData moduleData in partData.modules)
					{
						if (moduleData is RadiationCoilData coilData && coilData.effectData != null)
						{
							RadiationArrays.Add(coilData);
							continue;
						}

						if (moduleData is IRadiationEmitter emitter)
						{
							RadiationEmitters.Add(emitter);
							continue;
						}
					}
				}
			}
        }
    }
}
