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

		public class Input : Resource { }

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
		public string resourceName;
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

			resourceName = Lib.ConfigValue(node, "resourceName", name + "Resource");
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
					ErrorManager.AddError(false, $"Can't parse modifier for process '{name}'", $"modifier: {modifierString}\n{e.Message}");
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

			LogProcessRates();
		}

		private void LogProcessRates()
		{
#if DEBUG || DEVBUILD
			double totalInputMass = 0;
			double totalOutputMass = 0;
			StringBuilder sb = new StringBuilder();

			// this will only be printed if the process looks suspicious
			sb.Append($"Process {name} changes total mass of vessel:").AppendLine();

			foreach (Input i in inputs)
			{
				PartResourceDefinition resourceDef = PartResourceLibrary.Instance.GetDefinition(i.name);
				if (resourceDef == null)
				{
					sb.Append($"Unknown input resource {i.name}").AppendLine();
				}
				else
				{
					double kilosPerUnit =  resourceDef.density * 1000.0;
					double kilosPerHour = 3600.0 * i.rate * kilosPerUnit;
					totalInputMass += kilosPerHour;
					sb.Append($"Input {i.name}@{i.rate} = {kilosPerHour} kg/h").AppendLine();
				}
			}

			foreach (Output o in outputs)
			{
				PartResourceDefinition resourceDef = PartResourceLibrary.Instance.GetDefinition(o.name);
				if (resourceDef == null)
				{
					sb.Append($"$Unknown output resource {o.name}").AppendLine();
				}
				else
				{
					double kilosPerUnit =  resourceDef.density * 1000.0;
					double kilosPerHour = 3600.0 * o.rate * kilosPerUnit;
					totalOutputMass += kilosPerHour;
					sb.Append($"Output {o.name}@{o.rate} = {kilosPerHour} kg/h").AppendLine();
				}
			}

			sb.Append($"Total input mass : {totalInputMass}").AppendLine();
			sb.Append($"Total output mass: {totalOutputMass}").AppendLine();

			// there will be some numerical errors involved in the simulation.
			// due to the very small numbers (very low rates per second, calculated 20 times per second and more),
			// the actual rates might be quite different when the simulation runs. here we just look at nominal process
			// inputs and outputs for one hour, eliminating the error that will be introduced when the simulation runs
			// at slower speeds.
			// you can't put floating point numbers into a computer and expect perfect results, so we ignore processes that are "good enough".
			double diff = totalOutputMass - totalInputMass;

			if(diff > 0.001) // warn if process generates > 1g/h
			{
				sb.Append($"Process is generating mass: {diff} kg/h ({(diff*1000.0).ToString("F5")} g/h)").AppendLine();
				sb.Append("Note: this might be expected behaviour if external resources (like air) are used as an input.").AppendLine();
				Lib.Log(sb.ToString());
			}

			if(diff < -0.01) // warn if process looses > 10g/h
			{
				sb.Append($"Process looses more than 1g/h mass: {diff} kg/h ({(diff * 1000.0).ToString("F5")} g/h)");
				Lib.Log(sb.ToString());
			}
#endif
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
				return data.ResHandler.GetResource(resourceName).Amount;
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

