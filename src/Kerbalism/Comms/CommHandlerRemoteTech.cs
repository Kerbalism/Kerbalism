using System;
using System.Collections.Generic;

namespace KERBALISM
{
	public class CommHandlerRemoteTech : CommHandler
	{
		const double bitsPerMB = 1024.0 * 1024.0 * 8.0;

		private double baseRate;

		private class UnloadedTransmitter
		{
			public PartModule prefab;
			public ProtoPartModuleSnapshot protoTransmitter;

			public UnloadedTransmitter(PartModule prefab, ProtoPartModuleSnapshot protoTransmitter)
			{
				this.prefab = prefab;
				this.protoTransmitter = protoTransmitter;
			}
		}

		private List<UnloadedTransmitter> unloadedTransmitters;
		private List<PartModule> loadedTransmitters;

		protected override bool NetworkIsReady => RemoteTech.Enabled;

		protected override void UpdateNetwork(ConnectionInfo connection)
		{
			// if we're NOT connected
			if (!RemoteTech.Connected(vd.VesselId))
			{
				connection.linked = false;

				// is loss of connection due to a blackout
				if (RemoteTech.GetCommsBlackout(vd.VesselId))
					connection.Status = connection.storm ? LinkStatus.storm : LinkStatus.plasma;
				else
					connection.Status = LinkStatus.no_link;

				connection.strength = 0.0;
				connection.rate = 0.0;
				connection.hop_datarate = 0.0;
				connection.target_name = string.Empty;
				connection.next_hop = Guid.Empty;
				connection.hop_distance = 0.0;
				connection.hop_max_distance = 0.0;
				return;
			}

			connection.linked = RemoteTech.ConnectedToKSC(vd.VesselId);
			connection.Status = RemoteTech.TargetsKSC(vd.VesselId) ? LinkStatus.direct_link : LinkStatus.indirect_link;
			connection.target_name = connection.Status == LinkStatus.direct_link ? Lib.Ellipsis("DSN: " + (RemoteTech.NameTargetsKSC(vd.VesselId) ?? ""), 20) :
				Lib.Ellipsis(RemoteTech.NameFirstHopToKSC(vd.VesselId) ?? "", 20);

			Guid[] controlPath = null;
			if (connection.linked) controlPath = RemoteTech.GetCommsControlPath(vd.VesselId);

			// Get the lowest rate in ControlPath
			if (controlPath == null)
			{
				connection.strength = 0.0;
				connection.rate = 0.0;
				connection.hop_datarate = 0.0;
				connection.next_hop = Guid.Empty;
				connection.hop_distance = 0.0;
				connection.hop_max_distance = 0.0;
			}
			else
			{
				if (controlPath.Length > 0)
				{
					var nextHop = controlPath[0];

					double dist = RemoteTech.GetCommsDistance(vd.VesselId, nextHop);
					double maxDist = RemoteTech.GetCommsMaxDistance(vd.VesselId, nextHop);
					connection.strength = maxDist > 0.0 ? 1.0 - (dist / Math.Max(maxDist, 1.0)) : 0.0;
					connection.strength = Math.Pow(connection.strength, Sim.DataRateDampingExponentRT);

					connection.hop_datarate = baseRate * connection.strength;
					connection.rate = connection.hop_datarate;

					connection.hop_distance = RemoteTech.GetCommsDistance(vd.VesselId, nextHop);
					connection.hop_max_distance = RemoteTech.GetCommsMaxDistance(vd.VesselId, nextHop);

					// If using relay, get the lowest rate
					if (connection.Status != LinkStatus.direct_link)
					{
						// Check the connection link on the next vessel in the connection chain
						// to find the lowest data rate, this value can be one tick inaccurate depending on order of
						// processing of vessel connections, but is negligible
						Vessel target = FlightGlobals.FindVessel(nextHop);
						target.TryGetVesselDataTemp(out VesselData vd);
						ConnectionInfo ci = vd.Connection;
						connection.rate = Math.Min(ci.rate, connection.rate);

						// Also tell the ConnManager about it.
						connection.next_hop = nextHop;
					}
					else
						connection.next_hop = Guid.Empty; // Tell the ConnManager we are directly connected to the KSC.
				}

			}

			// set minimal data rate to what is defined in Settings (1 bit/s by default) 
			if (connection.rate > 0.0 && connection.rate * bitsPerMB < Settings.DataRateMinimumBitsPerSecond)
				connection.rate = Settings.DataRateMinimumBitsPerSecond / bitsPerMB;

			if (connection.hop_datarate > 0.0 && connection.hop_datarate * Lib.bitsPerMB < Settings.DataRateMinimumBitsPerSecond)
				connection.hop_datarate = Settings.DataRateMinimumBitsPerSecond / Lib.bitsPerMB;
		}

		protected override void UpdateTransmitters(ConnectionInfo connection, bool searchTransmitters)
		{
			Vessel v = vd.Vessel;

			if (RemoteTech.Enabled)
			{
				RemoteTech.SetPoweredDown(v.id, !connection.powered);
				RemoteTech.SetCommsBlackout(v.id, connection.storm);
			}

			baseRate = 0.0;
			connection.ec = 0.0;
			connection.ec_idle = 0.0;
			double highestDataRate = double.Epsilon;

			// Since RemoteTech is all about building comm networks by pointing dishes, we need to tweak some things.
			// Before we multiplied all transmitters together and then calculated a kind of an average for the rate,
			// now, we look for the highest transmitter available that is activated on the vessel currently.
			// this one will also determine the additional transmission energy cost with its packetResourceCost value.
			// The best way would be if RemoteTech provided API to get all the antenna partModules that actually make
			// up the current connection chain, then check for the lowest transmission rate, consume energy on all of them etc...
			if (v.loaded)
			{
				if (loadedTransmitters == null)
				{
					loadedTransmitters = new List<PartModule>();
					GetTransmittersLoaded(v);
				}
				else if (searchTransmitters)
				{
					loadedTransmitters.Clear();
					GetTransmittersLoaded(v);
				}

				if (unloadedTransmitters != null)
					unloadedTransmitters = null;

				foreach (PartModule pm in loadedTransmitters)
				{
					// calculate internal (passive) transmitter ec usage @ 0.5W each
					if (pm.moduleName == "ModuleRTAntennaPassive")
					{
						connection.ec_idle += 0.0005;
					}
					// calculate external transmitters
					else
					{
						// only include data rate and ec cost if transmitter is active
						if (Lib.ReflectionValue<bool>(pm, "IsRTActive"))
						{
							double dataRate = (Lib.ReflectionValue<float>(pm, "RTPacketSize") / Lib.ReflectionValue<float>(pm, "RTPacketInterval"));
							connection.ec_idle += RemoteTech.GetModuleRTAntennaConsumption(pm);
							// Transmit data only with the antenna that has highest transmission data rate, and also consume that
							// antennas energy transmission rate, based on the antenna's efficiency in configs
							if (dataRate > highestDataRate)
							{
								highestDataRate = dataRate;
								baseRate = dataRate;
								float packetResourceCost = Lib.ReflectionValue<float>(pm, "RTPacketResourceCost");
								connection.ec = dataRate * packetResourceCost;
							}
						}
					}
				}
			}
			else
			{
				if (unloadedTransmitters == null)
				{
					unloadedTransmitters = new List<UnloadedTransmitter>();
					GetTransmittersUnloaded(v);
				}
				else if (searchTransmitters)
				{
					unloadedTransmitters.Clear();
					GetTransmittersUnloaded(v);
				}

				if (loadedTransmitters != null)
					loadedTransmitters = null;

				foreach (UnloadedTransmitter mdt in unloadedTransmitters)
				{
					if (mdt.protoTransmitter.moduleName == "ModuleRTAntennaPassive")
					{
						connection.ec_idle += 0.0005;
					}
					else
					{
						if (!Lib.Proto.GetBool(mdt.protoTransmitter, "IsRTActive", false))
							continue;

						float? packet_size = Lib.SafeReflectionValue<float>(mdt.prefab, "RTPacketSize");
						float? packet_Interval = Lib.SafeReflectionValue<float>(mdt.prefab, "RTPacketInterval");

						if (packet_size != null && packet_Interval != null)
						{
							double dataRate = (float)packet_size / (float)packet_Interval;
							connection.ec_idle += RemoteTech.GetModuleRTAntennaConsumption(mdt.prefab);
							if (dataRate > highestDataRate)
							{
								float? packetResourceCost = Lib.SafeReflectionValue<float>(mdt.prefab, "RTPacketResourceCost");
								if (packetResourceCost != null)
								{
									highestDataRate = dataRate;
									baseRate = dataRate;
									connection.ec = dataRate * (float)packetResourceCost;
								}
							}
						}
					}
				}
			}

			// Apply Antenna Speed value from ksp in game settings
			baseRate *= PreferencesScience.Instance.transmitFactor;

			// With RemoteTech it is very common to build satellites with 3 or more relay antennas becuase of the fact the they
			// can only point in a certain direction, usually only covering a single target with their small cone of vision.
			// The compensation that RemoteTech users see is that many of the later dishes can reach longer than CommNet counterparts
			// at the cost of a much higher energy consumption for even establishing connection without transferring data.
			// I think it would be too much to add the "transmission energy" consumption of all the active antennas on the vessel even though
			// there will only be one of these antennas that actually is transmitting the data. So find the one with the highest data rate
			// and apply the transmissionCost per package for that one.

			// This is what RemoteTech users are used to, high energy costs just to have antenna running even without transferring data
			connection.ec_idle *= Settings.TransmitterPassiveEcFactorRT; // apply passive factor to "internal" antennas always-consumed rate
			connection.ec += connection.ec_idle;
			connection.ec *= Settings.TransmitterActiveEcFactorRT;

			connection.hasActiveAntenna = connection.ec_idle > 0.0;
		}

		public override double GetTransmissionCost(double transmittedTotal, double elapsed_s)
		{
			// I think a better model to calculate transmission cost with RemoteTech is to factor in the distance of
			// transmission. An antenna wouldn't have to "scream" as loud if the recieving satellite or station were close.

			// The Connection.strength value is already prepared with the correct value to the next satellite in the chain at this point
			// Better connection strength means cheaper transmission energy cost, 5% at minimum
			double strength = vd.Connection.strength < 0.95 ? vd.Connection.strength : 0.95;
			return ((vd.Connection.ec - vd.Connection.ec_idle) * (1 - strength)) * (transmittedTotal / (vd.Connection.rate * elapsed_s));
		}

		private void GetTransmittersLoaded(Vessel v)
		{
			foreach (Part p in v.parts)
			{
				foreach (PartModule pm in p.Modules)
				{
					if (pm.moduleName == "ModuleRTAntennaPassive")
					{
						loadedTransmitters.Add(pm);
					}
					else if (pm.moduleName == "ModuleRTAntenna")
					{
						pm.Events["EventTransmit"].active = false;
						pm.resHandler.inputResources.Clear(); // we handle consumption by ourselves
						loadedTransmitters.Add(pm);
					}
				}
			}
		}

		private void GetTransmittersUnloaded(Vessel v)
		{
			foreach (ProtoPartSnapshot pps in v.protoVessel.protoPartSnapshots)
			{
				// get part prefab (required for module properties)
				Part part_prefab = pps.partInfo.partPrefab;

				for (int i = 0; i < part_prefab.Modules.Count; i++)
				{
					if (part_prefab.Modules[i].moduleName == "ModuleRTAntenna")
					{
						ProtoPartModuleSnapshot ppms;
						if (i < pps.modules.Count && pps.modules[i].moduleName == "ModuleRTAntenna")
						{
							ppms = pps.modules[i];
						}
						// fallback in case the module indexes are messed up
						else
						{
							ppms = pps.FindModule("ModuleRTAntenna");
							Lib.LogDebug($"Could not find a ModuleRTAntenna at index {i} on part {pps.partName} on vessel {v.protoVessel.vesselName}", Lib.LogLevel.Warning);
						}

						if (ppms != null)
						{
							unloadedTransmitters.Add(new UnloadedTransmitter(part_prefab.Modules[i], ppms));
						}
					}
					else if (part_prefab.Modules[i].moduleName == "ModuleRTAntennaPassive")
					{
						ProtoPartModuleSnapshot ppms;
						if (i < pps.modules.Count && pps.modules[i].moduleName == "ModuleRTAntennaPassive")
						{
							ppms = pps.modules[i];
						}
						// fallback in case the module indexes are messed up
						else
						{
							ppms = pps.FindModule("ModuleRTAntennaPassive");
							Lib.LogDebug($"Could not find a ModuleRTAntennaPassive at index {i} on part {pps.partName} on vessel {v.protoVessel.vesselName}", Lib.LogLevel.Warning);
						}

						if (ppms != null)
						{
							unloadedTransmitters.Add(new UnloadedTransmitter(part_prefab.Modules[i], ppms));
						}
					}
				}
			}
		}

	}
}
