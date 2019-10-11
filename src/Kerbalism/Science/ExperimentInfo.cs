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
		// Example : only planets and moons that aren't in the home system (kerbin, mun & minmus in stock)
		BodyAllowed = HomeBodyAndMoons
		BodyNotAllowed = Suns
										
		// Optional : situation values will create-or-replace the stock situationMask/biomeMask values.
		// Multiple lines allowed, format is "Situation = SituationKeyword", and append "@Biomes" to allow biomes
		// Valid keywords : SrfLanded, SrfSplashed, FlyingLow, FlyingHigh, InSpaceLow, InSpaceHigh
		// There are other situations, but they aren't implemented properly yet.
		Situation = SrfLanded@Biomes
		Situation = SrfSplashed@Biomes
		Situation = FlyingLow@Biomes 
		Situation = FlyingHigh
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

		/// <summary> max data amount for the experiment </summary>
		public double SampleMass { get; private set; }

		public BodyConditions ExpBodyConditions { get; private set; }

		/// <summary> max data amount for the experiment, equal to stockDef.baseValue * stockDef.dataScale</summary>
		public double DataSize { get; private set; }

		public bool IsSample => SampleMass > 0.0;

		public double MassPerMB => SampleMass / DataSize;

		public double DataScale => stockDef.dataScale;

		/// <summary> experiment situation mask </summary>
		public uint SituationMask => stockDef.situationMask;

		/// <summary> experiment biome mask </summary>
		public uint BiomeMask => stockDef.biomeMask;

		public double ScienceCap => stockDef.scienceCap * HighLogic.CurrentGame.Parameters.Career.ScienceGainMultiplier;

		public ExperimentInfo(ScienceExperiment stockDef, ConfigNode expInfoNode)
		{
			this.stockDef = stockDef;
			ExperimentId = stockDef.id;

			// deduce short name for the experiment
			Title = this.stockDef != null ? this.stockDef.experimentTitle : Lib.UppercaseFirst(ExperimentId);

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
			// if we have a custom "KERBALISM_EXPERIMENT" definition for the experiment, load it, else just use an empty node to avoid nullrefs
			if (expInfoNode == null) expInfoNode = new ConfigNode();

			SampleMass = Lib.ConfigValue(expInfoNode, "SampleMass", 0.0);
			ExpBodyConditions = new BodyConditions(expInfoNode);

			// if defined, override stock situation / biome mask
			if (expInfoNode.HasValue("Situation"))
			{
				uint situationMask = 0;
				uint biomeMask = 0;
				foreach (string situation in expInfoNode.GetValues("Situation"))
				{
					string[] sitAtBiome = situation.Split(new char[] { '@' }, StringSplitOptions.RemoveEmptyEntries);
					if (sitAtBiome.Length == 0 || sitAtBiome.Length > 2)
						continue;

					ScienceSituation scienceSituation = ScienceSituationUtils.ScienceSituationDeserialize(sitAtBiome[0]);

					if (scienceSituation != ScienceSituation.None)
					{
						situationMask += scienceSituation.BitValue();

						if (sitAtBiome.Length == 2 && sitAtBiome[1].Equals("Biomes", StringComparison.OrdinalIgnoreCase))
						{
							biomeMask += scienceSituation.BitValue();
						}
					}
				}
				stockDef.situationMask = situationMask;
				stockDef.biomeMask = biomeMask;
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
					if (situation.IsBiomesRelevantForExperiment(this))
						result.Add(Lib.BuildString(situation.Title(), " (biomes)"));
					else
						result.Add(situation.Title());
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
				public override string Title => "home body and moons";
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

