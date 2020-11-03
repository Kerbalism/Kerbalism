using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{
	public static class BodyInfo
	{
		public static void Body_info(this Panel p)
		{
			// only show in mapview
			if (!MapView.MapIsEnabled) return;

			// only show if there is a selected body and that body is not the sun
			CelestialBody body = Lib.MapViewSelectedBody();
			if (body == null || (Sim.IsStar(body) && !Features.Radiation)) return;

			// calculate radiation at body surface
			double surfaceRadiation = Radiation.ComputeSurface(body, Sim.GammaTransparency(body, 0.0));

			// for all bodies except sun(s)
			bool isStar = Sim.IsStar(body);
			if (!isStar)
			{
				SimBody simBody = Sim.Bodies[body.flightGlobalsIndex];

				simBody.GetCurrentThermalStats(
					out double equilibriumTemperature,
					out double dayTemperature,
					out double nightTemperature,
					out double polarTemperature,
					out double rawIrradiance,
					out double surfaceIrradiance);

				// surface panel
				p.AddSection(Local.BodyInfo_SURFACE);//"SURFACE"
				p.AddContent("raw solar irradiance", Lib.HumanReadableIrradiance(rawIrradiance));
				if (simBody.coreThermalFlux != 0.0) p.AddContent("core thermal irradiance", Lib.HumanReadableIrradiance(simBody.coreThermalFlux));
				p.AddContent("bond albedo", simBody.albedo.ToString("F2"));
				p.AddContent("geometric albedo", simBody.geometricAlbedo.ToString("F2"));
				p.AddContent("equilibrium temperature", Lib.HumanReadableTemp(equilibriumTemperature));
				if (!body.atmosphere)
				{
					p.AddContent("day surface temperature", Lib.HumanReadableTemp(dayTemperature));
					p.AddContent("night surface temperature", Lib.HumanReadableTemp(nightTemperature));
				}

				// atmosphere panel
				if (body.atmosphere)
				{
					p.AddSection(Local.BodyInfo_ATMOSPHERE);//"ATMOSPHERE"
					p.AddContent(Local.BodyInfo_breathable, Sim.Breathable(body) ? Local.BodyInfo_breathable_yes : Local.BodyInfo_breathable_no);//"breathable""yes""no"
					p.AddContent(Local.BodyInfo_lightabsorption, Lib.HumanReadablePerc(1.0 - simBody.atmoAverageLightTransparency));//"light absorption"
					if (Features.Radiation) p.AddContent(Local.BodyInfo_gammaabsorption, Lib.HumanReadablePerc(1.0 - Sim.GammaTransparency(body, 0.0)));//"gamma absorption"
					p.AddContent("greenhouse effect", Lib.HumanReadableTemp(simBody.atmoGreenhouseTempOffset));
					p.AddContent("surface solar irradiance", Lib.HumanReadableIrradiance(surfaceIrradiance));
					p.AddContent("day equatorial temperature", Lib.HumanReadableTemp(dayTemperature));
					p.AddContent("night equatorial temperature", Lib.HumanReadableTemp(nightTemperature));
					p.AddContent("polar temperature", Lib.HumanReadableTemp(polarTemperature));

				}
			}

			if (Features.Radiation)
			{
				p.AddSection(Local.BodyInfo_RADIATION);//"RADIATION"

				string inner, outer, pause;
				double activity, cycle;
				RadiationLevels(body, out inner, out outer, out pause, out activity, out cycle);

				if (isStar)
				{
					var quality = Storm.SunObservationQuality(body);

					if (quality > 0.6 && activity > -1)
					{
						string title = Local.BodyInfo_solaractivity;//"solar activity"

						if (quality > 0.8)
							title = Lib.BuildString(title, ": ", Lib.Color(Local.BodyInfo_stormcycle.Format(Lib.HumanReadableDuration(cycle)), Lib.Kolor.LightGrey));// <<1>> cycle

						p.AddContent(title, Lib.HumanReadablePerc(activity));
					}

					if (quality > 0.9)
						p.AddContent(Local.BodyInfo_radiationonsurface, Lib.HumanReadableRadiation(surfaceRadiation));//"radiation on surface:"
				}
				else
				{
					// TODO show this only if we've landed on the body
					p.AddContent(Local.BodyInfo_radiationonsurface, Lib.HumanReadableRadiation(surfaceRadiation));//"radiation on surface:"
				}

				if (inner.Length > 0)
				{
					p.AddContent(Lib.BuildString(Local.BodyInfo_innerbelt, "\t", Lib.Bold(inner)),//"inner belt: "
					Radiation.show_inner ? Lib.Color(Local.BodyInfo_show, Lib.Kolor.Green) : Lib.Color(Local.BodyInfo_hide, Lib.Kolor.Red), string.Empty, () => p.Toggle(ref Radiation.show_inner));//"show""hide"
				}

				if (outer.Length > 0)
				{
					p.AddContent(Lib.BuildString(Local.BodyInfo_outerbelt, "\t", Lib.Bold(outer)),//"outer belt: "
					Radiation.show_outer ? Lib.Color(Local.BodyInfo_show, Lib.Kolor.Green) : Lib.Color(Local.BodyInfo_hide, Lib.Kolor.Red), string.Empty, () => p.Toggle(ref Radiation.show_outer));//"show""hide"
				}

				if (pause.Length > 0)
				{
					p.AddContent(Lib.BuildString(Local.BodyInfo_magnetopause, "\t", Lib.Bold(pause)),//"magnetopause: "
					Radiation.show_pause ? Lib.Color(Local.BodyInfo_show, Lib.Kolor.Green) : Lib.Color(Local.BodyInfo_hide, Lib.Kolor.Red), string.Empty, () => p.Toggle(ref Radiation.show_pause));//"show""hide"
				}
			}

			// explain the user how to toggle the BodyInfo window
			p.AddContent(string.Empty);
			p.AddContent("<i>" + Local.BodyInfo_BodyInfoToggleHelp.Format("<b>B</b>") + "</i>");//"Press <<1>> to open this window again"

			// set metadata
			p.Title(Lib.BuildString(Lib.Ellipsis(body.bodyName, Styles.ScaleStringLength(24)), " ", Lib.Color(Local.BodyInfo_title, Lib.Kolor.LightGrey)));//"BODY INFO"
		}

		private static void RadiationLevels(CelestialBody body, out string inner, out string outer, out string pause, out double activity, out double cycle)
		{
			// TODO cache this information somewhere

			var rb = Radiation.Info(body);
			double rad = Settings.ExternRadiation / 3600.0;
			var rbSun = Radiation.Info(Sim.GetParentStar(body));
			rad += rbSun.radiation_pause;

			if (rb.inner_visible)
				inner = rb.model.has_inner ? "<b>~</b> " + Lib.HumanReadableRadiation(Math.Max(0, rad + rb.radiation_inner)) : string.Empty;
			else
				inner = Local.BodyInfo_unknown;//"unknown"

			if (rb.outer_visible)
				outer = rb.model.has_outer ? "<b>~</b> " + Lib.HumanReadableRadiation(Math.Max(0, rad + rb.radiation_outer)) : string.Empty;
			else
				outer = Local.BodyInfo_unknown;//"unknown"

			if (rb.pause_visible)
				pause = rb.model.has_pause ? "<b>∆</b> " + Lib.HumanReadableRadiation(Math.Abs(rb.radiation_pause)) : string.Empty;
			else
				pause = Local.BodyInfo_unknown;//"unknown"

			activity = -1;
			cycle = rb.solar_cycle;
			if(cycle > 0) activity = rb.SolarActivity();
		}
	}


} // KERBALISM
