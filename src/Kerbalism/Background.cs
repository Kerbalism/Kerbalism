using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{
	public static class Background
	{
		public enum Module_type
		{
			Reliability = 0,
			Experiment,
			Greenhouse,
			GravityRing,
			Harvester,
			Emitter,
			Laboratory,
			Command,
			Generator,
			Converter,
			Drill,
			AsteroidDrill,
			StockLab,
			Light,
			Scanner,
			FissionGenerator,
			RadioisotopeGenerator,
			CryoTank,
			Unknown,
			FNGenerator,
			NonRechargeBattery,
			KerbalismProcess,
			SolarPanelFixer
		}

		public static Module_type ModuleType(string module_name)
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
				case "KerbalismScansat": return Module_type.Scanner;
				case "FissionGenerator": return Module_type.FissionGenerator;
				case "ModuleRadioisotopeGenerator": return Module_type.RadioisotopeGenerator;
				case "ModuleCryoTank": return Module_type.CryoTank;
				case "FNGenerator": return Module_type.FNGenerator;
				case "KerbalismProcess": return Module_type.KerbalismProcess;
				case "SolarPanelFixer": return Module_type.SolarPanelFixer;
			}
			return Module_type.Unknown;
		}

		internal class BackgroundPM
		{
			internal ProtoPartSnapshot p;
			internal ProtoPartModuleSnapshot m;
			internal PartModule module_prefab;
			internal Part part_prefab;
			internal Module_type type;
		}

		public static void Update(Vessel v, VesselData vd, VesselResources resources, double elapsed_s)
		{
			if (!Lib.IsVessel(v))
				return;

			// get most used resource handlers
			ResourceInfo ec = resources.GetResource(v, "ElectricCharge");

			foreach(var e in Background_PMs(v))
			{
				switch(e.type)
				{
					case Module_type.Reliability: Reliability.BackgroundUpdate(v, e.p, e.m, e.module_prefab as Reliability, elapsed_s); break;
					case Module_type.Experiment: (e.module_prefab as Experiment).BackgroundUpdate(v, vd, e.m, ec, resources, elapsed_s); break; // experiments use the prefab as a singleton instead of a static method
					case Module_type.Greenhouse: Greenhouse.BackgroundUpdate(v, e.m, e.module_prefab as Greenhouse, vd, resources, elapsed_s); break;
					case Module_type.GravityRing: GravityRing.BackgroundUpdate(v, e.p, e.m, e.module_prefab as GravityRing, ec, elapsed_s); break;
					case Module_type.Emitter: Emitter.BackgroundUpdate(v, e.p, e.m, e.module_prefab as Emitter, ec, elapsed_s); break;
					case Module_type.Harvester: Harvester.BackgroundUpdate(v, e.m, e.module_prefab as Harvester, elapsed_s); break; // Kerbalism ground and air harvester module
					case Module_type.Laboratory: Laboratory.BackgroundUpdate(v, e.p, e.m, e.module_prefab as Laboratory, ec, elapsed_s); break;
					case Module_type.Command: ProcessCommand(v, e.p, e.m, e.module_prefab as ModuleCommand, resources, elapsed_s); break;
					case Module_type.Generator: ProcessGenerator(v, e.p, e.m, e.module_prefab as ModuleGenerator, resources, elapsed_s); break;
					case Module_type.Converter: ProcessConverter(v, e.p, e.m, e.module_prefab as ModuleResourceConverter, resources, elapsed_s); break;
					case Module_type.Drill: ProcessDrill(v, e.p, e.m, e.module_prefab as ModuleResourceHarvester, resources, elapsed_s); break; // Stock ground harvester module
					case Module_type.AsteroidDrill: ProcessAsteroidDrill(v, e.p, e.m, e.module_prefab as ModuleAsteroidDrill, resources, elapsed_s); break; // Stock asteroid harvester module
					case Module_type.StockLab: ProcessStockLab(v, e.p, e.m, e.module_prefab as ModuleScienceConverter, ec, elapsed_s); break;
					case Module_type.Light: ProcessLight(v, e.p, e.m, e.module_prefab as ModuleLight, ec, elapsed_s); break;
					case Module_type.Scanner: KerbalismScansat.BackgroundUpdate(v, e.p, e.m, e.module_prefab as KerbalismScansat, e.part_prefab, vd, ec, elapsed_s); break;
					case Module_type.FissionGenerator: ProcessFissionGenerator(v, e.p, e.m, e.module_prefab, ec, elapsed_s); break;
					case Module_type.RadioisotopeGenerator: ProcessRadioisotopeGenerator(v, e.p, e.m, e.module_prefab, ec, elapsed_s); break;
					case Module_type.CryoTank: ProcessCryoTank(v, e.p, e.m, e.module_prefab, resources, ec, elapsed_s); break;
					case Module_type.FNGenerator: ProcessFNGenerator(v, e.p, e.m, e.module_prefab, ec, elapsed_s); break;
					case Module_type.SolarPanelFixer: SolarPanelFixer.BackgroundUpdate(v, e.m, e.module_prefab as SolarPanelFixer, vd, ec, elapsed_s); break;
				}
			}
		}

		private static List<BackgroundPM> Background_PMs(Vessel v)
		{
			var result = Cache.VesselObjectsCache<List<BackgroundPM>>(v, "background");
			if (result != null)
				return result;

			result = new List<BackgroundPM>();

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
					// TODO : this is to migrate pre-3.1 saves using WarpFixer to the new SolarPanelFixer. At some point in the future we can remove this code.
					if (m.moduleName == "WarpFixer") MigrateWarpFixer(v, part_prefab, p, m);

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

					var entry = new BackgroundPM();
					entry.p = p;
					entry.m = m;
					entry.module_prefab = module_prefab;
					entry.part_prefab = part_prefab;
					entry.type = type;
					result.Add(entry);
				}
			}

			Cache.SetVesselObjectsCache(v, "background", result);
			return result;
		}

		static void ProcessFNGenerator(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, PartModule fission_generator, ResourceInfo ec, double elapsed_s)
		{
			string maxPowerStr = Lib.Proto.GetString(m, "MaxPowerStr");
			double maxPower = 0;
			if (maxPowerStr.Contains("GW"))
				maxPower = double.Parse(maxPowerStr.Replace(" GW", "")) * 1000000;
			else if (maxPowerStr.Contains("MW"))
				maxPower = double.Parse(maxPowerStr.Replace(" MW", "")) * 1000;
			else
				maxPower = double.Parse(maxPowerStr.Replace(" KW", ""));

			ec.Produce(maxPower * elapsed_s, "KSPIE generator");
		}

		static void ProcessCommand(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, ModuleCommand command, VesselResources resources, double elapsed_s)
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
					resources.Consume(v, ir.name, ir.rate * elapsed_s, "command");
				}
			}
		}

		static void ProcessGenerator(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, ModuleGenerator generator, VesselResources resources, double elapsed_s)
		{
			// if active
			if (Lib.Proto.GetBool(m, "generatorIsActive"))
			{
				// create and commit recipe
				ResourceRecipe recipe = new ResourceRecipe(p, "generator");
				foreach (ModuleResource ir in generator.resHandler.inputResources)
				{
					recipe.AddInput(ir.name, ir.rate * elapsed_s);
				}
				foreach (ModuleResource or in generator.resHandler.outputResources)
				{
					recipe.AddOutput(or.name, or.rate * elapsed_s, true);
				}
				resources.AddRecipe(recipe);
			}
		}


		static void ProcessConverter(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, ModuleResourceConverter converter, VesselResources resources, double elapsed_s)
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
					ResourceInfo res = resources.GetResource(v, or.ResourceName);
					full &= (res.Level >= converter.FillAmount - double.Epsilon);
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
					ResourceRecipe recipe = new ResourceRecipe(p, "converter");
					foreach (var ir in converter.inputList)
					{
						recipe.AddInput(ir.ResourceName, ir.Ratio * exp_bonus * elapsed_s);
					}
					foreach (var or in converter.outputList)
					{
						recipe.AddOutput(or.ResourceName, or.Ratio * exp_bonus * elapsed_s, or.DumpExcess);
					}
					resources.AddRecipe(recipe);
				}

				// undo stock behavior by forcing last_update_time to now
				Lib.Proto.Set(m, "lastUpdateTime", Planetarium.GetUniversalTime());
			}
		}


		static void ProcessDrill(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, ModuleResourceHarvester harvester, VesselResources resources, double elapsed_s)
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
				if (resources.GetResource(v, harvester.ResourceName).Level < harvester.FillAmount - double.Epsilon)
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
						ResourceRecipe recipe = new ResourceRecipe(p, "drill");
						foreach (var ir in harvester.inputList)
						{
							recipe.AddInput(ir.ResourceName, ir.Ratio * elapsed_s);
						}
						recipe.AddOutput(harvester.ResourceName, abundance * harvester.Efficiency * exp_bonus * elapsed_s, true);
						resources.AddRecipe(recipe);
					}
				}

				// undo stock behavior by forcing last_update_time to now
				Lib.Proto.Set(m, "lastUpdateTime", Planetarium.GetUniversalTime());
			}
		}


		static void ProcessAsteroidDrill(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, ModuleAsteroidDrill asteroid_drill, VesselResources resources, double elapsed_s)
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
						ResourceRecipe recipe = new ResourceRecipe(p, "asteroidDrill");
						recipe.AddInput("ElectricCharge", asteroid_drill.PowerConsumption * elapsed_s);
						recipe.AddOutput(res_name, res_amount, true);
						resources.AddRecipe(recipe);

						// if there was ec
						// note: comparing against amount in previous simulation step
						if (resources.GetResource(v, "ElectricCharge").Amount > double.Epsilon)
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


		static void ProcessStockLab(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, ModuleScienceConverter lab, ResourceInfo ec, double elapsed_s)
		{
			// note: we are only simulating the EC consumption
			// note: there is no easy way to 'stop' the lab when there isn't enough EC

			// if active
			if (Lib.Proto.GetBool(m, "IsActivated"))
			{
				// consume ec
				ec.Consume(lab.powerRequirement * elapsed_s, "science converter");
			}
		}


		static void ProcessLight(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, ModuleLight light, ResourceInfo ec, double elapsed_s)
		{
			if (light.useResources && Lib.Proto.GetBool(m, "isOn"))
			{
				ec.Consume(light.resourceAmount * elapsed_s, "light");
			}
		}

		/*
		static void ProcessScanner(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, PartModule scanner, Part part_prefab, VesselData vd, ResourceInfo ec, double elapsed_s)
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
				ec.Consume(power * elapsed_s, "scanner");

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
				// re-enable at 25% EC
				if (ec.level > 0.25)
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
		*/

		static void ProcessFissionGenerator(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, PartModule fission_generator, ResourceInfo ec, double elapsed_s)
		{
			// note: ignore heat

			double power = Lib.ReflectionValue<float>(fission_generator, "PowerGeneration");
			var reactor = p.modules.Find(k => k.moduleName == "FissionReactor");
			double tweakable = reactor == null ? 1.0 : Lib.ConfigValue(reactor.moduleValues, "CurrentPowerPercent", 100.0) * 0.01;
			ec.Produce(power * tweakable * elapsed_s, "fission reactor");
		}


		static void ProcessRadioisotopeGenerator(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, PartModule radioisotope_generator, ResourceInfo ec, double elapsed_s)
		{
			// note: doesn't support easy mode

			double power = Lib.ReflectionValue<float>(radioisotope_generator, "BasePower");
			double half_life = Lib.ReflectionValue<float>(radioisotope_generator, "HalfLife");
			double mission_time = v.missionTime / (3600.0 * Lib.HoursInDay * Lib.DaysInYear);
			double remaining = Math.Pow(2.0, (-mission_time) / half_life);
			ec.Produce(power * remaining * elapsed_s, "RTG");
		}


		static void ProcessCryoTank(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, PartModule cryotank, VesselResources resources, ResourceInfo ec, double elapsed_s)
		{
			// Note. Currently background simulation of Cryotanks has an irregularity in that boiloff of a fuel type in a tank removes resources from all tanks
			// but at least some simulation is better than none ;)

			// get list of fuels, do nothing if no fuels
			IList fuels = Lib.ReflectionValue<IList>(cryotank, "fuels");
			if (fuels == null) return;

			// is cooling available, note: comparing against amount in previous simulation step
			bool available = (Lib.Proto.GetBool(m, "CoolingEnabled") && ec.Amount > double.Epsilon);

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
				ResourceInfo fuel = resources.GetResource(v, fuel_name);

				// if there is some fuel
				// note: comparing against amount in previous simulation step
				if (fuel.Amount > double.Epsilon)
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
						fuel.Consume(amount * (1.0 - Math.Pow(1.0 - boiloff_rate, elapsed_s)), "boiloff");
					}
				}
			}

			// apply EC consumption
			ec.Consume(total_cost * elapsed_s, "cryo tank");
		}

		// TODO : this is to migrate pre-3.1 saves using WarpFixer to the new SolarPanelFixer. At some point in the future we can remove this code.
		static void MigrateWarpFixer(Vessel v, Part prefab, ProtoPartSnapshot p, ProtoPartModuleSnapshot m)
		{
			ModuleDeployableSolarPanel panelModule = prefab.FindModuleImplementing<ModuleDeployableSolarPanel>();
			ProtoPartModuleSnapshot protoPanelModule = p.modules.Find(pm => pm.moduleName == "ModuleDeployableSolarPanel");

			if (panelModule == null || protoPanelModule == null)
			{
				Lib.Log("Vessel " + v.name + " has solar panels that can't be converted automatically following Kerbalism 3.1 update. Load it to fix the issue.");
				return;
			}

			SolarPanelFixer.PanelState state = SolarPanelFixer.PanelState.Unknown;
			string panelStateStr = Lib.Proto.GetString(protoPanelModule, "deployState");

			if (!Enum.IsDefined(typeof(ModuleDeployablePart.DeployState), panelStateStr)) return;
			ModuleDeployablePart.DeployState panelState = (ModuleDeployablePart.DeployState)Enum.Parse(typeof(ModuleDeployablePart.DeployState), panelStateStr);

			if (panelState == ModuleDeployablePart.DeployState.BROKEN)
				state = SolarPanelFixer.PanelState.Broken;
			else if (!panelModule.isTracking)
			{
				state = SolarPanelFixer.PanelState.Static;
			}
			else
			{
				switch (panelState)
				{
					case ModuleDeployablePart.DeployState.EXTENDED:
						if (!panelModule.retractable)
							state = SolarPanelFixer.PanelState.ExtendedFixed;
						else
							state = SolarPanelFixer.PanelState.Extended;
						break;
					case ModuleDeployablePart.DeployState.RETRACTED: state = SolarPanelFixer.PanelState.Retracted; break;
					case ModuleDeployablePart.DeployState.RETRACTING: state = SolarPanelFixer.PanelState.Retracting; break;
					case ModuleDeployablePart.DeployState.EXTENDING: state = SolarPanelFixer.PanelState.Extending; break;
					default: state = SolarPanelFixer.PanelState.Unknown; break;
				}
			}

			m.moduleName = "SolarPanelFixer";
			Lib.Proto.Set(m, "state", state);
			Lib.Proto.Set(m, "persistentFactor", 0.75);
			Lib.Proto.Set(m, "launchUT", Planetarium.GetUniversalTime());
			Lib.Proto.Set(m, "nominalRate", panelModule.chargeRate);
		}
	}
} // KERBALISM

