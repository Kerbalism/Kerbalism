using System.Collections.Generic;

namespace KERBALISM
{
	public class ProcessControllerB9ResourceSync : PartModule
	{
		private readonly Dictionary<ProcessController, string> lastResources = new Dictionary<ProcessController, string>();

		public void Start()
		{
			SyncControllers();
		}

		public void Update()
		{
			if (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight)
				SyncControllers();
		}

		private void SyncControllers()
		{
			foreach (ProcessController controller in part.FindModulesImplementing<ProcessController>())
			{
				if (!IsSwitchableProcess(controller.resource))
					continue;

				string lastResource;
				if (!lastResources.TryGetValue(controller, out lastResource))
				{
					lastResources[controller] = controller.resource;
					continue;
				}

				if (lastResource == controller.resource)
					continue;

				if (IsSwitchableProcess(lastResource))
					Lib.RemoveResource(part, lastResource);

				controller.Configure(true, 1);
				controller.SetRunning(controller.IsRunning());
				lastResources[controller] = controller.resource;
			}
		}

		private static bool IsSwitchableProcess(string resource)
		{
			return !string.IsNullOrEmpty(resource)
				&& (resource.StartsWith("_MAEC") || resource.StartsWith("_Convector") || resource.StartsWith("_STH1TEC"));
		}
	}
}
