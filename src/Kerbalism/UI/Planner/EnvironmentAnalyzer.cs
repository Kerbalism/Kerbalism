namespace KERBALISM.Planner
{

	///<summary> Planners simulator for the environment the vessel is presently in, according to the planners environment settings </summary>
	public sealed class EnvironmentAnalyzer
	{
		public void Analyze(CelestialBody body, double altitude_mult, bool sunlight)
		{
			// shortcuts
			CelestialBody sun = Lib.GetSun(body);

			this.body = body;
			altitude = body.Radius * altitude_mult;
			landed = altitude <= double.Epsilon;
			breathable = Sim.Breathable(body) && landed;
			atmo_factor = Sim.AtmosphereFactor(body, 0.7071);
			sun_dist = Sim.Apoapsis(Lib.PlanetarySystem(body)) - sun.Radius - body.Radius;
			Vector3d sun_dir = (sun.position - body.position).normalized;
			solar_flux = sunlight ? Sim.SolarFlux(sun_dist, sun) * (landed ? atmo_factor : 1.0) : 0.0;
			albedo_flux = sunlight ? Sim.AlbedoFlux(body, body.position + sun_dir * (body.Radius + altitude)) : 0.0;
			body_flux = Sim.BodyFlux(body, altitude);
			total_flux = solar_flux + albedo_flux + body_flux + Sim.BackgroundFlux();
			temperature = !landed || !body.atmosphere ? Sim.BlackBodyTemperature(total_flux) : body.GetTemperature(0.0);
			temp_diff = Sim.TempDiff(temperature, body, landed);
			orbital_period = Sim.OrbitalPeriod(body, altitude);
			shadow_period = Sim.ShadowPeriod(body, altitude);
			shadow_time = shadow_period / orbital_period;
			zerog = !landed && (!body.atmosphere || body.atmosphereDepth < altitude);

			RadiationBody rb = Radiation.Info(body);
			RadiationBody sun_rb = Radiation.Info(sun);
			gamma_transparency = Sim.GammaTransparency(body, 0.0);
			extern_rad = PreferencesStorm.Instance.ExternRadiation;
			heliopause_rad = extern_rad + sun_rb.radiation_pause;
			magnetopause_rad = heliopause_rad + rb.radiation_pause;
			inner_rad = magnetopause_rad + rb.radiation_inner;
			outer_rad = magnetopause_rad + rb.radiation_outer;
			surface_rad = magnetopause_rad * gamma_transparency;
			storm_rad = heliopause_rad + PreferencesStorm.Instance.StormRadiation * (solar_flux > double.Epsilon ? 1.0 : 0.0);
		}


		public CelestialBody body;                            // target body
		public double altitude;                               // target altitude
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
