using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSP.Localization;

namespace KERBALISM
{
	public enum MonitorPage
	{
		telemetry,
		data,
		scripts,
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

			// group text field style
			group_style = new GUIStyle(config_style)
			{
				imagePosition = ImagePosition.TextOnly,
				stretchWidth = true,
				fixedHeight = Styles.ScaleFloat(11.0f)
			};
			group_style.normal.textColor = Color.yellow;

			// initialize panel
			panel = new Panel();

			// auto-switch selected vessel on scene changes
			GameEvents.onVesselChange.Add((Vessel v) => { if (selected_id != Guid.Empty) selected_id = v.id; });
		}

		public void Update()
		{
			// reset panel
			panel.Clear();

			// get vessel
			selected_v = selected_id == Guid.Empty ? null : FlightGlobals.FindVessel(selected_id);

			// if nothing is selected, or if the selected vessel doesn't exist
			// anymore, or if it has become invalid for whatever reason
			if (selected_v == null || !Cache.VesselInfo(selected_v).is_valid)
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
					panel.AddHeader("<i>no vessels</i>");
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
				}
			}
		}

		public void Render()
		{
			// start scrolling view
			scroll_pos = GUILayout.BeginScrollView(scroll_pos, HighLogic.Skin.horizontalScrollbar, HighLogic.Skin.verticalScrollbar);

			// render panel content
			panel.Render();

			// end scroll view
			GUILayout.EndScrollView();

			// if a vessel is selected, and exist
			if (selected_v != null) Render_menu(selected_v);
			else Render_filter();

			// right click goes back to list view
			if (Event.current.type == EventType.MouseDown
			 && Event.current.button == 1)
			{
				selected_id = Guid.Empty;
			}
		}

		public float Width()
		{
			if ((page == MonitorPage.data || page == MonitorPage.log || selected_id == Guid.Empty) && !Lib.IsFlight())
				return Styles.ScaleWidthFloat(465.0f);
			return Styles.ScaleWidthFloat(355.0f);
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

		bool Filter_match(VesselType vesselType, string vesselGroup)
		{
			if(filter_types.Contains(vesselType)) return false;
			if(filter.Length <= 0 || filter == filter_placeholder) return true;

			List<string> filterTags = filter.ToLower().Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToList();
			List<string> vesselTags = vesselGroup.ToLower().Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToList();

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
			Vessel_info vi = Cache.VesselInfo(v);

			// skip invalid vessels
			if (!vi.is_valid) return false;

			// get data from db
			VesselData vd = DB.Vessel(v);

			// get vessel crew
			List<ProtoCrewMember> crew = Lib.CrewList(v);

			// get vessel name
			string vessel_name = v.isEVA ? crew[0].name : v.vesselName;

			// get body name
			string body_name = v.mainBody.name.ToUpper();

			// skip filtered vessels
			if (!Filter_match(v.vesselType, vd.group + " " + body_name + " " + vessel_name)) return false;

			// render entry
			p.AddHeader
			(
			  Lib.BuildString("<b>",
			  Lib.Ellipsis(vessel_name, Styles.ScaleStringLength(((page == MonitorPage.data || page == MonitorPage.log || selected_id == Guid.Empty) && !Lib.IsFlight()) ? 45 : 25)),
			  "</b> <size=", Styles.ScaleInteger(9).ToString(), ">", Lib.Color("#cccccc", Lib.Ellipsis(body_name, Styles.ScaleStringLength(8))), "</size>"),
			  string.Empty,
			  () => { selected_id = selected_id != v.id ? v.id : Guid.Empty; }
			);

			// vessel type icon
			if (!selected)
			p.SetIcon(GetVesselTypeIcon(v.vesselType), v.vesselType.displayDescription(), () => { selected_id = selected_id != v.id ? v.id : Guid.Empty; });
			else
			{
				if (FlightGlobals.ActiveVessel != v)
				{
					if (Lib.IsFlight())
					{
						p.SetIcon(GetVesselTypeIcon(v.vesselType), "Go to vessel!", () => Lib.Popup
						("Warning!",
							Lib.BuildString("Do you really want go to ", vessel_name, " vessel?"),
							new DialogGUIButton("Go", () => { GotoVessel.JumpToVessel(v); }),
							new DialogGUIButton("Target", () => { GotoVessel.SetVesselAsTarget(v); }),
							new DialogGUIButton("Stay", () => { })));
					}
					else
					{
						p.SetIcon(GetVesselTypeIcon(v.vesselType), "Go to vessel!", () => Lib.Popup
						("Warning!",
							Lib.BuildString("Do you really want go to ", vessel_name, " vessel?"),
							new DialogGUIButton("Go", () => { GotoVessel.JumpToVessel(v); }),
							new DialogGUIButton("Stay", () => { })));
					}
				}
				else
				{
					p.SetIcon(GetVesselTypeIcon(v.vesselType), v.vesselType.displayDescription(), () => { });
				}
			}

			// problem indicator
			Indicator_problems(p, v, vi, crew);

			// battery indicator
			Indicator_ec(p, v, vi);

			// supply indicator
			if (Features.Supplies) Indicator_supplies(p, v, vi);

			// reliability indicator
			if (Features.Reliability) Indicator_reliability(p, v, vi);

			// signal indicator
			if (RemoteTech.Enabled || HighLogic.fetch.currentGame.Parameters.Difficulty.EnableCommNet) Indicator_signal(p, v, vi);

			// done
			return true;
		}

		void Render_menu(Vessel v)
		{
			const string tooltip = "\n<i>(middle-click to popout in a window, middle-click again to close popout)</i>";
			VesselData vd = DB.Vessel(v);
			GUILayout.BeginHorizontal(Styles.entry_container);
			GUILayout.Label(new GUIContent(page == MonitorPage.telemetry ? " <color=#00ffff>INFO</color> " : " INFO ", Icons.small_info, "Telemetry readings" + tooltip), config_style);
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
				GUILayout.Label(new GUIContent(page == MonitorPage.data ? " <color=#00ffff>DATA</color> " : " DATA ", Icons.small_folder, "Stored files and samples" + tooltip), config_style);
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
				GUILayout.Label(new GUIContent(page == MonitorPage.scripts ? " <color=#00ffff>AUTO</color> " : " AUTO ", Icons.small_console, "Control and automate components" + tooltip), config_style);
				if (Lib.IsClicked()) page = MonitorPage.scripts;
				else if (Lib.IsClicked(2))
				{
					if (UI.window.PanelType == Panel.PanelType.scripts)
						UI.window.Close();
					else
						UI.Open((p) => p.Devman(v));
				}
			}
			if (PreferencesMessages.Instance.stockMessages != true)
			{
				GUILayout.Label(new GUIContent(page == MonitorPage.log ? " <color=#00ffff>LOG</color> " : " LOG ", Icons.small_notes, "See previous notifications" + tooltip), config_style);
				if (Lib.IsClicked()) page = MonitorPage.log;
				else if (Lib.IsClicked(2))
				{
					if (UI.window.PanelType == Panel.PanelType.log)
						UI.window.Close();
					else
						UI.Open((p) => p.Logman(v));
				}
			}
			GUILayout.Label(new GUIContent(page == MonitorPage.config ? " <color=#00ffff>CFG</color> " : " CFG ", Icons.small_config, "Configure the vessel" + tooltip), config_style);
			if (Lib.IsClicked()) page = MonitorPage.config;
			else if (Lib.IsClicked(2))
			{
				if (UI.window.PanelType == Panel.PanelType.config)
					UI.window.Close();
				else
					UI.Open((p) => p.Config(v));
			}
			GUILayout.Label(new GUIContent(" GROUP ", Icons.small_search, "Organize in groups"), config_style);
			vd.group = Lib.TextFieldPlaceholder("Kerbalism_group", vd.group, "NONE", group_style).ToUpper();
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

			filter = Lib.TextFieldPlaceholder("Kerbalism_filter", filter, filter_placeholder, filter_style).ToUpper();
			GUILayout.EndHorizontal();
			GUILayout.Space(Styles.ScaleFloat(10.0f));
		}

		void Render_TypeFilterButon(VesselType type)
		{
			bool isFiltered = filter_types.Contains(type);
			GUILayout.Label(new GUIContent(" ", GetVesselTypeIcon(type, isFiltered), type.displayDescription()), config_style);
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
				case VesselType.Base:    return disabled ? Icons.base_black :    Icons.base_white;
				case VesselType.EVA:     return disabled ? Icons.eva_black :     Icons.eva_white;
				case VesselType.Lander:  return disabled ? Icons.lander_black :  Icons.lander_white;
				case VesselType.Plane:   return disabled ? Icons.plane_black :   Icons.plane_white;
				case VesselType.Probe:   return disabled ? Icons.probe_black :   Icons.probe_white;
				case VesselType.Relay:   return disabled ? Icons.relay_black :   Icons.relay_white;
				case VesselType.Rover:   return disabled ? Icons.rover_black :   Icons.rover_white;
				case VesselType.Ship:    return disabled ? Icons.ship_black :    Icons.ship_white;
				case VesselType.Station: return disabled ? Icons.station_black : Icons.station_white;
				default: return Icons.empty; // this really shouldn't happen.
			}
		}

		void Problem_sunlight(Vessel_info info, ref List<Texture2D> icons, ref List<string> tooltips)
		{
			if (info.sunlight <= double.Epsilon)
			{
				icons.Add(Icons.sun_black);
				tooltips.Add("In shadow");
			}
		}

		void Problem_greenhouses(Vessel v, List<Greenhouse.Data> greenhouses, ref List<Texture2D> icons, ref List<string> tooltips)
		{
			if (greenhouses.Count == 0) return;

			foreach (Greenhouse.Data greenhouse in greenhouses)
			{
				if (greenhouse.issue.Length > 0)
				{
					if (!icons.Contains(Icons.plant_yellow)) icons.Add(Icons.plant_yellow);
					tooltips.Add(Lib.BuildString("Greenhouse: <b>", greenhouse.issue, "</b>"));
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
						tooltips.Add(Lib.BuildString(c.name, ": <b>", r.name, "</b>"));
					}
					else if (rd.problem > r.warning_threshold)
					{
						if (!r.breakdown) health_severity = Math.Max(health_severity, 1);
						else stress_severity = Math.Max(stress_severity, 1);
						tooltips.Add(Lib.BuildString(c.name, ": <b>", r.name, "</b>"));
					}
				}

			}
			if (health_severity == 1) icons.Add(Icons.health_yellow);
			else if (health_severity == 2) icons.Add(Icons.health_red);
			if (stress_severity == 1) icons.Add(Icons.brain_yellow);
			else if (stress_severity == 2) icons.Add(Icons.brain_red);
		}

		void Problem_radiation(Vessel_info info, ref List<Texture2D> icons, ref List<string> tooltips)
		{
			string radiation_str = Lib.BuildString(" (<i>", (info.radiation * 60.0 * 60.0).ToString("F3"), " rad/h)</i>");
			if (info.radiation > 1.0 / 3600.0)
			{
				icons.Add(Icons.radiation_red);
				tooltips.Add(Lib.BuildString("Exposed to extreme radiation", radiation_str));
			}
			else if (info.radiation > 0.15 / 3600.0)
			{
				icons.Add(Icons.radiation_yellow);
				tooltips.Add(Lib.BuildString("Exposed to intense radiation", radiation_str));
			}
			else if (info.radiation > 0.0195 / 3600.0)
			{
				icons.Add(Icons.radiation_yellow);
				tooltips.Add(Lib.BuildString("Exposed to moderate radiation", radiation_str));
			}
		}

		void Problem_poisoning(Vessel_info info, ref List<Texture2D> icons, ref List<string> tooltips)
		{
			string poisoning_str = Lib.BuildString("CO2 level in internal atmosphere: <b>", Lib.HumanReadablePerc(info.poisoning), "</b>");
			if (info.poisoning >= Settings.PoisoningThreshold)
			{
				icons.Add(Icons.recycle_red);
				tooltips.Add(poisoning_str);
			}
			else if (info.poisoning > Settings.PoisoningThreshold / 1.25)
			{
				icons.Add(Icons.recycle_yellow);
				tooltips.Add(poisoning_str);
			}
		}

		void Problem_humidity(Vessel_info info, ref List<Texture2D> icons, ref List<string> tooltips)
		{
			string humidity_str = Lib.BuildString("Humidity level in internal atmosphere: <b>", Lib.HumanReadablePerc(info.humidity), "</b>");
			if (info.humidity >= Settings.HumidityThreshold)
			{
				icons.Add(Icons.recycle_red);
				tooltips.Add(humidity_str);
			}
			else if (info.humidity > Settings.HumidityThreshold / 1.25)
			{
				icons.Add(Icons.recycle_yellow);
				tooltips.Add(humidity_str);
			}
		}

		void Problem_storm(Vessel v, ref List<Texture2D> icons, ref List<string> tooltips)
		{
			if (Storm.Incoming(v))
			{
				icons.Add(Icons.storm_yellow);
				tooltips.Add(Lib.BuildString("Coronal mass ejection incoming <i>(", Lib.HumanReadableDuration(Storm.TimeBeforeCME(v)), ")</i>"));
			}
			if (Storm.InProgress(v))
			{
				icons.Add(Icons.storm_red);
				tooltips.Add(Lib.BuildString("Solar storm in progress <i>(", Lib.HumanReadableDuration(Storm.TimeLeftCME(v)), ")</i>"));
			}
		}

		void Indicator_problems(Panel p, Vessel v, Vessel_info vi, List<ProtoCrewMember> crew)
		{
			// store problems icons & tooltips
			List<Texture2D> problem_icons = new List<Texture2D>();
			List<string> problem_tooltips = new List<string>();

			// detect problems
			Problem_sunlight(vi, ref problem_icons, ref problem_tooltips);
			if (Features.SpaceWeather) Problem_storm(v, ref problem_icons, ref problem_tooltips);
			if (crew.Count > 0 && Profile.rules.Count > 0) Problem_kerbals(crew, ref problem_icons, ref problem_tooltips);
			if (crew.Count > 0 && Features.Radiation) Problem_radiation(vi, ref problem_icons, ref problem_tooltips);
			Problem_greenhouses(v, vi.greenhouses, ref problem_icons, ref problem_tooltips);
			if (Features.Poisoning) Problem_poisoning(vi, ref problem_icons, ref problem_tooltips);
			if (Features.Humidity) Problem_humidity(vi, ref problem_icons, ref problem_tooltips);

			// choose problem icon
			const UInt64 problem_icon_time = 3;
			Texture2D problem_icon = Icons.empty;
			if (problem_icons.Count > 0)
			{
				UInt64 problem_index = ((UInt64)Time.realtimeSinceStartup / problem_icon_time) % (UInt64)(problem_icons.Count);
				problem_icon = problem_icons[(int)problem_index];
			}

			// generate problem icon
			p.AddIcon(problem_icon, String.Join("\n", problem_tooltips.ToArray()));
		}

		void Indicator_ec(Panel p, Vessel v, Vessel_info vi)
		{
			Resource_info ec = ResourceCache.Info(v, "ElectricCharge");
			Supply supply = Profile.supplies.Find(k => k.resource == "ElectricCharge");
			double low_threshold = supply != null ? supply.low_threshold : 0.15;
			double depletion = ec.Depletion(vi.crew_count);

			string tooltip = Lib.BuildString
			(
			  "<align=left /><b>name\tlevel\tduration</b>\n",
			  ec.level <= 0.005 ? "<color=#ff0000>" : ec.level <= low_threshold ? "<color=#ffff00>" : "<color=#cccccc>",
			  "EC\t",
			  Lib.HumanReadablePerc(ec.level), "\t",
			  depletion <= double.Epsilon ? "depleted" : Lib.HumanReadableDuration(depletion),
			  "</color>"
			);

			Texture2D image = ec.level <= 0.005
			  ? Icons.battery_red
			  : ec.level <= low_threshold
			  ? Icons.battery_yellow
			  : Icons.battery_white;

			p.AddIcon(image, tooltip);
		}

		void Indicator_supplies(Panel p, Vessel v, Vessel_info vi)
		{
			List<string> tooltips = new List<string>();
			uint max_severity = 0;
			if (vi.crew_count > 0)
			{
				foreach (Supply supply in Profile.supplies.FindAll(k => k.resource != "ElectricCharge"))
				{
					Resource_info res = ResourceCache.Info(v, supply.resource);
					double depletion = res.Depletion(vi.crew_count);

					if (res.capacity > double.Epsilon)
					{
						if (tooltips.Count == 0) tooltips.Add(String.Format("<align=left /><b>{0,-18}\tlevel\tduration</b>", "name"));
						tooltips.Add(Lib.BuildString
						(
						  res.level <= 0.005 ? "<color=#ff0000>" : res.level <= supply.low_threshold ? "<color=#ffff00>" : "<color=#cccccc>",
						  String.Format("{0,-18}\t{1}\t{2}", supply.resource, Lib.HumanReadablePerc(res.level),
						  depletion <= double.Epsilon ? "depleted" : Lib.HumanReadableDuration(depletion)),
						  "</color>"
						));

						uint severity = res.level <= 0.005 ? 2u : res.level <= supply.low_threshold ? 1u : 0;
						max_severity = Math.Max(max_severity, severity);
					}
				}
			}

			Texture2D image = max_severity == 2
			  ? Icons.box_red
			  : max_severity == 1
			  ? Icons.box_yellow
			  : Icons.box_white;

			p.AddIcon(image, string.Join("\n", tooltips.ToArray()));
		}

		void Indicator_reliability(Panel p, Vessel v, Vessel_info vi)
		{
			Texture2D image;
			string tooltip;
			if (!vi.malfunction)
			{
				image = Icons.wrench_white;
				tooltip = string.Empty;
			}
			else if (!vi.critical)
			{
				image = Icons.wrench_yellow;
				tooltip = "Malfunctions";
			}
			else
			{
				image = Icons.wrench_red;
				tooltip = "Critical failures";
			}

			p.AddIcon(image, tooltip);
		}

		void Indicator_signal(Panel p, Vessel v, Vessel_info vi)
		{
			ConnectionInfo conn = vi.connection;
			bool remotetech = RemoteTech.Enabled;

			// signal strength
			string signal_str = conn.strength > Double.Epsilon ? Lib.HumanReadablePerc(conn.strength, "F2") : Lib.Color("#ffaa00", Lib.Italic(Localizer.Format("#KERBALISM_Generic_NO")));

			// target name
			string target_str = conn.linked ? conn.target_name : Localizer.Format("#KERBALISM_Generic_NONE");

			// transmitting info
			string comms_str = conn.linked ? Localizer.Format("#KERBALISM_UI_telemetry") : Localizer.Format("#KERBALISM_Generic_NOTHING");
			if (vi.transmitting.Length > 0)
			{
				ExperimentInfo exp = Science.Experiment(vi.transmitting);
				comms_str = exp.name;
			}

			// create tooltip
			string tooltip = Lib.BuildString
			(
			  "<align=left />",
			  String.Format("{0,-14}\t<b>{1}</b>\n", Localizer.Format("#KERBALISM_UI_DSNconnected"), conn.linked ?
					Lib.Color("green", Localizer.Format("#KERBALISM_Generic_YES")) : Lib.Color("#ffaa00", Lib.Italic(Localizer.Format("#KERBALISM_Generic_NO")))),
			  String.Format("{0,-14}\t<b>{1}</b>\n", Localizer.Format("#KERBALISM_UI_sciencerate"), Lib.HumanReadableDataRate(conn.rate)),
			  String.Format("{0,-14}\t<b>{1}</b>\n", Localizer.Format("#KERBALISM_UI_strength"), signal_str),
			  String.Format("{0,-14}\t<b>{1}</b>\n", Localizer.Format("#KERBALISM_UI_target"), target_str),
			  String.Format("{0,-14}\t<b>{1}</b>", Localizer.Format("#KERBALISM_UI_transmitting"), comms_str)
			);

			// create icon status
			Texture2D image = Icons.signal_red;
			switch (conn.status)
			{
				case LinkStatus.direct_link:
					image = remotetech ? conn.strength < 15.0 ? Icons.signal_white : Icons.signal_yellow :      // 15 seconds for RemoteTech signal delay
						conn.strength > 0.05 ? Icons.signal_white : Icons.signal_yellow;                        // or 5% signal strength
					break;

				case LinkStatus.indirect_link:
					image = remotetech ? conn.strength < 15.0 ? Icons.signal_white : Icons.signal_yellow :      // 15 seconds for RemoteTech signal delay
						conn.strength > 0.05 ? Icons.signal_white : Icons.signal_yellow;                        // or 5% signal strength
					tooltip += Lib.Color("yellow", "\n" + Localizer.Format("#KERBALISM_UI_Signalrelayed"));
					break;

				case LinkStatus.plasma:
					tooltip += Lib.Color("red", Lib.Italic("\n" + Localizer.Format("#KERBALISM_UI_Plasmablackout")));
					break;

				case LinkStatus.storm:
					tooltip += Lib.Color("red", Lib.Italic("\n" + Localizer.Format("#KERBALISM_UI_Stormblackout")));
					break;
			}

			p.AddIcon(image, tooltip);
		}

		// id of selected vessel
		Guid selected_id;

		// selected vessel
		Vessel selected_v;

		// filter placeholder
		const string filter_placeholder = "SEARCH...";

		// current filter values
		string filter = string.Empty;
		List<VesselType> filter_types = new List<VesselType>();

		// used by scroll window mechanics
		Vector2 scroll_pos;

		// styles
		GUIStyle filter_style;            // vessel filter
		GUIStyle config_style;            // config entry label
		GUIStyle group_style;             // config group textfield

		// monitor page
		MonitorPage page = MonitorPage.telemetry;
		Panel panel;
	}
} // KERBALISM
