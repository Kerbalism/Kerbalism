﻿using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using KSP.Localization;

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

			// get vessel data
			VesselData vd = v.KerbalismData();

			// if not a valid vessel, leave the panel empty
			if (!vd.IsSimulated) return;

			// set metadata
			p.Title(Lib.BuildString(Lib.Ellipsis(v.vesselName, Styles.ScaleStringLength(20)), " ", Lib.Color("TELEMETRY", Lib.Kolor.LightGrey)));
			p.Width(Styles.ScaleWidthFloat(355.0f));
			p.paneltype = Panel.PanelType.telemetry;

			// time-out simulation
			if (p.Timeout(vd)) return;

			// get resources
			VesselResources resources = ResourceCache.Get(v);

			// get crew
			var crew = Lib.CrewList(v);

			// draw the content
			Render_crew(p, crew);
			if (Features.Science) Render_science(p, v, vd);
			Render_greenhouse(p, vd);
			Render_supplies(p, v, vd, resources);
			Render_habitat(p, v, vd);
			Render_environment(p, v, vd);

			// collapse eva kerbal sections into one
			if (v.isEVA) p.Collapse("EVA SUIT");
		}


		static void Render_environment(Panel p, Vessel v, VesselData vd)
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

			if (vd.SolarPanelsAverageExposure >= 0.0)
			{
				var exposureString = vd.SolarPanelsAverageExposure.ToString("P1");
				if (vd.SolarPanelsAverageExposure < 0.2) exposureString = Lib.Color(exposureString, Lib.Kolor.Orange);
				p.AddContent("solar panels average exposure", exposureString, "<b>Exposure ignoring bodies occlusion</b>\n<i>Won't change on unloaded vessels\nMake sure to optimize it before switching</i>");
			}

			foreach (string type in readings)
			{
				p.AddContent(type.ToLower().Replace('_', ' '), Sensor.Telemetry_content(v, vd, type), Sensor.Telemetry_tooltip(v, vd, type));
			}
			if (readings.Count == 0) p.AddContent("<i>no sensors installed</i>");
		}

		static void Render_habitat(Panel p, Vessel v, VesselData vd)
		{
			// if habitat feature is disabled, do not show the panel
			if (!Features.Habitat) return;

			// if vessel is unmanned, do not show the panel
			if (vd.CrewCount == 0) return;

			// render panel, add some content based on enabled features
			p.AddSection("HABITAT");
			if (Features.Poisoning) p.AddContent("co2 level", Lib.Color(vd.Poisoning > Settings.PoisoningThreshold, Lib.HumanReadablePerc(vd.Poisoning, "F2"), Lib.Kolor.Yellow));
			if (Features.Radiation && v.isEVA) p.AddContent("radiation", Lib.HumanReadableRadiation(vd.EnvHabitatRadiation));

			if (!v.isEVA)
			{
				if (Features.Pressure) p.AddContent("pressure", Lib.HumanReadablePressure(vd.Pressure * Sim.PressureAtSeaLevel()));
				if (Features.Shielding) p.AddContent("shielding", Habitat.Shielding_to_string(vd.Shielding));
				if (Features.LivingSpace) p.AddContent("living space", Habitat.Living_space_to_string(vd.LivingSpace));
				if (Features.Comfort) p.AddContent("comfort", vd.Comforts.Summary(), vd.Comforts.Tooltip());
				if (Features.Pressure) p.AddContent("EVA's available", vd.EnvBreathable ? "infinite" : Lib.HumanReadableInteger(vd.Evas), vd.EnvBreathable ? "breathable atmosphere" : "approx (derived from stored N2)");
			}
		}

		static void Render_science(Panel p, Vessel v, VesselData vd)
		{
			// don't show env panel in eva kerbals
			if (v.isEVA) return;

			p.AddSection("TRANSMISSION");

			// comm status
			if (vd.filesTransmitted.Count > 0)
			{
				double transmitRate = 0.0;
				StringBuilder tooltip = new StringBuilder();
				tooltip.Append(string.Format("<align=left /><b>{0,-15}\t{1}</b>\n", "rate", "file transmitted"));
				for (int i = 0; i < vd.filesTransmitted.Count; i++)
				{
					transmitRate += vd.filesTransmitted[i].transmitRate;
					tooltip.Append(string.Format("{0,-15}\t{1}", Lib.HumanReadableDataRate(vd.filesTransmitted[i].transmitRate), Lib.Ellipsis(vd.filesTransmitted[i].subjectData.FullTitle, 40u)));
					if (i < vd.filesTransmitted.Count - 1) tooltip.Append("\n");
				}
				
				p.AddContent("transmitting", Lib.BuildString(vd.filesTransmitted.Count.ToString(), vd.filesTransmitted.Count > 1 ? " files at " : " file at ",  Lib.HumanReadableDataRate(transmitRate)), tooltip.ToString());
			}
			else
			{
				p.AddContent("max transmission rate", Lib.HumanReadableDataRate(vd.Connection.rate));
			}

			p.AddContent("target", vd.Connection.target_name);

			// total science gained by vessel
			p.AddContent("total science transmitted", Lib.HumanReadableScience(vd.scienceTransmitted, false));
		}

		static void Render_supplies(Panel p, Vessel v, VesselData vd, VesselResources resources)
		{
			int supplies = 0;
			// for each supply
			foreach (Supply supply in Profile.supplies)
			{
				// get resource info
				ResourceInfo res = resources.GetResource(v, supply.resource);

				// only show estimate if the resource is present
				if (res.Capacity <= 1e-10) continue;

				// render panel title, if not done already
				if (supplies == 0) p.AddSection("SUPPLIES");

				// determine label
				var resource = PartResourceLibrary.Instance.resourceDefinitions[supply.resource];
				string label = Lib.SpacesOnCaps(resource.displayName).ToLower();

				StringBuilder sb = new StringBuilder();
				
				sb.Append("<align=left />");
				if (res.AverageRate != 0.0)
				{
					sb.Append(Lib.Color(res.AverageRate > 0.0,
						Lib.BuildString("+", Lib.HumanReadableRate(Math.Abs(res.AverageRate))), Lib.Kolor.PosRate,
						Lib.BuildString("-", Lib.HumanReadableRate(Math.Abs(res.AverageRate))), Lib.Kolor.NegRate,
						true));
				}
				else
				{
					sb.Append("<b>no change</b>");
				}

				if (res.AverageRate < 0.0 && res.Level < 0.0001) sb.Append(" <i>(empty)</i>");
				else if (res.AverageRate > 0.0 && res.Level > 0.9999) sb.Append(" <i>(full)</i>");
				else sb.Append("   "); // spaces to prevent alignement issues

				sb.Append("\t");
				sb.Append(res.Amount.ToString("F1"));
				sb.Append("/");
				sb.Append(res.Capacity.ToString("F1"));
				sb.Append(" (");
				sb.Append(res.Level.ToString("P0"));
				sb.Append(")");

				List<SupplyData.ResourceBroker> brokers = vd.Supply(supply.resource).ResourceBrokers;
				if (brokers.Count > 0)
				{
					sb.Append("\n<b>------------    \t------------</b>");
					foreach (SupplyData.ResourceBroker rb in brokers)
					{
						sb.Append("\n");
						sb.Append(Lib.Color(rb.rate > 0.0,
							Lib.BuildString("+", Lib.HumanReadableRate(Math.Abs(rb.rate)), "   "), Lib.Kolor.PosRate, // spaces to mitigate alignement issues
							Lib.BuildString("-", Lib.HumanReadableRate(Math.Abs(rb.rate)), "   "), Lib.Kolor.NegRate, // spaces to mitigate alignement issues
							true)); 
						sb.Append("\t");
						sb.Append(rb.name);
					}
				}

				string rate_tooltip = sb.ToString();

				// finally, render resource supply
				p.AddContent(label, Lib.HumanReadableDuration(res.DepletionTime()), rate_tooltip);
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
				p.AddContent(Lib.Ellipsis(name, Styles.ScaleStringLength(30)), kd.disabled ? Lib.Color("HYBERNATED", Lib.Kolor.Cyan) : string.Empty);
				p.AddRightIcon(health_severity == 0 ? Textures.health_white : health_severity == 1 ? Textures.health_yellow : Textures.health_red, tooltip);
				p.AddRightIcon(stress_severity == 0 ? Textures.brain_white : stress_severity == 1 ? Textures.brain_yellow : Textures.brain_red, tooltip);
			}
		}

		static void Render_greenhouse(Panel p, VesselData vd)
		{
			// do nothing without greenhouses
			if (vd.Greenhouses.Count == 0) return;

			// panel section
			p.AddSection("GREENHOUSE");

			// for each greenhouse
			for (int i = 0; i < vd.Greenhouses.Count; ++i)
			{
				var greenhouse = vd.Greenhouses[i];

				// state string
				string state = greenhouse.issue.Length > 0
				  ? Lib.Color(greenhouse.issue, Lib.Kolor.Yellow)
				  : greenhouse.growth >= 0.99
				  ? Lib.Color("ready to harvest", Lib.Kolor.Green)
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
				p.AddRightIcon(greenhouse.issue.Length == 0 ? Textures.plant_white : Textures.plant_yellow, tooltip);
			}
		}
	}


} // KERBALISM
