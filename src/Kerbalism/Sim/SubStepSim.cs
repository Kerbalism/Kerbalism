using KERBALISM.Collections;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Unity.Profiling;
using UnityEngine.Profiling;

namespace KERBALISM
{
	public static class SubStepSim
	{
		private static int maxWarpRateIndex;
		private static float lastMaxWarprate;

		// The interval is the in-game time between each substep evaluation.
		// It determine how many substeps will be required at a given timewarp rate.
		// Lower values give a more precise simulation (more sampling points)
		// Higher values will reduce the amount of substeps consumed per FixedUpdate, preventing the simulation
		// from falling behind when there is a large number of vessels or if higher than stock timewarp rates are used.

		private static Planetarium.CelestialFrame currentZup;
		private static double currentInverseRotAngle;

		public static Thread worker;
		public static bool workerIsAlive;
		public static double maxUT;
		public static double currentUT;
		public static int bodyCount;
		public static int stepCount;
		public static double lastStepUT;

		public static Dictionary<Guid, SubStepVessel> vessels = new Dictionary<Guid, SubStepVessel>();
		public static SubStepBody[] Bodies { get; private set; }
		public static IndexedQueue<SubStepGlobalData> steps = new IndexedQueue<SubStepGlobalData>();
		public static SubStepGlobalData lastStep;

		public static double subStepInterval;
		public static int subStepsAtMaxWarp;
		public static int subStepsToCompute;

		public static Queue<SubStepVessel> vesselsInNeedOfCatchup = new Queue<SubStepVessel>();

		private static readonly object workerLock = new object();

		public static void Init()
		{
			maxWarpRateIndex = TimeWarp.fetch.warpRates.Length - 1;

			subStepInterval = minInterval;
			UpdateMaxSubSteps();

			bodyCount = FlightGlobals.Bodies.Count;
			Bodies = new SubStepBody[bodyCount];

			for (int i = 0; i < bodyCount; i++)
			{
				SubStepBody safeBody = new SubStepBody(FlightGlobals.Bodies[i]);
				Bodies[i] = safeBody;
			}
		}

		public static void Load(ConfigNode node)
		{
			if (Lib.IsGameRunning)
			{
				subStepInterval = Lib.ConfigValue(node, nameof(subStepInterval), minInterval);
				UpdateMaxSubSteps();
			}
		}

		public static void Save(ConfigNode node)
		{
			node.AddValue(nameof(subStepInterval), subStepInterval);
		}

		private const int maxSubSteps = 100; // max amount of substeps, no matter the interval and max timewarp rate
		private const int subStepsMargin = 25; // substep "buffer"
		private const double minInterval = 30.0; // in seconds
		private const double maxInterval = 120.0; // in seconds
		private const double intervalChange = 15.0; // in seconds

		private const int fuLagCheckFrequency = 50 ; // 1 second
		private const int maxLaggingFu = 2;
		private const int minNonLaggingFu = 20;
		private const int lagChecksForDecision = 5;
		private const int lagChecksReset = 25;


		private static int fuCounter;
		private static int laggingFuCount;
		private static int nonLaggingFuCount;

		private static int lagChecksCount;
		private static int lagCheckResultsCount;
		private static int laggingLagChecks;
		private static int nonLaggingLagChecks;

		private static void WorkerLoadCheck()
		{
			if (lastMaxWarprate != TimeWarp.fetch.warpRates[maxWarpRateIndex])
			{
				UpdateMaxSubSteps();
				return;
			}

			fuCounter++;

			if (fuCounter < fuLagCheckFrequency)
				return;

			fuCounter = 0;
			lagChecksCount++;

			if (laggingFuCount > maxLaggingFu)
			{
				laggingLagChecks++;
				lagCheckResultsCount++;
			}
			else if (laggingFuCount == 0 && nonLaggingFuCount > minNonLaggingFu)
			{
				nonLaggingLagChecks++;
				lagCheckResultsCount++;
			}

			laggingFuCount = 0;
			nonLaggingFuCount = 0;

			if (lagChecksCount > lagChecksReset)
			{
				lagChecksCount = 0;
				lagCheckResultsCount = 0;
				nonLaggingLagChecks = 0;
				laggingLagChecks = 0;
				return;
			}

			if (lagCheckResultsCount > lagChecksForDecision)
			{
				if (nonLaggingLagChecks == 0 && laggingLagChecks == lagCheckResultsCount && subStepInterval < maxInterval)
				{
					subStepInterval = Math.Min(subStepInterval + intervalChange, maxInterval);
					UpdateMaxSubSteps();
				}
				else if (laggingLagChecks == 0 && nonLaggingLagChecks == lagCheckResultsCount && subStepInterval > minInterval)
				{
					subStepInterval = Math.Max(subStepInterval - intervalChange, minInterval);
					UpdateMaxSubSteps();
				}
			}
		}

		private static void UpdateMaxSubSteps()
		{
			lastMaxWarprate = TimeWarp.fetch.warpRates[maxWarpRateIndex];
			subStepsAtMaxWarp = (int)(lastMaxWarprate * 0.02 / subStepInterval);
			if (subStepsAtMaxWarp > maxSubSteps)
				subStepsAtMaxWarp = maxSubSteps;

			subStepsToCompute = subStepsAtMaxWarp + subStepsMargin;

			fuCounter = 0;
			laggingFuCount = 0;
			nonLaggingFuCount = 0;

			lagChecksCount = 0;
			lagCheckResultsCount = 0;
			laggingLagChecks = 0;
			nonLaggingLagChecks = 0;
		}

		public static void OnFixedUpdate()
		{
			if (Lib.IsGameRunning)
			{
				Synchronize();
				if (!workerIsAlive)
				{
					if (worker != null)
						worker.Abort();

					Lib.LogDebug($"Starting worker thread");
					workerIsAlive = true;
					worker = new Thread(new ThreadStart(WorkerLoop));
					worker.Start();
				}
			}
			else
			{
				workerIsAlive = false;
			}
		}


		public static void Synchronize()
		{
			MiniProfiler.lastFuTicks = fuWatch.ElapsedTicks;
			fuWatch.Restart();

			Profiler.BeginSample("Kerbalism.SubStepSim.Update");

			subStepsToCompute = (int)(TimeWarp.fetch.warpRates[7] * 0.02 * 1.5 / subStepInterval);

			object __lockObj = workerLock;
			bool __lockWasTaken = false;
			try
			{
				System.Threading.Monitor.TryEnter(__lockObj, 1, ref __lockWasTaken);
				if (__lockWasTaken)
				{
					ThreadSafeSynchronize();
					WorkerLoadCheck();
				}
				else
				{
					// maxUT is read from the worker thread in only one place, between step evaluation
					// so i guess (hope) it's ok to increment it even if we don't have a lock for it
					// Incrementing it will allow the worker thread to go forward using the old parameters,
					// instead of having to compute twice the normal amount of steps during the next FU.
					maxUT = Planetarium.GetUniversalTime() + (subStepsToCompute * subStepInterval);
					Lib.LogDebug("Could not acquire lock !", Lib.LogLevel.Warning);
				}
				
			}
			finally
			{
				if (__lockWasTaken) System.Threading.Monitor.Exit(__lockObj);
			}

			Profiler.EndSample();

		}

		private static void ThreadSafeSynchronize()
		{
			MiniProfiler.lastWorkerTicks = currentWorkerTicks;
			currentWorkerTicks = 0;

			currentUT = Planetarium.GetUniversalTime();

			maxUT = currentUT + (subStepsToCompute * subStepInterval);

			int stepsToConsume = 0;
			// remove old steps
			while (steps.Count > 0 && steps.Peek().ut < currentUT)
			{
				stepsToConsume++;
				steps.Dequeue().ReleaseToPool();
			}

			stepCount = steps.Count;

			MiniProfiler.workerTimeUsed = stepsToConsume * subStepInterval;

			if (stepCount == 0)
			{
				MiniProfiler.workerTimeMissed = (Math.Floor(TimeWarp.fixedDeltaTime / subStepInterval) - stepsToConsume) * subStepInterval;
				laggingFuCount++;
			}
			else if (stepsToConsume >= subStepsAtMaxWarp)
			{
				MiniProfiler.workerTimeMissed = 0.0;
				nonLaggingFuCount++;
			}
			else
			{
				MiniProfiler.workerTimeMissed = 0.0;
			}
			
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

				vessel.UpdatePosition(vd);
				vessel.Synchronize(vd, stepsToConsume);
			}
		}

		static Stopwatch fuWatch = new Stopwatch();
		static Stopwatch workerWatch = new Stopwatch();

		static long currentWorkerTicks;

		public static void WorkerLoop()
		{
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

				// Let the CPU breathe a bit. I'm not sure this is strictly needed, but it seems that not
				// doing it can cause other threads to hang. In particular, KSP + a running unity profiler will 
				// hang if this isn't called. It doesn't seem to affect the performance of the worker thread.
				// From the Thread.Sleep() docs :
				// If the value of the millisecondsTimeout argument is zero, the thread relinquishes
				// the remainder of its time slice to any thread of equal priority that is ready to run.
				// If there are no other threads of equal priority that are ready to run, execution of
				// the current thread is not suspended.
				Thread.Sleep(0);
			}
		}

		private static void DoWorkerStep()
		{
			if (lastStepUT < maxUT)
			{
				workerWatch.Restart();
				ComputeNextStep();
				workerWatch.Stop();
				currentWorkerTicks += workerWatch.ElapsedTicks;
			}

			if (vesselsInNeedOfCatchup.Count > 0)
			{
				workerWatch.Restart();

				if (vesselsInNeedOfCatchup.Peek().TryComputeMissingSteps())
					vesselsInNeedOfCatchup.Dequeue();

				workerWatch.Stop();
				currentWorkerTicks += workerWatch.ElapsedTicks;
			}
		}

		public static void ComputeNextStep()
		{
			stepCount++;
			lastStepUT = currentUT + (stepCount * subStepInterval);

			lastStep = SubStepGlobalData.GetFromPool();
			lastStep.ut = lastStepUT;
			lastStep.inverseRotAngle = currentInverseRotAngle;
			lastStep.zup = currentZup;
			steps.Enqueue(lastStep);

			foreach (SubStepBody body in Bodies)
			{
				body.ComputeNextStep();
			}

			foreach (SubStepVessel vessel in vessels.Values)
			{
				vessel.ComputeNextStep();
			}
		}
	}
}
