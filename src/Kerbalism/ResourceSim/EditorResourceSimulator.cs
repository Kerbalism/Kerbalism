using System;
using System.Collections;
using System.Reflection;
using System.Collections.Generic;
using ModuleWheels;

namespace KERBALISM.Planner
{

	///<summary> Planners simulator for resources contained, produced and consumed within the vessel </summary>
	public static class EditorResourceSimulator
	{
		// shortcuts
		private static VesselDataShip vd;
		private static VesselResHandler handler;

		/// <summary>
		/// run simulator to get statistics a fraction of a second after the vessel would spawn
		/// in the configured environment (celestial body, orbit height and presence of sunlight)
		/// </summary>
		public static void Analyze(List<Part> parts)
		{
			vd = VesselDataShip.Instance;
			handler = vd.ResHandler;

			// reset and re-find all resources amounts and capacities
			handler.ResourceUpdate(vd, null, VesselResHandler.VesselState.EditorInit, 1.0);

			// reach steady state, so all initial resources like WasteAtmosphere are produced
			// it is assumed that one cycle is needed to produce things that don't need inputs
			// another cycle is needed for processes to pick that up
			// another cycle may be needed for results of those processes to be picked up
			// two additional cycles are for having some margin
			for (int i = 0; i < 5; i++)
			{
				// do all produce/consume/recipe requests
				RunSimulatorStep(parts);
				// process them
				handler.ResourceUpdate(vd, null, VesselResHandler.VesselState.EditorStep, 1.0);
			}

			// set back all resources amounts to the stored amounts
			// this is for visualisation purposes, so the displayed amounts match the current values and not the resul of the simulation
			handler.ResourceUpdate(vd, null, VesselResHandler.VesselState.EditorFinalize, 1.0);
		}

		/// <summary>run a single timestamp of the simulator</summary>
		private static void RunSimulatorStep(List<Part> parts)
		{
			// process all rules
			foreach (Rule r in Profile.rules)
			{
				if (r.input.Length > 0 && r.rate > 0.0)
				{
					ExecuteRule(r);
				}
			}

			// process all processes
			foreach (Process p in Profile.processes)
			{
				ExecuteProcess(p);
			}

			// process comms
			// TODO : add a switch somewhere in the planner to select transmitting/not transmitting
			handler.ElectricCharge.Consume(vd.connection.ec_idle, ResourceBroker.CommsIdle);
			if (vd.connection.ec > 0.0)
				handler.ElectricCharge.Consume(vd.connection.ec - vd.connection.ec_idle, ResourceBroker.CommsXmit);

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
					if (!m.isEnabled)
						continue;

					switch (m.moduleName)
					{
						case "ModuleCommand"               : ProcessCommand(m as ModuleCommand); continue;
						case "ModuleGenerator"             : ProcessGenerator(m as ModuleGenerator, p); continue;
						case "ModuleResourceConverter"     : ProcessConverter(m as ModuleResourceConverter); continue;
						case "ModuleKPBSConverter"         : ProcessConverter(m as ModuleResourceConverter); continue;
						case "ModuleResourceHarvester"     : ProcessStockharvester(m as ModuleResourceHarvester); continue;
						case "ModuleScienceConverter"      : ProcessStocklab(m as ModuleScienceConverter); continue;
						case "ModuleActiveRadiator"        : ProcessRadiator(m as ModuleActiveRadiator); continue;
						case "ModuleWheelMotor"            : ProcessWheelMotor(m as ModuleWheelMotor); continue;
						case "ModuleWheelMotorSteering"    : ProcessWheelSteering(m as ModuleWheelMotorSteering); continue;
						case "ModuleLight"                 : ProcessLight(m as ModuleLight); continue;
						case "ModuleColoredLensLight"      : ProcessLight(m as ModuleLight); continue;
						case "ModuleMultiPointSurfaceLight": ProcessLight(m as ModuleLight); continue;
						case "KerbalismScansat"            : ProcessScanner(m as KerbalismScansat); continue;
						case "ModuleRadioisotopeGenerator" : ProcessRTG(p, m); continue;
						case "ModuleCryoTank"              : ProcessCryotank(p, m); continue;
						case "ModuleEngines"               : ProcessEngines(m as ModuleEngines); continue;
						case "ModuleEnginesFX"             : ProcessEnginesFX(m as ModuleEnginesFX); continue;
						case "ModuleRCS"                   : ProcessRCS(m as ModuleRCS); continue;
						case "ModuleRCSFX"                 : ProcessRCSFX(m as ModuleRCSFX); continue;
					}

					if (m is IPlannerModule ipm)
					{
						ipm.PlannerUpdate(vd.ResHandler, vd);
						continue;
					}

					if (PartModuleAPI.plannerModules.TryGetValue(m.GetType(), out Action<PartModule, CelestialBody, double, bool> apiUpdate))
					{
						apiUpdate(m, vd.body, vd.altitude, vd.InSunlight);
						continue;
					}
				}
			}
		}

		#region RULE/PROCESS HANDLERS

		/// <summary>execute a rule</summary>
		private static void ExecuteRule(Rule r)
		{
			// deduce rate per-second
			double rate = vd.crewCount * r.rate;
			double modifier = r.EvaluateModifier(vd);

			// prepare recipe
			if (r.output.Length == 0)
			{
				handler.GetResource(r.input).Consume(rate * modifier, r.broker);
			}
			else if (rate > double.Epsilon)
			{
				// - rules always dump excess overboard (because it is waste)
				Recipe recipe = new Recipe(r.broker);
				recipe.AddInput(r.input, rate * modifier);
				recipe.AddOutput(r.output, rate * modifier * r.ratio, true);
				handler.AddRecipe(recipe);
			}
		}

		/// <summary>execute a process</summary>
		private static void ExecuteProcess(Process pr)
		{
			pr.Execute(vd, 1.0);
		}

		#endregion

		#region PARTMODULE HANDLERS

		private static void ProcessCommand(ModuleCommand command)
		{
			if (command.hibernationMultiplier == 0.0)
				return;

			// do not consume if this is a non-probe MC with no crew
			// this make some sense: you left a vessel with some battery and nobody on board, you expect it to not consume EC
			if (command.minimumCrew == 0 || command.part.protoModuleCrew.Count > 0)
			{
				double ecRate = command.hibernationMultiplier;
				if (command.hibernation)
					ecRate *= Settings.HibernatingEcFactor;

				handler.ElectricCharge.Consume(ecRate, ResourceBroker.Command, true);
			}
		}

		private static void ProcessGenerator(ModuleGenerator generator, Part p)
		{
			Recipe recipe = new Recipe(ResourceBroker.GetOrCreate(p.partInfo.title));
			foreach (ModuleResource res in generator.resHandler.inputResources)
			{
				recipe.AddInput(res.name, res.rate);
			}
			foreach (ModuleResource res in generator.resHandler.outputResources)
			{
				recipe.AddOutput(res.name, res.rate, true);
			}
			handler.AddRecipe(recipe);
		}

		private static void ProcessConverter(ModuleResourceConverter converter)
		{
			// calculate experience bonus
			float exp_bonus = converter.UseSpecialistBonus
			  ? converter.EfficiencyBonus * (converter.SpecialistBonusBase + (converter.SpecialistEfficiencyFactor * (vd.crewEngineerMaxlevel + 1)))
			  : 1.0f;

			// use part name as recipe name
			// - include crew bonus in the recipe name
			string recipe_name = Lib.BuildString(converter.part.partInfo.title, " (efficiency: ", Lib.HumanReadablePerc(exp_bonus), ")");

			// generate recipe
			Recipe recipe = new Recipe(ResourceBroker.GetOrCreate(recipe_name));
			foreach (ResourceRatio res in converter.inputList)
			{
				recipe.AddInput(res.ResourceName, res.Ratio * exp_bonus);
			}
			foreach (ResourceRatio res in converter.outputList)
			{
				recipe.AddOutput(res.ResourceName, res.Ratio * exp_bonus, res.DumpExcess);
			}
			handler.AddRecipe(recipe);
		}

		private static void ProcessStockharvester(ModuleResourceHarvester harvester)
		{
			// calculate experience bonus
			float exp_bonus = harvester.UseSpecialistBonus
			  ? harvester.EfficiencyBonus * (harvester.SpecialistBonusBase + (harvester.SpecialistEfficiencyFactor * (vd.crewEngineerMaxlevel + 1)))
			  : 1.0f;

			// use part name as recipe name
			// - include crew bonus in the recipe name
			string recipe_name = Lib.BuildString(harvester.part.partInfo.title, " (efficiency: ", Lib.HumanReadablePerc(exp_bonus), ")");

			// generate recipe
			Recipe recipe = new Recipe(ResourceBroker.StockDrill);
			foreach (ResourceRatio res in harvester.inputList)
			{
				recipe.AddInput(res.ResourceName, res.Ratio);
			}
			recipe.AddOutput(harvester.ResourceName, harvester.Efficiency * exp_bonus, true);
			handler.AddRecipe(recipe);
		}

		private static void ProcessStocklab(ModuleScienceConverter lab)
		{
			handler.ElectricCharge.Consume(lab.powerRequirement, ResourceBroker.ScienceLab);
		}

		private static void ProcessRadiator(ModuleActiveRadiator radiator)
		{
			// note: IsCooling is not valid in the editor, for deployable radiators,
			// we will have to check if the related deploy module is deployed
			// we use PlannerController instead
			foreach (ModuleResource res in radiator.resHandler.inputResources)
			{
				handler.GetResource(res.name).Consume(res.rate, ResourceBroker.Radiator);
			}
		}

		private static void ProcessWheelMotor(ModuleWheelMotor motor)
		{
			foreach (ModuleResource res in motor.resHandler.inputResources)
			{
				handler.GetResource(res.name).Consume(res.rate, ResourceBroker.Wheel);
			}
		}

		private static void ProcessWheelSteering(ModuleWheelMotorSteering steering)
		{
			foreach (ModuleResource res in steering.resHandler.inputResources)
			{
				handler.GetResource(res.name).Consume(res.rate, ResourceBroker.Wheel);
			}
		}


		private static void ProcessLight(ModuleLight light)
		{
			if (light.useResources && light.isOn)
			{
				handler.ElectricCharge.Consume(light.resourceAmount, ResourceBroker.Light);
			}
		}


		private static void ProcessScanner(KerbalismScansat m)
		{
			handler.ElectricCharge.Consume(m.ec_rate, ResourceBroker.Scanner);
		}

		private static void ProcessRTG(Part p, PartModule m)
		{
			double max_rate = Lib.ReflectionValue<float>(m, "BasePower");

			handler.ElectricCharge.Produce(max_rate, ResourceBroker.RTG);
		}

		private static void ProcessCryotank(Part p, PartModule m)
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
						handler.GetResource(fuel_name).Consume(amount * boiloff_rate, ResourceBroker.Cryotank);
					}
				}
			}

			// apply EC consumption
			handler.ElectricCharge.Consume(total_cost, ResourceBroker.Cryotank);
		}

		private static void ProcessEngines(ModuleEngines me)
		{
			// calculate thrust fuel flow
			double thrust_flow = me.maxFuelFlow * 1e3 * me.thrustPercentage;

			// search fuel types
			foreach (Propellant fuel in me.propellants)
			{
				switch (fuel.name)
				{
					case "ElectricCharge":  // mainly used for Ion Engines
						handler.ElectricCharge.Consume(thrust_flow * fuel.ratio, ResourceBroker.Engine);
						break;
					case "LqdHydrogen":     // added for cryotanks and any other supported mod that uses Liquid Hydrogen
						handler.GetResource("LqdHydrogen").Consume(thrust_flow * fuel.ratio, ResourceBroker.Engine);
						break;
				}
			}
		}

		private static void ProcessEnginesFX(ModuleEnginesFX mefx)
		{
			// calculate thrust fuel flow
			double thrust_flow = mefx.maxFuelFlow * 1e3 * mefx.thrustPercentage;

			// search fuel types
			foreach (Propellant fuel in mefx.propellants)
			{
				switch (fuel.name)
				{
					case "ElectricCharge":  // mainly used for Ion Engines
						handler.ElectricCharge.Consume(thrust_flow * fuel.ratio, ResourceBroker.Engine);
						break;
					case "LqdHydrogen":     // added for cryotanks and any other supported mod that uses Liquid Hydrogen
						handler.GetResource("LqdHydrogen").Consume(thrust_flow * fuel.ratio, ResourceBroker.Engine);
						break;
				}
			}
		}

		private static void ProcessRCS(ModuleRCS mr)
		{
			// calculate thrust fuel flow
			double thrust_flow = mr.maxFuelFlow * 1e3 * mr.thrustPercentage * mr.thrusterPower;

			// search fuel types
			foreach (Propellant fuel in mr.propellants)
			{
				switch (fuel.name)
				{
					case "ElectricCharge":  // mainly used for Ion RCS
						handler.ElectricCharge.Consume(thrust_flow * fuel.ratio, ResourceBroker.GetOrCreate("rcs", ResourceBroker.BrokerCategory.VesselSystem, "rcs"));
						break;
					case "LqdHydrogen":     // added for cryotanks and any other supported mod that uses Liquid Hydrogen
						handler.GetResource("LqdHydrogen").Consume(thrust_flow * fuel.ratio, ResourceBroker.GetOrCreate("rcs", ResourceBroker.BrokerCategory.VesselSystem, "rcs"));
						break;
				}
			}
		}

		private static void ProcessRCSFX(ModuleRCSFX mrfx)
		{
			// calculate thrust fuel flow
			double thrust_flow = mrfx.maxFuelFlow * 1e3 * mrfx.thrustPercentage * mrfx.thrusterPower;

			// search fuel types
			foreach (Propellant fuel in mrfx.propellants)
			{
				switch (fuel.name)
				{
					case "ElectricCharge":  // mainly used for Ion RCS
						handler.ElectricCharge.Consume(thrust_flow * fuel.ratio, ResourceBroker.GetOrCreate("rcs", ResourceBroker.BrokerCategory.VesselSystem, "rcs"));
						break;
					case "LqdHydrogen":     // added for cryotanks and any other supported mod that uses Liquid Hydrogen
						handler.GetResource("LqdHydrogen").Consume(thrust_flow * fuel.ratio, ResourceBroker.GetOrCreate("rcs", ResourceBroker.BrokerCategory.VesselSystem, "rcs"));
						break;
				}
			}
		}

		#endregion
	}
} // KERBALISM
