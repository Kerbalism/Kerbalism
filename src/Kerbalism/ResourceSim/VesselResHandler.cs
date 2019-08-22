using System;
using System.Collections.Generic;

namespace KERBALISM
{
	/// <summary>
	/// Handler for the vessel resources simulator.
	/// Allow access to the resource information (VesselResource) for all resources on the vessel
	/// and also stores of all recorded recipes (ResourceRecipe)
	/// </summary>
	public sealed class VesselResHandler
	{
		private Dictionary<string, VesselResource> resources = new Dictionary<string, VesselResource>(32);
		private List<ResourceRecipe> recipes = new List<ResourceRecipe>(4);

		/// <summary>return the VesselResource object for this resource or create it if it doesn't exists</summary>
		public VesselResource GetResource(Vessel v, string resource_name)
		{
			// try to get existing entry if any
			VesselResource res;
			if (resources.TryGetValue(resource_name, out res)) return res;

			// create new entry
			res = new VesselResource(v, resource_name);

			// remember new entry
			resources.Add(resource_name, res);

			// return new entry
			return res;
		}

		/// <summary>
		/// Main vessel resource simulation update method.
		/// Execute all recipes to get final deferred amounts, then for each resource apply deferred requests, 
		/// synchronize the new amount in all parts and update VesselResource information properties (rates, brokers...)
		/// </summary>
		public void Sync(Vessel v, VesselData vd, double elapsed_s)
		{
			// execute all recorded recipes
			ResourceRecipe.ExecuteRecipes(v, this, recipes);

			// forget the recipes
			recipes.Clear();

			// apply all deferred requests and synchronize to vessel
			foreach (VesselResource info in resources.Values) info.Sync(v, vd, elapsed_s);
		}

		/// <summary> record deferred production of a resource (shortcut) </summary>
		/// <param name="brokerName">short ui-friendly name for the producer</param>
		public void Produce(Vessel v, string resource_name, double quantity, string brokerName)
		{
			GetResource(v, resource_name).Produce(quantity, brokerName);
		}

		/// <summary> record deferred consumption of a resource (shortcut) </summary>
		/// <param name="brokerName">short ui-friendly name for the consumer</param>
		public void Consume(Vessel v, string resource_name, double quantity, string tag)
		{
			GetResource(v, resource_name).Consume(quantity, tag);
		}

		/// <summary> record deferred execution of a recipe (shortcut) </summary>
		public void AddRecipe(ResourceRecipe recipe)
		{
			recipes.Add(recipe);
		}
	}
}
