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
						
					}
				}
			}

			// if nothing was found, assume the sun is the stock default
			if (suns.Count == 0)
			{
				suns.Add(new SunData(0, PhysicsGlobals.SolarLuminosityAtHome));
			}

			// calculate each sun total flux (must be done after the "suns" list is populated
			CelestialBody home = FlightGlobals.GetHomeBody();
			foreach (SunData sd in suns)
			{
				sd.InitSolarFluxTotal();

				// in some cases, weird Kopernicus (binary home stars ?) systems might imply a solar luminosity at home 
				// that is higher than what is defined in PhysicsGlobals.SolarLuminosityAtHome.
				// Can't remember what case exactly, if that code is causing issues,
				// remove it and just always use PhysicsGlobals.SolarLuminosityAtHome
				double distance = (home.position - sd.body.position).magnitude;
				double sunSolarFluxAtHome = sd.SolarFlux(distance, false);
				if (sunSolarFluxAtHome > 0.1)
				{
					SolarFluxAtHome += sunSolarFluxAtHome;
				}
			}

			// The above calculations will likely end up with a slightly lower SolarFluxAtHome than expected, correct if necessary.
			if (SolarFluxAtHome < PhysicsGlobals.SolarLuminosityAtHome)
			{
				SolarFluxAtHome = PhysicsGlobals.SolarLuminosityAtHome;
			}

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

		/// <summary>Fraction of orbital period in shadow for a vessel.<br/>
		/// Limitations:<br/>
		/// 1. This method assumes the orbit is an ellipse / circle which is not
		/// changing or being altered by other bodies.<br/>
		/// 2. It assumes the sun's rays are parallel across the orbiting
		/// planet, although all bodies are small enough and far enough from the
		/// sun for this to be nearly true.<br/>
		/// 3. The method does not take into account darkness caused by eclipses
		/// of a different body than the orbited body, for example, orbiting
		/// Laythe but Jool blocks the sun.<br/>
		/// 4. For elliptical orbits, this method approximates the actual
		/// amount of occlusion encountered when beta angle is below the
		/// threshold for constant sun (beta*), by taking advantage of the fact
		/// that the difference 1 degree makes being larger as beta* approaches
		/// zero, at the same time as the proportional increase in occlusion
		/// _area_ tapers off as the plane approaches the body's horizon. This
		/// is anything but exact, but is close enough for reasonable results.
		/// </summary>
		public static double EclipseFraction(Vessel v, CelestialBody sun, Vector3d sunVec)
		{
			var obt = v.orbitDriver?.orbit;
			if (obt == null)
				return 0;

			bool incNaN = double.IsNaN(obt.inclination);
			if (Lib.Landed(v) || incNaN)
			{
				var mb = v.mainBody;
				if (sun == mb)
				{
					return 0;
				}

				if (mb.referenceBody == sun && mb.tidallyLocked)
				{
					Vector3d vPos = incNaN ? (Vector3d)v.transform.position : v.orbitDriver.pos + mb.position;
					// We have to refind orbit pos in case inc is NaN
					return Vector3d.Dot(sunVec, mb.position - vPos) < 0 ? 0 : 1.0;
				}

				// Just assume half the time, since for non-tidally-locked
				// bodies without axial tilt, for sufficiently large timesteps
				// half the time will be spent in sunlight.
				return 0.5;
			}

			double e = obt.eccentricity;
			if (e >= 1d)
			{
				// This is wrong, of course, but given the speed of an escape trajectory
				// you'll be in shadow for a very miniscule fraction of the period.
				return 0;
			}
			Vector3d planeNormal = Vector3d.Cross(v.orbitDriver.vel, -v.orbitDriver.pos).normalized;
			double sunDot = Math.Abs(Vector3d.Dot(sunVec, planeNormal));
			double betaAngle = Math.PI * 0.5d - Math.Acos(sunDot);

			double a = obt.semiMajorAxis;
			double R = obt.referenceBody.Radius;

			// Now, branch depending on if we're in a low-ecc orbit
			// We check locally for betaStar because we might bail early in the Kerbalism case
			double frac;
			if (e < 0.1d)
				frac = FracEclipseCircular(betaAngle, a, R);
			else
				frac = FracEclipseElliptical(v, betaAngle, a, R, e, sunVec);

			return frac;
		}

		/// <summary>
		/// This computes eclipse fraction for circular orbits
		/// (well, realy circular _low_ orbits, but at higher altitudes
		/// you're not spending much time in shadow anyway).
		/// </summary>
		/// <param name="betaAngle">The beta angle (angle between the solar normal and its projection on the orbital plane)</param>
		/// <param name="sma">The semi-major axis</param>
		/// <param name="R">The body's radius</param>
		/// <returns></returns>
		private static double FracEclipseCircular(double betaAngle, double sma, double R)
		{
			// from https://commons.erau.edu/cgi/viewcontent.cgi?article=1412&context=ijaaa
			// beta* is the angle above which there is no occlusion of the orbit
			double betaStar = Math.Asin(R / sma);
			if (Math.Abs(betaAngle) >= betaStar)
				return 0;

			double avgHeight = sma - R;
			return (1.0 / Math.PI) * Math.Acos(Math.Sqrt(avgHeight * avgHeight + 2.0 * R * avgHeight) / (sma * Math.Cos(betaAngle)));
		}

		/// <summary>
		/// An analytic solution to the fraction of an orbit eclipsed by its primary
		/// </summary>
		/// <param name="v">The vessel</param>
		/// <param name="betaAngle">The beta angle (angle between the solar normal and its projection on the orbital plane)</param>
		/// <param name="a">semi-major axis</param>
		/// <param name="R">body radius</param>
		/// <param name="e">eccentricity</param>
		/// <param name="sunVec">The normalized vector to the sun</param>
		/// <returns></returns>
		private static double FracEclipseElliptical(Vessel v, double betaAngle, double a, double R, double e, Vector3d sunVec)
		{
			var obt = v.orbit;
			double b = obt.semiMinorAxis;
			// Just bail if we were going to report NaN, or we're in a weird state
			// We've likely avoided this already due to the eccentricity check in the main call, though
			if (a < b || b < R)
				return 0;

			// Compute where the Pe is with respect to the sun
			Vector3d PeToBody = -Planetarium.Zup.WorldToLocal(obt.semiLatusRectum / (1d + e) * obt.OrbitFrame.X).xzy;
			Vector3d orthog = Vector3d.Cross(obt.referenceBody.GetFrameVel().xzy.normalized, sunVec);
			Vector3d PeToBodyProj = (PeToBody - orthog * Vector3d.Dot(PeToBody, orthog)).normalized;
			// Use these to calculate true anomaly for this projected orbit
			double tA = Math.Acos(Vector3d.Dot(sunVec, PeToBodyProj));

			// Get distance to ellipse edge
			double r = a * (1.0 - e * e) / (1.0 + e * Math.Cos(tA));

			double betaStar = Math.Asin(R / r);
			double absBeta = Math.Abs(betaAngle);
			if (absBeta >= betaStar)
				return 0d;

			// Get the vector to the center of the eclipse
			double vecToHalfEclipsePortion = Math.Asin(R / r);
			// Get the true anomalies at the front and rear of the eclipse portion
			double vAhead = tA + vecToHalfEclipsePortion;
			double vBehind = tA - vecToHalfEclipsePortion;
			vAhead *= 0.5;
			vBehind *= 0.5;
			double ePlusOneSqrt = Math.Sqrt(1 + e);
			double eMinusOneSqrt = Math.Sqrt(1 - e);
			// Calculate eccentric and mean anomalies
			double EAAhead = 2.0 * Math.Atan2(eMinusOneSqrt * Math.Sin(vAhead), ePlusOneSqrt * Math.Cos(vAhead));
			double MAhead = EAAhead - e * Math.Sin(EAAhead);
			double EABehind = 2.0 * Math.Atan2(eMinusOneSqrt * Math.Sin(vBehind), ePlusOneSqrt * Math.Cos(vBehind));
			double Mbehind = EABehind - e * Math.Sin(EABehind);
			// Finally, calculate the eclipse fraction from mean anomalies
			double eclipseFrac = (MAhead - Mbehind) / (2.0 * Math.PI);
			// This is not quite correct I think, but it'll be close enough.
			// We just lerp between 0 occlusion at beta = betaStar, and full occlusion
			// at beta = 0. This takes advantage of the difference 1 degree makes being larger
			// as beta approaches zero, at the same time as the proportional increase in
			// occlusion *area* tapers off as the plane approaches the body's horizon.
			return eclipseFrac * absBeta / betaStar;
		}

		/// <summary>
		/// This expects to be called repeatedly
		/// </summary>
		public static double SampleSunFactor(Vessel v, double elapsedSeconds, CelestialBody sun)
		{
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.Sim.SunFactor2");

			bool isSurf = Lib.Landed(v);
			if (v.orbitDriver == null || v.orbitDriver.orbit == null || (!isSurf && double.IsNaN(v.orbit.inclination)))
			{
				UnityEngine.Profiling.Profiler.EndSample();
				return 1d; // fail safe
			}

			int sunSamples = 0;

			var now = Planetarium.GetUniversalTime();

			var vd = v.KerbalismData();
			List<CelestialBody> occluders = vd.EnvVisibleBodies;

			// set up CB position caches
			bodyCache.SetForOccluders(occluders);

			// Set max time to calc
			double maxCalculation = elapsedSeconds * 1.01d;

			// cache values for speed
			double semiLatusRectum = 0d;
			CelestialBody mb = v.mainBody;
			Vector3d surfPos;
			Vector3d polarAxis;
			if (isSurf)
			{
				surfPos = mb.GetRelSurfacePosition(v.latitude, v.longitude, v.altitude);
                // Doing this manually instead of swizzling surfPos avoids one of the two swizzles
                surfPos = (surfPos.x * mb.BodyFrame.X + surfPos.z * mb.BodyFrame.Y + surfPos.y * mb.BodyFrame.Z).xzy;

				// This will not be quite correct for Principia but at least it's
				// using the BodyFrame, which Principia clobbers, rather than the
				// transform.
				polarAxis = mb.BodyFrame.Rotation.swizzle * Vector3d.up;
			}
			else
			{
				semiLatusRectum = v.orbit.semiLatusRectum;
				maxCalculation = Math.Min(maxCalculation, v.orbit.period);
				surfPos = new Vector3d();
				polarAxis = new Vector3d();
			}

			// Set up timimg
			double stepLength = Math.Max(120d, elapsedSeconds * (1d / 40d));
			int sampleCount;
			if (stepLength > maxCalculation)
			{
				stepLength = maxCalculation;
				sampleCount = 1;
			}
			else
			{
				sampleCount = (int)Math.Ceiling(maxCalculation / stepLength);
				stepLength = maxCalculation / (double)sampleCount;
			}

			for (int i = sampleCount; i-- > 0;)
			{
				double ut = now - i * stepLength;
				bodyCache.SetForUT(ut, occluders);
				Vector3d bodyPos = bodyCache.GetBodyPosition(mb.flightGlobalsIndex);
				Vector3d pos;
				if (isSurf)
				{
					// Rotate the surface position based on where the body would have rotated in the past
					// Note: rotation is around *down* so we flip the sign of the rotation
					pos = QuaternionD.AngleAxis(mb.rotPeriodRecip * i * stepLength * 360d, polarAxis) * surfPos;
				}
				else
				{
					pos = FastGetRelativePositionAtUT(v.orbit, ut, semiLatusRectum);
				}
				// Apply the body's position
				pos += bodyPos;

				bool vis = IsSunVisibleAtTime(v, pos, sun, occluders, isSurf);
				if (vis)
					++sunSamples;
			}

			UnityEngine.Profiling.Profiler.EndSample();

			double sunFactor = (double)sunSamples / (double)sampleCount;
			//Lib.Log("Vessel " + v + " sun factor: " + sunFactor + " " + sunSamples + "/" + sampleCount + " #s=" + sampleCount + " e=" + elapsedSeconds + " step=" + stepLength);
			return sunFactor;
		}

		/// <summary>
		/// A version of IsBodyVisibleAt that is optimized for suns
		/// and supports using arbitrary time (assuming bodyCache is set)
		/// </summary>
		/// <param name="vessel"></param>
		/// <param name="vesselPos">Vessel position at time</param>
		/// <param name="sun"></param>
		/// <param name="sunIdx">The body index of the sun</param>
		/// <param name="occluders"></param>
		/// <param name="UT"></param>
		/// <param name="isSurf">is the vessel landed</param>
		/// <returns></returns>
		internal static bool IsSunVisibleAtTime(Vessel vessel, Vector3d vesselPos, CelestialBody sun, List<CelestialBody> occluders, bool isSurf)
		{
			// generate ray parameters
			Vector3d sunPos = bodyCache.GetBodyPosition(sun.flightGlobalsIndex) - vesselPos;
			var sunDir = sunPos;
			var sunDist = sunDir.magnitude;
			sunDir /= sunDist;
			sunDist -= sun.Radius;

			// for very small bodies the analytic method is very unreliable at high latitudes
			// So we use a modified version of the analytic method (unlike IsBodyVisible)
			bool ignoreMainbody = false;
			if (isSurf && vessel.mainBody.Radius < 100000.0)
			{
				ignoreMainbody = true;
				Vector3d mainBodyPos = bodyCache.GetBodyPosition(vessel.mainBody.flightGlobalsIndex);
				Vector3d mainBodyDir = (mainBodyPos - vesselPos).normalized;
				double dotSunBody = Vector3d.Dot(mainBodyDir, sunDir);
				Vector3d mainBodyDirProjected = mainBodyDir * dotSunBody;

				// Assume the sun is far enough away that we can treat the line from the vessel
				// to the sun as parallel to the line from the body center to the sun, which means
				// we can ignore testing further if we're very close to the plane orthogonal to the
				// sun vector but on the opposite side of the body from the sun.
				// We don't strictly test dot to give ourselves approx half a degree of slop
				if (mainBodyDirProjected.sqrMagnitude > 0.0001d && dotSunBody > 0d)
				{
					return false;
				}
			}

			// check if the ray intersect one of the provided bodies
			for (int i = occluders.Count; i-- > 0;)
			{
				CelestialBody occludingBody = occluders[i];
				if (occludingBody == sun)
					continue;
				if (ignoreMainbody && occludingBody == vessel.mainBody)
					continue;

				Vector3d toBody = bodyCache.GetBodyPosition(occludingBody.flightGlobalsIndex) - vesselPos;
				// projection of origin->body center ray over the raytracing direction
				double k = Vector3d.Dot(toBody, sunDir);
				// the ray doesn't hit body if its minimal analytical distance along the ray is less than its radius
				// simplified from 'start + dir * k - body.position'
				bool hit = k > 0d && k < sunDist && (sunDir * k - toBody).magnitude < occludingBody.Radius;
				if (hit)
					return false;
			}

			return true;
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

		/// <summary>
		/// A cache to speed calculation of body positions at a given UT, based on
		/// as set of occluders. Used when calculating solar exposure at analytic rates.
		/// This creates storage for each body in FlightGlobals, but only caches
		/// a lookup from occluder to body index, and then for each relevant occluder
		/// and its parents a lookup to each parent (and on up the chain) and the
		/// semilatus rectum. Then when set for a UT, it calculates positions
		/// for each occluder on up the chain to the root CB.
		/// </summary>
		public class BodyCache
		{
			private Vector3d[] positions = null;
			private int[] parents;
			private double[] semiLatusRectums;

			public Vector3d GetBodyPosition(int idx) { return positions[idx]; }

			/// <summary>
			/// Check and, if uninitialized, setup the body caches
			/// </summary>
			private void CheckInitBodies()
			{
				int c = FlightGlobals.Bodies.Count;
				if (positions != null && positions.Length == c)
					return;

				positions = new Vector3d[c];
				parents = new int[c];
				semiLatusRectums = new double[c];

				for (int i = 0; i < c; ++i)
				{
					var cb = FlightGlobals.Bodies[i];
					// Set parent index lookup
					var parent = cb.orbitDriver?.orbit?.referenceBody;
					if (parent != null && parent != cb)
					{
						parents[i] = parent.flightGlobalsIndex;
					}
					else
					{
						parents[i] = -1;
					}
				}
			}

			/// <summary>
			/// Initialize the cache for a set of occluders. This
			/// will set up the lookups for the occluder bodies and
			/// cache the semi-latus recturm for each body and its
			/// parents
			/// </summary>
			/// <param name="occluders"></param>
			public void SetForOccluders(List<CelestialBody> occluders)
			{
				CheckInitBodies();

				// Now clear all SLRs and then set only the relevant ones
				// (i.e. the occluders, their parents, their grandparents, etc)
				for (int i = semiLatusRectums.Length; i-- > 0;)
					semiLatusRectums[i] = double.MaxValue;
				for (int i = occluders.Count; i-- > 0;)
					SetSLRs(occluders[i].flightGlobalsIndex);
			}

			private void SetSLRs(int i)
			{
				// Check if set
				if (semiLatusRectums[i] != double.MaxValue)
					return;

				// Check if parent
				int pIdx = parents[i];
				if (pIdx == -1)
				{
					semiLatusRectums[i] = 1d;
					return;
				}

				semiLatusRectums[i] = FlightGlobals.Bodies[i].orbit.semiLatusRectum;
				SetSLRs(pIdx);
			}

			/// <summary>
			/// Set the occluder body positions at the given UT
			/// </summary>
			/// <param name="ut"></param>
			public void SetForUT(double ut, List<CelestialBody> occluders)
			{
				// Start from unknown positions
				for (int i = positions.Length; i-- > 0;)
					positions[i] = new Vector3d(double.MaxValue, double.MaxValue);

				// Fill positions at UT, recursively (skipping calculated parents)
				for (int i = occluders.Count; i-- > 0;)
					SetForUTInternal(occluders[i].flightGlobalsIndex, ut);
			}

			private void SetForUTInternal(int i, double ut)
			{
				// If we've already been here, bail
				if (positions[i].x != double.MaxValue)
					return;

				// Check if we have a parent. If not
				// position is just the body's position
				var cb = FlightGlobals.Bodies[i];
				int pIdx = parents[i];
				if (pIdx == -1)
				{
					positions[i] = cb.position;
					return;
				}

				// If we do have a parent, recurse and then
				// set position based on newly-set parent's pos
				SetForUTInternal(pIdx, ut);
				positions[i] = positions[pIdx] + FastGetRelativePositionAtUT(cb.orbit, ut, semiLatusRectums[i]);
			}
		}

		/// <summary>
		/// A fast version of KSP's GetRelativePositionAtUT.
		/// It skips a bunch of steps and uses cached values
		/// </summary>
		/// <param name="orbit"></param>
		/// <param name="UT"></param>
		/// <param name="semiLatusRectum"></param>
		/// <returns></returns>
		private static Vector3d FastGetRelativePositionAtUT(Orbit orbit, double UT, double semiLatusRectum)
		{
			double T = orbit.getObtAtUT(UT);

			double M = T * orbit.meanMotion;
			double E = orbit.solveEccentricAnomaly(M, orbit.eccentricity);
			double v = orbit.GetTrueAnomaly(E);

			double cos = Math.Cos(v);
			double sin = Math.Sin(v);
			Vector3d pos = semiLatusRectum / (1.0 + orbit.eccentricity * cos) * (orbit.OrbitFrame.X * cos + orbit.OrbitFrame.Y * sin);
			return Planetarium.Zup.WorldToLocal(pos).xzy;
		}

		static readonly BodyCache bodyCache = new BodyCache();

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
		public static double depthfactor = 0.1;

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
				body.GetSolarAtmosphericEffects(Vector3d.Dot(up, sun_dir), density, out _, out double stockFluxFactor);
				return stockFluxFactor;
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
				body.GetSolarAtmosphericEffects(cos_a, density, out _, out double stockFluxFactor);
				return stockFluxFactor;
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
				double atmo_factor_analytic = 0.0;
				for (int i = 0; i < 3; i++)
				{
					body.GetSolarAtmosphericEffects(Vector3d.Dot(radialOut, sunDirs[i]), density, out _, out double stockFluxFactor);
					atmo_factor_analytic += stockFluxFactor;
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
		#endregion

		#region GRAVIOLI
		public static double Graviolis(Vessel v)
		{
			double dist = Vector3d.Distance(v.GetWorldPos3D(), Lib.GetParentSun(v.mainBody).position);
			double au = dist / FlightGlobals.GetHomeBody().orbit.semiMajorAxis;
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
				// Value selected so we match pre-comms refactor damping exponent of ~6 in stock
				var desiredRateAt2AU = 0.3925;

				// dataRate = baseRate * (strengthAt2AU ^ exponent)
				// so...
				// exponent = log_strengthAt2AU(dataRate / baseRate)
				dampingExponent = Math.Log(desiredRateAt2AU / baseRate, strengthAt2AU);

				Lib.Log($"Calculated DataRateDampingExponent: {dampingExponent.ToString("F4")} (max. DSN range: {maxDsnRange.ToString("F0")}, strength at 2 AU: {strengthAt2AU.ToString("F3")})");

				return dampingExponent;
			}
		}

		public static double DataRateDampingExponentRT
		{
			get
			{
				if (dampingExponent != 0)
					return dampingExponent;

				if (Settings.DampingExponentOverride != 0)
					return Settings.DampingExponentOverride;

				// Since RemoteTech Mission Control KSC maximum range never exceeds 75Mm we can't use exactly the same logic as CommNet.
				// What we do here is take Duna as a reference and pretend that it is Mars.

				// Lets take a look at some real world examples
				// https://www.researchgate.net/figure/Calculation-of-Received-Power-from-space-probes-based-on-online-DSN-data-given-on-May_tbl1_308019760	

				// [ Satellite ]	[ Power output ]	[ Distance from Earth ]		[ Data rate ]
				//	Voyager 1			20W, 47 dBi			134 au						159 bps
				//	Voyager 2			20W, 47 dBi			111 au						160 bps
				//	New Horizons		12W, 42 dBi			35 au						4.21 kbps
				//	Cassini				20W, 46.6 dBi		9 au						22.12 kbps
				//	Dawn				100W, 39.6 dBi		3.5 au						125 kbps
				//	Rosetta				28W, 42.5 dBi		2.8 au						52.42 kbps
				//	SOHO (omni ant)		10W					4.4 Moon distance			245.76 kbps

				// https://mars.nasa.gov/mro/mission/communications/
				// Also, the Mars Reconnaissance Orbiter sends data to Earth, the data rate is about 500 to 4000 kilobits per second.
				// ie. between 62.5 kB/s - 500 kB/s
				// https://en.wikipedia.org/wiki/Mars_Reconnaissance_Orbiter
				// The spacecraft carries two 100-watt X-band amplifiers (one of which is a backup), one 35-watt Ka-band amplifier
				// so, 135 W to transmit?

				// For our estimation, we assume a base reach similar to the Reflectron KR-14 that has
				// similar specs to Mars Reconnaissance Orbiter
				double testRange = 60000000000.0; // 60Gm

				// signal strength at ~ farthest earth - mars distance, Duna is at 2.53 au with stock ksp solar sytem
				double strengthAt2AU = SignalStrength(testRange, 2.53 * AU);	// 34.4 Gm w stock solar system

				// For our estimation, we assume a base rate similar to the Reflectron KR-14
				double baseRate = 0.4815;

				// At 2 AU, this is the rate we want to get out of it
				double desiredRateAt2AU = 0.05;

				// dataRate = baseRate * (strengthAt2AU ^ exponent)
				// so...
				// exponent = log_strengthAt2AU(dataRate / baseRate)
				dampingExponent = Math.Log(desiredRateAt2AU / baseRate, strengthAt2AU);

				// 2.4 seems good for RemoteTech
				if (double.IsNaN(dampingExponent))
				{
					Lib.Log("dampingExponent is " + dampingExponent + ",... setting to 2.4");
					dampingExponent = 2.4;
				}

				return DataRateDampingExponent;
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

		#endregion
	}
} // KERBALISM

