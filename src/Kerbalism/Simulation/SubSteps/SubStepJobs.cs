using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace KERBALISM
{
	internal struct SubStepTimeNative
	{
		public double UT;
		public double Duration;
	}

	internal struct SubStepOrbitNative
	{
		public byte IsValid;
		public int ParentBody;
		public double Eccentricity;
		public double MeanMotion;
		public double Epoch;
		public double ObTAtEpoch;
		public double Period;
		public double SemiLatusRectum;
		public double3 BasisX;
		public double3 BasisY;

		public double3 PositionAtUT(double ut)
		{
			if (IsValid == 0)
				return new double3(double.NaN);

			double obt = ObTAtEpoch + ut - Epoch;
			if (Eccentricity < 1.0 && Period > 0.0 && !double.IsInfinity(Period))
			{
				double halfPeriod = Period * 0.5;
				obt -= math.floor((obt + halfPeriod) / Period) * Period;
			}

			double meanAnomaly = obt * MeanMotion;
			double eccentricAnomaly = SolveEccentricAnomaly(meanAnomaly, Eccentricity);
			if (!IsFinite(eccentricAnomaly))
				return new double3(double.NaN);
			double trueAnomaly = TrueAnomaly(eccentricAnomaly, Eccentricity);
			math.sincos(trueAnomaly, out double sin, out double cos);
			double denominator = 1.0 + Eccentricity * cos;
			if (math.abs(denominator) < 1e-14)
				return new double3(double.NaN);
			return (SemiLatusRectum / denominator) * (BasisX * cos + BasisY * sin);
		}

		private static double SolveEccentricAnomaly(double meanAnomaly, double eccentricity)
		{
			if (!IsFinite(meanAnomaly) || !IsFinite(eccentricity) || eccentricity < 0.0
				|| math.abs(eccentricity - 1.0) < 1e-8)
				return double.NaN;

			if (eccentricity < 1.0)
			{
				// Kepler's equation is monotonic over [-pi, pi]. Keep Newton
				// bracketed so highly eccentric, near-periapsis cases can't diverge.
				double estimate = eccentricity < 0.8 ? meanAnomaly : math.sign(meanAnomaly) * math.PI;
				double lower = -math.PI;
				double upper = math.PI;
				for (int i = 0; i < 48; i++)
				{
					math.sincos(estimate, out double sin, out double cos);
					double residual = estimate - eccentricity * sin - meanAnomaly;
					if (math.abs(residual) < 1e-13)
						return estimate;
					if (residual > 0.0)
						upper = estimate;
					else
						lower = estimate;

					double derivative = 1.0 - eccentricity * cos;
					double candidate = estimate - residual / derivative;
					estimate = candidate > lower && candidate < upper && IsFinite(candidate)
						? candidate
						: (lower + upper) * 0.5;
				}
				return double.NaN;
			}

			// Solve the positive half of the odd hyperbolic Kepler equation.
			// Near e = 1, an unconstrained Newton step can overshoot by orders
			// of magnitude; retain a bracket just as for elliptical orbits.
			double magnitude = math.abs(meanAnomaly);
			double hyperbolic = Asinh(magnitude / eccentricity);
			double lowerHyperbolic = 0.0;
			double upperHyperbolic = math.max(1.0, hyperbolic);
			while (eccentricity * math.sinh(upperHyperbolic) - upperHyperbolic < magnitude)
				upperHyperbolic *= 2.0;

			for (int i = 0; i < 48; i++)
			{
				double sinh = math.sinh(hyperbolic);
				double cosh = math.cosh(hyperbolic);
				double residual = eccentricity * sinh - hyperbolic - magnitude;
				double derivative = eccentricity * cosh - 1.0;
				double delta = residual / derivative;
				if (math.abs(delta) < 1e-12)
					return meanAnomaly < 0.0 ? -hyperbolic : hyperbolic;

				if (residual > 0.0)
					upperHyperbolic = hyperbolic;
				else
					lowerHyperbolic = hyperbolic;
				double candidate = hyperbolic - delta;
				hyperbolic = candidate > lowerHyperbolic && candidate < upperHyperbolic && IsFinite(candidate)
					? candidate
					: (lowerHyperbolic + upperHyperbolic) * 0.5;
			}
			return double.NaN;
		}

		private static double Asinh(double value)
		{
			double absolute = math.abs(value);
			double result = math.log(absolute + math.sqrt(absolute * absolute + 1.0));
			return value < 0.0 ? -result : result;
		}

		private static bool IsFinite(double value)
		{
			return !double.IsNaN(value) && !double.IsInfinity(value);
		}

		private static double TrueAnomaly(double eccentricAnomaly, double eccentricity)
		{
			if (eccentricity < 1.0)
			{
				double half = eccentricAnomaly * 0.5;
				math.sincos(half, out double sin, out double cos);
				return 2.0 * math.atan2(math.sqrt(1.0 + eccentricity) * sin, math.sqrt(1.0 - eccentricity) * cos);
			}

			double hyperbolicHalf = eccentricAnomaly * 0.5;
			return 2.0 * math.atan2(
				math.sqrt(eccentricity + 1.0) * math.sinh(hyperbolicHalf),
				math.sqrt(eccentricity - 1.0) * math.cosh(hyperbolicHalf));
		}
	}

	internal struct SubStepBodyNative
	{
		public int FlightGlobalsIndex;
		public int ParentBody;
		public SubStepOrbitNative Orbit;
		public double Radius;
		public double AtmosphereDepth;
		public double AngularVelocity;
		public double ReferenceUT;
		public double3 RootPosition;
		public double3 North;
		public double3 PrimeMeridian;
		public double3 East90;
	}

	internal struct SubStepVesselNative
	{
		public int MainBody;
		/// <summary>0 invalid, 1 orbit, 2 fixed body surface position.</summary>
		public byte Trajectory;
		public SubStepOrbitNative Orbit;
		public double Latitude;
		public double Longitude;
		public double Altitude;
	}

	internal struct SubStepVesselGeometryNative
	{
		public double3 Position;
		public double Latitude;
		public double Longitude;
		public double Altitude;
	}

	internal struct SubStepStarNative
	{
		public int BodyIndex;
		/// <summary>Luminosity divided by 4π, so flux is FluxScale / distance².</summary>
		public double FluxScale;
	}

	internal struct SubStepSunNative
	{
		public double3 Direction;
		public double CenterDistance;
		public double RawFlux;
		public byte Visible;
	}

	internal static class SubStepJobMath
	{
		public static double3 Rotate(double3 vector, double3 axis, double angle)
		{
			math.sincos(angle, out double sin, out double cos);
			return vector * cos + math.cross(axis, vector) * sin + axis * math.dot(axis, vector) * (1.0 - cos);
		}

		public static void BodyBasisAtUT(in SubStepBodyNative body, double ut, out double3 north, out double3 prime, out double3 east)
		{
			north = body.North;
			double angle = -(ut - body.ReferenceUT) * body.AngularVelocity;
			prime = Rotate(body.PrimeMeridian, north, angle);
			east = Rotate(body.East90, north, angle);
		}
	}

	[BurstCompile]
	internal struct SubStepBodyPositionJob : IJobParallelFor
	{
		[ReadOnly] public NativeArray<SubStepTimeNative> Times;
		[ReadOnly] public NativeArray<SubStepBodyNative> Bodies;
		public int BodyCount;
		// Each Execute owns one complete time slice. It reads parent entries
		// written earlier in that same slice, so the default per-index safety
		// restriction is intentionally disabled.
		[NativeDisableParallelForRestriction] public NativeArray<double3> Positions;

		public void Execute(int timeIndex)
		{
			double ut = Times[timeIndex].UT;
			int bodyCount = BodyCount;
			int offset = timeIndex * bodyCount;
			for (int bodyIndex = 0; bodyIndex < bodyCount; bodyIndex++)
			{
				SubStepBodyNative body = Bodies[bodyIndex];
				Positions[offset + bodyIndex] = body.ParentBody < 0
					? body.RootPosition
					: Positions[offset + body.ParentBody] + body.Orbit.PositionAtUT(ut);
			}
		}
	}

	[BurstCompile]
	internal struct SubStepVesselPositionJob : IJobParallelFor
	{
		[ReadOnly] public NativeArray<SubStepTimeNative> Times;
		[ReadOnly] public NativeArray<SubStepBodyNative> Bodies;
		[ReadOnly] public NativeArray<SubStepVesselNative> Vessels;
		[ReadOnly] public NativeArray<double3> BodyPositions;
		public int BodyCount;
		public int VesselCount;
		[WriteOnly] public NativeArray<SubStepVesselGeometryNative> Geometry;

		public void Execute(int index)
		{
			int vesselCount = VesselCount;
			int timeIndex = index / vesselCount;
			int vesselIndex = index - timeIndex * vesselCount;
			SubStepVesselNative vessel = Vessels[vesselIndex];
			SubStepBodyNative body = Bodies[vessel.MainBody];
			double ut = Times[timeIndex].UT;
			double3 bodyPosition = BodyPositions[timeIndex * BodyCount + vessel.MainBody];

			double3 relative;
			double latitude;
			double longitude;
			double altitude;

			SubStepJobMath.BodyBasisAtUT(body, ut, out double3 north, out double3 prime, out double3 east);
			if (vessel.Trajectory == 2)
			{
				double latRad = math.radians(vessel.Latitude);
				double lonRad = math.radians(vessel.Longitude);
				math.sincos(latRad, out double sinLat, out double cosLat);
				math.sincos(lonRad, out double sinLon, out double cosLon);
				relative = (prime * (cosLat * cosLon) + east * (cosLat * sinLon) + north * sinLat)
					* (body.Radius + vessel.Altitude);
				latitude = vessel.Latitude;
				longitude = vessel.Longitude;
				altitude = vessel.Altitude;
			}
			else
			{
				relative = vessel.Orbit.PositionAtUT(ut);
				double radius = math.length(relative);
				double3 normal = radius > 0.0 ? relative / radius : double3.zero;
				latitude = math.degrees(math.asin(math.clamp(math.dot(normal, north), -1.0, 1.0)));
				longitude = math.degrees(math.atan2(math.dot(normal, east), math.dot(normal, prime)));
				altitude = radius - body.Radius;
			}

			Geometry[index] = new SubStepVesselGeometryNative
			{
				Position = bodyPosition + relative,
				Latitude = latitude,
				Longitude = longitude,
				Altitude = altitude
			};
		}
	}

	[BurstCompile]
	internal struct SubStepSolarVisibilityJob : IJobParallelFor
	{
		[ReadOnly] public NativeArray<SubStepBodyNative> Bodies;
		[ReadOnly] public NativeArray<SubStepVesselNative> Vessels;
		[ReadOnly] public NativeArray<double3> BodyPositions;
		[ReadOnly] public NativeArray<SubStepVesselGeometryNative> VesselGeometry;
		[ReadOnly] public NativeArray<SubStepStarNative> Stars;
		[ReadOnly] public int VesselCount;
		public int BodyCount;
		public int SunCount;
		[WriteOnly] public NativeArray<SubStepSunNative> Results;

		public void Execute(int index)
		{
			int sunCount = SunCount;
			int sampleVesselIndex = index / sunCount;
			int sunIndex = index - sampleVesselIndex * sunCount;
			int timeIndex = sampleVesselIndex / VesselCount;
			int vesselIndex = sampleVesselIndex - timeIndex * VesselCount;
			int bodyOffset = timeIndex * BodyCount;
			SubStepStarNative star = Stars[sunIndex];
			int sunBody = star.BodyIndex;
			SubStepVesselNative vessel = Vessels[vesselIndex];

			double3 vesselPosition = VesselGeometry[sampleVesselIndex].Position;
			double3 toSun = BodyPositions[bodyOffset + sunBody] - vesselPosition;
			double centerDistance = math.length(toSun);
			double3 direction = centerDistance > 0.0 ? toSun / centerDistance : double3.zero;
			double rawFlux = centerDistance > 0.0
				? star.FluxScale / (centerDistance * centerDistance)
				: 0.0;
			double rayLength = math.max(0.0, centerDistance - Bodies[sunBody].Radius);
			bool visible = centerDistance > 0.0;

			for (int bodyIndex = 0; visible && bodyIndex < BodyCount; bodyIndex++)
			{
				if (bodyIndex == sunBody || Bodies[bodyIndex].Radius <= 0.0)
					continue;

				double3 toBody = BodyPositions[bodyOffset + bodyIndex] - vesselPosition;
				if (vessel.Trajectory == 2 && bodyIndex == vessel.MainBody && Bodies[bodyIndex].Radius < 100000.0)
				{
					double toBodyLength = math.length(toBody);
					double horizonDot = toBodyLength > 0.0 ? math.dot(toBody / toBodyLength, direction) : 0.0;
					// Match the established small-body landed tolerance: beyond
					// roughly half a degree onto the night side is definitely dark;
					// otherwise ignore the numerically fragile main-body sphere test.
					if (horizonDot > 0.01)
						visible = false;
					continue;
				}

				double projection = math.dot(toBody, direction);
				if (projection <= 0.0 || projection >= rayLength)
					continue;

				double3 closest = direction * projection - toBody;
				double radius = Bodies[bodyIndex].Radius;
				if (math.lengthsq(closest) < radius * radius)
					visible = false;
			}

			Results[index] = new SubStepSunNative
			{
				Direction = direction,
				CenterDistance = centerDistance,
				RawFlux = rawFlux,
				Visible = visible ? (byte)1 : (byte)0
			};
		}
	}
}
