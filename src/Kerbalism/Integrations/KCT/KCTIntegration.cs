using System;
using HarmonyLib;

namespace KERBALISM
{
	internal static class KCTIntegration
	{
		private static readonly Type vesselType = AccessTools.TypeByName("KerbalConstructionTime.KCT_BuildListVessel");

		public static bool Installed => vesselType != null;

		public static void ApplyHarmonyPatches(Harmony harmony)
		{
			var sanitize = AccessTools.Method(vesselType, "SanitizeShipNode", new[] { typeof(ConfigNode) });
			var kscType = AccessTools.TypeByName("KerbalConstructionTime.KCT_KSC");
			var load = AccessTools.Method(kscType, "FromConfigNode", new[] { typeof(ConfigNode) });
			if (sanitize == null || load == null)
				throw new MissingMethodException("KCT recovery/loading methods not found; science resupply integration disabled.");

			harmony.Patch(sanitize, postfix: new HarmonyMethod(typeof(KCTIntegration), nameof(OnRecovered)));
			harmony.Patch(load, prefix: new HarmonyMethod(typeof(KCTIntegration), nameof(OnStorageLoad)));
		}

		private static void OnRecovered(ConfigNode __result)
		{
			ResetExperiments(__result);
		}

		private static void OnStorageLoad(ConfigNode node)
		{
			// Repair previously recovered craft before KCT loads them into its warehouse/build lists.
			foreach (ConfigNode list in node.nodes)
			{
				if (list.name != "VABWarehouse" && list.name != "SPHWarehouse"
					&& list.name != "VABList" && list.name != "SPHList")
					continue;

				foreach (ConfigNode vessel in list.GetNodes("KCTVessel"))
					ResetExperiments(vessel.GetNode("ShipNode"));
			}
		}

		private static void ResetExperiments(ConfigNode ship)
		{
			foreach (ConfigNode part in ship.GetNodes("PART"))
			{
				foreach (ConfigNode module in part.GetNodes("MODULE"))
				{
					if (module.GetValue("name") != nameof(Experiment)
						|| !bool.TryParse(module.GetValue("firstStart"), out bool firstStart) || firstStart
						|| module.GetValue("expState") == nameof(Experiment.RunningState.Broken))
						continue;

					// FirstStart refills only consumable experiments using their current capacity/upgrades.
					// Keeping this true in storage also makes repeated loading idempotent.
					module.SetValue("firstStart", true);
					module.SetValue("remainingSampleMass", 0.0);
					module.SetValue("expState", nameof(Experiment.RunningState.Stopped));
					module.SetValue("status", nameof(Experiment.ExpStatus.Stopped));
					module.SetValue("issue", string.Empty);
					module.SetValue("situationId", 0);
					module.SetValue("didPrepare", false);
				}
			}
		}
	}
}
