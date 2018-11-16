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
											Lib.DebugLog("Old SaveGame PartModule ModuleRTAntenna for part '{0}' on unloaded vessel '{1}', using default values as a workaround", p.partName, v.vesselName);
											Lib.DebugLog("ElectricCharge isNull: '{0}', RTPacketSize isNull: '{1}', RTPacketInterval isNull: '{2}'", mResource == null, packet_size == null, packet_Interval == null);
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
									Lib.DebugLog("Could not find PartModule ModuleRTAntenna for part {0} on unloaded vessel {1}, using default values as a workaround", p.partName, v.vesselName);
									rate += 6.6666;          // 6.67 Mb/s in 100% factor
									ec += 0.025;             // 25 W/s
								}
							}
						}
					}
				}
			}
		}
	}
}
