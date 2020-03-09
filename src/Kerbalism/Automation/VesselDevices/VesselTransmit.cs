using KSP.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KERBALISM
{
	public class VesselDeviceTransmit : VesselDevice
	{
		public VesselDeviceTransmit(Vessel v, VesselData vd) : base(v, vd) { }

		public override string Name => Localizer.Format("#autoLOC_1200184"); // Transmissions

		public override string Status
		{
			get
			{
				if (vesselData.Hibernating)
				{
					return Lib.Color(Local.Generic_DISABLED, Lib.Kolor.Orange) + Lib.BuildString(" (", Localizer.Format("#autoLOC_217448"), ")"); // Hibernating
				}
				return Lib.Color(vesselData.deviceTransmit, Local.Generic_ENABLED, Lib.Kolor.Green, Local.Generic_DISABLED, Lib.Kolor.Yellow);
			}
		}

		public override void Ctrl(bool value)
		{
			if (!vesselData.Hibernating)
				vesselData.deviceTransmit = value;
		}

		public override void Toggle()
		{
			if (!vesselData.Hibernating)
				vesselData.deviceTransmit = !vesselData.deviceTransmit;
		}
	}
}
