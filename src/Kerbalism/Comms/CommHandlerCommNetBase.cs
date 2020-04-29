using CommNet;
using KSP.Localization;
using System;

namespace KERBALISM
{
	public class CommHandlerCommNetBase : CommHandler
	{
		/// <summary> base data rate set in derived classes from UpdateTransmitters()</summary>
		protected double baseRate = 0.0;

		protected override bool NetworkIsReady => CommNetNetwork.Initialized && CommNetNetwork.Instance?.CommNet != null;

		protected override void UpdateNetwork(ConnectionInfo connection)
		{
			Vessel v = vd.Vessel;

			if (v == null || v.connection == null || !connection.powered || !v.connection.IsConnected)
			{
				connection.linked = false;
				connection.Status = LinkStatus.no_link;
				connection.strength = 0.0;
				connection.rate = 0.0;
				connection.target_name = string.Empty;
				connection.control_path.Clear();

				// is loss of connection due to plasma blackout
				// calling InPlasma causes a StackOverflow :(
				if (vd.EnvInAtmosphere && v != null && v.connection != null && Lib.ReflectionValue<bool>(v.connection, "inPlasma"))
					connection.Status = LinkStatus.plasma;
				else
					connection.Status = LinkStatus.no_link;

				return;
			}

			// force a CommNet update for this vessel, if it is unloaded.
			if (!v.loaded)
				Lib.ReflectionValue(v.connection, "unloadedDoOnce", true);

			connection.linked = true;
			CommLink firstLink = v.connection.ControlPath.First;
			connection.Status = firstLink.hopType == HopType.Home ? LinkStatus.direct_link : LinkStatus.indirect_link;
			connection.strength = firstLink.signalStrength;

			connection.rate = baseRate * Math.Pow(firstLink.signalStrength, Settings.DataRateDampingExponent);

			connection.target_name = Lib.Ellipsis(Localizer.Format(v.connection.ControlPath.First.end.displayName).Replace("Kerbin", "DSN"), 20);

			if (connection.Status != LinkStatus.direct_link)
			{
				Vessel firstHop = Lib.CommNodeToVessel(v.Connection.ControlPath.First.end);
				// Get rate from the firstHop, each Hop will do the same logic, then we will have the min rate for whole path
				firstHop.TryGetVesselData(out VesselData vd);
				connection.rate = Math.Min(vd.Connection.rate, connection.rate);
			}


			connection.control_path.Clear();
			foreach (CommLink link in v.connection.ControlPath)
			{
				double antennaPower = link.end.isHome ? link.start.antennaTransmit.power + link.start.antennaRelay.power : link.start.antennaTransmit.power;
				double linkDistance = (link.start.position - link.end.position).magnitude;
				double linkMaxDistance = Math.Sqrt(antennaPower * link.end.antennaRelay.power);
				double signalStrength = 1 - (linkDistance / linkMaxDistance);
				signalStrength = (3 - (2 * signalStrength)) * Math.Pow(signalStrength, 2);

				string[] controlPoint = new string[3];

				// name
				controlPoint[0] = Localizer.Format(link.end.displayName);
				if (link.end.isHome)
					controlPoint[0] = controlPoint[0].Replace("Kerbin", "DSN");
				controlPoint[0] = Lib.Ellipsis(controlPoint[0], 35);

				// signal strength
				controlPoint[1] = Lib.HumanReadablePerc(Math.Ceiling(signalStrength * 10000) / 10000, "F2");

				// tooltip
				controlPoint[2] = Lib.BuildString(
					"Distance: ", Lib.HumanReadableDistance(linkDistance),
					"\nMax Distance: ", Lib.HumanReadableDistance(linkMaxDistance));

				connection.control_path.Add(controlPoint);
			}

			// set minimal data rate to what is defined in Settings (1 bit/s by default) 
			if (connection.rate > 0.0 && connection.rate * Lib.bitsPerMB < Settings.DataRateMinimumBitsPerSecond)
				connection.rate = Settings.DataRateMinimumBitsPerSecond / Lib.bitsPerMB;
		}
	}
}
