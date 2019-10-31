using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;
using System.Text;
using System.Reflection;


/*
 *
EXPERIMENT_DEFINITION:NEEDS[FeatureScience]
{
	id = kerbalism_FLOAT
	title = FLOAT
	baseValue = 75
	scienceCap = 75
	dataScale = 1

	requireAtmosphere = False
	situationMask = 16
	biomeMask = 0

	KERBALISM_EXPERIMENT
	{
		Description = 

		// file size in Mb. For samples, 1 slot = 1024Mb. Override stock "dataScale".
		DataSize = 1500				

		// maximum science points that can be gained. Override stock "scienceCap"
		TotalValue = 75				

		// if true, science points are credited only when the full DataSize is retrieved. If false, science points are credited continuously.
		CreditWhenComplete = false	

		// science value of the first complete file/sample retrieved. If not defined, will be set equal to TotalValue. Override stock "baseValue"
		FirstRunValue = 50

		// how many times DataSize needs to be collected to get the TotalValue.
		// If not defined, default is TotalScienceValue / FirstScienceValue, rounded up.
		// The value of each run after the first one is :
		// MIN(FirstRunValue ; (ScienceValueLeft * SQRT(TimesCollectable - TimesCollected)) / (TimesCollectable - TimesCollected))
		// In this example : run 1 = 50 pts, run 2 = 17.7 pts, run 3 = 7.3 pts
		TimesCollectable = 3		

		// sample mass in tons. if undefined or 0, the experiment produce a file
		SampleMass = 0.0

		// body restrictions, multiple lines allowed, can use BodyAllowed / BodyNotAllowed with either a body name or the following keywords :
		// Atmospheric, NonAtmospheric, Gaseous, Solid, Oceanic, HomeBody, HomeBodyAndMoons, Planets, Moons, Suns
		BodyAllowed = HomeBodyAndMoons	
										
		// Situation values will override the stock situationMask/biomeMask values
		Situation = FlyingLow@Biomes 
		Situation = FlyingHigh
	}
}

EXPERIMENT_DEFINITION
{
	id = kerbalism_FLOAT
	title = FLOAT
	baseValue = 75
	scienceCap = 75
	dataScale = 1

	KERBALISM_EXPERIMENT
	{		
		// sample mass in tons. if undefined or 0, the experiment produce a file
		SampleMass = 0.0

		// Body restrictions, multiple lines allowed (just don't use confictiong combinations).
		// Can be "BodyAllowed = X" / "BodyNotAllowed = X" with either a body name or the following keywords :
		// Atmospheric, NonAtmospheric, Gaseous, Solid, Oceanic, HomeBody, HomeBodyAndMoons, Planets, Moons, Suns
		// Example : all bodies that have an atmosphere excepted Duna and all suns (suns are atmospheric bodies)
		BodyAllowed = Atmospheric
		BodyNotAllowed = Suns
		BodyNotAllowed = Duna

		// Optional : virtual biomes are hardcoded special biomes that will generate individual subjects
		// Virtual biomes are enabled per situation and can't be combined with normal body biomes
		// When using multiple virtual biomes that may be available at the same time, the priority is hardcoded (see list)
		// Note that virtual biomes experiments are incompatible with the contract system, you may get contracts that are not doable.
		// Multiple lines allowed, format is `VirtualBiome = VirtualBiomeKeyword`. Valid keywords are :
		// - NoBiome : create a "biome-agostic" situation available when no virtual biome is available.
		// - NorthernHemisphere : available when on/over the body north hemisphere. Lowest priority. Implemented DMOS contracts compatibility.
		// - SouthernHemisphere : available when on/over the body south hemisphere. Lowest priority. Implemented DMOS contracts compatibility.
		// - InnerBelt : available when inside the body inner radiation belt
		// - OuterBelt : available when inside the body outer radiation belt
		// - Magnetosphere : available when inside the body magnetosphere. Lower priority than the belt biomes.
		// - Interstellar : available when in a sun SOI and outside the heliopause
		// - Reentry : available when descending rapidly in atmosphere over mach 5 while apoapsis is outside the atmosphere. 
		// Example : these 4 subjects will be available for every situation defined with `@VirtualBiomes`
		VirtualBiome = NoBiome
		VirtualBiome = InnerBelt
		VirtualBiome = OuterBelt
		VirtualBiome = Magnetosphere
										
		// Optional : situation values will create-or-replace the stock situationMask/biomeMask values.
		// Multiple lines allowed, format is `Situation = SituationKeyword`, and append `@Biomes` or `@VirtualBiomes` to allow biomes or virtual biomes
		// Valid situation keyword :
		// - SrfLanded, SrfSplashed, FlyingLow, FlyingHigh, InSpaceLow, InSpaceHigh
		// - Surface : valid when landed or splashed, uses the SrfLanded science value. Incompatible with SrfLanded/SrfSplashed.
		// - Flying : valid when in atmosphere, uses the FlyingHigh science value. Incompatible with FlyingLow/FlyingHigh.
		// - Space : valid when in space, uses the InSpaceLow science value. Incompatible with InSpaceLow/InSpaceHigh.
		// - BodyGlobal : always valid, uses the InSpaceLow science value. Incompatible with all other situations.
		// Example : normal body biomes for the landed+splashed situation and flying low, no biomes for flying high, and the virtual biomes for the space low+high situation
		Situation = Landed@Biomes
		Situation = FlyingLow@Biomes 
		Situation = FlyingHigh
		Situation = Space@VirtualBiomes
	}
}


*/

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

		public bool IsROC { get; private set; }

		public bool HasDBSubjects { get; private set; }

		public bool IgnoreBodyRestrictions { get; private set; }

		public ExperimentInfo(ScienceExperiment stockDef, ConfigNode expInfoNode)
		{
			// if we have a custom "KERBALISM_EXPERIMENT" definition for the experiment, load it, else just use an empty node to avoid nullrefs
			if (expInfoNode == null) expInfoNode = new ConfigNode();

			this.stockDef = stockDef;
			ExperimentId = stockDef.id;

			// We have some custom handling for breaking ground ROC experiments
			IsROC = ExperimentId.StartsWith("ROCScience");

			if (IsROC)
				Title = "ROC: " + stockDef.experimentTitle;	// group ROC together in the science archive (sorted by Title)
			else
				Title = stockDef.experimentTitle;

#if KSP15_16
			DataSize = this.stockDef.baseValue * this.stockDef.dataScale;
#else
			// A new bool field was added in 1.7 for serenity : applyScienceScale
			// if not specified, the default is `true`, which is the case for all non-serenity science defs
			// serenity ground experiments and ROCs have applyScienceScale = false.
			// for ground experiment, baseValue = science generated per hour
			// for ROC experiments, it doesn't change anything because they are all configured with baseValue = scienceCap
			if (this.stockDef.applyScienceScale)
				DataSize = this.stockDef.baseValue * this.stockDef.dataScale;
			else
				DataSize = this.stockDef.scienceCap * this.stockDef.dataScale;
#endif

			SampleMass = Lib.ConfigValue(expInfoNode, "SampleMass", 0.0);
			IsSample = SampleMass > 0.0;
			if (IsSample)
			{
				// make sure we don't produce NaN values down the line because of odd/wrong configs
				if (DataSize <= 0.0)
				{
					Lib.Log("ERROR: " + ExperimentId + " has DataSize=" + DataSize + ", your configuration is broken!");
					DataSize = 1.0;
				}
				MassPerMB = SampleMass / DataSize;
			}
			else
			{
				MassPerMB = 0.0;
			}

			if (IsROC)
			{
				// Parse the ROC definition name to find which body it's available on
				// This rely on the ROC definitions having the body name in the ExperimentId
				ConfigNode ROCBodyNode = new ConfigNode();
				foreach (CelestialBody body in FlightGlobals.Bodies)
				{
					if (ExperimentId.IndexOf(body.name, StringComparison.OrdinalIgnoreCase) != -1)
					{
						ROCBodyNode.AddValue("BodyAllowed", body.name);
						break;
					}
				}
				ExpBodyConditions = new BodyConditions(ROCBodyNode);
			}
			else
			{
				ExpBodyConditions = new BodyConditions(expInfoNode);
			}

			foreach (string virtualBiomeStr in expInfoNode.GetValues("VirtualBiome"))
			{
				if (Enum.IsDefined(typeof(VirtualBiome), virtualBiomeStr))
				{
					VirtualBiomes.Add((VirtualBiome)Enum.Parse(typeof(VirtualBiome), virtualBiomeStr));
				}
				else
				{
					Lib.Log("ERROR : Experiment definition `{0}` has unknown VirtualBiome={1}", ExperimentId, virtualBiomeStr);
				}
			}

			IgnoreBodyRestrictions = Lib.ConfigValue(expInfoNode, "IgnoreBodyRestrictions", false);

			uint situationMask = 0;
			uint biomeMask = 0;
			uint virtualBiomeMask = 0;
			// if defined, override stock situation / biome mask
			if (expInfoNode.HasValue("Situation"))
			{
				foreach (string situation in expInfoNode.GetValues("Situation"))
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
						Lib.Log("WARNING : Experiment definition `{0}` has unknown situation : `{1}`", ExperimentId, sitAtBiome[0]);
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
				Lib.Log("Experiment definition `{0}` : `0` situationMask is unsupported, patching to `BodyGlobal`", ExperimentId);
				situationMask = ScienceSituation.BodyGlobal.BitValue();
				HasDBSubjects = false;
			}
			else
			{
				HasDBSubjects = !Lib.ConfigValue(expInfoNode, "IsGeneratingSubjects", false);
			}

			string error;
			uint stockSituationMask;
			uint stockBiomeMask;
			if (!ScienceSituationUtils.ValidateSituationBitMask(ref situationMask, biomeMask, out stockSituationMask, out stockBiomeMask, out error))
			{
				Lib.Log("ERROR : Experiment definition `{0}` is incorrect :\n{1}", ExperimentId, error);
			}

			SituationMask = situationMask;
			BiomeMask = biomeMask;
			VirtualBiomeMask = virtualBiomeMask;
			stockDef.situationMask = stockSituationMask;
			stockDef.biomeMask = stockBiomeMask;

			// patch experiment prefabs and get module infos.
			// must be done at the end of the ctor so everything in "this" is properly setup
			SetupPrefabs();
		}

		/// <summary>
		/// parts that have experiments can't get their module info (what is shown in the VAB tooltip) correctly setup
		/// because the ExperimentInfo database isn't available at loading time, so we recompile their info manually.
		/// </summary>
		public void SetupPrefabs()
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

				bool partHasExperimentModule = false;

				foreach (PartModule module in ap.partPrefab.Modules)
				{
					if (module is Experiment expModule)
					{
						if (expModule.experiment_id == ExperimentId)
						{
							expModule.ExpInfo = this; // works inside the ExperimentInfo ctor, but make sure it's called at the end of it.
							partHasExperimentModule = true;

							// get module info for the ExperimentInfo, once
							if (string.IsNullOrEmpty(ModuleInfo))
							{
								ModuleInfo = Lib.Color(Title, Lib.Kolor.Cyan, true);
								ModuleInfo += "\n";
								ModuleInfo += expModule.GetInfo();
							}
						}
					}

					if (!string.IsNullOrEmpty(ModuleInfo))
						continue;

					if (module is ModuleScienceExperiment stockExpModule)
					{
						if (stockExpModule.experimentID == ExperimentId)
						{
							ModuleInfo = Lib.Color(Title, Lib.Kolor.Cyan, true);
							ModuleInfo += "\nData size: ";
							ModuleInfo += Lib.HumanReadableDataSize(DataSize);
							if (stockExpModule.xmitDataScalar < Science.maxXmitDataScalarForSample)
							{
								ModuleInfo += "\nWill generate a sample.";
								ModuleInfo += "\nSample size: ";
								ModuleInfo += Lib.HumanReadableSampleSize(DataSize);
							}
							ModuleInfo += "\n\n";
							ModuleInfo += Lib.Color("Situations:\n", Lib.Kolor.Cyan, true);

							foreach (string s in AvailableSituations())
								ModuleInfo += Lib.BuildString("• <b>", s, "</b>\n");

							ModuleInfo += "\n";
							ModuleInfo += stockExpModule.GetInfo();
						}
					}

#if !KSP15_16
					else if (module is ModuleGroundExperiment groundExpModule)
					{
						if (groundExpModule.experimentId == ExperimentId)
						{
							ModuleInfo = Lib.Color(Title, Lib.Kolor.Cyan, true);
							ModuleInfo += "\nData size: ";
							ModuleInfo += Lib.HumanReadableDataSize(DataSize);
							ModuleInfo += "\n\n";
							ModuleInfo += groundExpModule.GetInfo();
						}
					}
#endif
				}

				// special cases
				if (ExperimentId == "asteroidSample")
				{
					ModuleInfo = "Asteroid samples can be taken by kerbals on EVA";
					ModuleInfo += "\nSample size: ";
					ModuleInfo += Lib.HumanReadableSampleSize(DataSize);
					ModuleInfo += "\nSample mass: ";
					ModuleInfo += Lib.HumanReadableMass(DataSize * Settings.AsteroidSampleMassPerMB);
				}
#if !KSP15_16
				else if (IsROC)
				{
					string rocType = ExperimentId.Substring(ExperimentId.IndexOf('_') + 1);
					ROCDefinition rocDef = ROCManager.Instance.rocDefinitions.Find(p => p.type == rocType);
					if (rocDef != null)
					{
						ModuleInfo = Lib.Color(rocDef.displayName, Lib.Kolor.Cyan, true);
						ModuleInfo += "\n- Analyse with a scanner arm";
						ModuleInfo += "\n  Data size: ";
						ModuleInfo += Lib.HumanReadableDataSize(DataSize);

						if (rocDef.smallRoc)
						{
							ModuleInfo += "\n- Collectable on EVA as a sample";
							ModuleInfo += "\nSample size: ";
							ModuleInfo += Lib.HumanReadableSampleSize(DataSize);
						}
						else
						{
							ModuleInfo += "\n- Can't be collected on EVA";
						}

						foreach (RocCBDefinition body in rocDef.myCelestialBodies)
						{
							ModuleInfo += Lib.Color("\n\nFound on " + body.name + "'s :", Lib.Kolor.Cyan, true);
							foreach (string biome in body.biomes)
							{
								ModuleInfo += "\n- ";
								ModuleInfo += biome;
							}
						}
					}
				}
#endif


				if (partHasExperimentModule && !ap.name.StartsWith("kerbalEVA"))
				{
					ap.moduleInfos.Clear();
					ap.resourceInfos.Clear();
					try
					{
						Lib.ReflectionCall(PartLoader.Instance, "CompilePartInfo", new Type[] { typeof(AvailablePart), typeof(Part) }, new object[] { ap, ap.partPrefab });
					}
					catch (Exception ex)
					{
						Lib.Log("Could not patch the moduleInfo for part " + ap.name + " - " + ex.Message + "\n" + ex.StackTrace);
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
						result.Add(Lib.BuildString(situation.Title(), " (biomes)"));
					}
					else if (situation.IsVirtualBiomesRelevantForExperiment(this))
					{
						foreach (VirtualBiome biome in VirtualBiomes)
						{
							result.Add(Lib.BuildString(situation.Title(), " (", biome.Title(),")"));
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
					ExpInfoSB.Append(Lib.Color("Bodies allowed:\n", Lib.Kolor.Cyan, true));
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
					ExpInfoSB.Append(Lib.Color("Bodies not allowed:\n", Lib.Kolor.Cyan, true));
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
				public override string Title => "atmospheric";
			}

			private class NonAtmospheric : BodyCondition
			{
				public override bool TestCondition(CelestialBody body) => !body.atmosphere;
				public override string Title => "non-atmospheric";
			}

			private class Gaseous : BodyCondition
			{
				public override bool TestCondition(CelestialBody body) => body.hasSolidSurface;
				public override string Title => "gaseous";
			}

			private class Solid : BodyCondition
			{
				public override bool TestCondition(CelestialBody body) => !body.hasSolidSurface;
				public override string Title => "solid";
			}

			private class Oceanic : BodyCondition
			{
				public override bool TestCondition(CelestialBody body) => body.ocean;
				public override string Title => "oceanic";
			}

			private class HomeBody : BodyCondition
			{
				public override bool TestCondition(CelestialBody body) => body.isHomeWorld;
				public override string Title => "home body";
			}

			private class HomeBodyAndMoons : BodyCondition
			{
				public override bool TestCondition(CelestialBody body) => body.isHomeWorld || body.referenceBody.isHomeWorld;
				public override string Title => "home body and its moons";
			}

			private class Planets : BodyCondition
			{
				public override bool TestCondition(CelestialBody body) => !Lib.IsSun(body) && Lib.IsSun(body.referenceBody);
				public override string Title => "planets";
			}

			private class Moons : BodyCondition
			{
				public override bool TestCondition(CelestialBody body) => !Lib.IsSun(body) && !Lib.IsSun(body.referenceBody);
				public override string Title => "moons";
			}

			private class Suns : BodyCondition
			{
				public override bool TestCondition(CelestialBody body) => Lib.IsSun(body);
				public override string Title => "suns";
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
} // KERBALISM

