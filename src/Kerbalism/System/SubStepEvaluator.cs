using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KERBALISM
{
	public static class SubStepEvaluator
	{
		public static int bodyCount;
		public static double lastUT;
		public static double interval = 60.0;
		public static ConcurrentDictionary<Guid, ThreadSafeVessel> vessels;
		public static Queue<SubStep> substeps;
		public static List<ThreadSafeBody> bodies;
		public static Planetarium.CelestialFrame zup;

		public static void Update()
		{
			// lock
			// copy zup from Planetarium.Zup
			zup = Planetarium.Zup;
			// update bodies and their orbit
			foreach (ThreadSafeBody body in bodies)
				body.Update();

			// update vessels and their orbit
			foreach (VesselData vd in DB.VesselDatas)
			{
				if (!vessels.TryGetValue(vd.VesselId, out ThreadSafeVessel vessel))
				{
					vessel = new ThreadSafeVessel(vd.Vessel);
					vessels.TryAdd(vd.VesselId, vessel);
				}

				if (!vd.IsSimulated)
					continue;

				if (vessels.ContainsKey(vd.VesselId))
				{
					if (!vd.IsSimulated)
						continue;




				}

				if (!vd.IsSimulated && vessels.ContainsKey(vd.VesselId))
					vessels.TryRemove(vd.VesselId, out ThreadSafeVessel vessel);




			}
			// unlock
		}
	}

	public class ThreadSafeVessel
	{
		int id;
		bool landed;
		ThreadSafeOrbit orbit;
		ThreadSafeBody mainBody;

		public ThreadSafeVessel(Vessel vessel)
		{
			id = vessel.id.GetHashCode();
			landed = vessel.LandedOrSplashed;
			orbit = new ThreadSafeOrbit(vessel.orbit);
			mainBody = SubStepEvaluator.bodies[vessel.mainBody.flightGlobalsIndex];
		}
	}

	public class ThreadSafeOrbit : Orbit
	{
		public ThreadSafeBody safeReferenceBody;

		public ThreadSafeOrbit(Orbit stockOrbit)
		{
			safeReferenceBody = SubStepEvaluator.bodies[stockOrbit.referenceBody.flightGlobalsIndex];
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

			// Orbit.Init()
			OrbitFrame = stockOrbit.OrbitFrame; // Planetarium.CelestialFrame is a struct
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

		public Vector3d GetSafePositionAtUT(double UT)
		{
			return GetSafePositionAtT(getObtAtUT(UT)); // Orbit.getObtAtUT() is safe
		}

		public Vector3d GetSafePositionAtT(double T)
		{
			return safeReferenceBody.position + GetSafeRelativePositionAtT(T).xzy;
		}

		public Vector3d GetSafeRelativePositionAtT(double T)
		{
			double m = T * meanMotion;
			double e = solveEccentricAnomaly(m, eccentricity); // Orbit.solveEccentricAnomaly() is safe
			double tA = GetTrueAnomaly(e); // Orbit.GetTrueAnomaly() is safe
			return GetSafeRelativePositionFromTrueAnomaly(tA);
		}

		public Vector3d GetSafeRelativePositionFromTrueAnomaly(double tA)
		{
			double num = Math.Cos(tA);
			double d = Math.Sin(tA);
			Vector3d r = semiLatusRectum / (1.0 + eccentricity * num) * (OrbitFrame.X * num + OrbitFrame.Y * d);
			return SubStepEvaluator.zup.WorldToLocal(r);
		}
	}

	public class ThreadSafeBody
	{
		public int flightGlobalsIndex;
		public Vector3d position;
		public ThreadSafeOrbit orbit;

		public void Update()
		{
			position = FlightGlobals.Bodies[flightGlobalsIndex].position;
			orbit.Update(FlightGlobals.Bodies[flightGlobalsIndex].orbit);
		}
	}

	public class SubStep
	{
		public double ut;
		public Dictionary<int, SubStepVesselData> vessels = new Dictionary<int, SubStepVesselData>();

		public SubStep(double ut)
		{
			Vector3d[] bodyPositions = new Vector3d[SubStepEvaluator.bodyCount];

			foreach (ThreadSafeVessel vessel in SubStepEvaluator.vessels.Values)
			{
				SubStepVesselData vesselData = new SubStepVesselData();
				vessels.Add(vessel.id, vesselData);

			}
		}
	}

	public class SubStepVesselData
	{
		public double rad;
		public double sunFlux;
		public double totalFlux;
	}
}
