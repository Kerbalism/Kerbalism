using System.Linq;
using System.Collections.Generic;
using KSP.Localization;

namespace KERBALISM
{
	public enum ScriptType
	{
		power_low = 7,    // called when ec level goes below 15%
		power_high = 6,   // called when ec level goes above 15%
		sunlight = 4,     // called when sun rise
		shadow = 5,       // called when sun set
		unlinked = 11,    // called when signal is lost
		linked = 10,      // called when signal is regained
		landed = 1,       // called on landing
		atmo = 2,         // called on entering atmosphere
		space = 3,        // called on reaching space
		rad_low = 8,      // called when radiation goes below 0.05 rad/h
		rad_high = 9,     // called when radiation goes above 0.05 rad/h
		eva_out = 12,     // called when going out on eva
		eva_in = 13,      // called when coming back from eva
		action1 = 14,     // called when pressing 1
		action2 = 15,     // called when pressing 2
		action3 = 16,     // called when pressing 3
		action4 = 17,     // called when pressing 4
		action5 = 18,     // called when pressing 5
		last = 19
	}

	public sealed class Computer
	{
		public Computer()
		{
			scripts = new Dictionary<ScriptType, Script>();
		}

		public Computer(ConfigNode node)
		{
			// load scripts
			scripts = new Dictionary<ScriptType, Script>();
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
			Resource_info ec = ResourceCache.Info(v, "ElectricCharge");
			if (ec.amount <= double.Epsilon) return;

			// get the script
			Script script;
			if (scripts.TryGetValue(type, out script))
			{
				// execute the script
				script.Execute(Boot(v));

				// show message to the user
				// - unless the script is empty (can happen when being edited)
				if (script.states.Count > 0 && DB.Vessel(v).cfg_script)
				{
					Message.Post(Lib.BuildString(Localizer.Format("#KERBALISM_UI_scriptvessel"), " <b>", v.vesselName, "</b>"));
				}
			}
		}

		// call scripts automatically when conditions are met
		public void Automate(Vessel v, Vessel_info vi, Vessel_resources resources)
		{
			// do nothing if automation is disabled
			if (!Features.Automation) return;

			// get current states
			Resource_info ec = resources.Info(v, "ElectricCharge");
			bool sunlight = vi.sunlight > double.Epsilon;
			bool power_low = ec.level < 0.2;
			bool power_high = ec.level > 0.8;
			bool radiation_low = vi.radiation < 0.000005552; //< 0.02 rad/h
			bool radiation_high = vi.radiation > 0.00001388; //< 0.05 rad/h
			bool signal = vi.connection.linked;

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
				}
			}

			// if there are scripts to call
			if (to_exec.Count > 0)
			{
				// get list of devices
				// - we avoid creating it when there are no scripts to be executed, making its overall cost trivial
				var devices = Boot(v);

				// execute all scripts
				foreach (Script script in to_exec)
				{
					script.Execute(devices);
				}

				// show message to the user
				if (DB.Vessel(v).cfg_script)
				{
					Message.Post(Lib.BuildString("Script called on vessel <b>", v.vesselName, "</b>"));
				}
			}
		}

		// return set of devices on a vessel
		// - the list is only valid for a single simulation step
		public static Dictionary<uint, Device> Boot(Vessel v)
		{
			// store all devices
			var devices = new Dictionary<uint, Device>();

			// store device being added
			Device dev;

			// loaded vessel
			if (v.loaded)
			{
				foreach (PartModule m in Lib.FindModules<PartModule>(v))
				{
					switch (m.moduleName)
					{
						case "ProcessController":            dev = new ProcessDevice(m as ProcessController);                 break;
						case "Sickbay":                      dev = new SickbayDevice(m as Sickbay);                           break;
						case "Greenhouse":                   dev = new GreenhouseDevice(m as Greenhouse);                     break;
						case "GravityRing":                  dev = new RingDevice(m as GravityRing);                          break;
						case "Emitter":                      dev = new EmitterDevice(m as Emitter);                           break;
						case "Laboratory":                   dev = new LaboratoryDevice(m as Laboratory);                     break;
						case "Experiment":                   dev = new ExperimentDevice(m as Experiment);                     break;
						case "ModuleDeployableSolarPanel":   dev = new PanelDevice(m as ModuleDeployableSolarPanel);          break;
						case "ModuleGenerator":              dev = new GeneratorDevice(m as ModuleGenerator);                 break;
						case "ModuleResourceConverter":      dev = new ConverterDevice(m as ModuleResourceConverter);         break;
						case "ModuleKPBSConverter":          dev = new ConverterDevice(m as ModuleResourceConverter);         break;
						case "FissionReactor":               dev = new ConverterDevice(m as ModuleResourceConverter);         break;
						case "ModuleResourceHarvester":      dev = new DrillDevice(m as ModuleResourceHarvester);             break;
						case "ModuleLight":                  dev = new LightDevice(m as ModuleLight);                         break;
						case "ModuleColoredLensLight":       dev = new LightDevice(m as ModuleLight);                         break;
						case "ModuleMultiPointSurfaceLight": dev = new LightDevice(m as ModuleLight);                         break;
						case "SCANsat":                      dev = new ScannerDevice(m);                                      break;
						case "ModuleSCANresourceScanner":    dev = new ScannerDevice(m);                                      break;
						case "ModuleRTAntenna":
						case "ModuleDataTransmitter":        dev = new Antenna(m, m.moduleName);                              break;
						case "ModuleRTAntennaPassive":       dev = new Antenna(m, "ModuleRTAntenna"); break;
						default: continue;
					}

					// add the device
					// - multiple same-type components in the same part will have the same id, and are ignored
					if (!devices.ContainsKey(dev.Id()))
					{
						devices.Add(dev.Id(), dev);
					}
				}
			}
			// unloaded vessel
			else
			{
				// store data required to support multiple modules of same type in a part
				var PD = new Dictionary<string, Lib.Module_prefab_data>();

				var experiments = new List<KeyValuePair<Experiment, ProtoPartModuleSnapshot>>();

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
							case "ProcessController":            dev = new ProtoProcessDevice(m, module_prefab as ProcessController, p.flightID);                 break;
							case "Sickbay":                      dev = new ProtoSickbayDevice(m, module_prefab as Sickbay, p.flightID);                           break;
							case "Greenhouse":                   dev = new ProtoGreenhouseDevice(m, p.flightID);                                                  break;
							case "GravityRing":                  dev = new ProtoRingDevice(m, p.flightID);                                                        break;
							case "Emitter":                      dev = new ProtoEmitterDevice(m, p.flightID);                                                     break;
							case "Laboratory":                   dev = new ProtoLaboratoryDevice(m, p.flightID);                                                  break;
							
							case "Experiment":
								experiments.Add(new KeyValuePair<Experiment, ProtoPartModuleSnapshot>(module_prefab as Experiment, m));
								dev = new ProtoExperimentDevice(m, module_prefab as Experiment, p.flightID, experiments);
								break;

							case "ModuleDeployableSolarPanel":   dev = new ProtoPanelDevice(m, module_prefab as ModuleDeployableSolarPanel, p.flightID);          break;
							case "ModuleGenerator":              dev = new ProtoGeneratorDevice(m, module_prefab as ModuleGenerator, p.flightID);                 break;
							case "ModuleResourceConverter":      dev = new ProtoConverterDevice(m, module_prefab as ModuleResourceConverter, p.flightID);         break;
							case "ModuleKPBSConverter":          dev = new ProtoConverterDevice(m, module_prefab as ModuleResourceConverter, p.flightID);         break;
							case "FissionReactor":               dev = new ProtoConverterDevice(m, module_prefab as ModuleResourceConverter, p.flightID);         break;
							case "ModuleResourceHarvester":      dev = new ProtoDrillDevice(m, module_prefab as ModuleResourceHarvester, p.flightID);             break;
							case "ModuleLight":                  dev = new ProtoLightDevice(m, p.flightID);                                                       break;
							case "ModuleColoredLensLight":       dev = new ProtoLightDevice(m, p.flightID);                                                       break;
							case "ModuleMultiPointSurfaceLight": dev = new ProtoLightDevice(m, p.flightID);                                                       break;
							case "SCANsat":                      dev = new ProtoScannerDevice(m, part_prefab, v, p.flightID);                                     break;
							case "ModuleSCANresourceScanner":    dev = new ProtoScannerDevice(m, part_prefab, v, p.flightID);                                     break;
							case "ModuleRTAntenna":
							case "ModuleDataTransmitter":        dev = new ProtoPartAntenna(m, p, v, m.moduleName, p.flightID);                                   break;
							case "ModuleRTAntennaPassive":       dev = new ProtoPartAntenna(m, p, v, "ModuleRTAntenna", p.flightID); break;
							default: continue;
						}

						// add the device
						// - multiple same-type components in the same part will have the same id, and are ignored
						if (!devices.ContainsKey(dev.Id()))
						{
							devices.Add(dev.Id(), dev);
						}
					}
				}
			}

			devices = devices.OrderBy(k => k.Value.Name()).ToDictionary(pair => pair.Key, pair => pair.Value);
			//return all found devices sorted by name
			return devices;
		}

		Dictionary<ScriptType, Script> scripts;
	}
} // KERBALISM
