using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ModuleWheels;
using UnityEngine;


namespace KERBALISM
{


	public sealed class Planner
	{
		public Planner()
		{
			// left menu style
			leftmenu_style = new GUIStyle(HighLogic.Skin.label);
			leftmenu_style.richText = true;
			leftmenu_style.normal.textColor = new Color(0.0f, 0.0f, 0.0f, 1.0f);
			leftmenu_style.fixedWidth = 80.0f; // Fixed to avoid that the sun icon moves around for different planet name lengths
			leftmenu_style.stretchHeight = true;
			leftmenu_style.fontSize = Styles.ScaleInteger(10);
			leftmenu_style.alignment = TextAnchor.MiddleLeft;

			// right menu style
			rightmenu_style = new GUIStyle(leftmenu_style);
			rightmenu_style.alignment = TextAnchor.MiddleRight;

			// quote style
			quote_style = new GUIStyle(HighLogic.Skin.label);
			quote_style.richText = true;
			quote_style.normal.textColor = Color.black;
			quote_style.stretchWidth = true;
			quote_style.stretchHeight = true;
			quote_style.fontSize = Styles.ScaleInteger(11);
			quote_style.alignment = TextAnchor.LowerCenter;

			// center icon style
			icon_style = new GUIStyle();
			icon_style.alignment = TextAnchor.MiddleCenter;

			// set default body index & situation
			body_index = FlightGlobals.GetHomeBodyIndex();
			situation_index = 2;
			sunlight = true;

			// analyzers
			sim = new resource_simulator();
			env = new environment_analyzer();
			va = new vessel_analyzer();

			// resource panels
			panel_resource = new List<string>();
			Profile.supplies.FindAll(k => k.resource != "ElectricCharge").ForEach(k => panel_resource.Add(k.resource));

			// special panels
			// - stress & radiation panels require that a rule using the living_space/radiation modifier exist (current limitation)
			panel_special = new List<string>();
			if (Features.LivingSpace && Profile.rules.Find(k => k.modifiers.Contains("living_space")) != null) panel_special.Add("qol");
			if (Features.Radiation && Profile.rules.Find(k => k.modifiers.Contains("radiation")) != null) panel_special.Add("radiation");
			if (Features.Reliability) panel_special.Add("reliability");

			// environment panels
			panel_environment = new List<string>();
			if (Features.Pressure || Features.Poisoning) panel_environment.Add("habitat");
			panel_environment.Add("environment");

			// panel ui
			panel = new Panel();
		}


		public void update()
		{
			// clear the panel
			panel.clear();

			// if there is something in the editor
			if (EditorLogic.RootPart != null)
			{
				// get body, situation and altitude multiplier
				CelestialBody body = FlightGlobals.Bodies[body_index];
				string situation = situations[situation_index];
				double altitude_mult = altitude_mults[situation_index];

				// get parts recursively
				List<Part> parts = Lib.GetPartsRecursively(EditorLogic.RootPart);

				// analyze
				env.analyze(body, altitude_mult, sunlight);
				va.analyze(parts, sim, env);
				sim.analyze(parts, env, va);

				// ec panel
				render_ec(panel);

				// resource panel
				if (panel_resource.Count > 0)
				{
					render_resource(panel, panel_resource[resource_index]);
				}

				// special panel
				if (panel_special.Count > 0)
				{
					switch (panel_special[special_index])
					{
						case "qol": render_stress(panel); break;
						case "radiation": render_radiation(panel); break;
						case "reliability": render_reliability(panel); break;
					}
				}

				// environment panel
				switch (panel_environment[environment_index])
				{
					case "habitat": render_habitat(panel); break;
					case "environment": render_environment(panel); break;
				}
			}
		}


		public void render()
		{
			// if there is something in the editor
			if (EditorLogic.RootPart != null)
			{
				// get body, situation and altitude multiplier
				CelestialBody body = FlightGlobals.Bodies[body_index];
				string situation = situations[situation_index];
				double altitude_mult = altitude_mults[situation_index];

				// start header
				GUILayout.BeginHorizontal(Styles.title_container);

				// body selector
				GUILayout.Label(new GUIContent(body.name, "Target body"), leftmenu_style);
				if (Lib.IsClicked()) { body_index = (body_index + 1) % FlightGlobals.Bodies.Count; if (body_index == 0) ++body_index; }
				else if (Lib.IsClicked(1)) { body_index = (body_index - 1) % FlightGlobals.Bodies.Count; if (body_index == 0) body_index = FlightGlobals.Bodies.Count - 1; }

				// sunlight selector
				GUILayout.Label(new GUIContent(sunlight ? Icons.sun_white : Icons.sun_black, "In sunlight/shadow"), icon_style);
				if (Lib.IsClicked()) sunlight = !sunlight;

				// situation selector
				GUILayout.Label(new GUIContent(situation, "Target situation"), rightmenu_style);
				if (Lib.IsClicked()) { situation_index = (situation_index + 1) % situations.Length; }
				else if (Lib.IsClicked(1)) { situation_index = (situation_index == 0 ? situations.Length : situation_index) - 1; }

				// end header
				GUILayout.EndHorizontal();

				// render panel
				panel.render();
			}
			// if there is nothing in the editor
			else
			{
				// render quote
				GUILayout.FlexibleSpace();
				GUILayout.BeginHorizontal();
				GUILayout.Label("<i>In preparing for space, I have always found that\nplans are useless but planning is indispensable.\nWernher von Kerman</i>", quote_style);
				GUILayout.EndHorizontal();
				GUILayout.Space(Styles.ScaleFloat(10.0f));
			}
		}


		public float width()
		{
			return Styles.ScaleFloat(260.0f);
		}


		public float height()
		{
			if (EditorLogic.RootPart != null)
			{
				return Styles.ScaleFloat(30.0f) + panel.height(); // header + ui content
			}
			else
			{
				return Styles.ScaleFloat(66.0f); // quote-only
			}
		}


		void render_environment(Panel p)
		{
			string flux_tooltip = Lib.BuildString
			(
			  "<align=left /><b>source\t\tflux\t\ttemp</b>\n",
			  "solar\t\t", env.solar_flux > 0.0 ? Lib.HumanReadableFlux(env.solar_flux) : "none\t", "\t", Lib.HumanReadableTemp(Sim.BlackBodyTemperature(env.solar_flux)), "\n",
			  "albedo\t\t", env.albedo_flux > 0.0 ? Lib.HumanReadableFlux(env.albedo_flux) : "none\t", "\t", Lib.HumanReadableTemp(Sim.BlackBodyTemperature(env.albedo_flux)), "\n",
			  "body\t\t", env.body_flux > 0.0 ? Lib.HumanReadableFlux(env.body_flux) : "none\t", "\t", Lib.HumanReadableTemp(Sim.BlackBodyTemperature(env.body_flux)), "\n",
			  "background\t", Lib.HumanReadableFlux(Sim.BackgroundFlux()), "\t", Lib.HumanReadableTemp(Sim.BlackBodyTemperature(Sim.BackgroundFlux())), "\n",
			  "total\t\t", Lib.HumanReadableFlux(env.total_flux), "\t", Lib.HumanReadableTemp(Sim.BlackBodyTemperature(env.total_flux))
			);
			string atmosphere_tooltip = Lib.BuildString
			(
			  "<align=left />",
			  "breathable\t\t<b>", Sim.Breathable(env.body) ? "yes" : "no", "</b>\n",
			  "pressure\t\t<b>", Lib.HumanReadablePressure(env.body.atmospherePressureSeaLevel), "</b>\n",
			  "light absorption\t\t<b>", Lib.HumanReadablePerc(1.0 - env.atmo_factor), "</b>\n",
			  "gamma absorption\t<b>", Lib.HumanReadablePerc(1.0 - Sim.GammaTransparency(env.body, 0.0)), "</b>"
			);
			string shadowtime_str = Lib.HumanReadableDuration(env.shadow_period) + " (" + (env.shadow_time * 100.0).ToString("F0") + "%)";

			p.section("ENVIRONMENT", string.Empty, () => p.prev(ref environment_index, panel_environment.Count), () => p.next(ref environment_index, panel_environment.Count));
			p.content("temperature", Lib.HumanReadableTemp(env.temperature), env.body.atmosphere && env.landed ? "atmospheric" : flux_tooltip);
			p.content("difference", Lib.HumanReadableTemp(env.temp_diff), "difference between external and survival temperature");
			p.content("atmosphere", env.body.atmosphere ? "yes" : "no", atmosphere_tooltip);
			p.content("shadow time", shadowtime_str, "the time in shadow\nduring the orbit");
		}


		void render_ec(Panel p)
		{
			// get simulated resource
			simulated_resource res = sim.resource("ElectricCharge");

			// create tooltip
			string tooltip = res.tooltip();

			// render the panel section
			p.section("ELECTRIC CHARGE");
			p.content("storage", Lib.HumanReadableAmount(res.storage), tooltip);
			p.content("consumed", Lib.HumanReadableRate(res.consumed), tooltip);
			p.content("produced", Lib.HumanReadableRate(res.produced), tooltip);
			p.content("duration", Lib.HumanReadableDuration(res.lifetime()));
		}


		void render_resource(Panel p, string res_name)
		{
			// get simulated resource
			simulated_resource res = sim.resource(res_name);

			// create tooltip
			string tooltip = res.tooltip();

			// render the panel section
			p.section(Lib.SpacesOnCaps(res_name).ToUpper(), string.Empty, () => p.prev(ref resource_index, panel_resource.Count), () => p.next(ref resource_index, panel_resource.Count));
			p.content("storage", Lib.HumanReadableAmount(res.storage), tooltip);
			p.content("consumed", Lib.HumanReadableRate(res.consumed), tooltip);
			p.content("produced", Lib.HumanReadableRate(res.produced), tooltip);
			p.content("duration", Lib.HumanReadableDuration(res.lifetime()));
		}


		void render_stress(Panel p)
		{
			// get first living space rule
			// - guaranteed to exist, as this panel is not rendered if it doesn't
			// - even without crew, it is safe to evaluate the modifiers that use it
			Rule rule = Profile.rules.Find(k => k.modifiers.Contains("living_space"));

			// render title
			p.section("STRESS", string.Empty, () => p.prev(ref special_index, panel_special.Count), () => p.next(ref special_index, panel_special.Count));

			// render living space data
			// generate details tooltips
			string living_space_tooltip = Lib.BuildString
			(
			  "volume per-capita: <b>", Lib.HumanReadableVolume(va.volume / (double)Math.Max(va.crew_count, 1)), "</b>\n",
			  "ideal living space: <b>", Lib.HumanReadableVolume(Settings.IdealLivingSpace), "</b>"
			);
			p.content("living space", Habitat.living_space_to_string(va.living_space), living_space_tooltip);

			// render comfort data
			if (rule.modifiers.Contains("comfort"))
			{
				p.content("comfort", va.comforts.summary(), va.comforts.tooltip());
			}
			else
			{
				p.content("comfort", "n/a");
			}

			// render pressure data
			if (rule.modifiers.Contains("pressure"))
			{
				string pressure_tooltip = va.pressurized
				  ? "Free roaming in a pressurized environment is\nvastly superior to living in a suit."
				  : "Being forced inside a suit all the time greatly\nreduce the crew quality of life.\nThe worst part is the diaper.";
				p.content("pressurized", va.pressurized ? "yes" : "no", pressure_tooltip);
			}
			else
			{
				p.content("pressurized", "n/a");
			}

			// render life estimate
			double mod = Modifiers.evaluate(env, va, sim, rule.modifiers);
			p.content("duration", Lib.HumanReadableDuration(rule.fatal_threshold / (rule.degeneration * mod)));
		}


		void render_radiation(Panel p)
		{
			// get first radiation rule
			// - guaranteed to exist, as this panel is not rendered if it doesn't
			// - even without crew, it is safe to evaluate the modifiers that use it
			Rule rule = Profile.rules.Find(k => k.modifiers.Contains("radiation"));

			// detect if it use shielding
			bool use_shielding = rule.modifiers.Contains("shielding");

			// calculate various radiation levels
			var levels = new[]
			{
	  Math.Max(Radiation.Nominal, (env.surface_rad + va.emitted)),        // surface
      Math.Max(Radiation.Nominal, (env.magnetopause_rad + va.emitted)),   // inside magnetopause
      Math.Max(Radiation.Nominal, (env.inner_rad + va.emitted)),          // inside inner belt
      Math.Max(Radiation.Nominal, (env.outer_rad + va.emitted)),          // inside outer belt
      Math.Max(Radiation.Nominal, (env.heliopause_rad + va.emitted)),     // interplanetary
      Math.Max(Radiation.Nominal, (env.extern_rad + va.emitted)),         // interstellar
      Math.Max(Radiation.Nominal, (env.storm_rad + va.emitted))           // storm
    };

			// evaluate modifiers (except radiation)
			List<string> modifiers_except_radiation = new List<string>();
			foreach (string s in rule.modifiers) { if (s != "radiation") modifiers_except_radiation.Add(s); }
			double mod = Modifiers.evaluate(env, va, sim, modifiers_except_radiation);

			// calculate life expectancy at various radiation levels
			var estimates = new double[7];
			for (int i = 0; i < 7; ++i)
			{
				estimates[i] = rule.fatal_threshold / (rule.degeneration * mod * levels[i]);
			}

			// generate tooltip
			var mf = Radiation.Info(env.body).model;
			string tooltip = Lib.BuildString
			(
			  "<align=left />",
			  "surface\t\t<b>", Lib.HumanReadableDuration(estimates[0]), "</b>\n",
			  mf.has_pause ? Lib.BuildString("magnetopause\t<b>", Lib.HumanReadableDuration(estimates[1]), "</b>\n") : "",
			  mf.has_inner ? Lib.BuildString("inner belt\t<b>", Lib.HumanReadableDuration(estimates[2]), "</b>\n") : "",
			  mf.has_outer ? Lib.BuildString("outer belt\t<b>", Lib.HumanReadableDuration(estimates[3]), "</b>\n") : "",
			  "interplanetary\t<b>", Lib.HumanReadableDuration(estimates[4]), "</b>\n",
			  "interstellar\t<b>", Lib.HumanReadableDuration(estimates[5]), "</b>\n",
			  "storm\t\t<b>", Lib.HumanReadableDuration(estimates[6]), "</b>"
			);

			// render the panel
			p.section("RADIATION", string.Empty, () => p.prev(ref special_index, panel_special.Count), () => p.next(ref special_index, panel_special.Count));
			p.content("surface", Lib.HumanReadableRadiation(env.surface_rad + va.emitted), tooltip);
			p.content("orbit", Lib.HumanReadableRadiation(env.magnetopause_rad), tooltip);
			if (va.emitted >= 0.0) p.content("emission", Lib.HumanReadableRadiation(va.emitted), tooltip);
			else p.content("active shielding", Lib.HumanReadableRadiation(-va.emitted), tooltip);
			p.content("shielding", rule.modifiers.Contains("shielding") ? Habitat.shielding_to_string(va.shielding) : "n/a", tooltip);
		}


		void render_reliability(Panel p)
		{
			// evaluate redundancy metric
			// - 0: no redundancy
			// - 0.5: all groups have 2 elements
			// - 1.0: all groups have 3 or more elements
			double redundancy_metric = 0.0;
			foreach (var pair in va.redundancy)
			{
				switch (pair.Value)
				{
					case 1: break;
					case 2: redundancy_metric += 0.5 / (double)va.redundancy.Count; break;
					default: redundancy_metric += 1.0 / (double)va.redundancy.Count; break;
				}
			}

			// traduce the redundancy metric to string
			string redundancy_str = string.Empty;
			if (redundancy_metric <= 0.1) redundancy_str = "none";
			else if (redundancy_metric <= 0.33) redundancy_str = "poor";
			else if (redundancy_metric <= 0.66) redundancy_str = "okay";
			else redundancy_str = "great";

			// generate redundancy tooltip
			string redundancy_tooltip = string.Empty;
			if (va.redundancy.Count > 0)
			{
				StringBuilder sb = new StringBuilder();
				foreach (var pair in va.redundancy)
				{
					if (sb.Length > 0) sb.Append("\n");
					sb.Append("<b>");
					switch (pair.Value)
					{
						case 1: sb.Append("<color=red>"); break;
						case 2: sb.Append("<color=yellow>"); break;
						default: sb.Append("<color=green>"); break;
					}
					sb.Append(pair.Value.ToString());
					sb.Append("</color></b>\t");
					sb.Append(pair.Key);
				}
				redundancy_tooltip = Lib.BuildString("<align=left />", sb.ToString());
			}

			// generate repair string and tooltip
			string repair_str = "none";
			string repair_tooltip = string.Empty;
			if (va.crew_engineer)
			{
				repair_str = "engineer";
				repair_tooltip = "The engineer on board should\nbe able to handle all repairs";
			}
			else if (va.crew_capacity == 0)
			{
				repair_str = "safemode";
				repair_tooltip = "We have a chance of repairing\nsome of the malfunctions remotely";
			}

			// render panel
			p.section("RELIABILITY", string.Empty, () => p.prev(ref special_index, panel_special.Count), () => p.next(ref special_index, panel_special.Count));
			p.content("malfunctions", Lib.HumanReadableAmount(va.failure_year, "/y"), "average case estimate\nfor the whole vessel");
			p.content("high quality", Lib.HumanReadablePerc(va.high_quality), "percentage of high quality components");
			p.content("redundancy", redundancy_str, redundancy_tooltip);
			p.content("repair", repair_str, repair_tooltip);
		}

		void render_habitat(Panel p)
		{
			simulated_resource atmo_res = sim.resource("Atmosphere");
			simulated_resource waste_res = sim.resource("WasteAtmosphere");

			// generate tooltips
			string atmo_tooltip = atmo_res.tooltip();
			string waste_tooltip = waste_res.tooltip(true);

			// generate status string for scrubbing
			string waste_status = !Features.Poisoning                   //< feature disabled
			  ? "n/a"
			  : waste_res.produced <= double.Epsilon                    //< unnecessary
			  ? "not required"
			  : waste_res.consumed <= double.Epsilon                    //< no scrubbing
			  ? "<color=#ffff00>none</color>"
			  : waste_res.produced > waste_res.consumed * 1.001         //< insufficient scrubbing
			  ? "<color=#ffff00>inadequate</color>"
			  : "good";                                                 //< sufficient scrubbing

			// generate status string for pressurization
			string atmo_status = !Features.Pressure                     //< feature disabled
			  ? "n/a"
			  : atmo_res.consumed <= double.Epsilon                     //< unnecessary
			  ? "not required"
			  : atmo_res.produced <= double.Epsilon                     //< no pressure control
			  ? "none"
			  : atmo_res.consumed > atmo_res.produced * 1.001           //< insufficient pressure control
			  ? "<color=#ffff00>inadequate</color>"
			  : "good";                                                 //< sufficient pressure control

			p.section("HABITAT", string.Empty, () => p.prev(ref environment_index, panel_environment.Count), () => p.next(ref environment_index, panel_environment.Count));
			p.content("volume", Lib.HumanReadableVolume(va.volume), "volume of enabled habitats");
			p.content("surface", Lib.HumanReadableSurface(va.surface), "surface of enabled habitats");
			p.content("scrubbing", waste_status, waste_tooltip);
			p.content("pressurization", atmo_status, atmo_tooltip);
		}


		// store situations and altitude multipliers
		string[] situations = { "Landed", "Low Orbit", "Orbit", "High Orbit" };
		double[] altitude_mults = { 0.0, 0.33, 1.0, 3.0 };

		// styles
		GUIStyle leftmenu_style;
		GUIStyle rightmenu_style;
		GUIStyle quote_style;
		GUIStyle icon_style;

		// analyzers
		resource_simulator sim = new resource_simulator();
		environment_analyzer env = new environment_analyzer();
		vessel_analyzer va = new vessel_analyzer();

		// panel arrays
		List<string> panel_resource;
		List<string> panel_special;
		List<string> panel_environment;

		// body/situation/sunlight indexes
		int body_index;
		int situation_index;
		bool sunlight;

		// panel indexes
		int resource_index;
		int special_index;
		int environment_index;

		// panel ui
		Panel panel;
	}


	// analyze the environment
	public sealed class environment_analyzer
	{
		public void analyze(CelestialBody body, double altitude_mult, bool sunlight)
		{
			// shortcuts
			CelestialBody sun = FlightGlobals.Bodies[0];

			this.body = body;
			altitude = body.Radius * altitude_mult;
			landed = altitude <= double.Epsilon;
			breathable = Sim.Breathable(body) && landed;
			atmo_factor = Sim.AtmosphereFactor(body, 0.7071);
			sun_dist = Sim.Apoapsis(Lib.PlanetarySystem(body)) - sun.Radius - body.Radius;
			Vector3d sun_dir = (sun.position - body.position).normalized;
			solar_flux = sunlight ? Sim.SolarFlux(sun_dist) * (landed ? atmo_factor : 1.0) : 0.0;
			albedo_flux = sunlight ? Sim.AlbedoFlux(body, body.position + sun_dir * (body.Radius + altitude)) : 0.0;
			body_flux = Sim.BodyFlux(body, altitude);
			total_flux = solar_flux + albedo_flux + body_flux + Sim.BackgroundFlux();
			temperature = !landed || !body.atmosphere ? Sim.BlackBodyTemperature(total_flux) : body.GetTemperature(0.0);
			temp_diff = Sim.TempDiff(temperature, body, landed);
			orbital_period = Sim.OrbitalPeriod(body, altitude);
			shadow_period = Sim.ShadowPeriod(body, altitude);
			shadow_time = shadow_period / orbital_period;

			var rb = Radiation.Info(body);
			var sun_rb = Radiation.Info(sun);
			gamma_transparency = Sim.GammaTransparency(body, 0.0);
			extern_rad = Settings.ExternRadiation;
			heliopause_rad = extern_rad + sun_rb.radiation_pause;
			magnetopause_rad = heliopause_rad + rb.radiation_pause;
			inner_rad = magnetopause_rad + rb.radiation_inner;
			outer_rad = magnetopause_rad + rb.radiation_outer;
			surface_rad = magnetopause_rad * gamma_transparency;
			storm_rad = heliopause_rad + Settings.StormRadiation * (solar_flux > double.Epsilon ? 1.0 : 0.0);
		}


		public CelestialBody body;                            // target body
		public double altitude;                               // target altitude
		public bool landed;                                   // true if landed
		public bool breathable;                               // true if inside breathable atmosphere
		public double atmo_factor;                            // proportion of sun flux not absorbed by the atmosphere
		public double sun_dist;                               // distance from the sun
		public double solar_flux;                             // flux received from the sun (consider atmospheric absorption)
		public double albedo_flux;                            // solar flux reflected from the body
		public double body_flux;                              // infrared radiative flux from the body
		public double total_flux;                             // total flux at vessel position
		public double temperature;                            // vessel temperature
		public double temp_diff;                              // average difference from survival temperature
		public double orbital_period;                         // length of orbit
		public double shadow_period;                          // length of orbit in shadow
		public double shadow_time;                            // proportion of orbit that is in shadow

		public double gamma_transparency;                     // proportion of radiation not blocked by atmosphere
		public double extern_rad;                             // environment radiation outside the heliopause
		public double heliopause_rad;                         // environment radiation inside the heliopause
		public double magnetopause_rad;                       // environment radiation inside the magnetopause
		public double inner_rad;                              // environment radiation inside the inner belt
		public double outer_rad;                              // environment radiation inside the outer belt
		public double surface_rad;                            // environment radiation on the surface of the body
		public double storm_rad;                              // environment radiation during a solar storm, inside the heliopause
	}


	// analyze the vessel (excluding resource-related stuff)
	public sealed class vessel_analyzer
	{
		public void analyze(List<Part> parts, resource_simulator sim, environment_analyzer env)
		{
			// note: vessel analysis require resource analysis, but at the same time resource analysis
			// require vessel analysis, so we are using resource analysis from previous frame (that's okay)
			// in the past, it was the other way around - however that triggered a corner case when va.comforts
			// was null (because the vessel analysis was still never done) and some specific rule/process
			// in resource analysis triggered an exception, leading to the vessel analysis never happening
			// inverting their order avoided this corner-case

			analyze_crew(parts);
			analyze_habitat(sim, env);
			analyze_radiation(parts, sim);
			analyze_reliability(parts);
			analyze_qol(parts, sim, env);
			analyze_comms(parts);
		}


		void analyze_crew(List<Part> parts)
		{
			// get number of kerbals assigned to the vessel in the editor
			// note: crew manifest is not reset after root part is deleted
			var manifest = KSP.UI.CrewAssignmentDialog.Instance.GetManifest();
			List<ProtoCrewMember> crew = manifest.GetAllCrew(false).FindAll(k => k != null);
			crew_count = (uint)crew.Count;
			crew_engineer = crew.Find(k => k.trait == "Engineer") != null;
			crew_scientist = crew.Find(k => k.trait == "Scientist") != null;
			crew_pilot = crew.Find(k => k.trait == "Pilot") != null;

			crew_engineer_maxlevel = 0;
			crew_scientist_maxlevel = 0;
			crew_pilot_maxlevel = 0;
			foreach (ProtoCrewMember c in crew)
			{
				switch (c.trait)
				{
					case "Engineer": crew_engineer_maxlevel = Math.Max(crew_engineer_maxlevel, (uint)c.experienceLevel); break;
					case "Scientist": crew_scientist_maxlevel = Math.Max(crew_scientist_maxlevel, (uint)c.experienceLevel); break;
					case "Pilot": crew_pilot_maxlevel = Math.Max(crew_pilot_maxlevel, (uint)c.experienceLevel); break;
				}
			}

			// scan the parts
			crew_capacity = 0;
			foreach (Part p in parts)
			{
				// accumulate crew capacity
				crew_capacity += (uint)p.CrewCapacity;
			}

			// if the user press ALT, the planner consider the vessel crewed at full capacity
			if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) crew_count = crew_capacity;
		}


		void analyze_habitat(resource_simulator sim, environment_analyzer env)
		{
			// calculate total volume
			volume = sim.resource("Atmosphere").capacity;

			// calculate total surface
			surface = sim.resource("Shielding").capacity;

			// determine if the vessel has pressure control capabilities
			pressurized = sim.resource("Atmosphere").produced > 0.0 || env.breathable;

			// determine if the vessel has scrubbing capabilities
			scrubbed = sim.resource("WasteAtmosphere").consumed > 0.0 || env.breathable;
		}

		void analyze_comms(List<Part> parts)
		{
			has_comms = false;
			foreach (Part p in parts)
			{

				foreach (PartModule m in p.Modules)
				{
					// skip disabled modules
					if (!m.isEnabled) continue;

					if (m.moduleName == "ModuleDataTransmitter") has_comms = true;
					if (m.moduleName == "ModuleRTAntenna") //to ensure that short-range omnis that are built in don't count 
					{
						float omni_range = Lib.ReflectionValue<float>(m, "Mode1OmniRange");
						float dish_range = Lib.ReflectionValue<float>(m, "Mode1DishRange");
						Lib.Log("omni: " + omni_range.ToString());
						Lib.Log("dish: " + dish_range.ToString());
						if (omni_range >= 25000 || dish_range >= 25000) has_comms = true;//min 25 km range
					}
				}
			}
		}

		void analyze_radiation(List<Part> parts, resource_simulator sim)
		{
			// scan the parts
			emitted = 0.0;
			foreach (Part p in parts)
			{
				// for each module
				foreach (PartModule m in p.Modules)
				{
					// skip disabled modules
					if (!m.isEnabled) continue;

					// accumulate emitter radiation
					if (m.moduleName == "Emitter")
					{
						Emitter emitter = m as Emitter;

						emitted += emitter.running ? emitter.radiation : 0.0;
					}
				}
			}

			// calculate shielding factor
			double amount = sim.resource("Shielding").amount;
			double capacity = sim.resource("Shielding").capacity;
			shielding = (capacity > double.Epsilon ? amount / capacity : 1.0) * Settings.ShieldingEfficiency;
		}


		void analyze_reliability(List<Part> parts)
		{
			// reset data
			high_quality = 0.0;
			components = 0;
			failure_year = 0.0;
			redundancy = new Dictionary<string, int>();

			// scan the parts
			double year_time = 60.0 * 60.0 * Lib.HoursInDay() * Lib.DaysInYear();
			foreach (Part p in parts)
			{
				// for each module
				foreach (PartModule m in p.Modules)
				{
					// skip disabled modules
					if (!m.isEnabled) continue;

					// malfunctions
					if (m.moduleName == "Reliability")
					{
						Reliability reliability = m as Reliability;

						// calculate mtbf
						double mtbf = reliability.mtbf * (reliability.quality ? Settings.QualityScale : 1.0);

						// accumulate failures/y
						failure_year += year_time / mtbf;

						// accumulate high quality percentage
						high_quality += reliability.quality ? 1.0 : 0.0;

						// accumulate number of components
						++components;

						// compile redundancy data
						if (reliability.redundancy.Length > 0)
						{
							int count = 0;
							if (redundancy.TryGetValue(reliability.redundancy, out count))
							{
								redundancy[reliability.redundancy] = count + 1;
							}
							else
							{
								redundancy.Add(reliability.redundancy, 1);
							}
						}

					}
				}
			}

			// calculate high quality percentage
			high_quality /= (double)Math.Max(components, 1u);
		}


		void analyze_qol(List<Part> parts, resource_simulator sim, environment_analyzer env)
		{
			// calculate living space factor
			living_space = Lib.Clamp((volume / (double)Math.Max(crew_count, 1u)) / Settings.IdealLivingSpace, 0.1, 1.0);

			// calculate comfort factor
			comforts = new Comforts(parts, env.landed, crew_count > 1, has_comms);
		}


		// general
		public uint crew_count;                             // crew member on board
		public uint crew_capacity;                          // crew member capacity
		public bool crew_engineer;                          // true if an engineer is among the crew
		public bool crew_scientist;                         // true if a scientist is among the crew
		public bool crew_pilot;                             // true if a pilot is among the crew
		public uint crew_engineer_maxlevel;                 // experience level of top enginner on board
		public uint crew_scientist_maxlevel;                // experience level of top scientist on board
		public uint crew_pilot_maxlevel;                    // experience level of top pilot on board

		// habitat
		public double volume;                                 // total volume in m^3
		public double surface;                                // total surface in m^2
		public bool pressurized;                            // true if the vessel has pressure control capabilities
		public bool scrubbed;                               // true if the vessel has co2 scrubbing capabilities

		// radiation related
		public double emitted;                                // amount of radiation emitted by components
		public double shielding;                              // shielding factor

		// quality-of-life related
		public double living_space;                           // living space factor
		public Comforts comforts;                             // comfort info

		// reliability-related
		public uint components;                             // number of components that can fail
		public double high_quality;                           // percentual of high quality components
		public double failure_year;                           // estimated failures per-year, averaged per-component
		public Dictionary<string, int> redundancy;            // number of components per redundancy group

		public bool has_comms;
	}




	// simulate resource consumption & production
	public class resource_simulator
	{
		public void analyze(List<Part> parts, environment_analyzer env, vessel_analyzer va)
		{
			// clear previous resource state
			resources.Clear();

			// get amount and capacity from parts
			foreach (Part p in parts)
			{
				for (int i = 0; i < p.Resources.Count; ++i)
				{
					process_part(p, p.Resources[i].resourceName);
				}
			}

			// process all rules
			foreach (Rule r in Profile.rules)
			{
				if (r.input.Length > 0 && r.rate > 0.0)
				{
					process_rule(r, env, va);
				}
			}

			// process all processes
			foreach (Process p in Profile.processes)
			{
				process_process(p, env, va);
			}

			// process all modules
			foreach (Part p in parts)
			{
				// get planner controller in the part
				PlannerController ctrl = p.FindModuleImplementing<PlannerController>();

				// ignore all modules in the part if specified in controller
				if (ctrl != null && !ctrl.considered) continue;

				// for each module
				foreach (PartModule m in p.Modules)
				{
					// skip disabled modules
					// rationale: the Selector disable non-selected modules in this way
					if (!m.isEnabled) continue;

					switch (m.moduleName)
					{
						case "Greenhouse": process_greenhouse(m as Greenhouse, env, va); break;
						case "GravityRing": process_ring(m as GravityRing); break;
						case "Emitter": process_emitter(m as Emitter); break;
						case "Laboratory": process_laboratory(m as Laboratory); break;
						case "Experiment": process_experiment(m as Experiment); break;
						case "ModuleCommand": process_command(m as ModuleCommand); break;
						case "ModuleDeployableSolarPanel": process_panel(m as ModuleDeployableSolarPanel, env); break;
						case "ModuleGenerator": process_generator(m as ModuleGenerator, p); break;
						case "ModuleResourceConverter": process_converter(m as ModuleResourceConverter, va); break;
						case "ModuleKPBSConverter": process_converter(m as ModuleResourceConverter, va); break;
						case "ModuleResourceHarvester": process_harvester(m as ModuleResourceHarvester, va); break;
						case "ModuleScienceConverter": process_stocklab(m as ModuleScienceConverter); break;
						case "ModuleActiveRadiator": process_radiator(m as ModuleActiveRadiator); break;
						case "ModuleWheelMotor": process_wheel_motor(m as ModuleWheelMotor); break;
						case "ModuleWheelMotorSteering": process_wheel_steering(m as ModuleWheelMotorSteering); break;
						case "ModuleLight": process_light(m as ModuleLight); break;
						case "ModuleColoredLensLight": process_light(m as ModuleLight); break;
						case "ModuleMultiPointSurfaceLight": process_light(m as ModuleLight); break;
						case "SCANsat": process_scanner(m); break;
						case "ModuleSCANresourceScanner": process_scanner(m); break;
						case "ModuleCurvedSolarPanel": process_curved_panel(p, m, env); break;
						case "FissionGenerator": process_fission_generator(p, m); break;
						case "ModuleRadioisotopeGenerator": process_radioisotope_generator(p, m); break;
						case "ModuleCryoTank": process_cryotank(p, m); break;
						case "ModuleRTAntenna": process_rtantenna(p, m); break;
						case "ModuleDataTransmitter": process_datatransmitter(m as ModuleDataTransmitter); break;
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
					simulated_recipe recipe = recipes[i];
					if (recipe.left > double.Epsilon)
					{
						executing |= recipe.execute(this);
					}
				}
			}
			recipes.Clear();

			// clamp all resources
			foreach (var pair in resources) pair.Value.clamp();
		}


		public simulated_resource resource(string name)
		{
			simulated_resource res;
			if (!resources.TryGetValue(name, out res))
			{
				res = new simulated_resource();
				resources.Add(name, res);
			}
			return res;
		}


		void process_part(Part p, string res_name)
		{
			simulated_resource res = resource(res_name);
			res.storage += Lib.Amount(p, res_name);
			res.amount += Lib.Amount(p, res_name);
			res.capacity += Lib.Capacity(p, res_name);
		}


		void process_rule(Rule r, environment_analyzer env, vessel_analyzer va)
		{
			// deduce rate per-second
			double rate = (double)va.crew_count * (r.interval > 0.0 ? r.rate / r.interval : r.rate);

			// evaluate modifiers
			double k = Modifiers.evaluate(env, va, this, r.modifiers);

			// prepare recipe
			if (r.output.Length == 0)
			{
				resource(r.input).consume(rate * k, r.name);
			}
			else if (rate > double.Epsilon)
			{
				// - rules always dump excess overboard (because it is waste)
				simulated_recipe recipe = new simulated_recipe(r.name);
				recipe.input(r.input, rate * k);
				recipe.output(r.output, rate * k * r.ratio, true);
				recipes.Add(recipe);
			}
		}


		void process_process(Process p, environment_analyzer env, vessel_analyzer va)
		{
			// evaluate modifiers
			double k = Modifiers.evaluate(env, va, this, p.modifiers);

			// prepare recipe
			simulated_recipe recipe = new simulated_recipe(p.name);
			foreach (var input in p.inputs)
			{
				recipe.input(input.Key, input.Value * k);
			}
			foreach (var output in p.outputs)
			{
				recipe.output(output.Key, output.Value * k, p.dump.check(output.Key));
			}
			recipes.Add(recipe);
		}


		void process_greenhouse(Greenhouse g, environment_analyzer env, vessel_analyzer va)
		{
			// skip disabled greenhouses
			if (!g.active) return;

			// shortcut to resources
			simulated_resource ec = resource("ElectricCharge");
			simulated_resource res = resource(g.crop_resource);

			// calculate natural and artificial lighting
			double natural = env.solar_flux;
			double artificial = Math.Max(g.light_tolerance - natural, 0.0);

			// if lamps are on and artificial lighting is required
			if (artificial > 0.0)
			{
				// consume ec for the lamps
				ec.consume(g.ec_rate * (artificial / g.light_tolerance), "greenhouse");
			}

			// execute recipe
			simulated_recipe recipe = new simulated_recipe("greenhouse");
			foreach (ModuleResource input in g.resHandler.inputResources) recipe.input(input.name, input.rate);
			foreach (ModuleResource output in g.resHandler.outputResources) recipe.output(output.name, output.rate, true);
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
				res.produce(g.crop_size * g.crop_rate, "greenhouse");

				// add harvest info
				res.harvests.Add(Lib.BuildString(g.crop_size.ToString("F0"), " in ", Lib.HumanReadableDuration(1.0 / g.crop_rate)));
			}
		}


		void process_ring(GravityRing ring)
		{
			if (ring.deployed) resource("ElectricCharge").consume(ring.ec_rate, "gravity ring");
		}


		void process_emitter(Emitter emitter)
		{
			if (emitter.running) resource("ElectricCharge").consume(emitter.ec_rate, "emitter");
		}


		void process_laboratory(Laboratory lab)
		{
			// note: we are not checking if there is a scientist in the part
			if (lab.running)
			{
				resource("ElectricCharge").consume(lab.ec_rate, "laboratory");
			}
		}


		void process_experiment(Experiment exp)
		{
			if (exp.recording)
			{
				resource("ElectricCharge").consume(exp.ec_rate, exp.transmissible ? "sensor" : "experiment");
			}
		}


		void process_command(ModuleCommand command)
		{
			foreach (ModuleResource res in command.resHandler.inputResources)
			{
				resource(res.name).consume(res.rate, "command");
			}
		}


		void process_panel(ModuleDeployableSolarPanel panel, environment_analyzer env)
		{
			double generated = panel.resHandler.outputResources[0].rate * env.solar_flux / Sim.SolarFluxAtHome();
			resource("ElectricCharge").produce(generated, "solar panel");
		}


		void process_generator(ModuleGenerator generator, Part p)
		{
			// skip launch clamps, that include a generator
			if (Lib.PartName(p) == "launchClamp1") return;

			simulated_recipe recipe = new simulated_recipe("generator");
			foreach (ModuleResource res in generator.resHandler.inputResources)
			{
				recipe.input(res.name, res.rate);
			}
			foreach (ModuleResource res in generator.resHandler.outputResources)
			{
				recipe.output(res.name, res.rate, true);
			}
			recipes.Add(recipe);
		}


		void process_converter(ModuleResourceConverter converter, vessel_analyzer va)
		{
			// calculate experience bonus
			float exp_bonus = converter.UseSpecialistBonus
			  ? converter.EfficiencyBonus * (converter.SpecialistBonusBase + (converter.SpecialistEfficiencyFactor * (va.crew_engineer_maxlevel + 1)))
			  : 1.0f;

			// use part name as recipe name
			// - include crew bonus in the recipe name
			string recipe_name = Lib.BuildString(converter.part.partInfo.title, " (efficiency: ", Lib.HumanReadablePerc(exp_bonus), ")");

			// generate recipe
			simulated_recipe recipe = new simulated_recipe(recipe_name);
			foreach (ResourceRatio res in converter.inputList)
			{
				recipe.input(res.ResourceName, res.Ratio * exp_bonus);
			}
			foreach (ResourceRatio res in converter.outputList)
			{
				recipe.output(res.ResourceName, res.Ratio * exp_bonus, res.DumpExcess);
			}
			recipes.Add(recipe);
		}


		void process_harvester(ModuleResourceHarvester harvester, vessel_analyzer va)
		{
			// calculate experience bonus
			float exp_bonus = harvester.UseSpecialistBonus
			  ? harvester.EfficiencyBonus * (harvester.SpecialistBonusBase + (harvester.SpecialistEfficiencyFactor * (va.crew_engineer_maxlevel + 1)))
			  : 1.0f;

			// use part name as recipe name
			// - include crew bonus in the recipe name
			string recipe_name = Lib.BuildString(harvester.part.partInfo.title, " (efficiency: ", Lib.HumanReadablePerc(exp_bonus), ")");

			// generate recipe
			simulated_recipe recipe = new simulated_recipe(recipe_name);
			foreach (ResourceRatio res in harvester.inputList)
			{
				recipe.input(res.ResourceName, res.Ratio);
			}
			recipe.output(harvester.ResourceName, harvester.Efficiency * exp_bonus, true);
			recipes.Add(recipe);
		}


		void process_stocklab(ModuleScienceConverter lab)
		{
			resource("ElectricCharge").consume(lab.powerRequirement, "lab");
		}


		void process_radiator(ModuleActiveRadiator radiator)
		{
			// note: IsCooling is not valid in the editor, for deployable radiators,
			// we will have to check if the related deploy module is deployed
			// we use PlannerController instead
			foreach (var res in radiator.resHandler.inputResources)
			{
				resource(res.name).consume(res.rate, "radiator");
			}
		}


		void process_wheel_motor(ModuleWheelMotor motor)
		{
			foreach (var res in motor.resHandler.inputResources)
			{
				resource(res.name).consume(res.rate, "wheel");
			}
		}


		void process_wheel_steering(ModuleWheelMotorSteering steering)
		{
			foreach (var res in steering.resHandler.inputResources)
			{
				resource(res.name).consume(res.rate, "wheel");
			}
		}


		void process_light(ModuleLight light)
		{
			if (light.useResources && light.isOn)
			{
				resource("ElectricCharge").consume(light.resourceAmount, "light");
			}
		}


		void process_scanner(PartModule m)
		{
			resource("ElectricCharge").consume(SCANsat.EcConsumption(m), "SCANsat");
		}


		void process_curved_panel(Part p, PartModule m, environment_analyzer env)
		{
			// note: assume half the components are in sunlight, and average inclination is half

			// get total rate
			double tot_rate = Lib.ReflectionValue<float>(m, "TotalEnergyRate");

			// get number of components
			int components = p.FindModelTransforms(Lib.ReflectionValue<string>(m, "PanelTransformName")).Length;

			// approximate output
			// 0.7071: average clamped cosine
			resource("ElectricCharge").produce(tot_rate * 0.7071 * env.solar_flux / Sim.SolarFluxAtHome(), "curved panel");
		}


		void process_fission_generator(Part p, PartModule m)
		{
			double max_rate = Lib.ReflectionValue<float>(m, "PowerGeneration");

			// get fission reactor tweakable, will default to 1.0 for other modules
			var reactor = p.FindModuleImplementing<ModuleResourceConverter>();
			double tweakable = reactor == null ? 1.0 : Lib.ReflectionValue<float>(reactor, "CurrentPowerPercent") * 0.01f;

			resource("ElectricCharge").produce(max_rate * tweakable, "fission generator");
		}


		void process_radioisotope_generator(Part p, PartModule m)
		{
			double max_rate = Lib.ReflectionValue<float>(m, "BasePower");

			resource("ElectricCharge").produce(max_rate, "radioisotope generator");
		}


		void process_cryotank(Part p, PartModule m)
		{
			// note: assume cooling is active
			double cooling_cost = Lib.ReflectionValue<float>(m, "CoolingCost");
			string fuel_name = Lib.ReflectionValue<string>(m, "FuelName");

			resource("ElectricCharge").consume(cooling_cost * Lib.Capacity(p, fuel_name) * 0.001, "cryotank");
		}

		void process_rtantenna(Part p, PartModule m)
		{
			float ec_cost = Lib.ReflectionValue<float>(m, "EnergyCost");
			resource("ElectricCharge").consume(ec_cost, "communications");
		}

		void process_datatransmitter(ModuleDataTransmitter mdt)
		{
			resource("ElectricCharge").consume(mdt.packetResourceCost, "communications (transmitting)");
		}

		Dictionary<string, simulated_resource> resources = new Dictionary<string, simulated_resource>();
		List<simulated_recipe> recipes = new List<simulated_recipe>();
	}


	public sealed class simulated_resource
	{
		public simulated_resource()
		{
			consumers = new Dictionary<string, wrapper>();
			producers = new Dictionary<string, wrapper>();
			harvests = new List<string>();
		}

		public void consume(double quantity, string name)
		{
			if (quantity >= double.Epsilon)
			{
				amount -= quantity;
				consumed += quantity;

				if (!consumers.ContainsKey(name)) consumers.Add(name, new wrapper());
				consumers[name].value += quantity;
			}
		}

		public void produce(double quantity, string name)
		{
			if (quantity >= double.Epsilon)
			{
				amount += quantity;
				produced += quantity;

				if (!producers.ContainsKey(name)) producers.Add(name, new wrapper());
				producers[name].value += quantity;
			}
		}

		public void clamp()
		{
			amount = Lib.Clamp(amount, 0.0, capacity);
		}

		public double lifetime()
		{
			double rate = produced - consumed;
			return amount <= double.Epsilon ? 0.0 : rate > -1e-10 ? double.NaN : amount / -rate;
		}

		public string tooltip(bool invert = false)
		{
			var green = !invert ? producers : consumers;
			var red = !invert ? consumers : producers;

			var sb = new StringBuilder();
			foreach (var pair in green)
			{
				if (sb.Length > 0) sb.Append("\n");
				sb.Append("<b><color=#00ff00>");
				sb.Append(Lib.HumanReadableRate(pair.Value.value));
				sb.Append("</color></b>\t");
				sb.Append(pair.Key);
			}
			foreach (var pair in red)
			{
				if (sb.Length > 0) sb.Append("\n");
				sb.Append("<b><color=#ff0000>");
				sb.Append(Lib.HumanReadableRate(pair.Value.value));
				sb.Append("</color></b>\t");
				sb.Append(pair.Key);
			}
			if (harvests.Count > 0)
			{
				sb.Append("\n\n<b>Harvests</b>");
				foreach (string s in harvests)
				{
					sb.Append("\n");
					sb.Append(s);
				}
			}
			return Lib.BuildString("<align=left />", sb.ToString());
		}

		public double storage;                        // amount stored (at the start of simulation)
		public double capacity;                       // storage capacity
		public double amount;                         // amount stored (during simulation)
		public double consumed;                       // total consumption rate
		public double produced;                       // total production rate
		public List<string> harvests;                 // some extra data about harvests

		public class wrapper { public double value; }
		public Dictionary<string, wrapper> consumers; // consumers metadata
		public Dictionary<string, wrapper> producers; // producers metadata
	}


	public sealed class simulated_recipe
	{
		public simulated_recipe(string name)
		{
			this.name = name;
			this.inputs = new List<resource_recipe.entry>();
			this.outputs = new List<resource_recipe.entry>();
			this.left = 1.0;
		}

		// add an input to the recipe
		public void input(string resource_name, double quantity)
		{
			if (quantity > double.Epsilon) //< avoid division by zero
			{
				inputs.Add(new resource_recipe.entry(resource_name, quantity));
			}
		}

		// add an output to the recipe
		public void output(string resource_name, double quantity, bool dump)
		{
			if (quantity > double.Epsilon) //< avoid division by zero
			{
				outputs.Add(new resource_recipe.entry(resource_name, quantity, dump));
			}
		}

		// execute the recipe
		public bool execute(resource_simulator sim)
		{
			// determine worst input ratio
			double worst_input = left;
			if (outputs.Count > 0)
			{
				for (int i = 0; i < inputs.Count; ++i)
				{
					var e = inputs[i];
					simulated_resource res = sim.resource(e.name);
					worst_input = Lib.Clamp(res.amount * e.inv_quantity, 0.0, worst_input);
				}
			}

			// determine worst output ratio
			double worst_output = left;
			if (inputs.Count > 0)
			{
				for (int i = 0; i < outputs.Count; ++i)
				{
					var e = outputs[i];
					if (!e.dump) // ignore outputs that can dump overboard
					{
						simulated_resource res = sim.resource(e.name);
						worst_output = Lib.Clamp((res.capacity - res.amount) * e.inv_quantity, 0.0, worst_output);
					}
				}
			}

			// determine worst-io
			double worst_io = Math.Min(worst_input, worst_output);

			// consume inputs
			for (int i = 0; i < inputs.Count; ++i)
			{
				var e = inputs[i];
				simulated_resource res = sim.resource(e.name);
				res.consume(e.quantity * worst_io, name);
			}

			// produce outputs
			for (int i = 0; i < outputs.Count; ++i)
			{
				var e = outputs[i];
				simulated_resource res = sim.resource(e.name);
				res.produce(e.quantity * worst_io, name);
			}

			// update amount left to execute
			left -= worst_io;

			// the recipe was executed, at least partially
			return worst_io > double.Epsilon;
		}

		// store inputs and outputs
		public string name;                         // name used for consumer/producer tooltip
		public List<resource_recipe.entry> inputs;  // set of input resources
		public List<resource_recipe.entry> outputs; // set of output resources
		public double left;                         // what proportion of the recipe is left to execute
	}


} // KERBALISM
