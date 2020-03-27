using Flee.PublicTypes;
using System;
using System.Collections.Generic;
using System.Text;

namespace KERBALISM
{
	public sealed class Process
	{
		public class Resource
		{
			public string name;
			public string displayName;
			public string abbreviation;
			public double rate;

			public virtual bool Load(ConfigNode node)
			{
				name = Lib.ConfigValue(node, "name", string.Empty);
				if (name.Length == 0)
				{
					Lib.Log($"skipping INPUT definition with no name in process {name}", Lib.LogLevel.Error);
					return false;
				}

				rate = Lib.ConfigValue(node, "rate", 0.0);
				if (rate <= 0.0)
				{
					Lib.Log($"skipping INPUT definition with no rate in process {name}", Lib.LogLevel.Error);
					return false;
				}

				PartResourceDefinition resourceDef = PartResourceLibrary.Instance.GetDefinition(name);
				if (resourceDef != null)
				{
					displayName = resourceDef.displayName;
					abbreviation = resourceDef.abbreviation;
				}
				else
				{
					displayName = name;
					abbreviation = name.Substring(0, 3);
				}

				return true;
			}
		}

		public class Input : Resource
		{ }

		public class Output : Resource
		{
			public bool canDump;
			public bool dumpByDefault;

			public override bool Load(ConfigNode node)
			{
				if (!base.Load(node))
					return false;

				canDump = Lib.ConfigValue(node, "canDump", true);

				if (canDump)
					dumpByDefault = Lib.ConfigValue(node, "dumpByDefault", false);
				else
					dumpByDefault = false;

				return true;
			}
		}

		private static VesselDataBase modifierData = new VesselDataBase();
		private static StringBuilder sb = new StringBuilder();

		public string name;                           // unique name for the process
		public string pseudoResourceName;
		public string title;                          // UI title
		public string desc;                           // UI description (long text)
		public bool canToggle;                        // defines if this process can be toggled
		public List<Input> inputs;
		public List<Output> outputs;

		public ResourceBroker broker;
		public bool hasModifier;
		private IGenericExpression<double> modifier;
		

		public Process(ConfigNode node)
		{
			name = Lib.ConfigValue(node, "name", string.Empty);
			if (name.Length == 0)
				throw new Exception("skipping unnammed process");

			pseudoResourceName = name + "Res";
			title = Lib.ConfigValue(node, "title", string.Empty);
			desc = Lib.ConfigValue(node, "desc", string.Empty);
			canToggle = Lib.ConfigValue(node, "canToggle", true);
			broker = ResourceBroker.GetOrCreate(name, ResourceBroker.BrokerCategory.Converter, title);

			string modifierString = Lib.ConfigValue(node, "modifier", string.Empty);
			hasModifier = modifierString.Length > 0;
			if (hasModifier)
			{
				try
				{
					modifier = modifierData.ModifierContext.CompileGeneric<double>(modifierString);
				}
				catch (Exception e)
				{
					ErrorManager.AddError(false, $"Can't parse modifier for process '{name}'", $"modifier : {modifierString}\n{e.Message}");
					hasModifier = false;
				}
			}

			inputs = new List<Input>();
			foreach (ConfigNode inputNode in node.GetNodes("INPUT"))
			{
				Input input = new Input();
				if (!input.Load(inputNode))
					continue;

				inputs.Add(input);
			}

			outputs = new List<Output>();
			foreach (ConfigNode outputNode in node.GetNodes("OUTPUT"))
			{
				Output output = new Output();
				if (!output.Load(outputNode))
					continue;

				outputs.Add(output);
			}

			if (inputs.Count == 0 && outputs.Count == 0)
				throw new Exception($"Process {name} has no valid input or output, skipping..");
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

		public void Execute(VesselDataBase vd, double elapsed_s)
		{
			// get product of all environment modifiers
			double k = EvaluateModifier(vd);

			// only execute processes if necessary
			if (k <= 0.0)
				return;

			vd.VesselProcesses.TryGetProcessData(name, out VesselProcess vesselProcess);

			Recipe recipe = new Recipe(broker);

			foreach (Input input in inputs)
			{
				recipe.AddInput(input.name, input.rate * k * elapsed_s);
			}

			foreach (Output output in outputs)
			{
				if (vesselProcess != null && vesselProcess.dumpedOutputs.Contains(output.name))
					recipe.AddOutput(output.name, output.rate * k * elapsed_s, true);
				else
					recipe.AddOutput(output.name, output.rate * k * elapsed_s, output.dumpByDefault);
			}

			vd.ResHandler.AddRecipe(recipe);
		}

		public string GetInfo(double capacity, bool includeDescription)
		{
			sb.Clear();

			if (includeDescription && desc.Length > 0)
			{
				sb.AppendKSPLine(desc);
				sb.AppendKSPNewLine();
			}
			int inputCount = inputs.Count;
			int outputCount = outputs.Count;

			for (int i = 0; i < outputCount; i++)
			{
				Output output = outputs[i];
				sb.Append(Lib.Color(Lib.HumanReadableRate(output.rate * capacity, "F3", string.Empty, true), Lib.Kolor.PosRate, true));
				sb.Append("\t");
				sb.Append(output.displayName);

				if (i < outputCount - 1 || inputCount > 0)
					sb.AppendKSPNewLine();
			}

			for (int i = 0; i < inputCount; i++)
			{
				Input input = inputs[i];
				sb.Append(Lib.Color(Lib.HumanReadableRate(-input.rate * capacity, "F3", string.Empty, true), Lib.Kolor.NegRate, true));
				sb.Append("\t");
				sb.Append(input.displayName);

				if (i < inputCount - 1)
					sb.AppendKSPNewLine();
				
				// TODO : what about self-consuming processes ? 
				//specs.Add(Local.ProcessController_info1, Lib.HumanReadableDuration(0.5 / pair.Value));//"Half-life"
			}



			return sb.ToString();
		}
	}

} // KERBALISM

