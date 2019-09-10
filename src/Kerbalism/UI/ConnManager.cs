
namespace KERBALISM
{
	public static class ConnManager
	{
		/// <summary>
		/// Shows the Network status, ControlPath, Signal strength
		/// </summary>
		public static void ConnMan(this Panel p, Vessel v)
		{
			// avoid corner-case when this is called in a lambda after scene changes
			v = FlightGlobals.FindVessel(v.id);

			// if vessel doesn't exist anymore, leave the panel empty
			if (v == null) return;

			VesselData vd = v.KerbalismData();

			// if not a valid vessel, leave the panel empty
			if (!vd.IsValid) return;

			// set metadata
			p.Title(Lib.BuildString(Lib.Ellipsis(v.vesselName, Styles.ScaleStringLength(40)), " ", Lib.Color("CONNECTION MANAGER", Lib.KColor.LightGrey)));
			p.Width(Styles.ScaleWidthFloat(365.0f));
			p.paneltype = Panel.PanelType.connection;

			// time-out simulation
			if (!Lib.IsControlUnit(v) &&  p.Timeout(vd)) return;

			// draw ControlPath section
			p.AddSection("CONTROL PATH");
			if (vd.Connection.linked)
			{
				if(vd.Connection.control_path != null)
				{
					foreach(string[] hop in vd.Connection.control_path)
					{
						if (hop == null || hop.Length < 1) continue;
						string name = hop[0];
						string value = hop.Length >= 2 ? hop[1] : "";
						string tooltip = hop.Length >= 3 ? ("\n" + hop[2]) : "";
						p.AddContent(name, value, tooltip);
					}
				}
			}
			else p.AddContent("<i>no connection</i>", string.Empty);
		}
	}
}
