using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;


namespace KERBALISM.Planner
{

	///<summary> Class for the Planner used in the VAB/SPH, it is used to predict resource production/consumption and
	/// provide information on life support, radiation, comfort and other relevant factors. </summary>
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
			sim = new ResourceSimulator();
			env = new EnvironmentAnalyzer();
			va = new VesselAnalyzer();

			// resource panels
			panel_resource = new List<string>();
			Profile.supplies.FindAll(k => k.resource != "ElectricCharge").ForEach(k => panel_resource.Add(k.resource));

			// special panels
			// - stress & radiation panels require that a rule using the living_space/radiation modifier exist (current limitation)
			panel_special = new List<string>();
			if (Features.LivingSpace && Profile.rules.Find(k => k.modifiers.Contains("living_space")) != null)
				panel_special.Add("qol");
			if (Features.Radiation && Profile.rules.Find(k => k.modifiers.Contains("radiation")) != null)
				panel_special.Add("radiation");
			if (Features.Reliability)
				panel_special.Add("reliability");

			// environment panels
			panel_environment = new List<string>();
			if (Features.Pressure || Features.Poisoning || Features.Humidity)
				panel_environment.Add("habitat");
			panel_environment.Add("environment");

			// panel ui
			panel = new Panel();
		}

		/// <summary>Run simulators and visualize results in the planner UI panel</summary>
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
						case "qol":
							Render_stress(panel);
							break;
						case "radiation":
							Render_radiation(panel);
							break;
						case "reliability":
							Render_reliability(panel);
							break;
					}
				}

				// environment panel
				switch (panel_environment[environment_index])
				{
					case "habitat":
						Render_habitat(panel);
						break;
					case "environment":
						Render_environment(panel);
						break;
				}
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
				if (Lib.IsClicked())
				{ body_index = (body_index + 1) % FlightGlobals.Bodies.Count; if (body_index == 0) ++body_index; }
				else if (Lib.IsClicked(1))
				{ body_index = (body_index - 1) % FlightGlobals.Bodies.Count; if (body_index == 0) body_index = FlightGlobals.Bodies.Count - 1; }

				// sunlight selector
				GUILayout.Label(new GUIContent(sunlight ? Icons.sun_white : Icons.sun_black, "In sunlight/shadow"), icon_style);
				if (Lib.IsClicked())
					sunlight = !sunlight;

				// situation selector
				GUILayout.Label(new GUIContent(situation, "Target situation"), rightmenu_style);
				if (Lib.IsClicked())
				{ situation_index = (situation_index + 1) % situations.Length; }
				else if (Lib.IsClicked(1))
				{ situation_index = (situation_index == 0 ? situations.Length : situation_index) - 1; }

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

		/// <summary>render environment sub-panel, including tooltips</summary>
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

		/// <summary>render electric charge sub-panel, including tooltips</summary>
		void Render_ec(Panel p)
		{
			// get simulated resource
			SimulatedResource res = sim.Resource("ElectricCharge");

			// create tooltip
			string tooltip = res.Tooltip();

			// render the panel section
			p.AddSection("ELECTRIC CHARGE");
			p.AddContent("storage", Lib.HumanReadableAmount(res.storage), tooltip);
			p.AddContent("consumed", Lib.HumanReadableRate(res.consumed), tooltip);
			p.AddContent("produced", Lib.HumanReadableRate(res.produced), tooltip);
			p.AddContent("duration", Lib.HumanReadableDuration(res.Lifetime()));
		}

		/// <summary>render supply resource sub-panel, including tooltips</summary>
		/// <remarks>
		/// does not include electric charge
		/// does not special resources like waste atmosphere
		/// restricted to resources that are configured explicitly in the profile as supplies
		/// </remarks>
		void Render_resource(Panel p, string res_name)
		{
			// get simulated resource
			SimulatedResource res = sim.Resource(res_name);

			// create tooltip
			string tooltip = res.Tooltip();

			// render the panel section
			p.AddSection(Lib.SpacesOnCaps(res_name).ToUpper(), string.Empty, () => p.Prev(ref resource_index, panel_resource.Count), () => p.Next(ref resource_index, panel_resource.Count));
			p.AddContent("storage", Lib.HumanReadableAmount(res.storage), tooltip);
			p.AddContent("consumed", Lib.HumanReadableRate(res.consumed), tooltip);
			p.AddContent("produced", Lib.HumanReadableRate(res.produced), tooltip);
			p.AddContent("duration", Lib.HumanReadableDuration(res.Lifetime()));
		}

		/// <summary>render stress sub-panel, including tooltips</summary>
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
				"volume per-capita:<b>\t", Lib.HumanReadableVolume(va.volume / Math.Max(va.crew_count, 1)), "</b>\n",
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

		/// <summary>render radiation sub-panel, including tooltips</summary>
		void Render_radiation(Panel p)
		{
			// get first radiation rule
			// - guaranteed to exist, as this panel is not rendered if it doesn't
			// - even without crew, it is safe to evaluate the modifiers that use it
			Rule rule = Profile.rules.Find(k => k.modifiers.Contains("radiation"));

			// detect if it use shielding
			bool use_shielding = rule.modifiers.Contains("shielding");

			// calculate various radiation levels
			double[] levels = new[]
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
			foreach (string s in rule.modifiers)
			{ if (s != "radiation") modifiers_except_radiation.Add(s); }
			double mod = Modifiers.Evaluate(env, va, sim, modifiers_except_radiation);

			// calculate life expectancy at various radiation levels
			double[] estimates = new double[7];
			for (int i = 0; i < 7; ++i)
			{
				estimates[i] = rule.fatal_threshold / (rule.degeneration * mod * levels[i]);
			}

			// generate tooltip
			RadiationModel mf = Radiation.Info(env.body).model;
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
			if (va.emitted >= 0.0)
				p.AddContent("emission", Lib.HumanReadableRadiation(va.emitted), tooltip);
			else
				p.AddContent("active shielding", Lib.HumanReadableRadiation(-va.emitted), tooltip);
			p.AddContent("shielding", rule.modifiers.Contains("shielding") ? Habitat.Shielding_to_string(va.shielding) : "n/a", tooltip);
		}

		/// <summary>render reliability sub-panel, including tooltips</summary>
		void Render_reliability(Panel p)
		{
			// evaluate redundancy metric
			// - 0: no redundancy
			// - 0.5: all groups have 2 elements
			// - 1.0: all groups have 3 or more elements
			double redundancy_metric = 0.0;
			foreach (KeyValuePair<string, int> pair in va.redundancy)
			{
				switch (pair.Value)
				{
					case 1:
						break;
					case 2:
						redundancy_metric += 0.5 / va.redundancy.Count;
						break;
					default:
						redundancy_metric += 1.0 / va.redundancy.Count;
						break;
				}
			}

			// traduce the redundancy metric to string
			string redundancy_str = string.Empty;
			if (redundancy_metric <= 0.1)
				redundancy_str = "none";
			else if (redundancy_metric <= 0.33)
				redundancy_str = "poor";
			else if (redundancy_metric <= 0.66)
				redundancy_str = "okay";
			else
				redundancy_str = "great";

			// generate redundancy tooltip
			string redundancy_tooltip = string.Empty;
			if (va.redundancy.Count > 0)
			{
				StringBuilder sb = new StringBuilder();
				foreach (KeyValuePair<string, int> pair in va.redundancy)
				{
					if (sb.Length > 0)
						sb.Append("\n");
					sb.Append("<b>");
					switch (pair.Value)
					{
						case 1:
							sb.Append("<color=red>");
							break;
						case 2:
							sb.Append("<color=yellow>");
							break;
						default:
							sb.Append("<color=green>");
							break;
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

		/// <summary>render habitat sub-panel, including tooltips</summary>
		void Render_habitat(Panel p)
		{
			SimulatedResource atmo_res = sim.Resource("Atmosphere");
			SimulatedResource waste_res = sim.Resource("WasteAtmosphere");
			SimulatedResource moist_res = sim.Resource("MoistAtmosphere");

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
		private ResourceSimulator sim = new ResourceSimulator();
		private EnvironmentAnalyzer env = new EnvironmentAnalyzer();
		private VesselAnalyzer va = new VesselAnalyzer();

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


} // KERBALISM
