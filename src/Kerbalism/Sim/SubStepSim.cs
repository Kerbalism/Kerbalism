using KERBALISM.Collections;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace KERBALISM
{
	public class StepGlobalData
	{
		public double ut;
		public double inverseRotAngle; // Planetarium.InverseRotAngle
		public Planetarium.CelestialFrame zup; // Planetarium.Zup;
	}

	public static class SubStepSim
	{
		private static Planetarium.CelestialFrame currentZup;
		private static double currentInverseRotAngle;

		public static Thread worker;
		public static bool workerIsAlive;
		public static double maxUT;
		public static double currentUT;
		public static int bodyCount;
		public static int stepCount;
		public static double lastStepUT;
		public static double interval = 60.0;
		public static Dictionary<Guid, SubStepVessel> vessels = new Dictionary<Guid, SubStepVessel>();
		public static SubStepBody[] Bodies { get; private set; }
		public static IndexedQueue<StepGlobalData> steps = new IndexedQueue<StepGlobalData>();
		public static StepGlobalData lastStep;
		public static int maxSubsteps;

		public static Queue<SubStepVessel> vesselsInNeedOfCatchup = new Queue<SubStepVessel>();

		public static readonly object workerLock = new object();

		public static void Init()
		{
			bodyCount = FlightGlobals.Bodies.Count;
			Bodies = new SubStepBody[bodyCount];

			for (int i = 0; i < bodyCount; i++)
			{
				SubStepBody safeBody = new SubStepBody(FlightGlobals.Bodies[i]);
				Bodies[i] = safeBody;
			}

			maxSubsteps = (int)(TimeWarp.fetch.warpRates.Last() * 0.02 * 2.0 / interval);
		}

		public static void OnFixedUpdate()
		{
			if (Lib.IsGameRunning)
			{
				UpdateParameters();
				if (!workerIsAlive)
				{
					if (worker != null)
						worker.Abort();

					Lib.LogDebug($"Starting worker thread");
					workerIsAlive = true;
					worker = new Thread(new ThreadStart(ComputeLoop));
					worker.Start();
				}
			}
			else
			{
				workerIsAlive = false;
			}
		}


		public static void UpdateParameters()
		{
			Profiler.BeginSample("Kerbalism.SubStepSim.UpdateParameters");
			lock (workerLock)
			{
				currentUT = Planetarium.GetUniversalTime();

				maxUT = currentUT + (maxSubsteps * interval);

				int stepsToConsume = 0;
				// remove old steps
				while (steps.Count > 0 && steps.Peek().ut < currentUT)
				{
					stepsToConsume++;
					steps.Dequeue();
				}

				stepCount = steps.Count;

				// copy things from Planetarium
				currentZup = Planetarium.Zup;
				currentInverseRotAngle = Planetarium.InverseRotAngle;

				// update bodies and their orbit
				foreach (SubStepBody body in Bodies)
				{
					body.Update();
				}

				// update vessels and their orbit
				foreach (VesselData vd in DB.VesselDatas)
				{
					if (!vessels.TryGetValue(vd.VesselId, out SubStepVessel vessel))
					{
						if (vd.IsSimulated)
						{
							vessel = new SubStepVessel(vd.Vessel);
							vessels.Add(vd.VesselId, vessel);
							vesselsInNeedOfCatchup.Enqueue(vessel);
						}
						else
						{
							continue;
						}
					}
					else
					{
						if (!vd.IsSimulated)
						{
							vessels.Remove(vd.VesselId);
							continue;
						}
					}

					vessel.UpdateCurrent(vd);
					vessel.Update(vd, stepsToConsume);
				}
			}

			Profiler.EndSample();
		}


		static ProfilerMarker prfSubStep;
		static ProfilerMarker prfNewStepGlobalData;
		static ProfilerMarker prfSubStepBodyCompute;
		static ProfilerMarker prfSubStepVesselCompute;
		public static ProfilerMarker prfSubStepVesselInstantiate;
		public static ProfilerMarker prfSubStepVesselEvaluate;

		public static void ComputeLoop()
		{

			prfSubStep = new ProfilerMarker("Kerbalism.SubStep");
			prfNewStepGlobalData = new ProfilerMarker("Kerbalism.NewStepGlobalData");
			prfSubStepBodyCompute = new ProfilerMarker("Kerbalism.SubStepBodyCompute");
			prfSubStepVesselCompute = new ProfilerMarker("Kerbalism.SubStepVesselCompute");
			prfSubStepVesselInstantiate = new ProfilerMarker("Kerbalism.SubStepVesselInstantiate");
			prfSubStepVesselEvaluate = new ProfilerMarker("Kerbalism.SubStepVesselEvaluate");

			while (true)
			{
				if (!workerIsAlive)
					worker.Abort();

				bool isFree = true;
				lock (workerLock)
				{
					
					if (lastStepUT < maxUT)
					{
						prfSubStep.Begin();
						ComputeNextStep();
						prfSubStep.End();
						isFree = false;
					}

					while (vesselsInNeedOfCatchup.Count > 0)
					{
						prfSubStep.Begin();
						vesselsInNeedOfCatchup.Dequeue().Catchup();
						prfSubStep.End();
						isFree = false;
					}
				}

				if (isFree)
				{
					Thread.Sleep(0);
				}
			}
		}

		public static void ComputeNextStep()
		{
			stepCount++;
			lastStepUT = currentUT + (stepCount * interval);

			prfNewStepGlobalData.Begin();
			lastStep = new StepGlobalData();
			lastStep.ut = lastStepUT;
			lastStep.inverseRotAngle = currentInverseRotAngle;
			lastStep.zup = currentZup;
			steps.Enqueue(lastStep);
			prfNewStepGlobalData.End();

			foreach (SubStepBody body in Bodies)
			{
				prfSubStepBodyCompute.Begin();
				body.ComputeNextStep();
				prfSubStepBodyCompute.End();
			}

			foreach (SubStepVessel vessel in vessels.Values)
			{
				prfSubStepVesselCompute.Begin();
				vessel.ComputeNextStep();
				prfSubStepVesselCompute.End();
			}
		}
	}
}
