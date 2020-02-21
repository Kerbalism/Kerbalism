using System;
using System.Collections.Generic;

namespace KERBALISM
{
	public class CommProviderCommNetVessel : CommHandlerCommNetBase
	{
		private class UnloadedTransmitter
		{
			public ModuleDataTransmitter prefab;
			public ProtoPartModuleSnapshot protoTransmitter;

			public UnloadedTransmitter(ModuleDataTransmitter prefab, ProtoPartModuleSnapshot protoTransmitter)
			{
				this.prefab = prefab;
				this.protoTransmitter = protoTransmitter;
			}
		}

		private List<UnloadedTransmitter> unloadedTransmitters;
		private List<ModuleDataTransmitter> loadedTransmitters;

		protected override void UpdateTransmitters(ConnectionInfo connection, bool searchTransmitters)
		{
			Vessel v = vd.Vessel;

			baseRate = 1.0;
			connection.ec_idle = 0.0;
			connection.ec = 0.0;

			int transmitterCount = 0;

			if (v.loaded)
			{
				if (loadedTransmitters == null)
				{
					loadedTransmitters = new List<ModuleDataTransmitter>();
					GetTransmittersLoaded(v);
				}
				else if (searchTransmitters)
				{
					loadedTransmitters.Clear();
					GetTransmittersLoaded(v);
				}

				if (unloadedTransmitters != null)
					unloadedTransmitters = null;

				foreach (ModuleDataTransmitter mdt in loadedTransmitters)
				{
					// CanComm method : check if module has moduleIsEnabled = false or is broken or not deployed
					if (!mdt.isEnabled || !mdt.CanComm())
						continue;

					// do not include internal data rate, ec cost only
					if (mdt.antennaType == AntennaType.INTERNAL)
					{
						connection.ec_idle += mdt.DataResourceCost * mdt.DataRate;
					}
					else
					{
						baseRate *= mdt.DataRate;
						connection.ec += mdt.DataResourceCost * mdt.DataRate;
						transmitterCount++;
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
					// canComm is saved manually in ModuleDataTransmitter.OnSave() by calling the canComm() method,
					// checks if module has moduleIsEnabled = false or is broken or not deployed
					if (!Lib.Proto.GetBool(mdt.protoTransmitter, "isEnabled", true) || !Lib.Proto.GetBool(mdt.protoTransmitter, "canComm", true))
						continue;

					// do not include internal data rate, ec cost only
					if (mdt.prefab.antennaType == AntennaType.INTERNAL)
					{
						connection.ec_idle += mdt.prefab.DataResourceCost * mdt.prefab.DataRate;
					}
					else
					{
						baseRate *= mdt.prefab.DataRate;
						connection.ec += mdt.prefab.DataResourceCost * mdt.prefab.DataRate;
						transmitterCount++;
					}
				}
			}

			if (transmitterCount > 1)
				baseRate = Math.Pow(baseRate, 1.0 / transmitterCount);
			else if (transmitterCount == 0)
				baseRate = 0.0;

			// when transmitting, transmitters need more EC for the signal amplifiers.
			// while not transmitting, transmitters only use 10-20% of that
			// Note : ec_idle is substracted from ec before consumption in Science.Update().
			// Didn't change that as this is what is expected by the RealAntenna API handler
			connection.ec_idle *= Settings.TransmitterPassiveEcFactor; // apply passive factor to "internal" antennas always-consumed rate
			connection.ec_idle += connection.ec * Settings.TransmitterPassiveEcFactor; // add "transmit" antennas always-consumed rate
			connection.ec *= Settings.TransmitterActiveEcFactor; // adjust "transmit" antennas transmit-only rate by the factor
		}

		private void GetTransmittersLoaded(Vessel v)
		{
			foreach (Part p in v.parts)
			{
				foreach (PartModule pm in p.Modules)
				{
					if (pm is ModuleDataTransmitter mdt)
					{
						// Disable all stock buttons
						mdt.Events["TransmitIncompleteToggle"].active = false;
						mdt.Events["StartTransmission"].active = false;
						mdt.Events["StopTransmission"].active = false;
						mdt.Actions["StartTransmissionAction"].active = false;

						loadedTransmitters.Add(mdt);
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
					if (part_prefab.Modules[i] is ModuleDataTransmitter mdt)
					{
						ProtoPartModuleSnapshot ppms;
						// We want to also get a possible ModuleDataTransmitter derivative but type checking isn't available
						// so we check a specific value present on the base class (See ModuleDataTransmitter.OnSave())
						if (i < pps.modules.Count && pps.modules[i].moduleValues.HasValue("canComm"))
						{
							ppms = pps.modules[i];
						}
						// fallback in case the module indexes are messed up
						else
						{
							ppms = pps.FindModule("ModuleDataTransmitter");
							Lib.LogDebug($"Could not find a ModuleDataTransmitter or derivative at index {i} on part {pps.partName} on vessel {v.protoVessel.vesselName}", Lib.LogLevel.Warning);
						}

						if (ppms != null)
						{
							unloadedTransmitters.Add(new UnloadedTransmitter(mdt, ppms));
						}
					}
				}
			}
		}
	}
}
