using System.Linq;
using System.Collections.Generic;
using KSP.Localization;

namespace KERBALISM
{
	public enum ScriptType
	{
		power_low = 1,    // called when ec level goes below 15%
		power_high = 2,   // called when ec level goes above 15%
		sunlight = 3,     // called when sun rise
		shadow = 4,       // called when sun set
		unlinked = 5,     // called when signal is lost
		linked = 6,       // called when signal is regained
		drive_full = 7,  // called when storage capacity goes below 15%
		drive_empty = 8, // called when storage capacity goes above 30%
		landed = 9,       // called on landing
		atmo = 10,         // called on entering atmosphere
		space = 11,        // called on reaching space
		rad_low = 12,     // called when radiation goes below 0.05 rad/h
		rad_high = 13,    // called when radiation goes above 0.05 rad/h
		eva_out = 14,     // called when going out on eva
		eva_in = 15,      // called when coming back from eva
		action1 = 16,     // called when pressing 1
		action2 = 17,     // called when pressing 2
		action3 = 18,     // called when pressing 3
		action4 = 19,     // called when pressing 4
		action5 = 20,     // called when pressing 5
		last = 21
	}

	public sealed class Computer
	{

		public Computer(ConfigNode node)
		{
			scripts = new Dictionary<ScriptType, Script>();

			if (node == null)
				return;

			// load scripts
			foreach (var script_node in node.GetNode("scripts").GetNodes())
			{
				scripts.Add((ScriptType)Lib.Parse.ToUInt(script_node.name), new Script(script_node));
			}
		}

		public void Save(ConfigNode node)
		{
			// save scripts
			var scripts_node = node.AddNode("scripts");
			foreach (var p in scripts)
			{
				if (p.Value.states.Count == 0) continue; //< empty-script optimization
				p.Value.Save(scripts_node.AddNode(((uint)p.Key).ToString()));
			}
		}

		// get a script
		public Script Get(ScriptType type)
		{
			if (!scripts.ContainsKey(type)) scripts.Add(type, new Script());
			return scripts[type];
		}

		// execute a script
		public void Execute(Vessel v, ScriptType type)
		{
			// do nothing if there is no EC left on the vessel
			if (!v.KerbalismData().ResHandler.ElectricCharge.CriticalConsumptionSatisfied) return;

			// get the script
			Script script;
			if (scripts.TryGetValue(type, out script))
			{
				// execute the script
				script.Execute(GetModuleDevices(v));

				// show message to the user
				// - unless the script is empty (can happen when being edited)
				if (script.states.Count > 0 && v.KerbalismData().cfg_script)
				{
					Message.Post(Lib.BuildString(Local.UI_scriptvessel, " <b>", v.vesselName, "</b>"));
				}
			}
		}

		// call scripts automatically when conditions are met
		public void Automate(Vessel v, VesselData vd, VesselResHandler resources)
		{
			// get current states
			VesselResource ec = resources.GetResource("ElectricCharge");
			bool sunlight = !vd.EnvInFullShadow;
			bool power_low = ec.Level < 0.2;
			bool power_high = ec.Level > 0.8;
			bool radiation_low = vd.EnvRadiation < 0.000005552; //< 0.02 rad/h
			bool radiation_high = vd.EnvRadiation > 0.00001388; //< 0.05 rad/h
			bool signal = vd.Connection.linked;
			bool drive_full = vd.DrivesFreeSpace < double.MaxValue && (vd.DrivesFreeSpace / vd.DrivesCapacity < 0.15);
			bool drive_empty = vd.DrivesFreeSpace >= double.MaxValue || (vd.DrivesFreeSpace / vd.DrivesCapacity > 0.9);

			// get current situation
			bool landed = false;
			bool atmo = false;
			bool space = false;
			switch (v.situation)
			{
				case Vessel.Situations.LANDED:
				case Vessel.Situations.SPLASHED:
					landed = true;
					break;

				case Vessel.Situations.FLYING:
					atmo = true;
					break;

				case Vessel.Situations.SUB_ORBITAL:
				case Vessel.Situations.ORBITING:
				case Vessel.Situations.ESCAPING:
					space = true;
					break;
			}


			// compile list of scripts that need to be called
			var to_exec = new List<Script>();
			foreach (var p in scripts)
			{
				ScriptType type = p.Key;
				Script script = p.Value;
				if (script.states.Count == 0) continue; //< skip empty scripts (may happen during editing)

				switch (type)
				{
					case ScriptType.landed:
						if (landed && script.prev == "0") to_exec.Add(script);
						script.prev = landed ? "1" : "0";
						break;

					case ScriptType.atmo:
						if (atmo && script.prev == "0") to_exec.Add(script);
						script.prev = atmo ? "1" : "0";
						break;

					case ScriptType.space:
						if (space && script.prev == "0") to_exec.Add(script);
						script.prev = space ? "1" : "0";
						break;

					case ScriptType.sunlight:
						if (sunlight && script.prev == "0") to_exec.Add(script);
						script.prev = sunlight ? "1" : "0";
						break;

					case ScriptType.shadow:
						if (!sunlight && script.prev == "0") to_exec.Add(script);
						script.prev = !sunlight ? "1" : "0";
						break;

					case ScriptType.power_high:
						if (power_high && script.prev == "0") to_exec.Add(script);
						script.prev = power_high ? "1" : "0";
						break;

					case ScriptType.power_low:
						if (power_low && script.prev == "0") to_exec.Add(script);
						script.prev = power_low ? "1" : "0";
						break;

					case ScriptType.rad_low:
						if (radiation_low && script.prev == "0") to_exec.Add(script);
						script.prev = radiation_low ? "1" : "0";
						break;

					case ScriptType.rad_high:
						if (radiation_high && script.prev == "0") to_exec.Add(script);
						script.prev = radiation_high ? "1" : "0";
						break;

					case ScriptType.linked:
						if (signal && script.prev == "0") to_exec.Add(script);
						script.prev = signal ? "1" : "0";
						break;

					case ScriptType.unlinked:
						if (!signal && script.prev == "0") to_exec.Add(script);
						script.prev = !signal ? "1" : "0";
						break;

					case ScriptType.drive_full:
						if (drive_full && script.prev == "0") to_exec.Add(script);
						script.prev = drive_full ? "1" : "0";
						break;

					case ScriptType.drive_empty:
						if (drive_empty && script.prev == "0") to_exec.Add(script);
						script.prev = drive_empty ? "1" : "0";
						break;
				}
			}

			// if there are scripts to call
			if (to_exec.Count > 0)
			{
				// get list of devices
				// - we avoid creating it when there are no scripts to be executed, making its overall cost trivial
				List<Device> devices = GetModuleDevices(v);

				// execute all scripts
				foreach (Script script in to_exec)
				{
					script.Execute(devices);
				}

				// show message to the user
				if (v.KerbalismData().cfg_script)
				{
					Message.Post(Lib.BuildString("Script called on vessel <b>", v.vesselName, "</b>"));
				}
			}
		}

		// return set of devices on a vessel
		// - the list is only valid for a single simulation step
		public static List<Device> GetModuleDevices(Vessel v)
		{
			List<Device> moduleDevices = Cache.VesselObjectsCache<List<Device>>(v, "computer");
			if (moduleDevices != null)
				return moduleDevices;

			moduleDevices = new List<Device>();

			// store device being added
			Device device;

			// loaded vessel
			if (v.loaded)
			{
				foreach (PartModule m in Lib.FindModules<PartModule>(v))
				{
					switch (m.moduleName)
					{
						case "Sickbay":                      device = new SickbayDevice(m as Sickbay);                           break;
						case "Greenhouse":                   device = new GreenhouseDevice(m as Greenhouse);                     break;
						//case "GravityRing":                  device = new RingDevice(m as GravityRing);                          break;
						case "Emitter":                      device = new EmitterDevice(m as Emitter);                           break;
						case "Harvester":                    device = new HarvesterDevice(m as Harvester);                       break;
						case "Laboratory":                   device = new LaboratoryDevice(m as Laboratory);                     break;
						case "Experiment":                   device = new ExperimentDevice(m as Experiment);                     break;
						case "SolarPanelFixer":				 device = new PanelDevice(m as SolarPanelFixer);					 break;
						case "ModuleGenerator":              device = new GeneratorDevice(m as ModuleGenerator);                 break;
						case "ModuleResourceConverter":      device = new ConverterDevice(m as ModuleResourceConverter);         break;
						case "ModuleKPBSConverter":          device = new ConverterDevice(m as ModuleResourceConverter);         break;
						case "FissionReactor":               device = new ConverterDevice(m as ModuleResourceConverter);         break;
						case "ModuleResourceHarvester":      device = new DrillDevice(m as ModuleResourceHarvester);             break;
						case "ModuleLight":                  device = new LightDevice(m as ModuleLight);                         break;
						case "ModuleColoredLensLight":       device = new LightDevice(m as ModuleLight);                         break;
						case "ModuleMultiPointSurfaceLight": device = new LightDevice(m as ModuleLight);                         break;
						case "SCANsat":                      device = new ScannerDevice(m);                                      break;
						case "ModuleSCANresourceScanner":    device = new ScannerDevice(m);                                      break;
						case "ModuleDataTransmitter":
						case "ModuleDataTransmitterFeedeable": device = new AntennaDevice(m as ModuleDataTransmitter);           break;
						case "ModuleRTAntenna":
						case "ModuleRTAntennaPassive":       device = new AntennaRTDevice(m);                                    break;
						default: continue;
					}

					// add the device
					moduleDevices.Add(device);
				}
			}
			// unloaded vessel
			else
			{
				// store data required to support multiple modules of same type in a part
				var PD = new Dictionary<string, Lib.Module_prefab_data>();

				// for each part
				foreach (ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
				{
					// get part prefab (required for module properties)
					Part part_prefab = PartLoader.getPartInfoByName(p.partName).partPrefab;

					// get all module prefabs
					var module_prefabs = part_prefab.FindModulesImplementing<PartModule>();

					// clear module indexes
					PD.Clear();

					// for each module
					foreach (ProtoPartModuleSnapshot m in p.modules)
					{
						// get the module prefab
						// if the prefab doesn't contain this module, skip it
						PartModule module_prefab = Lib.ModulePrefab(module_prefabs, m.moduleName, PD);
						if (!module_prefab) continue;

						// if the module is disabled, skip it
						// note: this must be done after ModulePrefab is called, so that indexes are right
						if (!Lib.Proto.GetBool(m, "isEnabled")) continue;

						// depending on module name
						switch (m.moduleName)
						{
							case "Sickbay":                      device = new ProtoSickbayDevice(module_prefab as Sickbay, p, m);                  break;
							case "Greenhouse":                   device = new ProtoGreenhouseDevice(module_prefab as Greenhouse, p, m);            break;
							//case "GravityRing":                  device = new ProtoRingDevice(module_prefab as GravityRing, p, m);                 break;
							case "Emitter":                      device = new ProtoEmitterDevice(module_prefab as Emitter, p, m);                  break;
							case "Harvester":                    device = new ProtoHarvesterDevice(module_prefab as Harvester, p, m);              break;
							case "Laboratory":                   device = new ProtoLaboratoryDevice(module_prefab as Laboratory, p, m);            break;
							case "Experiment":					 device = new ProtoExperimentDevice(module_prefab as Experiment, p, m, v);         break;
							case "SolarPanelFixer":              device = new ProtoPanelDevice(module_prefab as SolarPanelFixer, p, m);            break;
							case "ModuleGenerator":              device = new ProtoGeneratorDevice(module_prefab as ModuleGenerator, p, m);        break;
							case "ModuleResourceConverter":
							case "ModuleKPBSConverter":
							case "FissionReactor":               device = new ProtoConverterDevice(module_prefab as ModuleResourceConverter, p, m);break;
							case "ModuleResourceHarvester":      device = new ProtoDrillDevice(module_prefab as ModuleResourceHarvester, p, m);    break;
							case "ModuleLight": 
							case "ModuleColoredLensLight": 
							case "ModuleMultiPointSurfaceLight": device = new ProtoLightDevice(module_prefab as ModuleLight, p, m);                break;
							case "SCANsat":                      device = new ProtoScannerDevice(module_prefab, p, m, v);                          break;
							case "ModuleSCANresourceScanner":    device = new ProtoScannerDevice(module_prefab, p, m, v);                          break;
							case "ModuleDataTransmitter":
							case "ModuleDataTransmitterFeedeable": device = new ProtoAntennaDevice(module_prefab as ModuleDataTransmitter, p, m);  break;
							case "ModuleRTAntenna":
							case "ModuleRTAntennaPassive":       device = new ProtoAntennaRTDevice(module_prefab, p, m);                           break;
							default: continue;
						}

						// add the device
						moduleDevices.Add(device);
					}
				}
			}

			// return all found module devices sorted by type, then by name
			// in reverse (the list will be presented from end to start in the UI)
			moduleDevices.Sort((b, a) =>
			{
				int xdiff = a.DeviceType.CompareTo(b.DeviceType);
				if (xdiff != 0) return xdiff;
				else return a.Name.CompareTo(b.Name);
			});

			// now add vessel wide devices to the end of the list
			VesselData vd = v.KerbalismData();

			moduleDevices.Add(new VesselDeviceTransmit(v, vd)); // vessel wide transmission toggle

			foreach(var process in vd.VesselProcesses.Processes)
				moduleDevices.Add(new VesselProcessDevice(v, vd, process));

			Cache.SetVesselObjectsCache(v, "computer", moduleDevices);
			return moduleDevices;
		}

		Dictionary<ScriptType, Script> scripts;
	}
} // KERBALISM
