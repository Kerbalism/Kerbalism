using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{
    public static class DB
    {
		private const string VALUENAME_VERSION = "version";
		private const string VALUENAME_UID = "uid";
		private const string NODENAME_VESSELS = "KERBALISMVESSELS";
		private const string NODENAME_KERBALS = "KERBALISMKERBALS";
		private const string NODENAME_STORMS = "KERBALISMSTORMS";
		private const string NODENAME_GUI = "KERBALISMGUI";
		public static readonly Version LAST_SUPPORTED_VERSION = new Version(4, 0);

		// savegame version
		public static Version version;
		// savegame unique id
		private static Guid uid;
		// store data per-kerbal
		private static Dictionary<string, KerbalData> kerbals;
		// store data per-vessel
		private static Dictionary<Guid, VesselData> vessels = new Dictionary<Guid, VesselData>();
		// store data per-body
		private static Dictionary<string, StormData> storms;
		// store ui data
		private static UIData uiData;                               

		public static Guid Guid => uid;
		public static Dictionary<string, KerbalData> Kerbals => kerbals;
		public static UIData UiData => uiData;
		public static Dictionary<Guid, VesselData>.ValueCollection VesselDatas => vessels.Values;

		#region LOAD/SAVE

		public static void Load(ConfigNode node)
        {
            // get version (or use current one for new savegames)
            string versionStr = Lib.ConfigValue(node, VALUENAME_VERSION, Lib.KerbalismVersion.ToString());
            // sanitize old saves (pre 3.1) format (X.X.X.X) to new format (X.X)
            if (versionStr.Split('.').Length > 2) versionStr = versionStr.Split('.')[0] + "." + versionStr.Split('.')[1];
            version = new Version(versionStr);

            // if this is an unsupported version, print warning
            if (version < LAST_SUPPORTED_VERSION)
            {
                Lib.Log($"Loading save from unsupported version " + version, Lib.LogLevel.Warning);
                return;
            }

            // get unique id (or generate one for new savegames)
            uid = Lib.ConfigValue(node, VALUENAME_UID, Guid.NewGuid());

			// load kerbals data
			kerbals = new Dictionary<string, KerbalData>();
            if (node.HasNode(NODENAME_KERBALS))
            {
                foreach (var kerbal_node in node.GetNode(NODENAME_KERBALS).GetNodes())
                {
                    kerbals.Add(FromSafeKey(kerbal_node.name), new KerbalData(kerbal_node));
                }
            }

			// load the science database, has to be before vessels are loaded
			ScienceDB.Load(node);

			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.DB.Load.Vessels");

			// remove all vessels
			vessels.Clear();

			// clear the dictionary of moduledatas
			ModuleData.ClearOnLoad();

			// flightstate will be null when first creating the game
			if (HighLogic.CurrentGame.flightState != null)
			{
				ConfigNode vesselsNode = node.GetNode(NODENAME_VESSELS);
				if (vesselsNode == null)
					vesselsNode = new ConfigNode();
				// HighLogic.CurrentGame.flightState.protoVessels is what is used by KSP to persist vessels
				// It is always available and synchronized in OnLoad, no matter the scene, excepted on
				// the first OnLoad in a new game.
				foreach (ProtoVessel pv in HighLogic.CurrentGame.flightState.protoVessels)
				{
					if (pv.vesselID == Guid.Empty)
					{
						// Flags have an empty GUID. skip them.
						Lib.LogDebug("Skipping VesselData load for vessel with empty GUID :" + pv.vesselName);
						continue;
					}

					VesselData vd = new VesselData(pv, vesselsNode.GetNode(pv.vesselID.ToString()));
					vessels.Add(pv.vesselID, vd);
					Lib.LogDebug("VesselData loaded for vessel " + pv.vesselName);
				}
			}
			UnityEngine.Profiling.Profiler.EndSample();

			// load bodies data
			storms = new Dictionary<string, StormData>();
            if (node.HasNode(NODENAME_STORMS))
            {
                foreach (var body_node in node.GetNode(NODENAME_STORMS).GetNodes())
                {
                    storms.Add(FromSafeKey(body_node.name), new StormData(body_node));
                }
            }

            // load ui data
            if (node.HasNode(NODENAME_GUI))
            {
                uiData = new UIData(node.GetNode(NODENAME_GUI));
            }
            else
            {
				uiData = new UIData();
            }

			// if an old savegame was imported, log some debug info
			if (version != Lib.KerbalismVersion) Lib.Log("savegame converted from version " + version + " to " + Lib.KerbalismVersion);
        }

        public static void Save(ConfigNode node)
        {
            // save version
            node.AddValue(VALUENAME_VERSION, Lib.KerbalismVersion.ToString());

            // save unique id
            node.AddValue(VALUENAME_UID, uid);

			// save kerbals data
			var kerbals_node = node.AddNode(NODENAME_KERBALS);
            foreach (var p in kerbals)
            {
                p.Value.Save(kerbals_node.AddNode(ToSafeKey(p.Key)));
            }

			// only persist vessels that exists in KSP own vessel persistence
			// this prevent creating junk data without going into the mess of using gameevents
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.DB.Save.Vessels");
			ConfigNode vesselsNode = node.AddNode(NODENAME_VESSELS);
			foreach (ProtoVessel pv in HighLogic.CurrentGame.flightState.protoVessels)
			{
				if (pv.vesselID == Guid.Empty)
				{
					// It seems flags are saved with an empty GUID. skip them.
					Lib.LogDebug("Skipping VesselData save for vessel with empty GUID :" + pv.vesselName);
					continue;
				}

                // TODO we currently save vessel data even for asteroids. save only
				// vessels with hard drives?
                if (pv.TryGetVesselData(out VesselData vd))
				{
					ConfigNode vesselNode = vesselsNode.AddNode(pv.vesselID.ToString());
					vd.Save(vesselNode);
				}
            }
			UnityEngine.Profiling.Profiler.EndSample();

			// save the science database
			ScienceDB.Save(node);

            // save bodies data
            var bodies_node = node.AddNode(NODENAME_STORMS);
            foreach (var p in storms)
            {
                p.Value.Save(bodies_node.AddNode(ToSafeKey(p.Key)));
            }

			// save ui data
			uiData.Save(node.AddNode(NODENAME_GUI));
        }

		/// <summary> Avoid leading and trailing spaces from being removed when saving a string to a ConfigNode value</summary>
		public static string ToSafeKey(string key) => key.Replace(" ", "___");

		/// <summary> Retrieve a string that was serialized using ToSafeKey() </summary>
		public static string FromSafeKey(string key) => key.Replace("___", " ");

		#endregion

		#region VESSELDATA METHODS

		public static VesselData NewVesselDataFromShipConstruct(Vessel v, ConfigNode shipNode, VesselDataShip shipVd)
		{
			Lib.LogDebug("Creating VesselData from ShipConstruct for launched vessel " + v.vesselName);
			VesselData vd = new VesselData(v, shipNode, shipVd);
			vessels.Add(v.id, vd);
			return vd;
		}

		public static void AddNewVesselData(VesselData vd)
		{
			if (vessels.ContainsKey(vd.VesselId))
			{
				Lib.LogDebugStack($"Trying to register new VesselData for {vd.VesselName} but that vessel exists already !", Lib.LogLevel.Error);
				return;
			}

			Lib.LogDebug($"Adding new VesselData for {vd.VesselName}");
			vessels.Add(vd.VesselId, vd);
		}

		public static bool TryGetVesselData(this Vessel vessel, out VesselData vesselData)
		{
			if (!vessels.TryGetValue(vessel.id, out vesselData))
			{
				Lib.LogStack($"Could not get VesselData for vessel {vessel.vesselName}", Lib.LogLevel.Error);
				return false;
			}
			return true;
		}

		public static bool TryGetVesselDataNoError(this Vessel vessel, out VesselData vesselData)
		{
			if (!vessels.TryGetValue(vessel.id, out vesselData))
			{
				Lib.LogDebug($"Could not get VesselData for vessel {vessel.vesselName} (this is normal)");
				return false;
			}
			return true;
		}

		/// <summary>
		/// Get the VesselData for this vessel. Will return null if that vessel isn't yet created in the DB, which can happen if this is called too early. <br/>
		/// Typically it's safe to use from partmodules FixedUpdate() and OnStart(), but not in Awake() and probably not from Update()
		/// </summary>
		public static VesselData GetVesselData(this Vessel vessel)
		{
			if (!vessels.TryGetValue(vessel.id, out VesselData vesselData))
			{
				Lib.LogStack($"Could not get VesselData for vessel {vessel.vesselName}");
				return null;
			}
			return vesselData;
		}

		public static bool TryGetVesselData(this ProtoVessel protoVessel, out VesselData vesselData)
		{
			if (!vessels.TryGetValue(protoVessel.vesselID, out vesselData))
			{
				Lib.LogStack($"Could not get VesselData for vessel {protoVessel.vesselName}", Lib.LogLevel.Error);
				return false;
			}
			return true;
		}
		//{
		//	VesselData vd;
		//	if (!vessels.TryGetValue(protoVessel.vesselID, out vd))
		//	{
		//		Lib.Log("VesselData for protovessel " + protoVessel.vesselName + ", ID=" + protoVessel.vesselID + " doesn't exist !", Lib.LogLevel.Warning);
		//		vd = new VesselData(protoVessel, null);
		//		vessels.Add(protoVessel.vesselID, vd);
		//	}
		//	return vd;
		//}

		/// <summary>shortcut for VesselData.IsValid. False in the following cases : asteroid, debris, flag, deployed ground part, dead eva, rescue</summary>
		//public static bool KerbalismIsValid(this Vessel vessel)
		//      {
		//          return TryGetVesselData(vessel).IsSimulated;
		//      }

		#endregion

		#region STORM METHODS

		public static StormData Storm(string name)
		{
			if (!storms.ContainsKey(name))
			{
				storms.Add(name, new StormData(null));
			}
			return storms[name];
		}

		#endregion

		#region KERBALS METHODS

		public static KerbalData Kerbal(string name)
		{
			if (!kerbals.ContainsKey(name))
			{
				kerbals.Add(name, new KerbalData());
			}
			return kerbals[name];
		}

		public static bool ContainsKerbal(string name)
        {
            return kerbals.ContainsKey(name);
        }

        /// <summary>
        /// Remove a Kerbal and his lifetime data from the database
        /// </summary>
        public static void KillKerbal(String name, bool reallyDead)
        {
            if (reallyDead)
            {
                kerbals.Remove(name);
            }
            else
            {
                // called when a vessel is destroyed. don't remove the kerbal just yet,
                // check with the roster if the kerbal is dead or not
                Kerbal(name).Recover();
            }
        }

        /// <summary>
        /// Resets all process data of a kerbal, except lifetime data
        /// </summary>
        public static void RecoverKerbal(string name)
        {
            if (ContainsKerbal(name))
            {
                if (Kerbal(name).eva_dead)
                {
                    kerbals.Remove(name);
                }
                else
                {
                    Kerbal(name).Recover();
                }
            }
        }

		#endregion

	}
} // KERBALISM



