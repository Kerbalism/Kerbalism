using System;
using System.Collections.Generic;
using System.IO;
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
		private class ModToCheck
		{
			public const string NODENAME = "MOD_CHECK";

			private const string requiredText = "Missing required mod dependency";
			private const string incompatibleText = "Incompatible mod detected";
			private const string warningText = "Mod with limited compatibility detected";

			public enum ModCompatibility { None, Required, Incompatible, Warning, WarningScience }
			public ModCompatibility compatibility;
			public ErrorManager.Error error;
			public string modName;

			public static ModToCheck Get(ConfigNode node)
			{
				ModToCheck mod = new ModToCheck();
				mod.modName = Lib.ConfigValue(node, "name", string.Empty);
				mod.compatibility = Lib.ConfigEnum(node, "modCompatibility", ModCompatibility.None);
				if (mod.modName.Length == 0 || mod.compatibility == ModCompatibility.None)
					return null;

				string comment = Lib.ConfigValue(node, "comment", string.Empty);

				switch (mod.compatibility)
				{
					case ModCompatibility.Required:
						mod.error = new ErrorManager.Error(true, $"{requiredText} : {mod.modName}", comment);
						break;
					case ModCompatibility.Incompatible:
						mod.error = new ErrorManager.Error(true, $"{incompatibleText} : {mod.modName}", comment);
						break;
					case ModCompatibility.Warning:
					case ModCompatibility.WarningScience:
						if (comment.Length == 0)
						{
							comment = "This mod has some issues running alongside Kerbalism, please consult the mod compatibility page on the Github wiki";
						}

						mod.error = new ErrorManager.Error(false, $"{warningText} : {mod.modName}", comment);
						break;
				}

				return mod;
			}
		}

		private static List<ModToCheck> modsRequired = new List<ModToCheck>();
		private static List<ModToCheck> modsIncompatible = new List<ModToCheck>();

		public static void Parse()
		{
			var kerbalismConfigNodes = GameDatabase.Instance.GetConfigs("KERBALISM_SETTINGS");
			if (kerbalismConfigNodes.Length < 1) return;
			ConfigNode cfg = kerbalismConfigNodes[0].config;

			// habitat & pressure 
			PressureSuitVolume = Lib.ConfigValue(cfg, "PressureSuitVolume", 100.0);
			HabitatAtmoResource = Lib.ConfigValue(cfg, "HabitatAtmoResource", "KsmAtmosphere");
			HabitatWasteResource = Lib.ConfigValue(cfg, "HabitatWasteResource", "KsmWasteAtmosphere");
			HabitatBreathableResource = Lib.ConfigValue(cfg, "HabitatBreathableResource", "Oxygen");
			HabitatBreathableResourceRate = Lib.ConfigValue(cfg, "HabitatBreathableResourceRate", 0.00172379825);
			DepressuriationDefaultRate = Lib.ConfigValue(cfg, "DepressuriationDefaultRate", 10.0);
			PressureFactor = Lib.ConfigValue(cfg, "PressureFactor", 10.0);
			PressureThreshold = Lib.ConfigValue(cfg, "PressureThreshold", 0.3);

			// poisoning
			PoisoningFactor = Lib.ConfigValue(cfg, "PoisoningFactor", 0.0);
			PoisoningThreshold = Lib.ConfigValue(cfg, "PoisoningThreshold", 0.02);

			// signal
			UnlinkedControl = Lib.ConfigEnum(cfg, "UnlinkedControl", UnlinkedCtrl.none);
			DataRateMinimumBitsPerSecond = Lib.ConfigValue(cfg, "DataRateMinimumBitsPerSecond", 1.0);
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
			HibernatingEcFactor = Lib.ConfigValue(cfg, "HibernatingEcFactor", 0.001);

			// save game settings presets
			LifeSupportAtmoLoss = Lib.ConfigValue(cfg, "LifeSupportAtmoLoss", 50);
			LifeSupportSurvivalTemperature = Lib.ConfigValue(cfg, "LifeSupportSurvivalTemperature", 295);
			LifeSupportSurvivalRange = Lib.ConfigValue(cfg, "LifeSupportSurvivalRange", 5);

			ComfortLivingSpace = Lib.ConfigValue(cfg, "ComfortLivingSpace", 20);
			ComfortFirmGround = Lib.ConfigValue(cfg, "ComfortFirmGround", 0.3f);
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

			UseSamplingSunFactor = Lib.ConfigValue(cfg, "UseSamplingSunFactor", false);

			// debug / logging
			VolumeAndSurfaceLogging = Lib.ConfigValue(cfg, "VolumeAndSurfaceLogging", false);

			foreach (ConfigNode modNode in cfg.GetNodes(ModToCheck.NODENAME))
			{
				ModToCheck mod = ModToCheck.Get(modNode);
				if (mod != null)
				{
					if (mod.compatibility == ModToCheck.ModCompatibility.Required)
						modsRequired.Add(mod);
					else
						modsIncompatible.Add(mod);
				}
			}

			loaded = true;
		}

		public static void CheckMods()
		{
			List<string> loadedModsAndAssemblies = new List<string>();

			string[] directories = Directory.GetDirectories(KSPUtil.ApplicationRootPath + "GameData");
			for (int i = 0; i < directories.Length; i++)
			{
				loadedModsAndAssemblies.Add(new DirectoryInfo(directories[i]).Name);
			}

			foreach (var a in AssemblyLoader.loadedAssemblies)
			{
				loadedModsAndAssemblies.Add(a.name);
			}

			foreach (ModToCheck mod in modsRequired)
			{
				if (!loadedModsAndAssemblies.Exists(p => string.Equals(p, mod.modName, StringComparison.OrdinalIgnoreCase)))
					ErrorManager.AddError(mod.error);
			}

			foreach (ModToCheck mod in modsIncompatible)
			{
				if (loadedModsAndAssemblies.Exists(p => string.Equals(p, mod.modName, StringComparison.OrdinalIgnoreCase)))
					ErrorManager.AddError(mod.error);
			}
		}

		// habitat
		public static double PressureSuitVolume;                // pressure / EVA suit volume in liters, used for determining CO2 poisoning level while kerbals are in a depressurized habitat
		public static string HabitatAtmoResource;               // resource used to manage habitat pressure
		public static string HabitatWasteResource;              // resource used to manage habitat CO2 level (poisoning)
		public static string HabitatBreathableResource;         // resource automagically produced when the habitat is under breathable external conditions (Oxygen in the default profile)
		public static double HabitatBreathableResourceRate;     // per second, per kerbal production of the breathable resource. Should match the consumption defined in the breathing rule. Set it to 0 to disable it entirely.
		public static double DepressuriationDefaultRate;        // liters / second / √(m3) of habitat volume
		public static double PressureFactor;                    // penalty multiplier applied to the "pressure" modifier when the vessel is fully depressurized
		public static double PressureThreshold;                 // below that threshold, the vessel will be considered under non-survivable pressure and kerbals will put their helmets
																// also determine the altitude threshold at which non-pressurized habitats can use the external air
		// poisoning
		public static double PoisoningFactor;                   // poisoning modifier value for vessels below threshold
		public static double PoisoningThreshold;                // level of waste atmosphere resource that determine co2 poisoning status

		// signal
		public static UnlinkedCtrl UnlinkedControl;             // available control for unlinked vessels: 'none', 'limited' or 'full'
		public static double DataRateMinimumBitsPerSecond;      // as long as there is a control connection, the science data rate will never go below this.
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
		public static double HibernatingEcFactor;               // % of ec consumed on hibernating probes (ModuleCommand.hibernationMultiplier is ignored by Kerbalism)

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

		public static bool UseSamplingSunFactor;

		// debug / logging
		public static bool VolumeAndSurfaceLogging;

		public static bool loaded { get; private set; } = false;
	}


} // KERBALISM
