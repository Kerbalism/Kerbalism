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

				// calculate radiation at body surface
				double radiation = Radiation.ComputeSurface(body, Sim.GammaTransparency(body, 0.0));

				// surface panel
				string temperature_str = body.atmosphere
				  ? Lib.HumanReadableTemp(temperature)
				  : Lib.BuildString(Lib.HumanReadableTemp(temperature_min), " / ", Lib.HumanReadableTemp(temperature));
				p.AddSection("SURFACE");
				p.AddContent("temperature", temperature_str);
				p.AddContent("solar flux", Lib.HumanReadableFlux(solar_flux));
				if (Features.Radiation) p.AddContent("radiation", Lib.HumanReadableRadiation(radiation));

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
				RadiationLevels(body, out inner, out outer, out pause);

				p.AddContent(Lib.BuildString("inner belt: ", Lib.Color(inner, Lib.KColor.LightGrey)),
					Radiation.show_inner ? Lib.Color("show", Lib.KColor.Green) : Lib.Color("hide", Lib.KColor.Orange), string.Empty, () => p.Toggle(ref Radiation.show_inner));
				p.AddContent(Lib.BuildString("outer belt: ", Lib.Color(outer, Lib.KColor.LightGrey)),
					Radiation.show_outer ? Lib.Color("show", Lib.KColor.Green) : Lib.Color("hide", Lib.KColor.Orange), string.Empty, () => p.Toggle(ref Radiation.show_outer));
				p.AddContent(Lib.BuildString("magnetopause: ", Lib.Color(pause, Lib.KColor.LightGrey)),
					Radiation.show_pause ? Lib.Color("show", Lib.KColor.Green) : Lib.Color("hide", Lib.KColor.Orange), string.Empty, () => p.Toggle(ref Radiation.show_pause));
			}

			// explain the user how to toggle the BodyInfo window
			p.AddContent(string.Empty);
			p.AddContent("<i>Press <b>B</b> to open this window again</i>");

			// set metadata
			p.Title(Lib.BuildString(Lib.Ellipsis(body.bodyName, Styles.ScaleStringLength(24)), " <color=#cccccc>BODY INFO</color>"));
		}

		private static void RadiationLevels(CelestialBody body, out string inner, out string outer, out string pause)
		{
			// TODO cache this information in RadiationBody

			double rad = PreferencesStorm.Instance.externRadiation;
			var rbSun = Radiation.Info(Lib.GetParentSun(body)); // TODO Kopernicus support : not sure if this work with multiple suns/stars
			rad += rbSun.radiation_pause;

			var rb = Radiation.Info(body);

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
		}
	}


} // KERBALISM
