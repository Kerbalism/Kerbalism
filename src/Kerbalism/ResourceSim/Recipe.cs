using System;
using System.Collections.Generic;

namespace KERBALISM
{
	/// <summary>
	/// ResourceRecipe is a mean of converting inputs to outputs.
	/// It does so in relation with the rest of the resource simulation to detect available amounts for inputs and available capacity for outputs.
	/// Outputs can be defined a "dumpeable" to avoid this last limitation.
	/// </summary>

	// TODO : (GOTMACHINE) At some point, we want to use "virtual" resources in recipes.
	// Their purpose would be to give the ability to scale the non-resource output of a pure consumer.
	// Example : to scale antenna data rate by EC availability, define an "antennaOutput" virtual resource and a recipe that convert EC to antennaOutput
	// then check "antennaOutput" availability to scale the amount of data sent

	// TODO : (yup another one) : in 95% of cases, the same recipes are recreated every update, with only a modification of the Entry.quantity field.
	// It would make a lot of sense to move Entry from a struct to a class and to make Recipe users keep the Recipe/Entry references, then just adjust the Entry.quantity.
	// That would notably allow to call only once GetResource() at the entry ctor (instead of again and again for each recipe execution step)
	// and change Recipe from okayish to lightning fast from a performance standpoint, as well as eliminate all heap memory allocations.

	// TODO : currently, "pure input recipes can just underflow", which is an issue for AvailabilityFactor calculations.
	// Since Recipes are assumed to auto-scale with availability, they register their consumptions in consumeCriticalRequests
	// and aren't scaled by AvailabilityFactor. The fix is probably to just remove the "if (outputs.Count > 0)" condition, but
	// I need to check that in detail before messing with this

	public sealed class Recipe
	{
		public struct Entry
		{
			public Entry(string name, double quantity, bool dump = true)
			{
				this.name = name;
				this.quantity = quantity;
				this.inv_quantity = 1.0 / quantity;
				this.dump = dump;
			}
			public string name;
			public double quantity;
			public double inv_quantity;
			public bool dump;
		}

		private List<Entry> inputs;   // set of input resources
		private List<Entry> outputs;  // set of output resources
		private double left;     // what proportion of the recipe is left to execute

		public double UtilizationFactor => 1.0 - left;

		private ResourceBroker broker;

		public Recipe(ResourceBroker broker)
		{
			this.inputs = new List<Entry>();
			this.outputs = new List<Entry>();
			this.left = 1.0;
			this.broker = broker;
		}

		/// <summary>add an input to the recipe</summary>
		public void AddInput(string resource_name, double quantity)
		{
			if (quantity > double.Epsilon) //< avoid division by zero
			{
				inputs.Add(new Entry(resource_name, quantity));
			}
		}

		/// <summary>add an output to the recipe</summary>
		public void AddOutput(string resource_name, double quantity, bool dump)
		{
			if (quantity > double.Epsilon) //< avoid division by zero
			{
				outputs.Add(new Entry(resource_name, quantity, dump));
			}
		}

		/// <summary>Execute all recipes and record deferred consumption/production for inputs/ouputs</summary>
		public static void ExecuteRecipes(VesselResHandler resources, List<Recipe> recipes, Vessel v = null)
		{
			bool executing = true;
			while (executing)
			{
				executing = false;
				for (int i = 0; i < recipes.Count; ++i)
				{
					Recipe recipe = recipes[i];
					if (recipe.left > double.Epsilon)
					{
						executing |= recipe.ExecuteRecipeStep(resources, v);
					}
				}
			}
		}

		/// <summary>
		/// Execute the recipe and record deferred consumption/production for inputs/ouputs.
		/// This need to be called multiple times until left <= 0.0 for complete execution of the recipe.
		/// return true if recipe execution is completed, false otherwise
		/// </summary>
		private bool ExecuteRecipeStep(VesselResHandler resources, Vessel v = null)
		{
			// determine worst input ratio
			// - pure input recipes can just underflow
			double worst_input = left;
			if (outputs.Count > 0)
			{
				for (int i = 0; i < inputs.Count; ++i)
				{
					Entry e = inputs[i];
					VesselResource res = resources.GetResource(e.name);
					worst_input = Lib.Clamp((res.Amount + res.Deferred) * e.inv_quantity, 0.0, worst_input);
				}
			}

			// determine worst output ratio
			// - pure output recipes can just overflow
			double worst_output = left;
			//if (inputs.Count > 0)
			//{
				for (int i = 0; i < outputs.Count; ++i)
				{
					Entry e = outputs[i];
					if (!e.dump) // ignore outputs that can dump overboard
					{
						VesselResource res = resources.GetResource(e.name);
						worst_output = Lib.Clamp((res.Capacity - (res.Amount + res.Deferred)) * e.inv_quantity, 0.0, worst_output);
					}
				}
			//}

			// determine worst-io
			double worst_io = Math.Min(worst_input, worst_output);

			// consume inputs
			for (int i = 0; i < inputs.Count; ++i)
			{
				Entry e = inputs[i];
				VesselResource res = resources.GetResource(e.name);
				res.RecipeConsume(e.quantity * worst_io, broker);
			}

			// produce outputs
			for (int i = 0; i < outputs.Count; ++i)
			{
				Entry e = outputs[i];
				VesselResource res = resources.GetResource(e.name);
				res.Produce(e.quantity * worst_io, broker);
			}

			// update amount left to execute
			left -= worst_io;

			// the recipe was executed, at least partially
			return worst_io > double.Epsilon;
		}
	}
}
