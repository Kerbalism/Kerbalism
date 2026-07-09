using System.Collections.Generic;
using HarmonyLib;
using KERBALISM;

namespace KERBALISM
{
	[HarmonyPatch(typeof(Computer), nameof(Computer.GetModuleDevices))]
	internal static class ComputerDevicesFissionHarmony
	{
		private static void Postfix(Vessel v, ref List<Device> __result)
		{
			if (__result == null || v == null || !Features.Automation)
				return;

			FissionDeviceCollector.RemoveDevices(__result);

			var moduleDevices = new List<Device>();
			if (v.loaded)
				FissionDeviceCollector.CollectLoaded(v, moduleDevices);
			else
				FissionDeviceCollector.CollectProto(v, moduleDevices);

			if (moduleDevices.Count == 0)
				return;

			__result.InsertRange(FindFirstVesselDeviceIndex(__result), moduleDevices);
		}

		private static int FindFirstVesselDeviceIndex(List<Device> devices)
		{
			for (int i = 0; i < devices.Count; i++)
			{
				if (devices[i] is VesselDevice)
					return i;
			}

			return devices.Count;
		}
	}
}
