using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{

	/// <summary> signal connection link status </summary>
	public enum LinkStatus
	{
		direct_link,
		indirect_link,	// relayed signal
		no_link,
		plasma,			// plasma blackout on reentry
		storm			// cme storm blackout
	};


	/// <summary> Stores a single vessels communication info</summary>
	public sealed class ConnectionInfo
	{
		/// <summary> true if there is a connection back to DSN </summary>
		public bool linked = false;

		/// <summary> status of the connection </summary>
		public LinkStatus status = LinkStatus.no_link;

		/// <summary> science data rate. note that internal transmitters can not transmit science data only telemetry data </summary>
		public double rate = 0.0;

		/// <summary> internal transmitter ec cost (control and telemetry) </summary>
		public double internal_cost = 0.0;

		/// <summary> external transmitter ec cost </summary>
		public double external_cost = 0.0;

		/// <summary> signal strength, or when using RemoteTech signal delay </summary>
		public double strength = 0.0;

		/// <summary> receiving node name </summary>
		public string target_name = "";


		// constructor
		/// <summary> Creates a <see cref="ConnectionInfo"/> object for the specified vessel from it's antenna modules</summary>
		public ConnectionInfo(Vessel v, bool powered, bool storm)
		{
			// return no connection if there is no ec left
			if (!powered)
			{
				// hysteresis delay
				if ((DB.Vessel(v).hyspos_signal >= 5.0))
				{
					DB.Vessel(v).hyspos_signal = 5.0;
					DB.Vessel(v).hysneg_signal = 0.0;
					return;
				}
				DB.Vessel(v).hyspos_signal += 0.1;
			}
			else
			{
				// hysteresis delay
				DB.Vessel(v).hysneg_signal += 0.1;
				if (!(DB.Vessel(v).hysneg_signal >= 5.0))
					return;
				DB.Vessel(v).hysneg_signal = 5.0;
				DB.Vessel(v).hyspos_signal = 0.0;
			}

			List<ModuleDataTransmitter> transmitters;

			// if vessel is loaded
			if (v.loaded)
			{
				// find transmitters
				transmitters = v.FindPartModulesImplementing<ModuleDataTransmitter>();

				if (transmitters != null)
				{
					foreach (ModuleDataTransmitter t in transmitters)
					{
						if (t.antennaType == AntennaType.INTERNAL) // do not include internal data rate
							internal_cost += t.DataResourceCost * t.DataRate;
						else
						{
							// do we have an animation
							ModuleDeployableAntenna animation = t.part.FindModuleImplementing<ModuleDeployableAntenna>();
							if (animation != null)
							{
								// only include data rate if transmitter is extended
								if (animation.deployState == ModuleDeployablePart.DeployState.EXTENDED)
								{
									rate += t.DataRate;
									external_cost += t.DataResourceCost * t.DataRate;
								}
							}
							// no animation
							else
							{
								rate += t.DataRate;
								external_cost += t.DataResourceCost * t.DataRate;
							}
						}
					}
				}
			}

			// if vessel is not loaded
			else
			{
				// find proto transmitters
				foreach (ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
				{
					// get part prefab (required for module properties)
					Part part_prefab = PartLoader.getPartInfoByName(p.partName).partPrefab;

					transmitters = part_prefab.FindModulesImplementing<ModuleDataTransmitter>();

					if (transmitters != null)
					{
						foreach (ModuleDataTransmitter t in transmitters)
						{
							if (t.antennaType == AntennaType.INTERNAL) // do not include internal data rate
								internal_cost += t.DataResourceCost * t.DataRate;
							else
							{
								// do we have an animation
								ProtoPartModuleSnapshot m = p.FindModule("ModuleDeployableAntenna");
								if (m != null)
								{
									// only include data rate if transmitter is extended
									string deployState = Lib.Proto.GetString(m, "deployState");
									if (deployState == "EXTENDED")
									{
										rate += t.DataRate;
										external_cost += t.DataResourceCost * t.DataRate;
									}
								}
								// no animation
								else
								{
									rate += t.DataRate;
									external_cost += t.DataResourceCost * t.DataRate;
								}
							}
						}
					}
				}
			}

			// if CommNet is enabled
			if (HighLogic.fetch.currentGame.Parameters.Difficulty.EnableCommNet)
			{
				// are we connected to DSN
				if (v.connection != null)
				{
					if (v.connection.IsConnected)
					{
						linked = true;
						status = v.connection.ControlPath.First.hopType == CommNet.HopType.Home ? LinkStatus.direct_link : LinkStatus.indirect_link;
						strength = v.connection.SignalStrength;
						rate = rate * strength;
						target_name = Lib.Ellipsis(Localizer.Format(v.connection.ControlPath.First.end.displayName).Replace("Kerbin", "DSN"), 20);
						return;
					}

					// is loss of connection due to plasma blackout
					else if (Lib.ReflectionValue<bool>(v.connection, "inPlasma"))  // calling InPlasma causes a StackOverflow :(
					{
						status = LinkStatus.plasma;
						rate = 0.0;
						internal_cost = 0.0;
						external_cost = 0.0;
						return;
					}
				}

				// no connection
				rate = 0.0;
				internal_cost = 0.0;
				external_cost = 0.0;
				return;
			}

			// the simple stupid always connected signal system
			linked = true;
			status = LinkStatus.direct_link;
			strength = 1;    // 100 %
			target_name = "DSN: KSC";
		}
	}


} // KERBALISM
