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

			string text = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(__instance.antennaType.displayDescription());

			// Antenna type: direct
			string result = Localizer.Format("#autoLOC_7001005", text);

			// Antenna rating: 500km
			result += Localizer.Format("#autoLOC_7001006", Lib.HumanReadableDistance(__instance.antennaPower));
			result += "\n";

			var dsn1 = CommNet.CommNetScenario.RangeModel.GetMaximumRange(__instance.antennaPower, GameVariables.Instance.GetDSNRange(0f));
			var dsn2 = CommNet.CommNetScenario.RangeModel.GetMaximumRange(__instance.antennaPower, GameVariables.Instance.GetDSNRange(0.5f));
			var dsn3 = CommNet.CommNetScenario.RangeModel.GetMaximumRange(__instance.antennaPower, GameVariables.Instance.GetDSNRange(1f));
			result += Lib.BuildString(Localizer.Format("#autoLOC_236834"), " ", Lib.HumanReadableDistance(dsn1));
			result += Lib.BuildString(Localizer.Format("#autoLOC_236835"), " ", Lib.HumanReadableDistance(dsn2));
			result += Lib.BuildString(Localizer.Format("#autoLOC_236836"), " ", Lib.HumanReadableDistance(dsn3));

			double ec = __instance.DataResourceCost * __instance.DataRate;

			Specifics specs = new Specifics();
			specs.Add(Local.DataTransmitter_ECidle, Lib.Color(Lib.HumanReadableRate(ec * Settings.TransmitterPassiveEcFactor), Lib.Kolor.Orange));//"EC (idle)"

			if (__instance.antennaType != AntennaType.INTERNAL) 
			{
				specs.Add(Local.DataTransmitter_ECTX, Lib.Color(Lib.HumanReadableRate(ec * Settings.TransmitterActiveEcFactor), Lib.Kolor.Orange));//"EC (transmitting)"
				specs.Add("");
				specs.Add(Local.DataTransmitter_Maxspeed, Lib.HumanReadableDataRate(__instance.DataRate));//"Max. speed"
			}

			__result = Lib.BuildString(result, "\n\n", specs.Info());

			// don't call default implementation
			return false;
		}
	}
}
