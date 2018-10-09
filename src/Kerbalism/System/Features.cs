using System;
using System.Collections.Generic;


namespace KERBALISM
{


	public static class Features
	{
		public static void Detect()
		{
			// set user-specified features
			Reliability = Settings.Reliability;
			Deploy = Settings.Deploy;
			Science = Settings.Science;
			SpaceWeather = Settings.SpaceWeather;
			Automation = Settings.Automation;

			// force-disable some features based on mods detected
			Reliability &= !Lib.HasAssembly("TestFlight");

			// detect all modifiers in use by current profile
			HashSet<string> modifiers = new HashSet<string>();
			foreach (Rule rule in Profile.rules)
			{
				foreach (string s in rule.modifiers) modifiers.Add(s);
			}
			foreach (Process process in Profile.processes)
			{
				foreach (string s in process.modifiers) modifiers.Add(s);
			}

			// detect features from modifiers
			Radiation = modifiers.Contains("radiation");
			Shielding = modifiers.Contains("shielding");
			LivingSpace = modifiers.Contains("living_space");
			Comfort = modifiers.Contains("comfort");
			Poisoning = modifiers.Contains("poisoning");
			Pressure = modifiers.Contains("pressure");
			Humidity = modifiers.Contains("humidity");

			// habitat is enabled if any of the values it provides are in use
			Habitat =
				 Shielding
			  || LivingSpace
			  || Poisoning
			  || Pressure
			  || Humidity
			  || modifiers.Contains("volume")
			  || modifiers.Contains("surface");

			// supplies is enabled if any non-EC supply exist
			Supplies = Profile.supplies.Find(k => k.resource != "ElectricCharge") != null;

			// log features
			Lib.Log("features:");
			Lib.Log("- Reliability: " + Reliability);
			Lib.Log("- Deploy: " + Deploy);
			Lib.Log("- Science: " + Science);
			Lib.Log("- SpaceWeather: " + SpaceWeather);
			Lib.Log("- Automation: " + Automation);
			Lib.Log("- Radiation: " + Radiation);
			Lib.Log("- Shielding: " + Shielding);
			Lib.Log("- LivingSpace: " + LivingSpace);
			Lib.Log("- Comfort: " + Comfort);
			Lib.Log("- Poisoning: " + Poisoning);
			Lib.Log("- Pressure: " + Pressure);
			Lib.Log("- Humidity: " + Humidity);
			Lib.Log("- Habitat: " + Habitat);
			Lib.Log("- Supplies: " + Supplies);
		}

		// user-specified features
		public static bool Reliability;
		public static bool Deploy;
		public static bool Science;
		public static bool SpaceWeather;
		public static bool Automation;

		// features detected automatically from modifiers
		public static bool Radiation;
		public static bool Shielding;
		public static bool LivingSpace;
		public static bool Comfort;
		public static bool Poisoning;
		public static bool Pressure;
		public static bool Humidity;

		// features detected in other ways
		public static bool Habitat;
		public static bool Supplies;
	}


} // KERBALISM
