using KSP.Localization;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{


	public static class DevManager
	{
		public static void Devman(this Panel p, Vessel v)
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
			p.Title(Lib.BuildString(Lib.Ellipsis(v.vesselName, Styles.ScaleStringLength(20)), " <color=#cccccc>" + Localizer.Format("#KERBALISM_UI_devman") + "</color>"));
			p.Width(Styles.ScaleWidthFloat(355.0f));
			p.paneltype = Panel.PanelType.scripts;

			// time-out simulation
			if (p.Timeout(vi)) return;

			// get devices
			Dictionary<uint, Device> devices = Computer.Boot(v);
			int deviceCount = 0;

			// direct control
			if (script_index == 0)
			{
				// draw section title and desc
				p.AddSection
				(
				  Localizer.Format("#KERBALISM_UI_devices"),
				  Description(),
				  () => p.Prev(ref script_index, (int)ScriptType.last),
				  () => p.Next(ref script_index, (int)ScriptType.last),
					 true
				);

				// for each device
				foreach (var pair in devices)
				{
					// render device entry
					Device dev = pair.Value;
					if (!dev.IsVisible()) continue;
					p.AddContent(dev.Name(), dev.Info(), string.Empty, dev.Toggle, () => Highlighter.Set(dev.Part(), Color.cyan));
					deviceCount++;
				}
			}
			// script editor
			else
			{
				// get script
				ScriptType script_type = (ScriptType)script_index;
				string script_name = script_type.ToString().Replace('_', ' ').ToUpper();
				Script script = DB.Vessel(v).computer.Get(script_type);

				// draw section title and desc
				p.AddSection
				(
				  script_name,
				  Description(),
				  () => p.Prev(ref script_index, (int)ScriptType.last),
				  () => p.Next(ref script_index, (int)ScriptType.last),
					 true
				);

				// for each device
				foreach (var pair in devices)
				{
					Device dev = pair.Value;
					if (!dev.IsVisible()) continue;

					// determine tribool state
					int state = !script.states.ContainsKey(pair.Key)
					  ? -1
					  : !script.states[pair.Key]
					  ? 0
					  : 1;

					// render device entry
					p.AddContent
					(
					  dev.Name(),
					  state == -1 ? "<color=#999999>" + Localizer.Format("#KERBALISM_UI_dontcare") + " </color>" : state == 0 ? "<color=red>" + Localizer.Format("#KERBALISM_Generic_OFF") + "</color>" : "<color=cyan>" + Localizer.Format("#KERBALISM_Generic_ON") + "</color>",
					  string.Empty,
					  () =>
					  {
						  switch (state)
						  {
							  case -1: script.Set(dev, true); break;
							  case 0: script.Set(dev, null); break;
							  case 1: script.Set(dev, false); break;
						  }
					  },
					  () => Highlighter.Set(dev.Part(), Color.cyan)
					);
					deviceCount++;
				}
			}

			// no devices case
			if (deviceCount == 0)
			{
				p.AddContent("<i>no devices</i>");
			}
		}

		// return short description of a script, or the time-out message
		static string Description()
		{
			if (script_index == 0) return "<i>Control vessel components directly</i>";
			switch ((ScriptType)script_index)
			{
				case ScriptType.landed: return Localizer.Format("#Kerbalism_UI_1000000");        // #Kerbalism_UI_1000000 = <i>Called on landing</i>
				case ScriptType.atmo: return Localizer.Format("#Kerbalism_UI_1000001");          // #Kerbalism_UI_1000001 = <i>Called on entering atmosphere</i>
				case ScriptType.space: return Localizer.Format("#Kerbalism_UI_1000002");         // #Kerbalism_UI_1000002 = <i>Called on reaching space</i>
				case ScriptType.sunlight: return Localizer.Format("#Kerbalism_UI_1000003");      // #Kerbalism_UI_1000003 = <i>Called when sun became visible</i>
				case ScriptType.shadow: return Localizer.Format("#Kerbalism_UI_1000004");        // #Kerbalism_UI_1000004 = <i>Called when sun became occluded</i>
				case ScriptType.power_high: return Localizer.Format("#Kerbalism_UI_1000005");    // #Kerbalism_UI_1000005 = <i>Called when EC level goes above 80%</i>
				case ScriptType.power_low: return Localizer.Format("#Kerbalism_UI_1000006");     // #Kerbalism_UI_1000006 = <i>Called when EC level goes below 20%</i>
				case ScriptType.rad_high: return Localizer.Format("#Kerbalism_UI_1000007");      // #Kerbalism_UI_1000007 = <i>Called when radiation exceed 0.05 rad/h</i>
				case ScriptType.rad_low: return Localizer.Format("#Kerbalism_UI_1000008");       // #Kerbalism_UI_1000008 = <i>Called when radiation goes below 0.02 rad/h</i>
				case ScriptType.linked: return Localizer.Format("#Kerbalism_UI_1000009");        // #Kerbalism_UI_1000009 = <i>Called when signal is regained</i>
				case ScriptType.unlinked: return Localizer.Format("#Kerbalism_UI_1000010");      // #Kerbalism_UI_1000010 = <i>Called when signal is lost</i>
				case ScriptType.eva_out: return Localizer.Format("#Kerbalism_UI_1000011");       // #Kerbalism_UI_1000011 = <i>Called when going out on EVA</i>
				case ScriptType.eva_in: return Localizer.Format("#Kerbalism_UI_1000012");        // #Kerbalism_UI_1000012 = <i>Called when returning from EVA</i>
				case ScriptType.action1: return Localizer.Format("#Kerbalism_UI_1000013");       // #Kerbalism_UI_1000013 = <i>Called by pressing <b>1</b> on the keyboard</i>
				case ScriptType.action2: return Localizer.Format("#Kerbalism_UI_1000014");       // #Kerbalism_UI_1000014 = <i>Called by pressing <b>2</b> on the keyboard</i>
				case ScriptType.action3: return Localizer.Format("#Kerbalism_UI_1000015");       // #Kerbalism_UI_1000015 = <i>Called by pressing <b>3</b> on the keyboard</i>
				case ScriptType.action4: return Localizer.Format("#Kerbalism_UI_1000016");       // #Kerbalism_UI_1000016 = <i>Called by pressing <b>4</b> on the keyboard</i>
				case ScriptType.action5: return Localizer.Format("#Kerbalism_UI_1000017");       // #Kerbalism_UI_1000017 = <i>Called by pressing <b>5</b> on the keyboard</i>
				case ScriptType.drive_full: return Localizer.Format("#Kerbalism_UI_1000018");
				case ScriptType.drive_empty: return Localizer.Format("#Kerbalism_UI_1000019");
			}
			return string.Empty;
		}

		// mode/script index
		static int script_index;
	}


} // KERBALISM

