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
					IsTargetKSC = API.GetMethod("HasDirectGroundStation");
					NameTargetKSC = API.GetMethod("GetClosestDirectGroundStation");
					NameFirstHopKSC = API.GetMethod("GetFirstHopToKSC");
					SignalDelay = API.GetMethod("GetSignalDelayToKSC");
					SetRadioBlackout = API.GetMethod("SetRadioBlackoutGuid");
					GetRadioBlackout = API.GetMethod("GetRadioBlackoutGuid");
					SetPowerDown = API.GetMethod("SetPowerDownGuid");
					GetPowerDown = API.GetMethod("GetPowerDownGuid");
					GetControlPath = API.GetMethod("GetControlPath");
					GetDistance = API.GetMethod("GetRangeDistance");
					GetMaxDistance = API.GetMethod("GetMaxRangeDistance");

					// check version is above 1.8.12, warn users if they are using an old version of RemoteTech
#if !KSP13
					if (!((a.versionMajor >= 1) && (a.versionMinor >= 8) && (a.versionRevision >= 13)))
#else
					if (!((a.versionMajor >= 1) && (a.versionMinor >= 8)))
#endif
					{
						Lib.Log("**WARNING** RemoteTech version is below v1.8.13 - Kerbalism's signal system will not operate correctly with the version" +
							" of RemoteTech currently installed." + Environment.NewLine + "Please update your installation of RemoteTech to the latest version.");
					}
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

		/// <summary> Returns the name of the first hop vessel with the shortest link to KSC by the vessel</summary>
		public static string NameFirstHopToKSC(Guid id)
		{
			if (API != null && NameFirstHopKSC != null)
				return (string)NameFirstHopKSC.Invoke(null, new Object[] { id });
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

		/// <summary> Returns an array of all vessel ids in the control path </summary>
		/// <param name="id">Vessel id to be searched</param>
		public static Guid[] GetCommsControlPath(Guid id)
		{
			return API != null && GetControlPath != null ? (Guid[])GetControlPath.Invoke(null, new Object[] { id }) : new Guid[0];
		}

		/// <summary> Returns distance between 2 satellites</summary>
		/// <param name="sat_A">Satellite Source</param>
		/// <param name="sat_B">Satellite Target</param>
		public static double GetCommsDistance(Guid sat_A, Guid sat_B)
		{
			return API != null && GetDistance != null ? (double)GetDistance.Invoke(null, new Object[] { sat_A, sat_B }) : 0.0;
		}

		/// <summary> Returns max distance between 2 satellites</summary>
		/// <param name="sat_A">Satellite Source</param>
		/// <param name="sat_B">Satellite Target</param>
		public static double GetCommsMaxDistance(Guid sat_A, Guid sat_B)
		{
			return API != null && GetMaxDistance != null ? (double)GetMaxDistance.Invoke(null, new Object[] { sat_A, sat_B }) : 0.0;
		}

		public static bool NetworkInitialized = false;

		private static Type API;
		private static MethodInfo IsEnabled;
		private static MethodInfo EnabledInSPC;
		private static MethodInfo IsConnected;
		private static MethodInfo IsConnectedKSC;
		private static MethodInfo IsTargetKSC;
		private static MethodInfo NameTargetKSC;
		private static MethodInfo NameFirstHopKSC;
		private static MethodInfo SignalDelay;
		private static MethodInfo SetRadioBlackout;
		private static MethodInfo GetRadioBlackout;
		private static MethodInfo SetPowerDown;
		private static MethodInfo GetPowerDown;
		private static MethodInfo GetControlPath;
		private static MethodInfo GetDistance;
		private static MethodInfo GetMaxDistance;
	}


} // KERBALISM
