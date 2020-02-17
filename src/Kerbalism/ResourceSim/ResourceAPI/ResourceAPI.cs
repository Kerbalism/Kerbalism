using System;
using System.Collections.Generic;

namespace KERBALISM
{
	public static class ResourceAPI
	{
		/// <summary>
		/// Call ResourceUpdate on all part modules that have that method
		/// </summary>
		public static void ResourceUpdate(Vessel v, VesselData vd, VesselResHandler vesselResHandler, double elapsed_s)
		{
			// only do this for loaded vessels. unloaded vessels will be handled in Background.cs
			if (!v.loaded) return;

			if (vd.resourceUpdateDelegates == null)
			{
				vd.resourceUpdateDelegates = new List<ResourceUpdateDelegate>();
				foreach (var part in v.parts)
				{
					foreach (var module in part.Modules)
					{
						if (!module.isEnabled) continue;
						var resourceUpdateDelegate = ResourceUpdateDelegate.Instance(module);
						if (resourceUpdateDelegate != null) vd.resourceUpdateDelegates.Add(resourceUpdateDelegate);
					}
				}
			}

			if (vd.resourceUpdateDelegates.Count == 0) return;

			List<KeyValuePair<string, double>> resourceChangeRequests = new List<KeyValuePair<string, double>>();

			foreach (var resourceUpdateDelegate in vd.resourceUpdateDelegates)
			{
				resourceChangeRequests.Clear();
				string title = resourceUpdateDelegate.invoke(vesselResHandler.APIResources, resourceChangeRequests);
				ResourceBroker broker = ResourceBroker.GetOrCreate(title);
				foreach (var rc in resourceChangeRequests)
				{
					if (rc.Value > 0) vesselResHandler.Produce(rc.Key, rc.Value * elapsed_s, broker);
					else if (rc.Value < 0) vesselResHandler.Consume(rc.Key, -rc.Value * elapsed_s, broker);
				}
			}
		}

		public static void BackgroundUpdate(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m,
	Part part_prefab, PartModule module_prefab, VesselResHandler vesselResHandler, List<KeyValuePair<string, double>> resourceChangeRequests, double elapsed_s)
		{
			resourceChangeRequests.Clear();

			try
			{
				string title = BackgroundDelegate.Instance(module_prefab).invoke(v, p, m, module_prefab, part_prefab, vesselResHandler.APIResources, resourceChangeRequests, elapsed_s);

				foreach (var cr in resourceChangeRequests)
				{
					if (cr.Value > 0) vesselResHandler.Produce(cr.Key, cr.Value * elapsed_s, ResourceBroker.GetOrCreate(title));
					else if (cr.Value < 0) vesselResHandler.Consume(cr.Key, -cr.Value * elapsed_s, ResourceBroker.GetOrCreate(title));
				}
			}
			catch (Exception ex)
			{
				Lib.Log("BackgroundUpdate in PartModule " + module_prefab.moduleName + " excepted: " + ex.Message + "\n" + ex.ToString());
			}
		}


	}
}
