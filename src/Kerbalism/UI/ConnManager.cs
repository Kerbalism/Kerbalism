using System;
using KSP.Localization;
using UnityEngine;
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
			if (!vd.IsSimulated) return;

			// set metadata
			p.Title(Lib.BuildString(Lib.Ellipsis(v.vesselName, Styles.ScaleStringLength(40)), " ", Lib.Color(Local.ConnManager_title, Lib.Kolor.LightGrey)));//"CONNECTION MANAGER"
			p.Width(Styles.ScaleWidthFloat(365.0f));
			p.paneltype = Panel.PanelType.connection;

			// time-out simulation
			if (!Lib.IsControlUnit(v) && p.Timeout(vd)) return;

			// draw ControlPath section
			p.AddSection(Local.ConnManager_CONTROLPATH);//"CONTROL PATH"
			if (vd.Connection.linked)
			{
				var currentName = Lib.Ellipsis(Localizer.Format(v.GetDisplayName()), 35);
				var current = vd.Connection;
				// The code producing the connections is not tick accurate,
				// there can be transient states that create cycles, so limit to 16 hops max to avoid infinite loops.
				for (int i = 0; current != null && i < 16; i++)
				{
					var nextHopName = "DSN";
					ConnectionInfo nextConnection = null;
					if (current.next_hop != Guid.Empty)
					{
						Vessel nextVessel = FlightGlobals.FindVessel(current.next_hop);
						if (nextVessel != null)
						{
							nextHopName = Lib.Ellipsis(Localizer.Format(nextVessel.GetDisplayName()), 35);
							if (nextVessel.TryGetVesselDataTemp(out var nextVesselData))
							{
								nextConnection = nextVesselData.Connection;
							}
						}
					}

					// HSV lerp since RGB lerps are not gamma correct (HSV keep constant perceived brightness).
					float hue = Mathf.Lerp(0.0f, 0.33f, Mathf.Clamp01((float)current.strength)); // red to green

					// Slightly muted saturation/value
					float saturation = 0.85f;
					float value = 0.9f;

					var code = ColorUtility.ToHtmlStringRGB(Color.HSVToRGB(hue, saturation, value));

					var hop = Lib.BuildString(currentName, " ➡ ", nextHopName);
					var speed = Lib.BuildString("<color=#", code, ">", Lib.HumanReadableDataRate(current.hop_datarate), "</color>");
					var hover = Lib.BuildString(
						"Strength: <color=#", code, ">", Lib.HumanReadablePerc(Math.Ceiling(current.strength * 10000) / 10000, "F2"), "</color>\n",
						"Distance: ", Lib.HumanReadableDistance(current.hop_distance),
						" (Max: ", Lib.HumanReadableDistance(current.hop_max_distance), ")"
					);
					p.AddContent(hop, speed, hover);

					currentName = nextHopName;
					current = nextConnection;
				}
			}
			else p.AddContent("<i>" + Local.ConnManager_noconnection + "</i>", string.Empty);//no connection
		}
	}
}
