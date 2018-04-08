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

		public static void SetCommsBlackout(Guid id, bool flag, string origin)
		{
			if (API != null && SetRadioBlackout != null) SetRadioBlackout.Invoke(null, new Object[] { id, flag, origin });
		}

		public static bool GetCommsBlackout(Guid id)
		{
			return API != null && GetRadioBlackout != null && (bool)GetRadioBlackout.Invoke(null, new Object[] { id });
		}

		public static void update(Vessel v, vessel_info vi, VesselData vd, double elapsed_s)
		{
			if (!Enabled()) return;

			if (vi.blackout)
			{
				SetCommsBlackout(v.id, true, "kerbalism_cme");
			}
			else
			{
				SetCommsBlackout(v.id, false, "kerbalism_cme");
			}

		}

		// reflection type of SCANUtils static class in SCANsat assembly, if present
		static Type API;
		static System.Reflection.MethodInfo IsEnabled;
		static System.Reflection.MethodInfo IsConnected;
		static System.Reflection.MethodInfo IsConnectedKSC;
		static System.Reflection.MethodInfo ShortestSignalDelay;
		static System.Reflection.MethodInfo SetRadioBlackout;
		static System.Reflection.MethodInfo GetRadioBlackout;
	}


} // KERBALISM

