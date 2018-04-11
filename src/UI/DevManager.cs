using KSP.Localization;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{


	public static class DevManager
	{
		public static void devman(this Panel p, Vessel v)
		{
			// avoid corner-case when this is called in a lambda after scene changes
			v = FlightGlobals.FindVessel(v.id);

			// if vessel doesn't exist anymore, leave the panel empty
			if (v == null) return;

			// get info from the cache
			vessel_info vi = Cache.VesselInfo(v);

			// if not a valid vessel, leave the panel empty
			if (!vi.is_valid) return;

			// set metadata
			p.title(Lib.BuildString(Lib.Ellipsis(v.vesselName, 24), " <color=#cccccc>" + Localizer.Format("#KERBALISM_UI_devman") + "</color>"));

			// time-out simulation
			if (p.timeout(vi)) return;

			// get devices
			Dictionary<uint, Device> devices = Computer.boot(v);

			// direct control
			if (script_index == 0)
			{
				// draw section title and desc
				p.section
				(
				  Localizer.Format("#KERBALISM_UI_devices"),
				  description(),
				  () => p.prev(ref script_index, (int)ScriptType.last),
				  () => p.next(ref script_index, (int)ScriptType.last)
				);

				// for each device
				foreach (var pair in devices)
				{
					// render device entry
					Device dev = pair.Value;
					p.content(dev.name(), dev.info(), string.Empty, dev.toggle, () => Highlighter.set(dev.part(), Color.cyan));
				}
			}
			// script editor
			else
			{
				// get script
				ScriptType script_type = (ScriptType)script_index;
				string script_name = script_type.ToString().Replace('_', ' ').ToUpper();
				Script script = DB.Vessel(v).computer.get(script_type);

				// draw section title and desc
				p.section
				(
				  script_name,
				  description(),
				  () => p.prev(ref script_index, (int)ScriptType.last),
				  () => p.next(ref script_index, (int)ScriptType.last)
				);

				// for each device
				foreach (var pair in devices)
				{
					// determine tribool state
					int state = !script.states.ContainsKey(pair.Key)
					  ? -1
					  : !script.states[pair.Key]
					  ? 0
					  : 1;

					// render device entry
					Device dev = pair.Value;
					p.content
					(
					  dev.name(),
					  state == -1 ? "<color=#999999>" + Localizer.Format("#KERBALISM_UI_dontcare") + " </color>" : state == 0 ? "<color=red>" + Localizer.Format("#KERBALISM_Generic_OFF") + "</color>" : "<color=cyan>" + Localizer.Format("#KERBALISM_Generic_ON") + "</color>",
					  string.Empty,
					  () =>
					  {
						  switch (state)
						  {
							  case -1: script.set(dev, true); break;
							  case 0: script.set(dev, null); break;
							  case 1: script.set(dev, false); break;
						  }
					  },
					  () => Highlighter.set(dev.part(), Color.cyan)
					);
				}
			}

			// no devices case
			if (devices.Count == 0)
			{
				p.content("<i>no devices</i>");
			}
		}

		// return short description of a script, or the time-out message
		static string description()
		{
			if (script_index == 0) return "<i>Control vessel components directly</i>";
			switch ((ScriptType)script_index)
			{
				case ScriptType.landed: return Localizer.Format("#Kerbalism_UI_1000000");        // #Kerbalism_UI_1000000 = <i>Called on landing</i>
				case ScriptType.atmo: return Localizer.Format("#Kerbalism_UI_1000001");      // #Kerbalism_UI_1000001 = <i>Called on entering atmosphere</i>
				case ScriptType.space: return Localizer.Format("#Kerbalism_UI_1000002");     // #Kerbalism_UI_1000002 = <i>Called on reaching space</i>
				case ScriptType.sunlight: return Localizer.Format("#Kerbalism_UI_1000003");      // #Kerbalism_UI_1000003 = <i>Called when sun became visible</i>
				case ScriptType.shadow: return Localizer.Format("#Kerbalism_UI_1000004");        // #Kerbalism_UI_1000004 = <i>Called when sun became occluded</i>
				case ScriptType.power_high: return Localizer.Format("#Kerbalism_UI_1000005");        // #Kerbalism_UI_1000005 = <i>Called when EC level goes above 80%</i>
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
			}
			return string.Empty;
		}

		// mode/script index
		static int script_index;
	}


} // KERBALISM

