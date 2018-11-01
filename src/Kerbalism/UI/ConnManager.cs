using System;
using CommNet;
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
			if (vi.connection.linked)
			{
				if (RemoteTech.Enabled)
				{
					if (vi.connection.controlPath != null)
					{
						Guid i = v.id;
						foreach (Guid id in vi.connection.controlPath)
						{
							p.AddContent(
								Lib.Ellipsis(RemoteTech.GetSatelliteName(i) + " \\ " + RemoteTech.GetSatelliteName(id), 35),
								Lib.HumanReadablePerc(Math.Ceiling((1 - (RemoteTech.GetCommsDistance(i, id) / RemoteTech.GetCommsMaxDistance(i, id))) * 10000) / 10000, "F2"),
								"\nDistance: " + Lib.HumanReadableRange(RemoteTech.GetCommsDistance(i, id)) +
								"\nMax Distance: " + Lib.HumanReadableRange(RemoteTech.GetCommsMaxDistance(i, id)));
							i = id;
						}
					}
				}
				if (HighLogic.fetch.currentGame.Parameters.Difficulty.EnableCommNet)
				{
					foreach (CommLink link in v.connection.ControlPath)
					{
						double antennaPower = link.end.isHome ? link.start.antennaTransmit.power + link.start.antennaRelay.power : link.start.antennaTransmit.power;
						double signalStrength = 1 - ((link.start.position - link.end.position).magnitude / Math.Sqrt(antennaPower * link.end.antennaRelay.power));

						signalStrength = (3 - (2 * signalStrength)) * Math.Pow(signalStrength, 2);

						p.AddContent(
							Lib.Ellipsis(Localizer.Format(link.end.name).Replace("Kerbin", "DSN"), 35),
							Lib.HumanReadablePerc(Math.Ceiling(signalStrength * 10000) / 10000, "F2"),
							"\nDistance: " + Lib.HumanReadableRange((link.start.position - link.end.position).magnitude) +
							"\nMax Distance: " + Lib.HumanReadableRange(Math.Sqrt((link.start.antennaTransmit.power + link.start.antennaRelay.power) * link.end.antennaRelay.power))
							);
					}
				}
			}
			else p.AddContent("<i>no connection</i>", string.Empty);
		}
	}
}
