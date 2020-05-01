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
	}
}
