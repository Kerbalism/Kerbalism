using KSP.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KERBALISM
{
	public class VesselProcessDevice : VesselDevice
	{
		public VesselProcessData vesselProcess { get; private set; }

		public VesselProcessDevice(Vessel v, VesselData vd, VesselProcessData process) : base(v, vd)
		{
			vesselProcess = process;
		}

		public override string Name => vesselProcess.process.title;

		public override string Tooltip => vesselProcess.Description();

		public override string Status
		{
			get
			{
				return Lib.Color(vesselProcess.enabled, Local.Generic_ENABLED, Lib.Kolor.Green, Local.Generic_DISABLED, Lib.Kolor.Yellow);
			}
		}

		public override void Ctrl(bool value)
		{
			vesselProcess.SetEnabled(value, vesselData.ResHandler);
		}

		public override void Toggle()
		{
			Ctrl(!vesselProcess.enabled);
		}
	}
}
