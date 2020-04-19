using Expansions.Serenity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static KERBALISM.ExperimentRequirements;

namespace KERBALISM
{
	public sealed class ExperimentModuleDefinition
	{
		private static StringBuilder sb = new StringBuilder();

		private string definitionSpec;
		private string completeSpec;

		public ExperimentInfo Info { get; private set; }

		public string Name { get; private set; }

		public string AvailableOnPartsList { get; private set; }

		/// <summary> Duration in seconds </summary>
		public double Duration { get; private set; } = 60;

		/// <summary> Data rate, automatically calculated from desired duration and experiments data size </summary>
		public double DataRate { get; private set; }

		/// <summary> EC requirement (units/second) </summary>
		public double RequiredEC = 0.01;

		/// <summary> the amount of samples this unit is shipped with </summary>
		public double Samples { get; private set; } = 0.0;

		/// <summary> If true, the experiment will generate mass out of nothing (surface samples) </summary>
		public bool SampleCollecting { get; private set; } = false;

		/// <summary> Operator crew. If set, crew has to be on vessel for the experiment to run </summary>
		public CrewSpecs CrewOperate { get; private set; }

		/// <summary> Experiment requirements </summary>
		public ExperimentRequirements Requirements { get; private set; }

		/// <summary> Resource requirements </summary>
		public List<ObjectPair<string, double>> Resources { get; private set; }

		/// <summary> tech nodes at which this module is unlocked </summary>
		private HashSet<string> availableAtTechs = new HashSet<string>();

		public ExperimentModuleDefinition(ExperimentInfo experimentInfo)
		{
			Info = experimentInfo;
			Name = experimentInfo.ExperimentId;
			DataRate = Info.DataSize / Duration;
		}

		public ExperimentModuleDefinition(ExperimentInfo experimentInfo, ConfigNode moduleDefinition)
		{
			Info = experimentInfo;

			Name = Lib.ConfigValue(moduleDefinition, "name", experimentInfo.ExperimentId);
			Duration = Lib.ParseConfigDuration(Lib.ConfigValue(moduleDefinition, "Duration", "60s"));
			DataRate = Info.DataSize / Duration;
			RequiredEC = Lib.ConfigValue(moduleDefinition, "RequiredEC", 0.0);
			SampleCollecting = Lib.ConfigValue(moduleDefinition, "SampleCollecting", false);
			Samples = Lib.ConfigValue(moduleDefinition, "Samples", SampleCollecting ? 0.0 : 1.0);
			CrewOperate = new CrewSpecs(Lib.ConfigValue(moduleDefinition, "CrewOperate", string.Empty));
			Requirements = new ExperimentRequirements(Lib.ConfigValue(moduleDefinition, "Requirements", string.Empty));
			Resources = ParseResources(Lib.ConfigValue(moduleDefinition, "Resources", string.Empty));
		}

		public string ModuleInfo(bool includeDescription = true, bool shortWithoutExpInfo = false)
		{
			sb.Clear();
			if (includeDescription)
			{
				sb.AppendKSPLine(Lib.Italic(Info.Description));
				sb.AppendKSPNewLine();
			}

			if (shortWithoutExpInfo && definitionSpec != null)
				sb.Append(definitionSpec);
			else
				sb.Append(completeSpec);

			return sb.ToString();
		}

		/// <summary> return true if <br/>
		/// - a part containing a module having that defintion is unlocked at the provided tech <br/>
		/// - this definition is available at any tech level
		/// </summary>
		public bool IsAvailableAtTech(string techNode)
		{
			if (availableAtTechs.Count == 0)
				return true;

			return availableAtTechs.Contains(techNode);
		}

		public bool IsResearched()
		{
			if (availableAtTechs.Count == 0)
				return true;

			foreach (string tech in availableAtTechs)
			{
				if (ResearchAndDevelopment.GetTechnologyState(tech) == RDTech.State.Available)
				{
					return true;
				}
			}

			return false;
		}

		private static readonly List<ObjectPair<string, double>> noResources = new List<ObjectPair<string, double>>();

		public static List<ObjectPair<string, double>> ParseResources(string resources, bool logErrors = false)
		{
			if (string.IsNullOrEmpty(resources)) return noResources;

			List<ObjectPair<string, double>> defs = new List<ObjectPair<string, double>>();
			var reslib = PartResourceLibrary.Instance.resourceDefinitions;

			foreach (string s in Lib.Tokenize(resources, ','))
			{
				// definitions are Resource@rate
				var p = Lib.Tokenize(s, '@');
				if (p.Count != 2) continue;             // malformed definition
				string res = p[0];
				if (!reslib.Contains(res)) continue;    // unknown resource
				double rate = double.Parse(p[1]);
				if (res.Length < 1 || rate < double.Epsilon) continue;  // rate <= 0
				defs.Add(new ObjectPair<string, double>(res, rate));
			}
			return defs;
		}

		/// <summary>
		/// parts that have experiments can't get their module info (what is shown in the VAB tooltip) correctly setup
		/// because the ExperimentInfo database isn't available at loading time, so we recompile their info manually.
		/// This returns true if the AvailablePart of the parameter module needs to be recompiled.
		/// </summary>
		public static bool CompileModuleInfo(PartModule modulePrefab)
		{
			if (modulePrefab is ModuleKsmExperiment ksmExperiment && !string.IsNullOrEmpty(ksmExperiment.moduleDefinition))
			{
				ExperimentModuleDefinition def = ScienceDB.GetExperimentModuleDefinition(ksmExperiment.moduleDefinition);
				if (def != null)
				{
					def.AddAvailableAtTech(ksmExperiment.part.partInfo.TechRequired);
					def.AddAvailableOnPart(modulePrefab.part);
					if (def.completeSpec == null)
					{
						def.completeSpec = def.CompileInfoForModule(ksmExperiment, true);
						def.definitionSpec = def.CompileInfoForModule(ksmExperiment, false);
					}

					return true;
				}
			}
			else if (modulePrefab is ModuleScienceExperiment stockExpModule)
			{
				ExperimentModuleDefinition def = ScienceDB.GetExperimentModuleDefinition(stockExpModule.experimentID);
				if (def != null)
				{
					def.AddAvailableAtTech(stockExpModule.part.partInfo.TechRequired);
					def.AddAvailableOnPart(modulePrefab.part);
					if (def.completeSpec == null)
						def.completeSpec = def.CompileInfoForModule(stockExpModule);
				}
			}
			else if (modulePrefab is ModuleGroundExperiment groundExpModule)
			{
				ExperimentModuleDefinition def = ScienceDB.GetExperimentModuleDefinition(groundExpModule.experimentId);
				if (def != null)
				{
					def.AddAvailableAtTech(groundExpModule.part.partInfo.TechRequired);
					def.AddAvailableOnPart(modulePrefab.part);
					if (def.completeSpec == null)
						def.completeSpec = def.CompileInfoForModule(groundExpModule);
				}
			}
			else if (modulePrefab is ModuleRobotArmScanner armScanner)
			{
				foreach (ExperimentInfo expInfo in ScienceDB.ExperimentInfos)
				{
					// small ROCS can be taken on EVA at any tech level
					if (!expInfo.IsROC || expInfo.ROCDef.smallRoc)
						continue;

					foreach (ExperimentModuleDefinition expModuleDefs in expInfo.ExperimentModuleDefinitions)
					{
						expModuleDefs.AddAvailableAtTech(armScanner.part.partInfo.TechRequired);
					}
				}
			}

			return false;
		}

		public static string CompileModuleInfo(ModuleKsmExperiment module, B9PartSwitch.SubtypeWrapper subtype, B9PartSwitch.ModuleModifierWrapper moduleModifier)
		{
			string moduleDefinition = Lib.ConfigValue(moduleModifier.DataNode, nameof(ModuleKsmExperiment.moduleDefinition), string.Empty);
			if (moduleDefinition.Length > 0)
			{
				ExperimentModuleDefinition def = ScienceDB.GetExperimentModuleDefinition(moduleDefinition);
				if (def != null)
				{
					if (!string.IsNullOrEmpty(subtype.TechRequired))
					{
						def.AddAvailableAtTech(subtype.TechRequired);
					}
					else
					{
						def.AddAvailableAtTech(module.part.partInfo.TechRequired);
					}

					def.AddAvailableOnPart(module.part);

					if (def.completeSpec == null)
					{
						def.completeSpec = def.CompileInfoForModule(module, true);
						def.definitionSpec = def.CompileInfoForModule(module, false);
					}
						
					return def.ModuleInfo();
				}
			}
			return string.Empty;
		}

		public void PopulateSpecIfUndefined()
		{
			if (completeSpec != null)
				return;

			if (Info.ExperimentId == "asteroidSample")
			{
				completeSpec = CompileInfoAsteroidSample();
			}
			else if (Info.IsROC)
			{
				completeSpec = CompileInfoROC();
			}
			else
			{
				completeSpec = CompileInfoDefault();
			}
		}

		private string CompileInfoForModule(ModuleKsmExperiment module, bool completeWithExperimentInfo)
		{
			sb.Clear();

			if (completeWithExperimentInfo)
				sb.AppendInfo("Base value", Lib.HumanReadableScience(Info.ScienceCap, true, true));

			double expSize = Info.DataSize;
			if (Info.SampleMass == 0.0)
			{
				sb.AppendInfo(Local.Module_Experiment_Specifics_info1, Lib.HumanReadableDataSize(expSize)); //"Data size"
				if (DataRate > 0)
				{
					sb.AppendInfo(Local.Module_Experiment_Specifics_info2, Lib.HumanReadableDataRate(DataRate));
					sb.AppendInfo(Local.Module_Experiment_Specifics_info3, Lib.HumanReadableDuration(Duration));
				}
			}
			else
			{
				sb.AppendInfo(Local.Module_Experiment_Specifics_info4, Lib.HumanReadableSampleSize(expSize));//"Sample size"
				sb.AppendInfo(Local.Module_Experiment_Specifics_info5, Lib.HumanReadableMass(Info.SampleMass));//"Sample mass"
				if (Info.SampleMass > 0.0 && !SampleCollecting)
					sb.AppendInfo(Local.Module_Experiment_Specifics_info6, Samples.ToString("F2"));//"Samples"
				if (Duration > 0)
					sb.AppendInfo(Local.Module_Experiment_Specifics_info7_sample, Lib.HumanReadableDuration(Duration));
			}

			if (completeWithExperimentInfo && Info.IncludedExperiments.Count > 0)
			{
				sb.AppendKSPNewLine();
				sb.AppendKSPLine(Lib.Color("Included experiments:", Lib.Kolor.Cyan, true));
				List<string> includedExpInfos = new List<string>();
				ExperimentInfo.GetIncludedExperimentTitles(Info, includedExpInfos);
				foreach (string includedExp in includedExpInfos)
				{
					sb.AppendList(includedExp);
				}
			}

			if (completeWithExperimentInfo)
			{
				List<string> situations = Info.AvailableSituations();
				if (situations.Count > 0)
				{
					sb.AppendKSPNewLine();
					sb.AppendKSPLine(Lib.Color(Local.Module_Experiment_Specifics_Situations, Lib.Kolor.Cyan, true));//"Situations:"
					foreach (string s in situations)
						sb.AppendList(Lib.Bold(s));
				}
			}

			if (completeWithExperimentInfo && Info.ExpBodyConditions.HasConditions)
			{
				sb.AppendKSPNewLine();
				sb.AppendKSPLine(Info.ExpBodyConditions.ConditionsToString());
			}

			if (RequiredEC > 0 || Resources.Count > 0)
			{
				sb.AppendKSPNewLine();
				sb.AppendKSPLine(Lib.Color(Local.Module_Experiment_Specifics_info8, Lib.Kolor.Cyan, true));//"Needs:"
				if (RequiredEC > 0)
					sb.AppendInfo(PartResourceLibrary.Instance.GetDefinition(PartResourceLibrary.ElectricityHashcode).displayName, Lib.HumanReadableRate(RequiredEC));
				foreach (var p in Resources)
					sb.AppendInfo(PartResourceLibrary.Instance.GetDefinition(p.Key).displayName, Lib.HumanReadableRate(p.Value)); 
			}

			if (CrewOperate)
			{
				sb.AppendKSPNewLine();
				sb.AppendInfo(Local.Module_Experiment_Specifics_info11, CrewOperate.Info());
			}

			if (Requirements.Requires.Length > 0)
			{
				sb.AppendKSPNewLine();
				sb.AppendKSPLine(Lib.Color(Local.Module_Experiment_Requires, Lib.Kolor.Cyan, true));//"Requires:"
				foreach (RequireDef req in Requirements.Requires)
					sb.AppendInfo(Lib.BuildString("• <b>", ReqName(req.require), "</b>"), ReqValueFormat(req.require, req.value));
			}

			if(!module.allow_shrouded)
			{
				sb.AppendKSPNewLine();
				sb.AppendKSPLine(Lib.Bold("Unavailable while shrouded"));
			}

			return sb.ToString();
		}

		private string CompileInfoForModule(ModuleScienceExperiment module)
		{
			sb.Clear();
			sb.AppendInfo("Base value", Lib.HumanReadableScience(Info.ScienceCap, true, true));
			sb.AppendInfo(Local.Experimentinfo_Datasize, Lib.HumanReadableDataSize(Info.DataSize));

			if (module.xmitDataScalar < Science.maxXmitDataScalarForSample)
			{
				sb.AppendKSPNewLine();
				sb.AppendKSPLine(Local.Experimentinfo_generatesample);//Will generate a sample.
				sb.AppendInfo(Local.Experimentinfo_Samplesize, Lib.HumanReadableSampleSize(Info.DataSize));
			}

			sb.AppendKSPNewLine();
			sb.AppendKSPLine(Lib.Color(Local.Module_Experiment_Specifics_Situations, Lib.Kolor.Cyan, true));//"Situations:"

			foreach (string s in Info.AvailableSituations())
				sb.AppendList(Lib.Bold(s));

			sb.AppendKSPNewLine();
			sb.Append(module.GetInfo());

			return sb.ToString();
		}

		private string CompileInfoForModule(ModuleGroundExperiment module)
		{
			sb.Clear();
			sb.AppendInfo("Base value", Lib.HumanReadableScience(Info.ScienceCap, true, true));
			sb.AppendInfo(Local.Experimentinfo_Datasize, Lib.HumanReadableDataSize(Info.DataSize));
			sb.AppendKSPNewLine();
			sb.Append(module.GetInfo());
			return sb.ToString();
		}

		private string CompileInfoAsteroidSample()
		{
			sb.Clear();
			sb.AppendInfo("Base value", Lib.HumanReadableScience(Info.ScienceCap, true, true));
			sb.AppendKSPNewLine();
			sb.AppendKSPLine(Local.Experimentinfo_Asteroid);//"Asteroid samples can be taken by kerbals on EVA"
			sb.AppendKSPNewLine();
			sb.AppendInfo(Local.Experimentinfo_Samplesize, Lib.HumanReadableSampleSize(Info.DataSize));
			sb.AppendInfo(Local.Experimentinfo_Samplemass, Lib.HumanReadableMass(Info.DataSize * Settings.AsteroidSampleMassPerMB));
			return sb.ToString();
		}

		private string CompileInfoROC()
		{
			sb.Clear();
			sb.AppendKSPLine(Lib.Color(Info.ROCDef.displayName, Lib.Kolor.Cyan, true));
			sb.AppendKSPNewLine();
			sb.AppendInfo("Base value", Lib.HumanReadableScience(Info.ScienceCap, true, true));

			sb.AppendKSPLine("- " + Local.Experimentinfo_scannerarm);//Analyse with a scanner arm
			sb.AppendInfo("  " + Local.Experimentinfo_Datasize, Lib.HumanReadableDataSize(Info.DataSize));

			if (Info.ROCDef.smallRoc)
			{
				sb.AppendKSPLine("- " + Local.Experimentinfo_smallRoc);//Collectable on EVA as a sample"
				sb.AppendInfo("  " + Local.Experimentinfo_Samplesize, Lib.HumanReadableSampleSize(Info.DataSize));
			}
			else
			{
				sb.AppendKSPLine("- " + Local.Experimentinfo_smallRoc2); //Can't be collected on EVA
			}

			foreach (RocCBDefinition rocBody in Info.ROCDef.myCelestialBodies)
			{
				CelestialBody body = FlightGlobals.GetBodyByName(rocBody.name);
				sb.AppendKSPNewLine();
				sb.AppendKSPLine(Lib.Color(Local.Experimentinfo_smallRoc3.Format(body.bodyName), Lib.Kolor.Cyan, true));//"Found on <<1>>'s :"
				foreach (string biome in rocBody.biomes)
				{
					sb.AppendList(ScienceUtil.GetBiomedisplayName(body, biome));
				}
			}

			return sb.ToString();
		}

		private string CompileInfoDefault()
		{
			sb.Clear();
			sb.AppendKSPNewLine();
			sb.AppendInfo("Base value", Lib.HumanReadableScience(Info.ScienceCap, true, true));
			sb.AppendInfo(Local.Experimentinfo_Datasize, Lib.HumanReadableDataSize(Info.DataSize));

			sb.AppendKSPNewLine();
			sb.AppendKSPLine(Lib.Color(Local.Module_Experiment_Specifics_Situations, Lib.Kolor.Cyan, true));//"Situations:"

			foreach (string s in Info.AvailableSituations())
				sb.AppendList(Lib.Bold(s));

			return sb.ToString();
		}

		private void AddAvailableOnPart(Part part)
		{
			string title = Lib.RemoveTags(part.partInfo.title);
			if (part.partInfo.TechHidden || string.IsNullOrEmpty(title))
				return;

			if (string.IsNullOrEmpty(AvailableOnPartsList))
			{
				AvailableOnPartsList = Lib.BuildString(
					Lib.Color("Available on part(s) :", Lib.Kolor.Cyan, true), "\n• ", Lib.Ellipsis(title, 25));
			}
			else
			{
				AvailableOnPartsList = Lib.BuildString(AvailableOnPartsList, "\n• ", Lib.Ellipsis(title, 25));
			}
		}

		private void AddAvailableAtTech(string techRequired)
		{
			if (!string.IsNullOrEmpty(techRequired))
				availableAtTechs.Add(techRequired);
		}
	}
}
