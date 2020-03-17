using Flee.PublicTypes;
using System;
using System.Collections.Generic;


namespace KERBALISM
{


	public sealed class Process
	{
		public Process(ConfigNode node)
		{
			name = Lib.ConfigValue(node, "name", string.Empty);
			resourceName = name + "Res";
			title = Lib.ConfigValue(node, "title", string.Empty);
			desc = Lib.ConfigValue(node, "desc", string.Empty);
			canToggle = Lib.ConfigValue(node, "canToggle", true);
			broker = ResourceBroker.GetOrCreate(name, ResourceBroker.BrokerCategory.Converter, title);

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

			// dumpable default: all outputs are dumpable
			dumpable = Lib.Tokenize(Lib.ConfigValue(node, "dumpable", "all"), ',');
			if(dumpable.Count == 1 && dumpable[0].ToLower() == "all")
			{
				dumpable = new List<string>(outputs.Keys);
			}
			else if(dumpable.Count == 1 && dumpable[0].ToLower() == "none")
			{
				dumpable.Clear();
			}

			// defaultDumped default: no outputs are dumped by default
			defaultDumped = new List<string>();
			List<string> dumpedList = Lib.Tokenize(Lib.ConfigValue(node, "defaultDumped", ""), ',');
			if (dumpedList.Count == 1 && dumpedList[0].ToLower() == "all")
			{
				dumpedList = new List<string>(outputs.Keys);
			}
			else if (dumpedList.Count == 1 && dumpedList[0].ToLower() == "none")
			{
				dumpedList.Clear();
			}
			// retain only default dumpable outputs that actually are dumpable
			foreach (string o in dumpedList)
			{
				if (dumpable.Contains(o))
					defaultDumped.Add(o);
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
					Lib.Log($"Error parsing modifier for process {name} : '{modifierString}'\n{e}", Lib.LogLevel.Error);
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

		public void Execute(VesselData vd, VesselResHandler resources, double elapsed_s, List<string> dump)
		{
			// get product of all environment modifiers
			double k = EvaluateModifier(vd);

			// only execute processes if necessary
			if (k == 0.0)
				return;

			Recipe recipe = new Recipe(broker);

			foreach (var p in inputs)
			{
				recipe.AddInput(p.Key, p.Value * k * elapsed_s);
			}
			foreach (var p in outputs)
			{
				recipe.AddOutput(p.Key, p.Value * k * elapsed_s, dump.Contains(p.Key));
			}
			foreach (var p in cures)
			{
				// TODO this assumes that the cure modifies always put the resource first
				// works: modifier = _SickbayRDU,zerog works
				// fails: modifier = zerog,_SickbayRDU
				recipe.AddCure(p.Key, p.Value * k * elapsed_s, resourceName);
			}
			resources.AddRecipe(recipe);
		}

		internal Specifics Specifics(double capacity)
		{
			Specifics specs = new Specifics();
			foreach (KeyValuePair<string, double> pair in inputs)
			{
				//if (!modifiers.Contains(pair.Key))
					specs.Add(pair.Key, Lib.BuildString("<color=#ffaa00>", Lib.HumanReadableRate(pair.Value * capacity), "</color>"));
				//else
				//specs.Add(Local.ProcessController_info1, Lib.HumanReadableDuration(0.5 / pair.Value));//"Half-life"
			}
			foreach (KeyValuePair<string, double> pair in outputs)
			{
				specs.Add(pair.Key, Lib.BuildString("<color=#00ff00>", Lib.HumanReadableRate(pair.Value * capacity), "</color>"));
			}
			return specs;
		}

		public string name;                           // unique name for the process
		public string resourceName;
		public string title;                          // UI title
		public string desc;                           // UI description (long text)
		public bool canToggle;						  // defines if this process can be toggled
		public List<string> dumpable;				  // list of all outputs that can be dumped
		public List<string> defaultDumped;		      // list of all outputs that are dumped by default
		public Dictionary<string, double> inputs;     // input resources and rates
		public Dictionary<string, double> outputs;    // output resources and rates
		public Dictionary<string, double> cures;      // cures and rates
		public ResourceBroker broker;
		public bool hasModifier;
		private IGenericExpression<double> modifier;
		private static VesselDataBase modifierData = new VesselDataBase();
	}

} // KERBALISM

