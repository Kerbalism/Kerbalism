using CommNet;
using KSP.Localization;
using System;
using System.Reflection;
using HarmonyLib;

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

			bool vIsNull = v == null || v.connection == null;

			connection.linked = !vIsNull && connection.powered && v.connection.IsConnected;

			if (!connection.linked)
			{
				connection.strength = 0.0;
				connection.rate = 0.0;
				connection.target_name = string.Empty;
				connection.control_path.Clear();

				if (!vIsNull && v.connection.InPlasma)
				{
					if (connection.storm)
						connection.Status = LinkStatus.storm;
					else
						connection.Status = LinkStatus.plasma;
				}
				else
				{
					connection.Status = LinkStatus.no_link;
				}

				return;
			}

			CommLink firstLink = v.connection.ControlPath.First;
			connection.Status = firstLink.hopType == HopType.Home ? LinkStatus.direct_link : LinkStatus.indirect_link;
			connection.strength = firstLink.signalStrength;

			connection.rate = baseRate * Math.Pow(firstLink.signalStrength, Sim.DataRateDampingExponent);

			connection.target_name = Lib.Ellipsis(Localizer.Format(v.connection.ControlPath.First.end.displayName).Replace("Kerbin", "DSN"), 20);

			if (connection.Status != LinkStatus.direct_link)
			{
				Vessel firstHop = CommNodeToVessel(v.Connection.ControlPath.First.end);
				// Get rate from the firstHop, each Hop will do the same logic, then we will have the min rate for whole path
				if (firstHop == null || !firstHop.TryGetVesselData(out VesselData vd))
					connection.rate = 0.0;
				else
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
				signalStrength = Math.Pow(signalStrength, Sim.DataRateDampingExponent);

				string[] controlPoint = new string[3];

				// name
				controlPoint[0] = Localizer.Format(link.end.displayName);
				if (link.end.isHome)
					controlPoint[0] = controlPoint[0].Replace("Kerbin", "DSN");
				controlPoint[0] = Lib.Ellipsis(controlPoint[0], 35);

				// signal strength
				controlPoint[1] = Lib.HumanReadablePerc(Math.Ceiling(signalStrength * 10000) / 10000, "F2");

				// extra info
				controlPoint[2] = Lib.BuildString(
					"Distance: ", Lib.HumanReadableDistance(linkDistance),
					" (Max: ", Lib.HumanReadableDistance(linkMaxDistance), ")");

				connection.control_path.Add(controlPoint);
			}

			// set minimal data rate to what is defined in Settings (1 bit/s by default) 
			if (connection.rate > 0.0 && connection.rate * Lib.bitsPerMB < Settings.DataRateMinimumBitsPerSecond)
				connection.rate = Settings.DataRateMinimumBitsPerSecond / Lib.bitsPerMB;
		}

		private static Vessel CommNodeToVessel(CommNode node)
		{
			return node?.transform?.gameObject.GetComponent<Vessel>();
		}

		public static void ApplyHarmonyPatches()
		{
			MethodInfo CommNetVessel_OnNetworkPreUpdate_Info = AccessTools.Method(typeof(CommNetVessel), nameof(CommNetVessel.OnNetworkPreUpdate));

			Loader.HarmonyInstance.Patch(CommNetVessel_OnNetworkPreUpdate_Info,
				new HarmonyMethod(AccessTools.Method(typeof(CommHandlerCommNetBase), nameof(CommNetVessel_OnNetworkPreUpdate_Prefix))));

			Loader.HarmonyInstance.Patch(CommNetVessel_OnNetworkPreUpdate_Info,
				null, new HarmonyMethod(AccessTools.Method(typeof(CommHandlerCommNetBase), nameof(CommNetVessel_OnNetworkPreUpdate_Postfix))));

			MethodInfo CommNetVessel_OnNetworkPostUpdate_Info = AccessTools.Method(typeof(CommNetVessel), nameof(CommNetVessel.OnNetworkPostUpdate));

			Loader.HarmonyInstance.Patch(CommNetVessel_OnNetworkPostUpdate_Info,
				new HarmonyMethod(AccessTools.Method(typeof(CommHandlerCommNetBase), nameof(CommNetVessel_OnNetworkPostUpdate_Prefix))));

			MethodInfo CommNetVessel_GetSignalStrengthModifier_Info = AccessTools.Method(typeof(CommNetVessel), nameof(CommNetVessel.GetSignalStrengthModifier));

			Loader.HarmonyInstance.Patch(CommNetVessel_GetSignalStrengthModifier_Info,
				new HarmonyMethod(AccessTools.Method(typeof(CommHandlerCommNetBase), nameof(CommNetVessel_GetSignalStrengthModifier_Prefix))));
		}

		// ensure unloadedDoOnce is true for unloaded vessels
		private static void CommNetVessel_OnNetworkPreUpdate_Prefix(CommNetVessel __instance, ref bool ___unloadedDoOnce)
		{
			if (!__instance.Vessel.loaded && __instance.CanComm)
				___unloadedDoOnce = true;
		}


		// ensure unloadedDoOnce is true for unloaded vessels
		private static void CommNetVessel_OnNetworkPostUpdate_Prefix(CommNetVessel __instance, ref bool ___unloadedDoOnce)
		{
			if (!__instance.Vessel.loaded && __instance.CanComm)
				___unloadedDoOnce = true;
		}


		// apply storm radiation factor to the comm strength multiplier used by stock for plasma blackout
		private static void CommNetVessel_OnNetworkPreUpdate_Postfix(CommNetVessel __instance, ref bool ___inPlasma, ref double ___plasmaMult)
		{
			if (!__instance.CanComm || !__instance.Vessel.TryGetVesselData(out VesselData vd))
				return;

			if (vd.EnvStormRadiation > 0.0)
			{
				___inPlasma = true;
				___plasmaMult = vd.EnvStormRadiation * 2.0 / PreferencesRadiation.Instance.StormRadiation; // We should probably have a threshold setting instead of this hardcoded formula
				___plasmaMult = Math.Max(1.0 - ___plasmaMult, 0.0);
			}
		}

		// apply storm radiation factor to the comm strength multiplier used by stock for plasma blackout
		private static bool CommNetVessel_GetSignalStrengthModifier_Prefix(CommNetVessel __instance, bool ___canComm, bool ___inPlasma, double ___plasmaMult, out double __result)
		{
			if (!___canComm)
			{
				__result = 0.0;
				return false;
			}

			if (!___inPlasma)
			{
				__result = 1.0;
				return false;
			}

			if (__instance.Vessel.TryGetVesselData(out VesselData vd) && vd.EnvStormRadiation > 0.0)
			{
				__result = ___plasmaMult;
				return false;
			}

			__result = 0.0;
			return true;
		}
	}
}
