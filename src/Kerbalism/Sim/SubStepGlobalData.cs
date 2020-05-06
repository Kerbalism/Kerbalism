using System.Collections.Generic;

namespace KERBALISM
{
	public class SubStepGlobalData
	{
		private static Queue<SubStepGlobalData> globalStepPool = new Queue<SubStepGlobalData>(200);

		public static SubStepGlobalData GetFromPool()
		{
			if (globalStepPool.TryDequeue(out SubStepGlobalData step))
				return step;

			return new SubStepGlobalData();
		}

		public double ut;
		public double inverseRotAngle; // Planetarium.InverseRotAngle
		public Planetarium.CelestialFrame zup; // Planetarium.Zup;

		public void ReleaseToPool()
		{
			globalStepPool.Enqueue(this);
		}
	}
}
