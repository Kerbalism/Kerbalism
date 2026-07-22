using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{


	public class Greenhouse : PartModule, IModuleInfo, ISpecifics, IContractObjectiveModule, IConfigurable
	{
		// config
		[KSPField] public string crop_resource;         // name of resource produced continuously
		[KSPField] public double crop_size;             // legacy harvest size; Food rate = crop_size * crop_rate
		[KSPField] public double crop_rate;             // legacy growth-per-second; Food rate = crop_size * crop_rate
		[KSPField] public double ec_rate;               // EC/s consumed by the lamp at max capacity, set to 0 to disable the lamp
		[KSPField] public double light_tolerance;       // minimum lighting flux required for production, in W/m^2
		[KSPField] public double pressure_tolerance;    // minimum pressure required for production, in sea level atmospheres (optional)
		[KSPField] public double radiation_tolerance;   // maximum radiation allowed for production in rad/s, considered after shielding is applied (optional)
		[KSPField] public string lamps;                 // object with emissive texture used to represent intensity graphically
		[KSPField] public string shutters;              // animation to manipulate shutters
		[KSPField] public string plants;                // animation to represent plants graphically

		[KSPField] public bool animBackwards = false;   // If animation is playing in backward, this can help to fix

		// PartUpgrade efficiency multipliers (defaults preserve the configured rates)
		[KSPField] public double food_rate_mult = 1.0;
		[KSPField] public double input_rate_mult = 1.0;
		[KSPField] public double ec_rate_mult = 1.0;

		// persistence
		[KSPField(isPersistant = true)] public bool active;               // on/off flag
		[KSPField(isPersistant = true)] public double natural;            // natural lighting flux
		[KSPField(isPersistant = true)] public double artificial;         // artificial lighting flux
		[KSPField(isPersistant = true)] public string issue;              // first detected issue, or empty if there is none

		// rmb ui status
		[KSPField(guiActive = true, guiName = "#KERBALISM_Greenhouse_status_natural", groupName = "Greenhouse", groupDisplayName = "#KERBALISM_Group_Greenhouse")]//Greenhouse
		public string status_natural;        // natural lighting
		[KSPField(guiActive = true, guiName = "#KERBALISM_Greenhouse_status_artificial", groupName = "Greenhouse", groupDisplayName = "#KERBALISM_Group_Greenhouse")]//Greenhouse
		public string status_artificial;  // artificial lighting
		[KSPField(guiActive = false, guiName = "#KERBALISM_TELEMETRY_pressure", groupName = "Greenhouse", groupDisplayName = "#KERBALISM_Group_Greenhouse")]//Greenhouse
		public string status_pressure;       // habitat pressure

		// animations
		Animator shutters_anim;
		Animator plants_anim;

		// other data
		Renderer lamps_rdr;
		public bool WACO2 = false;        // true if we have combined WasteAtmosphere and CarbonDioxide

		private bool isConfigurable = false;

		public double FoodRate => crop_size * crop_rate * food_rate_mult;

		public void Configure(bool enable, int multiplier) {
			// multiplier is ignored for greenhouses
			active = enable;
		}

		public void ModuleIsConfigured() => isConfigurable = true;

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
			if (plants_anim != null) plants_anim.Still(active ? 1.0 : 0.0);

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

			// detect combined WasteAtmosphere / CO2 inputs
			Set_WACO2();
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
				if (plants_anim != null) plants_anim.Still(active ? 1.0 : 0.0);

				// update ui
				if (part.IsPAWVisible())
				{
					string status = issue.Length > 0
						? Lib.BuildString("<color=yellow>", issue, "</color>")
						: Local.Greenhouse_producing;
					Events["Toggle"].guiName = Lib.StatusToggle(Local.Greenhouse_Greenhouse, active ? status : Local.Greenhouse_disabled);//"Greenhouse""disabled"
					Fields["status_natural"].guiActive = active;
					Fields["status_artificial"].guiActive = active;
					Fields["status_pressure"].guiActive = active && Features.Pressure;
					status_natural = Lib.HumanReadableFlux(natural);
					status_artificial = Lib.HumanReadableFlux(artificial);
					if (active && Features.Pressure)
						status_pressure = Lib.HumanReadableNormalizedPressure(vessel.KerbalismData().Pressure);
				}
			}
			// in editor
			else if (part.IsPAWVisible())
			{
				// update ui
				Events["Toggle"].guiName = Lib.StatusToggle(Local.Greenhouse_Greenhouse, active ? Local.Greenhouse_enabled : Local.Greenhouse_disabled);//"Greenhouse""enabled""disabled"
			}
		}

		public void FixedUpdate()
		{
			// do nothing in the editor
			if (Lib.IsEditor()) return;

			if (!active) return;

			VesselData vd = vessel.KerbalismData();
			VesselResources resources = ResourceCache.Get(vessel);

			SimulateGreenhouse(
				vessel,
				this,
				vd,
				resources,
				Kerbalism.elapsed_s,
				ref natural,
				ref artificial,
				ref issue);
		}


		public static void BackgroundUpdate(Vessel v, ProtoPartModuleSnapshot m, Greenhouse g,
											VesselData vd, VesselResources resources, double elapsed_s)
		{
			Profiler.BeginSample("Greenhouse.BackgroundUpdate");
			bool active = Lib.Proto.GetBool(m, "active");

			if (active)
			{
				double natural = Lib.Proto.GetDouble(m, "natural");
				double artificial = Lib.Proto.GetDouble(m, "artificial");
				string issue = Lib.Proto.GetString(m, "issue");

				SimulateGreenhouse(
					v,
					g,
					vd,
					resources,
					elapsed_s,
					ref natural,
					ref artificial,
					ref issue);

				Lib.Proto.Set(m, "natural", natural);
				Lib.Proto.Set(m, "artificial", artificial);
				Lib.Proto.Set(m, "issue", issue);
			}
			Profiler.EndSample();
		}

		/// <summary>
		/// Shared loaded/background continuous greenhouse simulation.
		/// </summary>
		static void SimulateGreenhouse(
			Vessel v,
			Greenhouse g,
			VesselData vd,
			VesselResources resources,
			double elapsed_s,
			ref double natural,
			ref double artificial,
			ref string issue)
		{
			if (elapsed_s <= double.Epsilon)
				return;

			// background updates use the part prefab; ensure WACO2 is detected
			g.Set_WACO2();

			natural = vd.EnvSolarFluxTotal;
			ResourceInfo ec = resources.GetResource(v, "ElectricCharge");
			double lamp_ec_rate = g.ec_rate * g.ec_rate_mult;
			// ec_rate == 0 disables lamps: no artificial light fill-in.
			artificial = lamp_ec_rate > double.Epsilon
				? Math.Max(g.light_tolerance - natural, 0.0)
				: 0.0;
			bool lamps_needed = artificial > double.Epsilon;
			if (lamps_needed && Available(ec) <= double.Epsilon)
				artificial = 0.0;

			bool lighting = natural + artificial >= g.light_tolerance;
			bool pressure = g.pressure_tolerance <= double.Epsilon || vd.Pressure >= g.pressure_tolerance;
			bool radiation = g.radiation_tolerance <= double.Epsilon
				|| (1.0 - vd.Shielding) * vd.EnvHabitatRadiation < g.radiation_tolerance;

			bool inputs = HasInputs(v, g, vd, resources, out string missing_res);
			double food_rate = g.FoodRate;
			bool food_storage = food_rate <= double.Epsilon || HasCropStorage(resources, v, g.crop_resource);

			issue = string.Empty;
			if (!inputs)
				issue = Lib.BuildString(Local.Greenhouse_resoucesmissing.Format(missing_res));
			else if (!lighting)
				issue = Local.Greenhouse_issue1;
			else if (!pressure)
				issue = Local.Greenhouse_issue2;
			else if (!radiation)
				issue = Local.Greenhouse_issue3;
			else if (!food_storage)
				issue = Local.Greenhouse_issue4;

			// Resource availability and storage can be resolved by other queued recipes
			// in this simulation step. Environmental and hardware failures cannot.
			if (!lighting || !pressure || !radiation)
				return;

			ResourceRecipe recipe = new ResourceRecipe(ResourceBroker.Greenhouse);

			foreach (ModuleResource input in g.resHandler.inputResources)
			{
				double rate = input.rate * g.input_rate_mult;
				if (g.WACO2 && input.name == Habitat.WasteAtmoResName)
					recipe.AddInput(input.name, vd.EnvBreathable ? 0.0 : rate * elapsed_s, "CarbonDioxide");
				else if (g.WACO2 && input.name == "CarbonDioxide")
					recipe.AddInput(input.name, vd.EnvBreathable ? 0.0 : rate * elapsed_s, "");
				else if (!g.WACO2 && (input.name == "CarbonDioxide" || input.name == Habitat.WasteAtmoResName))
					recipe.AddInput(input.name, vd.EnvBreathable ? 0.0 : rate * elapsed_s, "");
				else
					recipe.AddInput(input.name, rate * elapsed_s);
			}

			foreach (ModuleResource output in g.resHandler.outputResources)
			{
				if (output.name == "Oxygen")
					recipe.AddOutput(output.name, vd.EnvBreathable ? 0.0 : output.rate * elapsed_s, true);
				else
					recipe.AddOutput(output.name, output.rate * elapsed_s, true);
			}

			if (food_rate > double.Epsilon)
				recipe.AddOutput(g.crop_resource, food_rate * elapsed_s, false);

			if (lamps_needed)
				recipe.AddInput("ElectricCharge", lamp_ec_rate * (artificial / g.light_tolerance) * elapsed_s);

			if (food_rate > double.Epsilon && !Lib.Landed(v))
			{
				recipe.onExecuted = executed_fraction =>
				{
					if (executed_fraction > double.Epsilon)
						DB.landmarks.space_harvest = true;
				};
			}

			resources.AddRecipe(recipe);
		}

		static double Available(ResourceInfo resource)
		{
			return Math.Max(resource.Amount + resource.Deferred, 0.0);
		}

		static bool HasCropStorage(VesselResources resources, Vessel v, string resource_name)
		{
			ResourceInfo res = resources.GetResource(v, resource_name);
			return res.Capacity - (res.Amount + res.Deferred) > 1e-9;
		}

		static bool HasInputs(Vessel v, Greenhouse g, VesselData vd, VesselResources resources, out string missing_resource)
		{
			missing_resource = string.Empty;
			bool checked_combined = false;

			foreach (ModuleResource input in g.resHandler.inputResources)
			{
				bool carbon_input = input.name == Habitat.WasteAtmoResName || input.name == "CarbonDioxide";
				if (carbon_input && vd.EnvBreathable) continue;

				if (carbon_input && g.WACO2)
				{
					if (checked_combined) continue;
					checked_combined = true;
					double available_carbon = Available(resources.GetResource(v, Habitat.WasteAtmoResName))
						+ Available(resources.GetResource(v, "CarbonDioxide"));
					if (available_carbon <= double.Epsilon)
					{
						missing_resource = "CarbonDioxide";
						return false;
					}
					continue;
				}

				if (Available(resources.GetResource(v, input.name)) <= double.Epsilon)
				{
					missing_resource = input.name;
					return false;
				}
			}
			return true;
		}

		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", groupName = "Greenhouse", groupDisplayName = "#KERBALISM_Group_Greenhouse")]//Greenhouse
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

			specs.Add(Local.Greenhouse_info1, Lib.HumanOrSIRate(FoodRate, crop_resource.GetHashCode()));// Food rate
			specs.Add(Local.Greenhouse_info3, Lib.HumanReadableFlux(light_tolerance));//"Lighting tolerance"
			if (pressure_tolerance > double.Epsilon) specs.Add(Local.Greenhouse_info4, Lib.HumanReadablePressure(Sim.PressureAtSeaLevel() * pressure_tolerance));//"Pressure tolerance"
			if (radiation_tolerance > double.Epsilon) specs.Add(Local.Greenhouse_info5, Lib.HumanReadableRadiation(radiation_tolerance));//"Radiation tolerance"
			specs.Add(Local.Greenhouse_info6, Lib.HumanOrSIRate(ec_rate * ec_rate_mult, Lib.ECResID));//"Lamps EC rate"
			specs.Add(string.Empty);
			specs.Add("<color=#00ffff>" + Local.Greenhouse_info7 + "</color>");//Required resources

			// do we have combined WasteAtmosphere and CO2
			Set_WACO2();
			bool dis_WACO2 = false;
			foreach (ModuleResource input in resHandler.inputResources)
			{
				double rate = input.rate * input_rate_mult;
				// combine WasteAtmosphere and CO2 if both exist
				if (WACO2 && (input.name == Habitat.WasteAtmoResName || input.name == "CarbonDioxide"))
				{
					if (dis_WACO2) continue;
					ModuleResource sec;
					if (input.name == Habitat.WasteAtmoResName) sec = resHandler.inputResources.Find(x => x.name.Contains("CarbonDioxide"));
					else sec = resHandler.inputResources.Find(x => x.name.Contains(Habitat.WasteAtmoResName));
					specs.Add(Local.Greenhouse_CarbonDioxide, Lib.BuildString("<color=#ffaa00>", Lib.HumanOrSIRate((input.rate + sec.rate) * input_rate_mult, "CarbonDioxide".GetHashCode()), " </color>"));//"CarbonDioxide"
					specs.Add(Local.Greenhouse_CarbonDioxide_desc);//"Crops can also use the CO2 in the atmosphere without a scrubber."
					dis_WACO2 = true;
				}
				else
					specs.Add(Lib.GetResourceDisplayName(input.name), Lib.BuildString("<color=#ffaa00>", Lib.HumanOrSIRate(rate, input.id), "</color>"));
			}
			specs.Add(string.Empty);
			specs.Add("<color=#00ffff>"+Local.Greenhouse_Byproducts +"</color>");//By-products
			foreach (ModuleResource output in resHandler.outputResources)
			{
				specs.Add(Lib.GetResourceDisplayName(output.name), Lib.BuildString("<color=#00ff00>", Lib.HumanOrSIRate(output.rate, output.id), "</color>"));
			}
			return specs;
		}

		/// <summary>
		/// checks if we have WasteAtmosphere and CarbonDioxide inputs and sets the WACO2 flag accordingly
		/// </summary>
		public void Set_WACO2()
		{
			WACO2 = false;
			foreach (ModuleResource input in resHandler.inputResources)
			{
				// we have combined WasteAtmosphere and CO2 if both exist
				if (input.name == Habitat.WasteAtmoResName || input.name == "CarbonDioxide")
				{
					ModuleResource sec;
					if (input.name == Habitat.WasteAtmoResName)
					{
						sec = resHandler.inputResources.Find(x => x.name.Contains("CarbonDioxide"));
						// no CO2, we only have WasteAtmosphere
						if (sec == null) return;
					}
					else
					{
						sec = resHandler.inputResources.Find(x => x.name.Contains(Habitat.WasteAtmoResName));
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
			public double natural;          // natural lighting
			public double artificial;       // artificial lighting
			public string issue;            // first issue detected, or empty
		}
		public static List<Data> Greenhouses(Vessel v)
		{
			List<Data> ret = new List<Data>();
			if (v.loaded)
			{
				foreach (Greenhouse greenhouse in PartModuleCache.GetModules<Greenhouse>(v))
				{
					if (greenhouse.isEnabled && greenhouse.active)
					{
						Data gd = new Data
						{
							natural = greenhouse.natural,
							artificial = greenhouse.artificial,
							issue = greenhouse.issue
						};
						ret.Add(gd);
					}
				}
			}
			else
			{
				foreach (ProtoPartModuleSnapshot m in ProtoPartModuleCache.GetModules(v.protoVessel, "Greenhouse"))
				{
					if (Lib.Proto.GetBool(m, "active"))
					{
						Data gd = new Data
						{
							natural = Lib.Proto.GetDouble(m, "natural"),
							artificial = Lib.Proto.GetDouble(m, "artificial"),
							issue = Lib.Proto.GetString(m, "issue")
						};
						ret.Add(gd);
					}
				}
			}
			return ret;
		}

		// module info support
		public string GetModuleTitle() { return "<size=1><color=#00000000>00</color></size>" + Local.Greenhouse; }
		public override string GetModuleDisplayName() { return "<size=1><color=#00000000>00</color></size>"+Local.Greenhouse; } // Attempt to display at top of tooltip//"Greenhouse"
		public string GetPrimaryField() { return String.Empty; }
		public Callback<Rect> GetDrawModulePanelCallback() { return null; }
	}


} // KERBALISM
