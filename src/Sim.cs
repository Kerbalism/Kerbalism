using System;


namespace KERBALISM {


public static class Sim
{
  // --------------------------------------------------------------------------
  // GENERAL
  // --------------------------------------------------------------------------

  // return period of an orbit at specified altitude over a body
  public static double OrbitalPeriod(CelestialBody body, double altitude)
  {
    if (altitude <= double.Epsilon) return body.rotationPeriod;
    double Ra = altitude + body.Radius;
    return 2.0 * Math.PI * Math.Sqrt(Ra * Ra * Ra / body.gravParameter);
  }


  // return period in shadow of an orbit at specified altitude over a body
  public static double ShadowPeriod(CelestialBody body, double altitude)
  {
    if (altitude <= double.Epsilon) return body.rotationPeriod * 0.5;
    double Ra = altitude + body.Radius;
    double h = Math.Sqrt(Ra * body.gravParameter);
    return (2.0 * Ra * Ra / h) * Math.Asin(body.Radius / Ra);
  }

  // return orbital period of the specified vessel
  public static double OrbitalPeriod(Vessel v)
  {
    if (Lib.Landed(v) || double.IsNaN(v.orbit.inclination))
    {
      return v.mainBody.rotationPeriod;
    }
    else
    {
      return v.orbit.period;
    }
  }

  // return period in shadow of the specified vessel orbit
  public static double ShadowPeriod(Vessel v)
  {
    if (Lib.Landed(v) || double.IsNaN(v.orbit.inclination))
    {
      return v.mainBody.rotationPeriod * 0.5;
    }
    else
    {
      double Ra = v.altitude + v.mainBody.Radius;
      double h = Math.Sqrt(Ra * v.mainBody.gravParameter);
      return (2.0 * Ra * Ra / h) * Math.Asin(v.mainBody.Radius / Ra);
    }
  }


  // return rotation speed at body surface
  public static double SurfaceSpeed(CelestialBody body)
  {
    return 2.0 * Math.PI * body.Radius / body.rotationPeriod;
  }

  // return gravity at body surface
  public static double SurfaceGravity(CelestialBody body)
  {
    return body.gravParameter / (body.Radius * body.Radius);
  }


  // return apoapsis of a body orbit
  public static double Apoapsis(CelestialBody body)
  {
    return (1.0 + body.orbit.eccentricity) * body.orbit.semiMajorAxis;
  }


  // return periapsis of a body orbit
  public static double Periapsis(CelestialBody body)
  {
    return (1.0 - body.orbit.eccentricity) * body.orbit.semiMajorAxis;
  }


  // return apoapsis of a vessel orbit
  public static double Apoapsis(Vessel v)
  {
    if (double.IsNaN(v.orbit.inclination)) return 0.0;
    return (1.0 + v.orbit.eccentricity) * v.orbit.semiMajorAxis;
  }


  // return periapsis of a vessel orbit
  public static double Periapsis(Vessel v)
  {
    if (double.IsNaN(v.orbit.inclination)) return 0.0;
    return (1.0 - v.orbit.eccentricity) * v.orbit.semiMajorAxis;
  }


  // get distance from the sun
  public static double SunDistance(Vector3d pos)
  {
    CelestialBody sun = FlightGlobals.Bodies[0];
    return Vector3d.Distance(pos, sun.position) - sun.Radius;
  }


  // --------------------------------------------------------------------------
  // RAYTRACING
  // --------------------------------------------------------------------------

  // return true if the ray 'dir' starting at 'p' and of length 'dist' doesn't hit 'body'
  // - p: ray origin
  // - dir: ray direction
  // - dist: ray length
  // - body: obstacle
  public static bool Raytrace(Vector3d p, Vector3d dir, double dist, CelestialBody body)
  {
    // ray from origin to body center
    Vector3d diff = body.position - p;

    // projection of origin->body center ray over the raytracing direction
    double k = Vector3d.Dot(diff, dir);

    // the ray doesn't hit body if its minimal analytical distance along the ray is less than its radius
    // simplified from 'p + dir * k - body.position'
    return k < 0.0 || k > dist || (dir * k - diff).magnitude > body.Radius;
  }


  // return true if the body is visible from the vessel
  // - vessel: origin
  // - body: target
  // - dir: will contain normalized vector from vessel to body
  // - dist: will contain distance from vessel to body surface
  // - return: true if visible, false otherwise
  public static bool RaytraceBody(Vessel vessel, Vector3d vessel_pos, CelestialBody body, out Vector3d dir, out double dist)
  {
    // shortcuts
    CelestialBody mainbody = vessel.mainBody;
    CelestialBody refbody = mainbody.referenceBody;

    // generate ray parameters
    dir = body.position - vessel_pos;
    dist = dir.magnitude;
    dir /= dist;
    dist -= body.Radius;

    // raytrace
    return (body == mainbody || Raytrace(vessel_pos, dir, dist, mainbody))
        && (body == refbody || refbody == null || Raytrace(vessel_pos, dir, dist, refbody));
  }


  // return true if a vessel is visible from another one
  // - a: origin
  // - b: target
  // - dir: will contain normalized vector from a to b
  // - dist: will contain distance between a and b
  // - return: true if visible, false otherwise
  public static bool RaytraceVessel(Vessel a, Vessel b, Vector3d pos_a, Vector3d pos_b, out Vector3d dir, out double dist)
  {
    // shortcuts
    CelestialBody mainbody_a = a.mainBody;
    CelestialBody mainbody_b = b.mainBody;
    CelestialBody refbody_a = mainbody_a.referenceBody;
    CelestialBody refbody_b = mainbody_b.referenceBody;

    // generate ray parameters
    dir = pos_b - pos_a;
    dist = dir.magnitude;
    dir /= dist;

    // raytrace
    return Raytrace(pos_a, dir, dist, mainbody_a)
        && Raytrace(pos_a, dir, dist, mainbody_b)
        && (refbody_a == null || Raytrace(pos_a, dir, dist, refbody_a))
        && (refbody_b == null || Raytrace(pos_a, dir, dist, refbody_b));
  }


  // --------------------------------------------------------------------------
  // TEMPERATURE
  // --------------------------------------------------------------------------

  // calculate temperature in K from irradiance in W/m2, as per Stefan-Boltzmann equation
  public static double BlackBodyTemperature(double flux)
  {
    return Math.Pow(flux / PhysicsGlobals.StefanBoltzmanConstant, 0.25);
  }

  // calculate irradiance in W/m2 from solar flux reflected on a celestial body in direction of the vessel
  public static double AlbedoFlux(CelestialBody body, Vector3d pos)
  {
    CelestialBody sun = FlightGlobals.Bodies[0];
    Vector3d sun_dir = sun.position - body.position;
    double sun_dist = sun_dir.magnitude;
    sun_dir /= sun_dist;
    sun_dist -= sun.Radius;

    Vector3d body_dir = pos - body.position;
    double body_dist = body_dir.magnitude;
    body_dir /= body_dist;
    body_dist -= body.Radius;

    // used to scale with distance
    double d = Math.Min((body.Radius + body.atmosphereDepth) / (body.Radius + body_dist), 1.0);

    return SolarFlux(sun_dist)                            // solar radiation
      * body.albedo                                       // reflected
      * Math.Max(0.0, Vector3d.Dot(sun_dir, body_dir))    // clamped cosine
      * d * d;                                            // scale with distance
  }

  // return irradiance from the surface of a body in W/m2
  public static double BodyFlux(CelestialBody body, double altitude)
  {
    CelestialBody sun = FlightGlobals.Bodies[0];
    Vector3d sun_dir = sun.position - body.position;
    double sun_dist = sun_dir.magnitude;
    sun_dir /= sun_dist;
    sun_dist -= sun.Radius;

    // heat capacities, in J/(g K)
    const double water_k = 4.181;
    const double regolith_k = 0.67;
    const double hydrogen_k = 14.300;
    const double helium_k = 5.193;
    const double oxygen_k = 0.918;
    const double silicum_k = 0.703;
    const double aluminium_k = 0.897;
    const double iron_k = 0.412;
    const double co2_k = 0.839;
    const double nitrogen_k = 1.040;
    const double argon_k = 0.520;
    const double earth_surf_k = oxygen_k * 0.54 + silicum_k * 0.31 + aluminium_k * 0.09 + iron_k * 0.06;
    const double earth_atmo_k = nitrogen_k * 0.78 + oxygen_k * 0.21 + argon_k * 0.01;
    const double hell_atmo_k = co2_k * 0.96 + nitrogen_k * 0.04;
    const double gas_giant_k = hydrogen_k * 0.75 + helium_k * 0.25;

    // proportion of flux not absorbed by atmosphere
    double atmo_factor = AtmosphereFactor(body, 0.7071);

    // try to determine if this is a gas giant
    // - old method: density less than 20% of home planet
    bool is_gas_giant = !body.hasSolidSurface;

    // try to determine if this is a runaway greenhouse planet
    bool is_hell = atmo_factor < 0.5;

    // store heat capacity coefficients
    double surf_k = 0.0;
    double atmo_k = 0.0;

    // deduce surface and atmosphere heat capacity coefficients
    if (is_gas_giant)
    {
      surf_k = gas_giant_k;
      atmo_k = gas_giant_k;
    }
    else
    {
      if (body.atmosphere)
      {
        surf_k = !body.ocean ? earth_surf_k : earth_surf_k * 0.29 + water_k * 0.71;
        atmo_k = !is_hell ? earth_atmo_k : hell_atmo_k;
      }
      else
      {
        surf_k = !body.ocean ? regolith_k : regolith_k * 0.5 + water_k * 0.5;
      }
    }

    // how much flux a part of surface is able to store, in J
    double surf_capacity = surf_k // heat capacity, in J/(g K)
      * body.Radius / 6000.0      // volume of ground considered, 1x1xN m (100 m^3 at kerbin)
      * body.Density              // convert to grams
      / 4.0;                      // lighter matter on surface compared to overall density

    // how much flux the atmosphere is able to store, in J
    double atmo_capacity = atmo_k
      * body.atmospherePressureSeaLevel
      * 1000.0
      * SurfaceGravity(body);

    // solar flux striking the body
    double solar_flux = SolarFlux(sun_dist);

    // duration of lit and unlit periods
    double half_day = body.solarDayLength * 0.5;

    // flux stored by surface during daylight, and re-emitted during whole day
    double surf_flux = Math.Min
    (
        solar_flux              // incoming flux
      * (1.0 - body.albedo)     // not reflected
      * atmo_factor             // not absorbed by atmosphere
      * 0.7071                  // clamped cosine average
      * half_day,               // accumulated during daylight period
        surf_capacity           // clamped to storage capacity
    ) / body.solarDayLength;    // released during whole day

    // flux stored by atmosphere during daylight, and re-emitted during whole day
    double atmo_flux = Math.Min
    (
        solar_flux              // incoming flux
      * (1.0 - body.albedo)     // not reflected
      * (1.0 - atmo_factor)     // absorbed by atmosphere
      * half_day,               // accumulated during daylight period
        atmo_capacity           // clamped to storage capacity
    ) / body.solarDayLength;    // released during whole day

    // used to scale with distance
    double d = Math.Min((body.Radius + body.atmosphereDepth) / (body.Radius + altitude), 1.0);

    // return radiative cooling flux from the body
    return (surf_flux + atmo_flux) * d * d;
  }

  // return CMB irradiance in W/m2
  public static double BackgroundFlux()
  {
    return 3.14E-6;
  }

  // return temperature of a vessel
  public static double Temperature(Vessel v, Vector3d position, double sunlight, double atmo_factor, out double solar_flux, out double albedo_flux, out double body_flux, out double total_flux)
  {
    // get vessel body
    CelestialBody body = v.mainBody;

    // get solar radiation
    solar_flux = SolarFlux(SunDistance(position)) * sunlight * atmo_factor;

    // get albedo radiation
    albedo_flux = body.flightGlobalsIndex == 0 ? 0.0 : AlbedoFlux(body, position);

    // get cooling radiation from the body
    body_flux = body.flightGlobalsIndex == 0 ? 0.0 : BodyFlux(body, v.altitude);

    // calculate total flux
    total_flux = solar_flux + albedo_flux + body_flux + BackgroundFlux();

    // calculate temperature
    double temp = BlackBodyTemperature(total_flux);

    // if inside atmosphere
    if (body.atmosphere && v.altitude < body.atmosphereDepth)
    {
      // get atmospheric temperature
      double atmo_temp = body.GetTemperature(v.altitude);

      // mix between our temperature and the stock atmospheric model
      temp = Lib.Mix(atmo_temp, temp, Lib.Clamp(v.altitude / body.atmosphereDepth, 0.0, 1.0));
    }

    // finally, return the temperature
    return temp;
  }

  // return sun luminosity
  public static double SolarLuminosity()
  {
    // return solar luminosity
    // note: it is 0 before loading first vessel in a game session, we compute it in that case
    if (PhysicsGlobals.SolarLuminosity <= double.Epsilon)
    {
      double A = Lib.PlanetarySystem(FlightGlobals.GetHomeBody()).orbit.semiMajorAxis;
      return A * A * 12.566370614359172 * PhysicsGlobals.SolarLuminosityAtHome;
    }
    return PhysicsGlobals.SolarLuminosity;
  }

  // return energy flux from the sun
  // - distance from the sun surface
  public static double SolarFlux(double dist)
  {
    // note: for consistency we always consider distances to bodies to be relative to the surface
    // however, flux, luminosity and irradiance consider distance to the sun center, and not surface
    dist += FlightGlobals.Bodies[0].Radius;

    // calculate solar flux
    return SolarLuminosity() / (12.566370614359172 * dist * dist);
  }

  // return solar flux at home
  public static double SolarFluxAtHome()
  {
    return PhysicsGlobals.SolarLuminosityAtHome;
  }

  // return difference from survival temperature
  // - as a special case, there is no temp difference when landed on the home body
  public static double TempDiff(double k, CelestialBody body, bool landed)
  {
    if (body.flightGlobalsIndex == FlightGlobals.GetHomeBodyIndex() && landed) return 0.0;
    return Math.Max(Math.Abs(k - Settings.SurvivalTemperature) - Settings.SurvivalRange, 0.0);
  }


  // --------------------------------------------------------------------------
  // ATMOSPHERE
  // --------------------------------------------------------------------------

  // return proportion of flux not blocked by atmosphere
  // - position: sampling point
  // - sun_dir: normalized vector from sampling point to the sun
  public static double AtmosphereFactor(CelestialBody body, Vector3d position, Vector3d sun_dir)
  {
    // get up vector & altitude
    Vector3d up = position - body.position;
    double altitude = up.magnitude;
    up /= altitude;
    altitude -= body.Radius;
    altitude = Math.Abs(altitude); //< deal with underwater & fp precision issues

    double static_pressure = body.GetPressure(altitude);
    if (static_pressure > 0.0)
    {
      double density = body.GetDensity(static_pressure, body.GetTemperature(altitude));

      // nonrefracting radially symmetrical atmosphere model [Schoenberg 1929]
      double Ra = body.Radius + altitude;
      double Ya = body.atmosphereDepth - altitude;
      double q = Ra * Math.Max(0.0, Vector3d.Dot(up, sun_dir));
      double path = Math.Sqrt(q * q + 2.0 * Ra * Ya + Ya * Ya) - q;
      return body.GetSolarPowerFactor(density) * Ya / path;
    }
    return 1.0;
  }


  // return proportion of flux not blocked by atmosphere
  // note: this one assume the receiver is on the ground
  // - cos_a: cosine of angle between zenith and sun, in [0..1] range
  //          to get an average for stats purpose, use 0.7071
  public static double AtmosphereFactor(CelestialBody body, double cos_a)
  {
    double static_pressure = body.GetPressure(0.0);
    if (static_pressure > 0.0)
    {
      double density = body.GetDensity(static_pressure, body.GetTemperature(0.0));

      // nonrefracting radially symmetrical atmosphere model [Schoenberg 1929]
      double Ra = body.Radius;
      double Ya = body.atmosphereDepth;
      double q = Ra * cos_a;
      double path = Math.Sqrt(q * q + 2.0 * Ra * Ya + Ya * Ya) - q;
      return body.GetSolarPowerFactor(density) * Ya / path;
    }
    return 1.0;
  }


  // return proportion of ionizing radiation not blocked by atmosphere
  public static double GammaTransparency(CelestialBody body, double altitude)
  {
    // deal with underwater & fp precision issues
    altitude = Math.Abs(altitude);

    // get pressure
    double static_pressure = body.GetPressure(altitude);
    if (static_pressure > 0.0)
    {
      // get density
      double density = body.GetDensity(static_pressure, body.GetTemperature(altitude));

      // math, you know
      double Ra = body.Radius + altitude;
      double Ya = body.atmosphereDepth - altitude;
      double path = Math.Sqrt(Ra * Ra + 2.0 * Ra * Ya + Ya * Ya) - Ra;
      double factor = body.GetSolarPowerFactor(density) * Ya / path;

      // poor man atmosphere composition contribution
      if (body.atmosphereContainsOxygen || body.ocean)
      {
        factor = 1.0 - Math.Pow(1.0 - factor, 0.015);
      }
      return factor;
    }
    return 1.0;
  }


  // return true if the vessel is under water
  public static bool Underwater(Vessel v)
  {
    double safe_threshold = v.isEVA ? -0.5 : -2.0;
    return v.mainBody.ocean && v.altitude < safe_threshold;
  }


  // return true if a vessel is inside a breathable atmosphere
  public static bool Breathable(Vessel v, bool underwater)
  {
    // a vessel is inside a breathable atmosphere if:
    // - it is inside an atmosphere
    // - the atmospheric pressure is above 25kPA
    // - the body atmosphere is flagged as containing oxygen
    // - it isn't underwater
    CelestialBody body = v.mainBody;
    return body.atmosphereContainsOxygen
        && body.GetPressure(v.altitude) > 25.0
        && !underwater;
  }

  // return true if a celestial body atmosphere is breathable at surface conditions
  public static bool Breathable(CelestialBody body)
  {
    return body.atmosphereContainsOxygen
        && body.atmospherePressureSeaLevel > 25.0;
  }


  // return pressure at sea level in kPA
  public static double PressureAtSeaLevel()
  {
    // note: we could get the home body pressure at sea level, and deal with the case when it is atmosphere-less
    // however this function can be called to generate part tooltips, and at that point the bodies are not ready
    return 101.0;
  }


  // return true if vessel is inside the thermosphere
  public static bool InsideThermosphere(Vessel v)
  {
    var body = v.mainBody;
    return body.atmosphere && v.altitude > body.atmosphereDepth && v.altitude <= body.atmosphereDepth * 5.0;
  }


  // return true if vessel is inside the exosphere
  public static bool InsideExosphere(Vessel v)
  {
    var body = v.mainBody;
    return body.atmosphere && v.altitude > body.atmosphereDepth * 5.0 && v.altitude <= body.atmosphereDepth * 25.0;
  }


  // --------------------------------------------------------------------------
  // GRAVIOLI
  // --------------------------------------------------------------------------

  public static double Graviolis(Vessel v)
  {
    double dist = Vector3d.Distance(v.GetWorldPos3D(), FlightGlobals.Bodies[0].position);
    double au = dist / FlightGlobals.GetHomeBody().orbit.semiMajorAxis;
    return 1.0 - Math.Min(au, 1.0); // 0 at 1AU -> 1 at sun position
  }
}


} // KERBALISM

