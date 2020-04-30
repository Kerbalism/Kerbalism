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
		private static List<StepGlobalData> stepPool = new List<StepGlobalData>(200);
		private static Queue<int> freeSteps = new Queue<int>(200);

		public static StepGlobalData GetFromPool()
		{
			if (freeSteps.TryDequeue(out int index))
			{
				return stepPool[index];
			}
			else
			{
				StepGlobalData newStep = new StepGlobalData(stepPool.Count);
				stepPool.Add(newStep);
				return newStep;
			}
		}

		private readonly int stepPoolIndex;

		public double ut;
		public double inverseRotAngle; // Planetarium.InverseRotAngle
		public Planetarium.CelestialFrame zup; // Planetarium.Zup;

		private StepGlobalData(int stepPoolIndex)
		{
			this.stepPoolIndex = stepPoolIndex;
		}

		public void ReleaseToPool()
		{
			freeSteps.Enqueue(stepPoolIndex);
		}
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

		private static readonly object workerLock = new object();

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
			SimProfiler.lastFuTicks = fuWatch.ElapsedTicks;
			fuWatch.Restart();

			if (SimProfiler.lastFuTicks > SimProfiler.maxFuTicks)
				SimProfiler.maxFuTicks = SimProfiler.lastFuTicks;
			if (SimProfiler.lastFuTicks < SimProfiler.minFuTicks)
				SimProfiler.minFuTicks = SimProfiler.lastFuTicks;

			Profiler.BeginSample("Kerbalism.SubStepSim.Update");

			object __lockObj = workerLock;
			bool __lockWasTaken = false;
			try
			{
				System.Threading.Monitor.TryEnter(__lockObj, 1, ref __lockWasTaken);
				if (__lockWasTaken)
				{
					ThreadSafeUpdate();
				}
				else
				{
					// maxUT is read from the worker thread in only one place, between step evaluation
					// so i guess (hope) it's ok to increment it even if we don't have a lock for it
					// Incrementing it will allow the worker thread to go forward using the old parameters,
					// instead of having to compute twice the normal amount of steps during the next FU.
					maxUT += maxSubsteps * interval;
					Lib.LogDebug("Could not acquire lock !", Lib.LogLevel.Warning);
				}
				
			}
			finally
			{
				if (__lockWasTaken) System.Threading.Monitor.Exit(__lockObj);
			}

			Profiler.EndSample();

		}

		private static void ThreadSafeUpdate()
		{
			SimProfiler.lastWorkerTicks = currentWorkerTicks;
			currentWorkerTicks = 0;

			if (SimProfiler.lastWorkerTicks > SimProfiler.maxWorkerTicks)
				SimProfiler.maxWorkerTicks = SimProfiler.lastWorkerTicks;
			if (SimProfiler.lastWorkerTicks < SimProfiler.minWorkerTicks)
				SimProfiler.minWorkerTicks = SimProfiler.lastWorkerTicks;

			currentUT = Planetarium.GetUniversalTime();

			maxUT = currentUT + (maxSubsteps * interval);

			int stepsToConsume = 0;
			// remove old steps
			while (steps.Count > 0 && steps.Peek().ut < currentUT)
			{
				stepsToConsume++;
				steps.Dequeue().ReleaseToPool();
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


		static ProfilerMarker prfSubStep;
		static ProfilerMarker prfSubStepCatchup;
		static ProfilerMarker prfNewStepGlobalData;
		static ProfilerMarker prfSubStepBodyCompute;
		static ProfilerMarker prfSubStepVesselCompute;
		public static ProfilerMarker prfSubStepVesselInstantiate;
		public static ProfilerMarker prfSubStepVesselEvaluate;
		public static ProfilerMarker prfSubStepVesselGetVesselPos;
		public static ProfilerMarker prfSubStepVesselGetBodiesPos;
		static Stopwatch fuWatch = new Stopwatch();
		static Stopwatch workerWatch = new Stopwatch();

		static long currentWorkerTicks;



		public static void ComputeLoop()
		{

			prfSubStep = new ProfilerMarker("Kerbalism.SubStep");
			prfSubStepCatchup = new ProfilerMarker("Kerbalism.SubStepCatchup");
			prfNewStepGlobalData = new ProfilerMarker("Kerbalism.NewStepGlobalData");
			prfSubStepBodyCompute = new ProfilerMarker("Kerbalism.SubStepBodyCompute");
			prfSubStepVesselCompute = new ProfilerMarker("Kerbalism.SubStepVesselCompute");
			prfSubStepVesselInstantiate = new ProfilerMarker("Kerbalism.SubStepVesselInstantiate");
			prfSubStepVesselEvaluate = new ProfilerMarker("Kerbalism.SubStepVesselEvaluate");
			prfSubStepVesselGetVesselPos = new ProfilerMarker("Kerbalism.SubStepVesselGetVesselPos");
			prfSubStepVesselGetBodiesPos = new ProfilerMarker("Kerbalism.SubStepVesselGetBodiesPos");

			while (true)
			{
				if (!workerIsAlive)
					worker.Abort();


				object __lockObj = workerLock;
				bool __lockWasTaken = false;
				try
				{
					System.Threading.Monitor.Enter(__lockObj, ref __lockWasTaken);
					DoWorkerStep();
				}
				finally
				{
					if (__lockWasTaken) System.Threading.Monitor.Exit(__lockObj);
				}

				Thread.Sleep(0);
			}
		}

		private static void DoWorkerStep()
		{
			if (lastStepUT < maxUT)
			{
				prfSubStep.Begin();
				workerWatch.Restart();
				ComputeNextStep();
				workerWatch.Stop();
				currentWorkerTicks += workerWatch.ElapsedTicks;
				prfSubStep.End();
			}

			while (vesselsInNeedOfCatchup.Count > 0)
			{
				prfSubStepCatchup.Begin();
				vesselsInNeedOfCatchup.Dequeue().Catchup();
				prfSubStepCatchup.End();
			}
		}

		public static void ComputeNextStep()
		{
			stepCount++;
			lastStepUT = currentUT + (stepCount * interval);

			prfNewStepGlobalData.Begin();
			lastStep = StepGlobalData.GetFromPool();
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
