using System;
using Harmony;
using KSP.UI.TooltipTypes;

namespace KERBALISM
{
	[HarmonyPatch(typeof(TooltipController_CrewAC))]
	[HarmonyPatch("SetTooltip")]
	public class TooltipController_CrewAC_SetTooltip
	{
		static void Postfix(TooltipController_CrewAC __instance, ProtoCrewMember pcm)
		{
			var rules = DB.Kerbal(pcm.name).rules;
			if (!rules.ContainsKey("radiation")) return;

			var radiation = rules["radiation"];
			if (!radiation.lifetime) return;

			string unit = " rad";
			double limit = 200.0;
			if(Settings.RadiationInSievert)
			{
				// 100 rad = 1 Sv
				unit = " mSv";
				limit *= 10.0;
			}

			__instance.descriptionString += Lib.BuildString("\n\n<b>Career Radiation:</b> ",
				(radiation.problem * limit).ToString("F0"), "/", limit.ToString("F0"), unit,
				" (", Lib.HumanReadablePerc(radiation.problem), ")");
		}
	}
}
