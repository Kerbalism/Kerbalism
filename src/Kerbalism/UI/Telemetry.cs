using System;
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
			p.Title(Lib.BuildString(Lib.Ellipsis(v.vesselName, Styles.ScaleStringLength(20)), " ", Lib.Color(Local.TELEMETRY_title, Lib.Kolor.LightGrey)));//"TELEMETRY"
			p.Width(Styles.ScaleWidthFloat(355.0f));
			p.paneltype = Panel.PanelType.telemetry;

			// time-out simulation
			if (p.Timeout(vd)) return;

			// get resources
			VesselResHandler resources = ResourceCache.GetVesselHandler(v);

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
			if (v.isEVA) p.Collapse(Local.TELEMETRY_EVASUIT);//"EVA SUIT"
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

			p.AddSection(Local.TELEMETRY_ENVIRONMENT);//"ENVIRONMENT"

			if (vd.SolarPanelsAverageExposure >= 0.0)
			{
				var exposureString = vd.SolarPanelsAverageExposure.ToString("P1");
				if (vd.SolarPanelsAverageExposure < 0.2) exposureString = Lib.Color(exposureString, Lib.Kolor.Orange);
				p.AddContent(Local.TELEMETRY_SolarPanelsAverageExposure, exposureString, "<b>"+Local.TELEMETRY_Exposureignoringbodiesocclusion +"</b>\n<i>"+Local.TELEMETRY_Exposureignoringbodiesocclusion_desc +"</i>");//"solar panels average exposure""Exposure ignoring bodies occlusion""Won't change on unloaded vessels\nMake sure to optimize it before switching
			}

			foreach (string type in readings)
			{
				p.AddContent(type.ToLower().Replace('_', ' '), Sensor.Telemetry_content(v, vd, type), Sensor.Telemetry_tooltip(v, vd, type));
			}
			if (readings.Count == 0) p.AddContent("<i>"+Local.TELEMETRY_nosensorsinstalled +"</i>");//no sensors installed
		}

		static void Render_habitat(Panel p, Vessel v, VesselData vd)
		{
			// if habitat feature is disabled, do not show the panel
			if (!Features.Habitat) return;

			// if vessel is unmanned, do not show the panel
			if (vd.CrewCount == 0) return;

			// render panel, add some content based on enabled features
			p.AddSection(Local.TELEMETRY_HABITAT);//"HABITAT"
												  //if (Features.Poisoning) p.AddContent(Local.TELEMETRY_co2level, Lib.Color(vd.Poisoning > Settings.PoisoningThreshold, Lib.HumanReadablePerc(vd.Poisoning, "F2"), Lib.Kolor.Yellow));//"co2 level"
												  //if (Features.Radiation && v.isEVA) p.AddContent(Local.TELEMETRY_radiation, Lib.HumanReadableRadiation(vd.EnvHabitatRadiation));//"radiation"


			p.AddContent("livingVolume", vd.HabitatInfo.livingVolume.ToString("0.00 m3"));
			p.AddContent("volumePerCrew", vd.HabitatInfo.volumePerCrew.ToString("0.00 m3"));
			p.AddContent("livingSpaceModifier", vd.HabitatInfo.livingSpaceModifier.ToString("F2"));
			p.AddContent("pressurizedSurface", vd.HabitatInfo.pressurizedSurface.ToString("0.00 m2"));
			p.AddContent("pressurizedVolume", vd.HabitatInfo.pressurizedVolume.ToString("0.00 m3"));
			p.AddContent("pressureAtm", vd.HabitatInfo.pressureAtm.ToString("0.00 atm"));
			p.AddContent("pressureModifier", vd.HabitatInfo.pressureModifier.ToString("F2"));

			p.AddContent("shieldingSurface", vd.HabitatInfo.shieldingSurface.ToString("0.00 m2"));
			p.AddContent("shieldingAmount", vd.HabitatInfo.shieldingAmount.ToString("F2"));
			p.AddContent("shieldingModifier", vd.HabitatInfo.shieldingModifier.ToString("F2"));
			p.AddContent("poisoningLevel", vd.HabitatInfo.poisoningLevel.ToString("F2"));

			p.AddContent("comfortModifier", vd.HabitatInfo.comfortModifier.ToString("F2"), HabitatLib.ComfortTooltip(vd.HabitatInfo.comfortMask, vd.HabitatInfo.comfortModifier));


			//p.AddContent("volume-debug", Lib.HumanReadableVolume(vd.Volume));
			//p.AddContent("surface-debug", Lib.HumanReadableSurface(vd.Surface));
			//p.AddContent("shielding-debug", vd.Shielding.ToString("F3"));

			//if (!v.isEVA)
			//{
			//	if (Features.Pressure) p.AddContent(Local.TELEMETRY_pressure, Lib.HumanReadablePressure(vd.HabitatPressure * Sim.PressureAtSeaLevel));//"pressure"
			//	if (Features.Shielding) p.AddContent(Local.TELEMETRY_shielding, Radiation.VesselShieldingToString(vd.Shielding));//"shielding"
			//	if (Features.LivingSpace) p.AddContent(Local.TELEMETRY_livingspace, HabitatLib.LivingSpaceFactorToString(vd.LivingSpace));//"living space"
			//	if (Features.Comfort) p.AddContent(Local.TELEMETRY_comfort, HabitatLib.ComfortSummary(vd.ComfortFactor), HabitatLib.ComfortTooltip(vd.ComfortMask, vd.ComfortFactor));//"comfort"
			//	//if (Features.Pressure) p.AddContent(Local.TELEMETRY_EVAsavailable, vd.EnvInSurvivableAtmosphere ? Local.TELEMETRY_EnvBreathable : Lib.HumanReadableInteger(vd.Evas), vd.EnvInSurvivableAtmosphere ? Local.TELEMETRY_Breathableatm : Local.TELEMETRY_approx);//"EVA's available""infinite""breathable atmosphere""approx (derived from stored N2)"
			//}
		}

		static void Render_science(Panel p, Vessel v, VesselData vd)
		{
			// don't show env panel in eva kerbals
			if (v.isEVA) return;

			p.AddSection(Local.TELEMETRY_TRANSMISSION);//"TRANSMISSION"

			// comm status
			if (vd.filesTransmitted.Count > 0)
			{
				double transmitRate = 0.0;
				StringBuilder tooltip = new StringBuilder();
				tooltip.Append(string.Format("<align=left /><b>{0,-15}\t{1}</b>\n", Local.TELEMETRY_TRANSMISSION_rate, Local.TELEMETRY_filetransmitted));//"rate""file transmitted"
				for (int i = 0; i < vd.filesTransmitted.Count; i++)
				{
					transmitRate += vd.filesTransmitted[i].transmitRate;
					tooltip.Append(string.Format("{0,-15}\t{1}", Lib.HumanReadableDataRate(vd.filesTransmitted[i].transmitRate), Lib.Ellipsis(vd.filesTransmitted[i].subjectData.FullTitle, 40u)));
					if (i < vd.filesTransmitted.Count - 1) tooltip.Append("\n");
				}
				
				p.AddContent(Local.TELEMETRY_transmitting, Lib.BuildString(vd.filesTransmitted.Count.ToString(), vd.filesTransmitted.Count > 1 ? " files at " : " file at ",  Lib.HumanReadableDataRate(transmitRate)), tooltip.ToString());//"transmitting"
			}
			else
			{
				p.AddContent(Local.TELEMETRY_maxtransmissionrate, Lib.HumanReadableDataRate(vd.Connection.rate));//"max transmission rate"
			}

			p.AddContent(Local.TELEMETRY_target, vd.Connection.target_name);//"target"

			// total science gained by vessel
			p.AddContent(Local.TELEMETRY_totalsciencetransmitted, Lib.HumanReadableScience(vd.scienceTransmitted, false));//"total science transmitted"
		}

		static void Render_supplies(Panel p, Vessel v, VesselData vd, VesselResHandler resources)
		{
			int supplies = 0;
			// for each supply
			foreach (Supply supply in Profile.supplies)
			{
				// get resource info
				VesselResource res = (VesselResource)resources.GetResource(supply.resource);

				// only show estimate if the resource is present
				if (res.Capacity <= 1e-10) continue;

				// render panel title, if not done already
				if (supplies == 0) p.AddSection(Local.TELEMETRY_SUPPLIES);//"SUPPLIES"

				// determine label
				var resource = PartResourceLibrary.Instance.resourceDefinitions[supply.resource];
				string label = Lib.SpacesOnCaps(resource.displayName).ToLower();

				StringBuilder sb = new StringBuilder();

				sb.Append("\t");
				sb.Append(Lib.Bold(label));
				sb.Append("\n");
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
					sb.Append("<b>");
					sb.Append(Local.TELEMETRY_nochange);//no change
					sb.Append("</b>");
				}

				if (res.AverageRate < 0.0 && res.Level < 0.0001)
				{
					sb.Append(" <i>");
					sb.Append(Local.TELEMETRY_empty);//(empty)
					sb.Append("</i>");
				}
				else if (res.AverageRate > 0.0 && res.Level > 0.9999)
				{
					sb.Append(" <i>");
					sb.Append(Local.TELEMETRY_full);//(full)
					sb.Append("</i>");

				}
				else sb.Append("   "); // spaces to prevent alignement issues

				sb.Append("\t");
				sb.Append(res.Amount.ToString("F1"));
				sb.Append("/");
				sb.Append(res.Capacity.ToString("F1"));
				sb.Append(" (");
				sb.Append(res.Level.ToString("P0"));
				sb.Append(")");

				List<ResourceBrokerRate> brokers = ((VesselResource)ResourceCache.GetResource(v, supply.resource)).ResourceBrokers;
				if (brokers.Count > 0)
				{
					sb.Append("\n<b>------------    \t------------</b>");
					foreach (ResourceBrokerRate rb in brokers)
					{
						sb.Append("\n");
						sb.Append(Lib.Color(rb.rate > 0.0,
							Lib.BuildString("+", Lib.HumanReadableRate(Math.Abs(rb.rate)), "   "), Lib.Kolor.PosRate, // spaces to mitigate alignement issues
							Lib.BuildString("-", Lib.HumanReadableRate(Math.Abs(rb.rate)), "   "), Lib.Kolor.NegRate, // spaces to mitigate alignement issues
							true)); 
						sb.Append("\t");
						sb.Append(rb.broker.Title);
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
			p.AddSection(Local.TELEMETRY_VITALS);//"VITALS"

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
					tooltips.Add(Lib.BuildString("<b>", Lib.HumanReadablePerc(rd.problem / r.fatal_threshold), "</b>\t", r.title));

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
				p.AddContent(Lib.Ellipsis(name, Styles.ScaleStringLength(30)), kd.disabled ? Lib.Color(Local.TELEMETRY_HYBERNATED, Lib.Kolor.Cyan) : string.Empty);//"HYBERNATED"
				p.AddRightIcon(health_severity == 0 ? Textures.health_white : health_severity == 1 ? Textures.health_yellow : Textures.health_red, tooltip);
				p.AddRightIcon(stress_severity == 0 ? Textures.brain_white : stress_severity == 1 ? Textures.brain_yellow : Textures.brain_red, tooltip);
			}
		}

		static void Render_greenhouse(Panel p, VesselData vd)
		{
			// do nothing without greenhouses
			if (vd.Greenhouses.Count == 0) return;

			// panel section
			p.AddSection(Local.TELEMETRY_GREENHOUSE);//"GREENHOUSE"

			// for each greenhouse
			for (int i = 0; i < vd.Greenhouses.Count; ++i)
			{
				var greenhouse = vd.Greenhouses[i];

				// state string
				string state = greenhouse.issue.Length > 0
				  ? Lib.Color(greenhouse.issue, Lib.Kolor.Yellow)
				  : greenhouse.growth >= 0.99
				  ? Lib.Color(Local.TELEMETRY_readytoharvest, Lib.Kolor.Green)//"ready to harvest"
				  : Local.TELEMETRY_growing;//"growing"

				// tooltip with summary
				string tooltip = greenhouse.growth < 0.99 ? Lib.BuildString
				(
				  "<align=left />",
				  Local.TELEMETRY_timetoharvest, "\t<b>", Lib.HumanReadableDuration(greenhouse.tta), "</b>\n",//"time to harvest"
				  Local.TELEMETRY_growth, "\t\t<b>", Lib.HumanReadablePerc(greenhouse.growth), "</b>\n",//"growth"
				  Local.TELEMETRY_naturallighting, "\t<b>", Lib.HumanReadableFlux(greenhouse.natural), "</b>\n",//"natural lighting"
				  Local.TELEMETRY_artificiallighting, "\t<b>", Lib.HumanReadableFlux(greenhouse.artificial), "</b>"//"artificial lighting"
				) : string.Empty;

				// render it
				p.AddContent(Lib.BuildString(Local.TELEMETRY_crop, " #", (i + 1).ToString()), state, tooltip);//"crop"

				// issues too, why not
				p.AddRightIcon(greenhouse.issue.Length == 0 ? Textures.plant_white : Textures.plant_yellow, tooltip);
			}
		}
	}


} // KERBALISM
