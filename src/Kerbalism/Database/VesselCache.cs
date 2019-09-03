using System;
using System.Collections.Generic;

namespace KERBALISM
{
	public class VesselCache
	{
		private Vessel vessel;

		public VesselCache(Vessel vessel)
		{
			this.vessel = vessel;
		}

		public void Clear()
		{
			emitters = null;
			RadiationSunShieldingParts = null;
		}

		private List<Emitter> emitters;
		internal List<Emitter> Emitters()
		{
			if (emitters != null) return emitters;
			emitters = Lib.FindModules<Emitter>(vessel);
			return emitters;
		}

		public List<Radiation.SunShieldingInfo> RadiationSunShieldingParts { get; internal set; }
	}
}
