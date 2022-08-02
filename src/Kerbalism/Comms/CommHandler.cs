using System;

namespace KERBALISM
{
	public class CommHandler
	{
		private static bool CommNetStormPatchApplied = false;

		protected VesselData vd;

		private bool transmittersDirty;

		/// <summary>
		/// false while the network isn't initialized or when the transmitter list is not up-to-date
		/// </summary>
		public bool IsReady => NetworkIsReady && !transmittersDirty;

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
					CommHandlerCommNetBase.ApplyHarmonyPatches();
				}
			}

			if (API.Comm.handlers.Count > 0)
			{
				handler = new CommHandler();
				Lib.LogDebug("Created new API CommHandler", Lib.LogLevel.Message);
			}
			else if (RemoteTech.Installed)
			{
				handler = new CommHandlerRemoteTech();
				Lib.LogDebug("Created new CommHandlerRemoteTech", Lib.LogLevel.Message);
			}
			else if (isGroundController)
			{
				handler = new CommHandlerCommNetSerenity();
				Lib.LogDebug("Created new CommHandlerCommNetSerenity", Lib.LogLevel.Message);
			}
			else
			{
				handler = new CommHandlerCommNetVessel();
				Lib.LogDebug("Created new CommHandlerCommNetVessel", Lib.LogLevel.Message);
			}
				
			handler.vd = vd;
			handler.transmittersDirty = true;

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
					if (transmittersDirty)
					{
						UpdateTransmitters(connection, true);
						transmittersDirty = false;
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
				transmittersDirty = false;
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
		public void ResetPartTransmitters() => transmittersDirty = true;

		/// <summary>
		/// Get the cost for transmitting data with this CommHandler
		/// </summary>
		/// <param name="transmittedTotal">Amount of the total capacity of data that can be sent</param>
		/// <param name="elapsed_s"></param>
		/// <returns></returns>
		public virtual double GetTransmissionCost(double transmittedTotal, double elapsed_s)
		{
			return (vd.Connection.ec - vd.Connection.ec_idle) * (transmittedTotal / (vd.Connection.rate * elapsed_s));
		}

		/// <summary>
		/// update the fields that can be used as an input by API handlers
		/// </summary>
		protected virtual void UpdateInputs(ConnectionInfo connection)
		{
			connection.transmitting = vd.filesTransmitted.Count > 0;
			connection.storm = vd.EnvStorm;
			connection.powered = vd.Powered;
		}

		protected virtual bool NetworkIsReady => true;

		protected virtual void UpdateNetwork(ConnectionInfo connection) { }

		protected virtual void UpdateTransmitters(ConnectionInfo connection, bool searchTransmitters) { }
	}
}
