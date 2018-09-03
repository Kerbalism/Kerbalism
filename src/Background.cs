using System;
using System.Collections.Generic;
using System.Reflection;
using Experience;
using UnityEngine;
using System.Collections;


namespace KERBALISM
{


	public static class Background
	{
		enum Module_type
		{
			Reliability = 0,
			Experiment,
			Greenhouse,
			GravityRing,
			Harvester,
			Emitter,
			Laboratory,
			Command,
			Panel,
			Generator,
			Converter,
			Drill,
			AsteroidDrill,
			StockLab,
			Light,
			Scanner,
			CurvedPanel,
			FissionGenerator,
			RadioisotopeGenerator,
			CryoTank,
			Unknown,
			FNGenerator,
		}

		static Module_type ModuleType(string module_name)
		{
			switch (module_name)
			{
				case "Reliability": return Module_type.Reliability;
				case "Experiment": return Module_type.Experiment;
				case "Greenhouse": return Module_type.Greenhouse;
				case "GravityRing": return Module_type.GravityRing;
				case "Harvester": return Module_type.Harvester;
				case "Emitter": return Module_type.Emitter;
				case "Laboratory": return Module_type.Laboratory;
				case "ModuleCommand": return Module_type.Command;
				case "ModuleDeployableSolarPanel": return Module_type.Panel;
				case "ModuleGenerator": return Module_type.Generator;
				case "ModuleResourceConverter":
				case "ModuleKPBSConverter":
				case "FissionReactor": return Module_type.Converter;
				// Kerbalism default profile uses the Harvester module (both for air and ground harvesting)
				// Other profiles use the stock ModuleResourceHarvester (only for ground harvesting)
				case "ModuleResourceHarvester": return Module_type.Drill;
				case "ModuleAsteroidDrill": return Module_type.AsteroidDrill;
				case "ModuleScienceConverter": return Module_type.StockLab;
				case "ModuleLight":
				case "ModuleColoredLensLight":
				case "ModuleMultiPointSurfaceLight": return Module_type.Light;
				case "SCANsat":
				case "ModuleSCANresourceScanner": return Module_type.Scanner;
				case "ModuleCurvedSolarPanel": return Module_type.CurvedPanel;
				case "FissionGenerator": return Module_type.FissionGenerator;
				case "ModuleRadioisotopeGenerator": return Module_type.RadioisotopeGenerator;
				case "ModuleCryoTank": return Module_type.CryoTank;
				case "FNGenerator": return Module_type.FNGenerator;
			}
			return Module_type.Unknown;
		}

		public static void Update(Vessel v, Vessel_info vi, VesselData vd, Vessel_resources resources, double elapsed_s)
		{
			// get most used resource handlers
			Resource_info ec = resources.Info(v, "ElectricCharge");

			// store data required to support multiple modules of same type in a part
			var PD = new Dictionary<string, Lib.Module_prefab_data>();

			// This is basically handled in cache. However, when accelerating time warp while
			// the vessel is in shadow, the cache logic doesn't kick in soon enough. So we double-check here
			if (TimeWarp.CurrentRate > 1000.0f || elapsed_s > 150)  // we're time warping fast...
			{
				vi.highspeedWarp(v);
			}

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
					// get module type
					// if the type is unknown, skip it
					Module_type type = ModuleType(m.moduleName);
					if (type == Module_type.Unknown) continue;

					// get the module prefab
					// if the prefab doesn't contain this module, skip it
					PartModule module_prefab = Lib.ModulePrefab(module_prefabs, m.moduleName, PD);
					if (!module_prefab) continue;

					// if the module is disabled, skip it
					// note: this must be done after ModulePrefab is called, so that indexes are right
					if (!Lib.Proto.GetBool(m, "isEnabled")) continue;

					// process modules
					// note: this should be a fast switch, possibly compiled to a jump table
					switch (type)
					{
						case Module_type.Reliability: Reliability.BackgroundUpdate(v, p, m, module_prefab as Reliability); break;
						case Module_type.Experiment: Experiment.BackgroundUpdate(v, m, module_prefab as Experiment, ec, elapsed_s); break;
						case Module_type.Greenhouse: Greenhouse.BackgroundUpdate(v, m, module_prefab as Greenhouse, vi, resources, elapsed_s); break;
						case Module_type.GravityRing: GravityRing.BackgroundUpdate(v, p, m, module_prefab as GravityRing, ec, elapsed_s); break;
						case Module_type.Emitter: Emitter.BackgroundUpdate(v, p, m, module_prefab as Emitter, ec, elapsed_s); break;
						case Module_type.Harvester: Harvester.BackgroundUpdate(v, m, module_prefab as Harvester, elapsed_s); break; // Kerbalism ground and air harvester module
						case Module_type.Laboratory: Laboratory.BackgroundUpdate(v, p, m, module_prefab as Laboratory, ec, elapsed_s); break;
						case Module_type.Command: ProcessCommand(v, p, m, module_prefab as ModuleCommand, resources, elapsed_s); break;
						case Module_type.Panel: ProcessPanel(v, p, m, module_prefab as ModuleDeployableSolarPanel, vi, ec, elapsed_s); break;
						case Module_type.Generator: ProcessGenerator(v, p, m, module_prefab as ModuleGenerator, resources, elapsed_s); break;
						case Module_type.Converter: ProcessConverter(v, p, m, module_prefab as ModuleResourceConverter, resources, elapsed_s); break;
						case Module_type.Drill: ProcessDrill(v, p, m, module_prefab as ModuleResourceHarvester, resources, elapsed_s); break; // Stock ground harvester module
						case Module_type.AsteroidDrill: ProcessAsteroidDrill(v, p, m, module_prefab as ModuleAsteroidDrill, resources, elapsed_s); break; // Stock asteroid harvester module
						case Module_type.StockLab: ProcessStockLab(v, p, m, module_prefab as ModuleScienceConverter, ec, elapsed_s); break;
						case Module_type.Light: ProcessLight(v, p, m, module_prefab as ModuleLight, ec, elapsed_s); break;
						case Module_type.Scanner: ProcessScanner(v, p, m, module_prefab, part_prefab, vd, ec, elapsed_s); break;
						case Module_type.CurvedPanel: ProcessCurvedPanel(v, p, m, module_prefab, part_prefab, vi, ec, elapsed_s); break;
						case Module_type.FissionGenerator: ProcessFissionGenerator(v, p, m, module_prefab, ec, elapsed_s); break;
						case Module_type.RadioisotopeGenerator: ProcessRadioisotopeGenerator(v, p, m, module_prefab, ec, elapsed_s); break;
						case Module_type.CryoTank: ProcessCryoTank(v, p, m, module_prefab, resources, ec, elapsed_s); break;
						case Module_type.FNGenerator: ProcessFNGenerator(v, p, m, module_prefab, ec, elapsed_s); break;
					}
				}
			}
		}

		static void ProcessFNGenerator(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, PartModule fission_generator, Resource_info ec, double elapsed_s)
		{
			string maxPowerStr = Lib.Proto.GetString(m, "MaxPowerStr");
			double maxPower = 0;
			if (maxPowerStr.Contains("GW"))
				maxPower = double.Parse(maxPowerStr.Replace(" GW", "")) * 1000000;
			else if (maxPowerStr.Contains("MW"))
				maxPower = double.Parse(maxPowerStr.Replace(" MW", "")) * 1000;
			else
				maxPower = double.Parse(maxPowerStr.Replace(" KW", ""));

			ec.Produce(maxPower * elapsed_s);
		}

		static void ProcessCommand(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, ModuleCommand command, Vessel_resources resources, double elapsed_s)
		{
			// do not consume if this is a MCM with no crew
			// rationale: for consistency, the game doesn't consume resources for MCM without crew in loaded vessels
			//            this make some sense: you left a vessel with some battery and nobody on board, you expect it to not consume EC
			if (command.minimumCrew == 0 || p.protoModuleCrew.Count > 0)
			{
				// for each input resource
				foreach (ModuleResource ir in command.resHandler.inputResources)
				{
					// consume the resource
					resources.Consume(v, ir.name, ir.rate * elapsed_s);
				}
			}
		}


		static void ProcessPanel(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, ModuleDeployableSolarPanel panel, Vessel_info info, Resource_info ec, double elapsed_s)
		{
			// note: we ignore temperature curve, and make sure it is not relevant in the MM patch
			// note: we ignore power curve, that is used by no panel as far as I know
			// note: cylindrical and spherical panels are not supported
			// note: we assume the tracking target is SUN

			// if in sunlight and extended
			if (info.sunlight > double.Epsilon && m.moduleValues.GetValue("deployState") == "EXTENDED")
			{
				// get panel normal/pivot direction in world space
				Transform tr = panel.part.FindModelComponent<Transform>(panel.pivotName);
				Vector3d dir = panel.isTracking ? tr.up : tr.forward;
				dir = (v.transform.rotation * p.rotation * dir).normalized;

				// calculate cosine factor
				// - fixed panel: clamped cosine
				// - tracking panel, tracking pivot enabled: around the pivot
				// - tracking panel, tracking pivot disabled: assume perfect alignment
				double cosine_factor =
					!panel.isTracking
				  ? Math.Max(Vector3d.Dot(info.sun_dir, dir), 0.0)
				  : Settings.TrackingPivot
				  ? Math.Cos(1.57079632679 - Math.Acos(Vector3d.Dot(info.sun_dir, dir)))
				  : 1.0;

				// calculate normalized solar flux
				// - this include fractional sunlight if integrated over orbit
				// - this include atmospheric absorption if inside an atmosphere
				double norm_solar_flux = info.solar_flux / Sim.SolarFluxAtHome();

				// calculate output
				double output = panel.resHandler.outputResources[0].rate              // nominal panel charge rate at 1 AU
							  * norm_solar_flux                                       // normalized flux at panel distance from sun
							  * cosine_factor;                                        // cosine factor of panel orientation

				// produce EC
				ec.Produce(output * elapsed_s);
			}
		}


		static void ProcessGenerator(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, ModuleGenerator generator, Vessel_resources resources, double elapsed_s)
		{
			// if active
			if (Lib.Proto.GetBool(m, "generatorIsActive"))
			{
				// create and commit recipe
				Resource_recipe recipe = new Resource_recipe();
				foreach (ModuleResource ir in generator.resHandler.inputResources)
				{
					recipe.Input(ir.name, ir.rate * elapsed_s);
				}
				foreach (ModuleResource or in generator.resHandler.outputResources)
				{
					recipe.Output(or.name, or.rate * elapsed_s, true);
				}
				resources.Transform(recipe);
			}
		}


		static void ProcessConverter(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, ModuleResourceConverter converter, Vessel_resources resources, double elapsed_s)
		{
			// note: ignore stock temperature mechanic of converters
			// note: ignore auto shutdown
			// note: non-mandatory resources 'dynamically scale the ratios', that is exactly what mandatory resources do too (DERP ALERT)
			// note: 'undo' stock behavior by forcing lastUpdateTime to now (to minimize overlapping calculations from this and stock post-facto simulation)

			// if active
			if (Lib.Proto.GetBool(m, "IsActivated"))
			{
				// determine if vessel is full of all output resources
				// note: comparing against previous amount
				bool full = true;
				foreach (var or in converter.outputList)
				{
					Resource_info res = resources.Info(v, or.ResourceName);
					full &= (res.level >= converter.FillAmount - double.Epsilon);
				}

				// if not full
				if (!full)
				{
					// deduce crew bonus
					int exp_level = -1;
					if (converter.UseSpecialistBonus)
					{
						foreach (ProtoCrewMember c in Lib.CrewList(v))
						{
							if (c.experienceTrait.Effects.Find(k => k.Name == converter.ExperienceEffect) != null)
							{
								exp_level = Math.Max(exp_level, c.experienceLevel);
							}
						}
					}
					double exp_bonus = exp_level < 0
					  ? converter.EfficiencyBonus * converter.SpecialistBonusBase
					  : converter.EfficiencyBonus * (converter.SpecialistBonusBase + (converter.SpecialistEfficiencyFactor * (exp_level + 1)));

					// create and commit recipe
					Resource_recipe recipe = new Resource_recipe();
					foreach (var ir in converter.inputList)
					{
						recipe.Input(ir.ResourceName, ir.Ratio * exp_bonus * elapsed_s);
					}
					foreach (var or in converter.outputList)
					{
						recipe.Output(or.ResourceName, or.Ratio * exp_bonus * elapsed_s, or.DumpExcess);
					}
					resources.Transform(recipe);
				}

				// undo stock behavior by forcing last_update_time to now
				Lib.Proto.Set(m, "lastUpdateTime", Planetarium.GetUniversalTime());
			}
		}


		static void ProcessDrill(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, ModuleResourceHarvester harvester, Vessel_resources resources, double elapsed_s)
		{
			// note: ignore stock temperature mechanic of harvesters
			// note: ignore auto shutdown
			// note: ignore depletion (stock seem to do the same)
			// note: 'undo' stock behavior by forcing lastUpdateTime to now (to minimize overlapping calculations from this and stock post-facto simulation)

			// if active
			if (Lib.Proto.GetBool(m, "IsActivated"))
			{
				// do nothing if full
				// note: comparing against previous amount
				if (resources.Info(v, harvester.ResourceName).level < harvester.FillAmount - double.Epsilon)
				{
					// deduce crew bonus
					int exp_level = -1;
					if (harvester.UseSpecialistBonus)
					{
						foreach (ProtoCrewMember c in Lib.CrewList(v))
						{
							if (c.experienceTrait.Effects.Find(k => k.Name == harvester.ExperienceEffect) != null)
							{
								exp_level = Math.Max(exp_level, c.experienceLevel);
							}
						}
					}
					double exp_bonus = exp_level < 0
					  ? harvester.EfficiencyBonus * harvester.SpecialistBonusBase
					  : harvester.EfficiencyBonus * (harvester.SpecialistBonusBase + (harvester.SpecialistEfficiencyFactor * (exp_level + 1)));

					// detect amount of ore in the ground
					AbundanceRequest request = new AbundanceRequest
					{
						Altitude = v.altitude,
						BodyId = v.mainBody.flightGlobalsIndex,
						CheckForLock = false,
						Latitude = v.latitude,
						Longitude = v.longitude,
						ResourceType = (HarvestTypes)harvester.HarvesterType,
						ResourceName = harvester.ResourceName
					};
					double abundance = ResourceMap.Instance.GetAbundance(request);

					// if there is actually something (should be if active when unloaded)
					if (abundance > harvester.HarvestThreshold)
					{
						// create and commit recipe
						Resource_recipe recipe = new Resource_recipe();
						foreach (var ir in harvester.inputList)
						{
							recipe.Input(ir.ResourceName, ir.Ratio * elapsed_s);
						}
						recipe.Output(harvester.ResourceName, abundance * harvester.Efficiency * exp_bonus * elapsed_s, true);
						resources.Transform(recipe);
					}
				}

				// undo stock behavior by forcing last_update_time to now
				Lib.Proto.Set(m, "lastUpdateTime", Planetarium.GetUniversalTime());
			}
		}


		static void ProcessAsteroidDrill(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, ModuleAsteroidDrill asteroid_drill, Vessel_resources resources, double elapsed_s)
		{
			// note: untested
			// note: ignore stock temperature mechanic of asteroid drills
			// note: ignore auto shutdown
			// note: 'undo' stock behavior by forcing lastUpdateTime to now (to minimize overlapping calculations from this and stock post-facto simulation)

			// if active
			if (Lib.Proto.GetBool(m, "IsActivated"))
			{
				// get asteroid data
				ProtoPartModuleSnapshot asteroid_info = null;
				ProtoPartModuleSnapshot asteroid_resource = null;
				foreach (ProtoPartSnapshot pp in v.protoVessel.protoPartSnapshots)
				{
					if (asteroid_info == null) asteroid_info = pp.modules.Find(k => k.moduleName == "ModuleAsteroidInfo");
					if (asteroid_resource == null) asteroid_resource = pp.modules.Find(k => k.moduleName == "ModuleAsteroidResource");
				}

				// if there is actually an asteroid attached to this active asteroid drill (it should)
				if (asteroid_info != null && asteroid_resource != null)
				{
					// get some data
					double mass_threshold = Lib.Proto.GetDouble(asteroid_info, "massThresholdVal");
					double mass = Lib.Proto.GetDouble(asteroid_info, "currentMassVal");
					double abundance = Lib.Proto.GetDouble(asteroid_resource, "abundance");
					string res_name = Lib.Proto.GetString(asteroid_resource, "resourceName");
					double res_density = PartResourceLibrary.Instance.GetDefinition(res_name).density;

					// if asteroid isn't depleted
					if (mass > mass_threshold && abundance > double.Epsilon)
					{
						// deduce crew bonus
						int exp_level = -1;
						if (asteroid_drill.UseSpecialistBonus)
						{
							foreach (ProtoCrewMember c in Lib.CrewList(v))
							{
								if (c.experienceTrait.Effects.Find(k => k.Name == asteroid_drill.ExperienceEffect) != null)
								{
									exp_level = Math.Max(exp_level, c.experienceLevel);
								}
							}
						}
						double exp_bonus = exp_level < 0
						? asteroid_drill.EfficiencyBonus * asteroid_drill.SpecialistBonusBase
						: asteroid_drill.EfficiencyBonus * (asteroid_drill.SpecialistBonusBase + (asteroid_drill.SpecialistEfficiencyFactor * (exp_level + 1)));

						// determine resource extracted
						double res_amount = abundance * asteroid_drill.Efficiency * exp_bonus * elapsed_s;

						// transform EC into mined resource
						Resource_recipe recipe = new Resource_recipe();
						recipe.Input("ElectricCharge", asteroid_drill.PowerConsumption * elapsed_s);
						recipe.Output(res_name, res_amount, true);
						resources.Transform(recipe);

						// if there was ec
						// note: comparing against amount in previous simulation step
						if (resources.Info(v, "ElectricCharge").amount > double.Epsilon)
						{
							// consume asteroid mass
							Lib.Proto.Set(asteroid_info, "currentMassVal", (mass - res_density * res_amount));
						}
					}
				}

				// undo stock behavior by forcing last_update_time to now
				Lib.Proto.Set(m, "lastUpdateTime", Planetarium.GetUniversalTime());
			}
		}


		static void ProcessStockLab(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, ModuleScienceConverter lab, Resource_info ec, double elapsed_s)
		{
			// note: we are only simulating the EC consumption
			// note: there is no easy way to 'stop' the lab when there isn't enough EC

			// if active
			if (Lib.Proto.GetBool(m, "IsActivated"))
			{
				// consume ec
				ec.Consume(lab.powerRequirement * elapsed_s);
			}
		}


		static void ProcessLight(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, ModuleLight light, Resource_info ec, double elapsed_s)
		{
			if (light.useResources && Lib.Proto.GetBool(m, "isOn"))
			{
				ec.Consume(light.resourceAmount * elapsed_s);
			}
		}

		static void ProcessScanner(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, PartModule scanner, Part part_prefab, VesselData vd, Resource_info ec, double elapsed_s)
		{
			// get ec consumption rate
			double power = SCANsat.EcConsumption(scanner);

			// if the scanner doesn't require power to operate, we aren't interested in simulating it
			if (power <= double.Epsilon) return;

			// get scanner state
			bool is_scanning = Lib.Proto.GetBool(m, "scanning");

			// if its scanning
			if (is_scanning)
			{
				// consume ec
				ec.Consume(power * elapsed_s);

				// if there isn't ec
				// - comparing against amount in previous simulation step
				if (ec.amount <= double.Epsilon)
				{
					// unregister scanner
					SCANsat.StopScanner(v, m, part_prefab);
					is_scanning = false;

					// remember disabled scanner
					vd.scansat_id.Add(p.flightID);

					// give the user some feedback
					if (vd.cfg_ec) Message.Post(Lib.BuildString("SCANsat sensor was disabled on <b>", v.vesselName, "</b>"));
				}
			}
			// if it was disabled in background
			else if (vd.scansat_id.Contains(p.flightID))
			{
				// if there is enough ec
				// note: comparing against amount in previous simulation step
				if (ec.level > 0.25) //< re-enable at 25% EC
				{
					// re-enable the scanner
					SCANsat.ResumeScanner(v, m, part_prefab);
					is_scanning = true;

					// give the user some feedback
					if (vd.cfg_ec) Message.Post(Lib.BuildString("SCANsat sensor resumed operations on <b>", v.vesselName, "</b>"));
				}
			}

			// forget active scanners
			if (is_scanning) vd.scansat_id.Remove(p.flightID);
		}


		static void ProcessCurvedPanel(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, PartModule curved_panel, Part part_prefab, Vessel_info info, Resource_info ec, double elapsed_s)
		{
			// note: we assume deployed, this is a current limitation

			// if in sunlight
			if (info.sunlight > double.Epsilon)
			{
				// get values from module
				string transform_name = Lib.ReflectionValue<string>(curved_panel, "PanelTransformName");
				float tot_rate = Lib.ReflectionValue<float>(curved_panel, "TotalEnergyRate");

				// get components
				Transform[] components = part_prefab.FindModelTransforms(transform_name);
				if (components.Length == 0) return;

				// calculate normalized solar flux
				// note: this include fractional sunlight if integrated over orbit
				// note: this include atmospheric absorption if inside an atmosphere
				double norm_solar_flux = info.solar_flux / Sim.SolarFluxAtHome();

				// calculate rate per component
				double rate = (double)tot_rate / (double)components.Length;

				// calculate world-space part rotation quaternion
				// note: a possible optimization here is to cache the transform lookup (unity was coded by monkeys)
				Quaternion rot = v.transform.rotation * p.rotation;

				// calculate output of all components
				double output = 0.0;
				foreach (Transform t in components)
				{
					output += rate                                                                     // nominal rate per-component at 1 AU
							* norm_solar_flux                                                          // normalized solar flux at panel distance from sun
							* Math.Max(Vector3d.Dot(info.sun_dir, (rot * t.forward).normalized), 0.0); // cosine factor of component orientation
				}

				// produce EC
				ec.Produce(output * elapsed_s);
			}
		}


		static void ProcessFissionGenerator(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, PartModule fission_generator, Resource_info ec, double elapsed_s)
		{
			// note: ignore heat

			double power = Lib.ReflectionValue<float>(fission_generator, "PowerGeneration");
			var reactor = p.modules.Find(k => k.moduleName == "FissionReactor");
			double tweakable = reactor == null ? 1.0 : Lib.ConfigValue(reactor.moduleValues, "CurrentPowerPercent", 100.0) * 0.01;
			ec.Produce(power * tweakable * elapsed_s);
		}


		static void ProcessRadioisotopeGenerator(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, PartModule radioisotope_generator, Resource_info ec, double elapsed_s)
		{
			// note: doesn't support easy mode

			double power = Lib.ReflectionValue<float>(radioisotope_generator, "BasePower");
			double half_life = Lib.ReflectionValue<float>(radioisotope_generator, "HalfLife");
			double mission_time = v.missionTime / (3600.0 * Lib.HoursInDay() * Lib.DaysInYear());
			double remaining = Math.Pow(2.0, (-mission_time) / half_life);
			ec.Produce(power * remaining * elapsed_s);
		}


		static void ProcessCryoTank(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, PartModule cryotank, Vessel_resources resources, Resource_info ec, double elapsed_s)
		{
			// Note. Currently background simulation of Cryotanks has an irregularity in that boiloff of a fuel type in a tank removes resources from all tanks
			// but at least some simulation is better than none ;)

			// get list of fuels, do nothing if no fuels
			IList fuels = Lib.ReflectionValue<IList>(cryotank, "fuels");
			if (fuels == null) return;

			// is cooling available, note: comparing against amount in previous simulation step
			bool available = (Lib.Proto.GetBool(m, "CoolingEnabled") && ec.amount > double.Epsilon);

			// get cooling cost
			double cooling_cost = Lib.ReflectionValue<float>(cryotank, "CoolingCost");

			string fuel_name = "";
			double amount = 0.0;
			double total_cost = 0.0;
			double boiloff_rate = 0.0;

			foreach (var item in fuels)
			{
				fuel_name = Lib.ReflectionValue<string>(item, "fuelName");
				// if fuel_name is null, don't do anything
				if (fuel_name == null)
					continue;

				//get fuel resource
				Resource_info fuel = resources.Info(v, fuel_name);

				// if there is some fuel
				// note: comparing against amount in previous simulation step
				if (fuel.amount > double.Epsilon)
				{
					// Try to find resource "fuel_name" in PartResources
					ProtoPartResourceSnapshot proto_fuel = p.resources.Find(k => k.resourceName == fuel_name);

					// If part doesn't have the fuel, don't do anything.
					if (proto_fuel == null) continue;

					// get amount in the part
					amount = proto_fuel.amount;

					// if cooling is enabled and there is enough EC
					if (available)
					{
						// calculate ec consumption
						total_cost += cooling_cost * amount * 0.001;
					}
					// if cooling is disabled or there wasn't any EC
					else
					{
						// get boiloff rate per-second
						boiloff_rate = Lib.ReflectionValue<float>(item, "boiloffRate") / 360000.0f;

						// let it boil off
						fuel.Consume(amount * (1.0 - Math.Pow(1.0 - boiloff_rate, elapsed_s)));
					}
				}
			}

			// apply EC consumption
			ec.Consume(total_cost * elapsed_s);
		}
	}


} // KERBALISM

