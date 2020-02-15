using System;
using System.Collections.Generic;

namespace KERBALISM
{
	public class CommHandlerRemoteTech : CommHandler
	{
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
			// are we connected
			if (!RemoteTech.Connected(vd.VesselId))
			{
				connection.linked = false;

				// is loss of connection due to a blackout
				if (RemoteTech.GetCommsBlackout(vd.VesselId))
					connection.status = connection.storm ? (int)LinkStatus.storm : (int)LinkStatus.plasma;
				else
					connection.status = (int)LinkStatus.no_link;

				connection.strength = 0.0;
				connection.rate = 0.0;
				connection.target_name = string.Empty;
				connection.control_path.Clear();
				return;
			}

			connection.linked = RemoteTech.ConnectedToKSC(vd.VesselId);
			connection.status = RemoteTech.TargetsKSC(vd.VesselId) ? (int)LinkStatus.direct_link : (int)LinkStatus.indirect_link;
			connection.target_name = connection.status == (int)LinkStatus.direct_link ? Lib.Ellipsis("DSN: " + (RemoteTech.NameTargetsKSC(vd.VesselId) ?? ""), 20) :
				Lib.Ellipsis(RemoteTech.NameFirstHopToKSC(vd.VesselId) ?? "", 20);

			Guid[] controlPath = null;
			if (connection.linked) controlPath = RemoteTech.GetCommsControlPath(vd.VesselId);

			// Get the lowest rate in ControlPath
			if (controlPath != null)
			{
				// Get rate from the firstHop, each Hop will do the same logic, then we will have the lowest rate for the path
				if (controlPath.Length > 0)
				{
					double dist = RemoteTech.GetCommsDistance(vd.VesselId, controlPath[0]);
					double maxDist = RemoteTech.GetCommsMaxDistance(vd.VesselId, controlPath[0]);
					connection.strength = maxDist > 0.0 ? 1.0 - (dist / Math.Max(maxDist, 1.0)) : 0.0;
					connection.strength = Math.Pow(connection.strength, Settings.DataRateDampingExponentRT);

					connection.rate = baseRate * connection.strength;

					// If using relay, get the lowest rate
					if (connection.status != (int)LinkStatus.direct_link)
					{
						Vessel target = FlightGlobals.FindVessel(controlPath[0]);
						ConnectionInfo ci = target.KerbalismData().Connection;
						connection.strength *= ci.strength;
						connection.rate = Math.Min(ci.rate, connection.rate);
					}
				}

				connection.control_path = new List<string[]>();
				Guid i = vd.VesselId;
				foreach (Guid id in controlPath)
				{
					var name = Lib.Ellipsis(RemoteTech.GetSatelliteName(i) + " \\ " + RemoteTech.GetSatelliteName(id), 50);
					var value = Lib.HumanReadablePerc(Math.Ceiling((1 - (RemoteTech.GetCommsDistance(i, id) / RemoteTech.GetCommsMaxDistance(i, id))) * 10000) / 10000, "F2");
					var tooltip = "Distance: " + Lib.HumanReadableDistance(RemoteTech.GetCommsDistance(i, id)) +
						"\nMax Distance: " + Lib.HumanReadableDistance(RemoteTech.GetCommsMaxDistance(i, id));
					connection.control_path.Add(new string[] { name, value, tooltip });
					i = id;
				}
			}
		}

		protected override void UpdateTransmitters(ConnectionInfo connection, bool searchTransmitters)
		{
			Vessel v = vd.Vessel;

			if (RemoteTech.Enabled)
			{
				RemoteTech.SetPoweredDown(v.id, !connection.powered);
				RemoteTech.SetCommsBlackout(v.id, connection.storm);
			}

			baseRate = 1.0;
			connection.ec = 0.0;
			connection.ec_idle = 0.0;
			int transmitterCount = 0;

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
					else if (pm.moduleName == "ModuleRTAntenna")
					{
						ModuleResource mResource = pm.resHandler.inputResources.Find(r => r.name == "ElectricCharge");
						// only include data rate and ec cost if transmitter is active
						if (Lib.ReflectionValue<bool>(pm, "IsRTActive"))
						{
							baseRate *= (Lib.ReflectionValue<float>(pm, "RTPacketSize") / Lib.ReflectionValue<float>(pm, "RTPacketInterval"));
							connection.ec += mResource.rate;
							transmitterCount++;
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
						ModuleResource mResource = mdt.prefab.resHandler.inputResources.Find(r => r.name == "ElectricCharge");
						float? packet_size = Lib.SafeReflectionValue<float>(mdt.prefab, "RTPacketSize");
						float? packet_Interval = Lib.SafeReflectionValue<float>(mdt.prefab, "RTPacketInterval");

						if (!Lib.Proto.GetBool(mdt.protoTransmitter, "IsRTActive", false))
							continue;

						if (mResource != null && packet_size != null && packet_Interval != null)
						{
							baseRate *= (float)packet_size / (float)packet_Interval;
							connection.ec += mResource.rate;
							transmitterCount++;
						}
					}
				}
			}

			if (transmitterCount > 1)
				baseRate = Math.Pow(baseRate, 1.0 / transmitterCount);
			else if (transmitterCount == 0)
				baseRate = 0.0;

			// when transmitting, transmitters need more EC for the signal amplifiers.
			// while not transmitting, transmitters only use 10-20% of that
			if (!v.loaded)
			{
				connection.ec_idle += connection.ec;
				connection.ec *= Settings.TransmitterActiveEcFactor;
			}
			else
			{
				connection.ec = (connection.ec * Settings.TransmitterActiveEcFactor) - connection.ec;
			}
		}

		private void GetTransmittersLoaded(Vessel v)
		{
			foreach (Part p in v.parts)
			{
				foreach (PartModule pm in p.Modules)
				{
					if (pm.moduleName == "ModuleRTAntennaPassive" || pm.moduleName == "ModuleRTAntenna")
						loadedTransmitters.Add(pm);
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
