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
			leftmenu_style = new GUIStyle(HighLogic.Skin.label)
			{
				richText = true
			};
			leftmenu_style.normal.textColor = new Color(0.0f, 0.0f, 0.0f, 1.0f);
			leftmenu_style.fixedWidth = Styles.ScaleWidthFloat(80.0f); // Fixed to avoid that the sun icon moves around for different planet name lengths
			leftmenu_style.stretchHeight = true;
			leftmenu_style.fontSize = Styles.ScaleInteger(10);
			leftmenu_style.alignment = TextAnchor.MiddleLeft;

			// right menu style
			rightmenu_style = new GUIStyle(leftmenu_style)
			{
				alignment = TextAnchor.MiddleRight
			};

			// quote style
			quote_style = new GUIStyle(HighLogic.Skin.label)
			{
				richText = true
			};
			quote_style.normal.textColor = Color.black;
			quote_style.stretchWidth = true;
			quote_style.stretchHeight = true;
			quote_style.fontSize = Styles.ScaleInteger(11);
			quote_style.alignment = TextAnchor.LowerCenter;

			// center icon style
			icon_style = new GUIStyle
			{
				alignment = TextAnchor.MiddleCenter
			};

			// set default body index & situation
			body_index = FlightGlobals.GetHomeBodyIndex();
			situation_index = 2;
			sunlight = true;

			// analyzers
			sim = new Resource_simulator();
			env = new Environment_analyzer();
			va = new Vessel_analyzer();

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


		public void Update()
		{
			// clear the panel
			panel.Clear();

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
				env.Analyze(body, altitude_mult, sunlight);
				va.Analyze(parts, sim, env);
				sim.Analyze(parts, env, va);

				// ec panel
				Render_ec(panel);

				// resource panel
				if (panel_resource.Count > 0)
				{
					Render_resource(panel, panel_resource[resource_index]);
				}

				// special panel
				if (panel_special.Count > 0)
				{
					switch (panel_special[special_index])
					{
						case "qol": Render_stress(panel); break;
						case "radiation": Render_radiation(panel); break;
						case "reliability": Render_reliability(panel); break;
					}
				}

				// environment panel
				switch (panel_environment[environment_index])
				{
					case "habitat": Render_habitat(panel); break;
					case "environment": Render_environment(panel); break;
				}
			}
		}


		public void Render()
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
				panel.Render();
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


		public float Width()
		{
			return Styles.ScaleWidthFloat(280.0f);
		}


		public float Height()
		{
			if (EditorLogic.RootPart != null)
			{
				return Styles.ScaleFloat(30.0f) + panel.Height(); // header + ui content
			}
			else
			{
				return Styles.ScaleFloat(66.0f); // quote-only
			}
		}


		void Render_environment(Panel p)
		{
			string flux_tooltip = Lib.BuildString
			(
				"<align=left />" +
				String.Format("<b>{0,-14}\t{1,-15}\t{2}</b>\n", "Source", "Flux", "Temp"),
				String.Format("{0,-14}\t{1,-15}\t{2}\n", "solar", env.solar_flux > 0.0 ? Lib.HumanReadableFlux(env.solar_flux) : "none", Lib.HumanReadableTemp(Sim.BlackBodyTemperature(env.solar_flux))),
				String.Format("{0,-14}\t{1,-15}\t{2}\n", "albedo", env.albedo_flux > 0.0 ? Lib.HumanReadableFlux(env.albedo_flux) : "none", Lib.HumanReadableTemp(Sim.BlackBodyTemperature(env.albedo_flux))),
				String.Format("{0,-14}\t{1,-15}\t{2}\n", "body", env.body_flux > 0.0 ? Lib.HumanReadableFlux(env.body_flux) : "none", Lib.HumanReadableTemp(Sim.BlackBodyTemperature(env.body_flux))),
				String.Format("{0,-14}\t{1,-15}\t{2}\n", "background", Lib.HumanReadableFlux(Sim.BackgroundFlux()), Lib.HumanReadableTemp(Sim.BlackBodyTemperature(Sim.BackgroundFlux()))),
				String.Format("{0,-14}\t\t{1,-15}\t{2}", "total", Lib.HumanReadableFlux(env.total_flux), Lib.HumanReadableTemp(Sim.BlackBodyTemperature(env.total_flux)))
			);
			string atmosphere_tooltip = Lib.BuildString
			(
				"<align=left />",
				String.Format("{0,-14}\t<b>{1}</b>\n", "breathable", Sim.Breathable(env.body) ? "yes" : "no"),
				String.Format("{0,-14}\t<b>{1}</b>\n", "pressure", Lib.HumanReadablePressure(env.body.atmospherePressureSeaLevel)),
				String.Format("{0,-14}\t<b>{1}</b>\n", "light absorption", Lib.HumanReadablePerc(1.0 - env.atmo_factor)),
				String.Format("{0,-14}\t<b>{1}</b>", "gamma absorption", Lib.HumanReadablePerc(1.0 - Sim.GammaTransparency(env.body, 0.0)))
			);
			string shadowtime_str = Lib.HumanReadableDuration(env.shadow_period) + " (" + (env.shadow_time * 100.0).ToString("F0") + "%)";

			p.AddSection("ENVIRONMENT", string.Empty, () => p.Prev(ref environment_index, panel_environment.Count), () => p.Next(ref environment_index, panel_environment.Count));
			p.AddContent("temperature", Lib.HumanReadableTemp(env.temperature), env.body.atmosphere && env.landed ? "atmospheric" : flux_tooltip);
			p.AddContent("difference", Lib.HumanReadableTemp(env.temp_diff), "difference between external and survival temperature");
			p.AddContent("atmosphere", env.body.atmosphere ? "yes" : "no", atmosphere_tooltip);
			p.AddContent("shadow time", shadowtime_str, "the time in shadow\nduring the orbit");
		}


		void Render_ec(Panel p)
		{
			// get simulated resource
			Simulated_resource res = sim.Resource("ElectricCharge");

			// create tooltip
			string tooltip = res.Tooltip();

			// render the panel section
			p.AddSection("ELECTRIC CHARGE");
			p.AddContent("storage", Lib.HumanReadableAmount(res.storage), tooltip);
			p.AddContent("consumed", Lib.HumanReadableRate(res.consumed), tooltip);
			p.AddContent("produced", Lib.HumanReadableRate(res.produced), tooltip);
			p.AddContent("duration", Lib.HumanReadableDuration(res.Lifetime()));
		}


		void Render_resource(Panel p, string res_name)
		{
			// get simulated resource
			Simulated_resource res = sim.Resource(res_name);

			// create tooltip
			string tooltip = res.Tooltip();

			// render the panel section
			p.AddSection(Lib.SpacesOnCaps(res_name).ToUpper(), string.Empty, () => p.Prev(ref resource_index, panel_resource.Count), () => p.Next(ref resource_index, panel_resource.Count));
			p.AddContent("storage", Lib.HumanReadableAmount(res.storage), tooltip);
			p.AddContent("consumed", Lib.HumanReadableRate(res.consumed), tooltip);
			p.AddContent("produced", Lib.HumanReadableRate(res.produced), tooltip);
			p.AddContent("duration", Lib.HumanReadableDuration(res.Lifetime()));
		}


		void Render_stress(Panel p)
		{
			// get first living space rule
			// - guaranteed to exist, as this panel is not rendered if it doesn't
			// - even without crew, it is safe to evaluate the modifiers that use it
			Rule rule = Profile.rules.Find(k => k.modifiers.Contains("living_space"));

			// render title
			p.AddSection("STRESS", string.Empty, () => p.Prev(ref special_index, panel_special.Count), () => p.Next(ref special_index, panel_special.Count));

			// render living space data
			// generate details tooltips
			string living_space_tooltip = Lib.BuildString
			(
				"volume per-capita:<b>\t", Lib.HumanReadableVolume(va.volume / (double)Math.Max(va.crew_count, 1)), "</b>\n",
				"ideal living space:<b>\t", Lib.HumanReadableVolume(Settings.IdealLivingSpace), "</b>"
			);
			p.AddContent("living space", Habitat.Living_space_to_string(va.living_space), living_space_tooltip);

			// render comfort data
			if (rule.modifiers.Contains("comfort"))
			{
				p.AddContent("comfort", va.comforts.Summary(), va.comforts.Tooltip());
			}
			else
			{
				p.AddContent("comfort", "n/a");
			}

			// render pressure data
			if (rule.modifiers.Contains("pressure"))
			{
				string pressure_tooltip = va.pressurized
				  ? "Free roaming in a pressurized environment is\nvastly superior to living in a suit."
				  : "Being forced inside a suit all the time greatly\nreduces the crews quality of life.\nThe worst part is the diaper.";
				p.AddContent("pressurized", va.pressurized ? "yes" : "no", pressure_tooltip);
			}
			else
			{
				p.AddContent("pressurized", "n/a");
			}

			// render life estimate
			double mod = Modifiers.Evaluate(env, va, sim, rule.modifiers);
			p.AddContent("duration", Lib.HumanReadableDuration(rule.fatal_threshold / (rule.degeneration * mod)));
		}


		void Render_radiation(Panel p)
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
			double mod = Modifiers.Evaluate(env, va, sim, modifiers_except_radiation);

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
				String.Format("{0,-20}\t<b>{1}</b>\n", "surface", Lib.HumanReadableDuration(estimates[0])),
				mf.has_pause ? String.Format("{0,-20}\t<b>{1}</b>\n", "magnetopause", Lib.HumanReadableDuration(estimates[1])) : "",
				mf.has_inner ? String.Format("{0,-20}\t<b>{1}</b>\n", "inner belt", Lib.HumanReadableDuration(estimates[2])) : "",
				mf.has_outer ? String.Format("{0,-20}\t<b>{1}</b>\n", "outer belt", Lib.HumanReadableDuration(estimates[3])) : "",
				String.Format("{0,-20}\t<b>{1}</b>\n", "interplanetary", Lib.HumanReadableDuration(estimates[4])),
				String.Format("{0,-20}\t<b>{1}</b>\n", "interstellar", Lib.HumanReadableDuration(estimates[5])),
				String.Format("{0,-20}\t<b>{1}</b>", "storm", Lib.HumanReadableDuration(estimates[6]))
			);

			// render the panel
			p.AddSection("RADIATION", string.Empty, () => p.Prev(ref special_index, panel_special.Count), () => p.Next(ref special_index, panel_special.Count));
			p.AddContent("surface", Lib.HumanReadableRadiation(env.surface_rad + va.emitted), tooltip);
			p.AddContent("orbit", Lib.HumanReadableRadiation(env.magnetopause_rad), tooltip);
			if (va.emitted >= 0.0) p.AddContent("emission", Lib.HumanReadableRadiation(va.emitted), tooltip);
			else p.AddContent("active shielding", Lib.HumanReadableRadiation(-va.emitted), tooltip);
			p.AddContent("shielding", rule.modifiers.Contains("shielding") ? Habitat.Shielding_to_string(va.shielding) : "n/a", tooltip);
		}


		void Render_reliability(Panel p)
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
			p.AddSection("RELIABILITY", string.Empty, () => p.Prev(ref special_index, panel_special.Count), () => p.Next(ref special_index, panel_special.Count));
			p.AddContent("malfunctions", Lib.HumanReadableAmount(va.failure_year, "/y"), "average case estimate\nfor the whole vessel");
			p.AddContent("high quality", Lib.HumanReadablePerc(va.high_quality), "percentage of high quality components");
			p.AddContent("redundancy", redundancy_str, redundancy_tooltip);
			p.AddContent("repair", repair_str, repair_tooltip);
		}

		void Render_habitat(Panel p)
		{
			Simulated_resource atmo_res = sim.Resource("Atmosphere");
			Simulated_resource waste_res = sim.Resource("WasteAtmosphere");

			// generate tooltips
			string atmo_tooltip = atmo_res.Tooltip();
			string waste_tooltip = waste_res.Tooltip(true);

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

			p.AddSection("HABITAT", string.Empty, () => p.Prev(ref environment_index, panel_environment.Count), () => p.Next(ref environment_index, panel_environment.Count));
			p.AddContent("volume", Lib.HumanReadableVolume(va.volume), "volume of enabled habitats");
			p.AddContent("surface", Lib.HumanReadableSurface(va.surface), "surface of enabled habitats");
			p.AddContent("scrubbing", waste_status, waste_tooltip);
			p.AddContent("pressurization", atmo_status, atmo_tooltip);
		}


		// store situations and altitude multipliers
		private string[] situations = { "Landed", "Low Orbit", "Orbit", "High Orbit" };
		private readonly double[] altitude_mults = { 0.0, 0.33, 1.0, 3.0 };

		// styles
		private GUIStyle leftmenu_style;
		private readonly GUIStyle rightmenu_style;
		private GUIStyle quote_style;
		private readonly GUIStyle icon_style;

		// analyzers
		private Resource_simulator sim = new Resource_simulator();
		private Environment_analyzer env = new Environment_analyzer();
		private Vessel_analyzer va = new Vessel_analyzer();

		// panel arrays
		private List<string> panel_resource;
		private List<string> panel_special;
		private List<string> panel_environment;

		// body/situation/sunlight indexes
		private int body_index;
		private int situation_index;
		private bool sunlight;

		// panel indexes
		private int resource_index;
		private int special_index;
		private int environment_index;

		// panel ui
		private Panel panel;
	}


	// analyze the environment
	public sealed class Environment_analyzer
	{
		public void Analyze(CelestialBody body, double altitude_mult, bool sunlight)
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
	public sealed class Vessel_analyzer
	{
		public void Analyze(List<Part> parts, Resource_simulator sim, Environment_analyzer env)
		{
			// note: vessel analysis require resource analysis, but at the same time resource analysis
			// require vessel analysis, so we are using resource analysis from previous frame (that's okay)
			// in the past, it was the other way around - however that triggered a corner case when va.comforts
			// was null (because the vessel analysis was still never done) and some specific rule/process
			// in resource analysis triggered an exception, leading to the vessel analysis never happening
			// inverting their order avoided this corner-case

			Analyze_crew(parts);
			Analyze_habitat(sim, env);
			Analyze_radiation(parts, sim);
			Analyze_reliability(parts);
			Analyze_qol(parts, sim, env);
			Analyze_comms(parts);
		}


		void Analyze_crew(List<Part> parts)
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


		void Analyze_habitat(Resource_simulator sim, Environment_analyzer env)
		{
			// calculate total volume
			volume = sim.Resource("Atmosphere").capacity;

			// calculate total surface
			surface = sim.Resource("Shielding").capacity;

			// determine if the vessel has pressure control capabilities
			pressurized = sim.Resource("Atmosphere").produced > 0.0 || env.breathable;

			// determine if the vessel has scrubbing capabilities
			scrubbed = sim.Resource("WasteAtmosphere").consumed > 0.0 || env.breathable;
		}

		void Analyze_comms(List<Part> parts)
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

		void Analyze_radiation(List<Part> parts, Resource_simulator sim)
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
			double amount = sim.Resource("Shielding").amount;
			double capacity = sim.Resource("Shielding").capacity;
			shielding = (capacity > double.Epsilon ? amount / capacity : 1.0) * Settings.ShieldingEfficiency;
		}


		void Analyze_reliability(List<Part> parts)
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


		void Analyze_qol(List<Part> parts, Resource_simulator sim, Environment_analyzer env)
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
	public class Resource_simulator
	{
		public void Analyze(List<Part> parts, Environment_analyzer env, Vessel_analyzer va)
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
				if ((r.input.Length > 0 || (r.output_only && r.output.Length > 0)) && r.rate > 0.0)
				{
					Process_rule(r, env, va);
				}
			}

			// process all processes
			foreach (Process p in Profile.processes)
			{
				Process_process(p, env, va);
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
						case "Greenhouse": Process_greenhouse(m as Greenhouse, env, va); break;
						case "GravityRing": Process_ring(m as GravityRing); break;
						case "Emitter": Process_emitter(m as Emitter); break;
						case "Laboratory": Process_laboratory(m as Laboratory); break;
						case "Experiment": Process_experiment(m as Experiment); break;
						case "ModuleCommand": Process_command(m as ModuleCommand); break;
						case "ModuleDeployableSolarPanel": Process_panel(m as ModuleDeployableSolarPanel, env); break;
						case "ModuleGenerator": Process_generator(m as ModuleGenerator, p); break;
						case "ModuleResourceConverter": Process_converter(m as ModuleResourceConverter, va); break;
						case "ModuleKPBSConverter": Process_converter(m as ModuleResourceConverter, va); break;
						case "ModuleResourceHarvester": Process_harvester(m as ModuleResourceHarvester, va); break;
						case "ModuleScienceConverter": Process_stocklab(m as ModuleScienceConverter); break;
						case "ModuleActiveRadiator": Process_radiator(m as ModuleActiveRadiator); break;
						case "ModuleWheelMotor": Process_wheel_motor(m as ModuleWheelMotor); break;
						case "ModuleWheelMotorSteering": Process_wheel_steering(m as ModuleWheelMotorSteering); break;
						case "ModuleLight": Process_light(m as ModuleLight); break;
						case "ModuleColoredLensLight": Process_light(m as ModuleLight); break;
						case "ModuleMultiPointSurfaceLight": Process_light(m as ModuleLight); break;
						case "SCANsat": Process_scanner(m); break;
						case "ModuleSCANresourceScanner": Process_scanner(m); break;
						case "ModuleCurvedSolarPanel": Process_curved_panel(p, m, env); break;
						case "FissionGenerator": Process_fission_generator(p, m); break;
						case "ModuleRadioisotopeGenerator": Process_radioisotope_generator(p, m); break;
						case "ModuleCryoTank": Process_cryotank(p, m); break;
						case "ModuleRTAntenna": Process_rtantenna(p, m); break;
						case "ModuleDataTransmitter": Process_datatransmitter(m as ModuleDataTransmitter); break;
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
					Simulated_recipe recipe = recipes[i];
					if (recipe.left > double.Epsilon)
					{
						executing |= recipe.Execute(this);
					}
				}
			}
			recipes.Clear();

			// clamp all resources
			foreach (var pair in resources) pair.Value.Clamp();
		}


		public Simulated_resource Resource(string name)
		{
			Simulated_resource res;
			if (!resources.TryGetValue(name, out res))
			{
				res = new Simulated_resource();
				resources.Add(name, res);
			}
			return res;
		}


		void Process_part(Part p, string res_name)
		{
			Simulated_resource res = Resource(res_name);
			res.storage += Lib.Amount(p, res_name);
			res.amount += Lib.Amount(p, res_name);
			res.capacity += Lib.Capacity(p, res_name);
		}


		void Process_rule(Rule r, Environment_analyzer env, Vessel_analyzer va)
		{
			// deduce rate per-second
			double rate = (double)va.crew_count * (r.interval > 0.0 ? r.rate / r.interval : r.rate);

			// evaluate modifiers
			double k = Modifiers.Evaluate(env, va, this, r.modifiers);

			// prepare recipe
			if (r.output.Length == 0)
			{
				Resource(r.input).Consume(rate * k, r.name);
			}
			else if (rate > double.Epsilon)
			{
				// simulate recipe if output_only is false
				if (!r.output_only)
				{
					// - rules always dump excess overboard (because it is waste)
					Simulated_recipe recipe = new Simulated_recipe(r.name);
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


		void Process_process(Process p, Environment_analyzer env, Vessel_analyzer va)
		{
			// evaluate modifiers
			double k = Modifiers.Evaluate(env, va, this, p.modifiers);

			// prepare recipe
			Simulated_recipe recipe = new Simulated_recipe(p.name);
			foreach (var input in p.inputs)
			{
				recipe.Input(input.Key, input.Value * k);
			}
			foreach (var output in p.outputs)
			{
				recipe.Output(output.Key, output.Value * k, p.dump.Check(output.Key));
			}
			recipes.Add(recipe);
		}


		void Process_greenhouse(Greenhouse g, Environment_analyzer env, Vessel_analyzer va)
		{
			// skip disabled greenhouses
			if (!g.active) return;

			// shortcut to resources
			Simulated_resource ec = Resource("ElectricCharge");
			Simulated_resource res = Resource(g.crop_resource);

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
			Simulated_recipe recipe = new Simulated_recipe("greenhouse");
			foreach (ModuleResource input in g.resHandler.inputResources) recipe.Input(input.name, input.rate);
			foreach (ModuleResource output in g.resHandler.outputResources) recipe.Output(output.name, output.rate, true);
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
			if (ring.deployed) Resource("ElectricCharge").Consume(ring.ec_rate, "gravity ring");
		}


		void Process_emitter(Emitter emitter)
		{
			if (emitter.running) Resource("ElectricCharge").Consume(emitter.ec_rate, "emitter");
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
				Resource("ElectricCharge").Consume(exp.ec_rate, exp.transmissible ? "sensor" : "experiment");
			}
		}


		void Process_command(ModuleCommand command)
		{
			foreach (ModuleResource res in command.resHandler.inputResources)
			{
				Resource(res.name).Consume(res.rate, "command");
			}
		}


		void Process_panel(ModuleDeployableSolarPanel panel, Environment_analyzer env)
		{
			double generated = panel.resHandler.outputResources[0].rate * env.solar_flux / Sim.SolarFluxAtHome();
			Resource("ElectricCharge").Produce(generated, "solar panel");
		}


		void Process_generator(ModuleGenerator generator, Part p)
		{
			// skip launch clamps, that include a generator
			if (Lib.PartName(p) == "launchClamp1") return;

			Simulated_recipe recipe = new Simulated_recipe("generator");
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


		void Process_converter(ModuleResourceConverter converter, Vessel_analyzer va)
		{
			// calculate experience bonus
			float exp_bonus = converter.UseSpecialistBonus
			  ? converter.EfficiencyBonus * (converter.SpecialistBonusBase + (converter.SpecialistEfficiencyFactor * (va.crew_engineer_maxlevel + 1)))
			  : 1.0f;

			// use part name as recipe name
			// - include crew bonus in the recipe name
			string recipe_name = Lib.BuildString(converter.part.partInfo.title, " (efficiency: ", Lib.HumanReadablePerc(exp_bonus), ")");

			// generate recipe
			Simulated_recipe recipe = new Simulated_recipe(recipe_name);
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


		void Process_harvester(ModuleResourceHarvester harvester, Vessel_analyzer va)
		{
			// calculate experience bonus
			float exp_bonus = harvester.UseSpecialistBonus
			  ? harvester.EfficiencyBonus * (harvester.SpecialistBonusBase + (harvester.SpecialistEfficiencyFactor * (va.crew_engineer_maxlevel + 1)))
			  : 1.0f;

			// use part name as recipe name
			// - include crew bonus in the recipe name
			string recipe_name = Lib.BuildString(harvester.part.partInfo.title, " (efficiency: ", Lib.HumanReadablePerc(exp_bonus), ")");

			// generate recipe
			Simulated_recipe recipe = new Simulated_recipe(recipe_name);
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
			foreach (var res in radiator.resHandler.inputResources)
			{
				Resource(res.name).Consume(res.rate, "radiator");
			}
		}


		void Process_wheel_motor(ModuleWheelMotor motor)
		{
			foreach (var res in motor.resHandler.inputResources)
			{
				Resource(res.name).Consume(res.rate, "wheel");
			}
		}


		void Process_wheel_steering(ModuleWheelMotorSteering steering)
		{
			foreach (var res in steering.resHandler.inputResources)
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


		void Process_curved_panel(Part p, PartModule m, Environment_analyzer env)
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
			var reactor = p.FindModuleImplementing<ModuleResourceConverter>();
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
			// note: assume cooling is active
			double cooling_cost = Lib.ReflectionValue<float>(m, "CoolingCost");
			string fuel_name = Lib.ReflectionValue<string>(m, "FuelName");

			Resource("ElectricCharge").Consume(cooling_cost * Lib.Capacity(p, fuel_name) * 0.001, "cryotank");
		}

		void Process_rtantenna(Part p, PartModule m)
		{
			float ec_cost = Lib.ReflectionValue<float>(m, "EnergyCost");
			Resource("ElectricCharge").Consume(ec_cost, "communications");
		}

		void Process_datatransmitter(ModuleDataTransmitter mdt)
		{
			switch (mdt.antennaType)
			{
				case AntennaType.INTERNAL:
					Resource("ElectricCharge").Consume((mdt.packetResourceCost * mdt.DataRate) / mdt.packetSize, "communications (control)");
					break;
				default:
					Resource("ElectricCharge").Consume((mdt.packetResourceCost * mdt.DataRate) / mdt.packetSize, "communications (transmitting)");
					break;
			}
		}

		Dictionary<string, Simulated_resource> resources = new Dictionary<string, Simulated_resource>();
		List<Simulated_recipe> recipes = new List<Simulated_recipe>();
	}


	public sealed class Simulated_resource
	{
		public Simulated_resource()
		{
			consumers = new Dictionary<string, Wrapper>();
			producers = new Dictionary<string, Wrapper>();
			harvests = new List<string>();
		}

		public void Consume(double quantity, string name)
		{
			if (quantity >= double.Epsilon)
			{
				amount -= quantity;
				consumed += quantity;

				if (!consumers.ContainsKey(name)) consumers.Add(name, new Wrapper());
				consumers[name].value += quantity;
			}
		}

		public void Produce(double quantity, string name)
		{
			if (quantity >= double.Epsilon)
			{
				amount += quantity;
				produced += quantity;

				if (!producers.ContainsKey(name)) producers.Add(name, new Wrapper());
				producers[name].value += quantity;
			}
		}

		public void Clamp()
		{
			amount = Lib.Clamp(amount, 0.0, capacity);
		}

		public double Lifetime()
		{
			double rate = produced - consumed;
			return amount <= double.Epsilon ? 0.0 : rate > -1e-10 ? double.NaN : amount / -rate;
		}

		public string Tooltip(bool invert = false)
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

		public class Wrapper { public double value; }
		public Dictionary<string, Wrapper> consumers; // consumers metadata
		public Dictionary<string, Wrapper> producers; // producers metadata
	}


	public sealed class Simulated_recipe
	{
		public Simulated_recipe(string name)
		{
			this.name = name;
			this.inputs = new List<Resource_recipe.Entry>();
			this.outputs = new List<Resource_recipe.Entry>();
			this.left = 1.0;
		}

		// add an input to the recipe
		public void Input(string resource_name, double quantity)
		{
			if (quantity > double.Epsilon) //< avoid division by zero
			{
				inputs.Add(new Resource_recipe.Entry(resource_name, quantity));
			}
		}

		// add an output to the recipe
		public void Output(string resource_name, double quantity, bool dump)
		{
			if (quantity > double.Epsilon) //< avoid division by zero
			{
				outputs.Add(new Resource_recipe.Entry(resource_name, quantity, dump));
			}
		}

		// execute the recipe
		public bool Execute(Resource_simulator sim)
		{
			// determine worst input ratio
			double worst_input = left;
			if (outputs.Count > 0)
			{
				for (int i = 0; i < inputs.Count; ++i)
				{
					var e = inputs[i];
					Simulated_resource res = sim.Resource(e.name);
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
						Simulated_resource res = sim.Resource(e.name);
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
				Simulated_resource res = sim.Resource(e.name);
				res.Consume(e.quantity * worst_io, name);
			}

			// produce outputs
			for (int i = 0; i < outputs.Count; ++i)
			{
				var e = outputs[i];
				Simulated_resource res = sim.Resource(e.name);
				res.Produce(e.quantity * worst_io, name);
			}

			// update amount left to execute
			left -= worst_io;

			// the recipe was executed, at least partially
			return worst_io > double.Epsilon;
		}

		// store inputs and outputs
		public string name;                         // name used for consumer/producer tooltip
		public List<Resource_recipe.Entry> inputs;  // set of input resources
		public List<Resource_recipe.Entry> outputs; // set of output resources
		public double left;                         // what proportion of the recipe is left to execute
	}


} // KERBALISM
