// ====================================================================================================================
// functions implementing various simulation mechanics
// ====================================================================================================================


using System;
using System.Collections.Generic;
using System.Net.Mime;
using FinePrint.Utilities;
using UnityEngine;


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


  // return the sun celestial body
  public static CelestialBody Sun()
  {
    return FlightGlobals.Bodies[0];
  }


  // get distance from the sun
  public static double SunDistance(Vessel vessel)
  {
    CelestialBody sun = Sun();
    return Vector3d.Distance(Lib.VesselPosition(vessel), sun.position) - sun.Radius;
  }


  // --------------------------------------------------------------------------
  // RAYTRACING
  // --------------------------------------------------------------------------


  // calculate hit point of the ray indicated by origin + direction * t with the sphere centered at 0,0,0 and with radius 'radius'
  // if there is no hit return double max value
  // it there is an hit return distance from origin to hit point
  public static double RaytraceSphere(Vector3d origin, Vector3d direction, Vector3d center, double radius)
  {
    // operate in sphere object space, so the origin is translated by -sphere_pos
    origin -= center;

    double A = Vector3d.Dot(direction, direction);
    double B = 2.0 * Vector3d.Dot(direction, origin);
    double C = Vector3d.Dot(origin, origin) - radius * radius;
    double discriminant = B * B - 4.0 * A * C;

    // ray missed the sphere (we consider single hits as misses)
    if (discriminant <= 0.0) return double.MaxValue;

    double q = (-B - Math.Sign(B) * Math.Sqrt(discriminant)) * 0.5;
    double t0 = q / A;
    double t1 = C / q;
    double dist = Math.Min(t0, t1);

    // if sphere is behind, return maxvalue, else it is visible and distance is returned
    return dist < 0.0 ? double.MaxValue : dist;
  }


  // return true if the body is visible from the vessel
  // - vessel: vessel to test
  // - dir: normalized vector from vessel to body
  // - dist: distance from vector to body surface
  // - return: true if visible, false otherwise
  public static bool RaytraceBody(Vessel vessel, CelestialBody body, out Vector3d dir, out double dist)
  {
    // shortcuts
    CelestialBody sun = Sun();
    CelestialBody mainbody = vessel.mainBody;
    CelestialBody refbody = vessel.mainBody.referenceBody;

    // generate ray parameters
    Vector3d vessel_pos = Lib.VesselPosition(vessel);
    dir = body.position - vessel_pos;
    dist = dir.magnitude;
    dir /= dist;
    dist -= body.Radius;

    // store list of occluders
    List<CelestialBody> occluders = new List<CelestialBody>();

    // do not trace against the mainbody if that is our target
    if (body != mainbody) occluders.Add(mainbody);

    // do not trace against the reference body if that is our target, or if there isn't one (eg: mainbody is the sun)
    if (body != refbody || refbody == null) occluders.Add(refbody);

    // trace against any satellites, but not when mainbody is the sun (eg: mainbody is a planet)
    // we avoid the mainbody=sun case because it has a lot of satellites and the chances of occlusion are very low
    // and they probably will occlude the sun only partially at best in that case
    if (mainbody != sun) occluders.AddRange(mainbody.orbitingBodies);

    // do the raytracing
    double min_dist = double.MaxValue;
    foreach(CelestialBody cb in occluders)
    {
      min_dist = Math.Min(min_dist, RaytraceSphere(vessel_pos, dir, cb.position, cb.Radius));
    }

    // return true if body is visible from vessel
    return dist < min_dist;
  }


  // return true if a vessel is visible from another one
  // - a, b: vessels to test
  // - dir: normalized vector from vessel to body
  // - dist: distance from vector to body surface
  // - return: true if visible, false otherwise
  public static bool RaytraceVessel(Vessel a, Vessel b, out Vector3d dir, out double dist)
  {
    // shortcuts
    CelestialBody sun = Sun();
    CelestialBody refbody_a = a.mainBody.referenceBody;
    CelestialBody refbody_b = b.mainBody.referenceBody;

    // generate ray parameters
    Vector3d pos_a = Lib.VesselPosition(a);
    Vector3d pos_b = Lib.VesselPosition(b);
    dir = pos_b - pos_a;
    dist = dir.magnitude;
    dir /= dist;

    // store list of occluders
    // note: there are too many special cases of bodies to handle for two vessels, so we use an hashset
    // note: we still avoid adding satellites of the sun
    HashSet<CelestialBody> occluders = new HashSet<CelestialBody>();
    occluders.Add(a.mainBody);
    occluders.Add(b.mainBody);
    if (refbody_a != null) occluders.Add(refbody_a);
    if (refbody_b != null) occluders.Add(refbody_b);
    if (a.mainBody != sun) { foreach(var cb in a.mainBody.orbitingBodies) occluders.Add(cb); }
    if (b.mainBody != sun) { foreach(var cb in b.mainBody.orbitingBodies) occluders.Add(cb); }

    // do the raytracing
    double min_dist = double.MaxValue;
    foreach(CelestialBody cb in occluders)
    {
      min_dist = Math.Min(min_dist, RaytraceSphere(pos_a, dir, cb.position, cb.Radius));
    }

    // return true if body is visible from vessel
    return dist < min_dist;
  }


  // --------------------------------------------------------------------------
  // TEMPERATURE
  // --------------------------------------------------------------------------


  // return temperature of background radiation
  public static double BackgroundTemperature()
  {
    return PhysicsGlobals.SpaceTemperature;
  }


  // return temperature of a blackbody emitting flux specified (or receiving, same thing)
  // note: assume albedo of 30%
  // note: flux is not associative
  public static double BlackBody(double flux)
  {
    const double albedo = 0.3;
    return Math.Pow(flux * (1.0 - albedo) * 0.25 / PhysicsGlobals.StefanBoltzmanConstant, 1.0 / 4.0);
  }


  // return sun luminosity
  public static double SolarLuminosity()
  {
    // return solar luminosity
    // note: it is 0 before loading first vessel in a game session, we compute it in that case
    if (PhysicsGlobals.SolarLuminosity <= double.Epsilon)
    {
      double A = FlightGlobals.GetHomeBody().orbit.semiMajorAxis;
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
    dist += Sun().Radius;

    // calculate solar flux
    return SolarLuminosity() / (12.566370614359172 * dist * dist);
  }


  // return solar flux at home
  public static double SolarFluxAtHome()
  {
    return PhysicsGlobals.SolarLuminosityAtHome;
  }


  // return albedo radiation from a body
  public static double BodyFlux(CelestialBody body, Vector3d point)
  {
    // - solar_flux calculated as usual
    // - total solar energy: E = solar_flux * PI * body.radius^2
    // - reflected solar energy: R = E * body.albedo
    // - reflected solar energy from visible hemisphere, pseudo-integral: RQ = R / 4
    // - reflected solar energy in a direction: k = clamped wrapped cosine, RQD = RQ * k
    // - body_flux: RQD / (PI * 4 * dist * dist)

    // shortcut to the sun
    CelestialBody sun = Sun();

    // calculate sun direction and distance
    Vector3d sun_dir = sun.position - body.transform.position;
    double sun_dist = sun_dir.magnitude;
    sun_dir /= sun_dist;
    sun_dist -= sun.Radius;

    // calculate point direction and distance
    Vector3d point_dir = point - body.transform.position;
    double point_dist = point_dir.magnitude;
    point_dir /= point_dist;
    point_dist -= body.Radius;

    // clamp point to planet surface distance to a fraction of planet radius
    // rationale: our algorithm break at low distances
    point_dist = Math.Max(body.Radius * 0.33, point_dist);

    // calculate solar flux at planet position
    double flux = SolarFlux(sun_dist);

    // calculate total energy from the sun
    double incoming_energy = flux * Math.PI * body.Radius * body.Radius;

    // calculate reflected energy (only an hemisphere is visible from point, and the average N*L is 0.5)
    double reflected_energy = incoming_energy * body.albedo * 0.25;

    // calculate clamped cosine factor (note: 'wrapped')
    double k = Math.Max(0.0, (Vector3d.Dot(sun_dir, point_dir) + 1.05) / 2.05);

    // return flux from the body to point specified
    return reflected_energy * k / (12.566370614359172 * point_dist * point_dist);
  }


  // return temperature of a vessel
  public static double Temperature(Vessel vessel, bool in_sunlight)
  {
    // get flux from the sun
    double solar_flux = !in_sunlight ? 0.0 : SolarFlux(SunDistance(vessel));

    // get flux from the main body
    double body_flux = vessel.mainBody == Sun() ? 0.0 : BodyFlux(vessel.mainBody, Lib.VesselPosition(vessel));

    // calculate temperature
    double space_temp = BackgroundTemperature() + BlackBody(solar_flux) + BlackBody(body_flux);

    // calculate atmospheric temp
    double atmospheric_temp = vessel.mainBody.GetTemperature(vessel.altitude);

    // if the vessel is loaded. and inside an atmosphere, use the part skin temperature as atmospheric temp
    if (vessel.loaded && vessel.mainBody.atmosphere && vessel.altitude < vessel.mainBody.atmosphereDepth) atmospheric_temp = vessel.rootPart.skinTemperature;

    // blend between atmosphere and space temp
    return !vessel.mainBody.atmosphere ? space_temp : Lib.Mix(atmospheric_temp, space_temp, Math.Min(vessel.altitude / vessel.mainBody.atmosphereDepth, 1.0));
  }


  // return difference from survival temperature
  public static double TempDiff(double k)
  {
    return Math.Max(Math.Abs(k - Settings.SurvivalTemperature) - Settings.SurvivalRange, 0.0);
  }


  // --------------------------------------------------------------------------
  // ATMOSPHERE
  // --------------------------------------------------------------------------


  // return proportion of flux not blocked by atmosphere
  // note: while intuitively you are thinking to use this to calculate temperature inside an atmosphere,
  //       understand that atmospheric climate is complex and the game is using float curves to approximate it
  public static double AtmosphereFactor(CelestialBody body, Vector3d position, Vector3d sun_dir)
  {
    // get up vector & altitude
    Vector3d up = (position - body.position);
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


  // return true if inside a breathable atmosphere
  public static bool Breathable(Vessel v)
  {
    return v.mainBody.atmosphereContainsOxygen && v.mainBody.GetPressure(v.altitude) > 25.0;
  }
}


} // KERBALISM

