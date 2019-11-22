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
				List<ModuleDataTransmitter> transmitters = GetTransmittersLoaded(v);

				foreach (ModuleDataTransmitter t in transmitters)
				{
					// Disable all stock buttons
					t.Events["TransmitIncompleteToggle"].active = false;
					t.Events["StartTransmission"].active = false;
					t.Events["StopTransmission"].active = false;
					t.Actions["StartTransmissionAction"].active = false;

					// ignore broken / disabled transmitters
					if (!t.isEnabled) continue;

					if (t.antennaType == AntennaType.INTERNAL) // do not include internal data rate, ec cost only
						antennaInfo.ec += t.DataResourceCost * t.DataRate;
					else
					{
						// do we have an animation
						ModuleDeployableAntenna animation = t.part.FindModuleImplementing<ModuleDeployableAntenna>();
						ModuleAnimateGeneric animationGeneric = t.part.FindModuleImplementing<ModuleAnimateGeneric>();
						if (animation != null)
						{
							// only include data rate and ec cost if transmitter is extended
							if (animation.deployState == ModuleDeployablePart.DeployState.EXTENDED)
							{
								antennaInfo.rate *= t.DataRate;
								transmitterCount++;
								var e = t.DataResourceCost * t.DataRate;
								ec_transmitter += e;
							}
						}
						else if (animationGeneric != null)
						{
							// only include data rate and ec cost if transmitter is extended
							if (animationGeneric.animSpeed > 0)
							{
								antennaInfo.rate *= t.DataRate;
								transmitterCount++;
								var e = t.DataResourceCost * t.DataRate;
								ec_transmitter += e;
							}
						}
						// no animation
						else
						{
							antennaInfo.rate *= t.DataRate;
							transmitterCount++;
							var e = t.DataResourceCost * t.DataRate;
							ec_transmitter += e;
						}
					}
				}
			}
			// if vessel is not loaded
			else
			{
				List<KeyValuePair<ModuleDataTransmitter, ProtoPartSnapshot>> transmitters = GetTransmittersUnloaded(v);

				foreach(var pair in transmitters)
				{
					ModuleDataTransmitter t = pair.Key;
					ProtoPartSnapshot p = pair.Value;

					// ignore broken/disabled transmitters
					var mdt = p.FindModule("ModuleDataTransmitter");
					if (mdt != null && !Lib.Proto.GetBool(mdt, "isEnabled", true)) continue;

					if (t.antennaType == AntennaType.INTERNAL) // do not include internal data rate, ec cost only
						antennaInfo.ec += t.DataResourceCost * t.DataRate;
					else
					{
						// do we have an animation
						ProtoPartModuleSnapshot m = p.FindModule("ModuleDeployableAntenna") ?? p.FindModule("ModuleAnimateGeneric");
						if (m != null)
						{
							// only include data rate and ec cost if transmitter is extended
							string deployState = Lib.Proto.GetString(m, "deployState");
							float animSpeed = Lib.Proto.GetFloat(m, "animSpeed");
							if (deployState == "EXTENDED" || animSpeed > 0)
							{
								antennaInfo.rate *= t.DataRate;
								transmitterCount++;
								ec_transmitter += t.DataResourceCost * t.DataRate;
							}
						}
						// no animation
						else
						{
							antennaInfo.rate *= t.DataRate;
							transmitterCount++;
							ec_transmitter += t.DataResourceCost * t.DataRate;
						}
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

			antennaInfo.ec += ec_transmitter;

			Init();

			if (antennaInfo.linked && transmitterCount > 0)
			{
				var bitsPerMB = 1024.0 * 1024.0 * 8.0;
				antennaInfo.rate += Settings.DataRateMinimumBitsPerSecond / bitsPerMB;
			}

			return antennaInfo;
		}

		protected virtual List<KeyValuePair<ModuleDataTransmitter, ProtoPartSnapshot>> GetTransmittersUnloaded(Vessel v)
		{
			List<KeyValuePair<ModuleDataTransmitter, ProtoPartSnapshot>> transmitters;

			if (!Cache.HasVesselObjectsCache(v, "commnet_bg"))
			{
				transmitters = new List<KeyValuePair<ModuleDataTransmitter, ProtoPartSnapshot>>();

				// find proto transmitters
				foreach (ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
				{
					// get part prefab (required for module properties)
					Part part_prefab = PartLoader.getPartInfoByName(p.partName).partPrefab;

					foreach (ModuleDataTransmitter t in part_prefab.FindModulesImplementing<ModuleDataTransmitter>())
					{
						transmitters.Add(new KeyValuePair<ModuleDataTransmitter, ProtoPartSnapshot>(t, p));
					}
				}

				Cache.SetVesselObjectsCache(v, "commnet_bg", transmitters);
			}
			else
			{
				// cache transmitters
				transmitters = Cache.VesselObjectsCache<List<KeyValuePair<ModuleDataTransmitter, ProtoPartSnapshot>>>(v, "commnet_bg");
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
				Cache.SetVesselObjectsCache(v, "commnet", transmitters);
			}
			else
				transmitters = Cache.VesselObjectsCache<List<ModuleDataTransmitter>>(v, "commnet");

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
