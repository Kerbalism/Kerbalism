using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM
{
	public class SubStepOrbit : Orbit
	{
		private int lastCheckedOrbitHash;
		private int currentOrbitHash;
		public SubStepBody safeReferenceBody;

		public SubStepOrbit(Orbit stockOrbit)
		{
			safeReferenceBody = SubStepSim.Bodies[stockOrbit.referenceBody.flightGlobalsIndex];
			Update(stockOrbit);
			lastCheckedOrbitHash = currentOrbitHash;
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

			currentOrbitHash = GetOrbitHash();

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

		public bool OrbitHasChanged()
		{
			if (lastCheckedOrbitHash != currentOrbitHash)
			{
				lastCheckedOrbitHash = currentOrbitHash;
				return true;
			}
			return false;
		}

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

		private int GetOrbitHash() // TODO : this still doesn't work very well...
		{
			unchecked
			{
				int hash = 17;
				hash *= 31 + (int)(argumentOfPeriapsis * 0.1); // this is quite unstable
				hash *= 31 + (int)(LAN * 1.0); // a bit less
				hash *= 31 + (int)(inclination * 100000.0);
				hash *= 31 + (int)(eccentricity * 100000.0); 
				return hash;
			}
		}
	}
}
