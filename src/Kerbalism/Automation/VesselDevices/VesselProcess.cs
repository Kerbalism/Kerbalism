using KSP.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KERBALISM
{
	public class VesselProcessDevice : VesselDevice
	{
		private readonly DeviceIcon icon;
		private readonly VesselProcessData vesselProcess;

		public VesselProcessDevice(Vessel v, VesselData vd, VesselProcessData process) : base(v, vd)
		{
			vesselProcess = process;
			icon = new DeviceIcon(Textures.wrench_white, "open process window", () => new ProcessPopup(vesselProcess, vd));
		}

		public override DeviceIcon Icon => icon;

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
