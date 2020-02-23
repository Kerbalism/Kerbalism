using System;

namespace KERBALISM
{
	public class CommHandler
	{
		private bool resetTransmitters;
		protected VesselData vd;

		/// <summary>
		/// false while the network isn't initialized or when the transmitter list is not up-to-date
		/// </summary>
		public bool IsReady => NetworkIsReady && resetTransmitters == false;

		/// <summary>
		/// pseudo ctor for getting the right handler type
		/// </summary>
		public static CommHandler GetProvider(VesselData vd, bool isGroundController)
		{
			CommHandler handler;

			// Note : API CommHandlers may not be registered yet when this is called,
			// but this shouldn't be an issue, as the derived types UpdateTransmitters / UpdateNetwork
			// won't be called anymore once the API handler is registered.
			// This said, this isn't ideal, and it would be cleaner to have a "commHandledByAPI"
			// bool that mods should set once and for all before any vessel exist.
			if (API.Comm.handlers.Count > 0)
				handler = new CommHandler();
			else if (RemoteTech.Installed)
				handler = new CommHandlerRemoteTech();
#if !KSP15_16
			else if (isGroundController)
				handler = new CommHandlerCommNetSerenity();
#endif
			else
				handler = new CommProviderCommNetVessel();

			handler.vd = vd;
			handler.resetTransmitters = true;

			return handler;
		}

		/// <summary> Update the provided Connection </summary>
		public void UpdateConnection(ConnectionInfo connection)
		{
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.CommHandler.UpdateConnection");

			UpdateInputs(connection);

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
			connection.powered = vd.ResHandler.ElectricCharge.CriticalConsumptionSatisfied;
		}

		protected virtual bool NetworkIsReady => true;

		protected virtual void UpdateNetwork(ConnectionInfo connection) { }

		protected virtual void UpdateTransmitters(ConnectionInfo connection, bool searchTransmitters) { }
	}
}
