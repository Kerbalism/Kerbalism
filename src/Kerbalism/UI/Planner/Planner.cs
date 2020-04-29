using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using KSP.Localization;


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
			if (Features.LifeSupport)
				panel_special.Add("qol");
			if (Features.Radiation)
				panel_special.Add("radiation");
			if (Features.Failures)
				panel_special.Add("reliability");

			// environment panels
			if (Features.LifeSupport)
				panel_environment.Add("habitat");

			panel_environment.Add("environment");
			panel_environment.Add("comms");
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
		internal static void RefreshPlanner() => updateRequested = true;

		///<summary> Run simulators and update the planner UI sub-panels </summary>
		internal static void Update()
		{
			// get data objects references
			vesselData = VesselDataShip.Instance;
			resHandler = VesselDataShip.Instance.ResHandler;

			// get vessel crew manifest
			VesselCrewManifest manifest = KSP.UI.CrewAssignmentDialog.Instance.GetManifest();
			if (manifest == null)
				return;

			// check for number of crew change
			if (vesselData.crewCount != manifest.CrewCount)
				updateRequested = true;

			if (!updateRequested)
			{
				return;
			}
			else
			{
				// skip a few updates to make sure everything has properly reacted to whatever
				// was requesting an update
				if (update_counter < 3)
				{
					update_counter++;
					return;
				}
				else
				{
					updateRequested = false;
					update_counter = 0;
				}
			}

			// clear the panel
			panel.Clear();

			// if there is something in the editor
			if (EditorLogic.RootPart != null)
			{
				// get parts recursively
				List<Part> parts = Lib.GetPartsRecursively(EditorLogic.RootPart);

				// analyze using the settings from the panels user input
				vesselData.Analyze(parts, FlightGlobals.Bodies[body_index], altitude_mults[situation_index], sunlight);
				EditorResourceSimulator.Analyze(parts);

				// add ec panel
				AddSubPanelEC(panel);

				// get vessel resources
				panel_resource.Clear();
				foreach (string res in supplies)
					if (resHandler.GetResource(res).Capacity > 0.0)
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
					case "comms":
						AddSubPanelComms(panel);
						break;
				}
			}
			updateRequested = false;
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
				GUILayout.Label(new GUIContent(FlightGlobals.Bodies[body_index].bodyDisplayName, Local.Planner_Targetbody), leftmenu_style);//"Target body"
				if (Lib.IsClicked())
				{ body_index = (body_index + 1) % FlightGlobals.Bodies.Count; if (body_index == 0) ++body_index; updateRequested = true; }
				else if (Lib.IsClicked(1))
				{ body_index = (body_index - 1) % FlightGlobals.Bodies.Count; if (body_index == 0) body_index = FlightGlobals.Bodies.Count - 1; updateRequested = true; }

				// sunlight selector
				switch (sunlight)
				{
					case SunlightState.SunlightNominal: GUILayout.Label(new GUIContent(Textures.sun_white, Local.Planner_SunlightNominal), icon_style); break;//"In sunlight\n<b>Nominal</b> solar panel output"
					case SunlightState.SunlightSimulated: GUILayout.Label(new GUIContent(Textures.solar_panel, Local.Planner_SunlightSimulated), icon_style); break;//"In sunlight\n<b>Estimated</b> solar panel output\n<i>Sunlight direction : look at the shadows !</i>"
					case SunlightState.Shadow: GUILayout.Label(new GUIContent(Textures.sun_black, Local.Planner_Shadow), icon_style); break;//"In shadow"
				}
				if (Lib.IsClicked())
				{ sunlight = (SunlightState)(((int)sunlight + 1) % Enum.GetValues(typeof(SunlightState)).Length); updateRequested = true; }

				// situation selector
				GUILayout.Label(new GUIContent(situations[situation_index], Local.Planner_Targetsituation), rightmenu_style);//"Target situation"
				if (Lib.IsClicked())
				{ situation_index = (situation_index + 1) % situations.Length; updateRequested = true; }
				else if (Lib.IsClicked(1))
				{ situation_index = (situation_index == 0 ? situations.Length : situation_index) - 1; updateRequested = true; }

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
				GUILayout.Label("<i>"+Local.Planner_RenderQuote +"</i>", quote_style);//In preparing for space, I have always found that\nplans are useless but planning is indispensable.\nWernher von Kerman
				GUILayout.EndHorizontal();
				GUILayout.Space(Styles.ScaleFloat(10.0f));
			}
		}

		///<summary> Add electric charge sub-panel, including tooltips </summary>
		private static void AddSubPanelEC(Panel p)
		{
			// get simulated resource
			VesselKSPResource res = resHandler.ElectricCharge;

			// create tooltip
			string tooltip = res.BrokersListTooltip();

			// render the panel section
			p.AddSection(res.Title.ToUpper());//"ELECTRIC CHARGE"
			p.AddContent(Local.Planner_storage, Lib.HumanReadableStorage(res.Amount, res.Capacity), tooltip);//"storage"
			p.AddContent(Local.Planner_consumed, Lib.HumanReadableRate(res.ConsumeRequests), tooltip);//"consumed"
			p.AddContent(Local.Planner_produced, Lib.HumanReadableRate(res.ProduceRequests), tooltip);//"produced"
			p.AddContent(Local.Planner_duration, res.DepletionInfo);//"duration"
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
			VesselKSPResource res = (VesselKSPResource)resHandler.GetResource(res_name);

			// create tooltip
			string tooltip = res.BrokersListTooltip();

			// render the panel section
			p.AddSection(res.Title.ToUpper(), string.Empty,
				() => { p.Prev(ref resource_index, panel_resource.Count); updateRequested = true; },
				() => { p.Next(ref resource_index, panel_resource.Count); updateRequested = true; });
			p.AddContent(Local.Planner_storage, Lib.BuildString(Lib.HumanReadableStorage(res.Amount, res.Capacity), " (" + Lib.HumanReadableMass(res.Amount * res.Density) + ")"), tooltip);//"storage"
			p.AddContent(Local.Planner_consumed, Lib.HumanReadableRate(res.ConsumeRequests), tooltip);//"consumed"
			p.AddContent(Local.Planner_produced, Lib.HumanReadableRate(res.ProduceRequests), tooltip);//"produced"
			p.AddContent(Local.Planner_duration, res.DepletionInfo);//"duration"
		}

		///<summary> Add stress sub-panel, including tooltips </summary>
		private static void AddSubPanelStress(Panel p)
		{
			// get first living space rule
			// - guaranteed to exist, as this panel is not rendered if it doesn't
			// - even without crew, it is safe to evaluate the modifiers that use it
			Rule rule = Profile.rules.Find(k => k.name == "stress");

			// render title
			p.AddSection(Local.Planner_STRESS, string.Empty,//"STRESS"
				() => { p.Prev(ref special_index, panel_special.Count); updateRequested = true; },
				() => { p.Next(ref special_index, panel_special.Count); updateRequested = true; });

			// render living space data
			// generate details tooltips
			string living_space_tooltip = Lib.BuildString
			(
				Local.Planner_volumepercapita ,"<b>\t", Lib.HumanReadableVolume(vesselData.Habitat.volumePerCrew), "</b>\n",//"volume per-capita:
				Local.Planner_ideallivingspace ,"<b>\t", Lib.HumanReadableVolume(PreferencesComfort.Instance.livingSpace), "</b>"//"ideal living space:
			);
			p.AddContent(Local.Planner_livingspace, HabitatLib.LivingSpaceFactorToString(vesselData.Habitat.livingSpaceFactor), living_space_tooltip);//"living space"


			p.AddContent(Local.Planner_comfort, HabitatLib.ComfortSummary(vesselData.Habitat.comfortFactor), HabitatLib.ComfortTooltip(vesselData.Habitat.comfortMask, vesselData.Habitat.comfortFactor));//"comfort"


			// render pressure data
			string pressure_tooltip = vesselData.Habitat.pressure == 1.0
				? Local.Planner_analyzerpressurized1//"Free roaming in a pressurized environment is\nvastly superior to living in a suit."
				: Local.Planner_analyzerpressurized2;//"Being forced inside a suit all the time greatly\nreduces the crews quality of life.\nThe worst part is the diaper."
			p.AddContent(Local.Planner_pressurized, vesselData.Habitat.pressure == 1.0 ? Local.Planner_pressurized_yes : Local.Planner_pressurized_no, pressure_tooltip);//"pressurized""yes""no"

			// render life estimate
			p.AddContent(Local.Planner_lifeestimate, Lib.HumanReadableDuration(rule.fatal_threshold / (rule.degeneration * rule.EvaluateModifier(vesselData))));//"duration"
		}

		///<summary> Add radiation sub-panel, including tooltips </summary>
		private static void AddSubPanelRadiation(Panel p)
		{
			// get first radiation rule
			// - guaranteed to exist, as this panel is not rendered if it doesn't
			// - even without crew, it is safe to evaluate the modifiers that use it
			Rule rule = Profile.rules.Find(k => k.name.Contains("radiation"));

			// detect if it use shielding
			bool use_shielding = rule.name.Contains("shielding");

			// calculate various radiation levels
			double[] levels = new[]
			{
				Math.Max(Radiation.Nominal, (vesselData.surfaceRad + vesselData.emitted)),        // surface
				Math.Max(Radiation.Nominal, (vesselData.magnetopauseRad + vesselData.emitted)),   // inside magnetopause
				Math.Max(Radiation.Nominal, (vesselData.innerRad + vesselData.emitted)),          // inside inner belt
				Math.Max(Radiation.Nominal, (vesselData.outerRad + vesselData.emitted)),          // inside outer belt
				Math.Max(Radiation.Nominal, (vesselData.heliopauseRad + vesselData.emitted)),     // interplanetary
				Math.Max(Radiation.Nominal, (vesselData.externRad + vesselData.emitted)),         // interstellar
				Math.Max(Radiation.Nominal, (vesselData.stormRad + vesselData.emitted))           // storm
			};

			// calculate life expectancy at various radiation levels
			double[] estimates = new double[7];
			for (int i = 0; i < 7; ++i)
			{
				vesselData.habitatRadiation = levels[i];
				estimates[i] = rule.fatal_threshold / (rule.degeneration * rule.EvaluateModifier(vesselData));
			}

			// generate tooltip
			RadiationModel mf = Radiation.Info(vesselData.body).model;
			string tooltip = Lib.BuildString
			(
				"<align=left />",
				String.Format("{0,-20}\t<b>{1}</b>\n", Local.Planner_surface, Lib.HumanReadableDuration(estimates[0])),//"surface"
				mf.has_pause ? String.Format("{0,-20}\t<b>{1}</b>\n", Local.Planner_magnetopause, Lib.HumanReadableDuration(estimates[1])) : "",//"magnetopause"
				mf.has_inner ? String.Format("{0,-20}\t<b>{1}</b>\n", Local.Planner_innerbelt, Lib.HumanReadableDuration(estimates[2])) : "",//"inner belt"
				mf.has_outer ? String.Format("{0,-20}\t<b>{1}</b>\n", Local.Planner_outerbelt, Lib.HumanReadableDuration(estimates[3])) : "",//"outer belt"
				String.Format("{0,-20}\t<b>{1}</b>\n", Local.Planner_interplanetary, Lib.HumanReadableDuration(estimates[4])),//"interplanetary"
				String.Format("{0,-20}\t<b>{1}</b>\n", Local.Planner_interstellar, Lib.HumanReadableDuration(estimates[5])),//"interstellar"
				String.Format("{0,-20}\t<b>{1}</b>", Local.Planner_storm, Lib.HumanReadableDuration(estimates[6]))//"storm"
			);

			// render the panel
			p.AddSection(Local.Planner_RADIATION, string.Empty,//"RADIATION"
				() => { p.Prev(ref special_index, panel_special.Count); updateRequested = true; },
				() => { p.Next(ref special_index, panel_special.Count); updateRequested = true; });
			p.AddContent(Local.Planner_surface, Lib.HumanReadableRadiation(vesselData.surfaceRad + vesselData.emitted), tooltip);//"surface"
			p.AddContent(Local.Planner_orbit, Lib.HumanReadableRadiation(vesselData.magnetopauseRad), tooltip);//"orbit"
			if (vesselData.emitted >= 0.0)
				p.AddContent(Local.Planner_emission, Lib.HumanReadableRadiation(vesselData.emitted), tooltip);//"emission"
			else
				p.AddContent(Local.Planner_activeshielding, Lib.HumanReadableRadiation(-vesselData.emitted), tooltip);//"active shielding"
			p.AddContent(Local.Planner_shielding, Radiation.VesselShieldingToString(vesselData.Habitat.shieldingSurface > 0.0 ? vesselData.Habitat.shieldingAmount / vesselData.Habitat.shieldingSurface : 0.0), tooltip);//"shielding"
		}

		///<summary> Add reliability sub-panel, including tooltips </summary>
		private static void AddSubPanelReliability(Panel p)
		{
			// evaluate redundancy metric
			// - 0: no redundancy
			// - 0.5: all groups have 2 elements
			// - 1.0: all groups have 3 or more elements
			double redundancy_metric = 0.0;
			foreach (KeyValuePair<string, int> pair in vesselData.redundancy)
			{
				switch (pair.Value)
				{
					case 1:
						break;
					case 2:
						redundancy_metric += 0.5 / vesselData.redundancy.Count;
						break;
					default:
						redundancy_metric += 1.0 / vesselData.redundancy.Count;
						break;
				}
			}

			// traduce the redundancy metric to string
			string redundancy_str = string.Empty;
			if (redundancy_metric <= 0.1)
				redundancy_str = Local.Planner_none;//"none"
			else if (redundancy_metric <= 0.33)
				redundancy_str = Local.Planner_poor;//"poor"
			else if (redundancy_metric <= 0.66)
				redundancy_str = Local.Planner_okay;//"okay"
			else
				redundancy_str = Local.Planner_great;//"great"

			// generate redundancy tooltip
			string redundancy_tooltip = string.Empty;
			if (vesselData.redundancy.Count > 0)
			{
				StringBuilder sb = new StringBuilder();
				foreach (KeyValuePair<string, int> pair in vesselData.redundancy)
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
			string repair_str = Local.Planner_none;//"none"
			string repair_tooltip = string.Empty;
			if (vesselData.crewEngineer)
			{
				repair_str = "engineer";
				repair_tooltip = Local.Planner_engineer_tip;//"The engineer on board should\nbe able to handle all repairs"
			}
			else if (vesselData.crewCapacity == 0)
			{
				repair_str = "safemode";
				repair_tooltip = Local.Planner_safemode_tip;//"We have a chance of repairing\nsome of the malfunctions remotely"
			}

			// render panel
			p.AddSection(Local.Planner_RELIABILITY, string.Empty,//"RELIABILITY"
				() => { p.Prev(ref special_index, panel_special.Count); updateRequested = true; },
				() => { p.Next(ref special_index, panel_special.Count); updateRequested = true; });
			p.AddContent(Local.Planner_malfunctions, Lib.HumanReadableAmount(vesselData.failureYear, "/y"), Local.Planner_malfunctions_tip);//"malfunctions""average case estimate\nfor the whole vessel"
			p.AddContent(Local.Planner_highquality, Lib.HumanReadablePerc(vesselData.highQuality), Local.Planner_highquality_tip);//"high quality""percentage of high quality components"
			p.AddContent(Local.Planner_redundancy, redundancy_str, redundancy_tooltip);//"redundancy"
			p.AddContent(Local.Planner_repair, repair_str, repair_tooltip);//"repair"
		}

		///<summary> Add environment sub-panel, including tooltips </summary>
		private static void AddSubPanelEnvironment(Panel p)
		{
			string flux_tooltip = Lib.BuildString
			(
				"<align=left />" +
				String.Format("<b>{0,-14}\t{1,-15}\t{2}</b>\n", Local.Planner_Source, Local.Planner_Flux, Local.Planner_Temp),//"Source""Flux""Temp"
				String.Format("{0,-14}\t{1,-15}\t{2}\n", Local.Planner_solar, vesselData.solarFlux > 0.0 ? Lib.HumanReadableFlux(vesselData.solarFlux) : Local.Generic_NONE, Lib.HumanReadableTemp(Sim.BlackBodyTemperature(vesselData.solarFlux))),//"solar""none"
				String.Format("{0,-14}\t{1,-15}\t{2}\n", Local.Planner_albedo, vesselData.albedoFlux > 0.0 ? Lib.HumanReadableFlux(vesselData.albedoFlux) : Local.Generic_NONE, Lib.HumanReadableTemp(Sim.BlackBodyTemperature(vesselData.albedoFlux))),//"albedo""none"
				String.Format("{0,-14}\t{1,-15}\t{2}\n", Local.Planner_body, vesselData.bodyFlux > 0.0 ? Lib.HumanReadableFlux(vesselData.bodyFlux) : Local.Generic_NONE, Lib.HumanReadableTemp(Sim.BlackBodyTemperature(vesselData.bodyFlux))),//"body""none"
				String.Format("{0,-14}\t{1,-15}\t{2}\n", Local.Planner_background, Lib.HumanReadableFlux(Sim.BackgroundFlux), Lib.HumanReadableTemp(Sim.BlackBodyTemperature(Sim.BackgroundFlux))),//"background"
				String.Format("{0,-14}\t\t{1,-15}\t{2}", Local.Planner_total, Lib.HumanReadableFlux(vesselData.totalFlux), Lib.HumanReadableTemp(Sim.BlackBodyTemperature(vesselData.totalFlux)))//"total"
			);
			string atmosphere_tooltip = Lib.BuildString
			(
				"<align=left />",
				String.Format("{0,-14}\t<b>{1}</b>\n", Local.BodyInfo_breathable, Sim.Breathable(vesselData.body) ? Local.BodyInfo_breathable_yes : Local.BodyInfo_breathable_no),//"breathable""yes""no"
				String.Format("{0,-14}\t<b>{1}</b>\n", Local.Planner_pressure, Lib.HumanReadablePressure(vesselData.body.atmospherePressureSeaLevel)),//"pressure"
				String.Format("{0,-14}\t<b>{1}</b>\n", Local.BodyInfo_lightabsorption, Lib.HumanReadablePerc(1.0 - vesselData.atmoFactor)),//"light absorption"
				String.Format("{0,-14}\t<b>{1}</b>", Local.BodyInfo_gammaabsorption, Lib.HumanReadablePerc(1.0 - Sim.GammaTransparency(vesselData.body, 0.0)))//"gamma absorption"
			);
			string shadowtime_str = Lib.HumanReadableDuration(vesselData.shadowPeriod) + " (" + (vesselData.shadowTime * 100.0).ToString("F0") + "%)";

			p.AddSection(Local.TELEMETRY_ENVIRONMENT, string.Empty,//"ENVIRONMENT"
				() => { p.Prev(ref environment_index, panel_environment.Count); updateRequested = true; },
				() => { p.Next(ref environment_index, panel_environment.Count); updateRequested = true; });
			p.AddContent(Local.Planner_temperature, Lib.HumanReadableTemp(vesselData.temperature), vesselData.body.atmosphere && vesselData.landed ? Local.Planner_atmospheric : flux_tooltip);//"temperature""atmospheric"
			p.AddContent(Local.Planner_difference, Lib.HumanReadableTemp(vesselData.tempDiff), Local.Planner_difference_desc);//"difference""difference between external and survival temperature"
			p.AddContent(Local.Planner_atmosphere, vesselData.body.atmosphere ? Local.Planner_atmosphere_yes : Local.Planner_atmosphere_no, atmosphere_tooltip);//"atmosphere""yes""no"
			p.AddContent(Local.Planner_shadowtime, shadowtime_str, Local.Planner_shadowtime_desc);//"shadow time""the time in shadow\nduring the orbit"
		}

		///<summary> Add habitat sub-panel, including tooltips </summary>
		private static void AddSubPanelHabitat(Panel p)
		{

			VesselKSPResource atmo_res = (VesselKSPResource)resHandler.GetResource(Settings.HabitatAtmoResource);
			VesselKSPResource waste_res = (VesselKSPResource)resHandler.GetResource(Settings.HabitatWasteResource);

			// generate tooltips
			string atmo_tooltip = atmo_res.BrokersListTooltip();
			string waste_tooltip = waste_res.BrokersListTooltip();

			// generate status string for scrubbing
			string waste_status = !Features.LifeSupport                   //< feature disabled
			  ? "n/a"
			  : waste_res.ProduceRequests <= double.Epsilon                    //< unnecessary
			  ? Local.Planner_scrubbingunnecessary//"not required"
			  : waste_res.ConsumeRequests <= double.Epsilon                    //< no scrubbing
			  ? Lib.Color(Local.Planner_noscrubbing, Lib.Kolor.Orange)//"none"
			  : waste_res.ProduceRequests > waste_res.ConsumeRequests * 1.001         //< insufficient scrubbing
			  ? Lib.Color(Local.Planner_insufficientscrubbing, Lib.Kolor.Yellow)//"inadequate"
			  : Lib.Color(Local.Planner_sufficientscrubbing, Lib.Kolor.Green);//"good"                    //< sufficient scrubbing

			// generate status string for pressurization
			string atmo_status = !Features.LifeSupport                     //< feature disabled
			  ? "n/a"
			  : atmo_res.ConsumeRequests <= double.Epsilon                     //< unnecessary
			  ? Local.Planner_pressurizationunnecessary//"not required"
			  : atmo_res.ProduceRequests <= double.Epsilon                     //< no pressure control
			  ? Lib.Color(Local.Planner_nopressurecontrol, Lib.Kolor.Orange)//"none"
			  : atmo_res.ConsumeRequests > atmo_res.ProduceRequests * 1.001           //< insufficient pressure control
			  ? Lib.Color(Local.Planner_insufficientpressurecontrol, Lib.Kolor.Yellow)//"inadequate"
			  : Lib.Color(Local.Planner_sufficientpressurecontrol, Lib.Kolor.Green);//"good"                    //< sufficient pressure control

			p.AddSection(Local.Planner_HABITAT, string.Empty,//"HABITAT"
				() => { p.Prev(ref environment_index, panel_environment.Count); updateRequested = true; },
				() => { p.Next(ref environment_index, panel_environment.Count); updateRequested = true; });
			p.AddContent(Local.Planner_volume, Lib.HumanReadableVolume(vesselData.Habitat.livingVolume), Local.Planner_volume_tip);//"volume""volume of enabled habitats"
			p.AddContent(Local.Planner_habitatssurface, Lib.HumanReadableSurface(vesselData.Habitat.pressurizedSurface), Local.Planner_habitatssurface_tip);//"surface""surface of enabled habitats"
			p.AddContent(Local.Planner_scrubbing, waste_status, waste_tooltip);//"scrubbing"
			p.AddContent(Local.Planner_pressurization, atmo_status, atmo_tooltip);//"pressurization"

		}

		private static void AddSubPanelComms(Panel p)
		{
			p.AddSection("COMMS", string.Empty,
				() => { p.Prev(ref environment_index, panel_environment.Count); updateRequested = true; },
				() => { p.Next(ref environment_index, panel_environment.Count); updateRequested = true; });

			p.AddContent("Nominal power", Lib.BuildString(Lib.HumanReadableDistance(vesselData.connection.basePower), ", ", Lib.HumanReadableDataRate(vesselData.connection.baseRate)));
			p.AddContent(Lib.BuildString("Max range to", " L", vesselData.connection.dsnLevel.ToString(), " DSN"), Lib.HumanReadableDistance(vesselData.connection.maxRange));
			p.AddContent(Lib.BuildString("Power at", " ", Lib.HumanReadableDistance(vesselData.minHomeDistance)),
				Lib.BuildString(vesselData.connection.minDistanceStrength.ToString("P0"), ", ", Lib.HumanReadableDataRate(vesselData.connection.minDistanceRate)));
			p.AddContent(Lib.BuildString("Power at", " ", Lib.HumanReadableDistance(vesselData.maxHomeDistance)),
				Lib.BuildString(vesselData.connection.maxDistanceStrength.ToString("P0"), ", ", Lib.HumanReadableDataRate(vesselData.connection.maxDistanceRate)));
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

		// data objects shortcuts
		private static VesselDataShip vesselData;
		private static VesselResHandler resHandler;

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
		private static bool updateRequested = false;
		private static int update_counter = 0;
#endregion
	}


} // KERBALISM
