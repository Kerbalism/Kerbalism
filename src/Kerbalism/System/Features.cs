using System;
using System.Collections.Generic;


namespace KERBALISM
{
	public static class Features
	{
		public static bool LifeSupport;
		public static bool Stress;
		public static bool Radiation;
		public static bool Failures;
		public static bool Science;

		public static void Parse()
		{
			var featuresNodes = GameDatabase.Instance.GetConfigs("KERBALISM_FEATURES");

			ConfigNode cfg;
			if (featuresNodes.Length == 1)
				cfg = featuresNodes[0].config;
			else
				cfg = new ConfigNode();

			// user-defined features
			Failures = Lib.ConfigValue(cfg, "Failures", true);
			LifeSupport = Lib.ConfigValue(cfg, "LifeSupport", true);
			Science = Lib.ConfigValue(cfg, "Science", true);
			Radiation = Lib.ConfigValue(cfg, "Radiation", true);
			Stress = Lib.ConfigValue(cfg, "Stress", true);

			if (Stress && !LifeSupport)
			{
				ErrorManager.AddError(false, "Can't disable feature : LifeSupport", "Enforcing Kerbalism feature LifeSupport because you have Stress set to true in `Features.cfg`\nYou can't have one without the other.");
				LifeSupport = true;
			}

			if (Radiation && !LifeSupport)
			{
				ErrorManager.AddError(false, "Can't disable feature : LifeSupport", "Enforcing Kerbalism feature LifeSupport because you have Radiation set to true in `Features.cfg`\nYou can't have one without the other.");
				LifeSupport = true;
			}

			// log features
			Lib.Log("Features:");
			Lib.Log("- LifeSupport: " + LifeSupport);
			Lib.Log("- Stress: " + Stress);
			Lib.Log("- Failures: " + Failures);
			Lib.Log("- Science: " + Science);
			Lib.Log("- Radiation: " + Radiation);
		}
	}
} // KERBALISM
