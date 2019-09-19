using KSP.Localization;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{


	public static class FailuresManager
	{
		public static void Failman(this Panel p, Vessel v)
		{
			// avoid corner-case when this is called in a lambda after scene changes
			v = FlightGlobals.FindVessel(v.id);

			// if vessel doesn't exist anymore, leave the panel empty
			if (v == null) return;

			// get data
			VesselData vd = v.KerbalismData();

			// if not a valid vessel, leave the panel empty
			if (!vd.IsValid) return;

			// set metadata
			p.Title(Lib.BuildString(Lib.Ellipsis(v.vesselName, Styles.ScaleStringLength(20)), " ", Lib.Color("Quality Management", Lib.KColor.LightGrey)));
			p.Width(Styles.ScaleWidthFloat(355.0f));
			p.paneltype = Panel.PanelType.failures;

			string section = string.Empty;

			// get devices
			List<ReliabilityInfo> devices = vd.ReliabilityStatus();

			int deviceCount = 0;

			// for each device
			foreach (var ri in devices)
			{
				if(section != Group2Section(ri.group))
				{
					section = Group2Section(ri.group);
					p.AddSection(section);
				}

				string status = StatusString(ri);

				// render device entry
				p.AddContent(
					label: ri.title,
					value: status,
					hover: () => Highlighter.Set(ri.partId, Color.blue));
				deviceCount++;
			}

			// no devices case
			if (deviceCount == 0)
			{
				p.AddContent("<i>no quality info</i>");
			}
		}

		private static string Group2Section(string group)
		{
			if (string.IsNullOrEmpty(group)) return "Misc";
			return group;
		}

		private static string StatusString(ReliabilityInfo ri)
		{
			if (ri.broken)
			{
				if (ri.critical) return Lib.Color("busted", Lib.KColor.Red);
				return Lib.Color("needs repair", Lib.KColor.Orange);
			}
			if (ri.NeedsMaintenance())
			{
				return Lib.Color("needs service", Lib.KColor.Yellow);
			}

			if (ri.rel_duration > 0.75) return Lib.Color("operation duration", Lib.KColor.Yellow);
			if (ri.rel_ignitions > 0.95) return Lib.Color("ignition limit", Lib.KColor.Yellow);

			return Lib.Color("good", Lib.KColor.Green);
		}
	}


} // KERBALISM

