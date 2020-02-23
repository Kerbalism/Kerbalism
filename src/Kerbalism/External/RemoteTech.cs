using System;
using System.Reflection;

namespace KERBALISM
{

	/// <summary> Contains methods for RemoteTech's API</summary>
	public static class RemoteTech
	{
		private static MethodInfo ModuleRTAntennaConsumptionMultiplier;
		private static FieldInfo ModuleRTAntennaEnergyCost;

		private static Type RT_API;
		private static Func<bool> IsEnabled;
		private static Action<bool> EnabledInSPC;
		private static Func<Guid, bool> IsConnected;
		private static Func<Guid, bool> IsConnectedKSC;
		private static Func<Guid, bool> IsTargetKSC;
		private static Func<Guid, string> NameTargetKSC;
		private static Func<Guid, string> NameFirstHopKSC;
		private static Func<Guid, double> SignalDelay;
		private static Func<Guid, bool, string, bool> SetRadioBlackout;
		private static Func<Guid, bool> GetRadioBlackout;
		private static Func<Guid, bool, string, bool> SetPowerDown;
		private static Func<Guid, bool> GetPowerDown;
		private static Func<Guid, Guid[]> GetControlPath;
		private static Func<Guid, Guid, double> GetDistance;
		private static Func<Guid, Guid, double> GetMaxDistance;
		private static Func<Guid, string> GetSatName;

		// constructor
		static RemoteTech()
		{
			foreach (var a in AssemblyLoader.loadedAssemblies)
			{
				if (a.name == "RemoteTech")
				{
					Installed = true;
					RT_API = a.assembly.GetType("RemoteTech.API.API");

					IsEnabled        = (Func<bool>)                    Delegate.CreateDelegate(typeof(Func<bool>), RT_API.GetMethod("IsRemoteTechEnabled"));
					EnabledInSPC     = (Action<bool>)                  Delegate.CreateDelegate(typeof(Action<bool>), RT_API.GetMethod("EnableInSPC"));
					IsConnected      = (Func<Guid, bool>)              Delegate.CreateDelegate(typeof(Func<Guid, bool>), RT_API.GetMethod("HasAnyConnection"));
					IsConnectedKSC   = (Func<Guid, bool>)              Delegate.CreateDelegate(typeof(Func<Guid, bool>), RT_API.GetMethod("HasConnectionToKSC"));
					IsTargetKSC      = (Func<Guid, bool>)              Delegate.CreateDelegate(typeof(Func<Guid, bool>), RT_API.GetMethod("HasDirectGroundStation"));
					NameTargetKSC    = (Func<Guid, string>)            Delegate.CreateDelegate(typeof(Func<Guid, string>), RT_API.GetMethod("GetClosestDirectGroundStation"));
					NameFirstHopKSC  = (Func<Guid, string>)            Delegate.CreateDelegate(typeof(Func<Guid, string>), RT_API.GetMethod("GetFirstHopToKSC"));
					SignalDelay      = (Func<Guid, double>)            Delegate.CreateDelegate(typeof(Func<Guid, double>), RT_API.GetMethod("GetSignalDelayToKSC"));
					SetRadioBlackout = (Func<Guid, bool, string, bool>)Delegate.CreateDelegate(typeof(Func<Guid, bool, string, bool>), RT_API.GetMethod("SetRadioBlackoutGuid"));
					GetRadioBlackout = (Func<Guid, bool>)              Delegate.CreateDelegate(typeof(Func<Guid, bool>), RT_API.GetMethod("GetRadioBlackoutGuid"));
					SetPowerDown     = (Func<Guid, bool, string, bool>)Delegate.CreateDelegate(typeof(Func<Guid, bool, string, bool>), RT_API.GetMethod("SetPowerDownGuid"));
					GetPowerDown     = (Func<Guid, bool>)              Delegate.CreateDelegate(typeof(Func<Guid, bool>), RT_API.GetMethod("GetPowerDownGuid"));
					GetControlPath   = (Func<Guid, Guid[]>)            Delegate.CreateDelegate(typeof(Func<Guid, Guid[]>), RT_API.GetMethod("GetControlPath"));
					GetDistance      = (Func<Guid, Guid, double>)      Delegate.CreateDelegate(typeof(Func<Guid, Guid, double>), RT_API.GetMethod("GetRangeDistance"));
					GetMaxDistance   = (Func<Guid, Guid, double>)      Delegate.CreateDelegate(typeof(Func<Guid, Guid, double>), RT_API.GetMethod("GetMaxRangeDistance"));
					GetSatName       = (Func<Guid, string>)            Delegate.CreateDelegate(typeof(Func<Guid, string>), RT_API.GetMethod("GetName"));

					Type ModuleRTAntennaType = a.assembly.GetType("RemoteTech.Modules.ModuleRTAntenna");
					ModuleRTAntennaConsumptionMultiplier = ModuleRTAntennaType.GetProperty("ConsumptionMultiplier", BindingFlags.Instance | BindingFlags.NonPublic).GetGetMethod(true);
					ModuleRTAntennaEnergyCost = ModuleRTAntennaType.GetField("EnergyCost");

					// check version is above 1.9, warn users if they are using an old version of RemoteTech
					if (!((a.versionMajor >= 1) && (a.versionMinor >= 9)))
					{
						Lib.Log("RemoteTech version is below v1.9 - Kerbalism's signal system will not operate correctly with the version" +
							" of RemoteTech currently installed." + Environment.NewLine + "Please update your installation of RemoteTech to the latest version.", Lib.LogLevel.Warning);
					}

					API.Failure.Add(RTFailureHandler);
					break;
				}
			}
		}

		public static void RTFailureHandler(Part part, string type, bool failure)
		{
			foreach (PartModule m in part.Modules)
			{
				if (RemoteTech.IsRTAntenna(m))
				{
					RemoteTech.SetBroken(m, failure);
				}
			}
		}

		public static bool Installed { get; private set; } = false;

		/// <summary> Returns true if RemoteTech is enabled for the current game</summary>
		public static bool Enabled => IsEnabled != null && IsEnabled();

		/// <summary> Enables RTCore in the Space Center scene</summary>
		public static void EnableInSPC() => EnabledInSPC?.Invoke(true);

		/// <summary> Returns true if the vessel has a connection back to KSC</summary>
		public static bool ConnectedToKSC(Guid id) => IsConnectedKSC == null ? false : IsConnectedKSC(id);

		/// <summary> Returns true if the vessel directly targets KSC</summary>
		public static bool TargetsKSC(Guid id) => IsTargetKSC == null ? false : IsTargetKSC(id);

		/// <summary> Returns the name of the ground station directly targeted with the shortest link if any found by the vessel</summary>
		public static string NameTargetsKSC(Guid id) => NameTargetKSC?.Invoke(id);

		/// <summary> Returns the name of the first hop vessel with the shortest link to KSC by the vessel</summary>
		public static string NameFirstHopToKSC(Guid id) => NameFirstHopKSC?.Invoke(id);

		/// <summary> Returns true if the vessel has any connection</summary>
		public static bool Connected(Guid id) => IsConnected != null && IsConnected(id);

		/// <summary> Returns the signal delay of the shortest route to the KSC if any found</summary>
		public static double GetSignalDelay(Guid id) => SignalDelay == null ? 0.0 : SignalDelay(id);

		/// <summary> Sets the comms Blackout state for the vessel</summary>
		public static void SetCommsBlackout(Guid id, bool flag) => SetRadioBlackout?.Invoke(id, flag, "Kerbalism");

		/// <summary> Gets the comms Blackout state of the vessel</summary>
		public static bool GetCommsBlackout(Guid id) => GetRadioBlackout != null && GetRadioBlackout(id);

		/// <summary> Sets the Powered down state for the vessel</summary>
		public static void SetPoweredDown(Guid id, bool flag) => SetPowerDown?.Invoke(id, flag, "Kerbalism");

		/// <summary> Gets the Powered down state of the vessel</summary>
		public static bool IsPoweredDown(Guid id) => GetPowerDown != null && GetPowerDown(id);

		/// <summary> Returns an array of all vessel ids in the control path </summary>
		/// <param name="id"> Satellite id to be searched</param>
		public static Guid[] GetCommsControlPath(Guid id) => GetControlPath == null ? new Guid[0] : GetControlPath(id);

		/// <summary> Returns distance between 2 satellites</summary>
		/// <param name="id_A">Satellite Source id</param>
		/// <param name="id_B">Satellite Target id</param>
		public static double GetCommsDistance(Guid id_A, Guid id_B) => GetDistance == null ? 0.0 : GetDistance(id_A, id_B);

		/// <summary> Returns max distance between 2 satellites</summary>
		/// <param name="id_A">Satellite Source id</param>
		/// <param name="id_B">Satellite Target id</param>
		public static double GetCommsMaxDistance(Guid id_A, Guid id_B) => GetMaxDistance == null ? 0.0 : GetMaxDistance(id_A, id_B);

		/// <summary> Returns satellite name</summary>
		/// <param name="id">Satellite id</param>
		public static string GetSatelliteName(Guid id) => GetSatName == null ? "" : GetSatName(id);

		/// <summary> Sets the Broken state for the vessel</summary>
		public static void SetBroken(PartModule antenna, bool broken)
		{
			Lib.ReflectionValue(antenna, "IsRTBroken", broken);
		}

		public static float GetModuleRTAntennaConsumption(PartModule moduleRTAntenna)
		{
			return (float)ModuleRTAntennaConsumptionMultiplier.Invoke(moduleRTAntenna, null) * (float)ModuleRTAntennaEnergyCost.GetValue(moduleRTAntenna);
		}

		/// <summary> Returns true if the PartModule is a RemoteTech Antenna</summary>
		private static bool IsRTAntenna(PartModule m)
		{
			// we test for moduleName, but could use the boolean IsRTAntenna here
			return (m.moduleName == "ModuleRTAntenna" || m.moduleName == "ModuleRTAntennaPassive");
		}
	}


} // KERBALISM
