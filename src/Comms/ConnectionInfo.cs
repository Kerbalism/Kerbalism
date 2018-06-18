using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{


	public enum LinkStatus    // link state
	{
		direct_link,
		indirect_link,
		no_link,
		blackout
	};

	public sealed class ConnectionInfo
	{
		public ConnectionInfo() { }

		public ConnectionInfo(LinkStatus status, double rate, double strength, double internal_cost, double science_cost, string target_name)
		{
			this.linked = status == LinkStatus.direct_link || status == LinkStatus.indirect_link;
			this.status = status;
			this.rate = rate;
			this.strength = strength;
			this.internal_cost = internal_cost;
			this.science_cost = science_cost;
			this.target_name = target_name;
		}

		public ConnectionInfo(Vessel v)
		{
			// return no connection if there is no ec left
			if (ResourceCache.Info(v, "ElectricCharge").amount <= double.Epsilon)
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
								if (animation.status == "Extended")
								{
									rate += t.DataRate;
									science_cost += t.DataResourceCost * t.DataRate;
								}
							}
							// no animation
							else
							{
								rate += t.DataRate;
								science_cost += t.DataResourceCost * t.DataRate;
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
										science_cost += t.DataResourceCost * t.DataRate;
									}
								}
								// no animation
								else
								{
									rate += t.DataRate;
									science_cost += t.DataResourceCost * t.DataRate;
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
					else if (Lib.PrivateField<bool>(v.connection, "inPlasma"))  // calling InPlasma causes a StackOverflow :(
					{
						status = LinkStatus.blackout;
						rate = 0.0;
						internal_cost = 0.0;
						science_cost = 0.0;
						return;
					}
				}

				// no connection
				rate = 0.0;
				internal_cost = 0.0;
				science_cost = 0.0;
				return;
			}

			// the simple stupid always connected signal system
			linked = true;
			status = LinkStatus.direct_link;
			strength = 1;    // 100 %
			target_name = "DSN: KSC";
		}

		public bool linked = false;                       // true if there is a connection back to DSN
		public LinkStatus status = LinkStatus.no_link;    // the link status
		public double rate = 0.0;                         // science data rate, internal transmitters can not transmit science data only telemetry data
		public double internal_cost = 0.0;                // control and telemetry ec cost
		public double science_cost = 0.0;                 // science ec cost
		public double strength = 0.0;                     // signal strength
		public string target_name = "";                   // receiving node name
	}


} // KERBALISM
