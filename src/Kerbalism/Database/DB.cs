using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{
    public static class DB
    {
        public static void Load(ConfigNode node)
        {
            // get version (or use current one for new savegames)
            string versionStr = Lib.ConfigValue(node, "version", Lib.KerbalismVersion.ToString());
            // sanitize old saves (pre 3.1) format (X.X.X.X) to new format (X.X)
            if (versionStr.Split('.').Length > 2) versionStr = versionStr.Split('.')[0] + "." + versionStr.Split('.')[1];
            version = new Version(versionStr);

            // if this is an unsupported version, print warning
            if (version <= new Version(1, 2)) Lib.Log("loading save from unsupported version " + version);

            // get unique id (or generate one for new savegames)
            uid = Lib.ConfigValue(node, "uid", Lib.RandomInt(int.MaxValue));

			// load kerbals data
			kerbals = new Dictionary<string, KerbalData>();
            if (node.HasNode("kerbals"))
            {
                foreach (var kerbal_node in node.GetNode("kerbals").GetNodes())
                {
                    kerbals.Add(From_safe_key(kerbal_node.name), new KerbalData(kerbal_node));
                }
            }

			// load the science database, has to be before vessels are loaded
			ScienceDB.Load(node);

			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.DB.Load.Vessels");
			vessels.Clear();
			// flightstate will be null when first creating the game
			if (HighLogic.CurrentGame.flightState != null)
			{
				ConfigNode vesselsNode = node.GetNode("vessels2");
				if (vesselsNode == null)
					vesselsNode = new ConfigNode();
				// HighLogic.CurrentGame.flightState.protoVessels is what is used by KSP to persist vessels
				// It is always available and synchronized in OnLoad, no matter the scene, excepted on the first OnLoad in a new game
				foreach (ProtoVessel pv in HighLogic.CurrentGame.flightState.protoVessels)
				{
					if (pv.vesselID == Guid.Empty)
					{
						// It seems flags are saved with an empty GUID. skip them.
						Lib.LogDebug("Skipping VesselData load for vessel with empty GUID :" + pv.vesselName);
						continue;
					}

					VesselData vd = new VesselData(pv, vesselsNode.GetNode(pv.vesselID.ToString()));
					vessels.Add(pv.vesselID, vd);
					Lib.LogDebug("VesselData loaded for vessel " + pv.vesselName);
				}
			}
			UnityEngine.Profiling.Profiler.EndSample();

			// for compatibility with old saves, convert drives data (it's now saved in PartData)
			if (node.HasNode("drives"))
			{
				Dictionary<uint, PartData> allParts = new Dictionary<uint, PartData>();
				foreach (VesselData vesselData in vessels.Values)
				{
					foreach (PartData partData in vesselData.PartDatas)
					{
						// we had a case of someone having a save with multiple parts having the same flightID
						// 5 duplicates, all were asteroids.
						if (!allParts.ContainsKey(partData.FlightId))
						{
							allParts.Add(partData.FlightId, partData);
						}
					}
				}

				foreach (var drive_node in node.GetNode("drives").GetNodes())
				{
					uint driveId = Lib.Parse.ToUInt(drive_node.name);
					if (allParts.ContainsKey(driveId))
					{
						allParts[driveId].Drive = new Drive(drive_node);
					}
				}
			}

			// load bodies data
			storms = new Dictionary<string, StormData>();
            if (node.HasNode("bodies"))
            {
                foreach (var body_node in node.GetNode("bodies").GetNodes())
                {
                    storms.Add(From_safe_key(body_node.name), new StormData(body_node));
                }
            }

            // load landmark data
            if (node.HasNode("landmarks"))
            {
                landmarks = new LandmarkData(node.GetNode("landmarks"));
            }
            else
            {
                landmarks = new LandmarkData();
            }

            // load ui data
            if (node.HasNode("ui"))
            {
                ui = new UIData(node.GetNode("ui"));
            }
            else
            {
                ui = new UIData();
            }

			// if an old savegame was imported, log some debug info
			if (version != Lib.KerbalismVersion) Lib.Log("savegame converted from version " + version + " to " + Lib.KerbalismVersion);
        }

        public static void Save(ConfigNode node)
        {
            // save version
            node.AddValue("version", Lib.KerbalismVersion.ToString());

            // save unique id
            node.AddValue("uid", uid);

			// save kerbals data
			var kerbals_node = node.AddNode("kerbals");
            foreach (var p in kerbals)
            {
                p.Value.Save(kerbals_node.AddNode(To_safe_key(p.Key)));
            }

			// only persist vessels that exists in KSP own vessel persistence
			// this prevent creating junk data without going into the mess of using gameevents
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.DB.Save.Vessels");
			ConfigNode vesselsNode = node.AddNode("vessels2");
			foreach (ProtoVessel pv in HighLogic.CurrentGame.flightState.protoVessels)
			{
				if (pv.vesselID == Guid.Empty)
				{
					// It seems flags are saved with an empty GUID. skip them.
					Lib.LogDebug("Skipping VesselData save for vessel with empty GUID :" + pv.vesselName);
					continue;
				}

				VesselData vd = pv.KerbalismData();
				ConfigNode vesselNode = vesselsNode.AddNode(pv.vesselID.ToString());
				vd.Save(vesselNode);
			}
			UnityEngine.Profiling.Profiler.EndSample();

			// save the science database
			ScienceDB.Save(node);

            // save bodies data
            var bodies_node = node.AddNode("bodies");
            foreach (var p in storms)
            {
                p.Value.Save(bodies_node.AddNode(To_safe_key(p.Key)));
            }

            // save landmark data
            landmarks.Save(node.AddNode("landmarks"));

            // save ui data
            ui.Save(node.AddNode("ui"));
        }


        public static KerbalData Kerbal(string name)
        {
            if (!kerbals.ContainsKey(name))
            {
                kerbals.Add(name, new KerbalData());
            }
            return kerbals[name];
        }

		public static VesselData KerbalismData(this Vessel vessel)
		{
			VesselData vd;
			if (!vessels.TryGetValue(vessel.id, out vd))
			{
				Lib.LogDebug("Creating Vesseldata for new vessel " + vessel.vesselName);
				vd = new VesselData(vessel);
				vessels.Add(vessel.id, vd);
			}
			return vd;
		}

		public static VesselData KerbalismData(this ProtoVessel protoVessel)
		{
			VesselData vd;
			if (!vessels.TryGetValue(protoVessel.vesselID, out vd))
			{
				Lib.Log("VesselData for protovessel " + protoVessel.vesselName + ", ID=" + protoVessel.vesselID + " doesn't exist !", Lib.LogLevel.Warning);
				vd = new VesselData(protoVessel, null);
				vessels.Add(protoVessel.vesselID, vd);
			}
			return vd;
		}

		/// <summary>shortcut for VesselData.IsValid. False in the following cases : asteroid, debris, flag, deployed ground part, dead eva, rescue</summary>
		public static bool KerbalismIsValid(this Vessel vessel)
        {
            return KerbalismData(vessel).IsSimulated;
        }

		public static Dictionary<Guid, VesselData>.ValueCollection VesselDatas => vessels.Values;

        public static StormData Storm(string name)
        {
            if (!storms.ContainsKey(name))
            {
                storms.Add(name, new StormData(null));
            }
            return storms[name];
        }

		public static Boolean ContainsKerbal(string name)
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

        public static Dictionary<string, KerbalData> Kerbals()
        {
            return kerbals;
        }

        public static string To_safe_key(string key) { return key.Replace(" ", "___"); }
        public static string From_safe_key(string key) { return key.Replace("___", " "); }

        public static Version version;                         // savegame version
        public static int uid;                                 // savegame unique id
        private static Dictionary<string, KerbalData> kerbals; // store data per-kerbal
        private static Dictionary<Guid, VesselData> vessels = new Dictionary<Guid, VesselData>();    // store data per-vessel
        public static Dictionary<string, StormData> storms;     // store data per-body
        public static LandmarkData landmarks;                  // store landmark data
        public static UIData ui;                               // store ui data
    }


} // KERBALISM



