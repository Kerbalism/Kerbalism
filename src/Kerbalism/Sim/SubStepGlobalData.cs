using System.Collections.Generic;

namespace KERBALISM
{
	public class SubStepGlobalData
	{
		private static List<SubStepGlobalData> stepPool = new List<SubStepGlobalData>(200);
		private static Queue<int> freeSteps = new Queue<int>(200);

		public static SubStepGlobalData GetFromPool()
		{
			if (freeSteps.TryDequeue(out int index))
			{
				return stepPool[index];
			}
			else
			{
				SubStepGlobalData newStep = new SubStepGlobalData(stepPool.Count);
				stepPool.Add(newStep);
				return newStep;
			}
		}

		private readonly int stepPoolIndex;

		public double ut;
		public double inverseRotAngle; // Planetarium.InverseRotAngle
		public Planetarium.CelestialFrame zup; // Planetarium.Zup;

		private SubStepGlobalData(int stepPoolIndex)
		{
			this.stepPoolIndex = stepPoolIndex;
		}

		public void ReleaseToPool()
		{
			freeSteps.Enqueue(stepPoolIndex);
		}
	}
}
