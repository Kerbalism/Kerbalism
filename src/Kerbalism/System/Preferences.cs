using System;
using System.Reflection;
using KSP.IO;

namespace KERBALISM
{
	public class PreferencesBasic : GameParameters.CustomParameterNode
	{
		[GameParameters.CustomParameterUI("Highlight Malfunctions", toolTip = "Highlight faild parts in flight")]
		public bool highlights = true;

		[GameParameters.CustomParameterUI("Lifetime Radiation", toolTip = "Do not reset radiation values for kerbals recovered on kerbin")]
		public bool lifetime = false;

		[GameParameters.CustomParameterUI("Stress Breakdowns", toolTip = "Kerbals can make mistakes when they're under stress")]
		public bool stressBreakdowns = false;

		[GameParameters.CustomFloatParameterUI("Stress Breakdown Probability", asPercentage = true, minValue = 0, maxValue = 1, displayFormat = "F2", toolTip = "Probability of one stress induced mistake per year")]
		public float stressBreakdownRate = 0.25f;

		[GameParameters.CustomIntParameterUI("Breakdown Reputation Penalty", minValue = 0, maxValue = 300, toolTip = "Reputation removed when a Kerbal looses his marbles in space")]
		public int breakdownPenalty = 10;

		[GameParameters.CustomIntParameterUI("Death Reputation Penalty", minValue = 0, maxValue = 300, toolTip = "Reputation removed when a Kerbal dies")]
		public int deathPenalty = 100;

		[GameParameters.CustomParameterUI("Incentive Redundancy", toolTip = "Each malfunction will increase the MTBF\nof components in the same redundancy group")]
		public bool incentiveRedundancy = true;

		[GameParameters.CustomFloatParameterUI("Critical Failure Rate", asPercentage = true, minValue = 0, maxValue = 1, displayFormat = "F2", toolTip = "Proportion of malfunctions that lead to critical failures")]
		public float criticalChance = 0.25f;

		[GameParameters.CustomFloatParameterUI("Fixable Failure Rate", asPercentage = true, minValue = 0, maxValue = 1, displayFormat = "F2", toolTip = "Proportion of malfunctions that can be fixed remotely")]
		public float safeModeChance = 0.5f;

		public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }

		public override bool HasPresets { get { return false; } }

		public override string DisplaySection { get { return "Kerbalism"; } }

		public override string Section { get { return "Kerbalism"; } }

		public override int SectionOrder { get { return 0; } }

		public override string Title { get { return "Common"; } }

		private static PreferencesBasic instance;

		public static PreferencesBasic Instance
		{
			get
			{
				if (instance == null)
				{
					if (HighLogic.CurrentGame != null)
					{ instance = HighLogic.CurrentGame.Parameters.CustomParams<PreferencesBasic>(); }
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

		[GameParameters.CustomParameterUI("Only record valuable science", toolTip = "Record experiment data only if it has at least a nominal value")]
		public bool smartScience = true;

		[GameParameters.CustomFloatParameterUI("Antenna Speed", asPercentage = true, minValue = 0.01f, maxValue = 2f, displayFormat = "F2", toolTip = "Antenna Bandwidth factor")]
		public float transmitFactor = 1.0f;

		[GameParameters.CustomFloatParameterUI("Always allow sample transfers", toolTip = "When off, sample transfer is only available in crewed vessels")]
		public bool sampleTransfer = true;

		public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }

		public override bool HasPresets { get { return false; } }

		public override string DisplaySection { get { return "Kerbalism"; } }

		public override string Section { get { return "Kerbalism"; } }

		public override int SectionOrder { get { return 1; } }

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

		public override string DisplaySection { get { return "Kerbalism"; } }

		public override string Section { get { return "Kerbalism"; } }

		public override int SectionOrder { get { return 2; } }

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

	public class PreferencesLifeSupport : GameParameters.CustomParameterNode
	{
		[GameParameters.CustomIntParameterUI("Survival Temperature", minValue = 250, maxValue = 330, toolTip = "Ideal living temperature")]
		public int survivalTemperature = Settings.LifeSupportSurvivalTemperature;

		[GameParameters.CustomIntParameterUI("Survival Temperature Range", minValue = 1, maxValue = 20, toolTip = "Sweet spot around survival temperature")]
		public int survivalRange = Settings.LifeSupportSurvivalRange;

		[GameParameters.CustomIntParameterUI("Amount of atmosphere lost to airlock on EVA", minValue = 1, maxValue = 100, toolTip = "Atmosphere lost in EVA airlock")]
		public int evaAtmoLoss = Settings.LifeSupportAtmoLoss;

		[GameParameters.CustomIntParameterUI("Log resource consumption", toolTip = "WARNING: this logs A LOT")]
		public bool resourceLogging = false;

		public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }

		public override bool HasPresets { get { return false; } }

		public override string DisplaySection { get { return "Kerbalism (Advanced)"; } }

		public override string Section { get { return "Kerbalism (Advanced)"; } }

		public override int SectionOrder { get { return 2; } }

		public override string Title { get { return "Life Support"; } }

		private static PreferencesLifeSupport instance;

		public static PreferencesLifeSupport Instance
		{
			get
			{
				if (instance == null)
				{
					if (HighLogic.CurrentGame != null)
					{ instance = HighLogic.CurrentGame.Parameters.CustomParams<PreferencesLifeSupport>(); }
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

		public override bool HasPresets { get { return false; } }

		public override string DisplaySection { get { return "Kerbalism (Advanced)"; } }

		public override string Section { get { return "Kerbalism (Advanced)"; } }

		public override int SectionOrder { get { return 0; } }

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

	public class PreferencesStorm : GameParameters.CustomParameterNode
	{
		[GameParameters.CustomIntParameterUI("Average storm duration (hours)", minValue = 1, maxValue = 200, toolTip = "Average duration of a sun storm in hours")]
		public int stormDurationHours = Settings.StormDurationHours;

		[GameParameters.CustomFloatParameterUI("Shielding Efficiency", asPercentage = true, minValue = 0.01f, maxValue = 1, displayFormat = "F2", toolTip = "Proportion of radiation blocked by shielding (at max amount)")]
		public float shieldingEfficiency = Settings.ShieldingEfficiency;

		[GameParameters.CustomFloatParameterUI("Storm Radiation rad/h", minValue = 1, maxValue = 15, displayFormat = "F2", toolTip = "Radiation during a solar storm")]
		public float stormRadiation = Settings.StormRadiation;

		public double AvgStormDuration { get { return stormDurationHours * 3600.0; } }

		public double StormRadiation { get { return stormRadiation / 3600.0; } }

		public double StormEjectionSpeed { get { return Settings.StormEjectionSpeed * 299792458.0; } }

		public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }

		public override bool HasPresets { get { return false; } }

		public override string DisplaySection { get { return "Kerbalism (Advanced)"; } }

		public override string Section { get { return "Kerbalism (Advanced)"; } }

		public override int SectionOrder { get { return 2; } }

		public override string Title { get { return "Radiation"; } }

		private static PreferencesStorm instance;

		public static PreferencesStorm Instance
		{
			get
			{
				if (instance == null)
				{
					if (HighLogic.CurrentGame != null)
					{ instance = HighLogic.CurrentGame.Parameters.CustomParams<PreferencesStorm>(); }
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
