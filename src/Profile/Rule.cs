using System;
using System.Collections.Generic;


namespace KERBALISM
{


	public sealed class Rule
	{
		public Rule(ConfigNode node)
		{
			name = Lib.ConfigValue(node, "name", string.Empty);
			input = Lib.ConfigValue(node, "input", string.Empty);
			output = Lib.ConfigValue(node, "output", string.Empty);
			interval = Lib.ConfigValue(node, "interval", 0.0);
			rate = Lib.ConfigValue(node, "rate", 0.0);
			ratio = Lib.ConfigValue(node, "ratio", 0.0);
			input_threshold = Lib.ConfigValue(node, "input_threshold", 0.0);
			degeneration = Lib.ConfigValue(node, "degeneration", 0.0);
			variance = Lib.ConfigValue(node, "variance", 0.0);
			modifiers = Lib.Tokenize(Lib.ConfigValue(node, "modifier", string.Empty), ',');
			breakdown = Lib.ConfigValue(node, "breakdown", false);
			monitor = Lib.ConfigValue(node, "monitor", false);
			monitor_offset = Lib.ConfigValue(node, "monitor_offset", 0.0);
			warning_threshold = Lib.ConfigValue(node, "warning_threshold", 0.33);
			danger_threshold = Lib.ConfigValue(node, "danger_threshold", 0.66);
			fatal_threshold = Lib.ConfigValue(node, "fatal_threshold", 1.0);
			warning_message = Lib.ConfigValue(node, "warning_message", string.Empty);
			danger_message = Lib.ConfigValue(node, "danger_message", string.Empty);
			fatal_message = Lib.ConfigValue(node, "fatal_message", string.Empty);
			relax_message = Lib.ConfigValue(node, "relax_message", string.Empty);

            // check that name is specified
            if (name.Length == 0) throw new Exception("skipping unnamed rule");

			// check that degeneration is not zero
			if (degeneration <= double.Epsilon) throw new Exception("skipping zero degeneration rule");

			// check that resources exist
			if (input.Length > 0 && Lib.GetDefinition(input) == null) throw new Exception("resource '" + input + "' doesn't exist");
			if (output.Length > 0 && Lib.GetDefinition(output) == null) throw new Exception("resource '" + output + "' doesn't exist");

			// calculate ratio of input vs output resource
			if (input.Length > 0 && output.Length > 0 && ratio <= double.Epsilon)
			{
				var input_density = Lib.GetDefinition(input).density;
				var output_density = Lib.GetDefinition(output).density;
				ratio = Math.Min(input_density, output_density) > double.Epsilon ? input_density / output_density : 1.0;
			}

			trigger = false;
		}


		public void Execute(Vessel v, Vessel_info vi, Vessel_resources resources, double elapsed_s)
		{
			// store list of crew to kill
			List<ProtoCrewMember> deferred_kills = new List<ProtoCrewMember>();

			// get input resource handler
			Resource_info res = input.Length > 0 ? resources.Info(v, input) : null;

			// determine message variant
			uint variant = vi.temperature < Settings.SurvivalTemperature ? 0 : 1u;

			// get product of all environment modifiers
			double k = Modifiers.Evaluate(v, vi, resources, modifiers);

			// for each crew
			foreach (ProtoCrewMember c in Lib.CrewList(v))
			{
				// get kerbal data
				KerbalData kd = DB.Kerbal(c.name);

				// skip rescue kerbals
				if (kd.rescue) continue;

				// skip disabled kerbals
				if (kd.disabled) continue;

				// get kerbal property data from db
				RuleData rd = kd.Rule(name);

				// if continuous
				double step;
				if (interval <= double.Epsilon)
				{
					// influence consumption by elapsed time
					step = elapsed_s;
				}
				// if interval-based
				else
				{
					// accumulate time
					rd.time_since += elapsed_s;

					// determine number of steps
					step = Math.Floor(rd.time_since / interval);

					// consume time
					rd.time_since -= step * interval;

					// remember if a meal is consumed/produced in this simulation step
					res.meal_happened |= step > 0.99;
					if (output.Length > 0) ResourceCache.Info(v, output).meal_happened |= step > 0.99;
				}

				// if continuous, or if one or more intervals elapsed
				if (step > double.Epsilon)
				{
					// if there is a resource specified
					if (res != null && rate > double.Epsilon)
					{
						// determine amount of resource to consume
						double required = rate        // consumption rate
										* k           // product of environment modifiers
										* step;       // seconds elapsed or number of steps

						// if there is no output
						if (output.Length == 0)
						{
							// simply consume (that is faster)
							res.Consume(required);
						}
						// if there is an output and monitor is false
						else if (!monitor)
						{
							// transform input into output resource
							// - rules always dump excess overboard (because it is waste)
							Resource_recipe recipe = new Resource_recipe();
							recipe.Input(input, required);
							recipe.Output(output, required * ratio, true);
							resources.Transform(recipe);
						}
						// if monitor then do not consume input resource and only produce output if resource percentage + monitor_offset is < 100%
						else if ((res.amount / res.capacity) + monitor_offset < 1.0)
						{
							// simply produce (that is faster)
							resources.Produce(v, output, required * ratio);
						}
					}

					// degenerate:
					// - if the environment modifier is not telling to reset (by being zero)
					// - if the input threshold is reached if used
					// - if this rule is resource-less, or if there was not enough resource in the vessel
					if (input_threshold >= double.Epsilon)
					{
						if (res.amount >= double.Epsilon && res.capacity >= double.Epsilon)
							trigger = (res.amount / res.capacity) + monitor_offset >= input_threshold;
						else
							trigger = false;
					}
					else
						trigger = input.Length == 0 || res.amount <= double.Epsilon;

					if (k > 0.0 && trigger)
					{
						rd.problem += degeneration           // degeneration rate per-second or per-interval
								   * k                       // product of environment modifiers
								   * step                    // seconds elapsed or by number of steps
								   * Variance(c, variance);  // kerbal-specific variance
					}
					// else slowly recover
					else
					{
						rd.problem *= 1.0 / (1.0 + Math.Max(interval, 1.0) * step * 0.002);
						rd.problem = Math.Max(rd.problem, 0.0);
					}
				}

				// kill kerbal if necessary
				if (rd.problem >= fatal_threshold)
				{
					if (fatal_message.Length > 0)
						Message.Post(breakdown ? Severity.breakdown : Severity.fatality, Lib.ExpandMsg(fatal_message, v, c, variant));

					if (breakdown)
					{
						// trigger breakdown event
						Misc.Breakdown(v, c);

						// move back between warning and danger level
						rd.problem = (warning_threshold + danger_threshold) * 0.5;

						// make sure next danger message is shown
						rd.message = 1;
					}
					else
					{
						deferred_kills.Add(c);
					}
				}
				// show messages
				else if (rd.problem >= danger_threshold && rd.message < 2)
				{
					if (danger_message.Length > 0) Message.Post(Severity.danger, Lib.ExpandMsg(danger_message, v, c, variant));
					rd.message = 2;
				}
				else if (rd.problem >= warning_threshold && rd.message < 1)
				{
					if (warning_message.Length > 0) Message.Post(Severity.warning, Lib.ExpandMsg(warning_message, v, c, variant));
					rd.message = 1;
				}
				else if (rd.problem < warning_threshold && rd.message > 0)
				{
					if (relax_message.Length > 0) Message.Post(Severity.relax, Lib.ExpandMsg(relax_message, v, c, variant));
					rd.message = 0;
				}
			}

			// execute the deferred kills
			foreach (ProtoCrewMember c in deferred_kills)
			{
				Misc.Kill(v, c);
			}
		}


		// return per-kerbal variance, in the range [1-variance,1+variance]
		static double Variance(ProtoCrewMember c, double variance)
		{
			// get a value in [0..1] range associated with a kerbal
			double k = (double)Lib.Hash32(c.name.Replace(" Kerman", "")) / (double)UInt32.MaxValue;

			// move in [-1..+1] range
			k = k * 2.0 - 1.0;

			// return kerbal-specific variance in range [1-n .. 1+n]
			return 1.0 + variance * k;
		}


		public string name;               // unique name for the rule
		public string input;              // resource consumed, if any
		public string output;             // resource produced, if any
		public double interval;           // if 0 the rule is executed per-second, else it is executed every 'interval' seconds
		public double rate;               // amount of input resource to consume at each execution
		public double ratio;              // ratio of output resource in relation to input consumed
		public double input_threshold;    // when input resource reaches this percentage of its capacity trigger degeneration [range 0 to 1]
		public double degeneration;       // amount to add to the degeneration at each execution (when we must degenerate)
		public double variance;           // variance for degeneration rate, unique per-kerbal and in range [1.0-variance, 1.0+variance]
		public List<string> modifiers;    // if specified, rates are influenced by the product of all environment modifiers
		public bool breakdown;            // if true, trigger a breakdown instead of killing the kerbal
		public bool monitor;              // if true and input resource exists only monitor the input resource, do not consume it
		public double monitor_offset;     // add this percentage to input resource to stop output and alter trigger [range 0 to 1]
		public double warning_threshold;  // threshold of degeneration used to show warning messages and yellow status color
		public double danger_threshold;   // threshold of degeneration used to show danger messages and red status color
		public double fatal_threshold;    // threshold of degeneration used to show fatal messages and kill/breakdown the kerbal
		public string warning_message;    // messages shown on threshold crossings
		public string danger_message;     // .
		public string fatal_message;      // .
		public string relax_message;      // .

		private bool trigger;             // internal use
	}





} // KERBALISM


