using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{


	public class VesselData
	{
		public VesselData()
		{
			msg_signal = false;
			msg_belt = false;
			cfg_ec = PreferencesMessages.Instance.ec;
			cfg_supply = PreferencesMessages.Instance.supply;
			cfg_signal = PreferencesMessages.Instance.signal;
			cfg_malfunction = PreferencesMessages.Instance.malfunction;
			cfg_storm = PreferencesMessages.Instance.storm;
			cfg_script = PreferencesMessages.Instance.script;
			cfg_highlights = PreferencesBasic.Instance.highlights;
			cfg_showlink = true;
			hyspos_signal = 0.0;
			hysneg_signal = 5.0;
			storm_time = 0.0;
			storm_age = 0.0;
			storm_state = 0;
			group = "NONE";
			computer = new Computer();
			drives = new Dictionary<uint, Drive>();
			supplies = new Dictionary<string, SupplyData>();
			scansat_id = new List<uint>();
		}

		public VesselData(ConfigNode node)
		{
			msg_signal = Lib.ConfigValue(node, "msg_signal", false);
			msg_belt = Lib.ConfigValue(node, "msg_belt", false);
			cfg_ec = Lib.ConfigValue(node, "cfg_ec", PreferencesMessages.Instance.ec);
			cfg_supply = Lib.ConfigValue(node, "cfg_supply", PreferencesMessages.Instance.supply);
			cfg_signal = Lib.ConfigValue(node, "cfg_signal", PreferencesMessages.Instance.signal);
			cfg_malfunction = Lib.ConfigValue(node, "cfg_malfunction", PreferencesMessages.Instance.malfunction);
			cfg_storm = Lib.ConfigValue(node, "cfg_storm", PreferencesMessages.Instance.storm);
			cfg_script = Lib.ConfigValue(node, "cfg_script", PreferencesMessages.Instance.script);
			cfg_highlights = Lib.ConfigValue(node, "cfg_highlights", PreferencesBasic.Instance.highlights);
			cfg_showlink = Lib.ConfigValue(node, "cfg_showlink", true);
			hyspos_signal = Lib.ConfigValue(node, "hyspos_signal", 0.0);
			hysneg_signal = Lib.ConfigValue(node, "hysneg_signal", 0.0);
			storm_time = Lib.ConfigValue(node, "storm_time", 0.0);
			storm_age = Lib.ConfigValue(node, "storm_age", 0.0);
			storm_state = Lib.ConfigValue(node, "storm_state", 0u);
			group = Lib.ConfigValue(node, "group", "NONE");
			computer = node.HasNode("computer") ? new Computer(node.GetNode("computer")) : new Computer();
			drives = LoadDrives(node.GetNode("drives"));

			supplies = new Dictionary<string, SupplyData>();
			foreach (var supply_node in node.GetNode("supplies").GetNodes())
			{
				supplies.Add(DB.From_safe_key(supply_node.name), new SupplyData(supply_node));
			}

			scansat_id = new List<uint>();
			foreach (string s in node.GetValues("scansat_id"))
			{
				scansat_id.Add(Lib.Parse.ToUInt(s));
			}
		}

		public void Save(ConfigNode node)
		{
			node.AddValue("msg_signal", msg_signal);
			node.AddValue("msg_belt", msg_belt);
			node.AddValue("cfg_ec", cfg_ec);
			node.AddValue("cfg_supply", cfg_supply);
			node.AddValue("cfg_signal", cfg_signal);
			node.AddValue("cfg_malfunction", cfg_malfunction);
			node.AddValue("cfg_storm", cfg_storm);
			node.AddValue("cfg_script", cfg_script);
			node.AddValue("cfg_highlights", cfg_highlights);
			node.AddValue("cfg_showlink", cfg_showlink);
			node.AddValue("hyspos_signal", hyspos_signal);
			node.AddValue("hysneg_signal", hysneg_signal);
			node.AddValue("storm_time", storm_time);
			node.AddValue("storm_age", storm_age);
			node.AddValue("storm_state", storm_state);
			node.AddValue("group", group);
			computer.Save(node.AddNode("computer"));
			SaveDrives(node.AddNode("drives"));

			var supplies_node = node.AddNode("supplies");
			foreach (var p in supplies)
			{
				p.Value.Save(supplies_node.AddNode(DB.To_safe_key(p.Key)));
			}

			foreach (uint id in scansat_id)
			{
				node.AddValue("scansat_id", id.ToString());
			}
		}

		private Dictionary<uint, Drive> LoadDrives(ConfigNode node)
		{
			Dictionary<uint, Drive> result = new Dictionary<uint, Drive>();
			foreach(var n in node.GetNodes("drive"))
			{
				uint partId = Lib.ConfigValue(n, "partId", (uint)0);
				Drive drive = new Drive(n);
				result.Add(partId, drive);
			}
			return result;
		}

		private void SaveDrives(ConfigNode node)
		{
			foreach (var pair in drives)
			{
				var n = node.AddNode("drive");
				n.AddValue("partId", pair.Key);
				pair.Value.Save(n);
			}
		}

		public SupplyData Supply(string name)
		{
			if (!supplies.ContainsKey(name))
			{
				supplies.Add(name, new SupplyData());
			}
			return supplies[name];
		}

		public Drive DriveForPart(Part part, double dataCapacity, int sampleCapacity)
		{
			var partId = Lib.GetPartId(part);

			if(!drives.ContainsKey(partId))
				drives.Add(partId, new Drive(dataCapacity, sampleCapacity));
			return drives[partId];
		}

		public Drive BestDrive(double minDataCapacity = 0, int minSlots = 0)
		{
			Drive result = null;
			foreach(var drive in drives.Values)
			{
				if (result == null)
				{
					result = drive;
					continue;
				}

				if (minDataCapacity > double.Epsilon && drive.FileCapacityAvailable() < minDataCapacity)
					continue;
				if (minSlots > 0 && drive.SampleCapacityAvailable() < minSlots)
					continue;

				if (minDataCapacity > double.Epsilon && drive.FileCapacityAvailable() > result.FileCapacityAvailable())
					result = drive;
				if (minSlots > 0 && drive.SampleCapacityAvailable() > result.SampleCapacityAvailable())
					result = drive;
			}
			if(result == null)
			{
				// vessel has no drive.
				return new Drive(0, 0);
			}
			return result;
		}

		public bool msg_signal;       // message flag: link status
		public bool msg_belt;         // message flag: crossing radiation belt
		public bool cfg_ec;           // enable/disable message: ec level
		public bool cfg_supply;       // enable/disable message: supplies level
		public bool cfg_signal;       // enable/disable message: link status
		public bool cfg_malfunction;  // enable/disable message: malfunctions
		public bool cfg_storm;        // enable/disable message: storms
		public bool cfg_script;       // enable/disable message: scripts
		public bool cfg_highlights;   // show/hide malfunction highlights
		public bool cfg_showlink;     // show/hide link line
		public double hyspos_signal;  // used to stop toggling signal on/off when near zero ec
		public double hysneg_signal;  // used to stop toggling signal on/off when near zero ec
		public double storm_time;     // time of next storm (interplanetary CME)
		public double storm_age;      // time since last storm (interplanetary CME)
		public uint storm_state;      // 0: none, 1: inbound, 2: in progress (interplanetary CME)
		public string group;          // vessel group
		public Computer computer;     // store scripts
		public Dictionary<UInt32, Drive> drives; // store science data
		public Dictionary<string, SupplyData> supplies; // supplies data
		public List<uint> scansat_id; // used to remember scansat sensors that were disabled
	}


} // KERBALISM



