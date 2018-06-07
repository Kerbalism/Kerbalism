using System;
using System.Collections.Generic;
using UnityEngine;


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
			filter_style.alignment = TextAnchor.MiddleCenter;
			filter_style.fixedHeight = Styles.ScaleFloat(16.0f);
			filter_style.border = new RectOffset(0, 0, 0, 0);

			// vessel config style
			config_style = new GUIStyle(HighLogic.Skin.label);
			config_style.normal.textColor = Color.white;
			config_style.padding = new RectOffset(0, 0, 0, 0);
			config_style.alignment = TextAnchor.MiddleLeft;
			config_style.imagePosition = ImagePosition.ImageLeft;
			config_style.fontSize = Styles.ScaleInteger(9);

			// group texfield style
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

				// filter flag is updated on render_vessel
				show_filter = false;

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
				Render_vessel(panel, selected_v);

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
			if (selected_v != null)
			{
				Render_menu(selected_v);
			}
			// if at least one vessel is assigned to a group
			else if (show_filter)
			{
				Render_filter();
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
			if ((page == MonitorPage.data || page == MonitorPage.log || selected_id == Guid.Empty) && !Lib.IsFlight())
			{
				return Styles.ScaleWidthFloat(465.0f);
			}
			return Styles.ScaleWidthFloat(355.0f);
		}

		public float Height()
		{
			// top spacing
			float h = Styles.ScaleFloat(10.0f);

			// panel height
			h += panel.Height();

			// one is selected, or filter is required
			if (selected_id != Guid.Empty || show_filter)
			{
				h += Styles.ScaleFloat(26.0f);
			}

			// clamp to screen height
			return Math.Min(h, Screen.height * 0.75f);
		}

		bool Render_vessel(Panel p, Vessel v)
		{
			// get vessel info
			Vessel_info vi = Cache.VesselInfo(v);

			// skip invalid vessels
			if (!vi.is_valid) return false;

			// get data from db
			VesselData vd = DB.Vessel(v);

			// determine if filter must be shown
			show_filter |= vd.group.Length > 0 && vd.group != "NONE";

			// skip filtered vessels
			if (Filtered() && vd.group != filter) return false;

			// get resource handler
			Vessel_resources resources = ResourceCache.Get(v);

			// get vessel crew
			List<ProtoCrewMember> crew = Lib.CrewList(v);

			// get vessel name
			string vessel_name = v.isEVA ? crew[0].name : v.vesselName;

			// get body name
			string body_name = v.mainBody.name.ToUpper();

			// render entry
			p.AddHeader
			(
			  Lib.BuildString("<b>",
			  Lib.Ellipsis(vessel_name, Styles.ScaleStringLength(((page == MonitorPage.data || page == MonitorPage.log || selected_id == Guid.Empty) && !Lib.IsFlight()) ? 50 : 30)),
			  "</b> <size=", Styles.ScaleInteger(9).ToString(),
			  "><color=#cccccc>", Lib.Ellipsis(body_name, Styles.ScaleStringLength(8)), "</color></size>"),
			  string.Empty,
			  () => { selected_id = selected_id != v.id ? v.id : Guid.Empty; }
			);

			// problem indicator
			Indicator_problems(p, v, vi, crew);

			// battery indicator
			Indicator_ec(p, v, vi);

			// supply indicator
			if (Features.Supplies) Indicator_supplies(p, v, vi);

			// reliability indicator
			if (Features.Reliability) Indicator_reliability(p, v, vi);

			// signal indicator
			if (RemoteTech.Enabled() || HighLogic.fetch.currentGame.Parameters.Difficulty.EnableCommNet) Indicator_signal(p, v, vi);

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
			if (Settings.StockMessages != true)
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
			filter = Lib.TextFieldPlaceholder("Kerbalism_filter", filter, filter_placeholder, filter_style).ToUpper();
			GUILayout.EndHorizontal();
			GUILayout.Space(Styles.ScaleFloat(10.0f));
		}


		void Problem_sunlight(Vessel_info info, ref List<Texture> icons, ref List<string> tooltips)
		{
			if (info.sunlight <= double.Epsilon)
			{
				icons.Add(Icons.sun_black);
				tooltips.Add("In shadow");
			}
		}

		void Problem_greenhouses(Vessel v, List<Greenhouse.Data> greenhouses, ref List<Texture> icons, ref List<string> tooltips)
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

		void Problem_kerbals(List<ProtoCrewMember> crew, ref List<Texture> icons, ref List<string> tooltips)
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

		void Problem_radiation(Vessel_info info, ref List<Texture> icons, ref List<string> tooltips)
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

		void Problem_poisoning(Vessel_info info, ref List<Texture> icons, ref List<string> tooltips)
		{
			string poisoning_str = Lib.BuildString("CO2 level in internal atmosphere: <b>", Lib.HumanReadablePerc(info.poisoning), "</b>");
			if (info.poisoning >= 0.05)
			{
				icons.Add(Icons.recycle_red);
				tooltips.Add(poisoning_str);
			}
			else if (info.poisoning > 0.025)
			{
				icons.Add(Icons.recycle_yellow);
				tooltips.Add(poisoning_str);
			}
		}

		void Problem_storm(Vessel v, ref List<Texture> icons, ref List<string> tooltips)
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
			List<Texture> problem_icons = new List<Texture>();
			List<string> problem_tooltips = new List<string>();

			// detect problems
			Problem_sunlight(vi, ref problem_icons, ref problem_tooltips);
			if (Features.SpaceWeather) Problem_storm(v, ref problem_icons, ref problem_tooltips);
			if (crew.Count > 0 && Profile.rules.Count > 0) Problem_kerbals(crew, ref problem_icons, ref problem_tooltips);
			if (crew.Count > 0 && Features.Radiation) Problem_radiation(vi, ref problem_icons, ref problem_tooltips);
			Problem_greenhouses(v, vi.greenhouses, ref problem_icons, ref problem_tooltips);
			if (Features.Poisoning) Problem_poisoning(vi, ref problem_icons, ref problem_tooltips);

			// choose problem icon
			const UInt64 problem_icon_time = 3;
			Texture problem_icon = Icons.empty;
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

			Texture image = ec.level <= 0.005
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
						if (tooltips.Count == 0)
						{
							tooltips.Add("<align=left /><b>name\t\tlevel\tduration</b>");
						}

						tooltips.Add(Lib.BuildString
						(
						  res.level <= 0.005 ? "<color=#ff0000>" : res.level <= supply.low_threshold ? "<color=#ffff00>" : "<color=#cccccc>",
						  supply.resource,
						  supply.resource != "Ammonia" ? "\t\t" : "\t", //< hack: make ammonia fit damn it
						  Lib.HumanReadablePerc(res.level), "\t",
						  depletion <= double.Epsilon ? "depleted" : Lib.HumanReadableDuration(depletion),
						  "</color>"
						));

						uint severity = res.level <= 0.005 ? 2u : res.level <= supply.low_threshold ? 1u : 0;
						max_severity = Math.Max(max_severity, severity);
					}
				}
			}

			Texture image = max_severity == 2
			  ? Icons.box_red
			  : max_severity == 1
			  ? Icons.box_yellow
			  : Icons.box_white;

			p.AddIcon(image, string.Join("\n", tooltips.ToArray()));
		}


		void Indicator_reliability(Panel p, Vessel v, Vessel_info vi)
		{
			Texture image;
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
			if (RemoteTech.Enabled())
			{
				double signal_delay = RemoteTech.GetShortestSignalDelay(v.id);
				string signal_str = "";
				if (signal_delay < Double.Epsilon)
				{
					signal_str = "none";
				}
				else
				{
					signal_str = KSPUtil.dateTimeFormatter.PrintTimeStampCompact(signal_delay, false, false);
				}
				string tooltip_rt = Lib.BuildString(
				  "<align=left />",
				  String.Format("{0,-14}\t<b>{1}</b>\n", "connected", conn.linked ? "yes" : "no"),
				  String.Format("{0,-14}\t<b>{1}</b>\n", "delay", conn.linked ? signal_str : "no connection"),
				  String.Format("{0,-14}\t\t<b>{1}</b>", "rate", Lib.HumanReadableDataRate(vi.connection.rate))
				);
				Texture image_rt = Icons.signal_red;
				if (RemoteTech.Connected(v.id)) image_rt = Icons.signal_white;
				if (RemoteTech.Connected(v.id) && !RemoteTech.ConnectedToKSC(v.id)) image_rt = Icons.signal_yellow;
				if (vi.blackout || RemoteTech.GetCommsBlackout(v.id))
				{
					image_rt = Icons.signal_red;
					tooltip_rt += "\n\n<color=red><i>Blackout</i></color>";
				}
				p.AddIcon(image_rt, tooltip_rt);
				return;
			}

			// target name
			string target_str = conn.target_name;
			if (conn.status == LinkStatus.no_antenna || conn.status == LinkStatus.no_link)
				target_str = "none";

			// transmitted label, content and tooltip
			string comms_str = conn.linked ? "telemetry" : "nothing";
			if (vi.transmitting.Length > 0)
			{
				ExperimentInfo exp = Science.Experiment(vi.transmitting);
				comms_str = exp.name;
			}

			string tooltip = Lib.BuildString
			(
			  "<align=left />",
			  String.Format("{0,-14}\t<b>{1}</b>\n", "DSN connected", conn.linked ? "<color=green>yes</color>" : "<color=red><i>no</i></color>"),
			  String.Format("{0,-14}\t\t<b>{1}</b>\n", "rate", Lib.HumanReadableDataRate(conn.rate)),
			  String.Format("{0,-14}\t<b>{1}</b>\n", "strength", Lib.HumanReadablePerc(conn.strength, "F2")),
			  String.Format("{0,-14}\t<b>{1}</b>\n", "target", target_str),
			  String.Format("{0,-14}\t<b>{1}</b>", "transmitting", comms_str)
			);

			Texture image = Icons.signal_red;
			switch (conn.status)
			{
				case LinkStatus.direct_link:
					image = conn.rate > 0.0048828125 ? Icons.signal_white : Icons.signal_yellow; // 5Kb
					break;

				case LinkStatus.indirect_link:
					image = conn.rate > 0.0048828125 ? Icons.signal_white : Icons.signal_yellow; // 5Kb
					tooltip += "\n<color=yellow>Signal relayed</color>";
					break;

				case LinkStatus.no_link:
					image = Icons.signal_red;
					break;

				case LinkStatus.no_antenna:
					image = Icons.signal_red;
					tooltip += "\n<color=red>No antenna</color>";
					break;

				case LinkStatus.blackout:
					image = Icons.signal_red;
					tooltip += "\n<color=red><i>Blackout</i></color>";
					break;
			}

			p.AddIcon(image, tooltip);
		}

		// return true if the list of vessels is filtered
		bool Filtered()
		{
			return filter.Length > 0 && filter != filter_placeholder;
		}

		// id of selected vessel
		Guid selected_id;

		// selected vessel
		Vessel selected_v;

		// group filter placeholder
		const string filter_placeholder = "FILTER BY GROUP";

		// store group filter, if any
		string filter = string.Empty;

		// determine if filter is shown
		bool show_filter;

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