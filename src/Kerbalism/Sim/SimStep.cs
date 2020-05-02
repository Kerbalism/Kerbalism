using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	public class SimStep
	{
		private static List<SimStep> stepWorkerPool = new List<SimStep>(10000);
		private static ConcurrentQueue<int> freeWorkerSteps = new ConcurrentQueue<int>();

		public static SimStep GetFromWorkerPool()
		{
			if (freeWorkerSteps.IsEmpty)
			{
				SimStep newStep = new SimStep();
				stepWorkerPool.Add(newStep);
				newStep.stepPoolIndex = stepWorkerPool.Count - 1;
				return newStep;
			}

			if (freeWorkerSteps.TryDequeue(out int index))
				return stepWorkerPool[index];

			return new SimStep();
		}

		private int stepPoolIndex;

		// step results
		public double bodiesCoreFlux;
		public StarFlux[] starFluxes;

		// step parameters
		private SimVessel simVessel;
		private double ut;

		private Vector3d vesselPosition;

		private Vector3d mainBodyPosition;
		private Vector3d mainBodyDirection;
		private double altitude;
		private bool mainBodyIsVisible;
		private bool mainBodyIsMoon;
		private SimBody mainPlanet;
		private bool mainPlanetIsVisible;
		private Vector3d mainPlanetPosition;

		public double UT => ut;
		public double Altitude => altitude;

		private SimBody[] Bodies => simVessel.Bodies;
		private bool Landed => simVessel.landed;
		private SimBody MainBody => simVessel.mainBody;

		public SimStep()
		{
			stepPoolIndex = -1;
			starFluxes = StarFlux.StarArrayFactory();
		}

		public void ReleaseToWorkerPool()
		{
			if (stepPoolIndex < 0)
				return;

			freeWorkerSteps.Enqueue(stepPoolIndex);
		}

		public void Init(SimVessel simVessel, double ut = -1.0)
		{
			this.ut = ut;
			this.simVessel = simVessel;
			vesselPosition = simVessel.GetPosition(this);
			mainBodyPosition = MainBody.GetPosition(ut);

			mainBodyDirection = mainBodyPosition - vesselPosition;
			altitude = mainBodyDirection.magnitude;
			mainBodyDirection /= altitude;
			altitude -= MainBody.radius;

			foreach (SimBody body in Bodies)
			{
				body.stepCachePosition = body.GetPosition(ut);

				// vector from ray origin to sphere center
				body.stepCacheOcclusionDiff = body.stepCachePosition - vesselPosition;

				// if apparent diameter < ~10 arcmin (~0.003 radians), don't consider the body for occlusion checks
				// real apparent diameters at earth : sun/moon ~ 30 arcmin, Venus ~ 1 arcmin max
				body.stepCacheIsOccluding = (body.radius * 2.0) / body.stepCacheOcclusionDiff.magnitude > 0.003;
			}

			mainBodyIsVisible = IsMainBodyVisible();
			mainBodyIsMoon = !MainBody.ReferenceBody.isSun;
			if (mainBodyIsMoon)
			{
				mainPlanet = MainBody.ReferenceBody;
				mainPlanetIsVisible = IsMainPlanetVisible();
				mainPlanetPosition = mainPlanet.GetPosition(ut);
			}
			else
			{
				mainPlanetIsVisible = false;
			}
		}

		public void Evaluate()
		{
			AnalyzeSunFluxes();
			AnalyzeBodiesCoreFluxes();
		}

		private bool IsMainBodyVisible()
		{
			if (Landed || (MainBody.hasAtmosphere && altitude < MainBody.atmosphereDepth))
				return true;

			foreach (SimBody occludingBody in Bodies)
			{
				if (occludingBody == MainBody)
					continue;

				if (Sim.RayHitSphere(occludingBody.stepCacheOcclusionDiff, mainBodyDirection, occludingBody.radius, altitude))
					return false;
			}

			return true;
		}

		private bool IsMainPlanetVisible()
		{
			Vector3d vesselToPlanet = mainPlanetPosition - vesselPosition;
			double distance = vesselToPlanet.magnitude;
			vesselToPlanet /= distance;

			foreach (SimBody occludingBody in Bodies)
			{
				if (occludingBody == mainPlanet)
					continue;

				if (Sim.RayHitSphere(occludingBody.stepCacheOcclusionDiff, vesselToPlanet, occludingBody.radius, distance))
					return false;
			}

			return true;
		}

		private void AnalyzeSunFluxes()
		{
			foreach (StarFlux starFlux in starFluxes)
			{
				SimBody sun = Bodies[starFlux.Star.body.flightGlobalsIndex];

				Vector3d sunPosition = sun.GetPosition(ut);

				// generate ray parameters
				starFlux.direction = sunPosition - vesselPosition;
				starFlux.distance = starFlux.direction.magnitude;
				starFlux.direction /= starFlux.distance;

				bool isOccluded = false;
				foreach (SimBody occludingBody in Bodies)
				{
					if (occludingBody == sun)
						continue;

					if (Sim.RayHitSphere(occludingBody.stepCacheOcclusionDiff, starFlux.direction, occludingBody.radius, starFlux.distance))
					{
						isOccluded = true;
						break;
					}
				}

				// direct flux from this sun
				starFlux.directRawFlux = starFlux.Star.SolarFlux(starFlux.distance);

				if (isOccluded)
				{
					starFlux.directFlux = 0.0;
				}
				else
				{
					starFlux.directFlux = starFlux.directRawFlux;

					if (MainBody.hasAtmosphere && altitude < MainBody.atmosphereDepth)
						starFlux.directFlux *= Sim.AtmosphereFactor(MainBody, mainBodyPosition, starFlux.direction, vesselPosition, altitude);
				}

				// get indirect fluxes from bodies
				starFlux.bodiesAlbedoFlux = 0.0;
				starFlux.bodiesEmissiveFlux = 0.0;
				if (!MainBody.isSun)
				{
					if (mainBodyIsVisible)
					{
						Vector3d mainBodyToSun = sunPosition - mainBodyPosition;
						double mainBodyToSunDist = mainBodyToSun.magnitude;
						mainBodyToSun /= mainBodyToSunDist;

						bool mainBodyHasSunLoS = true;
						if (mainBodyIsMoon)
						{
							Vector3d moonToPlanet = mainPlanetPosition - mainBodyPosition;
							mainBodyHasSunLoS = !Sim.RayHitSphere(moonToPlanet, mainBodyToSun, mainPlanet.radius, mainBodyToSunDist);
						}

						GetBodyIndirectSunFluxes(starFlux, MainBody, mainBodyPosition, sunPosition, mainBodyToSunDist, mainBodyHasSunLoS);
					}

					// if main body is a moon, also consider fluxes from the planet
					if (mainBodyIsMoon && mainPlanetIsVisible)
					{
						double mainPlanetToSunDist = (sunPosition - mainPlanetPosition).magnitude;
						GetBodyIndirectSunFluxes(starFlux, mainPlanet, mainPlanetPosition, sunPosition, mainPlanetToSunDist, true);
					}
				}
			}
		}

		/// <summary>
		/// Get solar flux re-emitted by the body at the vessel position
		/// We work on the assumption that all solar flux blocked by the body disc
		/// is reflected back to space, either directly (albedo) or trough thermal re-emission.
		/// </summary>
		/// <param name="starFlux">Sun fluxes data to update</param>
		/// <param name="sunFluxAtBody">flux in W/m² received by this body from the considered sun</param>
		/// <param name="bodyIsVisibleFromSun">false if the sun LOS for a moon is blocked by it's parent planet</param>
		private void GetBodyIndirectSunFluxes(StarFlux starFlux, SimBody body, Vector3d bodyPosition, Vector3d sunPosition, double bodyToSunDist, bool bodyIsVisibleFromSun)
		{
			// Get solar flux re-emitted by the body at the vessel position
			// We work on the assumption that all solar flux blocked by the body disc
			// is reflected back to space, either directly (albedo) or trough thermal re-emission.
			double sunFluxAtBody = starFlux.Star.SolarFlux(bodyToSunDist);

			// ALBEDO
			double albedoFlux = 0.0;
			if (bodyIsVisibleFromSun)
			{
				// with r = body radius,
				// with a = altitude,
				// - The total energy received by the exposed surface area (disc) of the body is :
				// sunFluxAtBody * π * r²
				// - Assuming re-emitted power is spread over **one hemisphere**, that is a solid angle of :
				// 2 * π steradians
				// - So the energy emitted in watts per steradian can be expressed as :
				// sunFluxAtBody * π * r² / (2 * π * steradian)
				// - The sphere enclosing the body at the given altitude has a surface area of
				// 4 * π * (r + a)² 
				// - This translate in a surface area / steradian of
				// 4 * π * (r + a)² / (2 * π steradian) = (r + a)² / steradian
				// - So the flux received at the current altitude is :
				// sunFluxAtBody * π * r² / (2 * π * steradian) / ((r + a)² / steradian)
				// - Which can be simplified to :
				// (sunFluxAtBody * r²) / (2 * (r + a)²))
				double hemisphericFluxAtAltitude = (sunFluxAtBody * body.radius * body.radius) / (2.0 * Math.Pow(body.radius + altitude, 2.0));
				albedoFlux = hemisphericFluxAtAltitude * body.albedo;

				// ALDEBO COSINE FACTOR
				// the full albedo flux is received only when the vessel is positioned along the sun-body axis, and goes
				// down to zero on the night side.
				Vector3d bodyToSun = (sunPosition - bodyPosition).normalized;
				Vector3d bodyToVessel = (vesselPosition - bodyPosition).normalized;
				double anglefactor = (Vector3d.Dot(bodyToSun, bodyToVessel) + 1.0) / 2.0;
				// In addition of the crescent-shaped illuminated portion making the angle/flux relation non-linear,
				// the flux isn't scattered uniformely but tend to be reflected back in the sun direction,
				// especially on non atmospheric bodies, see https://en.wikipedia.org/wiki/Opposition_surge
				// We do some square scaling to approximate those effects, the choosen exponents seems to give results that 
				// aren't too far from RL data given by the JPL-HORIZONS data set : https://ssd.jpl.nasa.gov/horizons.cgi
				if (body.hasAtmosphere)
				{
					albedoFlux *= Math.Pow(anglefactor * 1.1, 2.0);
				}
				else
				{
					albedoFlux *= Math.Pow(anglefactor * 1.25, 3.0);
				}
			}

			// THERMAL RE-EMISSION
			// We account for this even if the body is currently occluded from the sun
			// We use the same formula, excepted re-emitted power is spread over the full
			// body sphere, that is a solid angle of 4 * π steradians
			// The end formula becomes :
			// (sunFluxAtBody * r²) / (4 * (r + a)²)
			double sphericFluxAtAltitude = (sunFluxAtBody * body.radius * body.radius) / (4.0 * Math.Pow(body.radius + altitude, 2.0));
			double emissiveFlux = sphericFluxAtAltitude * (1.0 - body.albedo);

			// if we are inside the atmosphere, scale down both fluxes by the atmosphere absorbtion at the current altitude
			// rationale : the atmosphere itself play a role in refracting the solar flux toward space, and the proportion of
			// the emissive flux released by the atmosphere itself is really only valid when you're in space. The real effects
			// are quite complex, this is a first approximation.
			if (body.hasAtmosphere && altitude < body.atmosphereDepth)
			{
				double atmoFactor = Sim.AtmosphereFactor(body, altitude);
				albedoFlux *= atmoFactor;
				emissiveFlux *= atmoFactor;
			}

			starFlux.bodiesAlbedoFlux += albedoFlux;
			starFlux.bodiesEmissiveFlux += emissiveFlux;
		}


		private void AnalyzeBodiesCoreFluxes()
		{
			bodiesCoreFlux = 0.0;
			if (MainBody.isSun)
				return;

			if (mainBodyIsVisible)
				bodiesCoreFlux += BodyCoreFlux(MainBody);

			// if main body is a moon, also consider core flux from the planet
			if (mainBodyIsMoon && mainPlanetIsVisible)
				bodiesCoreFlux += BodyCoreFlux(mainPlanet);
		}

		/// <summary>
		/// Some bodies emit an internal thermal flux due to various tidal, geothermal or accretional phenomenons
		/// This is given by CelestialBody.coreTemperatureOffset
		/// From that value we derive a sea level thermal flux in W/m² using the blackbody equation
		/// We assume that the atmosphere has no effect on that value.
		/// </summary>
		/// <returns>Flux in W/m2 at vessel altitude</returns>
		private double BodyCoreFlux(SimBody body)
		{
			if (body.coreThermalFlux == 0.0)
				return 0.0;

			// We use the same formula as GetBodiesIndirectSunFluxes for altitude scaling, but this time the 
			// coreThermalFlux is scaled with the body surface area : coreFluxAtSurface * 4 * π * r²
			// Resulting in the simplified formula :
			// (coreFluxAtSurface * r²) / (r + a)²
			return (body.coreThermalFlux * Math.Pow(body.radius, 2.0)) / Math.Pow(body.radius + altitude, 2.0);
		}
	}
}
