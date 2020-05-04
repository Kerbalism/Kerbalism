using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace KERBALISM
{
	public static class Sim
	{
		#region MAIN FIELDS/PROPERTIES

		/// <summary>List of all suns/stars, with reference to their CB and their (total) luminosity</summary>
		public static readonly List<SimStar> stars = new List<SimStar>();
		private static readonly List<int> starsIndex = new List<int>();

		/// <summary>
		/// Solar luminosity from all stars/suns at the home body, in W/m².
		/// Use this instead of PhysicsGlobals.SolarLuminosityAtHome
		/// </summary>
		public static double SolarFluxAtHome { get; private set; }

		private static double au = 0.0;
		/// <summary> Distance between the home body and its main sun</summary>
		public static double AU
		{
			get
			{
				if (au == 0.0)
				{
					CelestialBody home = GetParentPlanet(FlightGlobals.GetHomeBody());
					au = (Apoapsis(home) + Periapsis(home)) / 2.0;
				}
				return au;
			}
		}

		public static SimBody[] Bodies { get; private set; }

		/// <summary>Scaled space planetary layer for physic raytracing</summary>
		private static int planetaryLayerMask = int.MaxValue;

		#endregion

		#region INIT
		public static void Init()
		{
			Bodies = new SimBody[FlightGlobals.Bodies.Count];

			// see CelestialBody.GetSolarPowerFactor()
			CelestialBody stockHome = FlightGlobals.GetHomeBody();
			if (stockHome.atmosphereDepth > 0.0)
				homeAtmDensityASL = stockHome.atmDensityASL;
			else
				homeAtmDensityASL = 1.225;

			SolarFluxAtHome = 0.0;
			// Search for "LightShifter" components, added by Kopernicus to bodies that are stars
			foreach (CelestialBody body in FlightGlobals.Bodies)
			{
				foreach (var c in body.scaledBody.GetComponentsInChildren<MonoBehaviour>(true))
				{
					if (c.GetType().ToString().Contains("LightShifter"))
					{
						double starFluxAtHome = Lib.ReflectionValue<double>(c, "solarLuminosity");
						stars.Add(new SimStar(body, starFluxAtHome));
						starsIndex.Add(body.flightGlobalsIndex);
						if (starFluxAtHome > 1.0)
							SolarFluxAtHome += starFluxAtHome;
					}
				}

				SimBody simBody = new SimBody(body);
				Bodies[body.flightGlobalsIndex] = simBody;
			}

			// if nothing was found, assume the sun is the stock default
			if (stars.Count == 0)
			{
				stars.Add(new SimStar(FlightGlobals.Bodies[0], PhysicsGlobals.SolarLuminosityAtHome));
				starsIndex.Add(0);
				SolarFluxAtHome = PhysicsGlobals.SolarLuminosityAtHome;
			}

			// calculate each sun total flux (must be done after the "suns" list is populated
			foreach (SimStar star in stars)
			{
				star.InitSolarFluxTotal();
				Bodies[star.body.flightGlobalsIndex].isSun = true;
			}

			foreach (SimBody simBody in Bodies)
			{
				simBody.Init();
			}


			// get scaled space planetary layer for physic raytracing
			planetaryLayerMask = 1 << LayerMask.NameToLayer("Scaled Scenery");
		}

		public static void OnFixedUpdate()
		{
			foreach (SimBody body in Bodies)
			{
				body.Update();
			}
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

		/// <summary>For a given body, return the last parent body that is not a sun </summary>
		public static CelestialBody GetParentPlanet(CelestialBody body)
		{
			if (IsStar(body)) return body;
			CelestialBody checkedBody = body;
			while (!IsStar(checkedBody.referenceBody)) checkedBody = checkedBody.referenceBody;
			return checkedBody;
		}

		/// <summary> optimized method for getting normalized direction and distance between the surface of two bodies</summary>
		/// <param name="direction">normalized vector 'from' body 'to' body</param>
		/// <param name="distance">distance between the body surface</param>
		public static void DirectionAndDistance(CelestialBody from, CelestialBody to, out Vector3d direction, out double distance)
		{
			DirectionAndDistance(from.position, to.position, out direction, out distance);
			distance -= from.Radius + to.Radius;
		}

		/// <summary> optimized method for getting normalized direction and distance between a world position and the surface of a body</summary>
		/// <param name="direction">normalized vector 'from' position 'to' body</param>
		/// <param name="distance">distance to the body surface</param>
		public static void DirectionAndDistance(Vector3d from, CelestialBody to, out Vector3d direction, out double distance)
		{
			DirectionAndDistance(from, to.position, out direction, out distance);
			distance -= to.Radius;
		}

		/// <summary> optimized method for getting normalized direction and distance between two world positions</summary>
		/// <param name="direction">normalized vector 'from' position 'to' position</param>
		/// <param name="distance">distance between the body surface</param>
		public static void DirectionAndDistance(Vector3d from, Vector3d to, out Vector3d direction, out double distance)
		{
			direction = to - from;
			distance = direction.magnitude;
			direction /= distance;
		}

		#endregion

		#region STARS

		/// <summary> Is this body a star ? </summary>
		public static bool IsStar(CelestialBody body)
		{
			return starsIndex.Contains(body.flightGlobalsIndex);
		}

		/// <summary> get the star data for this body (if it is a star) </summary>
		public static bool TryGetStarData(CelestialBody body, out SimStar starData)
		{
			int index = starsIndex.IndexOf(body.flightGlobalsIndex);
			if (index < 0)
			{
				starData = null;
				return false;
			}

			starData = stars[index];
			return true;
		}

		/// <summary> return the first found parent star for a given body </summary>
		public static CelestialBody GetParentStar(CelestialBody body)
		{
			if (IsStar(body)) return body;

			CelestialBody refBody = body.referenceBody;
			do
			{
				if (IsStar(refBody)) return refBody;
				refBody = refBody.referenceBody;
			}
			while (refBody != null);

			return FlightGlobals.Bodies[0];
		}

		/// <summary> return the first found parent star data for a given body </summary>
		public static SimStar GetParentStarData(CelestialBody body)
		{
			SimStar foundStar;
			if (TryGetStarData(body, out foundStar))
				return foundStar;

			CelestialBody refBody = body.referenceBody;
			do
			{
				if (TryGetStarData(refBody, out foundStar))
					return foundStar;

				refBody = refBody.referenceBody;
			}
			while (refBody != null);

			return stars.FirstOrDefault();
		}

		public static double SunBodyAngle(Vessel vessel, Vector3d vesselPos, CelestialBody sun)
		{
			// orbit around sun?
			if (vessel.mainBody == sun) return 0.0;
			return Vector3d.Angle(vessel.mainBody.position - vesselPos, vessel.mainBody.position - sun.position);
		}

		#endregion

		#region RAYTRACING


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
		public static bool IsBodyVisible(Vessel vessel, Vector3d vesselPos, CelestialBody body, CelestialBody[] occludingBodies, out Vector3d bodyDir, out double bodyDist)
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

		public static bool RayHitSphere(Vector3d rayStartToSphereCenter, Vector3d rayDir, double sphereRadius, double maxDistance)
		{
			// projection of ray origin -> sphere center over the raytracing direction
			double k = Vector3d.Dot(rayStartToSphereCenter, rayDir);

			// the ray hit the sphere if its minimal analytical distance along the ray is more than the radius
			return k > 0.0 && k < maxDistance && (rayDir * k - rayStartToSphereCenter).sqrMagnitude < sphereRadius * sphereRadius;
		}

		#endregion

		#region TEMPERATURE

		/// <summary> return CMB irradiance in W/m2 </summary>
		public static double BackgroundFlux => 3.14E-6;

		/// <summary> return temperature in K from irradiance in W/m2, as per Stefan-Boltzmann equation </summary>
		public static double BlackBodyTemperature(double flux)
		{
			return Math.Pow(flux / PhysicsGlobals.StefanBoltzmanConstant, 0.25);
		}

		/// <summary> return irradiance in W/m2 from temperature in K, as per Stefan-Boltzmann equation </summary>
		public static double BlackBodyFlux(double temperature)
		{
			return Math.Pow(temperature, 4.0) * PhysicsGlobals.StefanBoltzmanConstant;
		}

		// TODO : move this to the step sim and :
		// - scale BackgroundFlux with atmo absorbtion
		// - assuming this is a surface temperature, this doesn't really make sense,
		//   we can't just add everything and ignore each irradiance component direction
		//   ex : albedo will usually be directed toward the "dark side" of the vessel
		public static double VesselTemperature(double irradiance)
		{
			return BlackBodyTemperature(irradiance + BackgroundFlux);
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

		private static double homeAtmDensityASL;

		public static double GetSolarPowerFactor(double density)
		{
			double num2 = (1.0 - PhysicsGlobals.SolarInsolationAtHome) * homeAtmDensityASL;
			return num2 / (num2 + density * PhysicsGlobals.SolarInsolationAtHome);
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
		public static bool InBreathableAtmosphere(Vessel v, bool inAtmosphere, bool underwater)
		{
			// a vessel is inside a breathable atmosphere if:
			// - it is inside an atmosphere
			// - the body atmosphere is flagged as containing oxygen
			// - it isn't underwater
			return inAtmosphere && v.mainBody.atmosphereContainsOxygen && !underwater;
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
			double dist = Vector3d.Distance(v.GetWorldPos3D(), GetParentStar(v.mainBody).position);
			return 1.0 - Math.Min(AU, 1.0); // 0 at 1AU -> 1 at sun position
		}
		#endregion

		#region SIGNAL

		private static double dampingExponent = 0;
		public static double DataRateDampingExponent
		{
			get
			{
				if (dampingExponent != 0)
					return dampingExponent;

				if (Settings.DampingExponentOverride != 0)
					return Settings.DampingExponentOverride;

				// KSP calculates the signal strength using a cubic formula based on distance (see below).
				// Based on that signal strength, we calculate a data rate. The goal is to get data rates that
				// are comparable to what NASA gets near Mars, depending on the distance between Earth and Mars
				// (~0.36 AU - ~2.73 AU).
				// The problem is that KSPs formula would be somewhat correct for signal strength in reality,
				// but the stock system is only 1/10th the size of the real solar system. Picture this: Jools
				// orbit is about as far removed from the sun as the real Mercury, which means that all other
				// planets would orbit the sun at a distance that is even smaller. In game, distance plays a
				// much smaller role than it would in reality, because the in-game distances are very small,
				// so signal strength just doesn't degrade fast enough with distance.
				//
				// We cannot change how KSP calculates signal strength, so we apply a damping formula
				// for the data rate. Basically, it goes like this:
				//
				// data rate = base rate * signal strength
				// (base rate would be the max. rate at 0 distance)
				//
				// To degrade the data rate with distance, Kerbalism will do this instead:
				//
				// data rate = base rate * (signal strength ^ damping exponent)
				// (this works because signal strength will always be in the range [0..1])
				//
				// The problem is, we don't know which solar system we'll be in, and how big it will be.
				// Popular systems like JNSQ are 2.7 times bigger than stock, RSS is 10 times bigger.
				// So we try to find a damping exponent that gives good results for the solar system we're in,
				// based on the distance of the home planet to the sun (1 AU).

				// range of DSN at max. level
				var maxDsnRange = GameVariables.Instance.GetDSNRange(1f);

				// signal strength at ~ average earth - mars distance
				var strengthAt2AU = SignalStrength(maxDsnRange, 2 * AU);

				// For our estimation, we assume a base rate similar to the stock communotron 88-88
				var baseRate = 0.48;

				// At 2 AU, this is the rate we want to get out of it
				var desiredRateAt2AU = 0.3;

				// dataRate = baseRate * (strengthAt2AU ^ exponent)
				// so...
				// exponent = log_strengthAt2AU(dataRate / baseRate)
				dampingExponent = Math.Log(desiredRateAt2AU / baseRate, strengthAt2AU);

				Lib.Log($"Calculated DataRateDampingExponent: {dampingExponent.ToString("F4")} (max. DSN range: {maxDsnRange.ToString("F0")}, strength at 2 AU: {strengthAt2AU.ToString("F3")})");

				return dampingExponent;
			}
		}

		public static double SignalStrength(double maxRange, double distance)
		{
			if (distance > maxRange)
				return 0.0;

			double relativeDistance = 1.0 - (distance / maxRange);
			double strength = (3.0 - (2.0 * relativeDistance)) * (relativeDistance * relativeDistance);

			if (strength < 0)
				return 0.0;

			return strength;
		}

		// I have no idea what to do about RT, so we're using the stock formula value
		public static double DataRateDampingExponentRT { get { return DataRateDampingExponentRT; } }

		#endregion
	}
}
