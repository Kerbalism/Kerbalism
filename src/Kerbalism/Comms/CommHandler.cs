using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using CommNet;
using HarmonyLib;

namespace KERBALISM
{
	public class CommHandler
	{
		private static bool CommNetStormPatchApplied = false;
		private bool resetTransmitters;
		protected VesselData vd;

		/// <summary>
		/// false while the network isn't initialized or when the transmitter list is not up-to-date
		/// </summary>
		public bool IsReady => NetworkIsReady && resetTransmitters == false;

		/// <summary>
		/// pseudo ctor for getting the right handler type
		/// </summary>
		public static CommHandler GetHandler(VesselData vd, bool isGroundController)
		{
			CommHandler handler;

			// Note : API CommHandlers may not be registered yet when this is called,
			// but this shouldn't be an issue, as the derived types UpdateTransmitters / UpdateNetwork
			// won't be called anymore once the API handler is registered.
			// This said, this isn't ideal, and it would be cleaner to have a "commHandledByAPI"
			// bool that mods should set once and for all before any vessel exist.

			if (!CommNetStormPatchApplied)
			{
				CommNetStormPatchApplied = true;

				if (API.Comm.handlers.Count == 0 && !RemoteTech.Installed)
				{
					CommNetStormPatch();
				}
			}

			if (API.Comm.handlers.Count > 0)
			{
				handler = new CommHandler();
				Lib.Log("created new CommHandler", Lib.LogLevel.Message);
			}
			else if (RemoteTech.Installed)
			{
				handler = new CommHandlerRemoteTech();
				Lib.Log("created new CommHandlerRemoteTech", Lib.LogLevel.Message);
			}
			else if (isGroundController)
			{
				handler = new CommHandlerCommNetSerenity();
				Lib.Log("created new CommHandlerCommNetSerenity", Lib.LogLevel.Message);
			}
			else {
				handler = new CommHandlerCommNetVessel();
				Lib.Log("created new CommHandlerCommNetVessel", Lib.LogLevel.Message);
			}
				
			handler.vd = vd;
			handler.resetTransmitters = true;

			return handler;
		}

		/// <summary> Update the provided Connection </summary>
		public void UpdateConnection(ConnectionInfo connection)
		{
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.CommHandler.UpdateConnection");

			UpdateInputs(connection);

			// Can this ever be anything other than 0? not atm, unless I'm missing something...
			if (API.Comm.handlers.Count == 0)
			{
				if (NetworkIsReady)
				{
					if (resetTransmitters)
					{
						UpdateTransmitters(connection, true);
						resetTransmitters = false;
					}
					else
					{
						UpdateTransmitters(connection, false);
					}

					UpdateNetwork(connection);
				}
			}
			else
			{
				try
				{
					API.Comm.handlers[0].Invoke(null, new object[] { connection, vd.Vessel });
				}
				catch (Exception e)
				{
					Lib.Log("CommInfo handler threw exception " + e.Message + "\n" + e.ToString(), Lib.LogLevel.Error);
				}
			}
			UnityEngine.Profiling.Profiler.EndSample();
		}

		/// <summary>
		/// Clear and re-find all transmitters partmodules on the vessel.
		/// Must be called when parts have been removed / added on the vessel.
		/// </summary>
		public void ResetPartTransmitters() => resetTransmitters = true;


		/// <summary>
		/// update the fields that can be used as an input by API handlers
		/// </summary>
		protected virtual void UpdateInputs(ConnectionInfo connection)
		{
			connection.transmitting = vd.filesTransmitted.Count > 0;
			connection.storm = vd.EnvStorm;
			//connection.powered = vd.ResHandler.ElectricCharge.CriticalConsumptionSatisfied;
			connection.powered = true;
		}

		protected virtual bool NetworkIsReady => true;

		protected virtual void UpdateNetwork(ConnectionInfo connection) { }

		protected virtual void UpdateTransmitters(ConnectionInfo connection, bool searchTransmitters) { }

		private static FieldInfo commNetVessel_inPlasma;
		private static FieldInfo commNetVessel_plasmaMult;

		private static void CommNetStormPatch()
		{
			commNetVessel_inPlasma = AccessTools.Field(typeof(CommNetVessel), "inPlasma");
			commNetVessel_plasmaMult = AccessTools.Field(typeof(CommNetVessel), "plasmaMult");

			MethodInfo CommNetVessel_OnNetworkPreUpdate_Info = AccessTools.Method(typeof(CommNetVessel), nameof(CommNetVessel.OnNetworkPreUpdate));
			MethodInfo CommNetVessel_OnNetworkPreUpdate_Postfix_Info = AccessTools.Method(typeof(CommHandler), nameof(CommNetVessel_OnNetworkPreUpdate_Postfix));

			//Loader.HarmonyInstance.Patch(CommNetVessel_OnNetworkPreUpdate_Info, null, new HarmonyMethod(CommNetVessel_OnNetworkPreUpdate_Postfix_Info));
		}

		private static void CommNetVessel_OnNetworkPreUpdate_Postfix(CommNetVessel __instance)
		{
			if (!__instance.Vessel.TryGetVesselData(out VesselData vd))
				return;

			if (vd.EnvStormRadiation > 0.0)
			{
				commNetVessel_inPlasma.SetValue(__instance, true);
				double stormIntensity = vd.EnvStormRadiation / PreferencesRadiation.Instance.StormRadiation;
				stormIntensity = Lib.Clamp(stormIntensity, 0.0, 1.0);
				commNetVessel_plasmaMult.SetValue(__instance, stormIntensity);
			}
		}
	}
}
