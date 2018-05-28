using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{


	public static class Telemetry
	{
		public static void TelemetryPanel(this Panel p, Vessel v)
		{
			// avoid corner-case when this is called in a lambda after scene changes
			v = FlightGlobals.FindVessel(v.id);

			// if vessel doesn't exist anymore, leave the panel empty
			if (v == null) return;

			// get info from the cache
			Vessel_info vi = Cache.VesselInfo(v);

			// if not a valid vessel, leave the panel empty
			if (!vi.is_valid) return;

			// set metadata
			p.Title(Lib.BuildString(Lib.Ellipsis(v.vesselName, Styles.ScaleStringLength(20)), " <color=#cccccc>TELEMETRY</color>"));
			p.Width(Styles.ScaleWidthFloat(355.0f));
			p.paneltype = Panel.PanelType.telemetry;

			// time-out simulation
			if (p.Timeout(vi)) return;

			// get vessel data
			VesselData vd = DB.Vessel(v);

			// get resources
			Vessel_resources resources = ResourceCache.Get(v);

			// get crew
			var crew = Lib.CrewList(v);

			// draw the content
			Render_crew(p, crew);
			Render_greenhouse(p, vi);
			Render_supplies(p, v, vi, resources);
			Render_habitat(p, v, vi);
			Render_environment(p, v, vi);

			// collapse eva kerbal sections into one
			if (v.isEVA) p.Collapse("EVA SUIT");
		}


		static void Render_environment(Panel p, Vessel v, Vessel_info vi)
		{
			// don't show env panel in eva kerbals
			if (v.isEVA) return;

			// get all sensor readings
			HashSet<string> readings = new HashSet<string>();
			if (v.loaded)
			{
				foreach (var s in Lib.FindModules<Sensor>(v))
				{
					readings.Add(s.type);
				}
			}
			else
			{
				foreach (ProtoPartModuleSnapshot m in Lib.FindModules(v.protoVessel, "Sensor"))
				{
					readings.Add(Lib.Proto.GetString(m, "type"));
				}
			}
			readings.Remove(string.Empty);

			p.AddSection("ENVIRONMENT");
			foreach (string type in readings)
			{
				p.AddContent(type, Sensor.Telemetry_content(v, vi, type), Sensor.Telemetry_tooltip(v, vi, type));
			}
			if (readings.Count == 0) p.AddContent("<i>no sensors installed</i>");
		}

		static void Render_habitat(Panel p, Vessel v, Vessel_info vi)
		{
			// if habitat feature is disabled, do not show the panel
			if (!Features.Habitat) return;

			// if vessel is unmanned, do not show the panel
			if (vi.crew_count == 0) return;

			// render panel, add some content based on enabled features
			p.AddSection("HABITAT");
			if (Features.Poisoning) p.AddContent("co2 level", Lib.Color(Lib.HumanReadablePerc(vi.poisoning, "F2"), vi.poisoning > Settings.PoisoningThreshold, "yellow"));
			if (!v.isEVA)
			{
				if (Features.Pressure) p.AddContent("pressure", Lib.HumanReadablePressure(vi.pressure * Sim.PressureAtSeaLevel()));
				if (Features.Shielding) p.AddContent("shielding", Habitat.Shielding_to_string(vi.shielding));
				if (Features.LivingSpace) p.AddContent("living space", Habitat.Living_space_to_string(vi.living_space));
				if (Features.Comfort) p.AddContent("comfort", vi.comforts.Summary(), vi.comforts.Tooltip());
			}
		}

		static void Render_supplies(Panel p, Vessel v, Vessel_info vi, Vessel_resources resources)
		{
			// for each supply
			int supplies = 0;
			foreach (Supply supply in Profile.supplies)
			{
				// get resource info
				Resource_info res = resources.Info(v, supply.resource);

				// only show estimate if the resource is present
				if (res.amount <= double.Epsilon) continue;

				// render panel title, if not done already
				if (supplies == 0) p.AddSection("SUPPLIES");

				// rate tooltip
				string rate_tooltip = Math.Abs(res.rate) >= 1e-10 ? Lib.BuildString
				(
				  res.rate > 0.0 ? "<color=#00ff00><b>" : "<color=#ff0000><b>",
				  Lib.HumanReadableRate(Math.Abs(res.rate)),
				  "</b></color>"
				) : string.Empty;

				// determine label
				string label = supply.resource == "ElectricCharge"
				  ? "battery"
				  : Lib.SpacesOnCaps(supply.resource).ToLower();

				// finally, render resource supply
				p.AddContent(label, Lib.HumanReadableDuration(res.Depletion(vi.crew_count)), rate_tooltip);
				++supplies;
			}
		}


		static void Render_crew(Panel p, List<ProtoCrewMember> crew)
		{
			// do nothing if there isn't a crew, or if there are no rules
			if (crew.Count == 0 || Profile.rules.Count == 0) return;

			// panel section
			p.AddSection("VITALS");

			// for each crew
			foreach (ProtoCrewMember kerbal in crew)
			{
				// get kerbal data from DB
				KerbalData kd = DB.Kerbal(kerbal.name);

				// analyze issues
				UInt32 health_severity = 0;
				UInt32 stress_severity = 0;

				// generate tooltip
				List<string> tooltips = new List<string>();
				foreach (Rule r in Profile.rules)
				{
					// get rule data
					RuleData rd = kd.Rule(r.name);

					// add to the tooltip
					tooltips.Add(Lib.BuildString("<b>", Lib.HumanReadablePerc(rd.problem / r.fatal_threshold), "</b>\t", Lib.SpacesOnCaps(r.name).ToLower()));

					// analyze issue
					if (rd.problem > r.danger_threshold)
					{
						if (!r.breakdown) health_severity = Math.Max(health_severity, 2);
						else stress_severity = Math.Max(stress_severity, 2);
					}
					else if (rd.problem > r.warning_threshold)
					{
						if (!r.breakdown) health_severity = Math.Max(health_severity, 1);
						else stress_severity = Math.Max(stress_severity, 1);
					}
				}
				string tooltip = Lib.BuildString("<align=left />", String.Join("\n", tooltips.ToArray()));

				// generate kerbal name
				string name = kerbal.name.ToLower().Replace(" kerman", string.Empty);

				// render selectable title
				p.AddContent(Lib.Ellipsis(name, Styles.ScaleStringLength(30)), kd.disabled ? "<color=#00ffff>HYBERNATED</color>" : string.Empty);
				p.AddIcon(health_severity == 0 ? Icons.health_white : health_severity == 1 ? Icons.health_yellow : Icons.health_red, tooltip);
				p.AddIcon(stress_severity == 0 ? Icons.brain_white : stress_severity == 1 ? Icons.brain_yellow : Icons.brain_red, tooltip);
			}
		}

		static void Render_greenhouse(Panel p, Vessel_info vi)
		{
			// do nothing without greenhouses
			if (vi.greenhouses.Count == 0) return;

			// panel section
			p.AddSection("GREENHOUSE");

			// for each greenhouse
			for (int i = 0; i < vi.greenhouses.Count; ++i)
			{
				var greenhouse = vi.greenhouses[i];

				// state string
				string state = greenhouse.issue.Length > 0
				  ? Lib.BuildString("<color=yellow>", greenhouse.issue, "</color>")
				  : greenhouse.growth >= 0.99
				  ? "<color=green>ready to harvest</color>"
				  : "growing";

				// tooltip with summary
				string tooltip = greenhouse.growth < 0.99 ? Lib.BuildString
				(
				  "<align=left />",
				  "time to harvest\t<b>", Lib.HumanReadableDuration(greenhouse.tta), "</b>\n",
				  "growth\t\t<b>", Lib.HumanReadablePerc(greenhouse.growth), "</b>\n",
				  "natural lighting\t<b>", Lib.HumanReadableFlux(greenhouse.natural), "</b>\n",
				  "artificial lighting\t<b>", Lib.HumanReadableFlux(greenhouse.artificial), "</b>"
				) : string.Empty;

				// render it
				p.AddContent(Lib.BuildString("crop #", (i + 1).ToString()), state, tooltip);

				// issues too, why not
				p.AddIcon(greenhouse.issue.Length == 0 ? Icons.plant_white : Icons.plant_yellow, tooltip);
			}
		}
	}


} // KERBALISM