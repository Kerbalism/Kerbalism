using System;
using System.Reflection;
using KSP.IO;

namespace KERBALISM
{
	public class PreferencesBasic : GameParameters.CustomParameterNode
	{
/* I really want this in the gui... but have to look at how this can be done in Science.cs
		[GameParameters.CustomParameterUI("Stock Science Dialog", toolTip = "Keep showing the stock science dialog")]
		public bool scienceDialog = true;
*/

		[GameParameters.CustomParameterUI("Transmit Science Immediately", toolTip = "Automatically transmit science if possible")]
		public bool transmitScience = true;

		[GameParameters.CustomParameterUI("Highlight Malfunctions", toolTip = "Highlight faild parts in flight")]
		public bool highlights = true;

		[GameParameters.CustomParameterUI("Stress Breakdowns", toolTip = "Kerbals can make mistakes when they're under stress")]
		public bool stressBreakdowns = false;

		[GameParameters.CustomFloatParameterUI("Stress Breakdown Probability", asPercentage = true, minValue = 0, maxValue = 1, displayFormat = "F2", toolTip = "Probability of one stress induced mistake per week")]
		public float stressBreakdownWeeklyRate = 0.25f;

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

	public class PreferencesMessages : GameParameters.CustomParameterNode
	{
		[GameParameters.CustomParameterUI("Electrical Charge", toolTip = "Show a message when EC level is low\n(Preset, can be changed per vessel)")]
		public bool ec = true;

		[GameParameters.CustomParameterUI("Supplies", toolTip = "Show a message when supply resources level is low\n(Preset, can be changed per vessel)")]
		public bool supply = true;

		[GameParameters.CustomParameterUI("Signal", toolTip = "Show a message when signal is lost or obtained\n(Preset, can be changed per vessel)")]
		public bool signal = true;

		[GameParameters.CustomParameterUI("Failures", toolTip = "Show a message when a components fail\n(Preset, can be changed per vessel)")]
		public bool malfunction = true;

		[GameParameters.CustomParameterUI("Space Weather", toolTip = "Show a message for CME events\n(Preset, can be changed per vessel)")]
		public bool storm = true;

		[GameParameters.CustomParameterUI("Scripts", toolTip = "Show a message when scripts are executed\n(Preset, can be changed per vessel)")]
		public bool script = true;

		[GameParameters.CustomParameterUI("Stock Messages", toolTip = "Use the stock message system instead of our own")]
		public bool stockMessages = false;

		[GameParameters.CustomIntParameterUI("Message Duration", minValue = 0, maxValue = 30, toolTip = "Duration of messages on screen in seconds")]
		public int messageLength = 4;

		public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }

		public override bool HasPresets { get { return false; } }

		public override string DisplaySection { get { return "Kerbalism"; } }

		public override string Section { get { return "Kerbalism"; } }

		public override int SectionOrder { get { return 1; } }

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
		public int survivalTemperature = 295;

		[GameParameters.CustomIntParameterUI("Survival Temperature Range", minValue = 1, maxValue = 20, toolTip = "Sweet spot around survival temperature")]
		public int survivalRange = 5;


		/*
		// pressure
		public static double PressureThreshold;                 // level of atmosphere resource that determine pressurized status

		// poisoning
		public static double PoisoningFactor;                   // poisoning modifier value for vessels below threshold
		public static double PoisoningThreshold;                // level of waste atmosphere resource that determine co2 poisoning status

		// humidity
		public static double HumidityFactor;                    // moisture modifier value for vessels below the threshold
		public static double HumidityThreshold;                 // level of moist atmosphere resource that determine high humidity status


		// pressure
			PressureFactor = Lib.ConfigValue(cfg, "PressureFactor", 10.0);
			PressureThreshold = Lib.ConfigValue(cfg, "PressureThreshold", 0.9);

			// poisoning
			PoisoningFactor = Lib.ConfigValue(cfg, "PoisoningFactor", 0.0);
			PoisoningThreshold = Lib.ConfigValue(cfg, "PoisoningThreshold", 0.02);

			// humidity
			HumidityFactor = Lib.ConfigValue(cfg, "HumidityFactor", 1.0);
			HumidityThreshold = Lib.ConfigValue(cfg, "HumidityThreshold", 0.95);
		*/

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
		public int livingSpace = 20;

		[GameParameters.CustomFloatParameterUI("Firm Ground Factor", minValue = 0, maxValue = 1, displayFormat = "F2", toolTip = "Having something to walk on")]
		public float firmGround = 0.4f;

		[GameParameters.CustomFloatParameterUI("Exercise Factor", minValue = 0, maxValue = 1, displayFormat = "F2", toolTip = "Having a treadmill")]
		public float exercise = 0.2f;

		[GameParameters.CustomFloatParameterUI("Social Factor", minValue = 0, maxValue = 1, displayFormat = "F2", toolTip = "Having more than one crew on a vessel")]
		public float notAlone = 0.1f;

		[GameParameters.CustomFloatParameterUI("Call Home Factor", minValue = 0, maxValue = 1, displayFormat = "F2", toolTip = "Having a way to communicate with Kerbin")]
		public float callHome = 0.1f;

		[GameParameters.CustomFloatParameterUI("Panorama Factor", minValue = 0, maxValue = 1, displayFormat = "F2", toolTip = "Comfort factor for having a panorama window")]
		public float panorama = 0.1f;

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
		[GameParameters.CustomIntParameterUI("Min Days Between Storms", minValue = 1, maxValue = 2000, toolTip = "Minimum days between storms over a system")]
		public int stormMinDays = 100;

		[GameParameters.CustomIntParameterUI("Max Days Between Storms", minValue = 1, maxValue = 2000, toolTip = "Maximum days between storms over a system")]
		public int stormMaxDays = 500;

		[GameParameters.CustomIntParameterUI("Storm Duration Hours", minValue = 1, maxValue = 200, toolTip = "Average duration of a storm in hours")]
		public int stormDurationHours = 6;

		[GameParameters.CustomFloatParameterUI("Storm Ejection Speed", asPercentage = true, minValue = 0.01f, maxValue = 1, displayFormat = "F2", toolTip = "CME speed as percentage of C")]
		public float stormEjectionSpeedC = 0.33f;

		[GameParameters.CustomFloatParameterUI("Shielding Efficiency", asPercentage = true, minValue = 0.01f, maxValue = 1, displayFormat = "F2", toolTip = "Proportion of radiation blocked by shielding (at max amount)")]
		public float shieldingEfficiency = 0.9f;

		[GameParameters.CustomFloatParameterUI("Storm Radiation rad/h", minValue = 1, maxValue = 15, displayFormat = "F2", toolTip = "Radiation during a solar storm")]
		public float stormRadiation = 5;

		[GameParameters.CustomFloatParameterUI("External Radiation rad/h", minValue = 0.01f, maxValue = 2, displayFormat = "F2", toolTip = "Radiation outside the heliopause")]
		public float externRadiation = 0.04f;

		public double StormMinTime { get { return stormMinDays * Lib.HoursInDay() * 3600.0; } }

		public double StormMaxTime { get { return stormMaxDays * Lib.HoursInDay() * 3600.0; } }

		public double StormDuration { get { return stormDurationHours * 3600.0; } }

		public double ExternRadiation { get { return externRadiation / 3600.0; } }

		public double StormRadiation { get { return stormRadiation / 3600.0; } }

		public double StormEjectionSpeed { get { return stormEjectionSpeedC * 299792458.0; } }

		public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }

		public override bool HasPresets { get { return false; } }

		public override string DisplaySection { get { return "Kerbalism (Advanced)"; } }

		public override string Section { get { return "Kerbalism (Advanced)"; } }

		public override int SectionOrder { get { return 2; } }

		public override string Title { get { return "Storms & Radiation"; } }

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
