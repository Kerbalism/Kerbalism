namespace KERBALISM
{
	internal static class FFTSettings
	{
		internal const string SettingsNode = "KERBALISM_FFT_SETTINGS";

		/// <summary>When false, background EC loss does not disable containment or annihilate antimatter.</summary>
		internal static bool AntimatterBackgroundDetonation = true;

		/// <summary>Seconds of cumulative EC deficit required before containment shuts down in background.</summary>
		internal static double AntimatterDetonationGraceSeconds = 0.0;

		/// <summary>Maximum antimatter units annihilated per background step when containment is off.</summary>
		internal static double AntimatterMaxDetonationPerStep = 0.0;

		internal static void Load()
		{
			ConfigNode node = GameDatabase.Instance.GetConfigNode(SettingsNode);
			if (node == null)
				return;

			if (node.HasValue("Antimatter_BackgroundDetonation"))
				AntimatterBackgroundDetonation = ParseBool(node.GetValue("Antimatter_BackgroundDetonation"), AntimatterBackgroundDetonation);

			if (node.HasValue("Antimatter_DetonationGraceSeconds"))
				AntimatterDetonationGraceSeconds = ParseDouble(node.GetValue("Antimatter_DetonationGraceSeconds"), AntimatterDetonationGraceSeconds);

			if (node.HasValue("Antimatter_MaxDetonationPerStep"))
				AntimatterMaxDetonationPerStep = ParseDouble(node.GetValue("Antimatter_MaxDetonationPerStep"), AntimatterMaxDetonationPerStep);
		}

		static bool ParseBool(string raw, bool fallback)
		{
			if (string.IsNullOrEmpty(raw))
				return fallback;
			raw = raw.Trim();
			if (bool.TryParse(raw, out bool value))
				return value;
			if (raw == "1")
				return true;
			if (raw == "0")
				return false;
			return fallback;
		}

		static double ParseDouble(string raw, double fallback)
		{
			if (string.IsNullOrEmpty(raw))
				return fallback;
			return double.TryParse(raw.Trim(), out double value) ? value : fallback;
		}
	}
}
