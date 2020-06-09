using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	public class SubStepOrbit : Orbit
	{
		public SubStepBody safeReferenceBody;

		public SubStepOrbit(Orbit stockOrbit)
		{
			safeReferenceBody = SubStepSim.Bodies[stockOrbit.referenceBody.flightGlobalsIndex];
			Update(stockOrbit);
		}

		public void Update(Orbit stockOrbit)
		{
			// Orbit ctor
			inclination = stockOrbit.inclination;
			eccentricity = stockOrbit.eccentricity;
			semiMajorAxis = stockOrbit.semiMajorAxis;
			LAN = stockOrbit.LAN;
			argumentOfPeriapsis = stockOrbit.argumentOfPeriapsis;
			meanAnomalyAtEpoch = stockOrbit.meanAnomalyAtEpoch;

			epoch = stockOrbit.epoch;
			referenceBody = stockOrbit.referenceBody;
			safeReferenceBody = SubStepSim.Bodies[stockOrbit.referenceBody.flightGlobalsIndex];

			// Orbit.Init()
			OrbitFrame = stockOrbit.OrbitFrame;
			an = stockOrbit.an;
			eccVec = stockOrbit.eccVec;
			h = stockOrbit.h;
			meanMotion = stockOrbit.meanMotion;
			meanAnomaly = stockOrbit.meanAnomaly;
			ObT = stockOrbit.ObT;
			ObTAtEpoch = stockOrbit.ObTAtEpoch;
			period = stockOrbit.period;
			orbitPercent = stockOrbit.orbitPercent;
		}

		public double SafeSemiLatusRectum => h.sqrMagnitude / safeReferenceBody.gravParameter;

		public Vector3d GetSafeTruePosition(double ut = -1.0)
		{
			if (ut < 0.0)
			{
				Vector3d rel = GetSafeRelativePositionAtUT(SubStepSim.lastStep.ut);
				return SubStepSim.lastStep.zup.WorldToLocal(rel).xzy + safeReferenceBody.GetPosition();
			}
			else
			{
				Vector3d rel = GetSafeRelativePositionAtUT(ut);
				return SubStepSim.lastStep.zup.WorldToLocal(rel).xzy + safeReferenceBody.GetPosition(ut);
			}
		}

		private Vector3d GetSafeRelativePositionAtUT(double ut)
		{
			// Orbit.getRelativePositionAtUT()
			double obtAtUT = getObtAtUT(ut); // Orbit.getObtAtUT() is safe

			// Orbit.getRelativePositionAtT()
			double m = obtAtUT * meanMotion;
			double e = solveEccentricAnomaly(m, eccentricity); // Orbit.solveEccentricAnomaly() is safe
			double tA = GetTrueAnomaly(e); // Orbit.GetTrueAnomaly() is safe

			// Orbit.getRelativePositionFromTrueAnomaly()
			double costA = Math.Cos(tA);
			return SafeSemiLatusRectum / (1.0 + eccentricity * costA) * (OrbitFrame.X * costA + OrbitFrame.Y * Math.Sin(tA));
		}
	}
}
