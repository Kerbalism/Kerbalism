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
					RT_API = a.assembly.GetType("RemoteTech.API.API");
					IsEnabled = RT_API.GetMethod("IsRemoteTechEnabled");
					EnabledInSPC = RT_API.GetMethod("EnableInSPC");
					IsConnected = RT_API.GetMethod("HasAnyConnection");
					IsConnectedKSC = RT_API.GetMethod("HasConnectionToKSC");
					IsTargetKSC = RT_API.GetMethod("HasDirectGroundStation");
					NameTargetKSC = RT_API.GetMethod("GetClosestDirectGroundStation");
					NameFirstHopKSC = RT_API.GetMethod("GetFirstHopToKSC");
					SignalDelay = RT_API.GetMethod("GetSignalDelayToKSC");
					SetRadioBlackout = RT_API.GetMethod("SetRadioBlackoutGuid");
					GetRadioBlackout = RT_API.GetMethod("GetRadioBlackoutGuid");
					SetPowerDown = RT_API.GetMethod("SetPowerDownGuid");
					GetPowerDown = RT_API.GetMethod("GetPowerDownGuid");
					GetControlPath = RT_API.GetMethod("GetControlPath");
					GetDistance = RT_API.GetMethod("GetRangeDistance");
					GetMaxDistance = RT_API.GetMethod("GetMaxRangeDistance");
					GetSatName = RT_API.GetMethod("GetName");

					// check version is above 1.9, warn users if they are using an old version of RemoteTech
					if (!((a.versionMajor >= 1) && (a.versionMinor >= 9)))
					{
						Lib.Log("RemoteTech version is below v1.9 - Kerbalism's signal system will not operate correctly with the version" +
							" of RemoteTech currently installed." + Environment.NewLine + "Please update your installation of RemoteTech to the latest version.", Lib.LogLevel.Warning);
					}
					break;
				}
			}
		}

		public static void Startup()
		{
			if (!Enabled) return;
			Lib.Log("RemoteTech starting up");

			var handler = typeof(RemoteTech).GetMethod("RTCommInfoHandler");
			API.Comm.Add(handler);

			API.Failure.Add(RTFailureHandler);
		}

		public static void RTCommInfoHandler(AntennaInfo antennaInfo, Vessel v)
		{
			var ai = new AntennaInfoRT(v, antennaInfo.powered, antennaInfo.storm);
			antennaInfo.linked = ai.linked;
			antennaInfo.ec = ai.ec;
			antennaInfo.control_path = ai.control_path;
			antennaInfo.rate = ai.rate;
			antennaInfo.status = ai.status;
			antennaInfo.strength = ai.strength;
			antennaInfo.target_name = ai.target_name;
		}

		public static void RTFailureHandler(Part part, string type, bool failure)
		{
			foreach (PartModule m in part.FindModulesImplementing<PartModule>())
			{
				if (RemoteTech.IsAntenna(m))
				{
					RemoteTech.SetBroken(m, failure);
				}
			}
		}

		/// <summary> Returns true if RemoteTech is enabled for the current game</summary>
		public static bool Enabled
		{
			get { return RT_API != null && (bool)IsEnabled.Invoke(null, new Object[] { }); }
		}

		/// <summary> Enables RTCore in the Space Center scene</summary>
		public static void EnableInSPC()
		{
			if (RT_API != null && EnabledInSPC != null)
				EnabledInSPC.Invoke(null, new Object[] { true });
		}

		/// <summary> Returns true if the vessel has a connection back to KSC</summary>
		public static bool ConnectedToKSC(Guid id)
		{
			return RT_API != null && (bool)IsConnectedKSC.Invoke(null, new Object[] { id });
		}

		/// <summary> Returns true if the vessel directly targets KSC</summary>
		public static bool TargetsKSC(Guid id)
		{
			return RT_API != null && (bool)IsTargetKSC.Invoke(null, new Object[] { id });
		}

		/// <summary> Returns the name of the ground station directly targeted with the shortest link if any found by the vessel</summary>
		public static string NameTargetsKSC(Guid id)
		{
			if (RT_API != null && NameTargetKSC != null)
				return (string)NameTargetKSC.Invoke(null, new Object[] { id });
			return null;
		}

		/// <summary> Returns the name of the first hop vessel with the shortest link to KSC by the vessel</summary>
		public static string NameFirstHopToKSC(Guid id)
		{
			if (RT_API != null && NameFirstHopKSC != null)
				return (string)NameFirstHopKSC.Invoke(null, new Object[] { id });
			return null;
		}

		/// <summary> Returns true if the vessel has any connection</summary>
		public static bool Connected(Guid id)
		{
			return RT_API != null && (bool)IsConnected.Invoke(null, new Object[] { id });
		}

		/// <summary> Returns the signal delay of the shortest route to the KSC if any found</summary>
		public static double GetSignalDelay(Guid id)
		{
			return (RT_API != null ? (double)SignalDelay.Invoke(null, new Object[] { id }) : 0);
		}

		/// <summary> Sets the comms Blackout state for the vessel</summary>
		public static void SetCommsBlackout(Guid id, bool flag)
		{
			if (RT_API != null && SetRadioBlackout != null)
				SetRadioBlackout.Invoke(null, new Object[] { id, flag, "Kerbalism" });
		}

		/// <summary> Gets the comms Blackout state of the vessel</summary>
		public static bool GetCommsBlackout(Guid id)
		{
			return RT_API != null && GetRadioBlackout != null && (bool)GetRadioBlackout.Invoke(null, new Object[] { id });
		}

		/// <summary> Sets the Powered down state for the vessel</summary>
		public static void SetPoweredDown(Guid id, bool flag)
		{
			if (RT_API != null && SetPowerDown != null)
				SetPowerDown.Invoke(null, new Object[] { id, flag, "Kerbalism" });
			else SetCommsBlackout(id, flag);  // Workaround for earlier versions of RT
		}

		/// <summary> Gets the Powered down state of the vessel</summary>
		public static bool IsPoweredDown(Guid id)
		{
			return RT_API != null && GetPowerDown != null && (bool)GetPowerDown.Invoke(null, new Object[] { id });
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
		/// <param name="id"> Satellite id to be searched</param>
		public static Guid[] GetCommsControlPath(Guid id)
		{
			return RT_API != null && GetControlPath != null ? (Guid[])GetControlPath.Invoke(null, new Object[] { id }) : new Guid[0];
		}

		/// <summary> Returns distance between 2 satellites</summary>
		/// <param name="id_A">Satellite Source id</param>
		/// <param name="id_B">Satellite Target id</param>
		public static double GetCommsDistance(Guid id_A, Guid id_B)
		{
			return RT_API != null && GetDistance != null ? (double)GetDistance.Invoke(null, new Object[] { id_A, id_B }) : 0.0;
		}

		/// <summary> Returns max distance between 2 satellites</summary>
		/// <param name="id_A">Satellite Source id</param>
		/// <param name="id_B">Satellite Target id</param>
		public static double GetCommsMaxDistance(Guid id_A, Guid id_B)
		{
			return RT_API != null && GetMaxDistance != null ? (double)GetMaxDistance.Invoke(null, new Object[] { id_A, id_B }) : 0.0;
		}

		/// <summary> Returns satellite name</summary>
		/// <param name="id">Satellite id</param>
		public static string GetSatelliteName(Guid id)
		{
			return RT_API != null && GetSatName != null ? (string)GetSatName.Invoke(null, new Object[] { id }) : string.Empty;
		}

		private static Type RT_API;
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
		private static MethodInfo GetSatName;
	}


} // KERBALISM
