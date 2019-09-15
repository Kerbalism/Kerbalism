using System;
using System.Collections.Generic;
using UnityEngine;


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
				p.AddSection("SURFACE");
				p.AddContent("temperature", temperature_str);
				p.AddContent("solar flux", Lib.HumanReadableFlux(solar_flux));
				if (Features.Radiation) p.AddContent("radiation", Lib.HumanReadableRadiation(surfaceRadiation));

				// atmosphere panel
				if (body.atmosphere)
				{
					p.AddSection("ATMOSPHERE");
					p.AddContent("breathable", Sim.Breathable(body) ? "yes" : "no");
					p.AddContent("light absorption", Lib.HumanReadablePerc(1.0 - Sim.AtmosphereFactor(body, 0.7071)));
					if (Features.Radiation) p.AddContent("gamma absorption", Lib.HumanReadablePerc(1.0 - Sim.GammaTransparency(body, 0.0)));
				}
			}

			// radiation panel
			if (Features.Radiation)
			{
				p.AddSection("RADIATION");

				string inner, outer, pause;
				double activity, cycle;
				RadiationLevels(body, out inner, out outer, out pause, out activity, out cycle);

				if (Storm.sun_observation_quality > 0.5 && activity > -1)
				{
					string title = "solar activity";

					if(Storm.sun_observation_quality > 0.7)
					{
						title = Lib.BuildString(title, ": ", Lib.Color(Lib.HumanReadableDuration(cycle) + " cycle", Lib.KColor.LightGrey));
					}

					p.AddContent(title, Lib.HumanReadablePerc(activity));
				}

				if (Storm.sun_observation_quality > 0.8)
				{
					p.AddContent("radiation on surface:", Lib.HumanReadableRadiation(surfaceRadiation));
				}

				p.AddContent(Lib.BuildString("inner belt: ", Lib.Color(inner, Lib.KColor.LightGrey)),
					Radiation.show_inner ? Lib.Color("show", Lib.KColor.Green) : Lib.Color("hide", Lib.KColor.Red), string.Empty, () => p.Toggle(ref Radiation.show_inner));
				p.AddContent(Lib.BuildString("outer belt: ", Lib.Color(outer, Lib.KColor.LightGrey)),
					Radiation.show_outer ? Lib.Color("show", Lib.KColor.Green) : Lib.Color("hide", Lib.KColor.Red), string.Empty, () => p.Toggle(ref Radiation.show_outer));
				p.AddContent(Lib.BuildString("magnetopause: ", Lib.Color(pause, Lib.KColor.LightGrey)),
					Radiation.show_pause ? Lib.Color("show", Lib.KColor.Green) : Lib.Color("hide", Lib.KColor.Red), string.Empty, () => p.Toggle(ref Radiation.show_pause));
			}

			// explain the user how to toggle the BodyInfo window
			p.AddContent(string.Empty);
			p.AddContent("<i>Press <b>B</b> to open this window again</i>");

			// set metadata
			p.Title(Lib.BuildString(Lib.Ellipsis(body.bodyName, Styles.ScaleStringLength(24)), " ", Lib.Color("BODY INFO", Lib.KColor.LightGrey)));
		}

		private static void RadiationLevels(CelestialBody body, out string inner, out string outer, out string pause, out double activity, out double cycle)
		{
			// TODO cache this information somewhere

			var rb = Radiation.Info(body);
			double rad = Settings.ExternRadiation / 3600.0;
			var rbSun = Radiation.Info(Lib.GetParentSun(body));
			rad += rbSun.radiation_pause;

			if (rb.inner_visible)
				inner = rb.model.has_inner ? "~" + Lib.HumanReadableRadiation(Math.Max(0, rad + rb.radiation_inner) / 3600.0) : "n/a";
			else
				inner = "unknown";

			if (rb.outer_visible)
				outer = rb.model.has_outer ? "~" + Lib.HumanReadableRadiation(Math.Max(0, rad + rb.radiation_outer) / 3600.0) : "n/a";
			else
				outer = "unknown";

			if (rb.pause_visible)
				pause = rb.model.has_pause ? "~" + Lib.HumanReadableRadiation(Math.Max(0, rad + rb.radiation_pause) / 3600.0) : "n/a";
			else
				pause = "unknown";

			activity = -1;
			cycle = rb.solar_cycle;
			if(cycle > 0) activity = rb.SolarActivity();
		}
	}


} // KERBALISM
