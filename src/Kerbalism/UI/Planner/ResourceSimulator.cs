﻿using System;
using System.Collections;
using System.Collections.Generic;
using ModuleWheels;

namespace KERBALISM.Planner
{

	///<summary> Planners simulator for resources contained, produced and consumed within the vessel </summary>
	public class ResourceSimulator
	{
		/// <summary>
		/// run simulator to get statistics a fraction of a second after the vessel would spawn
		/// in the configured environment (celestial body, orbit height and presence of sunlight)
		/// </summary>
		public void Analyze(List<Part> parts, EnvironmentAnalyzer env, VesselAnalyzer va)
		{
			// reach steady state, so all initial resources like WasteAtmosphere are produced
			// it is assumed that one cycle is needed to produce things that don't need inputs
			// another cycle is needed for processes to pick that up
			// another cycle may be needed for results of those processes to be picked up
			// two additional cycles are for having some margin
			for (int i = 0; i < 5; i++)
			{
				RunSimulator(parts, env, va);
			}

			// Do the actual run people will see from the simulator UI
			foreach (SimulatedResource r in resources.Values)
			{
				r.ResetSimulatorDisplayValues();
			}
			RunSimulator(parts, env, va);
		}

		/// <summary>run a single timestamp of the simulator</simulator>
		private void RunSimulator(List<Part> parts, EnvironmentAnalyzer env, VesselAnalyzer va)
		{
			// clear previous resource state
			resources.Clear();

			// get amount and capacity from parts
			foreach (Part p in parts)
			{
				for (int i = 0; i < p.Resources.Count; ++i)
				{
					Process_part(p, p.Resources[i].resourceName);
				}
			}

			// process all rules
			foreach (Rule r in Profile.rules)
			{
				if ((r.input.Length > 0 || (r.monitor && r.output.Length > 0)) && r.rate > 0.0)
				{
					Process_rule(parts, r, env, va);
				}
			}

			// process all processes
			foreach (Process p in Profile.processes)
			{
				Process_process(parts, p, env, va);
			}

			// process all modules
			foreach (Part p in parts)
			{
				// get planner controller in the part
				PlannerController ctrl = p.FindModuleImplementing<PlannerController>();

				// ignore all modules in the part if specified in controller
				if (ctrl != null && !ctrl.considered)
					continue;

				// for each module
				foreach (PartModule m in p.Modules)
				{
					// skip disabled modules
					// rationale: the Selector disable non-selected modules in this way
					if (!m.isEnabled)
						continue;

					switch (m.moduleName)
					{
						case "Greenhouse":
							Process_greenhouse(m as Greenhouse, env, va);
							break;
						case "GravityRing":
							Process_ring(m as GravityRing);
							break;
						case "Emitter":
							Process_emitter(m as Emitter);
							break;
						case "Laboratory":
							Process_laboratory(m as Laboratory);
							break;
						case "Experiment":
							Process_experiment(m as Experiment);
							break;
						case "ModuleCommand":
							Process_command(m as ModuleCommand);
							break;
						case "ModuleDeployableSolarPanel":
							Process_panel(m as ModuleDeployableSolarPanel, env);
							break;
						case "ModuleGenerator":
							Process_generator(m as ModuleGenerator, p);
							break;
						case "ModuleResourceConverter":
							Process_converter(m as ModuleResourceConverter, va);
							break;
						case "ModuleKPBSConverter":
							Process_converter(m as ModuleResourceConverter, va);
							break;
						case "ModuleResourceHarvester":
							Process_harvester(m as ModuleResourceHarvester, va);
							break;
						case "ModuleScienceConverter":
							Process_stocklab(m as ModuleScienceConverter);
							break;
						case "ModuleActiveRadiator":
							Process_radiator(m as ModuleActiveRadiator);
							break;
						case "ModuleWheelMotor":
							Process_wheel_motor(m as ModuleWheelMotor);
							break;
						case "ModuleWheelMotorSteering":
							Process_wheel_steering(m as ModuleWheelMotorSteering);
							break;
						case "ModuleLight":
							Process_light(m as ModuleLight);
							break;
						case "ModuleColoredLensLight":
							Process_light(m as ModuleLight);
							break;
						case "ModuleMultiPointSurfaceLight":
							Process_light(m as ModuleLight);
							break;
						case "SCANsat":
							Process_scanner(m);
							break;
						case "ModuleSCANresourceScanner":
							Process_scanner(m);
							break;
						case "ModuleCurvedSolarPanel":
							Process_curved_panel(p, m, env);
							break;
						case "FissionGenerator":
							Process_fission_generator(p, m);
							break;
						case "ModuleRadioisotopeGenerator":
							Process_radioisotope_generator(p, m);
							break;
						case "ModuleCryoTank":
							Process_cryotank(p, m);
							break;
						case "ModuleRTAntennaPassive":
						case "ModuleRTAntenna":
							Process_rtantenna(m);
							break;
						case "ModuleDataTransmitter":
							Process_datatransmitter(m as ModuleDataTransmitter);
							break;
						case "ModuleEngines":
							Process_engines(m as ModuleEngines);
							break;
						case "ModuleEnginesFX":
							Process_enginesfx(m as ModuleEnginesFX);
							break;
						case "ModuleRCS":
							Process_rcs(m as ModuleRCS);
							break;
						case "ModuleRCSFX":
							Process_rcsfx(m as ModuleRCSFX);
							break;
					}
				}
			}

			// execute all possible recipes
			bool executing = true;
			while (executing)
			{
				executing = false;
				for (int i = 0; i < recipes.Count; ++i)
				{
					SimulatedRecipe recipe = recipes[i];
					if (recipe.left > double.Epsilon)
					{
						executing |= recipe.Execute(this);
					}
				}
			}
			recipes.Clear();

			// clamp all resources
			foreach (KeyValuePair<string, SimulatedResource> pair in resources)
				pair.Value.Clamp();
		}

		/// <summary>obtain information on resource metrics for any resource contained within simulated vessel</simulator>
		public SimulatedResource Resource(string name)
		{
			SimulatedResource res;
			if (!resources.TryGetValue(name, out res))
			{
				res = new SimulatedResource(name);
				resources.Add(name, res);
			}
			return res;
		}

		/// <summary>transfer per-part resources to the simulator</simulator>
		void Process_part(Part p, string res_name)
		{
			SimulatedResourceView res = Resource(res_name).GetSimulatedResourceView(p);
			res.AddPartResources(p);
		}

		/// <summary>process a rule and add/remove the resources from the simulator</simulator>
		private void Process_rule_inner_body(double k, Part p, Rule r, EnvironmentAnalyzer env, VesselAnalyzer va)
		{
			// deduce rate per-second
			double rate = va.crew_count * (r.interval > 0.0 ? r.rate / r.interval : r.rate);

			// prepare recipe
			if (r.output.Length == 0)
			{
				Resource(r.input).Consume(rate * k, r.name);
			}
			else if (rate > double.Epsilon)
			{
				// simulate recipe if output_only is false
				if (!r.monitor)
				{
					// - rules always dump excess overboard (because it is waste)
					SimulatedRecipe recipe = new SimulatedRecipe(p, r.name);
					recipe.Input(r.input, rate * k);
					recipe.Output(r.output, rate * k * r.ratio, true);
					recipes.Add(recipe);
				}
				// only simulate output
				else
				{
					Resource(r.output).Produce(rate * k, r.name);
				}
			}
		}

		/// <summary>determine if the resources involved are restricted to a part, and then process a rule</summary>
		public void Process_rule(List<Part> parts, Rule r, EnvironmentAnalyzer env, VesselAnalyzer va)
		{
			// evaluate modifiers
			double k = Modifiers.Evaluate(env, va, this, r.modifiers);
			Process_rule_inner_body(k, null, r, env, va);
		}

		/// <summary>process the process and add/remove the resources from the simulator</summary>
		private void Process_process_inner_body(double k, Part p, Process pr, EnvironmentAnalyzer env, VesselAnalyzer va)
		{
			// prepare recipe
			SimulatedRecipe recipe = new SimulatedRecipe(p, pr.name);
			foreach (KeyValuePair<string, double> input in pr.inputs)
			{
				recipe.Input(input.Key, input.Value * k);
			}
			foreach (KeyValuePair<string, double> output in pr.outputs)
			{
				recipe.Output(output.Key, output.Value * k, pr.dump.Check(output.Key));
			}
			recipes.Add(recipe);
		}

		/// <summary>process the process and add/remove the resources from the simulator for the entire vessel at once</summary>
		private void Process_process_vessel_wide(Process pr, EnvironmentAnalyzer env, VesselAnalyzer va)
		{
			// evaluate modifiers
			double k = Modifiers.Evaluate(env, va, this, pr.modifiers);
			Process_process_inner_body(k, null, pr, env, va);
		}

		/// <summary>
		/// determine if the resources involved are restricted to a part, and then process
		/// the process and add/remove the resources from the simulator
		/// </summary>
		/// <remarks>while rules are usually input or output only, processes transform input to output</remarks>
		public void Process_process(List<Part> parts, Process pr, EnvironmentAnalyzer env, VesselAnalyzer va)
		{
			Process_process_vessel_wide(pr, env, va);
		}

		void Process_greenhouse(Greenhouse g, EnvironmentAnalyzer env, VesselAnalyzer va)
		{
			// skip disabled greenhouses
			if (!g.active)
				return;

			// shortcut to resources
			SimulatedResource ec = Resource("ElectricCharge");
			SimulatedResource res = Resource(g.crop_resource);

			// calculate natural and artificial lighting
			double natural = env.solar_flux;
			double artificial = Math.Max(g.light_tolerance - natural, 0.0);

			// if lamps are on and artificial lighting is required
			if (artificial > 0.0)
			{
				// consume ec for the lamps
				ec.Consume(g.ec_rate * (artificial / g.light_tolerance), "greenhouse");
			}

			// execute recipe
			SimulatedRecipe recipe = new SimulatedRecipe(g.part, "greenhouse");
			foreach (ModuleResource input in g.resHandler.inputResources)
			{
				// WasteAtmosphere is primary combined input
				if (g.WACO2 && input.name == "WasteAtmosphere")
					recipe.Input(input.name, env.breathable ? 0.0 : input.rate, "CarbonDioxide");
				// CarbonDioxide is secondary combined input
				else if (g.WACO2 && input.name == "CarbonDioxide")
					recipe.Input(input.name, env.breathable ? 0.0 : input.rate, "");
				// if atmosphere is breathable disable WasteAtmosphere / CO2
				else if (!g.WACO2 && (input.name == "CarbonDioxide" || input.name == "WasteAtmosphere"))
					recipe.Input(input.name, env.breathable ? 0.0 : input.rate, "");
				else
					recipe.Input(input.name, input.rate);
			}
			foreach (ModuleResource output in g.resHandler.outputResources)
			{
				// if atmosphere is breathable disable Oxygen
				if (output.name == "Oxygen")
					recipe.Output(output.name, env.breathable ? 0.0 : output.rate, true);
				else
					recipe.Output(output.name, output.rate, true);
			}
			recipes.Add(recipe);

			// determine environment conditions
			bool lighting = natural + artificial >= g.light_tolerance;
			bool pressure = va.pressurized || g.pressure_tolerance <= double.Epsilon;
			bool radiation = (env.landed ? env.surface_rad : env.magnetopause_rad) * (1.0 - va.shielding) < g.radiation_tolerance;

			// if all conditions apply
			// note: we are assuming the inputs are satisfied, we can't really do otherwise here
			if (lighting && pressure && radiation)
			{
				// produce food
				res.Produce(g.crop_size * g.crop_rate, "greenhouse");

				// add harvest info
				res.harvests.Add(Lib.BuildString(g.crop_size.ToString("F0"), " in ", Lib.HumanReadableDuration(1.0 / g.crop_rate)));
			}
		}


		void Process_ring(GravityRing ring)
		{
			if (ring.deployed)
				Resource("ElectricCharge").Consume(ring.ec_rate, "gravity ring");
		}


		void Process_emitter(Emitter emitter)
		{
			if (emitter.running)
				Resource("ElectricCharge").Consume(emitter.ec_rate, "emitter");
		}


		void Process_laboratory(Laboratory lab)
		{
			// note: we are not checking if there is a scientist in the part
			if (lab.running)
			{
				Resource("ElectricCharge").Consume(lab.ec_rate, "laboratory");
			}
		}


		void Process_experiment(Experiment exp)
		{
			if (exp.recording)
			{
				Resource("ElectricCharge").Consume(exp.ec_rate, exp.sample_mass < float.Epsilon ? "sensor" : "experiment");
			}
		}


		void Process_command(ModuleCommand command)
		{
			foreach (ModuleResource res in command.resHandler.inputResources)
			{
				Resource(res.name).Consume(res.rate, "command");
			}
		}


		void Process_panel(ModuleDeployableSolarPanel panel, EnvironmentAnalyzer env)
		{
			double generated = panel.resHandler.outputResources[0].rate * env.solar_flux / Sim.SolarFluxAtHome();
			Resource("ElectricCharge").Produce(generated, "solar panel");
		}


		void Process_generator(ModuleGenerator generator, Part p)
		{
			// skip launch clamps, that include a generator
			if (Lib.PartName(p) == "launchClamp1")
				return;

			SimulatedRecipe recipe = new SimulatedRecipe(p, "generator");
			foreach (ModuleResource res in generator.resHandler.inputResources)
			{
				recipe.Input(res.name, res.rate);
			}
			foreach (ModuleResource res in generator.resHandler.outputResources)
			{
				recipe.Output(res.name, res.rate, true);
			}
			recipes.Add(recipe);
		}


		void Process_converter(ModuleResourceConverter converter, VesselAnalyzer va)
		{
			// calculate experience bonus
			float exp_bonus = converter.UseSpecialistBonus
			  ? converter.EfficiencyBonus * (converter.SpecialistBonusBase + (converter.SpecialistEfficiencyFactor * (va.crew_engineer_maxlevel + 1)))
			  : 1.0f;

			// use part name as recipe name
			// - include crew bonus in the recipe name
			string recipe_name = Lib.BuildString(converter.part.partInfo.title, " (efficiency: ", Lib.HumanReadablePerc(exp_bonus), ")");

			// generate recipe
			SimulatedRecipe recipe = new SimulatedRecipe(converter.part, recipe_name);
			foreach (ResourceRatio res in converter.inputList)
			{
				recipe.Input(res.ResourceName, res.Ratio * exp_bonus);
			}
			foreach (ResourceRatio res in converter.outputList)
			{
				recipe.Output(res.ResourceName, res.Ratio * exp_bonus, res.DumpExcess);
			}
			recipes.Add(recipe);
		}


		void Process_harvester(ModuleResourceHarvester harvester, VesselAnalyzer va)
		{
			// calculate experience bonus
			float exp_bonus = harvester.UseSpecialistBonus
			  ? harvester.EfficiencyBonus * (harvester.SpecialistBonusBase + (harvester.SpecialistEfficiencyFactor * (va.crew_engineer_maxlevel + 1)))
			  : 1.0f;

			// use part name as recipe name
			// - include crew bonus in the recipe name
			string recipe_name = Lib.BuildString(harvester.part.partInfo.title, " (efficiency: ", Lib.HumanReadablePerc(exp_bonus), ")");

			// generate recipe
			SimulatedRecipe recipe = new SimulatedRecipe(harvester.part, recipe_name);
			foreach (ResourceRatio res in harvester.inputList)
			{
				recipe.Input(res.ResourceName, res.Ratio);
			}
			recipe.Output(harvester.ResourceName, harvester.Efficiency * exp_bonus, true);
			recipes.Add(recipe);
		}


		void Process_stocklab(ModuleScienceConverter lab)
		{
			Resource("ElectricCharge").Consume(lab.powerRequirement, "lab");
		}


		void Process_radiator(ModuleActiveRadiator radiator)
		{
			// note: IsCooling is not valid in the editor, for deployable radiators,
			// we will have to check if the related deploy module is deployed
			// we use PlannerController instead
			foreach (ModuleResource res in radiator.resHandler.inputResources)
			{
				Resource(res.name).Consume(res.rate, "radiator");
			}
		}


		void Process_wheel_motor(ModuleWheelMotor motor)
		{
			foreach (ModuleResource res in motor.resHandler.inputResources)
			{
				Resource(res.name).Consume(res.rate, "wheel");
			}
		}


		void Process_wheel_steering(ModuleWheelMotorSteering steering)
		{
			foreach (ModuleResource res in steering.resHandler.inputResources)
			{
				Resource(res.name).Consume(res.rate, "wheel");
			}
		}


		void Process_light(ModuleLight light)
		{
			if (light.useResources && light.isOn)
			{
				Resource("ElectricCharge").Consume(light.resourceAmount, "light");
			}
		}


		void Process_scanner(PartModule m)
		{
			Resource("ElectricCharge").Consume(SCANsat.EcConsumption(m), "SCANsat");
		}


		void Process_curved_panel(Part p, PartModule m, EnvironmentAnalyzer env)
		{
			// note: assume half the components are in sunlight, and average inclination is half

			// get total rate
			double tot_rate = Lib.ReflectionValue<float>(m, "TotalEnergyRate");

			// get number of components
			int components = p.FindModelTransforms(Lib.ReflectionValue<string>(m, "PanelTransformName")).Length;

			// approximate output
			// 0.7071: average clamped cosine
			Resource("ElectricCharge").Produce(tot_rate * 0.7071 * env.solar_flux / Sim.SolarFluxAtHome(), "curved panel");
		}


		void Process_fission_generator(Part p, PartModule m)
		{
			double max_rate = Lib.ReflectionValue<float>(m, "PowerGeneration");

			// get fission reactor tweakable, will default to 1.0 for other modules
			ModuleResourceConverter reactor = p.FindModuleImplementing<ModuleResourceConverter>();
			double tweakable = reactor == null ? 1.0 : Lib.ReflectionValue<float>(reactor, "CurrentPowerPercent") * 0.01f;

			Resource("ElectricCharge").Produce(max_rate * tweakable, "fission generator");
		}


		void Process_radioisotope_generator(Part p, PartModule m)
		{
			double max_rate = Lib.ReflectionValue<float>(m, "BasePower");

			Resource("ElectricCharge").Produce(max_rate, "radioisotope generator");
		}


		void Process_cryotank(Part p, PartModule m)
		{
			// is cooling available
			bool available = Lib.ReflectionValue<bool>(m, "CoolingEnabled");

			// get list of fuels, do nothing if no fuels
			IList fuels = Lib.ReflectionValue<IList>(m, "fuels");
			if (fuels == null)
				return;

			// get cooling cost
			double cooling_cost = Lib.ReflectionValue<float>(m, "CoolingCost");

			string fuel_name = "";
			double amount = 0.0;
			double total_cost = 0.0;
			double boiloff_rate = 0.0;

			// calculate EC cost of cooling
			foreach (object fuel in fuels)
			{
				fuel_name = Lib.ReflectionValue<string>(fuel, "fuelName");
				// if fuel_name is null, don't do anything
				if (fuel_name == null)
					continue;

				// get amount in the part
				amount = Lib.Amount(p, fuel_name);

				// if there is some fuel
				if (amount > double.Epsilon)
				{
					// if cooling is enabled
					if (available)
					{
						// calculate ec consumption
						total_cost += cooling_cost * amount * 0.001;
					}
					// if cooling is disabled
					else
					{
						// get boiloff rate per-second
						boiloff_rate = Lib.ReflectionValue<float>(fuel, "boiloffRate") / 360000.0f;

						// let it boil off
						Resource(fuel_name).Consume(amount * boiloff_rate, "cryotank");
					}
				}
			}

			// apply EC consumption
			Resource("ElectricCharge").Consume(total_cost, "cryotank");
		}


		void Process_rtantenna(PartModule m)
		{
			switch (m.moduleName)
			{
				case "ModuleRTAntennaPassive":
					Resource("ElectricCharge").Consume(0.0005, "communications (control)");   // 3km range needs approx 0.5 Watt
					break;
				case "ModuleRTAntenna":
					Resource("ElectricCharge").Consume(m.resHandler.inputResources.Find(r => r.name == "ElectricCharge").rate, "communications (transmitting)");
					break;
			}
		}

		void Process_datatransmitter(ModuleDataTransmitter mdt)
		{
			switch (mdt.antennaType)
			{
				case AntennaType.INTERNAL:
					Resource("ElectricCharge").Consume(mdt.DataResourceCost * mdt.DataRate, "communications (control)");
					break;
				default:
					Resource("ElectricCharge").Consume(mdt.DataResourceCost * mdt.DataRate, "communications (transmitting)");
					break;
			}
		}

		void Process_engines(ModuleEngines me)
		{
			// calculate thrust fuel flow
			double thrust_flow = me.maxFuelFlow * 1e3 * me.thrustPercentage;

			// search fuel types
			foreach (Propellant fuel in me.propellants)
			{
				switch (fuel.name)
				{
					case "ElectricCharge":  // mainly used for Ion Engines
						Resource("ElectricCharge").Consume(thrust_flow * fuel.ratio, "engines");
						break;
					case "LqdHydrogen":     // added for cryotanks and any other supported mod that uses Liquid Hydrogen
						Resource("LqdHydrogen").Consume(thrust_flow * fuel.ratio, "engines");
						break;
				}
			}
		}

		void Process_enginesfx(ModuleEnginesFX mefx)
		{
			// calculate thrust fuel flow
			double thrust_flow = mefx.maxFuelFlow * 1e3 * mefx.thrustPercentage;

			// search fuel types
			foreach (Propellant fuel in mefx.propellants)
			{
				switch (fuel.name)
				{
					case "ElectricCharge":  // mainly used for Ion Engines
						Resource("ElectricCharge").Consume(thrust_flow * fuel.ratio, "engines");
						break;
					case "LqdHydrogen":     // added for cryotanks and any other supported mod that uses Liquid Hydrogen
						Resource("LqdHydrogen").Consume(thrust_flow * fuel.ratio, "engines");
						break;
				}
			}
		}

		void Process_rcs(ModuleRCS mr)
		{
			// calculate thrust fuel flow
			double thrust_flow = mr.maxFuelFlow * 1e3 * mr.thrustPercentage * mr.thrusterPower;

			// search fuel types
			foreach (Propellant fuel in mr.propellants)
			{
				switch (fuel.name)
				{
					case "ElectricCharge":  // mainly used for Ion RCS
						Resource("ElectricCharge").Consume(thrust_flow * fuel.ratio, "rcs");
						break;
					case "LqdHydrogen":     // added for cryotanks and any other supported mod that uses Liquid Hydrogen
						Resource("LqdHydrogen").Consume(thrust_flow * fuel.ratio, "rcs");
						break;
				}
			}
		}

		void Process_rcsfx(ModuleRCSFX mrfx)
		{
			// calculate thrust fuel flow
			double thrust_flow = mrfx.maxFuelFlow * 1e3 * mrfx.thrustPercentage * mrfx.thrusterPower;

			// search fuel types
			foreach (Propellant fuel in mrfx.propellants)
			{
				switch (fuel.name)
				{
					case "ElectricCharge":  // mainly used for Ion RCS
						Resource("ElectricCharge").Consume(thrust_flow * fuel.ratio, "rcs");
						break;
					case "LqdHydrogen":     // added for cryotanks and any other supported mod that uses Liquid Hydrogen
						Resource("LqdHydrogen").Consume(thrust_flow * fuel.ratio, "rcs");
						break;
				}
			}
		}

		Dictionary<string, SimulatedResource> resources = new Dictionary<string, SimulatedResource>();
		List<SimulatedRecipe> recipes = new List<SimulatedRecipe>();
	}


} // KERBALISM
