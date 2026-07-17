using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{

	public class Greenhouse : PartModule, IModuleInfo, ISpecifics, IContractObjectiveModule, IConfigurable
	{
		const double ReadyThreshold = 0.99;

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
		[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#KERBALISM_Greenhouse_AutoHarvest", groupName = "Greenhouse", groupDisplayName = "#KERBALISM_Group_Greenhouse")]
		[UI_Toggle(enabledText = "#KERBALISM_Generic_ON", disabledText = "#KERBALISM_Generic_OFF")]
		public bool auto_harvest = false;                                 // optional automatic harvest when crop is ready
		[KSPField(isPersistant = true)] public double growth;             // current growth level
		[KSPField(isPersistant = true)] public double natural;            // natural lighting flux
		[KSPField(isPersistant = true)] public double artificial;         // artificial lighting flux
		[KSPField(isPersistant = true)] public double tta;                // time to harvest
		[KSPField(isPersistant = true)] public string issue;              // first detected issue, or empty if there is none
		[KSPField(isPersistant = true)] public bool storage_wait_notified; // true after posting the waiting-for-storage message

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

		public void Configure(bool enable, int multiplier) {
			// multiplier is ignored for greenhouses
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
				if (part.IsPAWVisible())
				{
					string status = issue.Length > 0 ? Lib.BuildString("<color=yellow>", issue, "</color>") : growth >= ReadyThreshold ? Local.TELEMETRY_readytoharvest : Local.TELEMETRY_growing;//"ready to harvest""growing"
					Events["Toggle"].guiName = Lib.StatusToggle(Local.Greenhouse_Greenhouse, active ? status : Local.Greenhouse_disabled);//"Greenhouse""disabled"
					Fields["status_natural"].guiActive = active && growth < ReadyThreshold;
					Fields["status_artificial"].guiActive = active && growth < ReadyThreshold;
					Fields["status_tta"].guiActive = active && growth < ReadyThreshold;
					status_natural = Lib.HumanReadableFlux(natural);
					status_artificial = Lib.HumanReadableFlux(artificial);
					status_tta = Lib.HumanReadableDuration(tta);

					// show/hide harvest buttons
					bool manned = FlightGlobals.ActiveVessel.isEVA || Lib.CrewCount(vessel) > 0;
					Events["Harvest"].active = manned && growth >= ReadyThreshold;
					Events["EmergencyHarvest"].active = manned && growth >= 0.5 && growth < ReadyThreshold;
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

			// idle when disabled, or when ripe and waiting for a manual harvest
			if (!active) return;
			if (growth >= ReadyThreshold && !auto_harvest)
			{
				if (issue == Local.Greenhouse_issue4) issue = string.Empty;
				return;
			}

			// deal with corner cases when greenhouse is assembled using KIS
			if (double.IsNaN(growth) || double.IsInfinity(growth)) growth = 0.0;

			VesselData vd = vessel.KerbalismData();
			VesselResources resources = ResourceCache.Get(vessel);

			SimulateGreenhouse(
				vessel,
				this,
				vd,
				resources,
				Kerbalism.elapsed_s,
				auto_harvest,
				ref growth,
				ref natural,
				ref artificial,
				ref tta,
				ref issue,
				ref storage_wait_notified);
		}


		public static void BackgroundUpdate(Vessel v, ProtoPartModuleSnapshot m, Greenhouse g,
											VesselData vd, VesselResources resources, double elapsed_s)
		{
			Profiler.BeginSample("Greenhouse.BackgroundUpdate");
			bool active = Lib.Proto.GetBool(m, "active");
			bool auto_harvest = Lib.Proto.GetBool(m, "auto_harvest");
			double growth = Lib.Proto.GetDouble(m, "growth");

			if (active && (growth < ReadyThreshold || auto_harvest))
			{
				double natural = Lib.Proto.GetDouble(m, "natural");
				double artificial = Lib.Proto.GetDouble(m, "artificial");
				double tta = Lib.Proto.GetDouble(m, "tta");
				string issue = Lib.Proto.GetString(m, "issue");
				bool storage_wait_notified = Lib.Proto.GetBool(m, "storage_wait_notified");

				SimulateGreenhouse(
					v,
					g,
					vd,
					resources,
					elapsed_s,
					auto_harvest,
					ref growth,
					ref natural,
					ref artificial,
					ref tta,
					ref issue,
					ref storage_wait_notified);

				Lib.Proto.Set(m, "natural", natural);
				Lib.Proto.Set(m, "artificial", artificial);
				Lib.Proto.Set(m, "tta", tta);
				Lib.Proto.Set(m, "issue", issue);
				Lib.Proto.Set(m, "growth", growth);
				Lib.Proto.Set(m, "storage_wait_notified", storage_wait_notified);
			}
			else if (active && growth >= ReadyThreshold && Lib.Proto.GetString(m, "issue") == Local.Greenhouse_issue4)
			{
				Lib.Proto.Set(m, "issue", string.Empty);
			}
			Profiler.EndSample();
		}

		/// <summary>
		/// Shared loaded/background greenhouse simulation.
		/// Supports multi-cycle auto-harvest across large elapsed times and capacity-safe harvests.
		/// </summary>
		static void SimulateGreenhouse(
			Vessel v,
			Greenhouse g,
			VesselData vd,
			VesselResources resources,
			double elapsed_s,
			bool auto_harvest,
			ref double growth,
			ref double natural,
			ref double artificial,
			ref double tta,
			ref string issue,
			ref bool storage_wait_notified)
		{
			natural = vd.EnvSolarFluxTotal;
			artificial = Math.Max(g.light_tolerance - natural, 0.0);

			ResourceInfo ec = resources.GetResource(v, "ElectricCharge");
			if (Available(ec) <= double.Epsilon) artificial = 0.0;

			bool lighting = natural + artificial >= g.light_tolerance;
			bool pressure = g.pressure_tolerance <= double.Epsilon || vd.Pressure >= g.pressure_tolerance;
			bool radiation = g.radiation_tolerance <= double.Epsilon
				|| (1.0 - vd.Shielding) * vd.EnvHabitatRadiation < g.radiation_tolerance;

			bool inputs = HasInputs(v, g, vd, resources, out string missing_res);
			bool environment_ok = lighting && pressure && radiation;
			double remaining = Math.Max(elapsed_s, 0.0);
			double harvested_total = 0.0;
			bool became_ready_manual = false;
			issue = string.Empty;
			List<ResourceRecipe> immediate_recipes = new List<ResourceRecipe>(1);

			while (true)
			{
				if (growth >= ReadyThreshold)
				{
					growth = 1.0;

					if (!auto_harvest)
						break;

					if (TryAutoHarvest(v, resources, g.crop_resource, g.crop_size, ref growth, ref storage_wait_notified, ref harvested_total))
					{
						// continue growing with any remaining time
						if (remaining <= double.Epsilon)
							break;
						continue;
					}

					issue = Local.Greenhouse_issue4;
					if (!storage_wait_notified)
					{
						Message.Post(Local.Greenhouse_msg_storage.Format("<b>" + v.vesselName + "</b>"));
						storage_wait_notified = true;
					}
					break;
				}

				if (remaining <= double.Epsilon)
					break;

				if (!environment_ok || !inputs || g.crop_rate <= double.Epsilon)
				{
					// keep prior behavior: still run lamps/recipe while growing but blocked
					ExecuteResourceRecipe(v, g, vd, resources, remaining, immediate_recipes);
					double blocked_lamp_fraction = LampFraction(ec, g, artificial, remaining);
					ConsumeLamp(ec, g, artificial, remaining * blocked_lamp_fraction);
					remaining = 0.0;
					issue =
						!inputs ? Lib.BuildString(Local.Greenhouse_resoucesmissing.Format(missing_res))
						: !lighting ? Local.Greenhouse_issue1
						: !pressure ? Local.Greenhouse_issue2
						: !radiation ? Local.Greenhouse_issue3
						: string.Empty;
					break;
				}

				double time_to_ripe = (ReadyThreshold - growth) / g.crop_rate;
				double dt = Math.Min(remaining, time_to_ripe);
				if (dt <= double.Epsilon)
					break;

				double resource_fraction = InputFraction(v, g, vd, resources, dt);
				double lamp_fraction = LampFraction(ec, g, artificial, dt);
				double growth_fraction = Math.Min(resource_fraction, lamp_fraction);
				double processed_time = dt * growth_fraction;

				remaining -= dt;
				if (processed_time > double.Epsilon)
				{
					double executed_fraction = ExecuteResourceRecipe(v, g, vd, resources, processed_time, immediate_recipes);
					processed_time *= executed_fraction;
					growth_fraction = processed_time / dt;
					ConsumeLamp(ec, g, artificial, processed_time);
					growth += g.crop_rate * processed_time;
				}

				if (growth_fraction < 1.0 - 1e-9)
				{
					if (lamp_fraction < resource_fraction)
					{
						artificial = 0.0;
						issue = Local.Greenhouse_issue1;
					}
					else
					{
						HasInputs(v, g, vd, resources, out missing_res);
						issue = Lib.BuildString(Local.Greenhouse_resoucesmissing.Format(missing_res));
					}
					break;
				}

				if (growth >= ReadyThreshold - 1e-12)
				{
					growth = 1.0;
					if (!auto_harvest)
					{
						became_ready_manual = true;
						break;
					}
					// auto path handles harvest at the top of the next loop iteration
				}
			}

			if (harvested_total > double.Epsilon)
			{
				PostHarvestMessage(v, g.crop_resource, harvested_total, emergency: false);
				if (!Lib.Landed(v)) DB.landmarks.space_harvest = true;
			}
			else if (became_ready_manual)
			{
				Message.Post(Local.harvestedready_msg.Format("<b>" + v.vesselName + "</b>"));
			}

			if (g.crop_rate > double.Epsilon && growth < ReadyThreshold)
				tta = (ReadyThreshold - growth) / g.crop_rate;
			else
				tta = 0.0;

			if (issue.Length == 0 && growth >= ReadyThreshold && auto_harvest && !HasCropStorage(resources, v, g.crop_resource, g.crop_size))
				issue = Local.Greenhouse_issue4;
		}

		static double Available(ResourceInfo resource)
		{
			return Math.Max(resource.Amount + resource.Deferred, 0.0);
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

		static double InputFraction(Vessel v, Greenhouse g, VesselData vd, VesselResources resources, double elapsed_s)
		{
			if (elapsed_s <= double.Epsilon) return 1.0;

			double fraction = 1.0;
			bool checked_combined = false;
			foreach (ModuleResource input in g.resHandler.inputResources)
			{
				bool carbon_input = input.name == Habitat.WasteAtmoResName || input.name == "CarbonDioxide";
				if (carbon_input && vd.EnvBreathable) continue;

				if (carbon_input && g.WACO2)
				{
					if (checked_combined) continue;
					checked_combined = true;

					double combined_rate = 0.0;
					foreach (ModuleResource combined_input in g.resHandler.inputResources)
					{
						if (combined_input.name == Habitat.WasteAtmoResName || combined_input.name == "CarbonDioxide")
							combined_rate += combined_input.rate;
					}

					if (combined_rate > double.Epsilon)
					{
						double available_carbon = Available(resources.GetResource(v, Habitat.WasteAtmoResName))
							+ Available(resources.GetResource(v, "CarbonDioxide"));
						fraction = Math.Min(fraction, available_carbon / (combined_rate * elapsed_s));
					}
					continue;
				}

				if (input.rate > double.Epsilon)
				{
					double available = Available(resources.GetResource(v, input.name));
					fraction = Math.Min(fraction, available / (input.rate * elapsed_s));
				}
			}
			return Lib.Clamp(fraction, 0.0, 1.0);
		}

		static double ExecuteResourceRecipe(
			Vessel v,
			Greenhouse g,
			VesselData vd,
			VesselResources resources,
			double elapsed_s,
			List<ResourceRecipe> immediate_recipes)
		{
			if (elapsed_s <= double.Epsilon) return 1.0;

			double input_fraction = InputFraction(v, g, vd, resources, elapsed_s);
			if (input_fraction <= double.Epsilon) return 0.0;
			double processed_s = elapsed_s * input_fraction;

			ResourceRecipe recipe = new ResourceRecipe(ResourceBroker.Greenhouse);
			foreach (ModuleResource input in g.resHandler.inputResources)
			{
				if (g.WACO2 && input.name == Habitat.WasteAtmoResName)
					recipe.AddInput(input.name, vd.EnvBreathable ? 0.0 : input.rate * processed_s, "CarbonDioxide");
				else if (g.WACO2 && input.name == "CarbonDioxide")
					recipe.AddInput(input.name, vd.EnvBreathable ? 0.0 : input.rate * processed_s, "");
				else if (!g.WACO2 && (input.name == "CarbonDioxide" || input.name == Habitat.WasteAtmoResName))
					recipe.AddInput(input.name, vd.EnvBreathable ? 0.0 : input.rate * processed_s, "");
				else
					recipe.AddInput(input.name, input.rate * processed_s);
			}
			foreach (ModuleResource output in g.resHandler.outputResources)
			{
				if (output.name == "Oxygen")
					recipe.AddOutput(output.name, vd.EnvBreathable ? 0.0 : output.rate * processed_s, true);
				else
					recipe.AddOutput(output.name, output.rate * processed_s, true);
			}

			immediate_recipes.Clear();
			immediate_recipes.Add(recipe);
			ResourceRecipe.ExecuteRecipes(v, resources, immediate_recipes);
			return input_fraction * Lib.Clamp(1.0 - recipe.left, 0.0, 1.0);
		}

		static double LampFraction(ResourceInfo ec, Greenhouse g, double artificial, double elapsed_s)
		{
			if (artificial <= double.Epsilon || g.ec_rate <= double.Epsilon || elapsed_s <= double.Epsilon)
				return 1.0;

			double required = g.ec_rate * (artificial / g.light_tolerance) * elapsed_s;
			return Lib.Clamp(Available(ec) / required, 0.0, 1.0);
		}

		static void ConsumeLamp(ResourceInfo ec, Greenhouse g, double artificial, double elapsed_s)
		{
			if (artificial <= double.Epsilon || g.ec_rate <= double.Epsilon || elapsed_s <= double.Epsilon)
				return;

			double required = g.ec_rate * (artificial / g.light_tolerance) * elapsed_s;
			ec.Consume(required, ResourceBroker.Greenhouse);
		}

		static bool HasCropStorage(VesselResources resources, Vessel v, string resource_name, double amount)
		{
			ResourceInfo res = resources.GetResource(v, resource_name);
			return res.Capacity - (res.Amount + res.Deferred) + 1e-9 >= amount;
		}

		static bool TryAutoHarvest(
			Vessel v,
			VesselResources resources,
			string crop_resource,
			double crop_size,
			ref double growth,
			ref bool storage_wait_notified,
			ref double harvested_total)
		{
			if (crop_size <= double.Epsilon)
				return false;
			if (!HasCropStorage(resources, v, crop_resource, crop_size))
				return false;

			resources.Produce(v, crop_resource, crop_size, ResourceBroker.Greenhouse);
			growth = 0.0;
			storage_wait_notified = false;
			harvested_total += crop_size;
			return true;
		}

		static void PostHarvestMessage(Vessel v, string crop_resource, double amount, bool emergency)
		{
			string amount_text = "<color=ffffff>" + amount.ToString("F0") + " " + Lib.GetResourceDisplayName(crop_resource) + "</color>";
			if (emergency)
			{
				Message.Post(Lib.BuildString(
					Local.Greenhouse_msg_1.Format("<color=ffffff>" + v.vesselName + "</color> "),
					Local.Greenhouse_msg_3.Format(" " + amount_text)));
			}
			else
			{
				Message.Post(Lib.BuildString(
					Local.Greenhouse_msg_1.Format("<color=ffffff>" + v.vesselName + "</color> "),
					Local.Greenhouse_msg_2.Format(amount_text)));
			}
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

		[KSPEvent(guiActive = true, guiActiveUnfocused = true, guiName = "#KERBALISM_Greenhouse_Harvest", active = false, groupName = "Greenhouse", groupDisplayName = "#KERBALISM_Group_Greenhouse")]//Greenhouse
		public void Harvest()
		{
			ResourceCache.Produce(vessel, crop_resource, crop_size, ResourceBroker.Greenhouse);
			growth = 0.0;
			storage_wait_notified = false;
			PostHarvestMessage(vessel, crop_resource, crop_size, emergency: false);
			if (!Lib.Landed(vessel)) DB.landmarks.space_harvest = true;
		}

		[KSPEvent(guiActive = true, guiActiveUnfocused = true, guiName = "#KERBALISM_Greenhouse_EmergencyHarvest", active = false, groupName = "Greenhouse", groupDisplayName = "#KERBALISM_Group_Greenhouse")]//Greenhouse
		public void EmergencyHarvest()
		{
			double reduced_harvest = crop_size * growth * 0.5;
			ResourceCache.Produce(vessel, crop_resource, reduced_harvest, ResourceBroker.Greenhouse);
			growth = 0.0;
			storage_wait_notified = false;
			PostHarvestMessage(vessel, crop_resource, reduced_harvest, emergency: true);
			if (!Lib.Landed(vessel)) DB.landmarks.space_harvest = true;
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

			specs.Add(Local.Greenhouse_info1, Lib.HumanReadableAmount(crop_size, " " + Lib.GetResourceDisplayName(crop_resource)));//"Harvest size"
			specs.Add(Local.Greenhouse_info2, Lib.HumanReadableDuration(1.0 / crop_rate));//"Harvest time"
			specs.Add(Local.Greenhouse_info3, Lib.HumanReadableFlux(light_tolerance));//"Lighting tolerance"
			if (pressure_tolerance > double.Epsilon) specs.Add(Local.Greenhouse_info4, Lib.HumanReadablePressure(Sim.PressureAtSeaLevel() * pressure_tolerance));//"Pressure tolerance"
			if (radiation_tolerance > double.Epsilon) specs.Add(Local.Greenhouse_info5, Lib.HumanReadableRadiation(radiation_tolerance));//"Radiation tolerance"
			specs.Add(Local.Greenhouse_info6, Lib.HumanOrSIRate(ec_rate, Lib.ECResID));//"Lamps EC rate"
			specs.Add(string.Empty);
			specs.Add("<color=#00ffff>" + Local.Greenhouse_info7 + "</color>");//Required resources

			// do we have combined WasteAtmosphere and CO2
			Set_WACO2();
			bool dis_WACO2 = false;
			foreach (ModuleResource input in resHandler.inputResources)
			{
				// combine WasteAtmosphere and CO2 if both exist
				if (WACO2 && (input.name == Habitat.WasteAtmoResName || input.name == "CarbonDioxide"))
				{
					if (dis_WACO2) continue;
					ModuleResource sec;
					if (input.name == Habitat.WasteAtmoResName) sec = resHandler.inputResources.Find(x => x.name.Contains("CarbonDioxide"));
					else sec = resHandler.inputResources.Find(x => x.name.Contains(Habitat.WasteAtmoResName));
					specs.Add(Local.Greenhouse_CarbonDioxide, Lib.BuildString("<color=#ffaa00>", Lib.HumanOrSIRate(input.rate + sec.rate, "CarbonDioxide".GetHashCode()), " </color>"));//"CarbonDioxide"
					specs.Add(Local.Greenhouse_CarbonDioxide_desc);//"Crops can also use the CO2 in the atmosphere without a scrubber."
					dis_WACO2 = true;
				}
				else
					specs.Add(Lib.GetResourceDisplayName(input.name), Lib.BuildString("<color=#ffaa00>", Lib.HumanOrSIRate(input.rate, input.id), "</color>"));
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
		private void Set_WACO2()
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
				foreach (Greenhouse greenhouse in PartModuleCache.GetModules<Greenhouse>(v))
				{
					if (greenhouse.isEnabled && greenhouse.active)
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
				foreach (ProtoPartModuleSnapshot m in ProtoPartModuleCache.GetModules(v.protoVessel, "Greenhouse"))
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
		public string GetModuleTitle() { return "<size=1><color=#00000000>00</color></size>" + Local.Greenhouse; }
		public override string GetModuleDisplayName() { return "<size=1><color=#00000000>00</color></size>"+Local.Greenhouse; } // Attempt to display at top of tooltip//"Greenhouse"
		public string GetPrimaryField() { return String.Empty; }
		public Callback<Rect> GetDrawModulePanelCallback() { return null; }
	}


} // KERBALISM
