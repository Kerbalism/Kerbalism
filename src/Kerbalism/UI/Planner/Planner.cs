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

			// Compute sorted body indices
			ComputeSortedBodyIndices();

			// set default body index to home
			body_index = FlightGlobals.GetHomeBodyIndex();
			ApplyBodyOrbitDefaults(FlightGlobals.Bodies[body_index]);

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

		///<summary>Constructed a list of CB indices that is sorted (hierarchically) by SMA</summary>
		private static void ComputeSortedBodyIndices()
		{
			void SortBodiesAndAppendIndicesToList(List<CelestialBody> bodies)
			{
				bodies.Sort((a, b) => a.orbit.semiMajorAxis.CompareTo(b.orbit.semiMajorAxis));
				foreach (var body in bodies)
				{
					sorted_body_indices.Add(body.flightGlobalsIndex);
					if (body.orbitingBodies.Count > 0)
					{
						SortBodiesAndAppendIndicesToList(new List<CelestialBody>(body.orbitingBodies));
					}
				}
			}
			SortBodiesAndAppendIndicesToList(new List<CelestialBody>(Planetarium.fetch.Sun.orbitingBodies));
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
			// Use the cached editor manifest. CrewAssignmentDialog.GetManifest() is extremely
			// expensive since KSP 1.11 and was forcing PAW rebuilds every frame while Planner is open.
			VesselCrewManifest manifest = Lib.EditorShipManifest;
			if (manifest == null)
				return;

			// Check both total crew and occupied parts. Moving a Kerbal between cabins
			// keeps CrewCount unchanged but can change the spin estimate substantially.
			// Throttle the occupied-part hash and use the editor's existing list to avoid
			// allocating a recursive part list every UI frame.
			if (vessel_analyzer.crew_count != manifest.CrewCount)
			{
				enforceUpdate = true;
				crew_assignment_check_counter = 0;
			}
			else if (++crew_assignment_check_counter >= 5)
			{
				crew_assignment_check_counter = 0;
				List<Part> editorParts = EditorLogic.fetch != null && EditorLogic.fetch.ship != null
					? EditorLogic.fetch.ship.parts
					: null;
				if (editorParts != null
					&& vessel_analyzer.crew_assignment_hash != SpinComfort.EditorCrewAssignmentHash(editorParts, manifest))
					enforceUpdate = true;
			}

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
				env_analyzer.Analyze(FlightGlobals.Bodies[body_index], orbital_altitude_m, sunlight);
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
				GUILayout.Label(new GUIContent(Lib.BodyDisplayName(FlightGlobals.Bodies[body_index]), Local.Planner_Targetbody), leftmenu_style);//"Target body"
				if (Lib.IsClicked())
				{
					CycleBody(+1);
					enforceUpdate = true;
				}
				else if (Lib.IsClicked(1))
				{
					CycleBody(-1);
					enforceUpdate = true;
				}

				// sunlight selector
				switch (sunlight)
				{
					case SunlightState.SunlightNominal: GUILayout.Label(new GUIContent(Textures.sun_white, Local.Planner_SunlightNominal), icon_style); break;//"In sunlight\n<b>Nominal</b> solar panel output"
					case SunlightState.SunlightSimulated: GUILayout.Label(new GUIContent(Textures.solar_panel, Local.Planner_SunlightSimulated), icon_style); break;//"In sunlight\n<b>Estimated</b> solar panel output\n<i>Sunlight direction : look at the shadows !</i>"
					case SunlightState.Shadow: GUILayout.Label(new GUIContent(Textures.sun_black, Local.Planner_Shadow), icon_style); break;//"In shadow"
				}
				if (Lib.IsClicked())
				{ sunlight = (SunlightState)(((int)sunlight + 1) % Enum.GetValues(typeof(SunlightState)).Length); enforceUpdate = true; }

				// situation selector
				GUILayout.Label(new GUIContent(SituationLabel(situation_index), Local.Planner_Targetsituation), rightmenu_style);//"Target situation"
				if (Lib.IsClicked())
				{ CycleSituation(+1); enforceUpdate = true; }
				else if (Lib.IsClicked(1))
				{ CycleSituation(-1); enforceUpdate = true; }

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

		///<summary> Add environment sub-panel, including tooltips </summary>
		private static void AddSubPanelEnvironment(Panel p)
		{
			string flux_tooltip = Lib.BuildString
			(
				"<align=left />" +
				String.Format("<b>{0,-14}\t{1,-15}\t{2}</b>\n", Local.Planner_Source, Local.Planner_Flux, Local.Planner_Temp),//"Source""Flux""Temp"
				String.Format("{0,-14}\t{1,-15}\t{2}\n", Local.Planner_solar, env_analyzer.solar_flux > 0.0 ? Lib.HumanReadableFlux(env_analyzer.solar_flux) : Local.Generic_NONE, Lib.HumanReadableTemp(Sim.BlackBodyTemperature(env_analyzer.solar_flux))),//"solar""none"
				String.Format("{0,-14}\t{1,-15}\t{2}\n", Local.Planner_albedo, env_analyzer.albedo_flux > 0.0 ? Lib.HumanReadableFlux(env_analyzer.albedo_flux) : Local.Generic_NONE, Lib.HumanReadableTemp(Sim.BlackBodyTemperature(env_analyzer.albedo_flux))),//"albedo""none"
				String.Format("{0,-14}\t{1,-15}\t{2}\n", Local.Planner_body, env_analyzer.body_flux > 0.0 ? Lib.HumanReadableFlux(env_analyzer.body_flux) : Local.Generic_NONE, Lib.HumanReadableTemp(Sim.BlackBodyTemperature(env_analyzer.body_flux))),//"body""none"
				String.Format("{0,-14}\t{1,-15}\t{2}\n", Local.Planner_background, Lib.HumanReadableFlux(Sim.BackgroundFlux()), Lib.HumanReadableTemp(Sim.BlackBodyTemperature(Sim.BackgroundFlux()))),//"background"
				String.Format("{0,-14}\t\t{1,-15}\t{2}", Local.Planner_total, Lib.HumanReadableFlux(env_analyzer.total_flux), Lib.HumanReadableTemp(Sim.BlackBodyTemperature(env_analyzer.total_flux)))//"total"
			);
			string atmosphere_tooltip = Lib.BuildString
			(
				"<align=left />",
				String.Format("{0,-14}\t<b>{1}</b>\n", Local.BodyInfo_breathable, Sim.Breathable(env_analyzer.body) ? Local.BodyInfo_breathable_yes : Local.BodyInfo_breathable_no),//"breathable""yes""no"
				String.Format("{0,-14}\t<b>{1}</b>\n", Local.Planner_pressure, Lib.HumanReadablePressure(env_analyzer.body.atmospherePressureSeaLevel)),//"pressure"
				String.Format("{0,-14}\t<b>{1}</b>\n", Local.BodyInfo_lightabsorption, Lib.HumanReadablePerc(1.0 - env_analyzer.atmo_factor)),//"light absorption"
				String.Format("{0,-14}\t<b>{1}</b>", Local.BodyInfo_gammaabsorption, Lib.HumanReadablePerc(1.0 - Sim.GammaTransparency(env_analyzer.body, 0.0)))//"gamma absorption"
			);
			string shadowtime_str = Lib.HumanReadableDuration(env_analyzer.shadow_period) + " (" + (env_analyzer.shadow_time * 100.0).ToString("F0") + "%)";

			p.AddSection(Local.TELEMETRY_ENVIRONMENT, string.Empty,//"ENVIRONMENT"
				() => { p.Prev(ref environment_index, panel_environment.Count); enforceUpdate = true; },
				() => { p.Next(ref environment_index, panel_environment.Count); enforceUpdate = true; });
			p.AddContent(Local.Planner_temperature, Lib.HumanReadableTemp(env_analyzer.temperature), env_analyzer.body.atmosphere && env_analyzer.landed ? Local.Planner_atmospheric : flux_tooltip);//"temperature""atmospheric"
			p.AddContent(Local.Planner_difference, Lib.HumanReadableTemp(env_analyzer.temp_diff), Local.Planner_difference_desc);//"difference""difference between external and survival temperature"
			p.AddContent(Local.Planner_atmosphere, env_analyzer.body.atmosphere ? Local.Planner_atmosphere_yes : Local.Planner_atmosphere_no, atmosphere_tooltip);//"atmosphere""yes""no"
			p.AddContent(Local.Planner_shadowtime, shadowtime_str, Local.Planner_shadowtime_desc);//"shadow time"
		}

		///<summary> Add electric charge sub-panel, including tooltips </summary>
		private static void AddSubPanelEC(Panel p)
		{
			// get simulated resource
			SimulatedResource res = resource_sim.Resource("ElectricCharge");

			// create tooltip
			string tooltip = res.Tooltip();
			double charge_time = res.ChargeTime();
			string charge_str = double.IsNaN(charge_time) ? Local.Generic_NONE : Lib.HumanReadableDuration(charge_time);

			// render the panel section
			p.AddSection(Local.Planner_ELECTRICCHARGE);//"ELECTRIC CHARGE"
			p.AddContent(Local.Planner_storage, Lib.HumanOrSIAmount(res.storage, Lib.ECResID), tooltip);//"storage"
			p.AddContent(Local.Planner_consumed, Lib.HumanOrSIRate(res.consumed, Lib.ECResID), tooltip);//"consumed"
			p.AddContent(Local.Planner_produced, Lib.HumanOrSIRate(res.produced, Lib.ECResID), tooltip);//"produced"
			p.AddContent(Local.Planner_duration, Lib.HumanReadableDuration(res.Lifetime()));//"duration"
			p.AddContent(Local.Planner_fullcharge, charge_str, Local.Planner_fullcharge_desc);//"full charge"

			CelestialBody body = FlightGlobals.Bodies[body_index];
			if (!body.isStar)
			{
				string orbit_str = (orbital_altitude_m / 1000.0).ToString("F0") + " km";
				p.AddContent(Local.Planner_vesselorbitalt, orbit_str, Local.Planner_vesselorbitalt_desc,
					() => { StepOrbitalAltitude(+1); enforceUpdate = true; },
					null,
					() => { StepOrbitalAltitude(-1); enforceUpdate = true; });
			}
		}

		private static bool ShiftHeld()
		{
			return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
		}

		private static void CycleBody(int direction)
		{
			int count = sorted_body_indices.Count;
			if (count <= 0)
				return;

			int sorted_index = sorted_body_indices.IndexOf(body_index);
			if (sorted_index < 0)
				sorted_index = 0;
			sorted_index = ((sorted_index + direction) % count + count) % count;
			body_index = sorted_body_indices[sorted_index];
			ApplyBodyOrbitDefaults(FlightGlobals.Bodies[body_index]);
		}

		private static string SituationLabel(int index)
		{
			if (index == custom_situation_index)
				return Local.Planner_Custom;
			if (index >= 0 && index < preset_situations.Length)
				return preset_situations[index];
			return Local.Planner_Custom;
		}

		private static void CycleSituation(int direction)
		{
			// Cycle only among altitude presets; Custom is entered by editing orbital altitude
			int preset_count = altitude_mults.Length;
			if (situation_index >= preset_count)
				situation_index = direction > 0 ? 0 : preset_count - 1;
			else
				situation_index = ((situation_index + direction) % preset_count + preset_count) % preset_count;
			ApplySituationAltitudePreset();
		}

		private static double PresetAltitude(CelestialBody body, int preset_index)
		{
			double alt = body.Radius * altitude_mults[preset_index];
			if (!body.isStar && body.sphereOfInfluence > 0.0)
				alt = Math.Min(alt, Math.Max(0.0, body.sphereOfInfluence - body.Radius));
			return alt;
		}

		private static void ApplySituationAltitudePreset()
		{
			if (situation_index < 0 || situation_index >= altitude_mults.Length)
				return;
			orbital_altitude_m = PresetAltitude(FlightGlobals.Bodies[body_index], situation_index);
		}

		private static void SyncSituationIndexFromAltitude()
		{
			CelestialBody body = FlightGlobals.Bodies[body_index];
			const double tolerance_m = 1.0;
			for (int i = 0; i < altitude_mults.Length; ++i)
			{
				if (Math.Abs(orbital_altitude_m - PresetAltitude(body, i)) <= tolerance_m)
				{
					situation_index = i;
					return;
				}
			}
			situation_index = custom_situation_index;
		}

		private static void ApplyBodyOrbitDefaults(CelestialBody body)
		{
			if (body.isStar)
			{
				orbital_altitude_m = 0.0;
				situation_index = 0;
				return;
			}

			double default_alt = body.atmosphereDepth + 10000.0;
			double max_alt = Math.Max(0.0, body.sphereOfInfluence - body.Radius);
			orbital_altitude_m = Lib.Clamp(default_alt, 0.0, max_alt);
			SyncSituationIndexFromAltitude();
		}

		private static void StepOrbitalAltitude(int direction)
		{
			CelestialBody body = FlightGlobals.Bodies[body_index];
			if (body.isStar)
				return;

			double step_m = (ShiftHeld() ? 50.0 : 5.0) * 1000.0; // km
			double max_alt = Math.Max(0.0, body.sphereOfInfluence - body.Radius);
			orbital_altitude_m = Lib.Clamp(orbital_altitude_m + direction * step_m, 0.0, max_alt);
			SyncSituationIndexFromAltitude();
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
			p.AddContent(Local.Planner_storage, Lib.HumanOrSIAmount(res.storage, resource.id), tooltip);//"storage"
			p.AddContent(Local.Planner_consumed, Lib.HumanOrSIRate(res.consumed, resource.id), tooltip);//"consumed"
			p.AddContent(Local.Planner_produced, Lib.HumanOrSIRate(res.produced, resource.id), tooltip);//"produced"
			p.AddContent(Local.Planner_duration, Lib.HumanReadableDuration(res.Lifetime()));//"duration"
		}

		///<summary> Add stress sub-panel, including tooltips </summary>
		private static void AddSubPanelStress(Panel p)
		{
			// get first living space rule
			// - guaranteed to exist, as this panel is not rendered if it doesn't
			// - even without crew, it is safe to evaluate the modifiers that use it
			Rule rule = Profile.rules.Find(k => k.modifiers.Contains("living_space"));

			// render title
			p.AddSection(Local.Planner_STRESS, string.Empty,//"STRESS"
				() => { p.Prev(ref special_index, panel_special.Count); enforceUpdate = true; },
				() => { p.Next(ref special_index, panel_special.Count); enforceUpdate = true; });

			// render living space data
			// generate details tooltips
			string living_space_tooltip = Lib.BuildString
			(
				Local.Planner_volumepercapita ,"<b>\t", Lib.HumanReadableVolume(vessel_analyzer.volume / Math.Max(vessel_analyzer.crew_count, 1)), "</b>\n",//"volume per-capita:
				Local.Planner_ideallivingspace ,"<b>\t", Lib.HumanReadableVolume(PreferencesComfort.Instance.livingSpace), "</b>"//"ideal living space:
			);
			p.AddContent(Local.Planner_livingspace, Lib.HumanReadableLivingSpace(vessel_analyzer.living_space), living_space_tooltip);//"living space"

			// render comfort data
			if (rule.modifiers.Contains("comfort"))
			{
				// The planner renders its design estimate on a dedicated row below, so hide
				// the flight-only persisted spin snapshot from the generic comfort tooltip.
				p.AddContent(Local.Planner_comfort, vessel_analyzer.comforts.Summary(), vessel_analyzer.comforts.Tooltip(false));//"comfort"
				AddSpinEstimateContent(p);
			}
			else
			{
				p.AddContent(Local.Planner_comfort, Local.Generic_notapplicable);//"comfort"
			}

			// render pressure data
			if (rule.modifiers.Contains("pressure"))
			{
				string pressure_tooltip = vessel_analyzer.pressurized
				  ? Local.Planner_analyzerpressurized1//"Free roaming in a pressurized environment is\nvastly superior to living in a suit."
				  : Local.Planner_analyzerpressurized2;//"Being forced inside a suit all the time greatly\nreduces the crews quality of life.\nThe worst part is the diaper."
				p.AddContent(Local.Planner_pressurized, vessel_analyzer.pressurized ? Local.Planner_pressurized_yes : Local.Planner_pressurized_no, pressure_tooltip);//"pressurized""yes""no"
			}
			else
			{
				p.AddContent(Local.Planner_pressurized, Local.Generic_notapplicable);//"pressurized"
			}

			// render life estimate
			double mod = Modifiers.Evaluate(env_analyzer, vessel_analyzer, resource_sim, rule.modifiers);
			p.AddContent(Local.Planner_lifeestimate, Lib.HumanReadableDuration(rule.fatal_threshold / (rule.degeneration * mod)));//"duration"
		}

		///<summary> Show whether the editor ship can meet spin firm-ground thresholds at max RPM.</summary>
		private static void AddSpinEstimateContent(Panel p)
		{
			if (!PreferencesComfort.Instance.spinFirmGround)
				return;

			SpinComfort.EditorEstimate spin = vessel_analyzer.spinEstimate;
			string value;
			string tooltip;

			if (!spin.available)
			{
				value = Local.Comfort_spin_na;
				tooltip = Local.Planner_spin_unavailable;
			}
			else if (spin.crewPartCount == 0)
			{
				value = Local.Comfort_spin_na;
				tooltip = Local.Planner_spin_nocrewparts;
			}
			else
			{
				string yes = Lib.BuildString("<b><color=#00ff00>", Local.Generic_YES, "</color></b>");
				string no = Lib.BuildString("<b><color=#ffaa00>", Local.Generic_NO, "</color></b>");
				value = spin.qualifies ? yes : no;
				tooltip = Lib.BuildString
				(
					"<align=left />",
					Local.Planner_spin_tip_intro, "\n",
					Local.Planner_spin_worst_radius, "\t<b>", spin.worstRadius.ToString("F1"), " m</b>\n",
					Local.Planner_spin_gee_at_max, "\t<b>", spin.geeAtMaxRpm.ToString("F2"), " g</b>\n",
					Local.Planner_spin_rpm_needed, "\t<b>",
					double.IsInfinity(spin.rpmRequired) ? "∞" : spin.rpmRequired.ToString("F2"),
					" rpm</b>\n",
					Local.Planner_spin_thresholds, "\t<b>",
					spin.requiredGee.ToString("F2"), " g / ≤ ", spin.maxRpm.ToString("F1"), " rpm</b>"
				);
			}

			p.AddContent(Local.Comfort_spin, value, tooltip);
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
				() => { p.Prev(ref special_index, panel_special.Count); enforceUpdate = true; },
				() => { p.Next(ref special_index, panel_special.Count); enforceUpdate = true; });
			p.AddContent(Local.Planner_surface, Lib.HumanReadableRadiation(env_analyzer.surface_rad + vessel_analyzer.emitted), tooltip);//"surface"
			p.AddContent(Local.Planner_orbit, Lib.HumanReadableRadiation(env_analyzer.magnetopause_rad), tooltip);//"orbit"
			if (vessel_analyzer.emitted >= 0.0)
				p.AddContent(Local.Planner_emission, Lib.HumanReadableRadiation(vessel_analyzer.emitted), tooltip);//"emission"
			else
				p.AddContent(Local.Planner_activeshielding, Lib.HumanReadableRadiation(-vessel_analyzer.emitted), tooltip);//"active shielding"
			p.AddContent(Local.Planner_shielding, rule.modifiers.Contains("shielding") ? Lib.HumanReadableShieldingLevel(vessel_analyzer.shielding) : "N/A", tooltip);//"shielding"
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
				redundancy_str = Local.Planner_none;//"none"
			else if (redundancy_metric <= 0.33)
				redundancy_str = Local.Planner_poor;//"poor"
			else if (redundancy_metric <= 0.66)
				redundancy_str = Local.Planner_okay;//"okay"
			else
				redundancy_str = Local.Planner_great;//"great"

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
					sb.Append(Reliability.LocalizeRedundancyGroup(pair.Key));
				}
				redundancy_tooltip = Lib.BuildString("<align=left />", sb.ToString());
			}

			// generate repair string and tooltip
			string repair_str = Local.Planner_none;//"none"
			string repair_tooltip = string.Empty;
			if (vessel_analyzer.crew_engineer)
			{
				repair_str = Local.Trait_Engineer;
				repair_tooltip = Local.Planner_engineer_tip;//"The engineer on board should\nbe able to handle all repairs"
			}
			else if (vessel_analyzer.crew_capacity == 0)
			{
				repair_str = Local.Planner_repair_safemode;
				repair_tooltip = Local.Planner_safemode_tip;//"We have a chance of repairing\nsome of the malfunctions remotely"
			}

			// render panel
			p.AddSection(Local.Planner_RELIABILITY, string.Empty,//"RELIABILITY"
				() => { p.Prev(ref special_index, panel_special.Count); enforceUpdate = true; },
				() => { p.Next(ref special_index, panel_special.Count); enforceUpdate = true; });
			p.AddContent(Local.Planner_malfunctions, Lib.HumanReadableAmount(vessel_analyzer.failure_year, "/y"), Local.Planner_malfunctions_tip);//"malfunctions""average case estimate\nfor the whole vessel"
			p.AddContent(Local.Planner_highquality, Lib.HumanReadablePerc(vessel_analyzer.high_quality), Local.Planner_highquality_tip);//"high quality""percentage of high quality components"
			p.AddContent(Local.Planner_redundancy, redundancy_str, redundancy_tooltip);//"redundancy"
			p.AddContent(Local.Planner_repair, repair_str, repair_tooltip);//"repair"
		}

		///<summary> Add habitat sub-panel, including tooltips </summary>
		private static void AddSubPanelHabitat(Panel p)
		{
			SimulatedResource atmo_res = resource_sim.Resource(Habitat.AtmoResName);
			SimulatedResource waste_res = resource_sim.Resource(Habitat.WasteAtmoResName);

			// generate tooltips
			string atmo_tooltip = atmo_res.Tooltip();
			string waste_tooltip = waste_res.Tooltip(true);

			// generate status string for scrubbing
			string waste_status = !Features.Poisoning                   //< feature disabled
			  ? Local.Generic_notapplicable
			  : waste_res.produced <= double.Epsilon                    //< unnecessary
			  ? Local.Planner_scrubbingunnecessary//"not required"
			  : waste_res.consumed <= double.Epsilon                    //< no scrubbing
			  ? Lib.Color(Local.Planner_noscrubbing, Lib.Kolor.Orange)//"none"
			  : waste_res.produced > waste_res.consumed * 1.001         //< insufficient scrubbing
			  ? Lib.Color(Local.Planner_insufficientscrubbing, Lib.Kolor.Yellow)//"inadequate"
			  : Lib.Color(Local.Planner_sufficientscrubbing, Lib.Kolor.Green);//"good"                    //< sufficient scrubbing

			// generate status string for pressurization
			string atmo_status = !Features.Pressure                     //< feature disabled
			  ? Local.Generic_notapplicable
			  : atmo_res.consumed <= double.Epsilon                     //< unnecessary
			  ? Local.Planner_pressurizationunnecessary//"not required"
			  : atmo_res.produced <= double.Epsilon                     //< no pressure control
			  ? Lib.Color(Local.Planner_nopressurecontrol, Lib.Kolor.Orange)//"none"
			  : atmo_res.consumed > atmo_res.produced * 1.001           //< insufficient pressure control
			  ? Lib.Color(Local.Planner_insufficientpressurecontrol, Lib.Kolor.Yellow)//"inadequate"
			  : Lib.Color(Local.Planner_sufficientpressurecontrol, Lib.Kolor.Green);//"good"                    //< sufficient pressure control

			p.AddSection(Local.Planner_HABITAT, string.Empty,//"HABITAT"
				() => { p.Prev(ref environment_index, panel_environment.Count); enforceUpdate = true; },
				() => { p.Next(ref environment_index, panel_environment.Count); enforceUpdate = true; });
			p.AddContent(Local.Planner_volume, Lib.HumanReadableVolume(vessel_analyzer.volume), Local.Planner_volume_tip);//"volume""volume of enabled habitats"
			p.AddContent(Local.Planner_habitatssurface, Lib.HumanReadableSurface(vessel_analyzer.surface), Local.Planner_habitatssurface_tip);//"surface""surface of enabled habitats"
			p.AddContent(Local.Planner_scrubbing, waste_status, waste_tooltip);//"scrubbing"
			p.AddContent(Local.Planner_pressurization, atmo_status, atmo_tooltip);//"pressurization"
		}
#endregion

#region FIELDS_PROPERTIES
		// altitude presets; Custom is used when orbital altitude does not match any preset
		private static readonly string[] preset_situations = { "Landed", "Low Orbit", "Orbit", "High Orbit" };
		private static readonly double[] altitude_mults = { 0.0, 0.33, 1.0, 3.0 };
		private const int custom_situation_index = 4;

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
		private static List<int> sorted_body_indices = new List<int>();
		private static int situation_index = 2;     // orbit
		private static double orbital_altitude_m;           // meters above body
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
		private static int crew_assignment_check_counter = 0;
#endregion
	}


} // KERBALISM
