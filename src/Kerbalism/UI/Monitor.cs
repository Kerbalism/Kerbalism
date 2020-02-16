using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;

namespace KERBALISM
{
	public enum MonitorPage
	{
		telemetry,
		data,
		scripts,
		failures,
		config,
		log
	}

	public sealed class Monitor
	{
		// ctor
		public Monitor()
		{
			// filter style
			filter_style = new GUIStyle(HighLogic.Skin.label);
			filter_style.normal.textColor = new Color(0.66f, 0.66f, 0.66f, 1.0f);
			filter_style.stretchWidth = true;
			filter_style.fontSize = Styles.ScaleInteger(12);
			filter_style.alignment = TextAnchor.MiddleLeft;
			filter_style.fixedHeight = Styles.ScaleFloat(16.0f);
			filter_style.border = new RectOffset(0, 0, 0, 0);

			// vessel config style
			config_style = new GUIStyle(HighLogic.Skin.label);
			config_style.normal.textColor = Color.white;
			config_style.padding = new RectOffset(0, 0, 0, 0);
			config_style.alignment = TextAnchor.MiddleLeft;
			config_style.imagePosition = ImagePosition.ImageLeft;
			config_style.fontSize = Styles.ScaleInteger(9);

			// initialize panel
			panel = new Panel();

			// by default don't show hidden vessels
			filter_types.Add(VesselType.Unknown);

			// auto-switch selected vessel on scene changes
			GameEvents.onVesselChange.Add((Vessel v) => { if (selected_id != Guid.Empty) selected_id = v.id; });
		}

		public void Update()
		{
			// reset panel
			panel.Clear();
			
			if (Lib.IsDevBuild) panel.AddHeader(Lib.Color("KERBALISM DEV BUILD " + Lib.KerbalismDevBuild, Lib.Kolor.Orange));

			// get vessel
			selected_v = selected_id == Guid.Empty ? null : FlightGlobals.FindVessel(selected_id);

			// if nothing is selected, or if the selected vessel doesn't exist
			// anymore, or if it has become invalid for whatever reason
			if (selected_v == null || !selected_v.KerbalismIsValid())
			{
				// forget the selected vessel, if any
				selected_id = Guid.Empty;

				// used to detect when no vessels are in list
				bool setup = false;

				// draw active vessel if any
				if (FlightGlobals.ActiveVessel != null)
				{
					setup |= Render_vessel(panel, FlightGlobals.ActiveVessel);
				}

				// for each vessel
				foreach (Vessel v in FlightGlobals.Vessels)
				{
					// skip active vessel
					if (v == FlightGlobals.ActiveVessel) continue;

					// draw the vessel
					setup |= Render_vessel(panel, v);
				}

				// empty vessel case
				if (!setup)
				{
					panel.AddHeader("<i>"+Local.Monitor_novessels +"</i>");//no vessels
				}
			}
			// if a vessel is selected
			else
			{
				// header act as title
				Render_vessel(panel, selected_v, true);

				// update page content
				switch (page)
				{
					case MonitorPage.telemetry: panel.TelemetryPanel(selected_v); break;
					case MonitorPage.data: panel.Fileman(selected_v, true); break;  // Using short_strings parameter to stop overlapping when inflight.
					case MonitorPage.scripts: panel.Devman(selected_v); break;
					case MonitorPage.config: panel.Config(selected_v); break;
					case MonitorPage.log: panel.Logman(selected_v); break;
					case MonitorPage.failures: panel.Failman(selected_v); break;
				}
			}
		}

		public void Render()
		{
			// in flight / map view, put the menu on top
			if (HighLogic.LoadedSceneIsFlight)
			{
				// vessel filter or vessel menu if a vessel is selected
				if (selected_v != null) Render_menu(selected_v);
				else Render_filter();
			}

			// start scrolling view
			scroll_pos = GUILayout.BeginScrollView(scroll_pos, HighLogic.Skin.horizontalScrollbar, HighLogic.Skin.verticalScrollbar);

			// render panel content
			panel.Render();

			// end scroll view
			GUILayout.EndScrollView();

			// in planetarium / space center, put the menu at bottom
			if (!HighLogic.LoadedSceneIsFlight)
			{
				// vessel filter or vessel menu if a vessel is selected
				if (selected_v != null) Render_menu(selected_v);
				else Render_filter();
			}

			// right click goes back to list view
			if (Event.current.type == EventType.MouseDown
			 && Event.current.button == 1)
			{
				selected_id = Guid.Empty;
			}
		}

		public float Width()
		{
			//if ((page == MonitorPage.data || page == MonitorPage.log || selected_id == Guid.Empty) && !Lib.IsFlight())
			//	return Styles.ScaleWidthFloat(465.0f);
			//return Styles.ScaleWidthFloat(355.0f);
			return Styles.ScaleWidthFloat(370.0f);
			//return Styles.ScaleWidthFloat(405.0f);
		}

		public float Height()
		{
			// top spacing
			float h = Styles.ScaleFloat(36.0f);

			// panel height
			h += panel.Height();

			// clamp to screen height
			return Math.Min(h, Screen.height * 0.75f);
		}

		bool Filter_match(Vessel vessel, string tags)
		{
			// if vessels type is hidden, do not include vessel
			if(filter_types.Contains(vessel.vesselType)) return false;

			// if hidden vessels aren't visible, see if this vessel is hidden
			if(filter_types.Contains(VesselType.Unknown))
			{
				if (!vessel.KerbalismData().cfg_show) return false;
			}

			if(filter.Length <= 0 || filter == filter_placeholder) return true;

			List<string> filterTags = Lib.Tokenize(filter.ToLower(), ' ');
			List<string> vesselTags = Lib.Tokenize(tags.ToLower(), ' ');

			foreach (string tag in filterTags)
			{
				foreach(string vesselTag in vesselTags)
				{
					if(vesselTag.StartsWith(tag, StringComparison.CurrentCulture))
						return true;
				}
			}
			return false;
		}

		bool Render_vessel(Panel p, Vessel v, bool selected = false)
		{
			// get vessel info
			VesselData vd = v.KerbalismData();

			// skip invalid vessels
			if (!vd.IsSimulated) return false;

			// get vessel crew
			List<ProtoCrewMember> crew = Lib.CrewList(v);

			// get vessel name
			string vessel_name = v.isEVA ? crew[0].name : v.vesselName;

			// get body name
			string body_name = v.mainBody.name.ToUpper();

			// skip filtered vessels
			if (!selected && !Filter_match(v, body_name + " " + vessel_name)) return false;

			// render entry
			p.AddHeader
			(
			  Lib.BuildString("<b>",
			  Lib.Ellipsis(vessel_name, Styles.ScaleStringLength(((page == MonitorPage.data || page == MonitorPage.log || selected_id == Guid.Empty) && !Lib.IsFlight()) ? 45 : 25)),
			  "</b> <size=", Styles.ScaleInteger(9).ToString(), ">", Lib.Color(Lib.Ellipsis(body_name, Styles.ScaleStringLength(8)), Lib.Kolor.LightGrey), "</size>"),
			  string.Empty,
			  () => { selected_id = selected_id != v.id ? v.id : Guid.Empty; }
			);

			// vessel type icon
			if (!selected)
			p.SetLeftIcon(GetVesselTypeIcon(v.vesselType), v.vesselType.displayDescription(), () => { selected_id = selected_id != v.id ? v.id : Guid.Empty; });
			else
			{
				if (FlightGlobals.ActiveVessel != v)
				{
					if (Lib.IsFlight())
					{
						p.SetLeftIcon(GetVesselTypeIcon(v.vesselType), Local.Monitor_Gotovessel, () => Lib.Popup//"Go to vessel!"
						(Local.Monitor_Warning_title,//"Warning!"
							Lib.BuildString(Local.Monitor_GoComfirm.Format(vessel_name)),//"Do you really want go to ", , " vessel?"
							new DialogGUIButton(Local.Monitor_GoComfirm_button1, () => { GotoVessel.JumpToVessel(v); }),//"Go"
							new DialogGUIButton(Local.Monitor_GoComfirm_button2, () => { GotoVessel.SetVesselAsTarget(v); }),//"Target"
							new DialogGUIButton(Local.Monitor_GoComfirm_button3, () => { })));//"Stay"
					}
					else
					{
						p.SetLeftIcon(GetVesselTypeIcon(v.vesselType), Local.Monitor_Gotovessel, () => Lib.Popup//"Go to vessel!"
						(Local.Monitor_Warning_title,//"Warning!"
							Lib.BuildString(Local.Monitor_GoComfirm.Format(vessel_name)),//"Do you really want go to ", , " vessel?"
							new DialogGUIButton(Local.Monitor_GoComfirm_button1, () => { GotoVessel.JumpToVessel(v); }),//"Go"
							new DialogGUIButton(Local.Monitor_GoComfirm_button3, () => { })));//"Stay"
					}
				}
				else
				{
					p.SetLeftIcon(GetVesselTypeIcon(v.vesselType), v.vesselType.displayDescription(), () => { });
				}
			}

			// problem indicator
			Indicator_problems(p, v, vd, crew);

			// battery indicator
			Indicator_ec(p, v, vd);

			// supply indicator
			if (Features.Supplies) Indicator_supplies(p, v, vd);

			// reliability indicator
			if (Features.Reliability) Indicator_reliability(p, v, vd);

			// signal indicator
			if (API.Comm.handlers.Count > 0 || HighLogic.fetch.currentGame.Parameters.Difficulty.EnableCommNet) Indicator_signal(p, v, vd);

			// done
			return true;
		}

		void Render_menu(Vessel v)
		{
			VesselData vd = v.KerbalismData();
			GUILayout.BeginHorizontal(Styles.entry_container);
			GUILayout.Label(new GUIContent(Lib.Color(page == MonitorPage.telemetry, " " + Local.Monitor_INFO, Lib.Kolor.Green, Lib.Kolor.None, true), Textures.small_info, Local.Monitor_INFO_desc + Local.Monitor_tooltip), config_style);//INFO"Telemetry readings"
			if (Lib.IsClicked()) page = MonitorPage.telemetry;
			else if (Lib.IsClicked(2))
			{
				if (UI.window.PanelType == Panel.PanelType.telemetry)
					UI.window.Close();
				else
					UI.Open((p) => p.TelemetryPanel(v));
			}
			if (Features.Science)
			{
				GUILayout.Label(new GUIContent(Lib.Color(page == MonitorPage.data, " " + Local.Monitor_DATA, Lib.Kolor.Green, Lib.Kolor.None, true), Textures.small_folder, Local.Monitor_DATA_desc + Local.Monitor_tooltip), config_style);//DATA"Stored files and samples"
				if (Lib.IsClicked()) page = MonitorPage.data;
				else if (Lib.IsClicked(2))
				{
					if (UI.window.PanelType == Panel.PanelType.data)
						UI.window.Close();
					else
						UI.Open((p) => p.Fileman(v));
				}
			}
			if (Features.Automation)
			{
				GUILayout.Label(new GUIContent(Lib.Color(page == MonitorPage.scripts, " " + Local.Monitor_AUTO, Lib.Kolor.Green, Lib.Kolor.None, true), Textures.small_console, Local.Monitor_AUTO_desc + Local.Monitor_tooltip), config_style);//AUTO"Control and automate components"
				if (Lib.IsClicked()) page = MonitorPage.scripts;
				else if (Lib.IsClicked(2))
				{
					if (UI.window.PanelType == Panel.PanelType.scripts)
						UI.window.Close();
					else
						UI.Open((p) => p.Devman(v));
				}
			}
			if (Features.Reliability)
			{
				GUILayout.Label(new GUIContent(Lib.Color(page == MonitorPage.failures, " " + Local.Monitor_FAILURES, Lib.Kolor.Green, Lib.Kolor.None, true), Textures.small_wrench, Local.Monitor_FAILURES_desc + Local.Monitor_tooltip), config_style);//FAILURES"See failures and maintenance state"
				if (Lib.IsClicked()) page = MonitorPage.failures;
				else if (Lib.IsClicked(2))
				{
					if (UI.window.PanelType == Panel.PanelType.failures)
						UI.window.Close();
					else
						UI.Open((p) => p.Failman(v));
				}
			}
			if (PreferencesMessages.Instance.stockMessages != true)
			{
				GUILayout.Label(new GUIContent(Lib.Color(page == MonitorPage.log, " " + Local.Monitor_LOG, Lib.Kolor.Green, Lib.Kolor.None, true), Textures.small_notes, Local.Monitor_LOG_desc + Local.Monitor_tooltip), config_style);//LOG"See previous notifications"
				if (Lib.IsClicked()) page = MonitorPage.log;
				else if (Lib.IsClicked(2))
				{
					if (UI.window.PanelType == Panel.PanelType.log)
						UI.window.Close();
					else
						UI.Open((p) => p.Logman(v));
				}
			}
			GUILayout.Label(new GUIContent(Lib.Color(page == MonitorPage.config, " " + Local.Monitor_CFG, Lib.Kolor.Green, Lib.Kolor.None, true), Textures.small_config, Local.Monitor_CFG_desc + Local.Monitor_tooltip), config_style);//CFG"Configure the vessel"
			if (Lib.IsClicked()) page = MonitorPage.config;
			else if (Lib.IsClicked(2))
			{
				if (UI.window.PanelType == Panel.PanelType.config)
					UI.window.Close();
				else
					UI.Open((p) => p.Config(v));
			}
			GUILayout.EndHorizontal();
			GUILayout.Space(Styles.ScaleFloat(10.0f));
		}

		void Render_filter()
		{
			// show the group filter
			GUILayout.BeginHorizontal(Styles.entry_container);

			Render_TypeFilterButon(VesselType.Probe);
			Render_TypeFilterButon(VesselType.Rover);
			Render_TypeFilterButon(VesselType.Lander);
			Render_TypeFilterButon(VesselType.Ship);
			Render_TypeFilterButon(VesselType.Station);
			Render_TypeFilterButon(VesselType.Base);
			Render_TypeFilterButon(VesselType.Plane);
			Render_TypeFilterButon(VesselType.Relay);
			Render_TypeFilterButon(VesselType.EVA);

#if !KSP15_16
			if (Kerbalism.SerenityEnabled) Render_TypeFilterButon(VesselType.DeployedScienceController);
#endif

			// we abuse the type Unknown to show/hide vessels that have the hidden toggle set to on
			Render_TypeFilterButon(VesselType.Unknown);

			filter = Lib.TextFieldPlaceholder("Kerbalism_filter", filter, filter_placeholder, filter_style).ToUpper();
			GUILayout.EndHorizontal();
			GUILayout.Space(Styles.ScaleFloat(10.0f));
		}

		void Render_TypeFilterButon(VesselType type)
		{
			bool isFiltered = filter_types.Contains(type);
			GUILayout.Label(new GUIContent(" ", GetVesselTypeIcon(type, isFiltered),
				type == VesselType.Unknown ? Local.Monitor_Hidden_Vessels : type.displayDescription()),
				config_style);
			if (Lib.IsClicked())
			{
				if(isFiltered) filter_types.Remove(type);
				else filter_types.Add(type);
			}
		}

		Texture2D GetVesselTypeIcon(VesselType type, bool disabled = false)
		{
			switch(type)
			{
				case VesselType.Base:    return disabled ? Textures.base_black :    Textures.base_white;
				case VesselType.EVA:     return disabled ? Textures.eva_black :     Textures.eva_white;
				case VesselType.Lander:  return disabled ? Textures.lander_black :  Textures.lander_white;
				case VesselType.Plane:   return disabled ? Textures.plane_black :   Textures.plane_white;
				case VesselType.Probe:   return disabled ? Textures.probe_black :   Textures.probe_white;
				case VesselType.Relay:   return disabled ? Textures.relay_black :   Textures.relay_white;
				case VesselType.Rover:   return disabled ? Textures.rover_black :   Textures.rover_white;
				case VesselType.Ship:    return disabled ? Textures.ship_black :    Textures.ship_white;
				case VesselType.Station: return disabled ? Textures.station_black : Textures.station_white;
#if !KSP15_16
				case VesselType.DeployedScienceController: return disabled ? Textures.controller_black : Textures.controller_white;
#endif
				case VesselType.Unknown: return disabled ? Textures.sun_black : Textures.sun_white;
				default: return Textures.empty; // this really shouldn't happen.
			}
		}

		void Problem_sunlight(VesselData vd, ref List<Texture2D> icons, ref List<string> tooltips)
		{
			if (vd.EnvInFullShadow)
			{
				icons.Add(Textures.sun_black);
				tooltips.Add(Local.Monitor_Inshadow);//"In shadow"
			}
		}

		void Problem_greenhouses(Vessel v, List<Greenhouse.Data> greenhouses, ref List<Texture2D> icons, ref List<string> tooltips)
		{
			if (greenhouses.Count == 0) return;

			foreach (Greenhouse.Data greenhouse in greenhouses)
			{
				if (greenhouse.issue.Length > 0)
				{
					if (!icons.Contains(Textures.plant_yellow)) icons.Add(Textures.plant_yellow);
					tooltips.Add(Lib.BuildString(Local.Monitor_Greenhouse, " <b>", greenhouse.issue, "</b>"));//"Greenhouse:"
				}
			}
		}

		void Problem_kerbals(List<ProtoCrewMember> crew, ref List<Texture2D> icons, ref List<string> tooltips)
		{
			UInt32 health_severity = 0;
			UInt32 stress_severity = 0;
			foreach (ProtoCrewMember c in crew)
			{
				// get kerbal data
				KerbalData kd = DB.Kerbal(c.name);

				// skip disabled kerbals
				if (kd.disabled) continue;

				foreach (Rule r in Profile.rules)
				{
					RuleData rd = kd.Rule(r.name);
					if (rd.problem > r.danger_threshold)
					{
						if (!r.breakdown) health_severity = Math.Max(health_severity, 2);
						else stress_severity = Math.Max(stress_severity, 2);
						tooltips.Add(Lib.BuildString(c.name, ": <b>", r.title, "</b>"));
					}
					else if (rd.problem > r.warning_threshold)
					{
						if (!r.breakdown) health_severity = Math.Max(health_severity, 1);
						else stress_severity = Math.Max(stress_severity, 1);
						tooltips.Add(Lib.BuildString(c.name, ": <b>", r.title, "</b>"));
					}
				}

			}
			if (health_severity == 1) icons.Add(Textures.health_yellow);
			else if (health_severity == 2) icons.Add(Textures.health_red);
			if (stress_severity == 1) icons.Add(Textures.brain_yellow);
			else if (stress_severity == 2) icons.Add(Textures.brain_red);
		}

		void Problem_radiation(VesselData vd, ref List<Texture2D> icons, ref List<string> tooltips)
		{
			string radiation_str = Lib.BuildString(" (<i>", (vd.EnvHabitatRadiation * 60.0 * 60.0).ToString("F3"), " rad/h)</i>");
			if (vd.EnvHabitatRadiation > 1.0 / 3600.0)
			{
				icons.Add(Textures.radiation_red);
				tooltips.Add(Lib.BuildString(Local.Monitor_ExposedRadiation1, radiation_str));//"Exposed to extreme radiation"
			}
			else if (vd.EnvHabitatRadiation > 0.15 / 3600.0)
			{
				icons.Add(Textures.radiation_yellow);
				tooltips.Add(Lib.BuildString(Local.Monitor_ExposedRadiation2, radiation_str));//"Exposed to intense radiation"
			}
			else if (vd.EnvHabitatRadiation > 0.0195 / 3600.0)
			{
				icons.Add(Textures.radiation_yellow);
				tooltips.Add(Lib.BuildString(Local.Monitor_ExposedRadiation3, radiation_str));//"Exposed to moderate radiation"
			}
		}

		void Problem_poisoning(VesselData vd, ref List<Texture2D> icons, ref List<string> tooltips)
		{
			string poisoning_str = Lib.BuildString(Local.Monitor_CO2level ," <b>", Lib.HumanReadablePerc(vd.Poisoning), "</b>");//CO2 level in internal atmosphere:
			if (vd.Poisoning >= Settings.PoisoningThreshold)
			{
				icons.Add(Textures.recycle_red);
				tooltips.Add(poisoning_str);
			}
			else if (vd.Poisoning > Settings.PoisoningThreshold / 1.25)
			{
				icons.Add(Textures.recycle_yellow);
				tooltips.Add(poisoning_str);
			}
		}

		void Problem_storm(Vessel v, ref List<Texture2D> icons, ref List<string> tooltips)
		{
			if (Storm.Incoming(v))
			{
				icons.Add(Textures.storm_yellow);

				var bd = Lib.IsSun(v.mainBody) ? v.KerbalismData().stormData : DB.Storm(Lib.GetParentPlanet(v.mainBody).name);
				var tti = bd.storm_time - Planetarium.GetUniversalTime();
				tooltips.Add(Lib.BuildString(Lib.Color(Local.Monitor_ejectionincoming, Lib.Kolor.Orange), "\n<i>", Local.Monitor_TimetoimpactCoronalmass, Lib.HumanReadableDuration(tti), "</i>"));//"Coronal mass ejection incoming"Time to impact:
			}
			if (Storm.InProgress(v))
			{
				icons.Add(Textures.storm_red);

				var bd = Lib.IsSun(v.mainBody) ? v.KerbalismData().stormData : DB.Storm(Lib.GetParentPlanet(v.mainBody).name);
				var remainingDuration = bd.storm_time + bd.displayed_duration - Planetarium.GetUniversalTime();
				tooltips.Add(Lib.BuildString(Lib.Color(Local.Monitor_Solarstorminprogress, Lib.Kolor.Red), "\n<i>", Local.Monitor_SolarstormRemaining, Lib.HumanReadableDuration(remainingDuration), "</i>"));//"Solar storm in progress"Remaining duration:
			}
		}

		void Indicator_problems(Panel p, Vessel v, VesselData vd, List<ProtoCrewMember> crew)
		{
			// store problems icons & tooltips
			List<Texture2D> problem_icons = new List<Texture2D>();
			List<string> problem_tooltips = new List<string>();

			// detect problems
			Problem_sunlight(vd, ref problem_icons, ref problem_tooltips);
			if (Features.SpaceWeather) Problem_storm(v, ref problem_icons, ref problem_tooltips);
			if (crew.Count > 0 && Profile.rules.Count > 0) Problem_kerbals(crew, ref problem_icons, ref problem_tooltips);
			if (crew.Count > 0 && Features.Radiation) Problem_radiation(vd, ref problem_icons, ref problem_tooltips);
			Problem_greenhouses(v, vd.Greenhouses, ref problem_icons, ref problem_tooltips);
			if (Features.Poisoning) Problem_poisoning(vd, ref problem_icons, ref problem_tooltips);

			// choose problem icon
			const UInt64 problem_icon_time = 3;
			Texture2D problem_icon = Textures.empty;
			if (problem_icons.Count > 0)
			{
				UInt64 problem_index = ((UInt64)Time.realtimeSinceStartup / problem_icon_time) % (UInt64)(problem_icons.Count);
				problem_icon = problem_icons[(int)problem_index];
			}

			// generate problem icon
			p.AddRightIcon(problem_icon, String.Join("\n", problem_tooltips.ToArray()));
		}

		void Indicator_ec(Panel p, Vessel v, VesselData vd)
		{
#if !KSP15_16
			if (v.vesselType == VesselType.DeployedScienceController)
				return;
#endif

			ResourceInfo ec = ResourceCache.GetResource(v, "ElectricCharge");
			Supply supply = Profile.supplies.Find(k => k.resource == "ElectricCharge");
			double low_threshold = supply != null ? supply.low_threshold : 0.15;
			double depletion = ec.DepletionTime();

			string tooltip = Lib.BuildString
			(
			  "<align=left /><b>", Local.Monitor_name, "\t", Local.Monitor_level, "\t" + Local.Monitor_duration, "</b>\n",//name"level"duration
			  Lib.Color(Lib.BuildString("EC\t", Lib.HumanReadablePerc(ec.Level), "\t", depletion <= double.Epsilon ? Local.Monitor_depleted : Lib.HumanReadableDuration(depletion)),//"depleted"
			  ec.Level <= 0.005 ? Lib.Kolor.Red : ec.Level <= low_threshold ? Lib.Kolor.Orange : Lib.Kolor.None)
			);

			Texture2D image = ec.Level <= 0.005
			  ? Textures.battery_red
			  : ec.Level <= low_threshold
			  ? Textures.battery_yellow
			  : Textures.battery_white;

			p.AddRightIcon(image, tooltip);
		}

		void Indicator_supplies(Panel p, Vessel v, VesselData vd)
		{
			List<string> tooltips = new List<string>();
			uint max_severity = 0;
			if (vd.CrewCount > 0)
			{
				foreach (Supply supply in Profile.supplies.FindAll(k => k.resource != "ElectricCharge"))
				{
					ResourceInfo res = ResourceCache.GetResource(v, supply.resource);
					double depletion = res.DepletionTime();

					if (res.Capacity > double.Epsilon)
					{
						if (tooltips.Count == 0) tooltips.Add(String.Format("<align=left /><b>{0,-18}\t" + Local.Monitor_level + "\t" + Local.Monitor_duration + "</b>", Local.Monitor_name));//level"duration"name"
						tooltips.Add(Lib.Color(
							String.Format("{0,-18}\t{1}\t{2}", supply.resource, Lib.HumanReadablePerc(res.Level), depletion <= double.Epsilon ? Local.Monitor_depleted : Lib.HumanReadableDuration(depletion)),//"depleted"
							res.Level <= 0.005 ? Lib.Kolor.Red : res.Level <= supply.low_threshold ? Lib.Kolor.Orange : Lib.Kolor.None
						));

						uint severity = res.Level <= 0.005 ? 2u : res.Level <= supply.low_threshold ? 1u : 0;
						max_severity = Math.Max(max_severity, severity);
					}
				}
			}

			Texture2D image = max_severity == 2
			  ? Textures.box_red
			  : max_severity == 1
			  ? Textures.box_yellow
			  : Textures.box_white;

			p.AddRightIcon(image, string.Join("\n", tooltips.ToArray()));
		}

		void Indicator_reliability(Panel p, Vessel v, VesselData vd)
		{
			Texture2D image;
			string tooltip;
			if (!vd.Malfunction)
			{
				image = Textures.wrench_white;
				tooltip = string.Empty;
			}
			else if (!vd.Critical)
			{
				image = Textures.wrench_yellow;
				tooltip = Local.Monitor_Malfunctions;//"Malfunctions"
			}
			else
			{
				image = Textures.wrench_red;
				tooltip = Local.Monitor_Criticalfailures;//"Critical failures"
			}

			p.AddRightIcon(image, tooltip);
		}

		void Indicator_signal(Panel p, Vessel v, VesselData vd)
		{
			ConnectionInfo conn = vd.Connection;

			// signal strength
			var strength = Math.Ceiling(conn.strength * 10000) / 10000;
			string signal_str = strength > 0.001 ? Lib.HumanReadablePerc(strength, "F2") : Lib.Color(Lib.Italic(Local.Generic_NO), Lib.Kolor.Orange);

			// target name
			string target_str = conn.linked ? conn.target_name : Local.Generic_NONE;

			// transmitting info
			string comms_str;
			if (!conn.linked)
				comms_str = Local.Generic_NOTHING;
			else if (vd.filesTransmitted.Count == 0)
				comms_str = Local.UI_telemetry;
			else
				comms_str = Lib.BuildString(vd.filesTransmitted.Count.ToString(), vd.filesTransmitted.Count > 1 ? " files" : " file");

			// create tooltip
			string tooltip = Lib.BuildString
			(
			  "<align=left />",
			  String.Format("{0,-14}\t<b>{1}</b>\n", Local.UI_DSNconnected, conn.linked ?
					Lib.Color(Local.Generic_YES, Lib.Kolor.Green) : Lib.Color(Lib.Italic(Local.Generic_NO), Lib.Kolor.Orange)),
			  String.Format("{0,-14}\t<b>{1}</b>\n", Local.UI_sciencerate, Lib.HumanReadableDataRate(conn.rate)),
			  String.Format("{0,-14}\t<b>{1}</b>\n", Local.UI_strength, signal_str),
			  String.Format("{0,-14}\t<b>{1}</b>\n", Local.UI_target, target_str),
			  String.Format("{0,-14}\t<b>{1}</b>", Local.UI_transmitting, comms_str)
			);

			// create icon status
			Texture2D image = Textures.signal_red;
			switch (conn.status)
			{
				case LinkStatus.direct_link:
					image = conn.strength > 0.05 ? Textures.signal_white : Textures.iconSwitch(Textures.signal_yellow, image);   // or 5% signal strength
					break;

				case LinkStatus.indirect_link:
					image = conn.strength > 0.05 ? Textures.signal_white : Textures.iconSwitch(Textures.signal_yellow, image);   // or 5% signal strength
					tooltip += Lib.Color("\n" + Local.UI_Signalrelayed, Lib.Kolor.Yellow);
					break;

				case LinkStatus.plasma:
					tooltip += Lib.Color(Lib.Italic("\n" + Local.UI_Plasmablackout), Lib.Kolor.Red);
					break;

				case LinkStatus.storm:
					tooltip += Lib.Color(Lib.Italic("\n" + Local.UI_Stormblackout), Lib.Kolor.Red);
					break;
			}

			p.AddRightIcon(image, tooltip, () => UI.Open((p2) => p2.ConnMan(v)));
		}

		// id of selected vessel
		Guid selected_id;

		// selected vessel
		Vessel selected_v;

		// filter placeholder
		string filter_placeholder = Local.Generic_search.ToUpper() + "...";

		// current filter values
		string filter = string.Empty;
		List<VesselType> filter_types = new List<VesselType>();

		// used by scroll window mechanics
		Vector2 scroll_pos;

		// styles
		GUIStyle filter_style;            // vessel filter
		GUIStyle config_style;            // config entry label

		// monitor page
		MonitorPage page = MonitorPage.telemetry;
		Panel panel;
	}
} // KERBALISM
