using System;
using System.Collections.Generic;

namespace KERBALISM
{
	/// <summary> Return antenna info for RemoteTech </summary>
	public sealed class AntennaInfoRT: AntennaInfo
	{
		public AntennaInfoRT(Vessel v, bool powered, bool storm)
		{
			RemoteTech.SetPoweredDown(v.id, !powered);
			RemoteTech.SetCommsBlackout(v.id, storm);

			// if vessel is loaded, don't calculate ec, RT already handle it.
			if (v.loaded)
			{
				// find transmitters
				foreach (Part p in v.parts)
				{
					foreach (PartModule m in p.Modules)
					{
						// calculate internal (passive) transmitter ec usage @ 0.5W each
						if (m.moduleName == "ModuleRTAntennaPassive") ec += 0.0005;
						// calculate external transmitters
						else if (m.moduleName == "ModuleRTAntenna")
						{
							// only include data rate and ec cost if transmitter is active
							if (Lib.ReflectionValue<bool>(m, "IsRTActive"))
							{
								rate += (Lib.ReflectionValue<float>(m, "RTPacketSize") / Lib.ReflectionValue<float>(m, "RTPacketInterval"));
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
					foreach (ProtoPartModuleSnapshot m in p.modules)
					{
						// calculate internal (passive) transmitter ec usage @ 0.5W each
						if (m.moduleName == "ModuleRTAntennaPassive") ec += 0.0005;
						// calculate external transmitters
						else if (m.moduleName == "ModuleRTAntenna")
						{
							// only include data rate and ec cost if transmitter is active
							if (Lib.Proto.GetBool(m, "IsRTActive"))
							{
								bool mFound = false;
								// get all modules in prefab
								foreach (PartModule pm in part_prefab.Modules)
								{
									if (pm.moduleName == m.moduleName)
									{
										mFound = true;
										ModuleResource mResource = pm.resHandler.inputResources.Find(r => r.name == "ElectricCharge");
										float? packet_size = Lib.SafeReflectionValue<float>(pm, "RTPacketSize");
										float? packet_Interval = Lib.SafeReflectionValue<float>(pm, "RTPacketInterval");

										// workaround for old savegames
										if (mResource == null || packet_size == null || packet_Interval == null)
										{
											Lib.LogDebugStack("Old SaveGame PartModule ModuleRTAntenna for part '{0}' on unloaded vessel '{1}', using default values as a workaround", p.partName, v.vesselName);
											Lib.LogDebugStack("ElectricCharge isNull: '{0}', RTPacketSize isNull: '{1}', RTPacketInterval isNull: '{2}'", mResource == null, packet_size == null, packet_Interval == null);
											rate += 6.6666;          // 6.67 Mb/s in 100% factor
											ec += 0.025;             // 25 W/s
										}
										else
										{
											rate += (float)packet_size / (float)packet_Interval;
											ec += mResource.rate;
										}
									}
								}
								if (!mFound)
								{
									Lib.LogDebugStack("Could not find PartModule ModuleRTAntenna for part {0} on unloaded vessel {1}, using default values as a workaround", p.partName, v.vesselName);
									rate += 6.6666;          // 6.67 Mb/s in 100% factor
									ec += 0.025;             // 25 W/s
								}
							}
						}
					}
				}
			}

			Init(v, powered, storm);
		}

		private void Init(Vessel v, bool powered, bool storm)
		{
			// are we connected
			if (RemoteTech.Connected(v.id))
			{
				linked = RemoteTech.ConnectedToKSC(v.id);
				status = RemoteTech.TargetsKSC(v.id) ? (int)LinkStatus.direct_link : (int)LinkStatus.indirect_link;
				target_name = status == (int)LinkStatus.direct_link ? Lib.Ellipsis("DSN: " + (RemoteTech.NameTargetsKSC(v.id) ?? ""), 20) :
					Lib.Ellipsis(RemoteTech.NameFirstHopToKSC(v.id) ?? "", 20);

				Guid[] controlPath = null;
				if (linked) controlPath = RemoteTech.GetCommsControlPath(v.id);

				// Get the lowest rate in ControlPath
				if (controlPath != null)
				{
					// Get rate from the firstHop, each Hop will do the same logic, then we will have the lowest rate for the path
					if (controlPath.Length > 0)
					{
						double dist = RemoteTech.GetCommsDistance(v.id, controlPath[0]);
						strength = 1 - (dist / Math.Max(RemoteTech.GetCommsMaxDistance(v.id, controlPath[0]), 1));
						strength = Math.Pow(strength, Settings.DataRateDampingExponentRT);

						// If using relay, get the lowest rate
						if (status != (int)LinkStatus.direct_link)
						{
							Vessel target = FlightGlobals.FindVessel(controlPath[0]);
							ConnectionInfo ci = target.KerbalismData().Connection;
							strength *= ci.strength;
							rate = Math.Min(ci.rate, rate * strength);
						}
						else rate *= strength;
					}

					control_path = new List<string[]>();
					Guid i = v.id;
					foreach (Guid id in controlPath)
					{
						var name = Lib.Ellipsis(RemoteTech.GetSatelliteName(i) + " \\ " + RemoteTech.GetSatelliteName(id), 35);
						var value = Lib.HumanReadablePerc(Math.Ceiling((1 - (RemoteTech.GetCommsDistance(i, id) / RemoteTech.GetCommsMaxDistance(i, id))) * 10000) / 10000, "F2");
						var tooltip = "Distance: " + Lib.HumanReadableDistance(RemoteTech.GetCommsDistance(i, id)) +
							"\nMax Distance: " + Lib.HumanReadableDistance(RemoteTech.GetCommsMaxDistance(i, id));
						control_path.Add(new string[] { name, value, tooltip });
						i = id;
					}
				}
			}
			// is loss of connection due to a blackout
			else if (RemoteTech.GetCommsBlackout(v.id))
			{
				status = storm ? (int)LinkStatus.storm : (int)LinkStatus.plasma;
			}
		}
	}
}
