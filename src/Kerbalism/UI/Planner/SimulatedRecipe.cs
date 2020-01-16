using System;
using System.Collections.Generic;

namespace KERBALISM.Planner
{

	///<summary> Description of how to convert inputs to outputs,
	/// this class is also responsible for executing the recipe, such that it is actualized in the <see cref="SimulatedResource"/> </summary>
	public sealed class SimulatedRecipe
	{
		public SimulatedRecipe(Part p, string name)
		{
			this.name = name;
			this.inputs = new List<ResourceRecipe.Entry>();
			this.outputs = new List<ResourceRecipe.Entry>();
			this.left = 1.0;
			this.loaded_part = p;
		}

		/// <summary>
		/// add an input to the recipe
		/// </summary>
		public void Input(string resource_name, double quantity)
		{
			if (quantity > double.Epsilon) //< avoid division by zero
			{
				inputs.Add(new ResourceRecipe.Entry(resource_name, quantity));
			}
		}

		/// <summary>
		/// add a combined input to the recipe
		/// </summary>
		public void Input(string resource_name, double quantity, string combined)
		{
			if (quantity > double.Epsilon) //< avoid division by zero
			{
				inputs.Add(new ResourceRecipe.Entry(resource_name, quantity, true, combined));
			}
		}

		// add an output to the recipe
		public void Output(string resource_name, double quantity, bool dump)
		{
			if (quantity > double.Epsilon) //< avoid division by zero
			{
				outputs.Add(new ResourceRecipe.Entry(resource_name, quantity, dump));
			}
		}

		// execute the recipe
		public bool Execute(ResourceSimulator sim)
		{
			// determine worst input ratio
			double worst_input = left;
			if (outputs.Count > 0)
			{
				for (int i = 0; i < inputs.Count; ++i)
				{
					ResourceRecipe.Entry e = inputs[i];
					SimulatedResourceView res = sim.Resource(e.name).GetSimulatedResourceView(loaded_part);
					// handle combined inputs
					if (e.combined != null)
					{
						// is combined resource the primary
						if (e.combined != "")
						{
							ResourceRecipe.Entry sec_e = inputs.Find(x => x.name.Contains(e.combined));
							SimulatedResourceView sec = sim.Resource(sec_e.name).GetSimulatedResourceView(loaded_part);
							double pri_worst = Lib.Clamp(res.amount * e.inv_quantity, 0.0, worst_input);
							if (pri_worst > 0.0)
								worst_input = pri_worst;
							else
								worst_input = Lib.Clamp(sec.amount * sec_e.inv_quantity, 0.0, worst_input);
						}
					}
					else
						worst_input = Lib.Clamp(res.amount * e.inv_quantity, 0.0, worst_input);
				}
			}

			// determine worst output ratio
			double worst_output = left;
			if (inputs.Count > 0)
			{
				for (int i = 0; i < outputs.Count; ++i)
				{
					ResourceRecipe.Entry e = outputs[i];
					if (!e.dump) // ignore outputs that can dump overboard
					{
						SimulatedResourceView res = sim.Resource(e.name).GetSimulatedResourceView(loaded_part);
						worst_output = Lib.Clamp((res.capacity - res.amount) * e.inv_quantity, 0.0, worst_output);
					}
				}
			}

			// determine worst-io
			double worst_io = Math.Min(worst_input, worst_output);

			// consume inputs
			for (int i = 0; i < inputs.Count; ++i)
			{
				ResourceRecipe.Entry e = inputs[i];
				SimulatedResource res = sim.Resource(e.name);
				// handle combined inputs
				if (e.combined != null)
				{
					// is combined resource the primary
					if (e.combined != "")
					{
						ResourceRecipe.Entry sec_e = inputs.Find(x => x.name.Contains(e.combined));
						SimulatedResourceView sec = sim.Resource(sec_e.name).GetSimulatedResourceView(loaded_part);
						double need = (e.quantity * worst_io) + (sec_e.quantity * worst_io);
						// do we have enough primary to satisfy needs, if so don't consume secondary
						if (res.amount >= need)
							res.Consume(need, name);
						// consume primary if any available and secondary
						else
						{
							need -= res.amount;
							res.Consume(res.amount, name);
							sec.Consume(need, name);
						}
					}
				}
				else
					res.Consume(e.quantity * worst_io, name);
			}

			// produce outputs
			for (int i = 0; i < outputs.Count; ++i)
			{
				ResourceRecipe.Entry e = outputs[i];
				SimulatedResourceView res = sim.Resource(e.name).GetSimulatedResourceView(loaded_part);
				res.Produce(e.quantity * worst_io, name);
			}

			// update amount left to execute
			left -= worst_io;

			// the recipe was executed, at least partially
			return worst_io > double.Epsilon;
		}

		// store inputs and outputs
		public string name;                         // name used for consumer/producer tooltip
		public List<ResourceRecipe.Entry> inputs;  // set of input resources
		public List<ResourceRecipe.Entry> outputs; // set of output resources
		public double left;                         // what proportion of the recipe is left to execute
		private Part loaded_part = null;            // part this recipe runs on, may be null for vessel wide recipe
	}


} // KERBALISM
