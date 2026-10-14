namespace KERBALISM.EngineFailures
{
	public class PreferencesEngineFailures : GameParameters.CustomParameterNode
	{
		[GameParameters.CustomParameterUI("#KERBALISM_EngineMalfunctions", toolTip = "#KERBALISM_EngineMalfunctions_desc")]
		public bool engineFailures = true;

		[GameParameters.CustomFloatParameterUI("#KERBALISM_EngineIgnitionFailureChance", asPercentage = true, minValue = 0, maxValue = 3, displayFormat = "F2", toolTip = "#KERBALISM_EngineIgnitionFailureChance_desc")]
		public float ignitionFailureChance = 1.0f;

		[GameParameters.CustomFloatParameterUI("#KERBALISM_EngineBurnFailureChance", asPercentage = true, minValue = 0, maxValue = 3, displayFormat = "F2", toolTip = "#KERBALISM_EngineBurnFailureChance_desc")]
		public float engineOperationFailureChance = 1.0f;

		public override GameParameters.GameMode GameMode => GameParameters.GameMode.ANY;

		public override bool HasPresets => true;

		public override void SetDifficultyPreset(GameParameters.Preset preset)
		{
			switch (preset)
			{
				case GameParameters.Preset.Easy:
					ignitionFailureChance = 0.5f;
					engineOperationFailureChance = 0.5f;
					engineFailures = false;
					break;
				case GameParameters.Preset.Normal:
					ignitionFailureChance = 0.75f;
					engineOperationFailureChance = 0.75f;
					engineFailures = true;
					break;
				case GameParameters.Preset.Moderate:
					ignitionFailureChance = 0.8f;
					engineOperationFailureChance = 0.8f;
					engineFailures = true;
					break;
				case GameParameters.Preset.Hard:
					ignitionFailureChance = 1f;
					engineOperationFailureChance = 1f;
					engineFailures = true;
					break;
			}
		}

		public override string DisplaySection => Local.Preferences_Section3;

		public override string Section => Local.Preferences_Section3;

		public override int SectionOrder => 0;

		public override string Title => Local.EngineMalfunctions;

		static PreferencesEngineFailures instance;

		public static PreferencesEngineFailures Instance
		{
			get
			{
				if (instance == null && HighLogic.CurrentGame != null)
					instance = HighLogic.CurrentGame.Parameters.CustomParams<PreferencesEngineFailures>();
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
