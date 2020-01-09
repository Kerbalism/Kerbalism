using System.Collections.Generic;
using System;
using KSP.Localization;
using CommNet;

namespace KERBALISM
{
	internal class AntennaInfoCommNet
	{
		protected Vessel v;
		protected AntennaInfo antennaInfo = new AntennaInfo();

		public AntennaInfoCommNet(Vessel v, bool powered, bool storm, bool transmitting)
		{
			this.v = v;
			antennaInfo.powered = powered;
			antennaInfo.storm = storm;
			antennaInfo.transmitting = transmitting;
		}

		public virtual AntennaInfo AntennaInfo()
		{
			int transmitterCount = 0;
			antennaInfo.rate = 1;
			double ec_transmitter = 0;

			// if vessel is loaded
			if (v.loaded)
			{
				foreach (ModuleDataTransmitter t in GetTransmittersLoaded(v))
				{
					// CanComm method : check if module has moduleIsEnabled = false or is broken or not deployed
					if (!t.isEnabled || !t.CanComm())
						continue;

					// do not include internal data rate, ec cost only
					if (t.antennaType == AntennaType.INTERNAL)
					{
						antennaInfo.ec += t.DataResourceCost * t.DataRate;
					}
					else
					{
						antennaInfo.rate *= t.DataRate;
						transmitterCount++;
						ec_transmitter += t.DataResourceCost * t.DataRate;
					}
				}
			}
			// if vessel is not loaded
			else
			{
				foreach (KeyValuePair<ModuleDataTransmitter, ProtoPartModuleSnapshot> pair in GetTransmittersUnloaded(v))
				{
					ModuleDataTransmitter prefab = pair.Key;
					ProtoPartModuleSnapshot ppms = pair.Value;

					// canComm is saved manually in ModuleDataTransmitter.OnSave() by calling the canComm() method,
					// checks if module has moduleIsEnabled = false or is broken or not deployed
					if (!Lib.Proto.GetBool(ppms, "isEnabled", true) || !Lib.Proto.GetBool(ppms, "canComm", true))
						continue;

					// do not include internal data rate, ec cost only
					if (prefab.antennaType == AntennaType.INTERNAL)
					{
						antennaInfo.ec += prefab.DataResourceCost * prefab.DataRate;
					}
					else
					{
						antennaInfo.rate *= prefab.DataRate;
						transmitterCount++;
						ec_transmitter += prefab.DataResourceCost * prefab.DataRate;
					}
				}
			}

			if (transmitterCount > 1)
				antennaInfo.rate = Math.Pow(antennaInfo.rate, 1.0 / transmitterCount);

			else if (transmitterCount == 0)
				antennaInfo.rate = 0;

			// when transmitting, transmitters need more EC for the signal amplifiers.
			// while not transmitting, transmitters only use 10-20% of that
			ec_transmitter *= antennaInfo.transmitting ? Settings.TransmitterActiveEcFactor : Settings.TransmitterPassiveEcFactor;

			antennaInfo.ec = ec_transmitter * Settings.TransmitterActiveEcFactor;
			antennaInfo.ec_idle = ec_transmitter * Settings.TransmitterPassiveEcFactor;

			Init();

			if (antennaInfo.linked && transmitterCount > 0)
			{
				var bitsPerMB = 1024.0 * 1024.0 * 8.0;
				antennaInfo.rate += Settings.DataRateMinimumBitsPerSecond / bitsPerMB;
			}

			return antennaInfo;
		}

		protected virtual List<KeyValuePair<ModuleDataTransmitter, ProtoPartModuleSnapshot>> GetTransmittersUnloaded(Vessel v)
		{
			List<KeyValuePair<ModuleDataTransmitter, ProtoPartModuleSnapshot>> transmitters;

			if (!Cache.HasVesselObjectsCache(v, "commnet_bg"))
			{
				transmitters = new List<KeyValuePair<ModuleDataTransmitter, ProtoPartModuleSnapshot>>();

				// find proto transmitters
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
							if (pps.modules[i].moduleValues.HasValue("canComm"))
							{
								ppms = pps.modules[i];
							}
							// fallback in case the module indexes are messed up
							else
							{
								ppms = pps.FindModule("ModuleDataTransmitter");
								Lib.LogDebug($"WARNING : Could not find a ModuleDataTransmitter or derivative at index {i} on part {pps.partName} on vessel {v.protoVessel.vesselName}");
							}

							if (ppms != null)
							{
								transmitters.Add(new KeyValuePair<ModuleDataTransmitter, ProtoPartModuleSnapshot>(mdt, ppms));
							}
						}
					}
				}

				Cache.SetVesselObjectsCache(v, "commnet_bg", transmitters);
			}
			else
			{
				// cache transmitters
				transmitters = Cache.VesselObjectsCache<List<KeyValuePair<ModuleDataTransmitter, ProtoPartModuleSnapshot>>>(v, "commnet_bg");
			}

			return transmitters;
		}

		protected virtual List<ModuleDataTransmitter> GetTransmittersLoaded(Vessel v)
		{
			List<ModuleDataTransmitter> transmitters;

			if (!Cache.HasVesselObjectsCache(v, "commnet"))
			{
				// find transmitters
				transmitters = v.FindPartModulesImplementing<ModuleDataTransmitter>();
				if (transmitters == null)
					transmitters = new List<ModuleDataTransmitter>();

				// Disable all stock buttons
				// TODO : moved from the update method (AntennaInfo()) to here to save performance, but maybe that is called too early and doesn't work
				foreach (ModuleDataTransmitter mdt in transmitters)
				{
					mdt.Events["TransmitIncompleteToggle"].active = false;
					mdt.Events["StartTransmission"].active = false;
					mdt.Events["StopTransmission"].active = false;
					mdt.Actions["StartTransmissionAction"].active = false;
				}

				Cache.SetVesselObjectsCache(v, "commnet", transmitters);
			}
			else
			{
				transmitters = Cache.VesselObjectsCache<List<ModuleDataTransmitter>>(v, "commnet");
			}

			return transmitters;
		}

		protected virtual void Init()
		{
			if(!antennaInfo.powered || v.connection == null)
			{
				antennaInfo.linked = false;
				antennaInfo.status = (int)LinkStatus.no_link;
				return;
			}

			// force CommNet update of unloaded vessels
			if (!v.loaded)
				Lib.ReflectionValue(v.connection, "unloadedDoOnce", true);

			// are we connected to DSN
			if (v.connection.IsConnected)
			{
				antennaInfo.linked = true;
				var link = v.connection.ControlPath.First;
				antennaInfo.status = link.hopType == CommNet.HopType.Home ? (int)LinkStatus.direct_link : (int)LinkStatus.indirect_link;
				antennaInfo.strength = link.signalStrength;

				antennaInfo.rate *= Math.Pow(link.signalStrength, Settings.DataRateDampingExponent);

				antennaInfo.target_name = Lib.Ellipsis(Localizer.Format(v.connection.ControlPath.First.end.displayName).Replace("Kerbin", "DSN"), 20);

				if (antennaInfo.status != (int)LinkStatus.direct_link)
				{
					Vessel firstHop = Lib.CommNodeToVessel(v.Connection.ControlPath.First.end);
					// Get rate from the firstHop, each Hop will do the same logic, then we will have the min rate for whole path
					antennaInfo.rate = Math.Min(firstHop.KerbalismData().Connection.rate, antennaInfo.rate);
				}
			}
			// is loss of connection due to plasma blackout
			else if (Lib.ReflectionValue<bool>(v.connection, "inPlasma"))  // calling InPlasma causes a StackOverflow :(
			{
				antennaInfo.status = (int)LinkStatus.plasma;
			}

			antennaInfo.control_path = new List<string[]>();
			foreach (CommLink link in v.connection.ControlPath)
			{
				double antennaPower = link.end.isHome ? link.start.antennaTransmit.power + link.start.antennaRelay.power : link.start.antennaTransmit.power;
				double signalStrength = 1 - ((link.start.position - link.end.position).magnitude / Math.Sqrt(antennaPower * link.end.antennaRelay.power));
				signalStrength = (3 - (2 * signalStrength)) * Math.Pow(signalStrength, 2);

				string name = Localizer.Format(link.end.displayName);
				if(link.end.isHome)
					name = name.Replace("Kerbin", "DSN");
				name = Lib.Ellipsis(name, 35);

				string value = Lib.HumanReadablePerc(Math.Ceiling(signalStrength * 10000) / 10000, "F2");
				string tooltip = "Distance: " + Lib.HumanReadableDistance((link.start.position - link.end.position).magnitude) +
					"\nMax Distance: " + Lib.HumanReadableDistance(Math.Sqrt((link.start.antennaTransmit.power + link.start.antennaRelay.power) * link.end.antennaRelay.power));
				antennaInfo.control_path.Add(new string[] { name, value, tooltip });
			}
		}
	}
}
