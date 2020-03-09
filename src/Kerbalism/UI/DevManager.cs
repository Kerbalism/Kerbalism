using KSP.Localization;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;


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

			// get data
			VesselData vd = v.KerbalismData();

			// if not a valid vessel, leave the panel empty
			if (!vd.IsSimulated) return;

			// set metadata
			p.Title(Lib.BuildString(Lib.Ellipsis(v.vesselName, Styles.ScaleStringLength(20)), Lib.Color(Local.UI_devman, Lib.Kolor.LightGrey)));
			p.Width(Styles.ScaleWidthFloat(355.0f));
			p.paneltype = Panel.PanelType.scripts;

			// time-out simulation
			if (!Lib.IsControlUnit(v) && p.Timeout(vd)) return;

			// get devices
			List<Device> devices = Computer.GetModuleDevices(v);
			int deviceCount = 0;

			// direct control
			if (script_index == 0)
			{
				// draw section title and desc
				p.AddSection
				(
				  Local.UI_devices,
				  Description(),
				  () => p.Prev(ref script_index, (int)ScriptType.last),
				  () => p.Next(ref script_index, (int)ScriptType.last),
					 false
				);

				bool hasVesselDeviceSection = false;
				bool hasModuleDeviceSection = false;

				// for each device
				for (int i = devices.Count - 1; i >= 0; i--)
				{
					Device dev = devices[i];
					
					dev.OnUpdate();
					if (!dev.IsVisible) continue;

					// create vessel device section if necessary
					if (dev is VesselDevice)
					{
						if (!hasVesselDeviceSection)
						{
							p.AddSection(Local.DevManager_VESSELDEVICES);//"VESSEL DEVICES"
							hasVesselDeviceSection = true;
						}
					}
					// create module device section if necessary
					else
					{
						if (!hasModuleDeviceSection)
						{
							p.AddSection(Local.DevManager_MODULEDEVICES);//"MODULE DEVICES"
							hasModuleDeviceSection = true;
						}
					}

					if (dev.PartId != 0u)
						p.AddContent(dev.DisplayName, dev.Status, dev.Tooltip, dev.Toggle, () => Highlighter.Set(dev.PartId, Color.cyan));
					else
						p.AddContent(dev.DisplayName, dev.Status, dev.Tooltip, dev.Toggle);

					if (dev.Icon != null)
						p.SetLeftIcon(dev.Icon.texture, dev.Icon.tooltip, dev.Icon.onClick);

					deviceCount++;
				}
			}
			// script editor
			else
			{
				// get script
				ScriptType script_type = (ScriptType)script_index;
				string script_name = Name().ToUpper();
				Script script = v.KerbalismData().computer.Get(script_type);

				// draw section title and desc
				p.AddSection
				(
				  script_name,
				  Description(),
				  () => p.Prev(ref script_index, (int)ScriptType.last),
				  () => p.Next(ref script_index, (int)ScriptType.last)
				);

				bool hasVesselDeviceSection = false;
				bool hasModuleDeviceSection = false;
				
				// for each device
				for (int i = devices.Count - 1; i >= 0; i--)
				{
					Device dev = devices[i];

					dev.OnUpdate();
					if (!dev.IsVisible) continue;

					// determine tribool state
					int state = !script.states.ContainsKey(dev.Id)
					  ? -1
					  : !script.states[dev.Id]
					  ? 0
					  : 1;

					// create vessel device section if necessary
					if (dev is VesselDevice)
					{
						if (!hasVesselDeviceSection)
						{
							p.AddSection(Local.DevManager_VESSELDEVICES);//"VESSEL DEVICES"
							hasVesselDeviceSection = true;
						}
					}
					// create module device section if necessary
					else
					{
						if (!hasModuleDeviceSection)
						{
							p.AddSection(Local.DevManager_MODULEDEVICES);//"MODULE DEVICES"
							hasModuleDeviceSection = true;
						}
					}

					// render device entry
					p.AddContent
					(
					  dev.DisplayName,
					  state == -1 ? Lib.Color(Local.UI_dontcare, Lib.Kolor.DarkGrey) : Lib.Color(state == 0, Local.Generic_OFF, Lib.Kolor.Yellow, Local.Generic_ON, Lib.Kolor.Green),
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
					  () => Highlighter.Set(dev.PartId, Color.cyan)
					);
					deviceCount++;
				}
			}

			// no devices case
			if (deviceCount == 0)
			{
				p.AddContent("<i>"+Local.DevManager_nodevices +"</i>");//no devices
			}
		}

		// return short description of a script, or the time-out message
		static string Name()
		{
			switch ((ScriptType)script_index)
			{
				case ScriptType.landed: return Local.DevManager_NameTabLanded;
				case ScriptType.atmo: return Local.DevManager_NameTabAtmo;
				case ScriptType.space: return Local.DevManager_NameTabSpace;
				case ScriptType.sunlight: return Local.DevManager_NameTabSunlight;
				case ScriptType.shadow: return Local.DevManager_NameTabShadow;
				case ScriptType.power_high: return Local.DevManager_NameTabPowerHigh;
				case ScriptType.power_low: return Local.DevManager_NameTabPowerLow;
				case ScriptType.rad_high: return Local.DevManager_NameTabRadHigh;
				case ScriptType.rad_low: return Local.DevManager_NameTabRadLow;
				case ScriptType.linked: return Local.DevManager_NameTabLinked;
				case ScriptType.unlinked: return Local.DevManager_NameTabUnlinked;
				case ScriptType.eva_out: return Local.DevManager_NameTabEVAOut;
				case ScriptType.eva_in: return Local.DevManager_NameTabEVAIn;
				case ScriptType.action1: return Local.DevManager_NameTabAct1;
				case ScriptType.action2: return Local.DevManager_NameTabAct2;
				case ScriptType.action3: return Local.DevManager_NameTabAct3;
				case ScriptType.action4: return Local.DevManager_NameTabAct4;
				case ScriptType.action5: return Local.DevManager_NameTabAct5;
				case ScriptType.drive_full: return Local.DevManager_NameTabDriveFull;
				case ScriptType.drive_empty: return Local.DevManager_NameTabDriveEmpty;
			}
			return string.Empty;
		}

		// return short description of a script, or the time-out message
		static string Description()
		{
			if (script_index == 0) return Local.DevManager_TabManual;//Control vessel components directly
			switch ((ScriptType)script_index)
			{
				case ScriptType.landed: return Local.DevManager_TabLanded;        // <i>Called on landing</i>
				case ScriptType.atmo: return Local.DevManager_TabAtmo;          // <i>Called on entering atmosphere</i>
				case ScriptType.space: return Local.DevManager_TabSpace;         // <i>Called on reaching space</i>
				case ScriptType.sunlight: return Local.DevManager_TabSunlight;      // <i>Called when sun became visible</i>
				case ScriptType.shadow: return Local.DevManager_TabShadow;        // <i>Called when sun became occluded</i>
				case ScriptType.power_high: return Local.DevManager_TabPowerHigh;    // <i>Called when EC level goes above 80%</i>
				case ScriptType.power_low: return Local.DevManager_TabPowerLow;     // <i>Called when EC level goes below 20%</i>
				case ScriptType.rad_high: return Local.DevManager_TabRadHigh;      // <i>Called when radiation exceed 0.05 rad/h</i>
				case ScriptType.rad_low: return Local.DevManager_TabRadLow;       // <i>Called when radiation goes below 0.02 rad/h</i>
				case ScriptType.linked: return Local.DevManager_TabLinked;        // <i>Called when signal is regained</i>
				case ScriptType.unlinked: return Local.DevManager_TabUnlinked;      // <i>Called when signal is lost</i>
				case ScriptType.eva_out: return Local.DevManager_TabEVAOut;       // <i>Called when going out on EVA</i>
				case ScriptType.eva_in: return Local.DevManager_TabEVAIn;        // <i>Called when returning from EVA</i>
				case ScriptType.action1: return Local.DevManager_TabAct1;       // <i>Called by pressing <b>1</b> on the keyboard</i>
				case ScriptType.action2: return Local.DevManager_TabAct2;       // <i>Called by pressing <b>2</b> on the keyboard</i>
				case ScriptType.action3: return Local.DevManager_TabAct3;       // <i>Called by pressing <b>3</b> on the keyboard</i>
				case ScriptType.action4: return Local.DevManager_TabAct4;       // <i>Called by pressing <b>4</b> on the keyboard</i>
				case ScriptType.action5: return Local.DevManager_TabAct5;       // <i>Called by pressing <b>5</b> on the keyboard</i>
				case ScriptType.drive_full: return Local.DevManager_TabDriveFull;
				case ScriptType.drive_empty: return Local.DevManager_TabDriveEmpty;
			}
			return string.Empty;
		}

		// mode/script index
		static int script_index;
	}


} // KERBALISM

