using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KERBALISM.VesselLogic
{
	public class VesselRadiation
	{
		Queue<PartRadiationData.RaycastTask> raycastTasks = new Queue<PartRadiationData.RaycastTask>();

		int partToUpdate;

		public void FixedUpdate(PartDataCollectionBase parts, bool loaded, double elapsedSec)
		{
			for (int i = 0; i < parts.Count; i++)
			{
				PartRadiationData radiationData = parts[i].radiationData;
				radiationData.AddElapsedTime(elapsedSec);

				if (i == partToUpdate)
				{
					radiationData.Update();

					if (loaded && radiationData.IsReceiver)
					{
						radiationData.EnqueueRaycastTasks(raycastTasks);
					}
				}
			}

			partToUpdate = (partToUpdate + 1) % parts.Count;

			if (raycastTasks.Count > 0)
			{
				PartRadiationData.RaycastTask task = raycastTasks.Dequeue();
				task.Raycast(raycastTasks.Count > 0 ? raycastTasks.Peek() : null);
			}
		}

	}
}
