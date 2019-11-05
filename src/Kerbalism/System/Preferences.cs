using System;
using System.Reflection;
using KSP.IO;

namespace KERBALISM
{


	public class PreferencesReliability : GameParameters.CustomParameterNode
	{
		[GameParameters.CustomParameterUI("Highlight Malfunctions", toolTip = "Highlight faild parts in flight")]
		public bool highlights = true;

		[GameParameters.CustomParameterUI("Part Malfunctions", toolTip = "Allow engine failures based on part age and mean time between failures")]
		public bool mtbfFailures = true;

		[GameParameters.CustomFloatParameterUI("Critical Failure Rate", asPercentage = true, minValue = 0, maxValue = 1, displayFormat = "F2", toolTip = "Proportion of malfunctions that lead to critical failures")]
		public float criticalChance = 0.25f;

		[GameParameters.CustomFloatParameterUI("Fixable Failure Rate", asPercentage = true, minValue = 0, maxValue = 1, displayFormat = "F2", toolTip = "Proportion of malfunctions that can be fixed remotely")]
		public float safeModeChance = 0.5f;

		[GameParameters.CustomParameterUI("Incentive Redundancy", toolTip = "Each malfunction will increase the MTBF\nof components in the same redundancy group")]
		public bool incentiveRedundancy = true;

		[GameParameters.CustomParameterUI("Engine Malfunctions", toolTip = "Allow engine failures on ignition and exceeded burn durations")]
		public bool engineFailures = true;

		[GameParameters.CustomFloatParameterUI("Engine Ignition Failure Chance", asPercentage = true, minValue = 0, maxValue = 3, displayFormat = "F2", toolTip = "Adjust the probability of engine failures on ignition")]
		public float ignitionFailureChance = 1.0f;

		public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }

		public override bool HasPresets { get { return true; } }

		public override void SetDifficultyPreset(GameParameters.Preset preset)
		{
			switch (preset)
			{
				case GameParameters.Preset.Easy:
					criticalChance = 0.15f;
					safeModeChance = 0.6f;
					ignitionFailureChance = 0.75f;
					engineFailures = false;
					mtbfFailures = false;
					break;
				case GameParameters.Preset.Normal:
					criticalChance = 0.25f;
					safeModeChance = 0.5f;
					ignitionFailureChance = 0.8f;
					engineFailures = true;
					mtbfFailures = true;
					break;
				case GameParameters.Preset.Moderate:
					criticalChance = 0.3f;
					safeModeChance = 0.45f;
					ignitionFailureChance = 1.0f;
					engineFailures = true;
					mtbfFailures = true;
					break;
				case GameParameters.Preset.Hard:
					criticalChance = 0.35f;
					safeModeChance = 0.4f;
					ignitionFailureChance = 1.5f;
					engineFailures = true;
					mtbfFailures = true;
					break;
				default:
					break;
			}
		}

		public override string DisplaySection { get { return "Kerbalism (1)"; } }

		public override string Section { get { return "Kerbalism (1)"; } }

		public override int SectionOrder { get { return 1; } }

		public override string Title { get { return "Reliability"; } }

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
		[GameParameters.CustomParameterUI("Transmit Science Immediately", toolTip = "Automatically flag science files for transmission")]
		public bool transmitScience = true;

		[GameParameters.CustomParameterUI("Analyze Samples Immediately", toolTip = "Automatically flag samples for analysis in a lab")]
		public bool analyzeSamples = true;

		[GameParameters.CustomFloatParameterUI("Antenna Speed", asPercentage = true, minValue = 0.01f, maxValue = 2f, displayFormat = "F2", toolTip = "Antenna Bandwidth factor")]
		public float transmitFactor = 1.0f;

		[GameParameters.CustomFloatParameterUI("Always allow sample transfers", toolTip = "When off, sample transfer is only available in crewed vessels")]
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

		public override string DisplaySection { get { return "Kerbalism (1)"; } }

		public override string Section { get { return "Kerbalism (1)"; } }

		public override int SectionOrder { get { return 2; } }

		public override string Title { get { return "Science"; } }

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
		[GameParameters.CustomParameterUI("Electrical Charge", toolTip = "Show a message when EC level is low\n(Preset, can be changed per vessel)")]
		public bool ec = true;

		[GameParameters.CustomParameterUI("Supplies", toolTip = "Show a message when supply resources level is low\n(Preset, can be changed per vessel)")]
		public bool supply = true;

		[GameParameters.CustomParameterUI("Signal", toolTip = "Show a message when signal is lost or obtained\n(Preset, can be changed per vessel)")]
		public bool signal = false;

		[GameParameters.CustomParameterUI("Failures", toolTip = "Show a message when a components fail\n(Preset, can be changed per vessel)")]
		public bool malfunction = true;

		[GameParameters.CustomParameterUI("Space Weather", toolTip = "Show a message for CME events\n(Preset, can be changed per vessel)")]
		public bool storm = true;

		[GameParameters.CustomParameterUI("Scripts", toolTip = "Show a message when scripts are executed\n(Preset, can be changed per vessel)")]
		public bool script = false;

		[GameParameters.CustomParameterUI("Stock Messages", toolTip = "Use the stock message system instead of our own")]
		public bool stockMessages = false;

		[GameParameters.CustomIntParameterUI("Message Duration", minValue = 0, maxValue = 30, toolTip = "Duration of messages on screen in seconds")]
		public int messageLength = 4;

		public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }

		public override bool HasPresets { get { return false; } }

		public override string DisplaySection { get { return "Kerbalism (2)"; } }

		public override string Section { get { return "Kerbalism (2)"; } }

		public override int SectionOrder { get { return 0; } }

		public override string Title { get { return "Notifications"; } }

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
		[GameParameters.CustomParameterUI("Stress Breakdowns", toolTip = "Kerbals can make mistakes when they're under stress")]
		public bool stressBreakdowns = false;

		[GameParameters.CustomFloatParameterUI("Stress Breakdown Probability", asPercentage = true, minValue = 0, maxValue = 1, displayFormat = "F2", toolTip = "Probability of one stress induced mistake per year")]
		public float stressBreakdownRate = 0.25f;

		[GameParameters.CustomIntParameterUI("Ideal Living Space", minValue = 5, maxValue = 200, toolTip = "Ideal living space per-capita in m^3")]
		public int livingSpace = Settings.ComfortLivingSpace;

		[GameParameters.CustomFloatParameterUI("Firm Ground Factor", minValue = 0, maxValue = 1, displayFormat = "F2", toolTip = "Having something to walk on")]
		public float firmGround = Settings.ComfortFirmGround;

		[GameParameters.CustomFloatParameterUI("Exercise Factor", minValue = 0, maxValue = 1, displayFormat = "F2", toolTip = "Having a treadmill")]
		public float exercise = Settings.ComfortExercise;

		[GameParameters.CustomFloatParameterUI("Social Factor", minValue = 0, maxValue = 1, displayFormat = "F2", toolTip = "Having more than one crew on a vessel")]
		public float notAlone = Settings.ComfortNotAlone;

		[GameParameters.CustomFloatParameterUI("Call Home Factor", minValue = 0, maxValue = 1, displayFormat = "F2", toolTip = "Having a way to communicate with Kerbin")]
		public float callHome = Settings.ComfortCallHome;

		[GameParameters.CustomFloatParameterUI("Panorama Factor", minValue = 0, maxValue = 1, displayFormat = "F2", toolTip = "Comfort factor for having a panorama window")]
		public float panorama = Settings.ComfortPanorama;

		[GameParameters.CustomFloatParameterUI("Plants Factor", minValue = 0, maxValue = 1, displayFormat = "F2", toolTip = "There is some comfort in tending to plants")]
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

		public override string DisplaySection { get { return "Kerbalism (2)"; } }

		public override string Section { get { return "Kerbalism (2)"; } }

		public override int SectionOrder { get { return 1; } }

		public override string Title { get { return "Comfort"; } }

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
		[GameParameters.CustomParameterUI("Lifetime Radiation", toolTip = "Do not reset radiation values for kerbals recovered on kerbin")]
		public bool lifetime = false;

		[GameParameters.CustomFloatParameterUI("Storm probability", asPercentage = true, minValue = 0, maxValue = 5, displayFormat = "F2", toolTip = "Probability of solar storms")]
		public float stormFrequency = Settings.StormFrequency;

		[GameParameters.CustomIntParameterUI("Average storm duration (hours)", minValue = 1, maxValue = 200, toolTip = "Average duration of a sun storm in hours")]
		public int stormDurationHours = Settings.StormDurationHours;

		[GameParameters.CustomFloatParameterUI("Average storm radiation rad/h", minValue = 1, maxValue = 15, displayFormat = "F2", toolTip = "Radiation during a solar storm")]
		public float stormRadiation = Settings.StormRadiation;

		[GameParameters.CustomFloatParameterUI("Shielding Efficiency", asPercentage = true, minValue = 0.01f, maxValue = 1, displayFormat = "F2", toolTip = "Proportion of radiation blocked by shielding (at max amount)")]
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

		public override string DisplaySection { get { return "Kerbalism (1)"; } }

		public override string Section { get { return "Kerbalism (1)"; } }

		public override int SectionOrder { get { return 0; } }

		public override string Title { get { return "Radiation"; } }

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
