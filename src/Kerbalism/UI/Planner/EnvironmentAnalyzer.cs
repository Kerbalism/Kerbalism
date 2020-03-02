using System;

namespace KERBALISM.Planner
{

    ///<summary> Planners simulator for the environment the vessel is presently in, according to the planners environment settings </summary>
    public sealed class EnvironmentAnalyzer
    {
        public void Analyze(CelestialBody body, double altitude_mult, Planner.SunlightState sunlight)
        {
            this.body = body;
            CelestialBody mainSun;
            Vector3d sun_dir;
            solar_flux = Sim.SolarFluxAtBody(body, true, out mainSun, out sun_dir, out sun_dist);
            altitude = body.Radius * altitude_mult;
            landed = altitude <= double.Epsilon;
            atmo_factor = Sim.AtmosphereFactor(body, 0.7071);
            solar_flux = sunlight == Planner.SunlightState.Shadow ? 0.0 : solar_flux * (landed ? atmo_factor : 1.0);
            breathable = Sim.Breathable(body) && landed;
            albedo_flux = sunlight == Planner.SunlightState.Shadow ? 0.0 : Sim.AlbedoFlux(body, body.position + sun_dir * (body.Radius + altitude));
            body_flux = Sim.BodyFlux(body, altitude);
            total_flux = solar_flux + albedo_flux + body_flux + Sim.BackgroundFlux();
            temperature = !landed || !body.atmosphere ? Sim.BlackBodyTemperature(total_flux) : body.GetTemperature(0.0);
            temp_diff = Sim.TempDiff(temperature, body, landed);
            orbital_period = Sim.OrbitalPeriod(body, altitude);
            shadow_period = Sim.ShadowPeriod(body, altitude);
            shadow_time = shadow_period / orbital_period;
            zerog = !landed && (!body.atmosphere || body.atmosphereDepth < altitude);

			CelestialBody homeBody = FlightGlobals.GetHomeBody();
			CelestialBody parentPlanet = Lib.GetParentPlanet(body);

			if (body == homeBody)
			{
				minHomeDistance = maxHomeDistance = Math.Max(altitude, 500.0);
			}
			else if (parentPlanet == homeBody)
			{
				minHomeDistance = Sim.Periapsis(body);
				maxHomeDistance = Sim.Apoapsis(body);
			}
			else if (Lib.IsSun(body))
			{
				minHomeDistance = Math.Abs(altitude - Sim.Periapsis(homeBody));
				maxHomeDistance = altitude + Sim.Apoapsis(homeBody);
			}
			else
			{
				minHomeDistance = Math.Abs(Sim.Periapsis(parentPlanet) - Sim.Periapsis(homeBody));
				maxHomeDistance = Sim.Apoapsis(parentPlanet) + Sim.Apoapsis(homeBody);
			}

			RadiationBody rb = Radiation.Info(body);
            RadiationBody sun_rb = Radiation.Info(mainSun); // TODO Kopernicus support: not sure if/how this work with multiple suns/stars
            gamma_transparency = Sim.GammaTransparency(body, 0.0);

			// add gamma radiation emitted by body and its sun
			var gamma_radiation = Radiation.DistanceRadiation(rb.radiation_r0, altitude) / 3600.0;

#if DEBUG_RADIATION
			Lib.Log("Planner/EA: " + body + " sun " + mainSun + " alt " + altitude + " sol flux " + solar_flux + " aalbedo flux " + albedo_flux + " body flux " + body_flux + " total flux " + total_flux);
			Lib.Log("Planner/EA: body surface radiation " + Lib.HumanReadableRadiation(gamma_radiation, false));
#endif

			var b = body;
			while (b != null && b.orbit != null && b != mainSun)
			{
				if (b == b.referenceBody) break;
				var dist = b.orbit.semiMajorAxis;
				b = b.referenceBody;

				gamma_radiation += Radiation.DistanceRadiation(Radiation.Info(b).radiation_r0, dist) / 3600.0;
#if DEBUG_RADIATION
				Lib.Log("Planner/EA: with gamma radiation from " + b + " " + Lib.HumanReadableRadiation(gamma_radiation, false));
				Lib.Log("Planner/EA: semi major axis " + dist);
#endif
			}

			extern_rad = Settings.ExternRadiation / 3600.0;
            heliopause_rad = gamma_radiation + extern_rad + sun_rb.radiation_pause;
			magnetopause_rad = gamma_radiation + heliopause_rad + rb.radiation_pause;
			inner_rad = gamma_radiation + magnetopause_rad + rb.radiation_inner;
			outer_rad = gamma_radiation + magnetopause_rad + rb.radiation_outer;
			surface_rad = magnetopause_rad * gamma_transparency + rb.radiation_surface / 3600.0;
			storm_rad = heliopause_rad + PreferencesRadiation.Instance.StormRadiation * (solar_flux > double.Epsilon ? 1.0 : 0.0);

#if DEBUG_RADIATION
			Lib.Log("Planner/EA: extern_rad " + Lib.HumanReadableRadiation(extern_rad, false));
			Lib.Log("Planner/EA: heliopause_rad " + Lib.HumanReadableRadiation(heliopause_rad, false));
			Lib.Log("Planner/EA: magnetopause_rad " + Lib.HumanReadableRadiation(magnetopause_rad, false));
			Lib.Log("Planner/EA: inner_rad " + Lib.HumanReadableRadiation(inner_rad, false));
			Lib.Log("Planner/EA: outer_rad " + Lib.HumanReadableRadiation(outer_rad, false));
			Lib.Log("Planner/EA: surface_rad " + Lib.HumanReadableRadiation(surface_rad, false));
			Lib.Log("Planner/EA: storm_rad " + Lib.HumanReadableRadiation(storm_rad, false));
#endif
		}

		public CelestialBody body;                            // target body
        public double altitude;                               // target altitude
		public double minHomeDistance;                        // min distance from KSC
		public double maxHomeDistance;                        // max distance from KSC
		public bool landed;                                   // true if landed
        public bool breathable;                               // true if inside breathable atmosphere
        public bool zerog;                                    // true if the vessel is experiencing zero g
        public double atmo_factor;                            // proportion of sun flux not absorbed by the atmosphere
        public double sun_dist;                               // distance from the sun
        public double solar_flux;                             // flux received from the sun (consider atmospheric absorption)
        public double albedo_flux;                            // solar flux reflected from the body
        public double body_flux;                              // infrared radiative flux from the body
        public double total_flux;                             // total flux at vessel position
        public double temperature;                            // vessel temperature
        public double temp_diff;                              // average difference from survival temperature
        public double orbital_period;                         // length of orbit
        public double shadow_period;                          // length of orbit in shadow
        public double shadow_time;                            // proportion of orbit that is in shadow
        public double gamma_transparency;                     // proportion of radiation not blocked by atmosphere
        public double extern_rad;                             // environment radiation outside the heliopause
        public double heliopause_rad;                         // environment radiation inside the heliopause
        public double magnetopause_rad;                       // environment radiation inside the magnetopause
        public double inner_rad;                              // environment radiation inside the inner belt
        public double outer_rad;                              // environment radiation inside the outer belt
        public double surface_rad;                            // environment radiation on the surface of the body
        public double storm_rad;                              // environment radiation during a solar storm, inside the heliopause
    }


} // KERBALISM
