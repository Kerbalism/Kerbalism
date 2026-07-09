using System.Collections.Generic;
using HarmonyLib;

namespace KERBALISM
{
	[HarmonyPatch(typeof(Computer), nameof(Computer.GetModuleDevices))]
	internal static class ComputerDevicesSHNativeHarmony
	{
		private static void Postfix(Vessel v, ref List<Device> __result)
		{
			if (__result == null || v == null || !Features.Automation)
				return;

			SHNativeDeviceCollector.RemoveDevices(__result);

			var moduleDevices = new List<Device>();
			if (v.loaded)
				SHNativeDeviceCollector.CollectLoaded(v, moduleDevices);
			else
				SHNativeDeviceCollector.CollectProto(v, moduleDevices);

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
