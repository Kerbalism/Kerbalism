using System;
using UnityEngine;
using System.Collections.Generic;

namespace KERBALISM
{
	public static class Sim
	{
		#region pseudo-ctor
		public static void Init()
		{
			Sim.SolarFluxAtHome = 0.0;
			// Search for "LightShifter" components, added by Kopernicus to bodies that are stars
			foreach (CelestialBody body in FlightGlobals.Bodies)
			{
				foreach (var c in body.scaledBody.GetComponentsInChildren<MonoBehaviour>(true))
				{
					if (c.GetType().ToString().Contains("LightShifter"))
					{
						double starFluxAtHome = Lib.ReflectionValue<double>(c, "solarLuminosity");
						suns.Add(new SunData(body.flightGlobalsIndex, starFluxAtHome));
						if (starFluxAtHome > 1.0) Sim.SolarFluxAtHome += starFluxAtHome;
					}
				}
			}

			// if nothing was found, assume the sun is the stock default
			if (suns.Count == 0)
			{
				suns.Add(new SunData(0, PhysicsGlobals.SolarLuminosityAtHome));
				Sim.SolarFluxAtHome = PhysicsGlobals.SolarLuminosityAtHome;
			}

			// calculate each sun total flux (must be done after the "suns" list is populated
			foreach (SunData sd in suns) sd.InitSolarFluxTotal();

			// get scaled space planetary layer for physic raytracing
			planetaryLayerMask = 1 << LayerMask.NameToLayer("Scaled Scenery");
		}
		#endregion

		#region GENERAL
		// return period of an orbit at specified altitude over a body
		public static double OrbitalPeriod(CelestialBody body, double altitude)
		{
			if (altitude <= double.Epsilon) return body.rotationPeriod;
			double Ra = altitude + body.Radius;
			return 2.0 * Math.PI * Math.Sqrt(Ra * Ra * Ra / body.gravParameter);
		}

		/// <summary>period in shadow of an orbit at specified altitude over a body</summary>
		public static double ShadowPeriod(CelestialBody body, double altitude)
		{
			if (altitude <= double.Epsilon) return body.rotationPeriod * 0.5;
			double Ra = altitude + body.Radius;
			double h = Math.Sqrt(Ra * body.gravParameter);
			return (2.0 * Ra * Ra / h) * Math.Asin(body.Radius / Ra);
		}

		/// <summary>orbital period of the specified vessel</summary>
		public static double OrbitalPeriod(Vessel v)
		{
			if (Lib.Landed(v) || double.IsNaN(v.orbit.inclination))
				return v.mainBody.rotationPeriod;

			return v.orbit.period;
		}

		/// <summary>Period in shadow of the vessels orbit.<br/>
		/// Limitations:<br/>
		/// 1. This method assumes the orbit is an ellipse / circle which is not
		/// changing or being altered by other bodies.<br/>
		/// 2. It assumes the sun's rays are parallel across the orbiting
		/// planet, although all bodies are small enough and far enough from the
		/// sun for this to be nearly true.<br/>
		/// 3. The method does not take into account darkness caused by eclipses
		/// of a different body than the orbited body, for example, orbiting
		/// Laythe but Jool blocks the sun.<br/>
		/// 4. The method calculates the longest amount of time spent in darkness, which for
		/// some orbits (e.g.polar orbits) will only be experienced periodically.
		/// It ignores inclination, the argument of periapsis and the beta angle
		/// (the angle the sun is in relation to the orbit). Even polar orbits above the
		/// day/night terminator line (which would be in the sun all the time) will
		/// be treated as orbits passing the nadir at apoapsis.<br/>
		/// 5. We err on the light side: if more than half the orbit period is calculated
		/// to be in shadow by this formula, we invert the result and assume it is in light
		/// instead.
		/// </summary>
		public static double ShadowPeriod(Vessel v)
		{
			if (Lib.Landed(v) || double.IsNaN(v.orbit.inclination) || v.orbit == null)
				return v.mainBody.rotationPeriod * 0.5;

			// the old method: this calculates the period for circular orbits
			// double Ra = v.altitude + v.mainBody.Radius;
			// double h = Math.Sqrt(Ra * v.mainBody.gravParameter);
			// return (2.0 * Ra * Ra / h) * Math.Asin(v.mainBody.Radius / Ra);

			// Calculation for elliptical orbits

			// see https://wiki.kerbalspaceprogram.com/wiki/Orbit_darkness_time

			// Limitations:
			//
			// - This method assumes the orbit is an ellipse / circle which is not
			// changing or being altered by other bodies.
			//
			// - It also assumes the sun's rays are parallel across the orbiting
			// planet, although all bodies are small enough and far enough from the
			// sun for this to be nearly true.
			//
			// - The method does not take into account darkness caused by eclipses
			// of a different body than the orbited body, for example, orbiting
			// Laythe but Jool blocks the sun.
			//
			// - The method gives the longest amount of time spent in darkness, which for some
			// orbits (e.g.polar orbits), will only be experienced periodically.
			
			// The formula:
			// Td = (2ab / h) (asin(R/b) + eR/b)
			// a is the semi-major axis
			// b the semi-minor axis
			// h the specific angular momentum
			// e the eccentricity
			// R the radius of the planet or moon
			// For reference these terms can be calculated by knowing the apoapsis(Ap), periapsis(Pe) and body to orbit:
			// h = sqrt(lµ)
			// l = (2 * ra * rp) / (ra + rp)
			// µ = G * M, the gravitational parameter
			// ra = Ap + R (apoapsis from center of the body)
			// rp = Pe + R (periapsis from center of the body)

			var R = v.mainBody.Radius;
			var ra = v.orbit.ApR;
			var rp = v.orbit.PeR;
			var a = v.orbit.semiMajorAxis;
			var b = v.orbit.semiMinorAxis;
			var e = v.orbit.eccentricity;
			var l = (2 * ra * rp) / (ra + rp);
			var µ = v.mainBody.gravParameter;
			var h = Math.Sqrt(l * µ);

			var Td = (2 * a * b / h) * (Math.Asin(R / b) + e * R / b);

			// err on the light side:
			// if more than half the orbit duration is in shadow, invert
			// the result. this will be wrong when the apoapsis is near nadir.
			// this is just... WRONG, I know, but it is wrong in a good way.
			// f.i. a very eccentric orbit with the apoapsis above one of the
			// poles won't be assumed to spend most of the time in shadow.
			if (Td > v.orbit.period / 2)
				return v.orbit.period - Td;

			return Td;
		}

		/// <summary>
		/// This expects to be called repeatedly
		/// </summary>
		public static double SampleSunFactor(Vessel v, double elapsedSeconds)
		{
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.Sim.SunFactor2");

			// assume 50% exposure for landed vessels (half the period is night)
			// this is bad for inclined bodies, but we're not landing on uranus
			// any time soon
			if(Lib.Landed(v) || double.IsNaN(v.orbit.inclination) || v.orbit == null)
				return 0.5;

			int sunSamples = 0;
			int sampleCount = 0;

			var now = Planetarium.GetUniversalTime();
			double step = Math.Max(120.0, elapsedSeconds / 40);
			var sun = v.KerbalismData().EnvMainSun.SunData.body;
			var occluders = v.KerbalismData().EnvVisibleBodies;
			double calculatedDuration = 0;
			double maxCalculation = Math.Min(elapsedSeconds * 1.5, v.orbit.period);

			// calculate for one orbit or 1.5 times the amount we need, whichever is shorter
			while (calculatedDuration < maxCalculation)
			{
				Vector3d position = v.orbit.getPositionAtUT(now + sampleCount * step);
				Vector3d direction;
				double distance;
				if (IsBodyVisible(v, position, sun, occluders, out direction, out distance))
					sunSamples++;

				sampleCount++;
				calculatedDuration = step * sampleCount;
			}

			UnityEngine.Profiling.Profiler.EndSample();

			double sunFactor = (double)sunSamples / (double)sampleCount;
			// Lib.Log("Vessel " + v + " sun factor: " + sunFactor + " " + sunSamples + "/" + sampleCount + " #s=" + sampleCount + " e=" + elapsedSeconds + " step=" + step);
			return sunFactor;
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
			if (body != null && body.orbit != null)
			{
				return (1.0 + body.orbit.eccentricity) * body.orbit.semiMajorAxis;
			}
			return 0;
		}


		// return periapsis of a body orbit
		public static double Periapsis(CelestialBody body)
		{
			if (body != null && body.orbit != null)
			{
				return (1.0 - body.orbit.eccentricity) * body.orbit.semiMajorAxis;
			}
			return 0;
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

		#endregion

		#region RAYTRACING
		/// <summary>Scaled space planetary layer for physic raytracing</summary>
		private static int planetaryLayerMask = int.MaxValue;

		/// <summary>Return true if there is no CelestialBody between the vessel position and the 'end' point. Beware that this uses a (very slow) physic raycast.</summary>
		/// <param name="endNegOffset">distance from which the ray will stop before hitting the 'end' point, put the body radius here if the end point is a CB</param>
		public static bool RaytracePhysic(Vessel vessel, Vector3d vesselPos, Vector3d end, double endNegOffset = 0.0)
		{
			// for unloaded vessels, position in scaledSpace is 1 fixedUpdate frame desynchronized :
			if (!vessel.loaded)
				vesselPos += vessel.mainBody.position - vessel.mainBody.getTruePositionAtUT(Planetarium.GetUniversalTime() + TimeWarp.fixedDeltaTime);

			// convert vessel position to scaled space
			ScaledSpace.LocalToScaledSpace(ref vesselPos);
			ScaledSpace.LocalToScaledSpace(ref end);
			Vector3d dir = end - vesselPos;
			if (endNegOffset > 0) dir -= dir.normalized * (endNegOffset * ScaledSpace.InverseScaleFactor);

			return !Physics.Raycast(vesselPos, dir, (float)dir.magnitude, planetaryLayerMask);
		}

		/// <summary> return true if the ray 'dir' starting at 'start' and of length 'dist' doesn't hit 'body'</summary>
		/// <param name="start">ray origin</param>
		/// <param name="dir">ray direction</param>
		/// <param name="dist">ray length</param>
		/// <param name="body">obstacle to check against</param>
		public static bool RayAvoidBody(Vector3d start, Vector3d dir, double dist, CelestialBody body)
		{
			// ray from origin to body center
			Vector3d diff = body.position - start;

			// projection of origin->body center ray over the raytracing direction
			double k = Vector3d.Dot(diff, dir);

			// the ray doesn't hit body if its minimal analytical distance along the ray is less than its radius
			// simplified from 'start + dir * k - body.position'
			return k < 0.0 || k > dist || (dir * k - diff).magnitude > body.Radius;
		}

		/// <summary>return true if 'body' is visible from 'vesselPos'. Very fast method.</summary>
		/// <param name="occludingBodies">the bodies that will be checked for occlusion</param>
		/// <param name="bodyDir">normalized vector from vessel to body</param>
		/// <param name="bodyDist">distance from vessel to body surface</param>
		/// <returns></returns>
		public static bool IsBodyVisible(Vessel vessel, Vector3d vesselPos, CelestialBody body, List<CelestialBody> occludingBodies, out Vector3d bodyDir, out double bodyDist)
		{
			// generate ray parameters
			bodyDir = body.position - vesselPos;
			bodyDist = bodyDir.magnitude;
			bodyDir /= bodyDist;
			bodyDist -= body.Radius;

			// for very small bodies the analytic method is very unreliable at high latitudes
			// we use a physic raycast (a lot slower)
			if (Lib.Landed(vessel) && vessel.mainBody.Radius < 100000.0 && (vessel.latitude < -45.0 || vessel.latitude > 45.0))
				return RaytracePhysic(vessel, vesselPos, body.position, body.Radius);

			// check if the ray intersect one of the provided bodies
			foreach (CelestialBody occludingBody in occludingBodies)
			{
				if (occludingBody == body) continue;
				if (!RayAvoidBody(vesselPos, bodyDir, bodyDist, occludingBody)) return false;
			}
			
			return true;
		}

		/// <summary>return the list of bodies whose apparent diameter is greater than 10 arcmin from the 'position' POV</summary>
		public static List<CelestialBody> GetLargeBodies(Vector3d position)
		{
			List <CelestialBody> visibleBodies = new List<CelestialBody>();
			foreach (CelestialBody occludingBody in FlightGlobals.Bodies)
			{
				// if apparent diameter > ~10 arcmin (~0.003 radians), consider the body for occlusion checks
				// real apparent diameters at earth : sun/moon ~ 30 arcmin, Venus ~ 1 arcmin max
				double apparentSize = (occludingBody.Radius * 2.0) / (occludingBody.position - position).magnitude;
				if (apparentSize > 0.003) visibleBodies.Add(occludingBody);
			}
			return visibleBodies;
		}
		#endregion

		#region SUN/STARS
		/// <summary>
		/// Solar luminosity from all stars/suns at the home body, in W/m².
		/// Use this instead of PhysicsGlobals.SolarLuminosityAtHome
		/// </summary>
		public static double SolarFluxAtHome { get; private set; }

		/// <summary>List of all suns/stars, with reference to their CB and their (total) luminosity</summary>
		public static readonly List<SunData> suns = new List<SunData>();
		public class SunData
		{
			public CelestialBody body;
			public int bodyIndex;
			private double solarFluxAtAU;
			private double solarFluxTotal;

			public SunData(int bodyIndex, double solarFluxAtAU)
			{
				body = FlightGlobals.Bodies[bodyIndex];
				this.bodyIndex = bodyIndex;
				this.solarFluxAtAU = solarFluxAtAU;
			}

			// This must be called after "suns" SunData list is populated (because it use AU > Lib.IsSun)
			public void InitSolarFluxTotal()
			{
				this.solarFluxTotal = solarFluxAtAU * AU * AU * Math.PI * 4.0;
			}

			/// <summary>Luminosity in W/m² at the given distance from this sun/star</summary>
			/// <param name="fromSunSurface">true if the 'distance' is from the sun surface</param>
			public double SolarFlux(double distance, bool fromSunSurface = true)
			{
				// note: for consistency we always consider distances to bodies to be relative to the surface
				// however, flux, luminosity and irradiance consider distance to the sun center, and not surface
				if (fromSunSurface) distance += body.Radius;

				// calculate solar flux
				return solarFluxTotal / (Math.PI * 4 * distance * distance);
			}
		}

		static double au = 0.0;
		/// <summary> Distance between the home body and its main sun</summary>
		public static double AU
		{
			get
			{
				if (au == 0.0)
				{
					CelestialBody home = FlightGlobals.GetHomeBody();
					au = (home.position - Lib.GetParentSun(home).position).magnitude;
				}
				return au;
			}
		}

		// get distance from the sun
		public static double SunDistance(Vector3d pos, CelestialBody sun)
		{
			return Vector3d.Distance(pos, sun.position) - sun.Radius;
		}

		/// <summary>Estimated solar flux from the first parent sun of the given body, including other neighbouring stars/suns (binary systems handling)</summary>
		/// <param name="body"></param>
		/// <param name="worstCase">if true, we use the largest distance between the body and the sun</param>
		/// <param name="mainSun"></param>
		/// <param name="mainSunDirection"></param>
		/// <param name="mainSunDistance"></param>
		/// <returns></returns>
		public static double SolarFluxAtBody(CelestialBody body, bool worstCase, out CelestialBody mainSun, out Vector3d mainSunDirection, out double mainSunDistance)
		{
			// get first parent sun
			mainSun = Lib.GetParentSun(body);

			// get direction and distance
			mainSunDirection = (mainSun.position - body.position).normalized;
			if (worstCase)
				mainSunDistance = Sim.Apoapsis(Lib.GetParentPlanet(body)) - mainSun.Radius - body.Radius;
			else
				mainSunDistance = Sim.SunDistance(body.position, mainSun);

			// get solar flux
			int mainSunIndex = mainSun.flightGlobalsIndex;
			Sim.SunData mainSunData = Sim.suns.Find(pr => pr.bodyIndex == mainSunIndex);
			double solarFlux = mainSunData.SolarFlux(mainSunDistance);
			// multiple suns handling (binary systems...)
			foreach (Sim.SunData otherSun in Sim.suns)
			{
				if (otherSun.body == mainSun) continue;
				Vector3d otherSunDir = (otherSun.body.position - body.position).normalized;
				double otherSunDist;
				if (worstCase)
					otherSunDist = Sim.Apoapsis(Lib.GetParentPlanet(body)) - otherSun.body.Radius;
				else
					otherSunDist = Sim.SunDistance(body.position, otherSun.body);
				// account only for other suns that have approximatively the same direction (+/- 30°), discard the others
				if (Vector3d.Angle(otherSunDir, mainSunDirection) > 30.0) continue;
				solarFlux += otherSun.SolarFlux(otherSunDist);
			}
			return solarFlux;
		}

		public static double SunBodyAngle(Vessel vessel, Vector3d vesselPos, CelestialBody sun)
		{
			// orbit around sun?
			if (vessel.mainBody == sun) return 0.0;
			return Vector3d.Angle(vessel.mainBody.position - vesselPos, vessel.mainBody.position - sun.position);
		}
		#endregion

		#region TEMPERATURE
		// calculate temperature in K from irradiance in W/m2, as per Stefan-Boltzmann equation
		public static double BlackBodyTemperature(double flux)
		{
			return Math.Pow(flux / PhysicsGlobals.StefanBoltzmanConstant, 0.25);
		}

		// calculate irradiance in W/m2 from solar flux reflected on a celestial body in direction of the vessel
		public static double AlbedoFlux(CelestialBody body, Vector3d pos)
		{
			CelestialBody sun = Lib.GetParentSun(body);
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

			return suns.Find(p => p.body == sun).SolarFlux(sun_dist)	// solar radiation
			  * body.albedo												// reflected
			  * Math.Max(0.0, Vector3d.Dot(sun_dir, body_dir))			// clamped cosine
			  * d * d;													// scale with distance
		}

		// return irradiance from the surface of a body in W/m2
		public static double BodyFlux(CelestialBody body, double altitude)
		{
			CelestialBody sun = Lib.GetParentSun(body);
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
			double solar_flux = suns.Find(p => p.body == sun).SolarFlux(sun_dist);

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
		public static double Temperature(Vessel v, Vector3d position, double solar_flux, out double albedo_flux, out double body_flux, out double total_flux)
		{
			// get vessel body
			CelestialBody body = v.mainBody;

			// get albedo radiation
			albedo_flux = Lib.IsSun(body) ? 0.0 : AlbedoFlux(body, position);

			// get cooling radiation from the body
			body_flux = Lib.IsSun(body) ? 0.0 : BodyFlux(body, v.altitude);

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

		// return difference from survival temperature
		// - as a special case, there is no temp difference when landed on the home body
		public static double TempDiff(double k, CelestialBody body, bool landed)
		{
			if (body.flightGlobalsIndex == FlightGlobals.GetHomeBodyIndex() && landed) return 0.0;
			return Math.Max(Math.Abs(k - Settings.LifeSupportSurvivalTemperature) - Settings.LifeSupportSurvivalRange, 0.0);
		}
		#endregion

		#region ATMOSPHERE
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

		// determine average atmospheric absorption factor over the daylight period (not the whole day)
		// - by doing an average of values at midday, sunrise and an intermediate value
		// - using the current sun direction at the given position to approximate
		//   the influence of high latitudes and of the inclinaison of the body orbit
		public static double AtmosphereFactorAnalytic(CelestialBody body, Vector3d position, Vector3d sun_dir)
		{
			// only for atmospheric bodies whose rotation period is less than 120 hours
			if (body.rotationPeriod > 432000.0)
				return AtmosphereFactor(body, position, sun_dir);

			// get up vector & altitude
			Vector3d radialOut = position - body.position;
			double altitude = radialOut.magnitude;
			radialOut /= altitude; // normalize
			altitude -= body.Radius;
			altitude = Math.Abs(altitude); //< deal with underwater & fp precision issues

			double static_pressure = body.GetPressure(altitude);
			if (static_pressure > 0.0)
			{
				Vector3d[] sunDirs = new Vector3d[3];

				// east - sunrise
				sunDirs[0] = body.getRFrmVel﻿(position).normalized;
				// perpendicular vector
				Vector3d sunUp = Vector3d.Cross(sunDirs[0], sun_dir).normalized;
				// midday vector (along the radial plane + an angle depending on the original vesselSundir)
				sunDirs[1] = Vector3d.Cross(sunUp, sunDirs[0]).normalized;
				// invert midday vector if it's pointing toward the ground (checking against radial-out vector)
				if (Vector3d.Dot(sunDirs[1], radialOut) < 0.0) sunDirs[1] *= -1.0;
				// get an intermediate vector between sunrise and midday
				sunDirs[2] = (sunDirs[0] + sunDirs[1]).normalized;

				double density = body.GetDensity(static_pressure, body.GetTemperature(altitude));

				// nonrefracting radially symmetrical atmosphere model [Schoenberg 1929]
				double Ra = body.Radius + altitude;
				double Ya = body.atmosphereDepth - altitude;
				double atmo_factor_analytic = 0.0;
				for (int i = 0; i < 3; i++)
				{
					double q = Ra * Math.Max(0.0, Vector3d.Dot(radialOut, sunDirs[i]));
					double path = Math.Sqrt(q * q + 2.0 * Ra * Ya + Ya * Ya) - q;
					atmo_factor_analytic += body.GetSolarPowerFactor(density) * Ya / path;
				}
				atmo_factor_analytic /= 3.0;
				return atmo_factor_analytic;
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

		public static double StaticPressureAtm(Vessel v)
		{
			return v.mainBody.GetPressure(v.altitude) * 0.0098692326671601278;
		}

		// return true if a vessel is inside a breathable atmosphere
		public static bool InBreathableAtmosphere(Vessel v, bool underwater)
		{
			// a vessel is inside a breathable atmosphere if:
			// - it is inside an atmosphere
			// - the body atmosphere is flagged as containing oxygen
			// - it isn't underwater
			return v.mainBody.atmosphereContainsOxygen && !underwater;
		}

		// return true if a celestial body atmosphere is breathable at surface conditions
		public static bool Breathable(CelestialBody body)
		{
			return body.atmosphereContainsOxygen
				&& body.atmospherePressureSeaLevel > 25.0;
		}


		// return pressure at sea level in kPA
		// note: we could get the home body pressure at sea level, and deal with the case when it is atmosphere-less
		// however this function can be called to generate part tooltips, and at that point the bodies are not ready
		public static double PressureAtSeaLevel { get; } = 101.0;


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
		#endregion

		#region GRAVIOLI
		public static double Graviolis(Vessel v)
		{
			double dist = Vector3d.Distance(v.GetWorldPos3D(), Lib.GetParentSun(v.mainBody).position);
			double au = dist / FlightGlobals.GetHomeBody().orbit.semiMajorAxis;
			return 1.0 - Math.Min(AU, 1.0); // 0 at 1AU -> 1 at sun position
		}
		#endregion
	}
} // KERBALISM

