using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace KERBALISM
{
	/// <summary>
	/// Handler for the vessel resources simulator.
	/// Allow access to the resource information (IResource) for all resources on the vessel
	/// and also stores of all recorded recipes (ResourceRecipe)
	/// </summary>
	public sealed class VesselResHandler
	{

		public VesselResource ElectricCharge { get; private set; }

		private class VesselResourceInfo
		{
			public IResource resource;
			public List<ResourceWrapper> partResources;

			public VesselResourceInfo(IResource resource, List<ResourceWrapper> partResources)
			{
				this.resource = resource;
				this.partResources = partResources;
			}
		}

		private static List<string> allResourceNames;
		private static List<string> AllResourceNames
		{
			get
			{
				if (allResourceNames == null)
				{
					allResourceNames = new List<string>();
					foreach (PartResourceDefinition resDefinition in PartResourceLibrary.Instance.resourceDefinitions)
						if (!allResourceNames.Contains(resDefinition.name))
							allResourceNames.Add(resDefinition.name);
				}
				return allResourceNames;
			}
		}

		private Dictionary<string, VesselResourceInfo> resources = new Dictionary<string, VesselResourceInfo>(32);
		private List<Recipe> recipes = new List<Recipe>(4);

		public VesselResHandler(ShipConstruct editorVessel)
		{
			foreach (string resourceName in AllResourceNames)
				resources.Add(resourceName, new VesselResourceInfo(new VesselResource(resourceName), new List<ResourceWrapper>()));

			SyncShipConstructPartResources(editorVessel);

			foreach (VesselResourceInfo vesselResourceInfo in resources.Values)
				((VesselResource)vesselResourceInfo.resource).InitAmounts(vesselResourceInfo.partResources);

			ElectricCharge = (VesselResource)resources["ElectricCharge"].resource;
		}

		public VesselResHandler(Vessel v)
		{
			foreach (string resourceName in AllResourceNames)
				resources.Add(resourceName, new VesselResourceInfo(new VesselResource(resourceName), new List<ResourceWrapper>()));

			SyncVesselPartResources(v);

			foreach (VesselResourceInfo vesselResourceInfo in resources.Values)
				((VesselResource)vesselResourceInfo.resource).InitAmounts(vesselResourceInfo.partResources);

			ElectricCharge = (VesselResource)resources["ElectricCharge"].resource;
		}

		public VesselResHandler(ProtoVessel pv)
		{
			foreach (string resourceName in AllResourceNames)
				resources.Add(resourceName, new VesselResourceInfo(new VesselResource(resourceName), new List<ResourceWrapper>()));

			SyncProtoVesselPartResources(pv);

			foreach (VesselResourceInfo vesselResourceInfo in resources.Values)
				((VesselResource)vesselResourceInfo.resource).InitAmounts(vesselResourceInfo.partResources);

			ElectricCharge = (VesselResource)resources["ElectricCharge"].resource;
		}

		/// <summary>return the VesselResource object for this resource or create it if it doesn't exists</summary>
		public IResource GetResource(string resourceName)
		{
			// try to get existing entry if any
			VesselResourceInfo resInfo;
			if (resources.TryGetValue(resourceName, out resInfo))
				return resInfo.resource;

			resInfo = new VesselResourceInfo(new VirtualResource(resourceName), null);

			// remember new entry
			resources.Add(resourceName, resInfo);

			// return new entry
			return resInfo.resource;
		}

		/// <summary>
		/// Get all virtual resources that exist on the vessel. Quite slow, don't use this in update/fixedupdate.
		/// Note that it isn't garanteed that these resources are still present/used on the vessel.
		/// </summary>
		public List<VirtualResource> GetVirtualResources()
		{
			List<VirtualResource> virtualResources = new List<VirtualResource>();
			foreach (IResource res in resources.Values)
				if (res is VirtualResource) virtualResources.Add((VirtualResource)res);

			return virtualResources;
		}

		/// <summary>
		/// Main vessel resource simulation update method.
		/// Execute all recipes to get final deferred amounts, then for each resource apply deferred requests, 
		/// synchronize the new amount in all parts and update VesselResource information properties (rates, brokers...)
		/// </summary>
		public void VesselResourceUpdate(Vessel v, double elapsed_s)
		{
			// execute all recorded recipes
			Recipe.ExecuteRecipes(this, recipes, v);

			// forget the recipes
			recipes.Clear();

			Dictionary<string, VesselResourceInfo>.ValueCollection resourcesCollection = resources.Values;

			foreach (VesselResourceInfo resourceInfo in resourcesCollection)
				resourceInfo.partResources.Clear();

			SyncVesselPartResources(v);

			// apply all deferred requests and synchronize to vessel
			foreach (VesselResourceInfo resourceInfo in resourcesCollection)
			{
				// Note : there is some resetting logic (deferred & brokers reset) in ExecuteAndSyncToParts that need to be applied, even if there are no parts.
				if (resourceInfo.resource.ExecuteAndSyncToParts(elapsed_s, resourceInfo.partResources) && v.loaded)
					CoherencyWarning(v, resourceInfo.resource.Name);
			}
		}

		public void ShipConstructResourceUpdate(ShipConstruct ship, double elapsed_s)
		{
			// execute all recorded recipes
			Recipe.ExecuteRecipes(this, recipes);

			// forget the recipes
			recipes.Clear();

			Dictionary<string, VesselResourceInfo>.ValueCollection resourcesCollection = resources.Values;

			foreach (VesselResourceInfo resourceInfo in resourcesCollection)
				resourceInfo.partResources.Clear();

			SyncShipConstructPartResources(ship);

			// apply all deferred requests and synchronize to vessel
			foreach (VesselResourceInfo resourceInfo in resourcesCollection)
			{
				//if (resourceInfo.partResources.Count == 0)
				//	continue;

				resourceInfo.resource.ExecuteAndSyncToParts(elapsed_s, resourceInfo.partResources);
			}
		}

		/// <summary> record deferred production of a resource (shortcut) </summary>
		/// <param name="broker">short ui-friendly name for the producer</param>
		public void Produce(string resource_name, double quantity, ResourceBroker broker)
		{
			GetResource(resource_name).Produce(quantity, broker);
		}

		/// <summary> record deferred consumption of a resource (shortcut) </summary>
		/// <param name="broker">short ui-friendly name for the consumer</param>
		public void Consume(string resource_name, double quantity, ResourceBroker broker)
		{
			GetResource(resource_name).Consume(quantity, broker);
		}

		/// <summary> record deferred execution of a recipe (shortcut) </summary>
		public void AddRecipe(Recipe recipe)
		{
			recipes.Add(recipe);
		}

		private void CoherencyWarning(Vessel v, string resourceName)
		{
			Message.Post
			(
				Severity.warning,
				Lib.BuildString
				(
				!v.isActiveVessel ? Lib.BuildString("On <b>", v.vesselName, "</b>\na ") : "A ",
				"producer of <b>", resourceName, "</b> has\n",
				"incoherent behavior at high warp speed.\n",
				"<i>Unload the vessel before warping</i>"
				)
			);
			Lib.StopWarp(5);
		}

		// Note : One of the reason this is done here and not inside each IResource is because
		// PartResourceList (type of Part.Resources) is a dictionary under the hood. This cause
		// each loop operation over it to be quite slow and memory intensive, so we avoid repeating
		// that Part > PartResource loop for every resource by doing it once from the vessel level object.
		private void SyncVesselPartResources(Vessel v)
		{
			if (v.loaded)
			{
				foreach (Part p in v.Parts)
				{
					foreach (PartResource r in p.Resources)
					{
						resources[r.resourceName].partResources.Add(new PartResourceWrapper(r));
#if DEBUG_RESOURCES
						// Force view all resource in Debug Mode
						r.isVisible = true;
#endif
					}
				}
			}
			else
			{
				SyncProtoVesselPartResources(v.protoVessel);
			}
		}

		private void SyncProtoVesselPartResources(ProtoVessel pv)
		{
			foreach (ProtoPartSnapshot p in pv.protoPartSnapshots)
			{
				foreach (ProtoPartResourceSnapshot r in p.resources)
				{
					resources[r.resourceName].partResources.Add(new ProtoPartResourceWrapper(r));
				}
			}
		}

		private void SyncShipConstructPartResources(ShipConstruct ship)
		{
			foreach (Part p in ship.Parts)
			{
				foreach (PartResource r in p.Resources)
				{
					resources[r.resourceName].partResources.Add(new PartResourceWrapper(r));
#if DEBUG_RESOURCES
					// Force view all resource in Debug Mode
					r.isVisible = true;
#endif
				}
			}
		}
	}
}
