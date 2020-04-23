using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;
using KERBALISM.Planner;

namespace KERBALISM
{

	public class Greenhouse : PartModule, IModuleInfo, ISpecifics, IContractObjectiveModule, IConfigurable, IPlannerModule
	{
		// config
		[KSPField] public string crop_resource;         // name of resource produced by harvests
		[KSPField] public double crop_size;             // amount of resource produced by harvests
		[KSPField] public double crop_rate;             // growth per-second when all conditions apply
		[KSPField] public double ec_rate;               // EC/s consumed by the lamp at max capacity, set to 0 to disable the lamp
		[KSPField] public double light_tolerance;       // minimum lighting flux required for growth, in W/m^2
		[KSPField] public double pressure_tolerance;    // minimum pressure required for growth, in sea level atmospheres (optional)
		[KSPField] public double radiation_tolerance;   // maximum radiation allowed for growth in rad/s, considered after shielding is applied (optional)
		[KSPField] public string lamps;                 // object with emissive texture used to represent intensity graphically
		[KSPField] public string shutters;              // animation to manipulate shutters
		[KSPField] public string plants;                // animation to represent plant growth graphically

		[KSPField] public bool animBackwards = false;   // If animation is playing in backward, this can help to fix

		// persistence
		[KSPField(isPersistant = true)] public bool active;               // on/off flag
		[KSPField(isPersistant = true)] public double growth;             // current growth level
		[KSPField(isPersistant = true)] public double natural;            // natural lighting flux
		[KSPField(isPersistant = true)] public double artificial;         // artificial lighting flux
		[KSPField(isPersistant = true)] public double tta;                // time to harvest
		[KSPField(isPersistant = true)] public string issue;              // first detected issue, or empty if there is none

		// rmb ui status
		[KSPField(guiActive = true, guiName = "#KERBALISM_Greenhouse_status_natural", groupName = "Greenhouse", groupDisplayName = "#KERBALISM_Group_Greenhouse")]//Greenhouse
		public string status_natural;        // natural lighting
		[KSPField(guiActive = true, guiName = "#KERBALISM_Greenhouse_status_artificial", groupName = "Greenhouse", groupDisplayName = "#KERBALISM_Group_Greenhouse")]//Greenhouse
		public string status_artificial;  // artificial lighting
		[KSPField(guiActive = true, guiName = "#KERBALISM_Greenhouse_status_tta", groupName = "Greenhouse", groupDisplayName = "#KERBALISM_Group_Greenhouse")]//Greenhouse
		public string status_tta;                // time to harvest

		// animations
		Animator shutters_anim;
		Animator plants_anim;

		// other data
		Renderer lamps_rdr;
		public bool WACO2 = false;        // true if we have combined WasteAtmosphere and CarbonDioxide

		private bool isConfigurable = false;

		public void Configure(bool enable) {
			active = enable;
			if(!active) {
				growth = 0;
				tta = 0;
			}
		}

		public void ModuleIsConfigured() => isConfigurable = true;

		public override void OnStart(StartState state)
		{
			// don't break tutorial scenarios
			if (Lib.DisableScenario(this)) return;

			// create animators
			if (shutters.Length > 0) shutters_anim = new Animator(part, shutters, animBackwards);
			if (plants.Length > 0) plants_anim = new Animator(part, plants);

			// still-play shutters animation
			if (shutters_anim != null) shutters_anim.Still(active ? 1f : 0f);

			// still-play plants animation
			if (plants_anim != null) plants_anim.Still((float)growth);

			// cache lamps renderer
			if (lamps.Length > 0)
			{
				foreach (var rdr in part.GetComponentsInChildren<Renderer>())
				{
					if (rdr.name == lamps) { lamps_rdr = rdr; break; }
				}
			}

			// do not allow light tolerance to be zero
			if (light_tolerance <= double.Epsilon) light_tolerance = 400.0;
		}


		public void Update()
		{
			// set lamps emissive object
			if (lamps_rdr != null)
			{
				float intensity = Lib.IsFlight ? (active ? (float)(artificial / light_tolerance) : 0.0f) : (active ? 1.0f : 0.0f);
				lamps_rdr.material.SetColor("_EmissiveColor", new Color(intensity, intensity, intensity, 1.0f));
			}

			// in flight
			if (Lib.IsFlight)
			{
				// still-play plants animation
				if (plants_anim != null) plants_anim.Still((float)growth);

				// update ui
				string status = issue.Length > 0 ? Lib.BuildString("<color=yellow>", issue, "</color>") : growth > 0.99 ? Local.TELEMETRY_readytoharvest : Local.TELEMETRY_growing;//"ready to harvest""growing"
				Events["Toggle"].guiName = Lib.StatusToggle(Local.Greenhouse_Greenhouse, active ? status : Local.Greenhouse_disabled);//"Greenhouse""disabled"
				Fields["status_natural"].guiActive = active && growth < 0.99;
				Fields["status_artificial"].guiActive = active && growth < 0.99;
				Fields["status_tta"].guiActive = active && growth < 0.99;
				status_natural = Lib.HumanReadableFlux(natural);
				status_artificial = Lib.HumanReadableFlux(artificial);
				status_tta = Lib.HumanReadableDuration(tta);

				// show/hide harvest buttons
				bool manned = FlightGlobals.ActiveVessel.isEVA || Lib.CrewCount(vessel) > 0;
				Events["Harvest"].active = manned && growth >= 0.99;
				Events["EmergencyHarvest"].active = manned && growth >= 0.5 && growth < 0.99;
			}
			// in editor
			else
			{
				// update ui
				Events["Toggle"].guiName = Lib.StatusToggle(Local.Greenhouse_Greenhouse, active ? Local.Greenhouse_enabled : Local.Greenhouse_disabled);//"Greenhouse""enabled""disabled"
			}
		}

		public void FixedUpdate()
		{
			// do nothing in the editor
			if (Lib.IsEditor) return;

			// if enabled and not ready for harvest
			if (active && growth < 0.99)
			{
				// get vessel info from the cache
				// - if the vessel is not valid (eg: flagged as debris) then solar flux will be 0 and landed false (but that's okay)
				vessel.TryGetVesselData(out VesselData vd);

				// get resource cache
				VesselResHandler resources = vd.ResHandler;
				VesselKSPResource ec = resources.ElectricCharge;

				// deal with corner cases when greenhouse is assembled using KIS
				if (double.IsNaN(growth) || double.IsInfinity(growth)) growth = 0.0;

				// calculate natural and artificial lighting
				natural = vd.EnvSolarFluxTotal;
				artificial = Math.Max(light_tolerance - natural, 0.0);

				// consume EC for the lamps, scaled by artificial light intensity
				if (artificial > 0.0) ec.Consume(ec_rate * (artificial / light_tolerance) * Kerbalism.elapsed_s, ResourceBroker.Greenhouse);

				// scale artificial by ec available
				artificial *= ec.AvailabilityFactor;

				// execute recipe
				Recipe recipe = new Recipe(ResourceBroker.Greenhouse);
				foreach (ModuleResource input in resHandler.inputResources)
				{
					// WasteAtmosphere is primary combined input
					if (WACO2 && input.name == "WasteAtmosphere") recipe.AddInput(input.name, vd.EnvInBreathableAtmosphere ? 0.0 : input.rate * Kerbalism.elapsed_s, "CarbonDioxide");
					// CarbonDioxide is secondary combined input
					else if (WACO2 && input.name == "CarbonDioxide") recipe.AddInput(input.name, vd.EnvInBreathableAtmosphere ? 0.0 : input.rate * Kerbalism.elapsed_s, "");
					// if atmosphere is breathable disable WasteAtmosphere / CO2
					else if (!WACO2 && (input.name == "CarbonDioxide" || input.name == "WasteAtmosphere")) recipe.AddInput(input.name, vd.EnvInBreathableAtmosphere ? 0.0 : input.rate, "");
					else recipe.AddInput(input.name, input.rate * Kerbalism.elapsed_s);
				}
				foreach (ModuleResource output in resHandler.outputResources)
				{
					// if atmosphere is breathable disable Oxygen
					if (output.name == "Oxygen") recipe.AddOutput(output.name, vd.EnvInBreathableAtmosphere ? 0.0 : output.rate * Kerbalism.elapsed_s, true);
					else recipe.AddOutput(output.name, output.rate * Kerbalism.elapsed_s, true);
				}
				resources.AddRecipe(recipe);

				// determine environment conditions
				bool lighting = natural + artificial >= light_tolerance;
				bool pressure = pressure_tolerance <= double.Epsilon || vd.Habitat.pressure >= pressure_tolerance;
				bool radiation = radiation_tolerance <= double.Epsilon || (1.0 - vd.Habitat.shieldingModifier) * vd.Habitat.radiationRate < radiation_tolerance;

				// determine input resources conditions
				// - comparing against amounts in previous simulation step
				bool inputs = true;
				string missing_res = string.Empty;
				bool dis_WACO2 = false;
				foreach (ModuleResource input in resHandler.inputResources)
				{
					// combine WasteAtmosphere and CO2 if both exist
					if (input.name == "WasteAtmosphere" || input.name == "CarbonDioxide")
					{
						if (dis_WACO2 || vd.EnvInBreathableAtmosphere) continue;    // skip if already checked or atmosphere is breathable
						if (WACO2)
						{
							if (resources.GetResource("WasteAtmosphere").Amount <= double.Epsilon && resources.GetResource("CarbonDioxide").Amount <= double.Epsilon)
							{
								inputs = false;
								missing_res = "CarbonDioxide";
								break;
							}
							dis_WACO2 = true;
							continue;
						}
					}
					if (resources.GetResource(input.name).Amount <= double.Epsilon)
					{
						inputs = false;
						missing_res = input.name;
						break;
					}
				}

				// if growing
				if (lighting && pressure && radiation && inputs)
				{
					// increase growth
					growth += crop_rate * Kerbalism.elapsed_s;
					growth = Math.Min(growth, 1.0);

					// notify the user when crop can be harvested
					if (growth >= 0.99)
					{
						Message.Post(Local.harvestedready_msg.Format("<b>" + vessel.vesselName + "</b>"));//Lib.BuildString("On <<1>> the crop is ready to be harvested")
						growth = 1.0;
					}
				}

				// update time-to-harvest
				tta = (1.0 - growth) / crop_rate;

				// update issues
				issue =
				  !inputs ? Lib.BuildString(Local.Greenhouse_resoucesmissing.Format(missing_res))//"missing <<1>>"
				: !lighting ? Local.Greenhouse_issue1//"insufficient lighting"
				: !pressure ? Local.Greenhouse_issue2//"insufficient pressure"
				: !radiation ? Local.Greenhouse_issue3//"excessive radiation"
				: string.Empty;
			}
		}


		public static void BackgroundUpdate(Vessel v, ProtoPartModuleSnapshot m, Greenhouse g,
											VesselData vd, VesselResHandler resources, double elapsed_s)
		{
			// get protomodule data
			bool active = Lib.Proto.GetBool(m, "active");
			double growth = Lib.Proto.GetDouble(m, "growth");

			// if enabled and not ready for harvest
			if (active && growth < 0.99)
			{
				// get resource handler
				VesselKSPResource ec = resources.ElectricCharge;

				// calculate natural and artificial lighting
				double natural = vd.EnvSolarFluxTotal;
				double artificial = Math.Max(g.light_tolerance - natural, 0.0);

				// consume EC for the lamps, scaled by artificial light intensity
				if (artificial > 0.0) ec.Consume(g.ec_rate * (artificial / g.light_tolerance) * elapsed_s, ResourceBroker.Greenhouse);

				// scale artificial by ec available
				 artificial *= ec.AvailabilityFactor;

				// execute recipe
				Recipe recipe = new Recipe(ResourceBroker.Greenhouse);
				foreach (ModuleResource input in g.resHandler.inputResources) //recipe.Input(input.name, input.rate * elapsed_s);
				{
					// WasteAtmosphere is primary combined input
					if (g.WACO2 && input.name == "WasteAtmosphere") recipe.AddInput(input.name, vd.EnvInBreathableAtmosphere ? 0.0 : input.rate * elapsed_s, "CarbonDioxide");
					// CarbonDioxide is secondary combined input
					else if (g.WACO2 && input.name == "CarbonDioxide") recipe.AddInput(input.name, vd.EnvInBreathableAtmosphere ? 0.0 : input.rate * elapsed_s, "");
					// if atmosphere is breathable disable WasteAtmosphere / CO2
					else if (!g.WACO2 && (input.name == "CarbonDioxide" || input.name == "WasteAtmosphere")) recipe.AddInput(input.name, vd.EnvInBreathableAtmosphere ? 0.0 : input.rate, "");
					else
						recipe.AddInput(input.name, input.rate * elapsed_s);
				}
				foreach (ModuleResource output in g.resHandler.outputResources)
				{
					// if atmosphere is breathable disable Oxygen
					if (output.name == "Oxygen") recipe.AddOutput(output.name, vd.EnvInBreathableAtmosphere ? 0.0 : output.rate * elapsed_s, true);
					else recipe.AddOutput(output.name, output.rate * elapsed_s, true);
				}
				resources.AddRecipe(recipe);

				// determine environment conditions
				bool lighting = natural + artificial >= g.light_tolerance;
				bool pressure = g.pressure_tolerance <= 0 || vd.Habitat.pressure >= g.pressure_tolerance;
				bool radiation = g.radiation_tolerance <= 0 || vd.EnvRadiation * (1.0 - vd.Habitat.shieldingModifier) < g.radiation_tolerance;

				// determine inputs conditions
				// note: comparing against amounts in previous simulation step
				bool inputs = true;
				string missing_res = string.Empty;
				bool dis_WACO2 = false;
				foreach (ModuleResource input in g.resHandler.inputResources)
				{
					// combine WasteAtmosphere and CO2 if both exist
					if (input.name == "WasteAtmosphere" || input.name == "CarbonDioxide")
					{
						if (dis_WACO2 || vd.EnvInBreathableAtmosphere) continue;    // skip if already checked or atmosphere is breathable
						if (g.WACO2)
						{
							if (resources.GetResource("WasteAtmosphere").Amount <= double.Epsilon && resources.GetResource("CarbonDioxide").Amount <= double.Epsilon)
							{
								inputs = false;
								missing_res = "CarbonDioxide";
								break;
							}
							dis_WACO2 = true;
							continue;
						}
					}
					if (resources.GetResource(input.name).Amount <= double.Epsilon)
					{
						inputs = false;
						missing_res = input.name;
						break;
					}
				}

				// if growing
				if (lighting && pressure && radiation && inputs)
				{
					// increase growth
					growth += g.crop_rate * elapsed_s;
					growth = Math.Min(growth, 1.0);

					// notify the user when crop can be harvested
					if (growth >= 0.99)
					{
						Message.Post(Local.harvestedready_msg.Format("<b>" + v.vesselName + "</b>"));//Lib.BuildString("On <<1>> the crop is ready to be harvested")
						growth = 1.0;
					}
				}

				// update time-to-harvest
				double tta = (1.0 - growth) / g.crop_rate;

				// update issues
				string issue =
				  !inputs ? Lib.BuildString(Local.Greenhouse_resoucesmissing.Format(missing_res))//"missing ", missing_res
				: !lighting ? Local.Greenhouse_issue1//"insufficient lighting"
				: !pressure ? Local.Greenhouse_issue2//"insufficient pressure"
				: !radiation ? Local.Greenhouse_issue3//"excessive radiation"
				: string.Empty;

				// update protomodule data
				Lib.Proto.Set(m, "natural", natural);
				Lib.Proto.Set(m, "artificial", artificial);
				Lib.Proto.Set(m, "tta", tta);
				Lib.Proto.Set(m, "issue", issue);
				Lib.Proto.Set(m, "growth", growth);
			}
		}

		public void PlannerUpdate(VesselResHandler resHandler, VesselDataShip vesselData)
		{
			// skip disabled greenhouses
			if (!active)
				return;

			// shortcut to resources
			VesselResource ec = resHandler.ElectricCharge;
			VesselResource res = resHandler.GetResource(crop_resource);

			// calculate natural and artificial lighting
			double natural = vesselData.solarFlux;
			double artificial = Math.Max(light_tolerance - natural, 0.0);

			// if lamps are on and artificial lighting is required
			if (artificial > 0.0)
			{
				// consume ec for the lamps
				ec.Consume(ec_rate * (artificial / light_tolerance), ResourceBroker.Greenhouse);
			}

			// execute recipe
			Recipe recipe = new Recipe(ResourceBroker.Greenhouse);
			foreach (ModuleResource input in this.resHandler.inputResources)
			{
				// WasteAtmosphere is primary combined input
				if (WACO2 && input.name == "WasteAtmosphere")
					recipe.AddInput(input.name, vesselData.breathable ? 0.0 : input.rate, "CarbonDioxide");
				// CarbonDioxide is secondary combined input
				else if (WACO2 && input.name == "CarbonDioxide")
					recipe.AddInput(input.name, vesselData.breathable ? 0.0 : input.rate, "");
				// if atmosphere is breathable disable WasteAtmosphere / CO2
				else if (!WACO2 && (input.name == "CarbonDioxide" || input.name == "WasteAtmosphere"))
					recipe.AddInput(input.name, vesselData.breathable ? 0.0 : input.rate, "");
				else
					recipe.AddInput(input.name, input.rate);
			}
			foreach (ModuleResource output in this.resHandler.outputResources)
			{
				// if atmosphere is breathable disable Oxygen
				if (output.name == "Oxygen")
					recipe.AddOutput(output.name, vesselData.breathable ? 0.0 : output.rate, true);
				else
					recipe.AddOutput(output.name, output.rate, true);
			}
			resHandler.AddRecipe(recipe);

			// determine environment conditions
			bool lighting = natural + artificial >= light_tolerance;
			bool pressure = vesselData.Habitat.pressure > Settings.PressureThreshold || pressure_tolerance <= double.Epsilon;
			bool radiation = (vesselData.landed ? vesselData.surfaceRad : vesselData.magnetopauseRad) * (1.0 - vesselData.Habitat.shieldingModifier) < radiation_tolerance;

			// if all conditions apply
			// note: we are assuming the inputs are satisfied, we can't really do otherwise here
			if (lighting && pressure && radiation)
			{
				// produce food
				res.Produce(crop_size * crop_rate, ResourceBroker.Greenhouse);

				// add harvest info
				//res.harvests.Add(Lib.BuildString(g.crop_size.ToString("F0"), " in ", Lib.HumanReadableDuration(1.0 / g.crop_rate)));
			}
		}


		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", groupName = "Greenhouse", groupDisplayName = "#KERBALISM_Group_Greenhouse")]//Greenhouse
		// toggle greenhouse
		public void Toggle()
		{
			bool deactivating = active;

			// switch status
			active = !active;

			// play animation
			if (shutters_anim != null) shutters_anim.Play(deactivating ^ animBackwards, false);

			// refresh VAB/SPH ui
			if (Lib.IsEditor) GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
		}

		[KSPEvent(guiActive = true, guiActiveUnfocused = true, guiName = "#KERBALISM_Greenhouse_Harvest", active = false, groupName = "Greenhouse", groupDisplayName = "#KERBALISM_Group_Greenhouse")]//Greenhouse
		// harvest
		public void Harvest()
		{
			// disable for dead eva kerbals
			Vessel v = FlightGlobals.ActiveVessel;
			if (v == null || EVA.IsDead(v)) return;

			// produce reduced quantity of food, proportional to current growth
			vessel.TryGetVesselData(out VesselData vd);
			vd.ResHandler.Produce(crop_resource, crop_size, ResourceBroker.Greenhouse);

			// reset growth
			growth = 0.0;

			// show message
			Message.Post(Lib.BuildString(Local.Greenhouse_msg_1.Format("<color=ffffff>" + vessel.vesselName + "</color> "), Local.Greenhouse_msg_2.Format("<color=ffffff>" + crop_size.ToString("F0") + " " + crop_resource + "</color>")));//"On <<1>>""harvest produced <<1>>", 
		}

		[KSPEvent(guiActive = true, guiActiveUnfocused = true, guiName = "#KERBALISM_Greenhouse_EmergencyHarvest", active = false, groupName = "Greenhouse", groupDisplayName = "#KERBALISM_Group_Greenhouse")]//Greenhouse
		// emergency harvest
		public void EmergencyHarvest()
		{
			// disable for dead eva kerbals
			Vessel v = FlightGlobals.ActiveVessel;
			if (v == null || EVA.IsDead(v)) return;

			// calculate reduced harvest size
			double reduced_harvest = crop_size * growth * 0.5;

			// produce reduced quantity of food, proportional to current growth
			vessel.TryGetVesselData(out VesselData vd);
			vd.ResHandler.Produce(crop_resource, reduced_harvest, ResourceBroker.Greenhouse);

			// reset growth
			growth = 0.0;

			// show message
			Message.Post(Lib.BuildString(Local.Greenhouse_msg_1.Format("<color=ffffff>" + vessel.vesselName + "</color> "), Local.Greenhouse_msg_3.Format(" <color=ffffff>"+ reduced_harvest.ToString("F0")+ " " + crop_resource +"</color>")));//"On <<1>>""emergency harvest produced"
		}

		// action groups
		[KSPAction("#KERBALISM_Greenhouse_Action")] public void Action(KSPActionParam param) { Toggle(); }


		// part tooltip
		public override string GetInfo()
		{
			if (!isConfigurable)
				return Specs().Info(Local.Greenhouse_desc);//"Grow crops in space and on the surface of celestial bodies, even far from the sun."
			else
				return string.Empty;
		}


		// specifics support
		public Specifics Specs()
		{
			Specifics specs = new Specifics();

			specs.Add(Local.Greenhouse_info1, Lib.HumanReadableAmount(crop_size, " " + crop_resource));//"Harvest size"
			specs.Add(Local.Greenhouse_info2, Lib.HumanReadableDuration(1.0 / crop_rate));//"Harvest time"
			specs.Add(Local.Greenhouse_info3, Lib.HumanReadableFlux(light_tolerance));//"Lighting tolerance"
			if (pressure_tolerance > double.Epsilon) specs.Add(Local.Greenhouse_info4, Lib.HumanReadablePressure(Sim.PressureAtSeaLevel * pressure_tolerance));//"Pressure tolerance"
			if (radiation_tolerance > double.Epsilon) specs.Add(Local.Greenhouse_info5, Lib.HumanReadableRadiation(radiation_tolerance));//"Radiation tolerance"
			specs.Add(Local.Greenhouse_info6, Lib.HumanReadableRate(ec_rate));//"Lamps EC rate"
			specs.Add(string.Empty);
			specs.Add("<color=#00ffff>" + Local.Greenhouse_info7 + "</color>");//Required resources

			// do we have combined WasteAtmosphere and CO2
			Set_WACO2();
			bool dis_WACO2 = false;
			foreach (ModuleResource input in resHandler.inputResources)
			{
				// combine WasteAtmosphere and CO2 if both exist
				if (WACO2 && (input.name == "WasteAtmosphere" || input.name == "CarbonDioxide"))
				{
					if (dis_WACO2) continue;
					ModuleResource sec;
					if (input.name == "WasteAtmosphere") sec = resHandler.inputResources.Find(x => x.name.Contains("CarbonDioxide"));
					else sec = resHandler.inputResources.Find(x => x.name.Contains("WasteAtmosphere"));
					specs.Add(Local.Greenhouse_CarbonDioxide, Lib.BuildString("<color=#ffaa00>", Lib.HumanReadableRate(input.rate + sec.rate), "</color>"));//"CarbonDioxide"
					specs.Add(Local.Greenhouse_CarbonDioxide_desc);//"Crops can also use the CO2 in the atmosphere without a scrubber."
					dis_WACO2 = true;
				}
				else
					specs.Add(input.name, Lib.BuildString("<color=#ffaa00>", Lib.HumanReadableRate(input.rate), "</color>"));
			}
			specs.Add(string.Empty);
			specs.Add("<color=#00ffff>"+Local.Greenhouse_Byproducts +"</color>");//By-products
			foreach (ModuleResource output in resHandler.outputResources)
			{
				specs.Add(output.name, Lib.BuildString("<color=#00ff00>", Lib.HumanReadableRate(output.rate), "</color>"));
			}
			return specs;
		}

		/// <summary>
		/// checks if we have WasteAtmosphere and CarbonDioxide inputs and sets the WACO2 flag accordingly
		/// </summary>
		private void Set_WACO2()
		{
			WACO2 = false;
			foreach (ModuleResource input in resHandler.inputResources)
			{
				// we have combined WasteAtmosphere and CO2 if both exist
				if (input.name == "WasteAtmosphere" || input.name == "CarbonDioxide")
				{
					ModuleResource sec;
					if (input.name == "WasteAtmosphere")
					{
						sec = resHandler.inputResources.Find(x => x.name.Contains("CarbonDioxide"));
						// no CO2, we only have WasteAtmosphere
						if (sec == null) return;
					}
					else
					{
						sec = resHandler.inputResources.Find(x => x.name.Contains("WasteAtmosphere"));
						// no WasteAtmosphere, we only have CO2
						if (sec == null) return;
					}
					// we have both WasteAtmosphere and CO2
					WACO2 = true;
					return;
				}
			}
		}

		// contract objective support
		public bool CheckContractObjectiveValidity() { return true; }
		public string GetContractObjectiveType() { return "Greenhouse"; }


		// return data about all greenhouses in a vessel
		public sealed class Data
		{
			public double growth;           // growth progress
			public double natural;          // natural lighting
			public double artificial;       // artificial lighting
			public double tta;              // time to harvest
			public string issue;            // first issue detected, or empty
		}
		public static List<Data> Greenhouses(Vessel v)
		{
			List<Data> ret = new List<Data>();
			if (v.loaded)
			{
				foreach (Greenhouse greenhouse in Lib.FindModules<Greenhouse>(v))
				{
					if (greenhouse.active)
					{
						Data gd = new Data
						{
							growth = greenhouse.growth,
							natural = greenhouse.natural,
							artificial = greenhouse.artificial,
							tta = greenhouse.tta,
							issue = greenhouse.issue
						};
						ret.Add(gd);
					}
				}
			}
			else
			{
				foreach (ProtoPartModuleSnapshot m in Lib.FindModules(v.protoVessel, "Greenhouse"))
				{
					if (Lib.Proto.GetBool(m, "active"))
					{
						Data gd = new Data
						{
							growth = Lib.Proto.GetDouble(m, "growth"),
							natural = Lib.Proto.GetDouble(m, "natural"),
							artificial = Lib.Proto.GetDouble(m, "artificial"),
							tta = Lib.Proto.GetDouble(m, "tta"),
							issue = Lib.Proto.GetString(m, "issue")
						};
						ret.Add(gd);
					}
				}
			}
			return ret;
		}

		// module info support
		public string GetModuleTitle() { return "<size=1><color=#00000000>00</color></size>Greenhouse"; } // attempt to display at the top//""+Local.Greenhouse
		public override string GetModuleDisplayName() { return "<size=1><color=#00000000>00</color></size>"+Local.Greenhouse; } // Attempt to display at top of tooltip//"Greenhouse"
		public string GetPrimaryField() { return String.Empty; }
		public Callback<Rect> GetDrawModulePanelCallback() { return null; }
	}


} // KERBALISM


