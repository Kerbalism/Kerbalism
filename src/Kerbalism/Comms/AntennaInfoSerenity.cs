using System.Collections.Generic;
using System;
using KSP.Localization;
using CommNet;
using KSPAssets;

namespace KERBALISM
{
	public sealed class AntennaInfoSerenity: AntennaInfo
	{
		public AntennaInfoSerenity(Vessel v, bool powered, bool storm, bool transmitting)
		{
#if !KSP16 && !KSP15 && !KSP14
			rate = 0;
			ec = 0;

			// if vessel is loaded
			if (v.loaded)
			{
				List<ModuleGroundCommsPart> transmitters;
				transmitters = v.FindPartModulesImplementing<ModuleGroundCommsPart>();

				foreach (ModuleGroundCommsPart t in transmitters)
				{
					Lib.Log("### serenity ModuleGroundCommsPart found, enabled: " + t.isEnabled);
					if (t.isEnabled)
						rate = 1;
				}
			}
			else
			{
				List<KeyValuePair<ModuleGroundCommsPart, ProtoPartSnapshot>> transmitters;

				transmitters = new List<KeyValuePair<ModuleGroundCommsPart, ProtoPartSnapshot>>();
				// find proto transmitters
				foreach (ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
				{
					// get part prefab (required for module properties)
					Part part_prefab = PartLoader.getPartInfoByName(p.partName).partPrefab;

					foreach (ModuleGroundCommsPart t in part_prefab.FindModulesImplementing<ModuleGroundCommsPart>())
					{
						transmitters.Add(new KeyValuePair<ModuleGroundCommsPart, ProtoPartSnapshot>(t, p));
					}
				}

				foreach (var pair in transmitters)
				{
					ModuleGroundCommsPart t = pair.Key;
					ProtoPartSnapshot p = pair.Value;

					Lib.Log("### bg serenity ModuleGroundCommsPart found, enabled: " + t.isEnabled);

					if (t.isEnabled)
						rate = 1;
				}
			}

			Init(v, storm);
		}

		private void Init(Vessel v, bool storm)
		{
			Lib.Log("### bg serenity connection: " + v.connection);
			Lib.Log("### bg serenity connection IsConnected: " + v.connection?.IsConnected);
			Lib.Log("### bg serenity connection ControlPath: " + v.connection?.ControlPath);


			if (v.connection == null)
			{
				linked = false;
				status = (int)LinkStatus.no_link;
				return;
			}

			// force CommNet update of unloaded vessels
			if (!v.loaded)
				Lib.ReflectionValue(v.connection, "unloadedDoOnce", true);

			// are we connected to DSN
			if (v.connection.IsConnected)
			{
				linked = true;
				var link = v.connection.ControlPath.First;
				status = link.hopType == CommNet.HopType.Home ? (int)LinkStatus.direct_link : (int)LinkStatus.indirect_link;
				strength = link.signalStrength;

				rate *= Math.Pow(link.signalStrength, Settings.DataRateDampingExponent);

				target_name = Lib.Ellipsis(Localizer.Format(v.connection.ControlPath.First.end.displayName).Replace("Kerbin", "DSN"), 20);

				if (status != (int)LinkStatus.direct_link)
				{
					Vessel firstHop = Lib.CommNodeToVessel(v.Connection.ControlPath.First.end);
					// Get rate from the firstHop, each Hop will do the same logic, then we will have the min rate for whole path
					rate = Math.Min(Cache.VesselInfo(FlightGlobals.FindVessel(firstHop.id)).connection.rate, rate);
				}
			}

			control_path = new List<string[]>();
			foreach (CommLink link in v.connection.ControlPath)
			{
				double antennaPower = link.end.isHome ? link.start.antennaTransmit.power + link.start.antennaRelay.power : link.start.antennaTransmit.power;
				double signalStrength = 1 - ((link.start.position - link.end.position).magnitude / Math.Sqrt(antennaPower * link.end.antennaRelay.power));
				signalStrength = (3 - (2 * signalStrength)) * Math.Pow(signalStrength, 2);

				string name = Lib.Ellipsis(Localizer.Format(link.end.name).Replace("Kerbin", "DSN"), 35);
				string value = Lib.HumanReadablePerc(Math.Ceiling(signalStrength * 10000) / 10000, "F2");
				string tooltip = "Distance: " + Lib.HumanReadableRange((link.start.position - link.end.position).magnitude) +
					"\nMax Distance: " + Lib.HumanReadableRange(Math.Sqrt((link.start.antennaTransmit.power + link.start.antennaRelay.power) * link.end.antennaRelay.power));
				control_path.Add(new string[] { name, value, tooltip });
			}
#endif
		}
	}
}
