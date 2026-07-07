using UnityEngine;

namespace KERBALISM
{
	[KSPAddon(KSPAddon.Startup.MainMenu, true)]
	public sealed class SystemHeatIntegrationSettingsLoader : MonoBehaviour
	{
		private void Awake()
		{
			Load();
		}

		internal static void Load()
		{
			ConfigNode[] settingsNodes = GameDatabase.Instance.GetConfigNodes("KERBALISM_BRIDGE_SETTINGS");
			ConfigNode settingsNode = settingsNodes == null || settingsNodes.Length == 0 ? null : settingsNodes[settingsNodes.Length - 1];
			if (settingsNode == null)
				return;

			string enabled = settingsNode.GetValue("BackgroundThermalSim");
			if (!string.IsNullOrEmpty(enabled))
				bool.TryParse(enabled, out SystemHeatBackgroundThermal.Enabled);

			settingsNode.TryGetValue("BackgroundRadiatorCoefficient", ref SystemHeatBackgroundThermal.RadiatorCoefficient);
		}
	}
}
