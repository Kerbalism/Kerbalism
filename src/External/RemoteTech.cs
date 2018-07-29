using System;
using System.Reflection;

namespace KERBALISM
{

	/// <summary> Contains methods for RemoteTech's API</summary>
	public static class RemoteTech
	{
		// constructor
		static RemoteTech()
		{
			foreach (var a in AssemblyLoader.loadedAssemblies)
			{
				if (a.name == "RemoteTech")
				{
					API = a.assembly.GetType("RemoteTech.API.API");
					IsEnabled = API.GetMethod("IsRemoteTechEnabled");
					EnabledInSPC = API.GetMethod("EnableInSPC");
					IsConnected = API.GetMethod("HasAnyConnection");
					IsConnectedKSC = API.GetMethod("HasConnectionToKSC");
					IsTargetKSC = API.GetMethod("HasGroundStationTarget");
					NameTargetKSC = API.GetMethod("GetNameGroundStationTarget");
					SignalDelay = API.GetMethod("GetSignalDelayToKSC");
					SetRadioBlackout = API.GetMethod("SetRadioBlackoutGuid");
					GetRadioBlackout = API.GetMethod("GetRadioBlackoutGuid");
					SetPowerDown = API.GetMethod("SetPowerDownGuid");
					GetPowerDown = API.GetMethod("GetPowerDownGuid");
					break;
				}
			}
		}

		/// <summary> Returns true if RemoteTech is enabled for the current game</summary>
		public static bool Enabled
		{
			get { return API != null && (bool)IsEnabled.Invoke(null, new Object[] { }); }
		}

		/// <summary> Enables RTCore in the Space Center scene</summary>
		public static void EnableInSPC()
		{
			if (API != null && EnabledInSPC != null)
				EnabledInSPC.Invoke(null, new Object[] { true });
		}

		/// <summary> Returns true if the vessel has a connection back to KSC</summary>
		public static bool ConnectedToKSC(Guid id)
		{
			return API != null && (bool)IsConnectedKSC.Invoke(null, new Object[] { id });
		}

		/// <summary> Returns true if the vessel directly targets KSC</summary>
		public static bool TargetsKSC(Guid id)
		{
			return API != null && (bool)IsTargetKSC.Invoke(null, new Object[] { id });
		}

		/// <summary> Returns the name of the ground station directly targeted with the shortest link if any found by the vessel</summary>
		public static string NameTargetsKSC(Guid id)
		{
			if (API != null && NameTargetKSC != null)
				return (string)NameTargetKSC.Invoke(null, new Object[] { id });
			return null;
		}

		/// <summary> Returns true if the vessel has any connection</summary>
		public static bool Connected(Guid id)
		{
			return API != null && (bool)IsConnected.Invoke(null, new Object[] { id });
		}

		/// <summary> Returns the signal delay of the shortest route to the KSC if any found</summary>
		public static double GetSignalDelay(Guid id)
		{
			return (API != null ? (double)SignalDelay.Invoke(null, new Object[] { id }) : 0);
		}

		/// <summary> Sets the comms Blackout state for the vessel</summary>
		public static void SetCommsBlackout(Guid id, bool flag)
		{
			if (API != null && SetRadioBlackout != null)
				SetRadioBlackout.Invoke(null, new Object[] { id, flag, "Kerbalism" });
		}

		/// <summary> Gets the comms Blackout state of the vessel</summary>
		public static bool GetCommsBlackout(Guid id)
		{
			return API != null && GetRadioBlackout != null && (bool)GetRadioBlackout.Invoke(null, new Object[] { id });
		}

		/// <summary> Sets the Powered down state for the vessel</summary>
		public static void SetPoweredDown(Guid id, bool flag)
		{
			if (API != null && SetPowerDown != null)
				SetPowerDown.Invoke(null, new Object[] { id, flag, "Kerbalism" });
			else SetCommsBlackout(id, flag);  // Workaround for earlier versions of RT
		}

		/// <summary> Gets the Powered down state of the vessel</summary>
		public static bool IsPoweredDown(Guid id)
		{
			return API != null && GetPowerDown != null && (bool)GetPowerDown.Invoke(null, new Object[] { id });
		}

		/// <summary> Sets the Broken state for the vessel</summary>
		public static void SetBroken(PartModule antenna, bool broken)
		{
			Lib.ReflectionValue(antenna, "IsRTBroken", broken);
		}

		/// <summary> Returns true if the PartModule is a RemoteTech Antenna</summary>
		public static bool IsAntenna(PartModule m)
		{
			// we test for moduleName, but could use the boolean IsRTAntenna here
			return (m.moduleName == "ModuleRTAntenna" || m.moduleName == "ModuleRTAntennaPassive");
		}

		private static Type API;
		private static MethodInfo IsEnabled;
		private static MethodInfo EnabledInSPC;
		private static MethodInfo IsConnected;
		private static MethodInfo IsConnectedKSC;
		private static MethodInfo IsTargetKSC;
		private static MethodInfo NameTargetKSC;
		private static MethodInfo SignalDelay;
		private static MethodInfo SetRadioBlackout;
		private static MethodInfo GetRadioBlackout;
		private static MethodInfo SetPowerDown;
		private static MethodInfo GetPowerDown;
	}


} // KERBALISM

