namespace KERBALISM
{
	/// <summary> Return antenna info for RemoteTech </summary>
	public sealed class AntennaInfoRT
	{
		/// <summary> science data rate. note that internal transmitters can not transmit science data only telemetry data </summary>
		public double rate = 0.0;

		/// <summary> ec cost </summary>
		public double ec = 0.0;

		public AntennaInfoRT(Vessel v)
		{
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
					int index = 0;      // module index

					foreach (ProtoPartModuleSnapshot m in p.modules)
					{
						// calculate internal (passive) transmitter ec usage @ 0.5W each
						if (m.moduleName == "ModuleRTAntennaPassive") ec += 0.0005;
						// calculate external transmitters
						else if (m.moduleName == "ModuleRTAntenna")
						{
							// only include data rate and ec cost if transmitter is active skip if index is out of range
							if (Lib.Proto.GetBool(m, "IsRTActive") && index < part_prefab.Modules.Count)
							{
								// get module prefab
								PartModule pm = part_prefab.Modules.GetModule(index);

								if (pm != null)
								{
									ec += pm.resHandler.inputResources.Find(r => r.name == "ElectricCharge").rate;
									float? packet_size = Lib.SafeReflectionValue<float>(pm, "RTPacketSize");
									// workaround for old savegames
									if (packet_size == null)
									{
										Lib.DebugLog("Old SaveGame PartModule ModuleRTAntenna for part {0} on unloaded vessel {1}, using default values as a workaround", p.partName, v.vesselName);
										rate += 6.6666;          // 6.67 Mb/s in 100% factor
									}
									else
									{
										rate += ((float)packet_size / Lib.ReflectionValue<float>(pm, "RTPacketInterval"));
									}
								}
								else
								{
									Lib.DebugLog("Could not find PartModule ModuleRTAntenna for part {0} on unloaded vessel {1}, using default values as a workaround", p.partName, v.vesselName);
									rate += 6.6666;          // 6.67 Mb/s in 100% factor
									ec += 0.025;             // 25 W/s
								}
							}
						}
						index++;
					}
				}
			}
		}
	}
}
