using System;
using System.Reflection;

namespace KERBALISM
{
	public static class RemoteTech
	{
		static RemoteTech()
		{
			foreach (var a in AssemblyLoader.loadedAssemblies)
			{
				if (a.name == "RemoteTech")
				{
					API = a.assembly.GetType("RemoteTech.API.API");
					IsEnabled = API.GetMethod("IsRemoteTechEnabled");
					IsConnected = API.GetMethod("HasAnyConnection");
					IsConnectedKSC = API.GetMethod("HasConnectionToKSC");
					ShortestSignalDelay = API.GetMethod("GetShortestSignalDelay");
					SetRadioBlackout = API.GetMethod("SetRadioBlackoutGuid");
					GetRadioBlackout = API.GetMethod("GetRadioBlackoutGuid");
					break;
				}
			}
		}

		// return true if RemoteTech is enabled for the current game
		public static bool Enabled()
		{
			return API != null && (bool)IsEnabled.Invoke(null, new Object[] { });
		}

		public static bool ConnectedToKSC(Guid id)
		{
			return API != null && (bool)IsConnectedKSC.Invoke(null, new Object[] { id });
		}
		// return true if the vessel is connected according to RemoteTech
		public static bool Connected(Guid id)
		{
			return API != null && (bool)IsConnected.Invoke(null, new Object[] { id });
		}

		public static double GetShortestSignalDelay(Guid id)
		{
			return (API != null ? (double)ShortestSignalDelay.Invoke(null, new Object[] { id }) : 0);
		}

		public static Object SetCommsBlackout(Guid id, bool flag, string origin)
		{
			if (API != null && SetRadioBlackout != null)
				return SetRadioBlackout.Invoke(null, new Object[] { id, flag, origin });
			return null;
		}

		public static bool GetCommsBlackout(Guid id)
		{
			return API != null && GetRadioBlackout != null && (bool)GetRadioBlackout.Invoke(null, new Object[] { id });
		}

		public static void Update(Vessel v, Vessel_info vi, VesselData vd, double elapsed_s)
		{
			if (!Enabled())
				return;

			bool blackout = vi.blackout || !vi.powered;
			SetCommsBlackout(v.id, blackout, "kerbalism");
		}

		public static bool IsActive(ProtoPartModuleSnapshot antenna)
		{
			return Lib.Proto.GetBool(antenna, "IsRTActive");
		}

		public static void SetBroken(PartModule antenna, bool broken)
		{
			Lib.ReflectionValue(antenna, "IsRTBroken", broken);
		}

		public static bool IsAntenna(PartModule m)
		{
			// we test for moduleName, but could use the boolean IsRTAntenna here
			return IsAntenna(m.moduleName);
		}

		public static bool IsAntenna(String moduleName) {
			return moduleName == "ModuleRTAntenna" || moduleName == "ModuleRTAntennaPassive";
		}

		static Type API;
		static MethodInfo IsEnabled;
		static MethodInfo IsConnected;
		static MethodInfo IsConnectedKSC;
		static MethodInfo ShortestSignalDelay;
		static MethodInfo SetRadioBlackout;
		static MethodInfo GetRadioBlackout;
	}
} // KERBALISM

