using Flee.PublicTypes;
using System;
using System.Collections.Generic;


namespace KERBALISM
{


	public sealed class Rule
	{
		public Rule(ConfigNode node)
		{
			name = Lib.ConfigValue(node, "name", string.Empty);
			title = Lib.ConfigValue(node, "title", name);
			input = Lib.ConfigValue(node, "input", string.Empty);
			output = Lib.ConfigValue(node, "output", string.Empty);
			rate = Lib.ConfigValue(node, "rate", 0.0);
			ratio = Lib.ConfigValue(node, "ratio", 0.0);
			degeneration = Lib.ConfigValue(node, "degeneration", 0.0);
			regeneration = Lib.ConfigValue(node, "regeneration", 0.002);
			variance = Lib.ConfigValue(node, "variance", 0.0);
			individuality = Lib.ConfigValue(node, "individuality", 0.0);
			breakdown = Lib.ConfigValue(node, "breakdown", false);
			lifetime = Lib.ConfigValue(node, "lifetime", false);
			warning_threshold = Lib.ConfigValue(node, "warning_threshold", 0.33);
			danger_threshold = Lib.ConfigValue(node, "danger_threshold", 0.66);
			fatal_threshold = Lib.ConfigValue(node, "fatal_threshold", 1.0);
			warning_message = Lib.ConfigValue(node, "warning_message", string.Empty);
			danger_message = Lib.ConfigValue(node, "danger_message", string.Empty);
			fatal_message = Lib.ConfigValue(node, "fatal_message", string.Empty);
			relax_message = Lib.ConfigValue(node, "relax_message", string.Empty);
			broker = ResourceBroker.GetOrCreate(name, ResourceBroker.BrokerCategory.Kerbal, title);

			if (warning_message.Length > 0 && warning_message[0] == '#') Lib.Log("Broken translation: " + warning_message, Lib.LogLevel.Warning);
			if (danger_message.Length > 0 && danger_message[0] == '#') Lib.Log("Broken translation: " + danger_message, Lib.LogLevel.Warning);
			if (fatal_message.Length > 0 && fatal_message[0] == '#') Lib.Log("Broken translation: " + fatal_message, Lib.LogLevel.Warning);
			if (relax_message.Length > 0 && relax_message[0] == '#') Lib.Log("Broken translation: " + relax_message, Lib.LogLevel.Warning);

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

			string modifierString = Lib.ConfigValue(node, "modifier", string.Empty);
			hasModifier = modifierString != string.Empty;
			if (hasModifier)
			{
				try
				{
					modifier = modifierData.ModifierContext.CompileGeneric<double>(modifierString);
				}
				catch (Exception e)
				{
					ErrorManager.AddError(false, $"Can't parse modifier for rule '{name}'", $"modifier : {modifierString}\n{e.Message}");
					hasModifier = false;
				}
			}
		}

		public double EvaluateModifier(VesselDataBase data)
		{
			if (hasModifier)
			{
				modifier.Owner = data;
				return Lib.Clamp(modifier.Evaluate(), 0.0, double.MaxValue);
			}
			else
			{
				return 1.0;
			}
		}

		public void Execute(Vessel v, VesselData vd, VesselResHandler resources, double elapsed_s)
		{
			// store list of crew to kill
			List<ProtoCrewMember> deferred_kills = new List<ProtoCrewMember>();

			// get input resource handler
			VesselResource res = input.Length > 0 ? resources.GetResource(input) : null;

			// determine message variant
			uint variant = vd.EnvTemperature < Settings.LifeSupportSurvivalTemperature ? 0 : 1u;

			// get product of all environment modifiers
			double k = EvaluateModifier(vd);

			bool lifetime_enabled = PreferencesRadiation.Instance.lifetime;

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
				rd.lifetime = lifetime_enabled && lifetime;

				// if there is a resource specified
				if (res != null && rate > double.Epsilon)
				{
					// get rate including per-kerbal variance
					double resRate =
						rate                                // consumption rate
						* Variance(name, c, individuality)  // kerbal-specific variance
						* k;								// product of environment modifiers

					// determine amount of resource to consume
					
					double required = resRate * elapsed_s;       // influence consumption by elapsed time

					// if there is no output
					if (output.Length == 0)
					{
						// simply consume (that is faster)
						res.Consume(required, broker);
					}
					// if there is an output
					else
					{
						// transform input into output resource
						// - rules always dump excess overboard (because it is waste)
						Recipe recipe = new Recipe(broker); // kerbals are not associated with a part
						recipe.AddInput(input, required);
						recipe.AddOutput(output, required * ratio, true);
						resources.AddRecipe(recipe);
					}
				}

				// degenerate:
				// - if the environment modifier is not telling to reset (by being zero)
				// - if this rule is resource-less, or if there was not enough resource in the vessel
				if (k > 0.0 && (input.Length == 0 || res.AvailabilityFactor < 1.0))
				{
					rd.problem += degeneration           // degeneration rate per-second or per-interval
								* k                       // product of environment modifiers
								* elapsed_s                    // seconds elapsed
								* (input.Length == 0 ? 1.0 : 1.0 - res.AvailabilityFactor) // scale by resource availability
								* Variance(name, c, variance); // kerbal-specific variance
				}
				// else slowly recover
				else
				{
					rd.problem = Math.Max(rd.problem - (regeneration * elapsed_s), 0.0);
				}

				bool do_breakdown = false;

				if (breakdown)
				{
					// don't do breakdowns and don't show stress message if disabled
					if (!PreferencesComfort.Instance.stressBreakdowns)
						return;

					// stress level
					double breakdown_probability = rd.problem / warning_threshold;
					breakdown_probability = Lib.Clamp(breakdown_probability, 0.0, 1.0);

					// use the stupidity of a kerbal.
					// however, nobody is perfect - not even a kerbal with a stupidity of 0.
					breakdown_probability *= c.stupidity * 0.6 + 0.4;

					// apply the weekly error rate
					breakdown_probability *= PreferencesComfort.Instance.stressBreakdownRate;

					// now we have the probability for one failure per week, based on the
					// individual stupidity and stress level of the kerbal.

					breakdown_probability = (breakdown_probability * elapsed_s) / Lib.SecondsInYearExact;
					if (breakdown_probability > Lib.RandomDouble()) {
						do_breakdown = true;

						// we're stressed out and just made a major mistake, this further increases the stress level...
						rd.problem += warning_threshold * 0.05; // add 5% of the warning treshold to current stress level
 					}
				}

				// kill kerbal if necessary
				if (rd.problem >= fatal_threshold)
				{
#if DEBUG || DEVBUILD
					Lib.Log("Rule " + name + " kills " + c.name + " at " + rd.problem + " " + degeneration + "/" + k + "/" + Variance(name, c, variance));
#endif
					if (fatal_message.Length > 0)
						Message.Post(breakdown ? Severity.breakdown : Severity.fatality, Lib.ExpandMsg(fatal_message, v, c, variant));

					if (breakdown)
					{
						do_breakdown = true;

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

				if(do_breakdown) {
					// trigger breakdown event
					Misc.Breakdown(v, c);
				}
			}

			// execute the deferred kills
			foreach (ProtoCrewMember c in deferred_kills)
			{
				Misc.Kill(v, c);
			}
		}


		// return per-kerbal variance, in the range [1-variance,1+variance]
		static double Variance(String name, ProtoCrewMember c, double variance)
		{
			if (variance < Double.Epsilon)
				return 1.0;

			// get a value in [0..1] range associated with a kerbal
			// we want this to be pseudo-random, so don't just add/multiply the two values, that would be too predictable
			// also add the process name. a kerbal that eats a lot shouldn't necessarily drink a lot, too
			double k = (double)Lib.Hash32(name + c.courage.ToString() + c.stupidity.ToString()) / (double)UInt32.MaxValue;

			// move in [-1..+1] range
			//k = Lib.Clamp(k * 2.0 - 1.0, -1.0, 1.0);
			k = k * 2.0 - 1.0;

			// return kerbal-specific variance in range [1-n .. 1+n]
			return 1.0 + variance * k;
		}


		public string name;               // unique name for the rule
		public string title;              // UI title
		public string input;              // resource consumed, if any
		public string output;             // resource produced, if any
		public double rate;               // amount of input resource to consume at each execution
		public double ratio;              // ratio of output resource in relation to input consumed
		public double degeneration;       // degeneration/second added when modifiers evaluation > 0 or input resource is unavailable
		public double regeneration;       // degeneration/second removed when modifiers evaluation == 0 and input resource is available
		public double variance;           // variance for degeneration rate, unique per-kerbal and in range [1.0-x, 1.0+x]
		public double individuality;      // variance for process rate, unique per-kerbal and in range [1.0-x, 1.0+x]
		public bool breakdown;            // if true, trigger a breakdown instead of killing the kerbal
		public double warning_threshold;  // threshold of degeneration used to show warning messages and yellow status color
		public double danger_threshold;   // threshold of degeneration used to show danger messages and red status color
		public double fatal_threshold;    // threshold of degeneration used to show fatal messages and kill/breakdown the kerbal
		public string warning_message;    // messages shown on threshold crossings
		public string danger_message;     // .
		public string fatal_message;      // .
		public string relax_message;      // .
		public bool lifetime;             // does this value accumulate over the lifetime of a kerbal
		public ResourceBroker broker;

		public bool hasModifier;
		private IGenericExpression<double> modifier;
		private static VesselDataBase modifierData = new VesselDataBase();
	}





} // KERBALISM


