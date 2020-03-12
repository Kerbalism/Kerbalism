using System;
using System.Collections.Generic;
using UnityEngine;
using System.Text;

namespace KERBALISM
{
	/// <summary>
	/// Stores information about an experiment_id or a subject_id
	/// Beware that subject information will be incomplete until the stock `ScienceSubject` is created in RnD
	/// </summary>
	public sealed class ExperimentInfo
	{
		public static StringBuilder ExpInfoSB = new StringBuilder();

		/// <summary> experiment definition </summary>
		private ScienceExperiment stockDef;

		/// <summary> experiment identifier </summary>
		public string ExperimentId { get; private set; }

		/// <summary> UI friendly name of the experiment </summary>
		public string Title { get; private set; }

		/// <summary> UI friendly name of the experiment </summary>
		public string Description { get; private set; }

		/// <summary> mass of a full sample </summary>
		public double SampleMass { get; private set; }

		public BodyConditions ExpBodyConditions { get; private set; }

		/// <summary> size of a full file or sample</summary>
		public double DataSize { get; private set; }

		public bool IsSample { get; private set; }

		public double MassPerMB { get; private set; }

		public double DataScale => stockDef.dataScale;

		/// <summary> situation mask </summary>
		public uint SituationMask { get; private set; }

		/// <summary> stock ScienceExperiment situation mask </summary>
		public uint StockSituationMask => stockDef.situationMask;

		/// <summary> biome mask </summary>
		public uint BiomeMask { get; private set; }

		/// <summary> stock ScienceExperiment biome mask </summary>
		public uint StockBiomeMask => stockDef.biomeMask;

		/// <summary> virtual biomes mask </summary>
		public uint VirtualBiomeMask { get; private set; }

		public List<VirtualBiome> VirtualBiomes { get; private set; } = new List<VirtualBiome>();

		public double ScienceCap => stockDef.scienceCap * HighLogic.CurrentGame.Parameters.Career.ScienceGainMultiplier;

		/// <summary> Cache the information returned by GetInfo() in the first found module using that experiment</summary>
		public string ModuleInfo { get; private set; } = string.Empty;

		/// <summary> If true, subject completion will enable the stock resource map for the corresponding body</summary>
		public bool UnlockResourceSurvey { get; private set; }

		public bool IsROC { get; private set; }

		public bool HasDBSubjects { get; private set; }

		public bool IgnoreBodyRestrictions { get; private set; }

		/// <summary> List of experiments that will be collected automatically alongside this one</summary>
		public List<ExperimentInfo> IncludedExperiments { get; private set; } = new List<ExperimentInfo>();

		/// <summary> List of experiments that will be collected automatically alongside this one</summary>
		public List<ExperimentModuleDefinition> ExperimentModuleDefinitions { get; private set; } = new List<ExperimentModuleDefinition>();

		private string[] includedExperimentsId;

		public ExperimentInfo(ScienceExperiment stockDef, ConfigNode kerbalismExperimentNode)
		{
			// if we have a custom "KERBALISM_EXPERIMENT" definition for the experiment, load it, else just use an empty node to avoid nullrefs
			if (kerbalismExperimentNode == null) kerbalismExperimentNode = new ConfigNode();

			this.stockDef = stockDef;
			ExperimentId = stockDef.id;

			// We have some custom handling for breaking ground ROC experiments
			IsROC = ExperimentId.StartsWith("ROCScience");

			if (IsROC)
				Title = "ROC: " + stockDef.experimentTitle; // group ROC together in the science archive (sorted by Title)
			else
				Title = stockDef.experimentTitle;

			Description = Lib.ConfigValue(kerbalismExperimentNode, "Description", string.Empty);

			// A new bool field was added in 1.7 for serenity : applyScienceScale
			// if not specified, the default is `true`, which is the case for all non-serenity science defs
			// serenity ground experiments and ROCs have applyScienceScale = false.
			// for ground experiment, baseValue = science generated per hour
			// for ROC experiments, it doesn't change anything because they are all configured with baseValue = scienceCap

			// Get the science points from the kerbalism node, and override the stock definition if it is set
			// This is primarily for simplification of the experiment definition process
			float SciencePoints = Lib.ConfigValue(kerbalismExperimentNode, "SciencePoints", 0f);
			if (SciencePoints > 0)
			{
				if(this.stockDef.applyScienceScale) // non-serenity
					this.stockDef.baseValue = this.stockDef.scienceCap = SciencePoints;
				else
					this.stockDef.scienceCap = SciencePoints;
			}

			// Get the desired data size from the kerbalism node, and adjust data scale if it is set
			DataSize = Lib.ConfigValue(kerbalismExperimentNode, "DataSize", 0);
			if(DataSize > 0)
			{
				if (this.stockDef.applyScienceScale)
					this.stockDef.dataScale = (float)(DataSize / this.stockDef.baseValue);
				else
					this.stockDef.dataScale = (float)(DataSize / this.stockDef.scienceCap);
			}
			else
			{
				if (this.stockDef.applyScienceScale)
					DataSize = this.stockDef.baseValue * this.stockDef.dataScale;
				else
					DataSize = this.stockDef.scienceCap * this.stockDef.dataScale;
			}

			// make sure we don't produce NaN values down the line because of odd/wrong configs
			if (DataSize <= 0.0)
			{
				Lib.Log(ExperimentId + " has DataSize=" + DataSize + ", your configuration is broken!", Lib.LogLevel.Warning);
				DataSize = 1.0;
			}

			// load the included experiments ids in a string array, we will populate the list after 
			// all ExperimentInfos are created. (can't do it here as they may not exist yet)
			includedExperimentsId = kerbalismExperimentNode.GetValues("IncludeExperiment");

			UnlockResourceSurvey = Lib.ConfigValue(kerbalismExperimentNode, "UnlockResourceSurvey", false);
			SampleMass = Lib.ConfigValue(kerbalismExperimentNode, "SampleMass", 0.0);
			IsSample = SampleMass > 0.0;
			if (IsSample)
				MassPerMB = SampleMass / DataSize;
			else
				MassPerMB = 0.0;

			// Patch stock science def restrictions as BodyAllowed/BodyNotAllowed restrictions
			if (!(kerbalismExperimentNode.HasValue("BodyAllowed") || kerbalismExperimentNode.HasValue("BodyNotAllowed")))
			{
				if (IsROC)
				{
					// Parse the ROC definition name to find which body it's available on
					// This rely on the ROC definitions having the body name in the ExperimentId
					foreach (CelestialBody body in FlightGlobals.Bodies)
					{
						if (ExperimentId.IndexOf(body.name, StringComparison.OrdinalIgnoreCase) != -1)
						{
							kerbalismExperimentNode.AddValue("BodyAllowed", body.name);
							break;
						}
					}
				}

				// parse the stock atmosphere restrictions into our own
				if (stockDef.requireAtmosphere)
					kerbalismExperimentNode.AddValue("BodyAllowed", "Atmospheric");
				else if (stockDef.requireNoAtmosphere)
					kerbalismExperimentNode.AddValue("BodyNotAllowed", "Atmospheric");
			}

			ExpBodyConditions = new BodyConditions(kerbalismExperimentNode);

			foreach (string virtualBiomeStr in kerbalismExperimentNode.GetValues("VirtualBiome"))
			{
				if (Enum.IsDefined(typeof(VirtualBiome), virtualBiomeStr))
				{
					VirtualBiomes.Add((VirtualBiome)Enum.Parse(typeof(VirtualBiome), virtualBiomeStr));
				}
				else
				{
					Lib.Log("Experiment definition `{0}` has unknown VirtualBiome={1}", Lib.LogLevel.Warning, ExperimentId, virtualBiomeStr);
				}
			}

			IgnoreBodyRestrictions = Lib.ConfigValue(kerbalismExperimentNode, "IgnoreBodyRestrictions", false);

			uint situationMask = 0;
			uint biomeMask = 0;
			uint virtualBiomeMask = 0;
			// if defined, override stock situation / biome mask
			if (kerbalismExperimentNode.HasValue("Situation"))
			{
				foreach (string situation in kerbalismExperimentNode.GetValues("Situation"))
				{
					string[] sitAtBiome = situation.Split(new char[] { '@' }, StringSplitOptions.RemoveEmptyEntries);
					if (sitAtBiome.Length == 0 || sitAtBiome.Length > 2)
						continue;

					ScienceSituation scienceSituation = ScienceSituationUtils.ScienceSituationDeserialize(sitAtBiome[0]);

					if (scienceSituation != ScienceSituation.None)
					{
						situationMask += scienceSituation.BitValue();

						if (sitAtBiome.Length == 2)
						{
							if (sitAtBiome[1].Equals("Biomes", StringComparison.OrdinalIgnoreCase))
							{
								biomeMask += scienceSituation.BitValue();
							}
							else if (sitAtBiome[1].Equals("VirtualBiomes", StringComparison.OrdinalIgnoreCase) && VirtualBiomes.Count > 0)
							{
								virtualBiomeMask += scienceSituation.BitValue();
							}
						}
					}
					else
					{
						Lib.Log("Experiment definition `{0}` has unknown situation : `{1}`", Lib.LogLevel.Warning, ExperimentId, sitAtBiome[0]);
					}
				}
			}
			else
			{
				situationMask = stockDef.situationMask;
				biomeMask = stockDef.biomeMask;
			}

			if (situationMask == 0)
			{
				Lib.Log("Experiment definition `{0}` : `0` situationMask is unsupported, patching to `BodyGlobal`", Lib.LogLevel.Message, ExperimentId);
				situationMask = ScienceSituation.BodyGlobal.BitValue();
				HasDBSubjects = false;
			}
			else
			{
				HasDBSubjects = !Lib.ConfigValue(kerbalismExperimentNode, "IsGeneratingSubjects", false);
			}

			string error;
			uint stockSituationMask;
			uint stockBiomeMask;
			if (!ScienceSituationUtils.ValidateSituationBitMask(ref situationMask, biomeMask, out stockSituationMask, out stockBiomeMask, out error))
			{
				Lib.Log("Experiment definition `{0}` is incorrect :\n{1}", Lib.LogLevel.Error, ExperimentId, error);
			}

			SituationMask = situationMask;
			BiomeMask = biomeMask;
			VirtualBiomeMask = virtualBiomeMask;
			stockDef.situationMask = stockSituationMask;
			stockDef.biomeMask = stockBiomeMask;

			foreach(var moduleDefinition in kerbalismExperimentNode.GetNodes("MODULE_DEFINITION"))
				ExperimentModuleDefinitions.Add(new ExperimentModuleDefinition(this, moduleDefinition));

			if(ExperimentModuleDefinitions.Count == 0)
			{
				Lib.Log("Experiment definition `{0}` has no MODULE_DEFINITION, generating default entry", Lib.LogLevel.Error, ExperimentId);
				ExperimentModuleDefinitions.Add(new ExperimentModuleDefinition(this));
			}
		}

		public void ParseIncludedExperiments()
		{
			foreach (string expId in includedExperimentsId)
			{
				ExperimentInfo includedInfo = ScienceDB.GetExperimentInfo(expId);
				if (includedInfo == null)
				{
					Lib.Log($"Experiment `{ExperimentId}` define a IncludedExperiment `{expId}`, but that experiment doesn't exist", Lib.LogLevel.Warning);
					continue;
				}

				// early prevent duplicated entries
				if (includedInfo.ExperimentId == ExperimentId || IncludedExperiments.Contains(includedInfo))
					continue;

				IncludedExperiments.Add(includedInfo);
			}
		}

		public static void CheckIncludedExperimentsRecursion(ExperimentInfo expInfoToCheck, List<ExperimentInfo> chainedExperiments)
		{
			List<ExperimentInfo> loopedExperiments = new List<ExperimentInfo>();
			foreach (ExperimentInfo includedExp in expInfoToCheck.IncludedExperiments)
			{
				if (chainedExperiments.Contains(includedExp))
				{
					loopedExperiments.Add(includedExp);
				}

				chainedExperiments.Add(includedExp);
			}

			foreach (ExperimentInfo loopedExperiment in loopedExperiments)
			{
				expInfoToCheck.IncludedExperiments.Remove(loopedExperiment);
				Lib.Log($"IncludedExperiment `{loopedExperiment.ExperimentId}` in experiment `{expInfoToCheck.ExperimentId}` would result in an infinite loop in the chain and has been removed", Lib.LogLevel.Warning);
			}

			foreach (ExperimentInfo includedExp in expInfoToCheck.IncludedExperiments)
			{
				CheckIncludedExperimentsRecursion(includedExp, chainedExperiments);
			}
		}

		public static void GetIncludedExperimentTitles(ExperimentInfo expinfo, List<string> includedExperiments)
		{
			foreach (ExperimentInfo includedExpinfo in expinfo.IncludedExperiments)
			{
				includedExperiments.Add(includedExpinfo.Title);
				GetIncludedExperimentTitles(includedExpinfo, includedExperiments);
			}
			includedExperiments.Sort((x, y) => x.CompareTo(y));
		}

		/// <summary>
		/// parts that have experiments can't get their module info (what is shown in the VAB tooltip) correctly setup
		/// because the ExperimentInfo database isn't available at loading time, so we recompile their info manually.
		/// </summary>
		public void CompileModuleInfos()
		{
			if (PartLoader.LoadedPartsList == null)
			{
				Lib.Log("Dazed and confused: PartLoader.LoadedPartsList == null");
				return;
			}

			foreach (AvailablePart ap in PartLoader.LoadedPartsList)
			{
				if (ap == null || ap.partPrefab == null)
				{
					Lib.Log("AvailablePart is null or without prefab: " + ap);
					continue;
				}

				foreach (PartModule module in ap.partPrefab.Modules)
				{
					if (module is ModuleKsmExperiment expModule)
					{
						if (string.IsNullOrEmpty(expModule.id))
							continue;

						var moduleDefinition = ExperimentModuleDefinitions.Find(d => d.Name == expModule.id);
						if (moduleDefinition == null)
							continue;

						expModule.ModuleDefinition = moduleDefinition;

						// get module info for the ExperimentInfo, once
						if (string.IsNullOrEmpty(ModuleInfo))
						{
							ModuleInfo = Lib.Color(Title, Lib.Kolor.Cyan, true);
							ModuleInfo += "\n";
							ModuleInfo += expModule.GetInfo();
						}
					}

					if (!string.IsNullOrEmpty(ModuleInfo))
						continue;

					if (module is ModuleScienceExperiment stockExpModule)
					{
						if (stockExpModule.experimentID == ExperimentId)
						{
							ModuleInfo = Lib.Color(Title, Lib.Kolor.Cyan, true);
							ModuleInfo += "\n" + Local.Experimentinfo_Datasize + ": ";//Data size
							ModuleInfo += Lib.HumanReadableDataSize(DataSize);
							if (stockExpModule.xmitDataScalar < Science.maxXmitDataScalarForSample)
							{
								ModuleInfo += "\n" + Local.Experimentinfo_generatesample;//Will generate a sample.
								ModuleInfo += "\n" + Local.Experimentinfo_Samplesize + " ";//Sample size:
								ModuleInfo += Lib.HumanReadableSampleSize(DataSize);
							}
							ModuleInfo += "\n\n";
							ModuleInfo += Lib.Color(Local.Experimentinfo_Situations, Lib.Kolor.Cyan, true);//"Situations:\n"

							foreach (string s in AvailableSituations())
								ModuleInfo += Lib.BuildString("• <b>", s, "</b>\n");

							ModuleInfo += "\n";
							ModuleInfo += stockExpModule.GetInfo();
						}
					}

					else if (module is ModuleGroundExperiment groundExpModule)
					{
						if (groundExpModule.experimentId == ExperimentId)
						{
							ModuleInfo = Lib.Color(Title, Lib.Kolor.Cyan, true);
							ModuleInfo += "\n" + Local.Experimentinfo_Datasize + ": ";//Data size
							ModuleInfo += Lib.HumanReadableDataSize(DataSize);
							ModuleInfo += "\n\n";
							ModuleInfo += groundExpModule.GetInfo();
						}
					}
				}

				// special cases
				if (ExperimentId == "asteroidSample")
				{
					ModuleInfo = Local.Experimentinfo_Asteroid;//"Asteroid samples can be taken by kerbals on EVA"
					ModuleInfo += "\n" + Local.Experimentinfo_Samplesize + " ";//Sample size:
					ModuleInfo += Lib.HumanReadableSampleSize(DataSize);
					ModuleInfo += "\n" + Local.Experimentinfo_Samplemass + " ";//Sample mass:
					ModuleInfo += Lib.HumanReadableMass(DataSize * Settings.AsteroidSampleMassPerMB);
				}
				else if (IsROC)
				{
					string rocType = ExperimentId.Substring(ExperimentId.IndexOf('_') + 1);
					ROCDefinition rocDef = ROCManager.Instance.rocDefinitions.Find(p => p.type == rocType);
					if (rocDef != null)
					{
						ModuleInfo = Lib.Color(rocDef.displayName, Lib.Kolor.Cyan, true);
						ModuleInfo += "\n- " + Local.Experimentinfo_scannerarm;//Analyse with a scanner arm
						ModuleInfo += "\n  " + Local.Experimentinfo_Datasize + ": ";//Data size
						ModuleInfo += Lib.HumanReadableDataSize(DataSize);

						if (rocDef.smallRoc)
						{
							ModuleInfo += "\n- " + Local.Experimentinfo_smallRoc;//Collectable on EVA as a sample"
							ModuleInfo += "\n" + Local.Experimentinfo_Samplesize + " ";//Sample size:
							ModuleInfo += Lib.HumanReadableSampleSize(DataSize);
						}
						else
						{
							ModuleInfo += "\n- " + Local.Experimentinfo_smallRoc2;//Can't be collected on EVA
						}

						foreach (RocCBDefinition body in rocDef.myCelestialBodies)
						{
							ModuleInfo += Lib.Color("\n\n" + Local.Experimentinfo_smallRoc3.Format(body.name), Lib.Kolor.Cyan, true);//"Found on <<1>>'s :"
							foreach (string biome in body.biomes)
							{
								ModuleInfo += "\n- ";
								ModuleInfo += biome;
							}
						}
					}
				}
			}
		}

		/// <summary> UI friendly list of situations available for the experiment</summary>
		public List<string> AvailableSituations()
		{
			List<string> result = new List<string>();

			foreach (ScienceSituation situation in ScienceSituationUtils.validSituations)
			{
				if (situation.IsAvailableForExperiment(this))
				{
					if (situation.IsBodyBiomesRelevantForExperiment(this))
					{
						result.Add(Lib.BuildString(situation.Title(), " ", Local.Situation_biomes));//(biomes)"
					}
					else if (situation.IsVirtualBiomesRelevantForExperiment(this))
					{
						foreach (VirtualBiome biome in VirtualBiomes)
						{
							result.Add(Lib.BuildString(situation.Title(), " (", biome.Title(), ")"));
						}
					}
					else
					{
						result.Add(situation.Title());
					}
				}
			}

			return result;
		}

		public class BodyConditions
		{
			private static string typeNamePlus = typeof(BodyConditions).FullName + "+";

			public bool HasConditions { get; private set; }
			private List<BodyCondition> bodiesAllowed = new List<BodyCondition>();
			private List<BodyCondition> bodiesNotAllowed = new List<BodyCondition>();

			public BodyConditions(ConfigNode node)
			{
				foreach (string allowed in node.GetValues("BodyAllowed"))
				{
					BodyCondition bodyCondition = ParseCondition(allowed);
					if (bodyCondition != null)
						bodiesAllowed.Add(bodyCondition);
				}

				foreach (string notAllowed in node.GetValues("BodyNotAllowed"))
				{
					BodyCondition bodyCondition = ParseCondition(notAllowed);
					if (bodyCondition != null)
						bodiesNotAllowed.Add(bodyCondition);
				}

				HasConditions = bodiesAllowed.Count > 0 || bodiesNotAllowed.Count > 0;
			}

			private BodyCondition ParseCondition(string condition)
			{
				Type type = Type.GetType(typeNamePlus + condition);
				if (type != null)
				{
					return (BodyCondition)Activator.CreateInstance(type);
				}
				else
				{
					foreach (CelestialBody body in FlightGlobals.Bodies)
						if (body.name.Equals(condition, StringComparison.OrdinalIgnoreCase))
							return new SpecificBody(body.name);
				}
				Lib.Log("Invalid BodyCondition : '" + condition + "' defined in KERBALISM_EXPERIMENT node.");
				return null;
			}

			public bool IsBodyAllowed(CelestialBody body)
			{
				bool isAllowed;

				if (bodiesAllowed.Count > 0)
				{
					isAllowed = false;
					foreach (BodyCondition bodyCondition in bodiesAllowed)
						isAllowed |= bodyCondition.TestCondition(body);
				}
				else
				{
					isAllowed = true;
				}

				foreach (BodyCondition bodyCondition in bodiesNotAllowed)
					isAllowed &= !bodyCondition.TestCondition(body);

				return isAllowed;
			}

			public string ConditionsToString()
			{
				ExpInfoSB.Length = 0;

				if (bodiesAllowed.Count > 0)
				{
					ExpInfoSB.Append(Lib.Color(Local.Experimentinfo_Bodiesallowed + "\n", Lib.Kolor.Cyan, true));//Bodies allowed:
					for (int i = bodiesAllowed.Count - 1; i >= 0; i--)
					{
						ExpInfoSB.Append(bodiesAllowed[i].Title);
						if (i > 0) ExpInfoSB.Append(", ");
					}

					if (bodiesNotAllowed.Count > 0)
						ExpInfoSB.Append("\n");
				}

				if (bodiesNotAllowed.Count > 0)
				{
					ExpInfoSB.Append(Lib.Color(Local.Experimentinfo_Bodiesnotallowed + "\n", Lib.Kolor.Cyan, true));//Bodies not allowed:
					for (int i = bodiesNotAllowed.Count - 1; i >= 0; i--)
					{
						ExpInfoSB.Append(bodiesNotAllowed[i].Title);
						if (i > 0) ExpInfoSB.Append(", ");
					}
				}

				return ExpInfoSB.ToString();
			}

			private abstract class BodyCondition
			{
				public abstract bool TestCondition(CelestialBody body);
				public abstract string Title { get; }
			}

			private class Atmospheric : BodyCondition
			{
				public override bool TestCondition(CelestialBody body) => body.atmosphere;
				public override string Title => Local.Experimentinfo_BodyCondition1;//"atmospheric"
			}

			private class NonAtmospheric : BodyCondition
			{
				public override bool TestCondition(CelestialBody body) => !body.atmosphere;
				public override string Title => Local.Experimentinfo_BodyCondition2;//"non-atmospheric"
			}

			private class Gaseous : BodyCondition
			{
				public override bool TestCondition(CelestialBody body) => body.hasSolidSurface;
				public override string Title => Local.Experimentinfo_BodyCondition3;//"gaseous"
			}

			private class Solid : BodyCondition
			{
				public override bool TestCondition(CelestialBody body) => !body.hasSolidSurface;
				public override string Title => Local.Experimentinfo_BodyCondition4;//"solid"
			}

			private class Oceanic : BodyCondition
			{
				public override bool TestCondition(CelestialBody body) => body.ocean;
				public override string Title => Local.Experimentinfo_BodyCondition5;//"oceanic"
			}

			private class HomeBody : BodyCondition
			{
				public override bool TestCondition(CelestialBody body) => body.isHomeWorld;
				public override string Title => Local.Experimentinfo_BodyCondition6;//"home body"
			}

			private class HomeBodyAndMoons : BodyCondition
			{
				public override bool TestCondition(CelestialBody body) => body.isHomeWorld || body.referenceBody.isHomeWorld;
				public override string Title => Local.Experimentinfo_BodyCondition7;//"home body and its moons"
			}

			private class Planets : BodyCondition
			{
				public override bool TestCondition(CelestialBody body) => !Lib.IsSun(body) && Lib.IsSun(body.referenceBody);
				public override string Title => Local.Experimentinfo_BodyCondition8;//"planets"
			}

			private class Moons : BodyCondition
			{
				public override bool TestCondition(CelestialBody body) => !Lib.IsSun(body) && !Lib.IsSun(body.referenceBody);
				public override string Title => Local.Experimentinfo_BodyCondition9;//"moons"
			}

			private class Suns : BodyCondition
			{
				public override bool TestCondition(CelestialBody body) => Lib.IsSun(body);
				public override string Title => Local.Experimentinfo_BodyCondition10;//"suns"
			}

			private class SpecificBody : BodyCondition
			{
				private string bodyName;
				public override bool TestCondition(CelestialBody body) => body.name == bodyName;
				public override string Title => string.Empty;
				public SpecificBody(string bodyName) { this.bodyName = bodyName; }
			}
		}
	}

	public sealed class ExperimentModuleDefinition
	{
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
			Duration = Lib.ParseDuration(Lib.ConfigValue(moduleDefinition, "Duration", "60s"));
			DataRate = Info.DataSize / Duration;
			RequiredEC = Lib.ConfigValue(moduleDefinition, "RequiredEC", 0.0);
			SampleCollecting = Lib.ConfigValue(moduleDefinition, "SampleCollecting", false);
			Samples = Lib.ConfigValue(moduleDefinition, "Samples", SampleCollecting ? 0.0 : 1.0);
			CrewOperate = new CrewSpecs(Lib.ConfigValue(moduleDefinition, "CrewOperate", string.Empty));
			Requirements = new ExperimentRequirements(Lib.ConfigValue(moduleDefinition, "Requirements", string.Empty));
			Resources = ParseResources(Lib.ConfigValue(moduleDefinition, "Resources", string.Empty));
		}

		private static readonly List<ObjectPair<string, double>> noResources = new List<ObjectPair<string, double>>();

		internal static List<ObjectPair<string, double>> ParseResources(string resources, bool logErros = false)
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
	}

} // KERBALISM

