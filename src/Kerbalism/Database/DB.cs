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

            // load vessels data
            if (node.HasNode("vessels2")) // old vessels used flightId, we switched to Guid with vessels2
            {
                foreach (var vessel_node in node.GetNode("vessels2").GetNodes())
                {
                    Guid vId = Lib.Parse.ToGuid(vessel_node.name);
                    VesselData vd;
                    if (!vessels.ContainsKey(vId))
                    {
                        vd = new VesselData();
                        vessels.Add(Lib.Parse.ToGuid(vessel_node.name), vd);
                    }
                    else
                    {
                        vd = vessels[vId];
                    }
                    vd.Load(vessel_node);
                }
            }

			// load the science database, has to be before drives are loaded
			ScienceDB.Load(node);

			// load drives
			drives = new Dictionary<uint, Drive>();
            if (node.HasNode("drives")) // old vessels used flightId, we switched to Guid with vessels2
            {
                foreach (var drive_node in node.GetNode("drives").GetNodes())
                {
                    drives.Add(Lib.Parse.ToUInt(drive_node.name), new Drive(drive_node));
                }
            }

            // load bodies data
            bodies = new Dictionary<string, StormData>();
            if (node.HasNode("bodies"))
            {
                foreach (var body_node in node.GetNode("bodies").GetNodes())
                {
                    bodies.Add(From_safe_key(body_node.name), new StormData(body_node));
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

            // save vessels data, and clean the database of vessels that no longer exists
            var vessels_node = node.AddNode("vessels2");
			List<Guid> vesselsToRemove = new List<Guid>();
            foreach (var p in vessels)
            {
				if (p.Value.Vessel == null)
				{
					vesselsToRemove.Add(p.Key);
					continue;
				}
                p.Value.Save(vessels_node.AddNode(p.Key.ToString()));
            }

			foreach (Guid guid in vesselsToRemove)
				vessels.Remove(guid);

			// save the science database
			ScienceDB.Save(node);

			// save drives
			var drives_node = node.AddNode("drives");
            foreach (var p in drives)
            {
                p.Value.Save(drives_node.AddNode(p.Key.ToString()));
            }

            // save bodies data
            var bodies_node = node.AddNode("bodies");
            foreach (var p in bodies)
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

        /// <summary> use the KerbalismData vessel extension method instead</summary>
        private static VesselData VesselData(Vessel v)
        {
            Guid vesselId = Lib.VesselID(v);
            if (!vessels.ContainsKey(vesselId))
            {
                vessels.Add(vesselId, new VesselData());
            }
            return vessels[vesselId];
        }

        public static VesselData KerbalismData(this Vessel vessel)
        {
            return VesselData(vessel);
        }

        /// <summary>shortcut for VesselData.IsValid. False in the following cases : asteroid, debris, flag, deployed ground part, dead eva, rescue</summary>
        public static bool KerbalismIsValid(this Vessel vessel)
        {
            return VesselData(vessel).IsValid;
        }

        public static void KerbalismDataDelete(this Vessel vessel)
        {
            vessels.Remove(Lib.VesselID(vessel));
        }

        public static void KerbalismDataDelete(this ProtoVessel protoVessel)
        {
            vessels.Remove(Lib.VesselID(protoVessel));
        }


        public static Drive Drive(uint partId, string title = "Brick", double dataCapacity = -1, int sampleCapacity = -1)
        {
            if (!drives.ContainsKey(partId))
            {
                var d = new Drive(title, dataCapacity, sampleCapacity);
                drives.Add(partId, d);
            }
            return drives[partId];
        }

        public static StormData Body(string name)
        {
            if (!bodies.ContainsKey(name))
            {
                bodies.Add(name, new StormData());
            }
            return bodies[name];
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
        private static Dictionary<Guid, VesselData> vessels = new Dictionary<Guid, VesselData>();    // store data per-vessel, indexed by root part id
        public static Dictionary<uint, Drive> drives;          // all drives, of all vessels
        public static Dictionary<string, StormData> bodies;     // store data per-body
        public static LandmarkData landmarks;                  // store landmark data
        public static UIData ui;                               // store ui data
    }


} // KERBALISM



