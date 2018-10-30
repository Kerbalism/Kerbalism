using System;
using KSP.Localization;

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

			// get info from the cache
			Vessel_info vi = Cache.VesselInfo(v);

			// if not a valid vessel, leave the panel empty
			if (!vi.is_valid) return;

			// set metadata
			p.Title(Lib.BuildString(Lib.Ellipsis(v.vesselName, Styles.ScaleStringLength(40)), " <color=#cccccc>CONNECTION MANAGER</color>"));
			p.Width(Styles.ScaleWidthFloat(365.0f));
			p.paneltype = Panel.PanelType.connection;

			// time-out simulation
			if (p.Timeout(vi)) return;

			// draw ControlPath section
			p.AddSection("CONTROL PATH");
			if (Cache.VesselInfo(v).connection.linked)
			{
				if (RemoteTech.Enabled)
				{
					if (Cache.VesselInfo(v).connection.controlPath != null)
					{
						Guid i = v.id;
						foreach (Guid id in Cache.VesselInfo(v).connection.controlPath)
						{
							p.AddContent(
								Lib.Ellipsis(RemoteTech.GetSatelliteName(i) +" \\ " + RemoteTech.GetSatelliteName(id), 35),
								Lib.HumanReadablePerc(Math.Ceiling((1 - (RemoteTech.GetCommsDistance(i, id) / RemoteTech.GetCommsMaxDistance(i, id))) * 10000) / 10000, "F2"),
								"\nDistance: " + Lib.HumanReadableRange(RemoteTech.GetCommsDistance(i, id)) +
								"\nMax Distance: " + Lib.HumanReadableRange(RemoteTech.GetCommsMaxDistance(i, id)));
							i = id;
						}
					}
				}
				if (HighLogic.fetch.currentGame.Parameters.Difficulty.EnableCommNet)
				{
					p.AddContent(Lib.Ellipsis(Localizer.Format(v.connection.ControlPath.First.end.displayName).Replace("Kerbin", "DSN"), 20));
					//if (v.Connection.ControlPath.First.end.isHome);
				}
			}
			else p.AddContent("<i>no connection</i>", string.Empty);
		}
	}
}
