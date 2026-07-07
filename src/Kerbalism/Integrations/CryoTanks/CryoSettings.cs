namespace KERBALISM
{
	internal static class CryoSettings
	{
		internal const string SettingsNode = "KERBALISM_CRYO_SETTINGS";

		internal static bool Enabled = true;

		internal static void Load()
		{
			ConfigNode node = GameDatabase.Instance.GetConfigNode(SettingsNode);
			if (node == null)
				return;

			if (node.HasValue("Enabled"))
				Enabled = ParseBool(node.GetValue("Enabled"), Enabled);
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
	}
}
