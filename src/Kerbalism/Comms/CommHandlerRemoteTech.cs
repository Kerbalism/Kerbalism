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
				connection.target_name = string.Empty;
				connection.control_path.Clear();
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
				connection.control_path.Clear();
			}
			else
			{
				if (controlPath.Length > 0)
				{
					connection.control_path.Clear();
					Guid currentID = vd.VesselId;
					double lowestStrength = double.MaxValue;

					// since RemoteTech doesn't have a concept of signal strength like CommNet, we have to iterate
					// over the connection chain to find the link with the lowest connection strenth.
					// this will give correct values even if this vessel is directly connected to KSC
					foreach (Guid iterationID in controlPath)
					{
						double dist = RemoteTech.GetCommsDistance(currentID, iterationID);
						double maxDist = RemoteTech.GetCommsMaxDistance(currentID, iterationID);
						double linkStrength = maxDist > 0.0 ? 1.0 - (dist / Math.Max(maxDist, 1.0)) : 0.0;
						linkStrength = Math.Pow(linkStrength, Settings.DataRateDampingExponentRT);
						if(linkStrength < lowestStrength)
							lowestStrength = linkStrength;

						// Add all values to the UI element
						var name = Lib.Ellipsis(RemoteTech.GetSatelliteName(currentID) + " \\ " + RemoteTech.GetSatelliteName(iterationID), 50);
						var value = Lib.HumanReadablePerc(Math.Ceiling(linkStrength * 10000) / 10000, "F2");
						var tooltip = "Distance: " + Lib.HumanReadableDistance(dist) + "\nMax Distance: " + Lib.HumanReadableDistance(maxDist);
						connection.control_path.Add(new string[] { name, value, tooltip });

						currentID = iterationID;
					}

					connection.strength = lowestStrength;
					connection.rate = baseRate * lowestStrength;
				}
			}

			// set minimal data rate to what is defined in Settings (1 bit/s by default) 
			if (connection.rate > 0.0 && connection.rate * bitsPerMB < Settings.DataRateMinimumBitsPerSecond)
				connection.rate = Settings.DataRateMinimumBitsPerSecond / bitsPerMB;
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
							if(dataRate > highestDataRate)
							{
								baseRate = highestDataRate;
								float? packetResourceCost = Lib.SafeReflectionValue<float>(mdt.prefab, "RTPacketResourceCost");
								if(packetResourceCost != null)
									connection.ec = dataRate * (float)packetResourceCost;
							}
						}
					}
				}
			}

			// With RemoteTech it is very common to build satellites with 3 or more relay antennas becuase of the fact the they
			// can only point in a certain direction, usually only covering a single target with their small cone of vision.
			// The compensation that RemoteTech users see is that many of the later dishes can reach longer than CommNet counterparts
			// at the cost of a much higher energy consumption for even establishing connection without transferring data.
			// I think it would be too much to add the "transmission energy" consumption of all the active antennas on the vessel even though
			// there will only be one of these antennas that actually is transmitting the data. So find the one with the highest data rate
			// and apply the transmissionCost per package for that one.

			//connection.ec_idle *= Settings.TransmitterPassiveEcFactor; // apply passive factor to "internal" antennas always-consumed rate
			//connection.ec_idle += connection.ec * Settings.TransmitterPassiveEcFactor; // add "transmit" antennas always-consumed rate
			//connection.ec *= Settings.TransmitterActiveEcFactor; // adjust "transmit" antennas transmit-only rate by the factor

			// This is what RemoteTech users are used to, high energy costs just to have antenna running even without transferring data
			connection.ec += connection.ec_idle;

			connection.hasActiveAntenna = connection.ec_idle > 0.0;
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
