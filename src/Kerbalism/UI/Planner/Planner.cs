using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;


namespace KERBALISM.Planner
{

	///<summary> Class for the Planner used in the VAB/SPH, it is used to predict resource production/consumption and
	/// provide information on life support, radiation, comfort and other relevant factors. </summary>
	public static class Planner
	{
		#region CONSTRUCTORS_DESTRUCTORS
		///<summary> Initializes the Planner for use </summary>
		internal static void Initialize()
		{
			// set the ui styles
			SetStyles();

			// set default body index to home
			body_index = FlightGlobals.GetHomeBodyIndex();

			// resource panels
			// - add all resources defined in the Profiles Supply configs except EC
			Profile.supplies.FindAll(k => k.resource != "ElectricCharge").ForEach(k => supplies.Add(k.resource));

			// special panels
			// - stress & radiation panels require that a rule using the living_space/radiation modifier exist (current limitation)
			if (Features.LivingSpace && Profile.rules.Find(k => k.modifiers.Contains("living_space")) != null)
				panel_special.Add("qol");
			if (Features.Radiation && Profile.rules.Find(k => k.modifiers.Contains("radiation")) != null)
				panel_special.Add("radiation");
			if (Features.Reliability)
				panel_special.Add("reliability");

			// environment panels
			if (Features.Pressure || Features.Poisoning)
				panel_environment.Add("habitat");
			panel_environment.Add("environment");
		}

		///<summary> Sets the styles for the panels UI </summary>
		private static void SetStyles()
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

			// debug header style
			devbuild_style = new GUIStyle();
			devbuild_style.normal.textColor = Color.white;
			devbuild_style.stretchHeight = true;
			devbuild_style.fontSize = Styles.ScaleInteger(12);
			devbuild_style.alignment = TextAnchor.MiddleCenter;
		}
		#endregion

		#region EVENTS
		///<summary> Method called when the vessel in the editor has been modified </summary>
		internal static void EditorShipModifiedEvent(ShipConstruct sc) => RefreshPlanner();
		#endregion

		#region METHODS
		///<summary> Call this to trigger a planner update</summary>
		internal static void RefreshPlanner() => update_counter = 0;

		///<summary> Run simulators and update the planner UI sub-panels </summary>
		internal static void Update()
		{
			// get vessel crew manifest
			VesselCrewManifest manifest = KSP.UI.CrewAssignmentDialog.Instance.GetManifest();
			if (manifest == null)
				return;

			// check for number of crew change
			if (vessel_analyzer.crew_count != manifest.CrewCount)
				enforceUpdate = true;

			// only update when we need to, repeat update a number of times to allow the simulators to catch up
			if (!enforceUpdate && update_counter++ > 3)
				return;

			// clear the panel
			panel.Clear();

			// if there is something in the editor
			if (EditorLogic.RootPart != null)
			{
				// get parts recursively
				List<Part> parts = Lib.GetPartsRecursively(EditorLogic.RootPart);

				// analyze using the settings from the panels user input
				env_analyzer.Analyze(FlightGlobals.Bodies[body_index], altitude_mults[situation_index], sunlight);
				vessel_analyzer.Analyze(parts, resource_sim, env_analyzer);
				resource_sim.Analyze(parts, env_analyzer, vessel_analyzer);

				// add ec panel
				AddSubPanelEC(panel);

				// get vessel resources
				panel_resource.Clear();
				foreach (string res in supplies)
					if (resource_sim.Resource(res).capacity > 0.0)
						panel_resource.Add(res);

				// reset current panel if necessary
				if (resource_index >= panel_resource.Count) resource_index = 0;

				// add resource panel
				if (panel_resource.Count > 0)
					AddSubPanelResource(panel, panel_resource[resource_index]);

				// add special panel
				if (panel_special.Count > 0)
				{
					switch (panel_special[special_index])
					{
						case "qol":
							AddSubPanelStress(panel);
							break;
						case "radiation":
							AddSubPanelRadiation(panel);
							break;
						case "reliability":
							AddSubPanelReliability(panel);
							break;
					}
				}

				// add environment panel
				switch (panel_environment[environment_index])
				{
					case "habitat":
						AddSubPanelHabitat(panel);
						break;
					case "environment":
						AddSubPanelEnvironment(panel);
						break;
				}
			}
			enforceUpdate = false;
		}

		///<summary> Planner panel UI width </summary>
		internal static float Width()
		{
			return Styles.ScaleWidthFloat(280.0f);
		}

		///<summary> Planner panel UI height </summary>
		internal static float Height()
		{
			if (EditorLogic.RootPart != null)
				return Styles.ScaleFloat(Lib.IsDevBuild ? 45.0f : 30.0f) + panel.Height(); // header + ui content + dev build header if present
			else
				return Styles.ScaleFloat(66.0f); // quote-only
		}

		///<summary> Render planner UI panel </summary>
		internal static void Render()
		{
			// if there is something in the editor
			if (EditorLogic.RootPart != null)
			{
				if (Lib.IsDevBuild)
				{
					GUILayout.BeginHorizontal(Styles.title_container);
					GUILayout.Label(new GUIContent("KERBALISM DEV BUILD " + Lib.KerbalismDevBuild), devbuild_style);
					GUILayout.EndHorizontal();
				}

				// start header
				GUILayout.BeginHorizontal(Styles.title_container);

				// body selector
				GUILayout.Label(new GUIContent(FlightGlobals.Bodies[body_index].name, "Target body"), leftmenu_style);
				if (Lib.IsClicked())
				{ body_index = (body_index + 1) % FlightGlobals.Bodies.Count; if (body_index == 0) ++body_index; enforceUpdate = true; }
				else if (Lib.IsClicked(1))
				{ body_index = (body_index - 1) % FlightGlobals.Bodies.Count; if (body_index == 0) body_index = FlightGlobals.Bodies.Count - 1; enforceUpdate = true; }

				// sunlight selector
				switch (sunlight)
				{
					case SunlightState.SunlightNominal: GUILayout.Label(new GUIContent(Textures.sun_white, "In sunlight\n<b>Nominal</b> solar panel output"), icon_style); break;
					case SunlightState.SunlightSimulated: GUILayout.Label(new GUIContent(Textures.solar_panel, "In sunlight\n<b>Estimated</b> solar panel output\n<i>Sunlight direction : look at the shadows !</i>"), icon_style); break;
					case SunlightState.Shadow: GUILayout.Label(new GUIContent(Textures.sun_black, "In shadow"), icon_style); break;
				}
				if (Lib.IsClicked())
				{ sunlight = (SunlightState)(((int)sunlight + 1) % Enum.GetValues(typeof(SunlightState)).Length); enforceUpdate = true; }

				// situation selector
				GUILayout.Label(new GUIContent(situations[situation_index], "Target situation"), rightmenu_style);
				if (Lib.IsClicked())
				{ situation_index = (situation_index + 1) % situations.Length; enforceUpdate = true; }
				else if (Lib.IsClicked(1))
				{ situation_index = (situation_index == 0 ? situations.Length : situation_index) - 1; enforceUpdate = true; }

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

		///<summary> Add environment sub-panel, including tooltips </summary>
		private static void AddSubPanelEnvironment(Panel p)
		{
			string flux_tooltip = Lib.BuildString
			(
				"<align=left />" +
				String.Format("<b>{0,-14}\t{1,-15}\t{2}</b>\n", "Source", "Flux", "Temp"),
				String.Format("{0,-14}\t{1,-15}\t{2}\n", "solar", env_analyzer.solar_flux > 0.0 ? Lib.HumanReadableFlux(env_analyzer.solar_flux) : "none", Lib.HumanReadableTemp(Sim.BlackBodyTemperature(env_analyzer.solar_flux))),
				String.Format("{0,-14}\t{1,-15}\t{2}\n", "albedo", env_analyzer.albedo_flux > 0.0 ? Lib.HumanReadableFlux(env_analyzer.albedo_flux) : "none", Lib.HumanReadableTemp(Sim.BlackBodyTemperature(env_analyzer.albedo_flux))),
				String.Format("{0,-14}\t{1,-15}\t{2}\n", "body", env_analyzer.body_flux > 0.0 ? Lib.HumanReadableFlux(env_analyzer.body_flux) : "none", Lib.HumanReadableTemp(Sim.BlackBodyTemperature(env_analyzer.body_flux))),
				String.Format("{0,-14}\t{1,-15}\t{2}\n", "background", Lib.HumanReadableFlux(Sim.BackgroundFlux()), Lib.HumanReadableTemp(Sim.BlackBodyTemperature(Sim.BackgroundFlux()))),
				String.Format("{0,-14}\t\t{1,-15}\t{2}", "total", Lib.HumanReadableFlux(env_analyzer.total_flux), Lib.HumanReadableTemp(Sim.BlackBodyTemperature(env_analyzer.total_flux)))
			);
			string atmosphere_tooltip = Lib.BuildString
			(
				"<align=left />",
				String.Format("{0,-14}\t<b>{1}</b>\n", "breathable", Sim.Breathable(env_analyzer.body) ? "yes" : "no"),
				String.Format("{0,-14}\t<b>{1}</b>\n", "pressure", Lib.HumanReadablePressure(env_analyzer.body.atmospherePressureSeaLevel)),
				String.Format("{0,-14}\t<b>{1}</b>\n", "light absorption", Lib.HumanReadablePerc(1.0 - env_analyzer.atmo_factor)),
				String.Format("{0,-14}\t<b>{1}</b>", "gamma absorption", Lib.HumanReadablePerc(1.0 - Sim.GammaTransparency(env_analyzer.body, 0.0)))
			);
			string shadowtime_str = Lib.HumanReadableDuration(env_analyzer.shadow_period) + " (" + (env_analyzer.shadow_time * 100.0).ToString("F0") + "%)";

			p.AddSection("ENVIRONMENT", string.Empty,
				() => { p.Prev(ref environment_index, panel_environment.Count); enforceUpdate = true; },
				() => { p.Next(ref environment_index, panel_environment.Count); enforceUpdate = true; });
			p.AddContent("temperature", Lib.HumanReadableTemp(env_analyzer.temperature), env_analyzer.body.atmosphere && env_analyzer.landed ? "atmospheric" : flux_tooltip);
			p.AddContent("difference", Lib.HumanReadableTemp(env_analyzer.temp_diff), "difference between external and survival temperature");
			p.AddContent("atmosphere", env_analyzer.body.atmosphere ? "yes" : "no", atmosphere_tooltip);
			p.AddContent("shadow time", shadowtime_str, "the time in shadow\nduring the orbit");
		}

		///<summary> Add electric charge sub-panel, including tooltips </summary>
		private static void AddSubPanelEC(Panel p)
		{
			// get simulated resource
			SimulatedResource res = resource_sim.Resource("ElectricCharge");

			// create tooltip
			string tooltip = res.Tooltip();

			// render the panel section
			p.AddSection("ELECTRIC CHARGE");
			p.AddContent("storage", Lib.HumanReadableAmount(res.storage), tooltip);
			p.AddContent("consumed", Lib.HumanReadableRate(res.consumed), tooltip);
			p.AddContent("produced", Lib.HumanReadableRate(res.produced), tooltip);
			p.AddContent("duration", Lib.HumanReadableDuration(res.Lifetime()));
		}

		///<summary> Add supply resource sub-panel, including tooltips </summary>
		///<remarks>
		/// does not include electric charge
		/// does not include special resources like waste atmosphere
		/// restricted to resources that are configured explicitly in the profile as supplies
		///</remarks>
		private static void AddSubPanelResource(Panel p, string res_name)
		{
			// get simulated resource
			SimulatedResource res = resource_sim.Resource(res_name);

			// create tooltip
			string tooltip = res.Tooltip();

			var resource = PartResourceLibrary.Instance.resourceDefinitions[res_name];

			// render the panel section
			p.AddSection(Lib.SpacesOnCaps(resource.displayName).ToUpper(), string.Empty,
				() => { p.Prev(ref resource_index, panel_resource.Count); enforceUpdate = true; },
				() => { p.Next(ref resource_index, panel_resource.Count); enforceUpdate = true; });
			p.AddContent("storage", Lib.HumanReadableAmount(res.storage), tooltip);
			p.AddContent("consumed", Lib.HumanReadableRate(res.consumed), tooltip);
			p.AddContent("produced", Lib.HumanReadableRate(res.produced), tooltip);
			p.AddContent("duration", Lib.HumanReadableDuration(res.Lifetime()));
		}

		///<summary> Add stress sub-panel, including tooltips </summary>
		private static void AddSubPanelStress(Panel p)
		{
			// get first living space rule
			// - guaranteed to exist, as this panel is not rendered if it doesn't
			// - even without crew, it is safe to evaluate the modifiers that use it
			Rule rule = Profile.rules.Find(k => k.modifiers.Contains("living_space"));

			// render title
			p.AddSection("STRESS", string.Empty,
				() => { p.Prev(ref special_index, panel_special.Count); enforceUpdate = true; },
				() => { p.Next(ref special_index, panel_special.Count); enforceUpdate = true; });

			// render living space data
			// generate details tooltips
			string living_space_tooltip = Lib.BuildString
			(
				"volume per-capita:<b>\t", Lib.HumanReadableVolume(vessel_analyzer.volume / Math.Max(vessel_analyzer.crew_count, 1)), "</b>\n",
				"ideal living space:<b>\t", Lib.HumanReadableVolume(PreferencesComfort.Instance.livingSpace), "</b>"
			);
			p.AddContent("living space", Habitat.Living_space_to_string(vessel_analyzer.living_space), living_space_tooltip);

			// render comfort data
			if (rule.modifiers.Contains("comfort"))
			{
				p.AddContent("comfort", vessel_analyzer.comforts.Summary(), vessel_analyzer.comforts.Tooltip());
			}
			else
			{
				p.AddContent("comfort", "n/a");
			}

			// render pressure data
			if (rule.modifiers.Contains("pressure"))
			{
				string pressure_tooltip = vessel_analyzer.pressurized
				  ? "Free roaming in a pressurized environment is\nvastly superior to living in a suit."
				  : "Being forced inside a suit all the time greatly\nreduces the crews quality of life.\nThe worst part is the diaper.";
				p.AddContent("pressurized", vessel_analyzer.pressurized ? "yes" : "no", pressure_tooltip);
			}
			else
			{
				p.AddContent("pressurized", "n/a");
			}

			// render life estimate
			double mod = Modifiers.Evaluate(env_analyzer, vessel_analyzer, resource_sim, rule.modifiers);
			p.AddContent("duration", Lib.HumanReadableDuration(rule.fatal_threshold / (rule.degeneration * mod)));
		}

		///<summary> Add radiation sub-panel, including tooltips </summary>
		private static void AddSubPanelRadiation(Panel p)
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
				Math.Max(Radiation.Nominal, (env_analyzer.surface_rad + vessel_analyzer.emitted)),        // surface
				Math.Max(Radiation.Nominal, (env_analyzer.magnetopause_rad + vessel_analyzer.emitted)),   // inside magnetopause
				Math.Max(Radiation.Nominal, (env_analyzer.inner_rad + vessel_analyzer.emitted)),          // inside inner belt
				Math.Max(Radiation.Nominal, (env_analyzer.outer_rad + vessel_analyzer.emitted)),          // inside outer belt
				Math.Max(Radiation.Nominal, (env_analyzer.heliopause_rad + vessel_analyzer.emitted)),     // interplanetary
				Math.Max(Radiation.Nominal, (env_analyzer.extern_rad + vessel_analyzer.emitted)),         // interstellar
				Math.Max(Radiation.Nominal, (env_analyzer.storm_rad + vessel_analyzer.emitted))           // storm
			};

			// evaluate modifiers (except radiation)
			List<string> modifiers_except_radiation = new List<string>();
			foreach (string s in rule.modifiers)
			{ if (s != "radiation") modifiers_except_radiation.Add(s); }
			double mod = Modifiers.Evaluate(env_analyzer, vessel_analyzer, resource_sim, modifiers_except_radiation);

			// calculate life expectancy at various radiation levels
			double[] estimates = new double[7];
			for (int i = 0; i < 7; ++i)
			{
				estimates[i] = rule.fatal_threshold / (rule.degeneration * mod * levels[i]);
			}

			// generate tooltip
			RadiationModel mf = Radiation.Info(env_analyzer.body).model;
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
			p.AddSection("RADIATION", string.Empty,
				() => { p.Prev(ref special_index, panel_special.Count); enforceUpdate = true; },
				() => { p.Next(ref special_index, panel_special.Count); enforceUpdate = true; });
			p.AddContent("surface", Lib.HumanReadableRadiation(env_analyzer.surface_rad + vessel_analyzer.emitted), tooltip);
			p.AddContent("orbit", Lib.HumanReadableRadiation(env_analyzer.magnetopause_rad), tooltip);
			if (vessel_analyzer.emitted >= 0.0)
				p.AddContent("emission", Lib.HumanReadableRadiation(vessel_analyzer.emitted), tooltip);
			else
				p.AddContent("active shielding", Lib.HumanReadableRadiation(-vessel_analyzer.emitted), tooltip);
			p.AddContent("shielding", rule.modifiers.Contains("shielding") ? Habitat.Shielding_to_string(vessel_analyzer.shielding) : "n/a", tooltip);
		}

		///<summary> Add reliability sub-panel, including tooltips </summary>
		private static void AddSubPanelReliability(Panel p)
		{
			// evaluate redundancy metric
			// - 0: no redundancy
			// - 0.5: all groups have 2 elements
			// - 1.0: all groups have 3 or more elements
			double redundancy_metric = 0.0;
			foreach (KeyValuePair<string, int> pair in vessel_analyzer.redundancy)
			{
				switch (pair.Value)
				{
					case 1:
						break;
					case 2:
						redundancy_metric += 0.5 / vessel_analyzer.redundancy.Count;
						break;
					default:
						redundancy_metric += 1.0 / vessel_analyzer.redundancy.Count;
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
			if (vessel_analyzer.redundancy.Count > 0)
			{
				StringBuilder sb = new StringBuilder();
				foreach (KeyValuePair<string, int> pair in vessel_analyzer.redundancy)
				{
					if (sb.Length > 0)
						sb.Append("\n");
					sb.Append(Lib.Color(pair.Value.ToString(), pair.Value == 1 ? Lib.Kolor.Red : pair.Value == 2 ? Lib.Kolor.Yellow : Lib.Kolor.Green, true));
					sb.Append("\t");
					sb.Append(pair.Key);
				}
				redundancy_tooltip = Lib.BuildString("<align=left />", sb.ToString());
			}

			// generate repair string and tooltip
			string repair_str = "none";
			string repair_tooltip = string.Empty;
			if (vessel_analyzer.crew_engineer)
			{
				repair_str = "engineer";
				repair_tooltip = "The engineer on board should\nbe able to handle all repairs";
			}
			else if (vessel_analyzer.crew_capacity == 0)
			{
				repair_str = "safemode";
				repair_tooltip = "We have a chance of repairing\nsome of the malfunctions remotely";
			}

			// render panel
			p.AddSection("RELIABILITY", string.Empty,
				() => { p.Prev(ref special_index, panel_special.Count); enforceUpdate = true; },
				() => { p.Next(ref special_index, panel_special.Count); enforceUpdate = true; });
			p.AddContent("malfunctions", Lib.HumanReadableAmount(vessel_analyzer.failure_year, "/y"), "average case estimate\nfor the whole vessel");
			p.AddContent("high quality", Lib.HumanReadablePerc(vessel_analyzer.high_quality), "percentage of high quality components");
			p.AddContent("redundancy", redundancy_str, redundancy_tooltip);
			p.AddContent("repair", repair_str, repair_tooltip);
		}

		///<summary> Add habitat sub-panel, including tooltips </summary>
		private static void AddSubPanelHabitat(Panel p)
		{
			SimulatedResource atmo_res = resource_sim.Resource("Atmosphere");
			SimulatedResource waste_res = resource_sim.Resource("WasteAtmosphere");

			// generate tooltips
			string atmo_tooltip = atmo_res.Tooltip();
			string waste_tooltip = waste_res.Tooltip(true);

			// generate status string for scrubbing
			string waste_status = !Features.Poisoning                   //< feature disabled
			  ? "n/a"
			  : waste_res.produced <= double.Epsilon                    //< unnecessary
			  ? "not required"
			  : waste_res.consumed <= double.Epsilon                    //< no scrubbing
			  ? Lib.Color("none", Lib.Kolor.Orange)
			  : waste_res.produced > waste_res.consumed * 1.001         //< insufficient scrubbing
			  ? Lib.Color("inadequate", Lib.Kolor.Yellow)
			  : Lib.Color("good", Lib.Kolor.Green);                    //< sufficient scrubbing

			// generate status string for pressurization
			string atmo_status = !Features.Pressure                     //< feature disabled
			  ? "n/a"
			  : atmo_res.consumed <= double.Epsilon                     //< unnecessary
			  ? "not required"
			  : atmo_res.produced <= double.Epsilon                     //< no pressure control
			  ? Lib.Color("none", Lib.Kolor.Orange)
			  : atmo_res.consumed > atmo_res.produced * 1.001           //< insufficient pressure control
			  ? Lib.Color("inadequate", Lib.Kolor.Yellow)
			  : Lib.Color("good", Lib.Kolor.Green);                    //< sufficient pressure control

			p.AddSection("HABITAT", string.Empty,
				() => { p.Prev(ref environment_index, panel_environment.Count); enforceUpdate = true; },
				() => { p.Next(ref environment_index, panel_environment.Count); enforceUpdate = true; });
			p.AddContent("volume", Lib.HumanReadableVolume(vessel_analyzer.volume), "volume of enabled habitats");
			p.AddContent("surface", Lib.HumanReadableSurface(vessel_analyzer.surface), "surface of enabled habitats");
			p.AddContent("scrubbing", waste_status, waste_tooltip);
			p.AddContent("pressurization", atmo_status, atmo_tooltip);
		}
#endregion

#region FIELDS_PROPERTIES
		// store situations and altitude multipliers
		private static readonly string[] situations = { "Landed", "Low Orbit", "Orbit", "High Orbit" };
		private static readonly double[] altitude_mults = { 0.0, 0.33, 1.0, 3.0 };

		// styles
		private static GUIStyle devbuild_style;
		private static GUIStyle leftmenu_style;
		private static GUIStyle rightmenu_style;
		private static GUIStyle quote_style;
		private static GUIStyle icon_style;

		// analyzers
		private static ResourceSimulator resource_sim = new ResourceSimulator();
		private static EnvironmentAnalyzer env_analyzer = new EnvironmentAnalyzer();
		private static VesselAnalyzer vessel_analyzer = new VesselAnalyzer();

		// panel arrays
		private static List<string> supplies = new List<string>();
		private static List<string> panel_resource = new List<string>();
		private static List<string> panel_special = new List<string>();
		private static List<string> panel_environment = new List<string>();

		// body/situation/sunlight indexes
		private static int body_index;
		private static int situation_index = 2;     // orbit
		public enum SunlightState { SunlightNominal = 0, SunlightSimulated = 1, Shadow = 2 }
		private static SunlightState sunlight = SunlightState.SunlightSimulated;
		public static SunlightState Sunlight => sunlight;

		// panel indexes
		private static int resource_index;
		private static int special_index;
		private static int environment_index;

		// panel ui
		private static Panel panel = new Panel();
		private static bool enforceUpdate = false;
		private static int update_counter = 0;
#endregion
	}


} // KERBALISM
