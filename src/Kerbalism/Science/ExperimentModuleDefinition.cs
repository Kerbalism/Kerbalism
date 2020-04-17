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
		private Specifics spec;

		public ExperimentInfo Info { get; private set; }

		public string Name { get; private set; }

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

		public string ModuleInfo(bool includeDescription = true)
		{
			return spec.Info(includeDescription ? Info.Description : string.Empty);
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
					def.availableAtTechs.Add(ksmExperiment.part.partInfo.TechRequired);

					if (def.spec == null)
						def.CompileSpecificsForModule(ksmExperiment);

					return true;
				}
			}
			else if (modulePrefab is ModuleScienceExperiment stockExpModule)
			{
				ExperimentModuleDefinition def = ScienceDB.GetExperimentModuleDefinition(stockExpModule.experimentID);
				if (def != null)
				{
					def.availableAtTechs.Add(stockExpModule.part.partInfo.TechRequired);

					if (def.spec == null)
						def.CompileSpecificsForModule(stockExpModule);
				}
			}
			else if (modulePrefab is ModuleGroundExperiment groundExpModule)
			{
				ExperimentModuleDefinition def = ScienceDB.GetExperimentModuleDefinition(groundExpModule.experimentId);
				if (def != null)
				{
					def.availableAtTechs.Add(groundExpModule.part.partInfo.TechRequired);

					if (def.spec == null)
						def.CompileSpecificsForModule(groundExpModule);
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
						expModuleDefs.availableAtTechs.Add(armScanner.part.partInfo.TechRequired);
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
						def.availableAtTechs.Add(subtype.TechRequired);
					}
					else
					{
						def.availableAtTechs.Add(module.part.partInfo.TechRequired);
					}

					if (def.spec == null)
						def.CompileSpecificsForModule(module);

					return def.ModuleInfo();
				}
			}
			return string.Empty;
		}

		public void PopulateSpecIfUndefined()
		{
			if (spec != null)
				return;

			if (Info.ExperimentId == "asteroidSample")
			{
				CompileSpecificsAsteroidSample();
			}
			else
			{
				CompileSpecificsDefault();
			}
		}

		private void CompileSpecificsForModule(ModuleKsmExperiment module)
		{
			spec = new Specifics();
			spec.Add("Base value", Lib.HumanReadableScience(Info.ScienceCap, true, true));

			double expSize = Info.DataSize;
			if (Info.SampleMass == 0.0)
			{
				spec.Add(Local.Module_Experiment_Specifics_info1, Lib.HumanReadableDataSize(expSize)); //"Data size"
				if (DataRate > 0)
				{
					spec.Add(Local.Module_Experiment_Specifics_info2, Lib.HumanReadableDataRate(DataRate));
					spec.Add(Local.Module_Experiment_Specifics_info3, Lib.HumanReadableDuration(Duration));
				}
			}
			else
			{
				spec.Add(Local.Module_Experiment_Specifics_info4, Lib.HumanReadableSampleSize(expSize));//"Sample size"
				spec.Add(Local.Module_Experiment_Specifics_info5, Lib.HumanReadableMass(Info.SampleMass));//"Sample mass"
				if (Info.SampleMass > 0.0 && !SampleCollecting)
					spec.Add(Local.Module_Experiment_Specifics_info6, Samples.ToString("F2"));//"Samples"
				if (Duration > 0)
					spec.Add(Local.Module_Experiment_Specifics_info7_sample, Lib.HumanReadableDuration(Duration));
			}

			if (Info.IncludedExperiments.Count > 0)
			{
				spec.Add(string.Empty);
				spec.Add(Lib.Color("Included experiments:", Lib.Kolor.Cyan, true));
				List<string> includedExpInfos = new List<string>();
				ExperimentInfo.GetIncludedExperimentTitles(Info, includedExpInfos);
				foreach (string includedExp in includedExpInfos)
				{
					spec.Add("• " + includedExp);
				}
			}

			List<string> situations = Info.AvailableSituations();
			if (situations.Count > 0)
			{
				spec.Add(string.Empty);
				spec.Add(Lib.Color(Local.Module_Experiment_Specifics_Situations, Lib.Kolor.Cyan, true));//"Situations:"
				foreach (string s in situations)
					spec.Add(Lib.BuildString("• <b>", s, "</b>"));
			}

			if (Info.ExpBodyConditions.HasConditions)
			{
				spec.Add(string.Empty);
				spec.Add(Info.ExpBodyConditions.ConditionsToString());
			}

			if (RequiredEC > 0 || Resources.Count > 0)
			{
				spec.Add(string.Empty);
				spec.Add(Lib.Color(Local.Module_Experiment_Specifics_info8, Lib.Kolor.Cyan, true));//"Needs:"
				if (RequiredEC > 0)
					spec.Add(PartResourceLibrary.Instance.GetDefinition(PartResourceLibrary.ElectricityHashcode).displayName, Lib.HumanReadableRate(RequiredEC));
				foreach (var p in Resources)
					spec.Add(PartResourceLibrary.Instance.GetDefinition(p.Key).displayName, Lib.HumanReadableRate(p.Value)); 
			}

			if (CrewOperate)
			{
				spec.Add(string.Empty);
				spec.Add(Local.Module_Experiment_Specifics_info11, CrewOperate.Info());
			}

			if (Requirements.Requires.Length > 0)
			{
				spec.Add(string.Empty);
				spec.Add(Lib.Color(Local.Module_Experiment_Requires, Lib.Kolor.Cyan, true));//"Requires:"
				foreach (RequireDef req in Requirements.Requires)
					spec.Add(Lib.BuildString("• <b>", ReqName(req.require), "</b>"), ReqValueFormat(req.require, req.value));
			}

			if(!module.allow_shrouded)
			{
				spec.Add(string.Empty);
				spec.Add(Lib.Bold("Unavailable while shrouded"));
			}
		}

		private void CompileSpecificsForModule(ModuleScienceExperiment module)
		{
			spec = new Specifics();
			spec.Add("Base value", Lib.HumanReadableScience(Info.ScienceCap, true, true));
			spec.Add(Local.Experimentinfo_Datasize, Lib.HumanReadableDataSize(Info.DataSize));

			if (module.xmitDataScalar < Science.maxXmitDataScalarForSample)
			{
				spec.Add(string.Empty);
				spec.Add(Local.Experimentinfo_generatesample);//Will generate a sample.
				spec.Add(Local.Experimentinfo_Samplesize, Lib.HumanReadableSampleSize(Info.DataSize));
			}

			spec.Add(string.Empty);
			spec.Add(Lib.Color(Local.Module_Experiment_Specifics_Situations, Lib.Kolor.Cyan, true));//"Situations:"

			foreach (string s in Info.AvailableSituations())
				spec.Add(Lib.BuildString("• <b>", s, "</b>\n"));

			spec.Add(string.Empty);
			spec.Add(module.GetInfo());
		}

		private void CompileSpecificsForModule(ModuleGroundExperiment module)
		{
			spec = new Specifics();
			spec.Add("Base value", Lib.HumanReadableScience(Info.ScienceCap, true, true));
			spec.Add(Local.Experimentinfo_Datasize, Lib.HumanReadableDataSize(Info.DataSize));
			spec.Add(string.Empty);
			spec.Add(module.GetInfo());
		}

		private void CompileSpecificsAsteroidSample()
		{
			spec = new Specifics();
			spec.Add("Base value", Lib.HumanReadableScience(Info.ScienceCap, true, true));
			spec.Add(string.Empty);
			spec.Add(Local.Experimentinfo_Asteroid);//"Asteroid samples can be taken by kerbals on EVA"
			spec.Add(string.Empty);
			spec.Add(Local.Experimentinfo_Samplesize, Lib.HumanReadableSampleSize(Info.DataSize));
			spec.Add(Local.Experimentinfo_Samplemass, Lib.HumanReadableMass(Info.DataSize * Settings.AsteroidSampleMassPerMB));



		}

		private void CompileSpecificsROC()
		{
			if (!Info.IsROC)
			{
				CompileSpecificsDefault();
			}
			else
			{
				spec = new Specifics();
				spec.Add(Lib.Color(Info.ROCDef.displayName, Lib.Kolor.Cyan, true));
				spec.Add(string.Empty);
				spec.Add("Base value", Lib.HumanReadableScience(Info.ScienceCap, true, true));

				spec.Add("- " + Local.Experimentinfo_scannerarm);//Analyse with a scanner arm
				spec.Add("  " + Local.Experimentinfo_Datasize, Lib.HumanReadableDataSize(Info.DataSize));

				if (Info.ROCDef.smallRoc)
				{
					spec.Add("- " + Local.Experimentinfo_smallRoc);//Collectable on EVA as a sample"
					spec.Add("  " + Local.Experimentinfo_Samplesize, Lib.HumanReadableSampleSize(Info.DataSize));
				}
				else
				{
					spec.Add("- " + Local.Experimentinfo_smallRoc2); //Can't be collected on EVA
				}

				foreach (RocCBDefinition rocBody in Info.ROCDef.myCelestialBodies)
				{
					CelestialBody body = FlightGlobals.GetBodyByName(rocBody.name);
					spec.Add(string.Empty);
					spec.Add(Lib.Color(Local.Experimentinfo_smallRoc3.Format(body.bodyName), Lib.Kolor.Cyan, true));//"Found on <<1>>'s :"
					foreach (string biome in rocBody.biomes)
					{
						spec.Add("• " + ScienceUtil.GetBiomedisplayName(body, biome));
					}
				}
			}
		}

		private void CompileSpecificsDefault()
		{
			spec = new Specifics();
			spec.Add(string.Empty);
			spec.Add("Base value", Lib.HumanReadableScience(Info.ScienceCap, true, true));
			spec.Add(Local.Experimentinfo_Datasize, Lib.HumanReadableDataSize(Info.DataSize));

			spec.Add(string.Empty);
			spec.Add(Lib.Color(Local.Module_Experiment_Specifics_Situations, Lib.Kolor.Cyan, true));//"Situations:"

			foreach (string s in Info.AvailableSituations())
				spec.Add(Lib.BuildString("• <b>", s, "</b>\n"));

		}
	}
}
