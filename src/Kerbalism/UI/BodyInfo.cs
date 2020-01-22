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
			if (body == null || (Lib.IsSun(body) && !Features.Radiation)) return;

			// calculate radiation at body surface
			double surfaceRadiation = Radiation.ComputeSurface(body, Sim.GammaTransparency(body, 0.0));

			// for all bodies except sun(s)
			if (!Lib.IsSun(body))
			{
				CelestialBody mainSun;
				Vector3d sun_dir;
				double sun_dist;
				double solar_flux = Sim.SolarFluxAtBody(body, false, out mainSun, out sun_dir, out sun_dist);
				solar_flux *= Sim.AtmosphereFactor(body, 0.7071);

				// calculate simulation values
				double albedo_flux = Sim.AlbedoFlux(body, body.position + sun_dir * body.Radius);
				double body_flux = Sim.BodyFlux(body, 0.0);
				double total_flux = solar_flux + albedo_flux + body_flux + Sim.BackgroundFlux();
				double temperature = body.atmosphere ? body.GetTemperature(0.0) : Sim.BlackBodyTemperature(total_flux);

				// calculate night-side temperature
				double total_flux_min = Sim.AlbedoFlux(body, body.position - sun_dir * body.Radius) + body_flux + Sim.BackgroundFlux();
				double temperature_min = Sim.BlackBodyTemperature(total_flux_min);

				// surface panel
				string temperature_str = body.atmosphere
				  ? Lib.HumanReadableTemp(temperature)
				  : Lib.BuildString(Lib.HumanReadableTemp(temperature_min), " / ", Lib.HumanReadableTemp(temperature));
				p.AddSection(Local.BodyInfo_SURFACE);//"SURFACE"
				p.AddContent(Local.BodyInfo_temperature, temperature_str);//"temperature"
				p.AddContent(Local.BodyInfo_solarflux, Lib.HumanReadableFlux(solar_flux));//"solar flux"
				if (Features.Radiation) p.AddContent(Local.BodyInfo_radiation, Lib.HumanReadableRadiation(surfaceRadiation));//"radiation"

				// atmosphere panel
				if (body.atmosphere)
				{
					p.AddSection(Local.BodyInfo_ATMOSPHERE);//"ATMOSPHERE"
					p.AddContent(Local.BodyInfo_breathable, Sim.Breathable(body) ? Local.BodyInfo_breathable_yes : Local.BodyInfo_breathable_no);//"breathable""yes""no"
					p.AddContent(Local.BodyInfo_lightabsorption, Lib.HumanReadablePerc(1.0 - Sim.AtmosphereFactor(body, 0.7071)));//"light absorption"
					if (Features.Radiation) p.AddContent(Local.BodyInfo_gammaabsorption, Lib.HumanReadablePerc(1.0 - Sim.GammaTransparency(body, 0.0)));//"gamma absorption"
				}
			}

			// radiation panel
			if (Features.Radiation)
			{
				p.AddSection(Local.BodyInfo_RADIATION);//"RADIATION"

				string inner, outer, pause;
				double activity, cycle;
				RadiationLevels(body, out inner, out outer, out pause, out activity, out cycle);

				if (Storm.sun_observation_quality > 0.5 && activity > -1)
				{
					string title = Local.BodyInfo_solaractivity;//"solar activity"

					if(Storm.sun_observation_quality > 0.7)
					{
						title = Lib.BuildString(title, ": ", Lib.Color(Localizer.Format("#KERBALISM_BodyInfo_stormcycle", Lib.HumanReadableDuration(cycle)), Lib.Kolor.LightGrey));// <<1>> cycle
					}

					p.AddContent(title, Lib.HumanReadablePerc(activity));
				}

				if (Storm.sun_observation_quality > 0.8)
				{
					p.AddContent(Local.BodyInfo_radiationonsurface, Lib.HumanReadableRadiation(surfaceRadiation));//"radiation on surface:"
				}

				p.AddContent(Lib.BuildString(Local.BodyInfo_innerbelt , " ", Lib.Color(inner, Lib.Kolor.LightGrey)),//"inner belt: "
					Radiation.show_inner ? Lib.Color(Local.BodyInfo_show, Lib.Kolor.Green) : Lib.Color(Local.BodyInfo_hide, Lib.Kolor.Red), string.Empty, () => p.Toggle(ref Radiation.show_inner));//"show""hide"
				p.AddContent(Lib.BuildString(Local.BodyInfo_outerbelt , " ", Lib.Color(outer, Lib.Kolor.LightGrey)),//"outer belt: "
					Radiation.show_outer ? Lib.Color(Local.BodyInfo_show, Lib.Kolor.Green) : Lib.Color(Local.BodyInfo_hide, Lib.Kolor.Red), string.Empty, () => p.Toggle(ref Radiation.show_outer));//"show""hide"
				p.AddContent(Lib.BuildString(Local.BodyInfo_magnetopause , " ", Lib.Color(pause, Lib.Kolor.LightGrey)),//"magnetopause: "
					Radiation.show_pause ? Lib.Color(Local.BodyInfo_show, Lib.Kolor.Green) : Lib.Color(Local.BodyInfo_hide, Lib.Kolor.Red), string.Empty, () => p.Toggle(ref Radiation.show_pause));//"show""hide"
			}

			// explain the user how to toggle the BodyInfo window
			p.AddContent(string.Empty);
			p.AddContent("<i>" + Localizer.Format("#KERBALISM_BodyInfo_BodyInfoToggleHelp", "<b>B</b>") + "</i>");//"Press <<1>> to open this window again"

			// set metadata
			p.Title(Lib.BuildString(Lib.Ellipsis(body.bodyName, Styles.ScaleStringLength(24)), " ", Lib.Color(Local.BodyInfo_title, Lib.Kolor.LightGrey)));//"BODY INFO"
		}

		private static void RadiationLevels(CelestialBody body, out string inner, out string outer, out string pause, out double activity, out double cycle)
		{
			// TODO cache this information somewhere

			var rb = Radiation.Info(body);
			double rad = Settings.ExternRadiation / 3600.0;
			var rbSun = Radiation.Info(Lib.GetParentSun(body));
			rad += rbSun.radiation_pause;

			if (rb.inner_visible)
				inner = rb.model.has_inner ? "~" + Lib.HumanReadableRadiation(Math.Max(0, rad + rb.radiation_inner)) : "n/a";
			else
				inner = Local.BodyInfo_unknown;//"unknown"

			if (rb.outer_visible)
				outer = rb.model.has_outer ? "~" + Lib.HumanReadableRadiation(Math.Max(0, rad + rb.radiation_outer)) : "n/a";
			else
				outer = Local.BodyInfo_unknown;//"unknown"

			if (rb.pause_visible)
				pause = rb.model.has_pause ? "∆" + Lib.HumanReadableRadiation(Math.Abs(rb.radiation_pause)) : "n/a";
			else
				pause = Local.BodyInfo_unknown;//"unknown"

			activity = -1;
			cycle = rb.solar_cycle;
			if(cycle > 0) activity = rb.SolarActivity();
		}
	}


} // KERBALISM
