using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KERBALISM
{
	public class SimBody
	{
		// references
		public CelestialBody stockBody;
		protected int flightGlobalsIndex;
		protected int referenceBodyFlightGlobalsIndex;

		// values updated every FixedUpdate
		protected Vector3d currentPosition;
		protected bool currentInverseRotation;

		// stock body base properties
		public double radius;
		public bool hasSolidSurface;
		public bool hasOcean;
		public double density;
		public double surfaceArea;
		public double albedo;

		// stock body atmosphere
		public bool hasAtmosphere;
		public double atmosphereDepth;
		public bool atmosphereUsePressureCurve;
		public bool atmospherePressureCurveIsNormalized;
		public double atmospherePressureSeaLevel;
		public double atmosphereTemperatureLapseRate;
		public double atmosphereTemperatureSeaLevel;
		public double atmosphereGasMassLapseRate;
		public double atmosphereMolarMass;
		public bool atmosphereUseTemperatureCurve;
		public bool atmosphereTemperatureCurveIsNormalized;
		public FloatCurve atmosphereTemperatureCurve;
		public FloatCurve atmospherePressureCurve;

		// custom stats that don't exist on CelestialBody
		public double coreThermalFlux;
		public bool canRotate;
		public bool isSun;
		public double surfaceGravity;
		public double geometricAlbedo;


		public double mainStarsRawIrradiance;
		public double equilibriumTemperature;

		public double atmoSurfaceTemperature;
		public double atmoTempPolarOffset;
		public double atmoTempDayOffset;
		public double atmoTempNightOffset;
		public double atmoGreenhouseTempOffset;
		public double atmoAverageLightTransparency;


		// step cache
		public bool stepCacheIsOccluding;
		public Vector3d stepCacheOcclusionDiff;
		public Vector3d stepCachePosition;

		public SimBody(CelestialBody body)
		{
			stockBody = body;
			flightGlobalsIndex = body.flightGlobalsIndex;
			referenceBodyFlightGlobalsIndex = body.referenceBody.flightGlobalsIndex;

			// core properties
			radius = body.Radius;
			albedo = body.albedo;

			hasSolidSurface = body.hasSolidSurface;
			hasOcean = body.ocean;
			density = body.Density;

			surfaceArea = body.SurfaceArea;

			// atmosphere properties
			hasAtmosphere = body.atmosphere;
			atmosphereDepth = body.atmosphereDepth;
			atmosphereUsePressureCurve = body.atmosphereUsePressureCurve;
			atmospherePressureCurveIsNormalized = body.atmospherePressureCurveIsNormalized;
			atmospherePressureSeaLevel = body.atmospherePressureSeaLevel;
			atmosphereTemperatureLapseRate = body.atmosphereTemperatureLapseRate;
			atmosphereTemperatureSeaLevel = body.atmosphereTemperatureSeaLevel;
			atmosphereGasMassLapseRate = body.atmosphereGasMassLapseRate;
			atmosphereMolarMass = body.atmosphereMolarMass;
			atmosphereUseTemperatureCurve = body.atmosphereUseTemperatureCurve;
			atmosphereTemperatureCurveIsNormalized = body.atmosphereTemperatureCurveIsNormalized;

			ConfigNode node;
			node = new ConfigNode();
			body.atmospherePressureCurve.Save(node);
			atmospherePressureCurve = new FloatCurve();
			atmospherePressureCurve.Load(node);

			node = new ConfigNode();
			body.atmosphereTemperatureCurve.Save(node);
			atmosphereTemperatureCurve = new FloatCurve();
			atmosphereTemperatureCurve.Load(node);

			// thermal properties :
			canRotate = body.rotates
				&& body.rotationPeriod != 0.0
				&& (!body.tidallyLocked || (body.orbit != null && body.orbit.period != 0.0));

			// thermal flux in W/m2 intrinsincly emitted by this body at surface level
			coreThermalFlux = Sim.BlackBodyFlux(body.coreTemperatureOffset);

			surfaceGravity = body.gravParameter / (body.Radius * body.Radius);
		}

		public virtual void Init()
		{
			mainStarsRawIrradiance = GetIrradianceFromMainStars(true, true);
			equilibriumTemperature = Math.Pow(mainStarsRawIrradiance * (1.0 - albedo) / (4.0 * PhysicsGlobals.StefanBoltzmanConstant), 0.25);
			geometricAlbedo = albedo * GeometricAlbedoFactor(1.0);

			if (hasAtmosphere)
			{
				atmoSurfaceTemperature = GetTemperature(0.0);
				CelestialBody parentStar = Sim.GetParentStar(stockBody);
				Vector3d bodyToStar = (parentStar.position - stockBody.position).normalized;

				atmoTempPolarOffset = GetAtmoSurfaceTemperature(parentStar, stockBody.position + (Vector3d.up * radius)) - atmoSurfaceTemperature;
				atmoTempDayOffset = GetAtmoSurfaceTemperature(parentStar, stockBody.position + (bodyToStar * radius)) - atmoSurfaceTemperature;
				atmoTempNightOffset = GetAtmoSurfaceTemperature(parentStar, stockBody.position + (bodyToStar * radius * -1.0)) - atmoSurfaceTemperature;

				double averageTempOffset = (((atmoTempDayOffset + atmoTempNightOffset) * 0.5) + atmoTempPolarOffset) * 0.5;
				atmoGreenhouseTempOffset = atmoSurfaceTemperature + averageTempOffset - equilibriumTemperature;
				atmoAverageLightTransparency = AverageLightTransparencyFactor(0.0);
			}
		}

		public virtual SimBody ReferenceBody => Sim.Bodies[referenceBodyFlightGlobalsIndex];

		public virtual void Update()
		{
			currentPosition = stockBody.position;
			currentInverseRotation = stockBody.inverseRotation;
		}

		public virtual Vector3d GetPosition(double ut = -1.0)
		{
			return currentPosition;
		}

		public virtual Vector3d GetSurfacePosition(double lat, double lon, double alt, double ut = -1.0)
		{
			return stockBody.GetWorldSurfacePosition(lat, lon, alt);
		}

		// stock method
		public double GetPressure(double altitude)
		{
			if (hasAtmosphere && altitude < atmosphereDepth)
			{
				if (atmosphereUsePressureCurve)
				{
					if (atmospherePressureCurveIsNormalized)
					{
						return Mathf.Lerp(0f, (float)atmospherePressureSeaLevel, atmospherePressureCurve.Evaluate((float)(altitude / atmosphereDepth)));
					}
					return atmospherePressureCurve.Evaluate((float)altitude);
				}
				return atmospherePressureSeaLevel * Math.Pow(1.0 - atmosphereTemperatureLapseRate * altitude / atmosphereTemperatureSeaLevel, atmosphereGasMassLapseRate);
			}
			return 0.0;
		}

		// stock method
		public double GetDensity(double pressure, double temperature)
		{
			if (!(pressure <= 0.0) && temperature > 0.0)
			{
				return pressure * 1000.0 * atmosphereMolarMass / (PhysicsGlobals.IdealGasConstant * temperature);
			}
			return 0.0;
		}

		// stock method
		public double GetTemperature(double altitude)
		{
			if (altitude >= atmosphereDepth)
			{
				return 0.0;
			}
			if (atmosphereUseTemperatureCurve)
			{
				if (atmosphereTemperatureCurveIsNormalized)
				{
					return UtilMath.Lerp(PhysicsGlobals.SpaceTemperature, atmosphereTemperatureSeaLevel, atmosphereTemperatureCurve.Evaluate((float)(altitude / atmosphereDepth)));
				}
				return atmosphereTemperatureCurve.Evaluate((float)altitude);
			}
			return atmosphereTemperatureSeaLevel - atmosphereTemperatureLapseRate * altitude;
		}

		/// <summary>
		/// return proportion of flux not blocked by atmosphere, for a flux going straight up
		/// </summary>
		public double LightTransparencyFactor(double altitude)
		{
			double staticPressure = GetPressure(altitude);

			if (staticPressure > 0.0)
			{
				double density = GetDensity(staticPressure, GetTemperature(altitude));

				// nonrefracting radially symmetrical atmosphere model [Schoenberg 1929]
				double Ra = radius + altitude;
				double Ya = atmosphereDepth - altitude;
				double path = Math.Sqrt(Ra * Ra + 2.0 * Ra * Ya + Ya * Ya) - Ra;
				return Sim.GetSolarPowerFactor(density) * Ya / path;
			}
			return 1.0;
		}

		/// <summary>
		/// return proportion of flux not blocked by atmosphere, for the given positions and flux direction
		/// </summary>
		public double LightTransparencyFactor(Vector3d bodyPosition, Vector3d fluxDir, Vector3d vesselPosition, double altitude)
		{
			// get up vector
			Vector3d up = (vesselPosition - bodyPosition).normalized;

			double staticPressure = GetPressure(altitude);

			if (staticPressure > 0.0)
			{
				double density = GetDensity(staticPressure, GetTemperature(altitude));

				// nonrefracting radially symmetrical atmosphere model [Schoenberg 1929]
				double Ra = radius + altitude;
				double Ya = atmosphereDepth - altitude;
				double q = Ra * Math.Max(0.0, Vector3d.Dot(up, fluxDir));
				double path = Math.Sqrt(q * q + 2.0 * Ra * Ya + Ya * Ya) - q;
				return Sim.GetSolarPowerFactor(density) * Ya / path;
			}
			return 1.0;
		}

		/// <summary>
		/// return proportion of flux not blocked by atmosphere, for the given positions and flux direction
		/// </summary>
		private double AverageLightTransparencyFactor(double altitude)
		{
			double staticPressure = GetPressure(altitude);

			if (staticPressure > 0.0)
			{
				double density = GetDensity(staticPressure, GetTemperature(altitude));

				// nonrefracting radially symmetrical atmosphere model [Schoenberg 1929]
				double Ra = radius + altitude;
				double Ya = atmosphereDepth - altitude;
				double q = Ra * 0.7071;
				double path = Math.Sqrt(q * q + 2.0 * Ra * Ya + Ya * Ya) - q;
				return Sim.GetSolarPowerFactor(density) * Ya / path;
			}
			return 1.0;
		}



		/// <summary> [0;1] geometric albedo </summary>
		/// <param name="sunBodyObserverAngleFactor">
		/// [0;1] factor for the [180;0]° angle between
		/// the star, the body and the observer (1.0 when the angle is 0°)
		/// </param>
		// In addition of the crescent-shaped illuminated portion making the angle/flux relation non-linear,
		// the flux isn't scattered uniformely but tend to be reflected back in the sun direction,
		// especially on non atmospheric bodies, see https://en.wikipedia.org/wiki/Opposition_surge
		// The albedo value when in direct opposition is the geometric albedo, as opposed to the bond albedo
		// which is the global value. We assume that the stock value is a bond albedo :
		// - For atmospheric bodies, we assume that the geometric albedo = 1.15 * bond albedo
		// - For airless bodies, we assume that the geometric albedo = 1.50 * bond albedo
		// We do some square scaling to approximate those effects end to ensure that the total flux
		// stay the same we check that the surface area under the curve stay the same :
		// - [0,1] integral of y = x is 0.5
		// - [0,1] integral of y = (x * 1.113)^1.3 is ~0.5
		// - [0,1] integral of y = (x * 1.225)^2.0 is ~0.5
		public double GeometricAlbedoFactor(double sunBodyObserverAngleFactor)
		{
			if (hasAtmosphere)
				return Math.Pow(sunBodyObserverAngleFactor * 1.113, 1.3);
			else
				return Math.Pow(sunBodyObserverAngleFactor * 1.225, 2.0);
		}

		private double GetAtmoSurfaceTemperature(CelestialBody parentStar, Vector3d worldPos)
		{
			Vector3d starDir = (parentStar.position - worldPos).normalized;
			Vector3d upAxis = FlightGlobals.getUpAxis(stockBody, worldPos);
			double starDot = Vector3d.Dot(starDir, upAxis);
			stockBody.GetAtmoThermalStats(false, parentStar, starDir, starDot, upAxis, 0.0, out double atmosphereTemperatureOffset, out double nope1, out double nope2);
			return stockBody.GetFullTemperature(0.0, atmosphereTemperatureOffset);
		}

		/// <summary> return equilibrium temperature in K for a body </summary>
		/// <param name="irradiance">star irradiance in W/m² </param>
		public double EquilibriumTemperature(double irradiance)
		{
			return Math.Pow(irradiance * (1.0 - albedo) / (4.0 * PhysicsGlobals.StefanBoltzmanConstant), 0.25);
		}

		public double GetIrradianceFromMainStars(bool ignoreOcclusion = false, bool averageDistance = false)
		{
			List<CelestialBody> visibleBodies = null;

			if (!ignoreOcclusion)
				visibleBodies = Sim.GetLargeBodies(stockBody.position);
			
			SimStar parentStar = Sim.GetParentStarData(stockBody);
			Vector3d parentStarDirection = parentStar.body.position - stockBody.position;
			double parentStarDistance = parentStarDirection.magnitude;
			parentStarDirection /= parentStarDistance;

			double irradiance = 0.0;
			foreach (SimStar star in Sim.stars)
			{
				Vector3d starDirection;
				double starDistance;
				if (star != parentStar)
				{
					starDirection = star.body.position - stockBody.position;
					starDistance = starDirection.magnitude;
					starDirection /= starDistance;
					if (Vector3d.Dot(starDirection, parentStarDirection) < 0.75 || Math.Abs(starDistance - parentStarDistance) > 1e+12)
						continue;
				}
				else
				{
					starDirection = parentStarDirection;
					starDistance = parentStarDistance;
				}

				if (!ignoreOcclusion)
				{
					bool isOccluded = false;
					foreach (CelestialBody occludingBody in visibleBodies)
					{
						if (occludingBody == star.body || occludingBody == stockBody)
							continue;

						if (!Sim.RayAvoidBody(stockBody.position, starDirection, starDistance, occludingBody))
						{
							isOccluded = true;
							break;
						}
					}
					if (isOccluded)
						break;
				}

				if (averageDistance)
				{
					CelestialBody planet = Sim.GetParentPlanet(stockBody);
					starDistance = (Sim.Apoapsis(planet) + Sim.Periapsis(planet)) * 0.5;
				}

				irradiance += star.SolarFlux(starDistance);
			}
			return irradiance;
		}

		/// <summary> return equilibrium temperature in K for a body </summary>
		/// <param name="rawIrradiance">star irradiance in W/m² </param>
		public void GetCurrentThermalStats(
			out double equilibriumTemperature,
			out double dayTemperature,
			out double nightTemperature,
			out double polarTemperature,
			out double rawIrradiance,
			out double surfaceIrradiance)
		{
			
			rawIrradiance = GetIrradianceFromMainStars();
			equilibriumTemperature = EquilibriumTemperature(rawIrradiance);
			surfaceIrradiance = rawIrradiance * atmoAverageLightTransparency;

			if (hasAtmosphere)
			{
				dayTemperature = atmoSurfaceTemperature + atmoTempDayOffset;
				nightTemperature = atmoSurfaceTemperature + atmoTempNightOffset;
				polarTemperature = atmoSurfaceTemperature + atmoTempPolarOffset;
			}
			else
			{
				dayTemperature = Sim.BlackBodyTemperature(rawIrradiance + coreThermalFlux + Sim.BackgroundFlux);
				nightTemperature = Sim.BlackBodyTemperature(coreThermalFlux + Sim.BackgroundFlux);
				polarTemperature = (dayTemperature + nightTemperature) * 0.5; // this is wrong but we don't use it anyway
			}
		}
	}
}
