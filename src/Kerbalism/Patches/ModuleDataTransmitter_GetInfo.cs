using System;
using System.Collections.Generic;
using System.Text;
using Harmony;
using KSP.Localization;
using System.Globalization;


namespace KERBALISM
{
    /* Improves module info for stock data transmitter modules. */

    [HarmonyPatch(typeof(ModuleDataTransmitter))]
	[HarmonyPatch("GetInfo")]
	class ModuleDataTransmitter_GetInfo
	{
		static bool Prefix(ModuleDataTransmitter __instance, ref string __result)
		{
			// Patch only if science is enabled
			if (!Features.Science) return true;

			Specifics specs = new Specifics();

			// Antenna type: direct
			string text = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(__instance.antennaType.displayDescription());
			specs.Add(Localizer.Format("#autoLOC_7001005", text));

			// Antenna rating: 500km
			specs.Add(Localizer.Format("#autoLOC_7001006", Lib.HumanReadableDistance(__instance.antennaPower)));
			specs.Add("");

			double ec = __instance.DataResourceCost * __instance.DataRate;
			specs.Add("EC (idle)", Lib.BuildString("<color=#ffaa00>", Lib.HumanReadableRate(ec * Settings.TransmitterPassiveEcFactor), "</color>"));

			if (__instance.antennaType != AntennaType.INTERNAL)
			{
				specs.Add("EC (transmitting)", Lib.BuildString("<color=#ffaa00>", Lib.HumanReadableRate(ec * Settings.TransmitterActiveEcFactor), "</color>"));
				specs.Add("");
				specs.Add("Max. speed", Lib.HumanReadableDataRate(__instance.DataRate));
			}

			__result = specs.Info();

			// don't call default implementation
			return false;
		}
	}
}
