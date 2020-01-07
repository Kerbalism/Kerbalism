using System;
using System.Reflection;
using KSP.IO;
using KSP.Localization;

namespace KERBALISM
{


	public class PreferencesReliability : GameParameters.CustomParameterNode
	{
		[GameParameters.CustomParameterUI("#KERBALISM_HighlightMalfunctions", toolTip = "#KERBALISM_HighlightMalfunctions_desc")]//Highlight Malfunctions--Highlight faild parts in flight
		public bool highlights = true;

		[GameParameters.CustomParameterUI("#KERBALISM_PartMalfunctions", toolTip = "#KERBALISM_PartMalfunctions_desc")]//Part Malfunctions--Allow engine failures based on part age and mean time between failures
		public bool mtbfFailures = true;

		[GameParameters.CustomFloatParameterUI("#KERBALISM_CriticalFailureRate", asPercentage = true, minValue = 0, maxValue = 1, displayFormat = "F2", toolTip = "#KERBALISM_CriticalFailureRate_desc")]//Critical Failure Rate---Proportion of malfunctions that lead to critical failures
		public float criticalChance = 0.25f;

		[GameParameters.CustomFloatParameterUI("#KERBALISM_FixableFailureRate", asPercentage = true, minValue = 0, maxValue = 1, displayFormat = "F2", toolTip = "#KERBALISM_FixableFailureRate_desc")]//Fixable Failure Rate--Proportion of malfunctions that can be fixed remotely
		public float safeModeChance = 0.5f;

		[GameParameters.CustomParameterUI("#KERBALISM_IncentiveRedundancy", toolTip = "#KERBALISM_IncentiveRedundancy_desc")]//Incentive Redundancy--Each malfunction will increase the MTBF\nof components in the same redundancy group
		public bool incentiveRedundancy = true;

		[GameParameters.CustomParameterUI("#KERBALISM_EngineMalfunctions", toolTip = "#KERBALISM_EngineMalfunctions_desc")]//Engine Malfunctions--Allow engine failures on ignition and exceeded burn durations
		public bool engineFailures = true;

		[GameParameters.CustomFloatParameterUI("#KERBALISM_EngineIgnitionFailureChance", asPercentage = true, minValue = 0, maxValue = 3, displayFormat = "F2", toolTip = "#KERBALISM_EngineIgnitionFailureChance_desc")]//Engine Ignition Failure Chance--Adjust the probability of engine failures on ignition
		public float ignitionFailureChance = 1.0f;

		[GameParameters.CustomFloatParameterUI("#KERBALISM_EngineBurnFailureChance", asPercentage = true, minValue = 0, maxValue = 3, displayFormat = "F2", toolTip = "#KERBALISM_EngineBurnFailureChance_desc")]//Engine Burn Failure Chance--Adjust the probability of an engine failure caused by excessive burn time
		public float engineOperationFailureChance = 1.0f;

		public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }

		public override bool HasPresets { get { return true; } }

		public override void SetDifficultyPreset(GameParameters.Preset preset)
		{
			switch (preset)
			{
				case GameParameters.Preset.Easy:
					criticalChance = 0.15f;
					safeModeChance = 0.6f;
					ignitionFailureChance = 0.5f;
					engineOperationFailureChance = 0.5f;
					engineFailures = false;
					mtbfFailures = false;
					break;
				case GameParameters.Preset.Normal:
					criticalChance = 0.25f;
					safeModeChance = 0.5f;
					ignitionFailureChance = 0.75f;
					engineOperationFailureChance = 0.75f;
					engineFailures = true;
					mtbfFailures = true;
					break;
				case GameParameters.Preset.Moderate:
					criticalChance = 0.3f;
					safeModeChance = 0.45f;
					ignitionFailureChance = 0.8f;
					engineOperationFailureChance = 0.8f;
					engineFailures = true;
					mtbfFailures = true;
					break;
				case GameParameters.Preset.Hard:
					criticalChance = 0.35f;
					safeModeChance = 0.4f;
					ignitionFailureChance = 1f;
					engineOperationFailureChance = 1f;
					engineFailures = true;
					mtbfFailures = true;
					break;
				default:
					break;
			}
		}

		public override string DisplaySection { get { return "Kerbalism (1)"; } }//

		public override string Section { get { return "Kerbalism (1)"; } }//

		public override int SectionOrder { get { return 1; } }

		public override string Title { get { return Localizer.Format("#KERBALISM_Preferences_Reliability"); } }//"Reliability"

		private static PreferencesReliability instance;

		public static PreferencesReliability Instance
		{
			get
			{
				if (instance == null)
				{
					if (HighLogic.CurrentGame != null)
					{ instance = HighLogic.CurrentGame.Parameters.CustomParams<PreferencesReliability>(); }
				}
				return instance;
			}
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
			instance = null;
		}
	}

	public class PreferencesScience : GameParameters.CustomParameterNode
	{
		[GameParameters.CustomParameterUI("#KERBALISM_TransmitScienceImmediately", toolTip = "#KERBALISM_TransmitScienceImmediately_desc")]//Transmit Science Immediately--Automatically flag science files for transmission
		public bool transmitScience = true;

		[GameParameters.CustomParameterUI("#KERBALISM_AnalyzeSamplesImmediately", toolTip = "#KERBALISM_AnalyzeSamplesImmediately_desc")]//Analyze Samples Immediately--Automatically flag samples for analysis in a lab
		public bool analyzeSamples = true;

		[GameParameters.CustomFloatParameterUI("#KERBALISM_AntennaSpeed", asPercentage = true, minValue = 0.01f, maxValue = 2f, displayFormat = "F2", toolTip = "#KERBALISM_AntennaSpeed_desc")]//Antenna Speed--Antenna Bandwidth factor
		public float transmitFactor = 1.0f;

		[GameParameters.CustomFloatParameterUI("#KERBALISM_Alwaysallowsampletransfers", toolTip = "#KERBALISM_Alwaysallowsampletransfers_desc")]//Always allow sample transfers---When off, sample transfer is only available in crewed vessels
		public bool sampleTransfer = true;

		public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }

		public override bool HasPresets { get { return true; } }

		public override void SetDifficultyPreset(GameParameters.Preset preset)
		{
			switch (preset)
			{
				case GameParameters.Preset.Easy:
					sampleTransfer = true;
					transmitFactor = 2f;
					break;
				case GameParameters.Preset.Normal:
					sampleTransfer = false;
					transmitFactor = 1.5f;
					break;
				case GameParameters.Preset.Moderate:
					sampleTransfer = false;
					transmitFactor = 1.2f;
					break;
				case GameParameters.Preset.Hard:
					sampleTransfer = false;
					transmitFactor = 1.0f;
					break;
				default:
					break;
			}
		}

		public override string DisplaySection { get { return "Kerbalism (1)"; } }//

		public override string Section { get { return "Kerbalism (1)"; } }//

		public override int SectionOrder { get { return 2; } }

		public override string Title { get { return Localizer.Format("#KERBALISM_Preferences_Science"); } }//"Science"

		private static PreferencesScience instance;

		public static PreferencesScience Instance
		{
			get
			{
				if (instance == null)
				{
					if (HighLogic.CurrentGame != null)
					{ instance = HighLogic.CurrentGame.Parameters.CustomParams<PreferencesScience>(); }
				}
				return instance;
			}
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
			instance = null;
		}
	}

	public class PreferencesMessages : GameParameters.CustomParameterNode
	{
		[GameParameters.CustomParameterUI("#KERBALISM_ElectricalCharge", toolTip = "#KERBALISM_ElectricalCharge_desc")]//Electrical Charge--Show a message when EC level is low\n(Preset, can be changed per vessel)
		public bool ec = true;

		[GameParameters.CustomParameterUI("#KERBALISM_Supplies", toolTip = "#KERBALISM_Supplies_desc")]//Supplies--Show a message when supply resources level is low\n(Preset, can be changed per vessel)
		public bool supply = true;

		[GameParameters.CustomParameterUI("#KERBALISM_Signal", toolTip = "#KERBALISM_Signal_desc")]//Signal--Show a message when signal is lost or obtained\n(Preset, can be changed per vessel)
		public bool signal = false;

		[GameParameters.CustomParameterUI("#KERBALISM_Failures", toolTip = "#KERBALISM_Failures_desc")]//Failures--Show a message when a components fail\n(Preset, can be changed per vessel)
		public bool malfunction = true;

		[GameParameters.CustomParameterUI("#KERBALISM_SpaceWeather", toolTip = "#KERBALISM_SpaceWeather_desc")]//Space Weather--Show a message for CME events\n(Preset, can be changed per vessel)
		public bool storm = false;

		[GameParameters.CustomParameterUI("#KERBALISM_Scripts", toolTip = "#KERBALISM_Scripts_desc")]//Scripts--Show a message when scripts are executed\n(Preset, can be changed per vessel)
		public bool script = false;

		[GameParameters.CustomParameterUI("#KERBALISM_StockMessages", toolTip = "#KERBALISM_StockMessages_desc")]//Stock Messages---Use the stock message system instead of our own
		public bool stockMessages = false;

		[GameParameters.CustomIntParameterUI("#KERBALISM_MessageDuration", minValue = 0, maxValue = 30, toolTip = "#KERBALISM_MessageDuration_desc")]//Message Duration--Duration of messages on screen in seconds
		public int messageLength = 4;

		public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }

		public override bool HasPresets { get { return false; } }

		public override string DisplaySection { get { return "Kerbalism (2)"; } }//

		public override string Section { get { return "Kerbalism (2)"; } }//

		public override int SectionOrder { get { return 0; } }

		public override string Title { get { return Localizer.Format("#KERBALISM_Preferences_Notifications"); } }//"Notifications"

		private static PreferencesMessages instance;

		public static PreferencesMessages Instance
		{
			get
			{
				if (instance == null)
				{
					if (HighLogic.CurrentGame != null)
					{ instance = HighLogic.CurrentGame.Parameters.CustomParams<PreferencesMessages>(); }
				}
				return instance;
			}
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
			instance = null;
		}
	}

	public class PreferencesComfort : GameParameters.CustomParameterNode
	{
		[GameParameters.CustomParameterUI("#KERBALISM_StressBreakdowns", toolTip = "#KERBALISM_StressBreakdowns_desc")]//Stress Breakdowns--Kerbals can make mistakes when they're under stress
		public bool stressBreakdowns = false;

		[GameParameters.CustomFloatParameterUI("#KERBALISM_StressBreakdownProbability", asPercentage = true, minValue = 0, maxValue = 1, displayFormat = "F2", toolTip = "#KERBALISM_StressBreakdownProbability_desc")]//Stress Breakdown Probability--Probability of one stress induced mistake per year
		public float stressBreakdownRate = 0.25f;

		[GameParameters.CustomIntParameterUI("#KERBALISM_IdealLivingSpace", minValue = 5, maxValue = 200, toolTip = "#KERBALISM_IdealLivingSpace_desc")]//Ideal Living Space--Ideal living space per-capita in m^3
		public int livingSpace = Settings.ComfortLivingSpace;

		[GameParameters.CustomFloatParameterUI("#KERBALISM_FirmGroundFactor", minValue = 0, maxValue = 1, displayFormat = "F2", toolTip = "#KERBALISM_FirmGroundFactor_desc")]//Firm Ground Factor--Having something to walk on
		public float firmGround = Settings.ComfortFirmGround;

		[GameParameters.CustomFloatParameterUI("#KERBALISM_ExerciseFactor", minValue = 0, maxValue = 1, displayFormat = "F2", toolTip = "#KERBALISM_ExerciseFactor_desc")]//Exercise Factor--Having a treadmill
		public float exercise = Settings.ComfortExercise;

		[GameParameters.CustomFloatParameterUI("#KERBALISM_SocialFactor", minValue = 0, maxValue = 1, displayFormat = "F2", toolTip = "#KERBALISM_SocialFactor_desc")]//Social Factor--Having more than one crew on a vessel
		public float notAlone = Settings.ComfortNotAlone;

		[GameParameters.CustomFloatParameterUI("#KERBALISM_CallHomeFactor", minValue = 0, maxValue = 1, displayFormat = "F2", toolTip = "#KERBALISM_CallHomeFactor_desc")]//Call Home Factor---Having a way to communicate with Kerbin
		public float callHome = Settings.ComfortCallHome;

		[GameParameters.CustomFloatParameterUI("#KERBALISM_PanoramaFactor", minValue = 0, maxValue = 1, displayFormat = "F2", toolTip = "#KERBALISM_PanoramaFactor_desc")]//Panorama Factor--Comfort factor for having a panorama window
		public float panorama = Settings.ComfortPanorama;

		[GameParameters.CustomFloatParameterUI("#KERBALISM_PlantsFactor", minValue = 0, maxValue = 1, displayFormat = "F2", toolTip = "#KERBALISM_PlantsFactor_desc")]//Plants Factor--There is some comfort in tending to plants
		public float plants = Settings.ComfortPlants;

		public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }

		public override bool HasPresets { get { return true; } }

		public override void SetDifficultyPreset(GameParameters.Preset preset)
		{
			switch (preset)
			{
				case GameParameters.Preset.Easy:
					stressBreakdowns = false;
					stressBreakdownRate = 0.2f;
					break;
				case GameParameters.Preset.Normal:
					stressBreakdowns = true;
					stressBreakdownRate = 0.25f;
					break;
				case GameParameters.Preset.Moderate:
					stressBreakdowns = true;
					stressBreakdownRate = 0.3f;
					break;
				case GameParameters.Preset.Hard:
					stressBreakdowns = true;
					stressBreakdownRate = 0.35f;
					break;
				default:
					break;
			}
		}

		public override string DisplaySection { get { return "Kerbalism (2)"; } }//

		public override string Section { get { return "Kerbalism (2)"; } }//

		public override int SectionOrder { get { return 1; } }

		public override string Title { get { return Localizer.Format("#KERBALISM_Preferences_Comfort"); } }//"Comfort"

		private static PreferencesComfort instance;

		public static PreferencesComfort Instance
		{
			get
			{
				if (instance == null)
				{
					if (HighLogic.CurrentGame != null)
					{ instance = HighLogic.CurrentGame.Parameters.CustomParams<PreferencesComfort>(); }
				}
				return instance;
			}
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
			instance = null;
		}
	}

	public class PreferencesRadiation : GameParameters.CustomParameterNode
	{
		[GameParameters.CustomParameterUI("#KERBALISM_LifetimeRadiation", toolTip = "#KERBALISM_LifetimeRadiation_desc")]//Lifetime Radiation--Do not reset radiation values for kerbals recovered on kerbin
		public bool lifetime = false;

		[GameParameters.CustomFloatParameterUI("#KERBALISM_Stormprobability", asPercentage = true, minValue = 0, maxValue = 5, displayFormat = "F2", toolTip = "#KERBALISM_Stormprobability_desc")]//Storm probability--Probability of solar storms
		public float stormFrequency = Settings.StormFrequency;

		[GameParameters.CustomIntParameterUI("#KERBALISM_stormDurationHours", minValue = 1, maxValue = 200, toolTip = "#KERBALISM_stormDurationHours_desc")]//Average storm duration (hours)--Average duration of a sun storm in hours
		public int stormDurationHours = Settings.StormDurationHours;

		[GameParameters.CustomFloatParameterUI("#KERBALISM_stormRadiation", minValue = 1, maxValue = 15, displayFormat = "F2", toolTip = "#KERBALISM_stormRadiation_desc")]//Average storm radiation rad/h--Radiation during a solar storm
		public float stormRadiation = Settings.StormRadiation;

		[GameParameters.CustomFloatParameterUI("#KERBALISM_ShieldingEfficiency", asPercentage = true, minValue = 0.01f, maxValue = 1, displayFormat = "F2", toolTip = "#KERBALISM_ShieldingEfficiency_desc")]//Shielding Efficiency--Proportion of radiation blocked by shielding (at max amount)
		public float shieldingEfficiency = Settings.ShieldingEfficiency;

		public double AvgStormDuration { get { return stormDurationHours * 3600.0; } }

		public double StormRadiation { get { return stormRadiation / 3600.0; } }

		public double StormEjectionSpeed { get { return Settings.StormEjectionSpeed * 299792458.0; } }

		public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }

		public override bool HasPresets { get { return true; } }

		public override void SetDifficultyPreset(GameParameters.Preset preset)
		{
			switch (preset)
			{
				case GameParameters.Preset.Easy:
					lifetime = false;
					stormFrequency = Settings.StormFrequency * 0.9f;
					stormRadiation = Settings.StormRadiation * 0.9f;
					shieldingEfficiency = Lib.Clamp(Settings.ShieldingEfficiency * 1.1f, 0.0f, 0.99f);
					break;
				case GameParameters.Preset.Normal:
					lifetime = false;
					stormFrequency = Settings.StormFrequency;
					stormRadiation = Settings.StormRadiation;
					shieldingEfficiency = Lib.Clamp(Settings.ShieldingEfficiency, 0.0f, 0.99f);
					break;
				case GameParameters.Preset.Moderate:
					lifetime = false;
					stormFrequency = Settings.StormFrequency * 1.3f;
					stormRadiation = Settings.StormRadiation * 1.2f;
					shieldingEfficiency = Lib.Clamp(Settings.ShieldingEfficiency * 0.9f, 0.0f, 0.99f);
					break;
				case GameParameters.Preset.Hard:
					lifetime = true;
					stormFrequency = Settings.StormFrequency * 1.5f;
					stormRadiation = Settings.StormRadiation * 1.5f;
					shieldingEfficiency = Lib.Clamp(Settings.ShieldingEfficiency * 0.8f, 0.0f, 0.99f);
					break;
				default:
					break;
			}
		}

		public override string DisplaySection { get { return "Kerbalism (1)"; } }//

		public override string Section { get { return "Kerbalism (1)"; } }//

		public override int SectionOrder { get { return 0; } }

		public override string Title { get { return Localizer.Format("#KERBALISM_Preferences_Radiation"); } }//"Radiation"

		private static PreferencesRadiation instance;

		public static PreferencesRadiation Instance
		{
			get
			{
				if (instance == null)
				{
					if (HighLogic.CurrentGame != null)
					{ instance = HighLogic.CurrentGame.Parameters.CustomParams<PreferencesRadiation>(); }
				}
				return instance;
			}
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
			instance = null;
		}
	}
}
