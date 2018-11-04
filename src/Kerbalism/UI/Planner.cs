﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ModuleWheels;
using UnityEngine;


namespace KERBALISM
{

	/// <summary>
	/// class that is used to predict resource production/consumption while in the vehicle assumbly building
	/// information on life support, radiation, comfort and other relevant factors is also given
	/// </summary>
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
			if (Features.Pressure || Features.Poisoning || Features.Humidity) panel_environment.Add("habitat");
			panel_environment.Add("environment");

			// panel ui
			panel = new Panel();
		}

		/// <summary>Run simulators and visualize results in planner UI panel</summary>
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


		/// <summary>Render planner UI panel</summary>
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

		/// <summary>planner panel UI width</summary>
		public float Width()
		{
			return Styles.ScaleWidthFloat(280.0f);
		}

		/// <summary>planner panel UI height</summary>
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

		/// <summary>render environment subpanel, including tooltips</summary>
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


		/// <summary>render electric charge subpanel, including tooltips</summary>
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

		/// <summary>render supply resource subpanel, including tooltips</summary>
		/// <remarks>
		/// does not include eletric charge
		/// does not special resources like waste atmosphere
		/// restricted to resources that are configured explicitly in the profile as supplies
		/// </remarks>
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

		/// <summary>render stress subpanel, including tooltips</summary>
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
				"ideal living space:<b>\t", Lib.HumanReadableVolume(PreferencesComfort.Instance.livingSpace), "</b>"
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

		/// <summary>render radiation subpanel, including tooltips</summary>
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

		/// <summary>render reliablity subpanel, including tooltips</summary>
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

		/// <summary>render habitat subpanel, including tooltips</summary>
		void Render_habitat(Panel p)
		{
			Simulated_resource atmo_res = sim.Resource("Atmosphere");
			Simulated_resource waste_res = sim.Resource("WasteAtmosphere");
			Simulated_resource moist_res = sim.Resource("MoistAtmosphere");

			// generate tooltips
			string atmo_tooltip = atmo_res.Tooltip();
			string waste_tooltip = waste_res.Tooltip(true);
			string moist_tooltip = moist_res.Tooltip(true);

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

			// generate status string for humidity
			string moist_status = !Features.Humidity                    //< feature disabled
			  ? "n/a"
			  : moist_res.produced <= double.Epsilon                    //< unnecessary
			  ? "not required"
			  : moist_res.consumed <= double.Epsilon                    //< no humidity control
			  ? "<color=#ffff00>none</color>"
			  : moist_res.produced > moist_res.consumed * 1.001         //< insufficient humidity control
			  ? "<color=#ffff00>inadequate</color>"
			  : "good";                                                 //< sufficient humidity control

			// generate status string for pressurization
			string atmo_status = !Features.Pressure                     //< feature disabled
			  ? "n/a"
			  : atmo_res.consumed <= double.Epsilon                     //< unnecessary
			  ? "not required"
			  : atmo_res.produced <= double.Epsilon                     //< no pressure control
			  ? "<color=#ffff00>none</color>"
			  : atmo_res.consumed > atmo_res.produced * 1.001           //< insufficient pressure control
			  ? "<color=#ffff00>inadequate</color>"
			  : "good";                                                 //< sufficient pressure control

			p.AddSection("HABITAT", string.Empty, () => p.Prev(ref environment_index, panel_environment.Count), () => p.Next(ref environment_index, panel_environment.Count));
			p.AddContent("volume", Lib.HumanReadableVolume(va.volume), "volume of enabled habitats");
			p.AddContent("surface", Lib.HumanReadableSurface(va.surface), "surface of enabled habitats");
			p.AddContent("scrubbing", waste_status, waste_tooltip);
			p.AddContent("humidity", moist_status, moist_tooltip);
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


	/// <summary>simulator for the environment the vessel is present in according to planner settings</summary>
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
			zerog = !landed && (!body.atmosphere || body.atmosphereDepth < altitude);

			var rb = Radiation.Info(body);
			var sun_rb = Radiation.Info(sun);
			gamma_transparency = Sim.GammaTransparency(body, 0.0);
			extern_rad = PreferencesStorm.Instance.ExternRadiation;
			heliopause_rad = extern_rad + sun_rb.radiation_pause;
			magnetopause_rad = heliopause_rad + rb.radiation_pause;
			inner_rad = magnetopause_rad + rb.radiation_inner;
			outer_rad = magnetopause_rad + rb.radiation_outer;
			surface_rad = magnetopause_rad * gamma_transparency;
			storm_rad = heliopause_rad + PreferencesStorm.Instance.StormRadiation * (solar_flux > double.Epsilon ? 1.0 : 0.0);
		}


		public CelestialBody body;                            // target body
		public double altitude;                               // target altitude
		public bool landed;                                   // true if landed
		public bool breathable;                               // true if inside breathable atmosphere
		public bool zerog;									  // true if the vessel is experiencing zero g
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

	/// <summary>simulator for all vessel aspects, but excluding resource simulation</summary>
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
			volume = sim.Resource("Atmosphere").capacity / 1e3;

			// calculate total surface
			surface = sim.Resource("Shielding").capacity;

			// determine if the vessel has pressure control capabilities
			pressurized = sim.Resource("Atmosphere").produced > 0.0 || env.breathable;

			// determine if the vessel has scrubbing capabilities
			scrubbed = sim.Resource("WasteAtmosphere").consumed > 0.0 || env.breathable;

			// determine if the vessel has humidity control capabilities
			humid = sim.Resource("MoistAtmosphere").consumed > 0.0 || env.breathable;
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

					// RemoteTech enabled, passive's don't count
					if (m.moduleName == "ModuleRTAntenna") has_comms = true;
					else if (m.moduleName == "ModuleDataTransmitter")
					{
						// CommNet enabled and external transmitter
						if (HighLogic.fetch.currentGame.Parameters.Difficulty.EnableCommNet)
						{
							if (Lib.ReflectionValue<AntennaType>(m, "antennaType") != AntennaType.INTERNAL) has_comms = true;
						}
						// the simple stupid always connected signal system
						else has_comms = true;
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
			shielding = (capacity > double.Epsilon ? amount / capacity : 1.0) * PreferencesStorm.Instance.shieldingEfficiency;
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
			living_space = Lib.Clamp((volume / (double)Math.Max(crew_count, 1u)) / PreferencesComfort.Instance.livingSpace, 0.1, 1.0);

			// calculate comfort factor
			comforts = new Comforts(parts, env.landed, crew_count > 1, has_comms);
		}


		// general
		public uint crew_count;                             // crew member on board
		public uint crew_capacity;                          // crew member capacity
		public bool crew_engineer;                          // true if an engineer is among the crew
		public bool crew_scientist;                         // true if a scientist is among the crew
		public bool crew_pilot;                             // true if a pilot is among the crew
		public uint crew_engineer_maxlevel;                 // experience level of top engineer on board
		public uint crew_scientist_maxlevel;                // experience level of top scientist on board
		public uint crew_pilot_maxlevel;                    // experience level of top pilot on board

		// habitat
		public double volume;                               // total volume in m^3
		public double surface;                              // total surface in m^2
		public bool pressurized;                            // true if the vessel has pressure control capabilities
		public bool scrubbed;                               // true if the vessel has co2 scrubbing capabilities
		public bool humid;                                  // true if the vessel has co2 scrubbing capabilities

		// radiation related
		public double emitted;                              // amount of radiation emitted by components
		public double shielding;                            // shielding factor

		// quality-of-life related
		public double living_space;                         // living space factor
		public Comforts comforts;                           // comfort info

		// reliability-related
		public uint components;                             // number of components that can fail
		public double high_quality;                         // percentage of high quality components
		public double failure_year;                         // estimated failures per-year, averaged per-component
		public Dictionary<string, int> redundancy;          // number of components per redundancy group

		public bool has_comms;
	}

	/// <summary>simulator for resources contained, produced and consumed within the vessel</summary>
	public class Resource_simulator
	{
		/// <summary>
		/// run simulator to get statistics a fraction of a second after the vessel would spawn
		/// in the configured environment (celestial body, orbit height and presence of sunlight)
		/// </summary>
		public void Analyze(List<Part> parts, Environment_analyzer env, Vessel_analyzer va)
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
			foreach (Simulated_resource r in resources.Values)
			{
				r.ResetSimulatorDisplayValues();
			}
			RunSimulator(parts, env, va);
		}

		/// <summary>run a single timestemp of the simulator</simulator>
		private void RunSimulator(List<Part> parts, Environment_analyzer env, Vessel_analyzer va)
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
						case "ModuleRTAntennaPassive":
						case "ModuleRTAntenna": Process_rtantenna(m); break;
						case "ModuleDataTransmitter": Process_datatransmitter(m as ModuleDataTransmitter); break;
						case "ModuleEngines": Process_engines(m as ModuleEngines); break;
						case "ModuleEnginesFX": Process_enginesfx(m as ModuleEnginesFX); break;
						case "ModuleRCS": Process_rcs(m as ModuleRCS); break;
						case "ModuleRCSFX": Process_rcsfx(m as ModuleRCSFX); break;
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

		/// <summary>obtain information on resource metrics for any resource contained within simulated vessel</simulator>
		public Simulated_resource Resource(string name)
		{
			Simulated_resource res;
			if (!resources.TryGetValue(name, out res))
			{
				res = new Simulated_resource(name);
				resources.Add(name, res);
			}
			return res;
		}

		/// <summary>transfer per-part resources to the simulator</simulator>
		void Process_part(Part p, string res_name)
		{
			Simulated_resource_view res = Resource(res_name).GetSimulatedResourceView(p);
			res.AddPartResources(p);
		}

		/// <summary>process a rule and add/remove the resources from the simulator</simulator>
		private void Process_rule_inner_body(double k, Part p, Rule r, Environment_analyzer env, Vessel_analyzer va)
		{
			// deduce rate per-second
			double rate = (double)va.crew_count * (r.interval > 0.0 ? r.rate / r.interval : r.rate);

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
					Simulated_recipe recipe = new Simulated_recipe(p, r.name);
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

		/// <summary>process a rule for resources that can flow through the entire vessel</simulator>
		private void Process_rule_vessel_wide(Rule r, Environment_analyzer env, Vessel_analyzer va)
		{
			// evaluate modifiers
			double k = Modifiers.Evaluate(env, va, this, r.modifiers);
			Process_rule_inner_body(k, null, r, env, va);
		}

		/// <summary>process a rule for a case where at least one resource cannot flow through the entire vessel</summary>
		private void Process_rule_per_part(List<Part> parts, Rule r, Environment_analyzer env, Vessel_analyzer va)
		{
			foreach (Part p in parts)
			{
				// evaluate modifiers
				double k = Modifiers.Evaluate(env, va, this, r.modifiers, p);
				Process_rule_inner_body(k, p, r, env, va);
			}
		}

		/// <summary>determine if the resources involved are restricted to a part, and then process a rule</summary>
		public void Process_rule(List<Part> parts, Rule r, Environment_analyzer env, Vessel_analyzer va)
		{
			bool restricted = false;
			// input/output of a rule may be empty string
			if (!r.monitor && Lib.IsResourceImpossibleToFlow(r.input, true)) restricted = true;
			if (Lib.IsResourceImpossibleToFlow(r.output, true)) restricted = true;

			if (restricted) Process_rule_per_part(parts, r, env, va);
			else Process_rule_vessel_wide(r, env, va);
		}

		/// <summary>process the process and add/remove the resources from the simulator</summary>
		private void Process_process_inner_body(double k, Part p, Process pr, Environment_analyzer env, Vessel_analyzer va)
		{
			// prepare recipe
			Simulated_recipe recipe = new Simulated_recipe(p, pr.name);
			foreach (var input in pr.inputs)
			{
				recipe.Input(input.Key, input.Value * k);
			}
			foreach (var output in pr.outputs)
			{
				recipe.Output(output.Key, output.Value * k, pr.dump.Check(output.Key));
			}
			recipes.Add(recipe);
		}

		/// <summary>process the process and add/remove the resources from the simulator for the entire vessel at once</summary>
		private void Process_process_vessel_wide(Process pr, Environment_analyzer env, Vessel_analyzer va)
		{
			// evaluate modifiers
			double k = Modifiers.Evaluate(env, va, this, pr.modifiers);
			Process_process_inner_body(k, null, pr, env, va);
		}

		/// <summary>process the process and add/remove the resources from the simulator on a per part basis</summary>
		private void Process_process_per_part(List<Part> parts, Process pr, Environment_analyzer env, Vessel_analyzer va)
		{
			foreach (Part p in parts)
			{
				// evaluate modifiers
				double k = Modifiers.Evaluate(env, va, this, pr.modifiers, p);
				Process_process_inner_body(k, p, pr, env, va);
			}
		}

		/// <summary>
		/// determine if the resources involved are restricted to a part, and then process
		/// the process and add/remove the resources from the simulator
		/// </summary>
		/// <remarks>while rules are usually input or output only, processes transform input to output</remarks>
		public void Process_process(List<Part> parts, Process pr, Environment_analyzer env, Vessel_analyzer va)
		{
			bool restricted = false;
			foreach (var input in pr.inputs)
			{
				if (Lib.IsResourceImpossibleToFlow(input.Key)) restricted = true;
			}
			foreach (var output in pr.outputs)
			{
				if (Lib.IsResourceImpossibleToFlow(output.Key)) restricted = true;
			}
			if (restricted) Process_process_per_part(parts, pr, env, va);
			else Process_process_vessel_wide(pr, env, va);
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
			Simulated_recipe recipe = new Simulated_recipe(g.part, "greenhouse");
			foreach (ModuleResource input in g.resHandler.inputResources)
			{
				// WasteAtmosphere is primary combined input
				if (g.WACO2 && input.name == "WasteAtmosphere") recipe.Input(input.name, env.breathable ? 0.0 : input.rate, "CarbonDioxide");
				// CarbonDioxide is secondary combined input
				else if (g.WACO2 && input.name == "CarbonDioxide") recipe.Input(input.name, env.breathable ? 0.0 : input.rate, "");
				// if atmosphere is breathable disable WasteAtmosphere / CO2
				else if (!g.WACO2 && (input.name == "CarbonDioxide" || input.name == "WasteAtmosphere")) recipe.Input(input.name, env.breathable ? 0.0 : input.rate, "");
				else recipe.Input(input.name, input.rate);
			}
			foreach (ModuleResource output in g.resHandler.outputResources)
			{
				// if atmosphere is breathable disable Oxygen
				if (output.name == "Oxygen") recipe.Output(output.name, env.breathable ? 0.0 : output.rate, true);
				else recipe.Output(output.name, output.rate, true);
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

			Simulated_recipe recipe = new Simulated_recipe(p, "generator");
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
			Simulated_recipe recipe = new Simulated_recipe(converter.part, recipe_name);
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
			Simulated_recipe recipe = new Simulated_recipe(harvester.part, recipe_name);
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
			// is cooling available
			bool available = Lib.ReflectionValue<bool>(m, "CoolingEnabled");

			// get list of fuels, do nothing if no fuels
			IList fuels = Lib.ReflectionValue<IList>(m, "fuels");
			if (fuels == null) return;

			// get cooling cost
			double cooling_cost = Lib.ReflectionValue<float>(m, "CoolingCost");

			string fuel_name = "";
			double amount = 0.0;
			double total_cost = 0.0;
			double boiloff_rate = 0.0;

			// calculate EC cost of cooling
			foreach (var fuel in fuels)
			{
				fuel_name = Lib.ReflectionValue<string>(fuel, "fuelName");
				// if fuel_name is null, don't do anything
				if (fuel_name == null) continue;

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

		Dictionary<string, Simulated_resource> resources = new Dictionary<string, Simulated_resource>();
		List<Simulated_recipe> recipes = new List<Simulated_recipe>();
	}

	/// <summary> offers a view on a single resource in the simulator</summary>
	/// <remarks>
	/// hides the difference between vessel wide resources that can flow through the entire vessel
	/// and resources that are restricted to a single part
	/// <remarks>
	public abstract class Simulated_resource_view
	{
		protected Simulated_resource_view() {}

		public abstract double amount { get; }
		public abstract double capacity { get; }
		public abstract double storage { get; }

		public abstract void AddPartResources(Part p);
		public abstract void Produce(double quantity, string name);
		public abstract void Consume(double quantity, string name);
		public abstract void Clamp();
	}

	/// <summary>contains all the data for a single resource within the (vessel) simulator</summary>
	public sealed class Simulated_resource
	{
		public Simulated_resource(string name)
		{
			ResetSimulatorDisplayValues();

			_storage  = new Dictionary<Resource_location, double>();
			_capacity = new Dictionary<Resource_location, double>();
			_amount   = new Dictionary<Resource_location, double>();

			vessel_wide_location = new Resource_location();
			InitDicts(vessel_wide_location);
			_cached_part_views = new Dictionary<Part, Simulated_resource_view>();
			_vessel_wide_view = new Simulated_resource_view_impl(null, resource_name, this);

			resource_name = name;
		}

		/// <summary>reset the values that are displayed to the user in the planner UI</summary>
		/// <remarks>
		/// use this after several simulator steps to do the final calculations under steady state
		/// where resources that are initially empty at vessel start have been created, otherwise
		/// user sees data only relevant for first simulation step (typically 1/50 seconds)
		/// </remarks>
		public void ResetSimulatorDisplayValues()
		{
			consumers = new Dictionary<string, Wrapper>();
			producers = new Dictionary<string, Wrapper>();
			harvests = new List<string>();
			consumed = 0.0;
			produced = 0.0;
		}

		/// <summary>
		/// Identifier to identify the part or vessel where resources are stored
		/// </summary>
		/// <remarks>
		/// KSP 1.3 does not support the neccesary persistent identifier for per part resources
		/// KSP 1.3 always defaults to vessel wide
		/// design is shared with Resource_location in Resource.cs module
		/// </remarks>
		private class Resource_location
		{
			public Resource_location(Part p)
			{
#if !KSP13
				vessel_wide = false;
				persistent_identifier = p.persistentId;
#endif
			}
			public Resource_location() {}

			/// <summary>Equals method in order to ensure object behaves like a value object</summary>
			public override bool Equals(object obj)
			{
				if (obj == null || obj.GetType() != GetType())
				{
					return false;
				}
				return (((Resource_location) obj).persistent_identifier == persistent_identifier) &&
					   (((Resource_location) obj).vessel_wide == vessel_wide);
			}

			/// <summary>GetHashCode method in order to ensure object behaves like a value object</summary>
			public override int GetHashCode()
			{
				return (int) persistent_identifier;
			}

			public bool IsVesselWide() { return vessel_wide; }
			public uint GetPersistentPartId() { return persistent_identifier; }

			private bool vessel_wide = true;
			private uint persistent_identifier = 0;
		}

		/// <summary>implementation of Simulated_resource_view</summary>
		/// <remarks>only construced by Simulated_resource class to hide the dependencies between the two</remarks>
		private class Simulated_resource_view_impl : Simulated_resource_view
		{
			public Simulated_resource_view_impl(Part p, string resource_name, Simulated_resource i)
			{
				info = i;
				if (p != null && Lib.IsResourceImpossibleToFlow(resource_name))
				{
					location = new Resource_location(p);
					if (!info._capacity.ContainsKey(location)) info.InitDicts(location);
				}
				else
				{
					location = info.vessel_wide_location;
				}
			}

			public override void AddPartResources(Part p)
			{
				info.AddPartResources(location, p);
			}
			public override void Produce(double quantity, string name)
			{
				info.Produce(location, quantity, name);
			}
			public override void Consume(double quantity, string name)
			{
				info.Consume(location, quantity, name);
			}
			public override void Clamp()
			{
				info.Clamp(location);
			}

			public override double amount
			{
				get => info._amount[location];
			}
			public override double capacity
			{
				get => info._capacity[location];
			}
			public override double storage
			{
				get => info._storage[location];
			}

			private Simulated_resource info;
			private Resource_location location;
		}

		/// <summary>initialize resource amounts for new resource location</summary>
		/// <remarks>typically for a part that has not yet used this resource</remarks>
		private void InitDicts(Resource_location location)
		{
			_storage[location] = 0.0;
			_amount[location] = 0.0;
			_capacity[location] = 0.0;
		}

		/// <summary>obtain a view on this resource for a given loaded part</summary>
		/// <remarks>passing a null part forces it vessel wide view</remarks>
		public Simulated_resource_view GetSimulatedResourceView(Part p)
		{
			if (p != null && Lib.IsResourceImpossibleToFlow(resource_name))
			{
				if (!_cached_part_views.ContainsKey(p))
				{
					_cached_part_views[p] = new Simulated_resource_view_impl(p, resource_name, this);
				}
				return _cached_part_views[p];
			}
			else
			{
				return _vessel_wide_view;
			}
		}

		/// <summary>add resource information contained within part to vessel wide simulator</summary>
		public void AddPartResources(Part p)
		{
			AddPartResources(vessel_wide_location, p);
		}
		/// <summary>add resource information within part to per-part simulator</summary>
		private void AddPartResources(Resource_location location, Part p)
		{
			_storage[location] += Lib.Amount(p, resource_name);
			_amount[location] += Lib.Amount(p, resource_name);
			_capacity[location] += Lib.Capacity(p, resource_name);
		}

		/// <summary>consume resource from the vessel wide bookkeeping</summary>
		public void Consume(double quantity, string name)
		{
			Consume(vessel_wide_location, quantity, name);
		}
		/// <summary>consume resource from the per-part bookkeeping</summary>
		/// <remarks>also works for vessel wide location</remarks>
		private void Consume(Resource_location location, double quantity, string name)
		{
			if (quantity >= double.Epsilon)
			{
				_amount[location] -= quantity;
				consumed += quantity;

				if (!consumers.ContainsKey(name)) consumers.Add(name, new Wrapper());
				consumers[name].value += quantity;
			}
		}

		/// <summary>produce resource for the vessel wide bookkeeping</summary>
		public void Produce(double quantity, string name)
		{
			Produce(vessel_wide_location, quantity, name);
		}
		/// <summary>produce resource for the per-part bookkeeping</summary>
		/// <remarks>also works for vessel wide location</remarks>
		private void Produce(Resource_location location, double quantity, string name)
		{
			if (quantity >= double.Epsilon)
			{
				_amount[location] += quantity;
				produced += quantity;

				if (!producers.ContainsKey(name)) producers.Add(name, new Wrapper());
				producers[name].value += quantity;
			}
		}

		/// <summary>clamp resource amount to capacity for the vessel wide bookkeeping</summary>
		public void Clamp()
		{
			Clamp(vessel_wide_location);
		}
		/// <summary>clamp resource amount to capacity for the per-part bookkeeping</summary>
		private void Clamp(Resource_location location)
		{
			_amount[location] = Lib.Clamp(_amount[location], 0.0, _capacity[location]);
		}

		/// <summary>determine how long a resource will last at simulated consumption/production levels</summary>
		public double Lifetime()
		{
			double rate = produced - consumed;
			return amount <= double.Epsilon ? 0.0 : rate > -1e-10 ? double.NaN : amount / -rate;
		}

		/// <summary>generate resource tooltip multi-line string</summary>
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
				sb.Append("<b><color=#ffaa00>");
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

		// Enforce that modification happens through official accessor functions
		// Many external classes need to read these values, and they want convenient access
		// However direct modification of these members from outside would make the coupling far too high
		public string resource_name
		{
			get {
				return _resource_name;
			}
			private set {
				_resource_name = value;
			}
		}
		public List<string> harvests
		{
			get {
				return _harvests;
			}
			private set {
				_harvests = value;
			}
		}
		public double consumed
		{
			get {
				return _consumed;
			}
			private set {
				_consumed = value;
			}
		}
		public double produced
		{
			get {
				return _produced;
			}
			private set {
				_produced = value;
			}
		}

		// only getters, use official interface for setting that support resource location
		public double storage
		{
			get {
				return _storage.Values.Sum();
			}
		}
		public double capacity
		{
			get {
				return _capacity.Values.Sum();
			}
		}
		public double amount
		{
			get {
				return _amount.Values.Sum();
			}
		}

		private string _resource_name;  // associated resource name
		private List<string> _harvests; // some extra data about harvests
		private double _consumed;       // total consumption rate
		private double _produced;       // total production rate

		private IDictionary<Resource_location, double> _storage;  // amount stored (at the start of simulation)
		private IDictionary<Resource_location, double> _capacity; // storage capacity
		private IDictionary<Resource_location, double> _amount;   // amount stored (during simulation)

		private class Wrapper { public double value; }
		private IDictionary<string, Wrapper> consumers; // consumers metadata
		private IDictionary<string, Wrapper> producers; // producers metadata
		private Resource_location vessel_wide_location;
		private IDictionary<Part, Simulated_resource_view> _cached_part_views;
		private Simulated_resource_view _vessel_wide_view;
	}


	/// <summary>destription of how to convert inputs to outputs</summary>
	/// <remarks>
	/// this class is also responsible for executing the recipe, such that it is actualized in the Simulated_resource
	/// </remarks>
	public sealed class Simulated_recipe
	{
		public Simulated_recipe(Part p, string name)
		{
			this.name = name;
			this.inputs = new List<Resource_recipe.Entry>();
			this.outputs = new List<Resource_recipe.Entry>();
			this.left = 1.0;
			this.loaded_part = p;
		}

		/// <summary>
		/// add an input to the recipe
		/// </summary>
		public void Input(string resource_name, double quantity)
		{
			if (quantity > double.Epsilon) //< avoid division by zero
			{
				inputs.Add(new Resource_recipe.Entry(resource_name, quantity));
			}
		}

		/// <summary>
		/// add a combined input to the recipe
		/// </summary>
		public void Input(string resource_name, double quantity, string combined)
		{
			if (quantity > double.Epsilon) //< avoid division by zero
			{
				inputs.Add(new Resource_recipe.Entry(resource_name, quantity, true, combined));
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
					Resource_recipe.Entry e = inputs[i];
					Simulated_resource_view res = sim.Resource(e.name).GetSimulatedResourceView(loaded_part);
					// handle combined inputs
					if (e.combined != null)
					{
						// is combined resource the primary
						if (e.combined != "")
						{
							Resource_recipe.Entry sec_e = inputs.Find(x => x.name.Contains(e.combined));
							Simulated_resource_view sec = sim.Resource(sec_e.name).GetSimulatedResourceView(loaded_part);
							double pri_worst = Lib.Clamp(res.amount * e.inv_quantity, 0.0, worst_input);
							if (pri_worst > 0.0) worst_input = pri_worst;
							else worst_input = Lib.Clamp(sec.amount * sec_e.inv_quantity, 0.0, worst_input);
						}
					}
					else worst_input = Lib.Clamp(res.amount * e.inv_quantity, 0.0, worst_input);
				}
			}

			// determine worst output ratio
			double worst_output = left;
			if (inputs.Count > 0)
			{
				for (int i = 0; i < outputs.Count; ++i)
				{
					Resource_recipe.Entry e = outputs[i];
					if (!e.dump) // ignore outputs that can dump overboard
					{
						Simulated_resource_view res = sim.Resource(e.name).GetSimulatedResourceView(loaded_part);
						worst_output = Lib.Clamp((res.capacity - res.amount) * e.inv_quantity, 0.0, worst_output);
					}
				}
			}

			// determine worst-io
			double worst_io = Math.Min(worst_input, worst_output);

			// consume inputs
			for (int i = 0; i < inputs.Count; ++i)
			{
				Resource_recipe.Entry e = inputs[i];
				Simulated_resource res = sim.Resource(e.name);
				// handle combined inputs
				if (e.combined != null)
				{
					// is combined resource the primary
					if (e.combined != "")
					{
						Resource_recipe.Entry sec_e = inputs.Find(x => x.name.Contains(e.combined));
						Simulated_resource_view sec = sim.Resource(sec_e.name).GetSimulatedResourceView(loaded_part);
						double need = (e.quantity * worst_io) + (sec_e.quantity * worst_io);
						// do we have enough primary to satisfy needs, if so don't consume secondary
						if (res.amount >= need) res.Consume(need, name);
						// consume primary if any available and secondary
						else
						{
							need -= res.amount;
							res.Consume(res.amount, name);
							sec.Consume(need, name);
						}
					}
				}
				else res.Consume(e.quantity * worst_io, name);
			}

			// produce outputs
			for (int i = 0; i < outputs.Count; ++i)
			{
				Resource_recipe.Entry e = outputs[i];
				Simulated_resource_view res = sim.Resource(e.name).GetSimulatedResourceView(loaded_part);
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
		private Part loaded_part = null;            // part this recipe runs on, may be null for vessel wide recipe
	}


} // KERBALISM
