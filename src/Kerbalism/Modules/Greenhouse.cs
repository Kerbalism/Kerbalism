using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{


	public sealed class Greenhouse : PartModule, IModuleInfo, ISpecifics, IContractObjectiveModule, IConfigurable
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
		[KSPField(guiActive = true, guiName = "#KERBALISM_Greenhouse_status_natural")] public string status_natural;        // natural lighting
		[KSPField(guiActive = true, guiName = "#KERBALISM_Greenhouse_status_artificial")] public string status_artificial;  // artificial lighting
		[KSPField(guiActive = true, guiName = "#KERBALISM_Greenhouse_status_tta")] public string status_tta;                // time to harvest

		// animations
		Animator shutters_anim;
		Animator plants_anim;

		// other data
		Renderer lamps_rdr;
		public bool WACO2 = false;        // true if we have combined WasteAtmosphere and CarbonDioxide


		public void Configure(bool enable) {
			active = enable;
			if(!active) {
				growth = 0;
				tta = 0;
			}
		}


		public override void OnStart(StartState state)
		{
			// don't break tutorial scenarios
			if (Lib.DisableScenario(this)) return;

			// create animators
			if (shutters.Length > 0) shutters_anim = new Animator(part, shutters);
			if (plants.Length > 0) plants_anim = new Animator(part, plants);

			// still-play shutters animation
			if (shutters_anim != null) shutters_anim.Still((active ^ animBackwards) ? 1.0 : 0.0);

			// still-play plants animation
			if (plants_anim != null) plants_anim.Still(growth);

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
				float intensity = Lib.IsFlight() ? (active ? (float)(artificial / light_tolerance) : 0.0f) : (active ? 1.0f : 0.0f);
				lamps_rdr.material.SetColor("_EmissiveColor", new Color(intensity, intensity, intensity, 1.0f));
			}

			// in flight
			if (Lib.IsFlight())
			{
				// still-play plants animation
				if (plants_anim != null) plants_anim.Still(growth);

				// update ui
				string status = issue.Length > 0 ? Lib.BuildString("<color=yellow>", issue, "</color>") : growth > 0.99 ? "ready to harvest" : "growing";
				Events["Toggle"].guiName = Lib.StatusToggle("Greenhouse", active ? status : "disabled");
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
				Events["Toggle"].guiName = Lib.StatusToggle("Greenhouse", active ? "enabled" : "disabled");
			}
		}

		public void FixedUpdate()
		{
			// do nothing in the editor
			if (Lib.IsEditor()) return;

			// if enabled and not ready for harvest
			if (active && growth < 0.99)
			{
				// get vessel info from the cache
				// - if the vessel is not valid (eg: flagged as debris) then solar flux will be 0 and landed false (but that's okay)
				Vessel_info vi = Cache.VesselInfo(vessel);

				// get resource cache
				Vessel_resources resources = ResourceCache.Get(vessel);
				Resource_info ec = resources.Info(vessel, "ElectricCharge");

				// deal with corner cases when greenhouse is assembled using KIS
				if (double.IsNaN(growth) || double.IsInfinity(growth)) growth = 0.0;

				// calculate natural and artificial lighting
				natural = vi.solar_flux;
				artificial = Math.Max(light_tolerance - natural, 0.0);

				// consume EC for the lamps, scaled by artificial light intensity
				if (artificial > double.Epsilon) ec.Consume(ec_rate * (artificial / light_tolerance) * Kerbalism.elapsed_s, "greenhouse");

				// reset artificial lighting if there is no ec left
				// - comparing against amount in previous simulation step
				if (ec.amount <= double.Epsilon) artificial = 0.0;

				// execute recipe
				Resource_recipe recipe = new Resource_recipe(part, "greenhouse");
				foreach (ModuleResource input in resHandler.inputResources)
				{
					// WasteAtmosphere is primary combined input
					if (WACO2 && input.name == "WasteAtmosphere") recipe.Input(input.name, vi.breathable ? 0.0 : input.rate * Kerbalism.elapsed_s, "CarbonDioxide");
					// CarbonDioxide is secondary combined input
					else if (WACO2 && input.name == "CarbonDioxide") recipe.Input(input.name, vi.breathable ? 0.0 : input.rate * Kerbalism.elapsed_s, "");
					// if atmosphere is breathable disable WasteAtmosphere / CO2
					else if (!WACO2 && (input.name == "CarbonDioxide" || input.name == "WasteAtmosphere")) recipe.Input(input.name, vi.breathable ? 0.0 : input.rate, "");
					else recipe.Input(input.name, input.rate * Kerbalism.elapsed_s);
				}
				foreach (ModuleResource output in resHandler.outputResources)
				{
					// if atmosphere is breathable disable Oxygen
					if (output.name == "Oxygen") recipe.Output(output.name, vi.breathable ? 0.0 : output.rate * Kerbalism.elapsed_s, true);
					else recipe.Output(output.name, output.rate * Kerbalism.elapsed_s, true);
				}
				resources.Transform(recipe);

				// determine environment conditions
				bool lighting = natural + artificial >= light_tolerance;
				bool pressure = pressure_tolerance <= double.Epsilon || vi.pressure >= pressure_tolerance;
				bool radiation = radiation_tolerance <= double.Epsilon || vi.radiation * (1.0 - vi.shielding) < radiation_tolerance;

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
						if (dis_WACO2 || Cache.VesselInfo(vessel).breathable) continue;    // skip if already checked or atmosphere is breathable
						if (WACO2)
						{
							if (resources.Info(vessel, "WasteAtmosphere").amount <= double.Epsilon && resources.Info(vessel, "CarbonDioxide").amount <= double.Epsilon)
							{
								inputs = false;
								missing_res = "CarbonDioxide";
								break;
							}
							dis_WACO2 = true;
							continue;
						}
					}
					if (resources.Info(vessel, input.name).amount <= double.Epsilon)
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
						Message.Post(Lib.BuildString("On <b>", vessel.vesselName, "</b> the crop is ready to be harvested"));
						growth = 1.0;
					}
				}

				// update time-to-harvest
				tta = (1.0 - growth) / crop_rate;

				// update issues
				issue =
				  !inputs ? Lib.BuildString("missing ", missing_res)
				: !lighting ? "insufficient lighting"
				: !pressure ? "insufficient pressure"
				: !radiation ? "excessive radiation"
				: string.Empty;
			}
		}


		public static void BackgroundUpdate(Vessel v, ProtoPartModuleSnapshot m, Greenhouse g,
											Vessel_info vi, Vessel_resources resources, double elapsed_s)
		{
			// get protomodule data
			bool active = Lib.Proto.GetBool(m, "active");
			double growth = Lib.Proto.GetDouble(m, "growth");

			// if enabled and not ready for harvest
			if (active && growth < 0.99)
			{
				// get resource handler
				Resource_info ec = resources.Info(v, "ElectricCharge");

				// calculate natural and artificial lighting
				double natural = vi.solar_flux;
				double artificial = Math.Max(g.light_tolerance - natural, 0.0);

				// consume EC for the lamps, scaled by artificial light intensity
				if (artificial > double.Epsilon) ec.Consume(g.ec_rate * (artificial / g.light_tolerance) * elapsed_s, "greenhouse");

				// reset artificial lighting if there is no ec left
				// note: comparing against amount in previous simulation step
				if (ec.amount <= double.Epsilon) artificial = 0.0;

				// execute recipe
				Resource_recipe recipe = new Resource_recipe(g.part, "greenhouse");
				foreach (ModuleResource input in g.resHandler.inputResources) //recipe.Input(input.name, input.rate * elapsed_s);
				{
					// WasteAtmosphere is primary combined input
					if (g.WACO2 && input.name == "WasteAtmosphere") recipe.Input(input.name, vi.breathable ? 0.0 : input.rate * elapsed_s, "CarbonDioxide");
					// CarbonDioxide is secondary combined input
					else if (g.WACO2 && input.name == "CarbonDioxide") recipe.Input(input.name, vi.breathable ? 0.0 : input.rate * elapsed_s, "");
					// if atmosphere is breathable disable WasteAtmosphere / CO2
					else if (!g.WACO2 && (input.name == "CarbonDioxide" || input.name == "WasteAtmosphere")) recipe.Input(input.name, vi.breathable ? 0.0 : input.rate, "");
					else
						recipe.Input(input.name, input.rate * elapsed_s);
				}
				foreach (ModuleResource output in g.resHandler.outputResources)
				{
					// if atmosphere is breathable disable Oxygen
					if (output.name == "Oxygen") recipe.Output(output.name, vi.breathable ? 0.0 : output.rate * elapsed_s, true);
					else recipe.Output(output.name, output.rate * elapsed_s, true);
				}
				resources.Transform(recipe);

				// determine environment conditions
				bool lighting = natural + artificial >= g.light_tolerance;
				bool pressure = g.pressure_tolerance <= double.Epsilon || vi.pressure >= g.pressure_tolerance;
				bool radiation = g.radiation_tolerance <= double.Epsilon || vi.radiation * (1.0 - vi.shielding) < g.radiation_tolerance;

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
						if (dis_WACO2 || vi.breathable) continue;    // skip if already checked or atmosphere is breathable
						if (g.WACO2)
						{
							if (resources.Info(v, "WasteAtmosphere").amount <= double.Epsilon && resources.Info(v, "CarbonDioxide").amount <= double.Epsilon)
							{
								inputs = false;
								missing_res = "CarbonDioxide";
								break;
							}
							dis_WACO2 = true;
							continue;
						}
					}
					if (resources.Info(v, input.name).amount <= double.Epsilon)
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
						Message.Post(Lib.BuildString("On <b>", v.vesselName, "</b> the crop is ready to be harvested"));
						growth = 1.0;
					}
				}

				// update time-to-harvest
				double tta = (1.0 - growth) / g.crop_rate;

				// update issues
				string issue =
				  !inputs ? Lib.BuildString("missing ", missing_res)
				: !lighting ? "insufficient lighting"
				: !pressure ? "insufficient pressure"
				: !radiation ? "excessive radiation"
				: string.Empty;

				// update protomodule data
				Lib.Proto.Set(m, "natural", natural);
				Lib.Proto.Set(m, "artificial", artificial);
				Lib.Proto.Set(m, "tta", tta);
				Lib.Proto.Set(m, "issue", issue);
				Lib.Proto.Set(m, "growth", growth);
			}
		}


		// toggle greenhouse
		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_")]
		public void Toggle()
		{
			bool deactivating = active;

			// switch status
			active = !active;

			// play animation
			if (shutters_anim != null) shutters_anim.Play(deactivating ^ animBackwards, false);

			// refresh VAB/SPH ui
			if (Lib.IsEditor()) GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
		}


		// harvest
		[KSPEvent(guiActive = true, guiActiveUnfocused = true, guiName = "#KERBALISM_Greenhouse_Harvest", active = false)]
		public void Harvest()
		{
			// disable for dead eva kerbals
			Vessel v = FlightGlobals.ActiveVessel;
			if (v == null || EVA.IsDead(v)) return;

			// produce reduced quantity of food, proportional to current growth
			ResourceCache.Produce(vessel, crop_resource, crop_size, "greenhouse");

			// reset growth
			growth = 0.0;

			// show message
			Message.Post(Lib.BuildString("On <color=ffffff>", vessel.vesselName, "</color> harvest produced <color=ffffff>",
			  crop_size.ToString("F0"), " ", crop_resource, "</color>"));

			// record first harvest
			if (!Lib.Landed(vessel)) DB.landmarks.space_harvest = true;
		}


		// emergency harvest
		[KSPEvent(guiActive = true, guiActiveUnfocused = true, guiName = "#KERBALISM_Greenhouse_EmergencyHarvest", active = false)]
		public void EmergencyHarvest()
		{
			// disable for dead eva kerbals
			Vessel v = FlightGlobals.ActiveVessel;
			if (v == null || EVA.IsDead(v)) return;

			// calculate reduced harvest size
			double reduced_harvest = crop_size * growth * 0.5;

			// produce reduced quantity of food, proportional to current growth
			ResourceCache.Produce(vessel, crop_resource, reduced_harvest, "greenhouse");

			// reset growth
			growth = 0.0;

			// show message
			Message.Post(Lib.BuildString("On <color=ffffff>", vessel.vesselName, "</color> emergency harvest produced <color=ffffff>",
			  reduced_harvest.ToString("F0"), " ", crop_resource, "</color>"));

			// record first harvest
			if (!Lib.Landed(vessel)) DB.landmarks.space_harvest = true;
		}


		// action groups
		[KSPAction("#KERBALISM_Greenhouse_Action")] public void Action(KSPActionParam param) { Toggle(); }


		// part tooltip
		public override string GetInfo()
		{
			return Specs().Info("Grow crops in space and on the surface of celestial bodies, even far from the sun.");
		}


		// specifics support
		public Specifics Specs()
		{
			Specifics specs = new Specifics();

			specs.Add("Harvest size", Lib.HumanReadableAmount(crop_size, " " + crop_resource));
			specs.Add("Harvest time", Lib.HumanReadableDuration(1.0 / crop_rate));
			specs.Add("Lighting tolerance", Lib.HumanReadableFlux(light_tolerance));
			if (pressure_tolerance > double.Epsilon) specs.Add("Pressure tolerance", Lib.HumanReadablePressure(Sim.PressureAtSeaLevel() * pressure_tolerance));
			if (radiation_tolerance > double.Epsilon) specs.Add("Radiation tolerance", Lib.HumanReadableRadiation(radiation_tolerance));
			specs.Add("Lamps EC rate", Lib.HumanReadableRate(ec_rate));
			specs.Add(string.Empty);
			specs.Add("<color=#00ffff>Required resources</color>");

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
					specs.Add("CarbonDioxide", Lib.BuildString("<color=#ffaa00>", Lib.HumanReadableRate(input.rate + sec.rate), "</color>"));
					specs.Add("Crops can also use the CO2 in the atmosphere without a scrubber.");
					dis_WACO2 = true;
				}
				else
					specs.Add(input.name, Lib.BuildString("<color=#ffaa00>", Lib.HumanReadableRate(input.rate), "</color>"));
			}
			specs.Add(string.Empty);
			specs.Add("<color=#00ffff>By-products</color>");
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
		public string GetModuleTitle() { return "<size=1><color=#00000000>00</color></size>Greenhouse"; } // attempt to display at the top
		public override string GetModuleDisplayName() { return "<size=1><color=#00000000>00</color></size>Greenhouse"; } // Attempt to display at top of tooltip
		public string GetPrimaryField() { return String.Empty; }
		public Callback<Rect> GetDrawModulePanelCallback() { return null; }
	}


} // KERBALISM


