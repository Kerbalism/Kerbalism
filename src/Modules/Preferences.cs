using System;
using System.Reflection;
using KSP.IO;

namespace KERBALISM
{
	public class PreferencesFeatures : GameParameters.CustomParameterNode
	{
		[GameParameters.CustomParameterUI("Enable Reliability", toolTip = "Component malfunctions and critical failures")]
		public bool enableReliability = true;

		[GameParameters.CustomParameterUI("Deployment requires EC", toolTip = "Add EC cost to keep modules working.\nAdds EC cost to Extend/Retract landing gears")]
		public bool enableDeploy = true;

		[GameParameters.CustomParameterUI("Kerbalistic Science", toolTip = "Replace stock science data storage, transmission and analysis")]
		public bool enableScience = true;

		[GameParameters.CustomParameterUI("Space Weather", toolTip = "Simulate coronal mass ejections")]
		public bool enableSpaceWeather = true;

		[GameParameters.CustomParameterUI("Automation", toolTip = "Control vessel components using an on-board computer")]
		public bool enableAutomation = true;


		[GameParameters.CustomParameterUI("Stock Science Dialog", toolTip = "Keep showing the stock science dialog")]
		public bool scienceDialog = true;

		[GameParameters.CustomParameterUI("Stock Messages", toolTip = "Use the stock message system instead of our own")]
		public bool stockMessages = false;

		[GameParameters.CustomIntParameterUI("Message Duration", minValue = 0, maxValue = 30, toolTip = "Duration of messages on screen in seconds")]
		public int messageLength = 4;

		[GameParameters.CustomIntParameterUI("Breakdown Penalty", minValue = 0, maxValue = 300, toolTip = "Reputation to remove in case of breakdown")]
		public int breakdownPenalty = 10;

		[GameParameters.CustomIntParameterUI("Death Penalty", minValue = 0, maxValue = 300, toolTip = "Reputation to remove in case of a death")]
		public int deathPenalty = 100;

		public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }

		public override bool HasPresets { get { return false; } }

		public override string DisplaySection { get { return "Kerbalism"; } }

		public override string Section { get { return "Kerbalism"; } }

		public override int SectionOrder { get { return 0; } }

		public override string Title { get { return "Basic Options"; } }
	}

	public class PreferencesComfort : GameParameters.CustomParameterNode
	{
		[GameParameters.CustomIntParameterUI("Survival Temperature", minValue = 250, maxValue = 330, toolTip = "Ideal living temperature")]
		public int survivalTemperature = 295;

		[GameParameters.CustomIntParameterUI("Survival Temperature Range", minValue = 1, maxValue = 20, toolTip = "Sweet spot around survival temperature")]
		public int survivalRange = 5;

		[GameParameters.CustomIntParameterUI("Ideal Living Space", minValue = 5, maxValue = 200, toolTip = "Ideal living space per-capita in m^3")]
		public int livingSpace = 40;

		[GameParameters.CustomFloatParameterUI("Firm Ground Factor", minValue = 0, maxValue = 1, displayFormat = "F2", toolTip = "Having something to walk on")]
		public float firmGroundComfortFactor = 0.4f;

		[GameParameters.CustomFloatParameterUI("Exercise Factor", minValue = 0, maxValue = 1, displayFormat = "F2", toolTip = "Having a treadmill")]
		public float exerciseComfortFactor = 0.2f;

		[GameParameters.CustomFloatParameterUI("Social Factor", minValue = 0, maxValue = 1, displayFormat = "F2", toolTip = "Having more than one crew on a vessel")]
		public float notAloneComfortFactor = 0.1f;

		[GameParameters.CustomFloatParameterUI("Call Home Factor", minValue = 0, maxValue = 1, displayFormat = "F2", toolTip = "Having a way to communicate with Kerbin")]
		public float callHomeComfortFactor = 0.1f;

		[GameParameters.CustomFloatParameterUI("Panorama Factor", minValue = 0, maxValue = 1, displayFormat = "F2", toolTip = "Comfort factor for having a panorama window")]
		public float panoramaComfortFactor = 0.1f;

		public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }

		public override bool HasPresets { get { return false; } }

		public override string DisplaySection { get { return "Kerbalism"; } }

		public override string Section { get { return "Kerbalism"; } }

		public override int SectionOrder { get { return 1; } }

		public override string Title { get { return "Comfort"; } }
	}

	public class PreferencesMessages : GameParameters.CustomParameterNode
	{
		[GameParameters.CustomParameterUI("Electrical Charge", toolTip = "Show a message when EC level is low")]
		public bool ec = true;

		[GameParameters.CustomParameterUI("Resources Low", toolTip = "Show a message when supply resources level is low")]
		public bool resources = true;

		[GameParameters.CustomParameterUI("Signal", toolTip = "Show a message when signal is lost or obtained")]
		public bool signal = true;

		[GameParameters.CustomParameterUI("Failures", toolTip = "Show a message when a components fail")]
		public bool failures = true;

		[GameParameters.CustomParameterUI("Space Weather", toolTip = "Show a message for CME events")]
		public bool cme = true;

		[GameParameters.CustomParameterUI("Scripts", toolTip = "Show a message when scripts are executed")]
		public bool scripts = true;

		public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }

		public override bool HasPresets { get { return false; } }

		public override string DisplaySection { get { return "Kerbalism"; } }

		public override string Section { get { return "Kerbalism"; } }

		public override int SectionOrder { get { return 2; } }

		public override string Title { get { return "Notification Presets"; } }
	}
}
