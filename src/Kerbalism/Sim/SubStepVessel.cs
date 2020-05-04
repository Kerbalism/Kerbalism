using KERBALISM.Collections;
using System;
using System.Collections.Generic;

namespace KERBALISM
{
	public class SubStepVessel : SimVessel
	{
		public bool needCatchup;
		public SubStepOrbit orbit;
		public Deque<SimStep> stepsData = new Deque<SimStep>();

		public override SimBody[] Bodies => SubStepSim.Bodies;

		public SubStepVessel(Vessel stockVessel)
		{
			stepsData = new Deque<SimStep>(SubStepSim.subStepsToCompute);

			if (stockVessel.orbit != null)
				orbit = new SubStepOrbit(stockVessel.orbit);
		}

		public override Vector3d GetPosition(SimStep step)
		{
			if (landed)
				return mainBody.GetSurfacePosition(vesselLatitude, vesselLongitude, step.Altitude, step.UT);
			else
				return orbit.GetSafeTruePosition(step.UT);
		}

		public void Synchronize(VesselData vd, int stepsToConsume)
		{
			Vessel vessel = vd.Vessel;

			if (orbit == null && vessel.orbit != null)
				orbit = new SubStepOrbit(vessel.orbit);
			else
				orbit.Update(vessel.orbit);

			for (int i = 0; i < stepsToConsume; i++)
			{
				if (stepsData.Count == 0)
				{
					Lib.LogDebug($"Simulation thread can't catch up : {i}/{stepsToConsume} steps consumed !", Lib.LogLevel.Warning);
					break;
				}

				vd.subSteps.Add(stepsData.RemoveFromFront());
			}

			if (soiHasChanged && stepsData.Count > 1)
			{
				// always recompute all steps for vessels that just changed SOI
				Lib.LogDebug($"{vd.VesselName} SOI has changed, clearing substeps");
				stepsData.Clear();
				SubStepSim.vesselsInNeedOfCatchup.Enqueue(this);
			}
			else if (vessel.loaded && stepsToConsume == 0)
			{
				// on loaded vessels, the orbit can change at any time unless we are timewarping
				// if we aren't timewarping (no steps consumed), the worker thread will be mostly idle
				// so take advantage of that and always recompute all steps between every FixedUpdate
				stepsData.Clear();
				SubStepSim.vesselsInNeedOfCatchup.Enqueue(this);
			}
		}

		public void ComputeNextStep()
		{
			SimStep step = SimStep.GetFromWorkerPool();
			step.Init(this);
			step.Evaluate();

			stepsData.AddToBack(step);
		}

		public bool TryComputeMissingSteps()
		{
			int stepCount = stepsData.Count;
			int maxSteps = stepCount + 25;

			while (stepCount < SubStepSim.stepCount)
			{
				double stepUT = SubStepSim.steps[stepCount].ut;
				SimStep step = SimStep.GetFromWorkerPool();
				step.Init(this, stepUT);
				step.Evaluate();
				stepsData.AddToBack(step);
				stepCount++;

				if (stepCount > maxSteps)
					return false;
			}

			return true;
		}
	}
}
