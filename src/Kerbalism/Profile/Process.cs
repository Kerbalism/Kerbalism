using System;
using System.Collections.Generic;


namespace KERBALISM
{


	public sealed class Process
	{
		public Process(ConfigNode node)
		{
			name = Lib.ConfigValue(node, "name", string.Empty);
			modifiers = Lib.Tokenize(Lib.ConfigValue(node, "modifier", string.Empty), ',');

			// check that name is specified
			if (name.Length == 0) throw new Exception("skipping unnamed process");

			inputs = new Dictionary<string, double>();
			foreach (string input in node.GetValues("input"))
			{
				// get parameters
				List<string> tok = Lib.Tokenize(input, '@');
				if (tok.Count != 2) throw new Exception("malformed input on process " + name);
				string input_res = tok[0];
				double input_rate = Lib.Parse.ToDouble(tok[1]);

				// check that resource is specified
				if (input_res.Length == 0) throw new Exception("skipping resource-less process " + name);

				// check that resource exist
				if (Lib.GetDefinition(input_res) == null) throw new Exception("resource " + input_res + " doesn't exist for process " + name);

				// record input
				inputs[input_res] = input_rate;
			}

			outputs = new Dictionary<string, double>();
			foreach (string output in node.GetValues("output"))
			{
				// get parameters
				List<string> tok = Lib.Tokenize(output, '@');
				if (tok.Count != 2) throw new Exception("malformed output on process " + name);
				string output_res = tok[0];
				double output_rate = Lib.Parse.ToDouble(tok[1]);

				// check that resource is specified
				if (output_res.Length == 0) throw new Exception("skipping resource-less process " + name);

				// check that resource exist
				if (Lib.GetDefinition(output_res) == null) throw new Exception("resource " + output_res + " doesn't exist for process " + name);

				// record output
				outputs[output_res] = output_rate;
			}

			cures = new Dictionary<string, double>();
			foreach (string output in node.GetValues("cures"))
			{
				// get parameters
				List<string> tok = Lib.Tokenize(output, '@');
				if (tok.Count != 2) throw new Exception("malformed cure on process " + name);
				string cure = tok[0];
				double cure_rate = Lib.Parse.ToDouble(tok[1]);

				// check that resource is specified
				if (cure.Length == 0) throw new Exception("skipping resource-less process " + name);

				// record cure
				cures[cure] = cure_rate;
			}

			// parse dump specs
			dump = new DumpSpecs(Lib.ConfigValue(node, "dump", "false"), Lib.ConfigValue(node, "dump_valve", "false"));
		}


		public void Execute(Vessel v, Vessel_info vi, Vessel_resources resources, double elapsed_s)
		{
			// evaluate modifiers
			double k = Modifiers.Evaluate(v, vi, resources, modifiers);

			// only execute processes if necessary
			if (k > double.Epsilon)
			{
				// prepare recipe
				Resource_recipe recipe = new Resource_recipe();
				foreach (var p in inputs)
				{
					recipe.Input(p.Key, p.Value * k * elapsed_s);
				}
				foreach (var p in outputs)
				{
					recipe.Output(p.Key, p.Value * k * elapsed_s, dump.Check(p.Key));
				}
				foreach (var p in cures)
				{
					// TODO this assumes that the cure modifies always put the resource first
					// works: modifier = _SickbayRDU,zerog works
					// fails: modifier = zerog,_SickbayRDU
					recipe.Cure(p.Key, p.Value * k * elapsed_s, modifiers[0]);
				}
				resources.Transform(recipe);
			}
		}

		public string name;                           // unique name for the process
		public List<string> modifiers;                // if specified, rates are influenced by the product of all environment modifiers
		public Dictionary<string, double> inputs;     // input resources and rates
		public Dictionary<string, double> outputs;    // output resources and rates
		public Dictionary<string, double> cures;      // cures and rates
		public DumpSpecs dump;                        // set of output resources that should dump overboard
	}





} // KERBALISM

