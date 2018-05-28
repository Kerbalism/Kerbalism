﻿using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{


	public static class DB
	{
		public static void Load(ConfigNode node)
		{
			// get version (or use current one for new savegames)
			version = Lib.ConfigValue(node, "version", Lib.Version());

			// get unique id (or generate one for new savegames)
			uid = Lib.ConfigValue(node, "uid", Lib.RandomInt(int.MaxValue));

			// if this is an unsupported version, print warning
			if (string.CompareOrdinal(version, "1.1.5.0") < 0)
			{
				Lib.Log("loading save from unsupported version " + version);
			}

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
			vessels = new Dictionary<uint, VesselData>();
			if (node.HasNode("vessels"))
			{
				foreach (var vessel_node in node.GetNode("vessels").GetNodes())
				{
					vessels.Add(Lib.Parse.ToUInt(vessel_node.name), new VesselData(vessel_node));
				}
			}

			// load bodies data
			bodies = new Dictionary<string, BodyData>();
			if (node.HasNode("bodies"))
			{
				foreach (var body_node in node.GetNode("bodies").GetNodes())
				{
					bodies.Add(From_safe_key(body_node.name), new BodyData(body_node));
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
			if (version != Lib.Version()) Lib.Log("savegame converted from version " + version);
		}


		public static void Save(ConfigNode node)
		{
			// save version
			node.AddValue("version", Lib.Version());

			// save unique id
			node.AddValue("uid", uid);

			// save kerbals data
			var kerbals_node = node.AddNode("kerbals");
			foreach (var p in kerbals)
			{
				p.Value.Save(kerbals_node.AddNode(To_safe_key(p.Key)));
			}

			// save vessels data
			var vessels_node = node.AddNode("vessels");
			foreach (var p in vessels)
			{
				p.Value.Save(vessels_node.AddNode(p.Key.ToString()));
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


		public static VesselData Vessel(Vessel v)
		{
			uint id = Lib.RootID(v);
			if (!vessels.ContainsKey(id))
			{
				vessels.Add(id, new VesselData());
			}
			return vessels[id];
		}


		public static BodyData Body(string name)
		{
			if (!bodies.ContainsKey(name))
			{
				bodies.Add(name, new BodyData());
			}
			return bodies[name];
		}


		public static string To_safe_key(string key) { return key.Replace(" ", "___"); }
		public static string From_safe_key(string key) { return key.Replace("___", " "); }

		public static string version;                          // savegame version
		public static int uid;                                 // savegame unique id
		public static Dictionary<string, KerbalData> kerbals;  // store data per-kerbal
		public static Dictionary<uint, VesselData> vessels;    // store data per-vessel, indexed by root part id
		public static Dictionary<string, BodyData> bodies;     // store data per-body
		public static LandmarkData landmarks;                  // store landmark data
		public static UIData ui;                               // store ui data
	}


} // KERBALISM



