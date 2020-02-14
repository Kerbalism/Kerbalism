using System;
using System.Collections.Generic;

namespace KERBALISM
{
	public static class ResourceAPI
	{
		public static List<IResource> GetAllResources(Vessel v, VesselResHandler vesselResHandler)
		{
			List<string> knownResources = new List<string>();
			List<IResource> result = new List<IResource>();

			if (v.loaded)
			{
				foreach (Part p in v.Parts)
				{
					foreach (PartResource r in p.Resources)
					{
						if (knownResources.Contains(r.resourceName)) continue;
						knownResources.Add(r.resourceName);
						result.Add(vesselResHandler.GetResource(r.resourceName));
					}
				}
			}
			else
			{
				foreach (ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
				{
					foreach (ProtoPartResourceSnapshot r in p.resources)
					{
						if (knownResources.Contains(r.resourceName)) continue;
						knownResources.Add(r.resourceName);
						result.Add(vesselResHandler.GetResource(r.resourceName));
					}
				}
			}

			return result;
		}

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

			List<IResource> allResources = GetAllResources(v, vesselResHandler); // there might be some performance to be gained by caching the list of all resource

			Dictionary<string, double> availableResources = new Dictionary<string, double>();
			foreach (IResource res in allResources)
				availableResources[res.Name] = res.Amount;
			List<KeyValuePair<string, double>> resourceChangeRequests = new List<KeyValuePair<string, double>>();

			foreach (var resourceUpdateDelegate in vd.resourceUpdateDelegates)
			{
				resourceChangeRequests.Clear();
				string title = resourceUpdateDelegate.invoke(availableResources, resourceChangeRequests);
				ResourceBroker broker = ResourceBroker.GetOrCreate(title);
				foreach (var rc in resourceChangeRequests)
				{
					if (rc.Value > 0) vesselResHandler.Produce(rc.Key, rc.Value * elapsed_s, broker);
					else if (rc.Value < 0) vesselResHandler.Consume(rc.Key, -rc.Value * elapsed_s, broker);
				}
			}
		}

		public static void BackgroundUpdate(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m,
	Part part_prefab, PartModule module_prefab, VesselResHandler vesselResHandler, Dictionary<string, double> availableResources, List<KeyValuePair<string, double>> resourceChangeRequests, double elapsed_s)
		{
			resourceChangeRequests.Clear();

			try
			{
				string title = BackgroundDelegate.Instance(module_prefab).invoke(v, p, m, module_prefab, part_prefab, availableResources, resourceChangeRequests, elapsed_s);

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
