using KERBALISM.Collections;
using System;
using System.Collections.Generic;

namespace KERBALISM
{
	public class SubStepVessel : SimVessel
	{
		public bool needCatchup;
		public SubStepOrbit orbit;
		public Deque<Step> stepsData = new Deque<Step>();
		public Step lastStep;

		private bool isNew;

		internal override SimBody[] Bodies => SubStepSim.Bodies;

		public SubStepVessel(Vessel stockVessel)
		{
			isNew = true;
			stepsData = new Deque<Step>(SubStepSim.maxSubsteps);

			if (stockVessel.orbit != null)
				orbit = new SubStepOrbit(stockVessel.orbit);
		}

		internal override Vector3d GetPosition(Step step)
		{
			if (landed)
				return mainBody.GetSurfacePosition(vesselLatitude, vesselLongitude, step.Altitude, step.UT);
			else
				return orbit.GetSafeTruePosition(step.UT);
		}

		public void Update(VesselData vd, int stepsToConsume)
		{
			if (isNew && stepsToConsume == 0)
			{
				if (stepsData.Count > 0)
				{
					Lib.LogDebug($"Forcing evaluation of new vessel {vd.VesselName}");
					stepsToConsume = 1;
					isNew = false;
				}
				else
				{
					Lib.LogDebug($"Can't evaluate new vessel {vd.VesselName} yet, no steps computed !");
				}
			}

			bool orbitHasChanged = false;
			Vessel vessel = vd.Vessel;

			if (orbit == null && vessel.orbit != null)
			{
				orbit = new SubStepOrbit(vessel.orbit);
			}
			else
			{
				orbit.Update(vessel.orbit);
				orbitHasChanged = !landed && orbit.OrbitHasChanged();
			}

			for (int i = 0; i < stepsToConsume; i++)
			{
				if (stepsData.Count == 0)
				{
					Lib.LogDebug($"Simulation thread can't catch up : {i}/{stepsToConsume} steps consumed !", Lib.LogLevel.Warning);
					break;
				}

				vd.subSteps.Add(stepsData.RemoveFromFront());
			}

			lastStep = null;

			if (orbitHasChanged && stepsData.Count > 1)
			{
				Lib.Log("Orbit has changed, clearing substeps");
				stepsData.RemoveRange(1, stepsData.Count - 1);
			}

			if (stepsData.Count < SubStepSim.stepCount - stepsToConsume)
			{
				SubStepSim.vesselsInNeedOfCatchup.Enqueue(this);
			}
		}

		public void ComputeNextStep()
		{
			SubStepSim.prfSubStepVesselInstantiate.Begin();
			Step step = new Step(this);
			SubStepSim.prfSubStepVesselInstantiate.End();

			SubStepSim.prfSubStepVesselEvaluate.Begin();
			step.Evaluate();
			SubStepSim.prfSubStepVesselEvaluate.End();

			stepsData.AddToBack(step);
			lastStep = step;
		}

		public void Catchup()
		{
			int stepCount = stepsData.Count;

			while (stepCount < SubStepSim.stepCount)
			{
				double stepUT = SubStepSim.steps[stepCount].ut;
				Step step = new Step(this, stepUT);
				step.Evaluate();
				stepsData.AddToBack(step);
				lastStep = step;
				stepCount++;
			}

			lastStep = stepsData[stepCount - 1];
		}
	}
}
