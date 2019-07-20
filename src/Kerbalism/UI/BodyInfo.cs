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
			CelestialBody body = Lib.SelectedBody();
			if (body == null || (Lib.IsSun(body) && !Features.Radiation)) return;

			// shortcut
			CelestialBody sun = Lib.GetSun(body);

			// for all bodies except the sun
			if (body != sun)
			{
				// calculate simulation values
				double atmo_factor = Sim.AtmosphereFactor(body, 0.7071);
				double gamma_factor = Sim.GammaTransparency(body, 0.0);
				double sun_dist = Sim.Apoapsis(Lib.PlanetarySystem(body)) - sun.Radius - body.Radius;
				Vector3d sun_dir = (sun.position - body.position).normalized;
				double solar_flux = Sim.SolarFlux(sun_dist, sun) * atmo_factor;
				double albedo_flux = Sim.AlbedoFlux(body, body.position + sun_dir * body.Radius);
				double body_flux = Sim.BodyFlux(body, 0.0);
				double total_flux = solar_flux + albedo_flux + body_flux + Sim.BackgroundFlux();
				double temperature = body.atmosphere ? body.GetTemperature(0.0) : Sim.BlackBodyTemperature(total_flux);

				// calculate night-side temperature
				double total_flux_min = Sim.AlbedoFlux(body, body.position - sun_dir * body.Radius) + body_flux + Sim.BackgroundFlux();
				double temperature_min = Sim.BlackBodyTemperature(total_flux_min);

				// calculate radiation at body surface
				double radiation = Radiation.ComputeSurface(body, gamma_factor);

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

				p.AddContent(Lib.BuildString("inner belt: ", Lib.Color("#cccccc", inner)),
					Radiation.show_inner ? "<color=green>show</color>" : "<color=red>hide</color>", string.Empty, () => p.Toggle(ref Radiation.show_inner));
				p.AddContent(Lib.BuildString("outer belt: ", Lib.Color("#cccccc", outer)),
					Radiation.show_outer ? "<color=green>show</color>" : "<color=red>hide</color>", string.Empty, () => p.Toggle(ref Radiation.show_outer));
				p.AddContent(Lib.BuildString("magnetopause: ", Lib.Color("#cccccc", pause)),
					Radiation.show_pause ? "<color=green>show</color>" : "<color=red>hide</color>", string.Empty, () => p.Toggle(ref Radiation.show_pause));
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
			var rbSun = Radiation.Info(Lib.GetSun(body));
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