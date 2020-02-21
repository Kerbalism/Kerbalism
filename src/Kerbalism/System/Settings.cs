using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{


    public enum UnlinkedCtrl
    {
        none,     // disable all controls
        limited,  // disable all controls except full/zero throttle and staging
        full      // do not disable controls at all
    }


	public static class Settings
	{
		private static string MODS_REQUIRED = "";
		private static string MODS_INCOMPATIBLE = "TacLifeSupport,Snacks,BackgroundResources,BackgroundProcessing,KolonyTools,USILifeSupport";
		private static string MODS_WARNING = "RemoteTech,ResearchBodies,CommNetAntennasInfo";
		private static string MODS_SCIENCE = "KEI,[x] Science!";

		public static void Parse()
		{
			var kerbalismConfigNodes = GameDatabase.Instance.GetConfigs("Kerbalism");
			if (kerbalismConfigNodes.Length < 1) return;
			ConfigNode cfg = kerbalismConfigNodes[0].config;

			// profile used
			Profile = Lib.ConfigValue(cfg, "Profile", string.Empty);

			// user-defined features
			Reliability = Lib.ConfigValue(cfg, "Reliability", false);
			Deploy = Lib.ConfigValue(cfg, "Deploy", false);
			Science = Lib.ConfigValue(cfg, "Science", false);
			SpaceWeather = Lib.ConfigValue(cfg, "SpaceWeather", false);
			Automation = Lib.ConfigValue(cfg, "Automation", false);

			// pressure
			PressureFactor = Lib.ConfigValue(cfg, "PressureFactor", 10.0);
			PressureThreshold = Lib.ConfigValue(cfg, "PressureThreshold", 0.9);

			// poisoning
			PoisoningFactor = Lib.ConfigValue(cfg, "PoisoningFactor", 0.0);
			PoisoningThreshold = Lib.ConfigValue(cfg, "PoisoningThreshold", 0.02);

			// signal
			UnlinkedControl = Lib.ConfigEnum(cfg, "UnlinkedControl", UnlinkedCtrl.none);
			DataRateMinimumBitsPerSecond = Lib.ConfigValue(cfg, "DataRateMinimumBitsPerSecond", 1.0f);
			DataRateDampingExponent = Lib.ConfigValue(cfg, "DataRateDampingExponent", 6.0f);
			DataRateDampingExponentRT = Lib.ConfigValue(cfg, "DataRateDampingExponentRT", 6.0f);
			DataRateSurfaceExperiment = Lib.ConfigValue(cfg, "DataRateSurfaceExperiment", 0.3f);
			TransmitterActiveEcFactor = Lib.ConfigValue(cfg, "TransmitterActiveEcFactor", 1.5);
			TransmitterPassiveEcFactor = Lib.ConfigValue(cfg, "TransmitterPassiveEcFactor", 0.04);

			// science
			ScienceDialog = Lib.ConfigValue(cfg, "ScienceDialog", true);
			AsteroidSampleMassPerMB = Lib.ConfigValue(cfg, "AsteroidSampleMassPerMB", 0.00002);

			// reliability
			QualityScale = Lib.ConfigValue(cfg, "QualityScale", 4.0);

			// crew level
			LaboratoryCrewLevelBonus = Lib.ConfigValue(cfg, "LaboratoryCrewLevelBonus", 0.2);
			MaxLaborartoryBonus = Lib.ConfigValue(cfg, "MaxLaborartoryBonus", 2.0);
			HarvesterCrewLevelBonus = Lib.ConfigValue(cfg, "HarvesterCrewLevelBonus", 0.1);
			MaxHarvesterBonus = Lib.ConfigValue(cfg, "MaxHarvesterBonus", 2.0);

			// misc
			EnforceCoherency = Lib.ConfigValue(cfg, "EnforceCoherency", true);
			HeadLampsCost = Lib.ConfigValue(cfg, "HeadLampsCost", 0.002);
			LowQualityRendering = Lib.ConfigValue(cfg, "LowQualityRendering", false);
			UIScale = Lib.ConfigValue(cfg, "UIScale", 1.0f);
			UIPanelWidthScale = Lib.ConfigValue(cfg, "UIPanelWidthScale", 1.0f);
			KerbalDeathReputationPenalty = Lib.ConfigValue(cfg, "KerbalDeathReputationPenalty", 100.0f);
			KerbalBreakdownReputationPenalty = Lib.ConfigValue(cfg, "KerbalBreakdownReputationPenalty", 30f);

			// save game settings presets
			LifeSupportAtmoLoss = Lib.ConfigValue(cfg, "LifeSupportAtmoLoss", 50);
			LifeSupportSurvivalTemperature = Lib.ConfigValue(cfg, "LifeSupportSurvivalTemperature", 295);
			LifeSupportSurvivalRange = Lib.ConfigValue(cfg, "LifeSupportSurvivalRange", 5);

			ComfortLivingSpace = Lib.ConfigValue(cfg, "ComfortLivingSpace", 20);
			ComfortFirmGround = Lib.ConfigValue(cfg, "ComfortFirmGround", 0.1f);
			ComfortExercise = Lib.ConfigValue(cfg, "ComfortExercise", 0.2f);
			ComfortNotAlone = Lib.ConfigValue(cfg, "ComfortNotAlone", 0.3f);
			ComfortCallHome = Lib.ConfigValue(cfg, "ComfortCallHome", 0.2f);
			ComfortPanorama = Lib.ConfigValue(cfg, "ComfortPanorama", 0.1f);
			ComfortPlants = Lib.ConfigValue(cfg, "ComfortPlants", 0.1f);

			StormFrequency = Lib.ConfigValue(cfg, "StormFrequency", 0.4f);
			StormDurationHours = Lib.ConfigValue(cfg, "StormDurationHours", 2);
			StormEjectionSpeed = Lib.ConfigValue(cfg, "StormEjectionSpeed", 0.33f);
			ShieldingEfficiency = Lib.ConfigValue(cfg, "ShieldingEfficiency", 0.9f);
			StormRadiation = Lib.ConfigValue(cfg, "StormRadiation", 5.0f);
			ExternRadiation = Lib.ConfigValue(cfg, "ExternRadiation", 0.04f);
			RadiationInSievert = Lib.ConfigValue(cfg, "RadiationInSievert", false);

			ModsIncompatible = Lib.ConfigValue(cfg, "ModsIncompatible", MODS_INCOMPATIBLE);
			ModsRequired = Lib.ConfigValue(cfg, "ModsRequired", MODS_REQUIRED);
			ModsWarning = Lib.ConfigValue(cfg, "ModsWarning", MODS_WARNING);
			ModsScience = Lib.ConfigValue(cfg, "ModsScience", MODS_SCIENCE);
			CheckForCRP = Lib.ConfigValue(cfg, "CheckForCRP", true);

			UseSamplingSunFactor = Lib.ConfigValue(cfg, "UseSamplingSunFactor", false);

			// debug / logging
			VolumeAndSurfaceLogging = Lib.ConfigValue(cfg, "VolumeAndSurfaceLogging", false);

			loaded = true;
		}

		// profile used
		public static string Profile;

		internal static List<string> IncompatibleMods()
		{
			var result = Lib.Tokenize(ModsIncompatible.ToLower(), ',');
			return result;
		}

		internal static List<string> WarningMods()
		{
			var result = Lib.Tokenize(ModsWarning.ToLower(), ',');
			if (Features.Science) result.AddRange(Lib.Tokenize(ModsScience.ToLower(), ','));
			return result;
		}

		internal static List<string> RequiredMods()
		{
			var result = Lib.Tokenize(ModsRequired, ',');
			return result;
		}

		// name of profile to use, if any

		// user-defined features
		public static bool Reliability;                         // component malfunctions and critical failures
		public static bool Deploy;                              // add EC cost to keep module working/animation, add EC cost to Extend\Retract
		public static bool Science;                             // science data storage, transmission and analysis
		public static bool SpaceWeather;                        // coronal mass ejections
		public static bool Automation;                          // control vessel components using scripts

		// pressure
		public static double PressureFactor;                    // pressurized modifier value for vessels below the threshold
		public static double PressureThreshold;                 // level of atmosphere resource that determine pressurized status

		// poisoning
		public static double PoisoningFactor;                   // poisoning modifier value for vessels below threshold
		public static double PoisoningThreshold;                // level of waste atmosphere resource that determine co2 poisoning status

		// signal
		public static UnlinkedCtrl UnlinkedControl;             // available control for unlinked vessels: 'none', 'limited' or 'full'
		public static float DataRateMinimumBitsPerSecond;       // as long as there is a control connection, the science data rate will never go below this.
		public static float DataRateDampingExponent;            // how much to damp data rate. stock is equivalent to 1, 6 gives nice values, RSS would use 4
		public static float DataRateDampingExponentRT;          // same for RemoteTech
		public static float DataRateSurfaceExperiment;          // transmission rate for surface experiments (Serenity DLC)
		public static double TransmitterActiveEcFactor;         // how much of the configured EC rate is used while transmitter is active
		public static double TransmitterPassiveEcFactor;        // how much of the configured EC rate is used while transmitter is passive

		// science
		public static bool ScienceDialog;                       // keep showing the stock science dialog
		public static double AsteroidSampleMassPerMB;           // When taking an asteroid sample, mass (in t) per MB of sample (baseValue * dataScale). default of 0.00002 => 34 Kg in stock

		// reliability
		public static double QualityScale;                      // scale applied to MTBF for high-quality components


		// crew level
		public static double LaboratoryCrewLevelBonus;          // factor for laboratory rate speed gain per crew level above minimum
		public static double MaxLaborartoryBonus;               // max bonus to be gained by having skilled crew on a laboratory
		public static double HarvesterCrewLevelBonus;           // factor for harvester speed gain per engineer level above minimum
		public static double MaxHarvesterBonus;                 // max bonus to be gained by having skilled engineers on a mining rig

		// misc
		public static bool EnforceCoherency;                    // detect and avoid issues at high timewarp in external modules
		public static double HeadLampsCost;                     // EC/s cost if eva headlamps are on
		public static bool LowQualityRendering;                 // use less particles to render the magnetic fields
		public static float UIScale;                            // scale UI elements by this factor, relative to KSP scaling settings, useful for high PPI screens
		public static float UIPanelWidthScale;                  // scale UI Panel Width by this factor, relative to KSP scaling settings, useful for high PPI screens
		public static float KerbalDeathReputationPenalty;       // Reputation penalty when Kerbals dies
		public static float KerbalBreakdownReputationPenalty;   // Reputation removed when Kerbals loose their marbles in space


		// presets for save game preferences

		public static int LifeSupportAtmoLoss;
		public static int LifeSupportSurvivalTemperature;
		public static int LifeSupportSurvivalRange;
		public static int ComfortLivingSpace;
		public static float ComfortFirmGround;
		public static float ComfortExercise;
		public static float ComfortNotAlone;
		public static float ComfortCallHome;
		public static float ComfortPanorama;
		public static float ComfortPlants;

		public static float StormFrequency;
		public static int StormDurationHours;
		public static float StormEjectionSpeed;
		public static float ShieldingEfficiency;
		public static float StormRadiation;
		public static float ExternRadiation;
		public static bool RadiationInSievert; // use Sievert iso. rad

		// sanity check settings
		public static string ModsRequired;
		public static string ModsIncompatible;
		public static string ModsWarning;
		public static string ModsScience;
		public static bool CheckForCRP;

		public static bool UseSamplingSunFactor;

		// debug / logging
		public static bool VolumeAndSurfaceLogging;

		public static bool loaded { get; private set; } = false;
	}


} // KERBALISM
