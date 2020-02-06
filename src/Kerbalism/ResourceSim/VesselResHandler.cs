using System;
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
		private Dictionary<string, IResource> resources = new Dictionary<string, IResource>(32);
		private List<Recipe> recipes = new List<Recipe>(4);

		/// <summary>return the VesselResource object for this resource or create it if it doesn't exists</summary>
		public IResource GetResource(Vessel v, string resourceName)
		{
			// try to get existing entry if any
			IResource res;
			if (resources.TryGetValue(resourceName, out res)) return res;

			// create new entry, "real" resource if it exists in the game, virtual resource otherwise.
			if (PartResourceLibrary.Instance.resourceDefinitions.Contains(resourceName))
				res = new VesselResource(v, resourceName);
			else
				res = new VirtualResource(resourceName);

			// remember new entry
			resources.Add(resourceName, res);

			// return new entry
			return res;
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
		public void Sync(Vessel v, VesselData vd, double elapsed_s)
		{
			// execute all recorded recipes
			Recipe.ExecuteRecipes(v, this, recipes);

			// forget the recipes
			recipes.Clear();

			// apply all deferred requests and synchronize to vessel
			foreach (IResource vr in resources.Values) vr.Sync(v, vd, elapsed_s);
		}

		/// <summary> record deferred production of a resource (shortcut) </summary>
		/// <param name="broker">short ui-friendly name for the producer</param>
		public void Produce(Vessel v, string resource_name, double quantity, ResourceBroker broker)
		{
			GetResource(v, resource_name).Produce(quantity, broker);
		}

		/// <summary> record deferred consumption of a resource (shortcut) </summary>
		/// <param name="broker">short ui-friendly name for the consumer</param>
		public void Consume(Vessel v, string resource_name, double quantity, ResourceBroker broker)
		{
			GetResource(v, resource_name).Consume(quantity, broker);
		}

		/// <summary> record deferred execution of a recipe (shortcut) </summary>
		public void AddRecipe(Recipe recipe)
		{
			recipes.Add(recipe);
		}
	}
}
