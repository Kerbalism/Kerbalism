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
		public static IndexedQueue<SubStepGlobalData> steps = new IndexedQueue<SubStepGlobalData>();
		public static SubStepGlobalData lastStep;
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

			maxSubsteps = (int)(TimeWarp.fetch.warpRates.Last() * 0.02 * 1.5 / interval);
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

			object __lockObj = workerLock;
			bool __lockWasTaken = false;
			try
			{
				System.Threading.Monitor.TryEnter(__lockObj, 1, ref __lockWasTaken);
				if (__lockWasTaken)
				{
					ThreadSafeSynchronize();
				}
				else
				{
					// maxUT is read from the worker thread in only one place, between step evaluation
					// so i guess (hope) it's ok to increment it even if we don't have a lock for it
					// Incrementing it will allow the worker thread to go forward using the old parameters,
					// instead of having to compute twice the normal amount of steps during the next FU.
					maxUT = Planetarium.GetUniversalTime() + (maxSubsteps * interval);
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

			maxUT = currentUT + (maxSubsteps * interval);

			int stepsToConsume = 0;
			// remove old steps
			while (steps.Count > 0 && steps.Peek().ut < currentUT)
			{
				stepsToConsume++;
				steps.Dequeue().ReleaseToPool();
			}

			stepCount = steps.Count;

			MiniProfiler.workerTimeUsed = stepsToConsume * interval;

			if (stepCount == 0)
			{
				MiniProfiler.workerTimeMissed = (Math.Floor(TimeWarp.fixedDeltaTime / interval) - stepsToConsume) * interval;
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



		public static void WorkerLoop()
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

			if (vesselsInNeedOfCatchup.Count > 0)
			{
				prfSubStepCatchup.Begin();
				workerWatch.Restart();

				if (vesselsInNeedOfCatchup.Peek().TryComputeMissingSteps())
					vesselsInNeedOfCatchup.Dequeue();

				workerWatch.Stop();
				currentWorkerTicks += workerWatch.ElapsedTicks;
				prfSubStepCatchup.End();
			}
		}

		public static void ComputeNextStep()
		{
			stepCount++;
			lastStepUT = currentUT + (stepCount * interval);

			prfNewStepGlobalData.Begin();
			lastStep = SubStepGlobalData.GetFromPool();
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
