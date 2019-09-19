using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;

namespace KERBALISM
{

	public sealed class Drive
	{
		public Drive(string name, double dataCapacity, int sampleCapacity)
		{
			this.files = new Dictionary<string, File>();
			this.samples = new Dictionary<string, Sample>();
			this.fileSendFlags = new Dictionary<string, bool>();
			this.dataCapacity = dataCapacity;
			this.sampleCapacity = sampleCapacity;
			this.name = name;
		}

		public Drive() : this("Brick", 0, 0) { }

		public Drive(ConfigNode node)
		{
			// parse science  files
			files = new Dictionary<string, File>();
			if (node.HasNode("files"))
			{
				foreach (var file_node in node.GetNode("files").GetNodes())
				{
					string subject_id = DB.From_safe_key(file_node.name);
					files.Add(subject_id, new File(subject_id, file_node));
				}
			}

			// parse science samples
			samples = new Dictionary<string, Sample>();
			if (node.HasNode("samples"))
			{
				foreach (var sample_node in node.GetNode("samples").GetNodes())
				{
					string subject_id = DB.From_safe_key(sample_node.name);
					samples.Add(subject_id, new Sample(subject_id, sample_node));
				}
			}

			name = Lib.ConfigValue(node, "name", "DRIVE");
			is_private = Lib.ConfigValue(node, "is_private", false);

			// parse capacities. be generous with default values for backwards
			// compatibility (drives had unlimited storage before this)
			dataCapacity = Lib.ConfigValue(node, "dataCapacity", 100000.0);
			sampleCapacity = Lib.ConfigValue(node, "sampleCapacity", 1000);

			fileSendFlags = new Dictionary<string, bool>();
			var fileNames = Lib.ConfigValue(node, "sendFileNames", string.Empty);
			foreach (var fileName in Lib.Tokenize(fileNames, ','))
			{
				Send(fileName, true);
			}
		}

		public void Save(ConfigNode node)
		{
			// save science files
			var files_node = node.AddNode("files");
			foreach (var p in files)
			{
				p.Value.Save(files_node.AddNode(DB.To_safe_key(p.Key)));
			}

			// save science samples
			var samples_node = node.AddNode("samples");
			foreach (var p in samples)
			{
				p.Value.Save(samples_node.AddNode(DB.To_safe_key(p.Key)));
			}

			node.AddValue("name", name);
			node.AddValue("is_private", is_private);
			node.AddValue("dataCapacity", dataCapacity);
			node.AddValue("sampleCapacity", sampleCapacity);

			string fileNames = string.Empty;
			foreach (var fileName in fileSendFlags.Keys)
			{
				if (fileNames.Length > 0) fileNames += ",";
				fileNames += fileName;
			}
			node.AddValue("sendFileNames", fileNames);
		}

		public static double StoreFile(Vessel vessel, string subject_id, double size, bool include_private = false)
		{
			if (size < double.Epsilon)
				return 0;

			// store what we can

			var drives = GetDrives(vessel, include_private);
			drives.Insert(0, Cache.WarpCache(vessel));

			foreach (var d in drives)
			{
				var available = d.FileCapacityAvailable();
				var chunk = Math.Min(size, available);
				if (!d.Record_file(subject_id, chunk, true))
					break;
				size -= chunk;

				if (size < double.Epsilon)
					break;
			}

			return size;
		}

		// add science data, creating new file or incrementing existing one
		public bool Record_file(string subject_id, double amount, bool allowImmediateTransmission = true, bool clamp = true)
		{
			if (dataCapacity >= 0 && FilesSize() + amount > dataCapacity)
				return false;

			// create new data or get existing one
			File file;
			if (!files.TryGetValue(subject_id, out file))
			{
				file = new File(subject_id);
				files.Add(subject_id, file);

				if (!allowImmediateTransmission) Send(subject_id, false);
			}

			// increase amount of data stored in the file
			file.size += amount;

			// keep track of data collected
			Science.Experiment(subject_id).AddDataCollectedInFlight(amount);

			return true;
		}

		public void Send(string filename, bool send)
		{
			if (!fileSendFlags.ContainsKey(filename)) fileSendFlags.Add(filename, send);
			else fileSendFlags[filename] = send;
		}

		public bool GetFileSend(string filename)
		{
			if (!fileSendFlags.ContainsKey(filename)) return PreferencesScience.Instance.transmitScience;
			return fileSendFlags[filename];
		}

		// add science sample, creating new sample or incrementing existing one
		public bool Record_sample(string subject_id, double amount, double mass)
		{
			int currentSampleSlots = SamplesSize();
			if (sampleCapacity >= 0)
			{
				if (!samples.ContainsKey(subject_id) && currentSampleSlots >= sampleCapacity)
				{
					// can't take a new sample if we're already at capacity
					return false;
				}
			}

			Sample sample;
			if (samples.ContainsKey(subject_id) && sampleCapacity >= 0)
			{
				// test if adding the amount to the sample would exceed our capacity
				sample = samples[subject_id];

				int existingSampleSlots = Lib.SampleSizeToSlots(sample.size);
				int newSampleSlots = Lib.SampleSizeToSlots(sample.size + amount);
				if (currentSampleSlots - existingSampleSlots + newSampleSlots > sampleCapacity)
					return false;
			}

			// create new data or get existing one
			if (!samples.TryGetValue(subject_id, out sample))
			{
				sample = new Sample(subject_id);
				sample.analyze = PreferencesScience.Instance.analyzeSamples;
				samples.Add(subject_id, sample);
			}

			// increase amount of data stored in the sample
			sample.size += amount;
			sample.mass += mass;

			// keep track of data collected
			Science.Experiment(subject_id).AddDataCollectedInFlight(amount);

			return true;
		}

		// remove science data, deleting the file when it is empty
		public void Delete_file(string subject_id, double amount)
		{
			// get data
			File file;
			if (files.TryGetValue(subject_id, out file))
			{
				// decrease amount of data stored in the file
				amount = Math.Min(file.size, amount);
				file.size -= amount;

				// keep track of data collected
				Science.Experiment(subject_id).RemoveDataCollectedInFlight(amount);

				// remove file if empty
				if (file.size <= 0.0) files.Remove(subject_id);
			}
		}

		// remove science sample, deleting the sample when it is empty
		public double Delete_sample(string subject_id, double amount)
		{
			// get data
			Sample sample;
			if (samples.TryGetValue(subject_id, out sample))
			{
				// decrease amount of data stored in the sample
				amount = Math.Min(amount, sample.size);
				double massDelta = sample.mass * amount / sample.size;
				sample.size -= amount;
				sample.mass -= massDelta;

				// keep track of data collected
				Science.Experiment(subject_id).RemoveDataCollectedInFlight(amount);

				// remove sample if empty
				if (sample.size <= 0.0) samples.Remove(subject_id);

				return massDelta;
			}
			return 0.0;
		}

		// set analyze flag for a sample
		public void Analyze(string subject_id, bool b)
		{
			Sample sample;
			if (samples.TryGetValue(subject_id, out sample))
			{
				sample.analyze = b;
			}
		}

		// move all data to another drive
		public bool Move(Drive destination, bool moveSamples)
		{
			bool result = true;

			// copy files
			var filesList = new List<string>();
			foreach (var p in files)
			{
				double size = Math.Min(p.Value.size, destination.FileCapacityAvailable());
				if (destination.Record_file(p.Key, size, true))
				{
					p.Value.size -= size;
					Science.Experiment(p.Key).RemoveDataCollectedInFlight(size);
					if (p.Value.size < double.Epsilon)
					{
						filesList.Add(p.Key);
					}
					else
					{
						result = false;
						break;
					}
				}
				else
				{
					result = false;
					break;
				}
			}
			foreach (var id in filesList) files.Remove(id);

			if (!moveSamples) return result;

			// move samples
			var samplesList = new List<string>();
			foreach (var p in samples)
			{
				double size = Math.Min(p.Value.size, destination.SampleCapacityAvailable(p.Key));
				if (size < double.Epsilon)
				{
					result = false;
					break;
				}

				double mass = p.Value.mass * (p.Value.size / size);
				if (destination.Record_sample(p.Key, size, mass))
				{
					p.Value.size -= size;
					Science.Experiment(p.Key).RemoveDataCollectedInFlight(size);
					p.Value.mass -= mass;

					if (p.Value.size < double.Epsilon)
					{
						samplesList.Add(p.Key);
					}
					else
					{
						result = false;
						break;
					}
				}
				else
				{
					result = false;
					break;
				}
			}
			foreach (var id in samplesList) samples.Remove(id);

			return result; // true if everything was moved, false otherwise
		}

		public double FileCapacityAvailable()
		{
			if (dataCapacity < 0) return double.MaxValue;
			return dataCapacity - FilesSize();
		}

		public double FilesSize()
		{
			double amount = 0.0;
			foreach (var p in files)
			{
				amount += p.Value.size;
			}
			return amount;
		}

		public double SampleCapacityAvailable(string filename = "")
		{
			if (sampleCapacity < 0) return double.MaxValue;

			double result = Lib.SlotsToSampleSize(sampleCapacity - SamplesSize());
			if (samples.ContainsKey(filename))
			{
				int slotsForMyFile = Lib.SampleSizeToSlots(samples[filename].size);
				double amountLostToSlotting = Lib.SlotsToSampleSize(slotsForMyFile) - samples[filename].size;
				result += amountLostToSlotting;
			}
			return result;
		}

		public int SamplesSize()
		{
			int amount = 0;
			foreach (var p in samples)
			{
				amount += Lib.SampleSizeToSlots(p.Value.size);
			}
			return amount;
		}

		// return size of data stored in Mb (including samples)
		public string Size()
		{
			var f = FilesSize();
			var s = SamplesSize();
			var result = f > double.Epsilon ? Lib.HumanReadableDataSize(f) : "";
			if (result.Length > 0) result += " ";
			if (s > 0) result += Lib.HumanReadableSampleSize(s);
			return result;
		}

		public bool Empty()
		{
			return files.Count + samples.Count == 0;
		}

		// transfer data from a vessel to a drive
		public static bool Transfer(Vessel src, Drive dst, bool samples)
		{
			double dataAmount = 0.0;
			int sampleSlots = 0;
			foreach (var drive in GetDrives(src, true))
			{
				dataAmount += drive.FilesSize();
				sampleSlots += drive.SamplesSize();
			}

			if (dataAmount < double.Epsilon && (sampleSlots == 0 || !samples))
				return true;

			// get drives
			var allSrc = GetDrives(src, true);

			bool allMoved = true;
			foreach (var a in allSrc)
			{
				if (a.Move(dst, samples))
				{
					allMoved = true;
					break;
				}
			}

			return allMoved;
		}

		// transfer data from a drive to a vessel
		public static bool Transfer(Drive drive, Vessel dst, bool samples)
		{
			double dataAmount = drive.FilesSize();
			int sampleSlots = drive.SamplesSize();

			if (dataAmount < double.Epsilon && (sampleSlots == 0 || !samples))
				return true;

			// get drives
			var allDst = GetDrives(dst);

			bool allMoved = true;
			foreach (var b in allDst)
			{
				if (drive.Move(b, samples))
				{
					allMoved = true;
					break;
				}
			}

			return allMoved;
		}

		// transfer data between two vessels
		public static void Transfer(Vessel src, Vessel dst, bool samples)
		{
			double dataAmount = 0.0;
			int sampleSlots = 0;
			foreach (var drive in GetDrives(src, true))
			{
				dataAmount += drive.FilesSize();
				sampleSlots += drive.SamplesSize();
			}

			if (dataAmount < double.Epsilon && (sampleSlots == 0 || !samples))
				return;

			var allSrc = GetDrives(src, true);
			bool allMoved = false;
			foreach (var a in allSrc)
			{
				if (Transfer(a, dst, samples))
				{
					allMoved = true;
					break;
				}
			}

			// inform the user
			if (allMoved)
				Message.Post
				(
					Lib.HumanReadableDataSize(dataAmount) + " " + Localizer.Format("#KERBALISM_Science_ofdatatransfer"),
				 	Lib.BuildString(Localizer.Format("#KERBALISM_Generic_FROM"), " <b>", src.vesselName, "</b> ", Localizer.Format("#KERBALISM_Generic_TO"), " <b>", dst.vesselName, "</b>")
				);
			else
				Message.Post
				(
					Lib.Color(Lib.BuildString("WARNING: not evering copied"), Lib.KColor.Red, true),
					Lib.BuildString(Localizer.Format("#KERBALISM_Generic_FROM"), " <b>", src.vesselName, "</b> ", Localizer.Format("#KERBALISM_Generic_TO"), " <b>", dst.vesselName, "</b>")
				);
		}

		public void Purge(uint flightID)
		{
			foreach (var file in files)
				Science.Experiment(file.Key).RemoveDataCollectedInFlight(file.Value.size);

			foreach (var sample in samples)
				Science.Experiment(sample.Key).RemoveDataCollectedInFlight(sample.Value.size);

			DB.drives.Remove(flightID);
		}

		public static void Purge(ProtoVessel proto_vessel)
		{
			foreach (ProtoPartSnapshot p in proto_vessel.protoPartSnapshots)
			{
				if (DB.drives.ContainsKey(p.flightID))
					DB.drives[p.flightID].Purge(p.flightID);
			}
		}

		public static void Purge(Vessel vessel)
		{
			foreach (var kvp in GetDriveParts(vessel))
			{
				kvp.Value.Purge(kvp.Key);
			}
		}

		public static List<Drive> GetDrives(ProtoVessel proto_vessel)
		{
			List<Drive> result = new List<Drive>();

			foreach (var hd in proto_vessel.protoPartSnapshots)
			{
				if (DB.drives.ContainsKey(hd.flightID))
					result.Add(DB.drives[hd.flightID]);
			}

			return result;
		}

		public static Dictionary<uint, Drive> GetDriveParts(Vessel vessel)
		{
			Dictionary<uint, Drive> result = Cache.VesselObjectsCache<Dictionary<uint, Drive>>(vessel, "drives");
			if (result != null)
				return result;
			result = new Dictionary<uint, Drive>();

			if(vessel.loaded)
			{
				foreach (var hd in vessel.FindPartModulesImplementing<HardDrive>())
				{
					if (!hd.isEnabled || hd.hdId == 0) continue;

					if (result.ContainsKey(hd.part.flightID))
					{
						Lib.Log("WARNING: you have multiple hard drives in one part. This is not supported and an issue in your configuration. I will combine the capacities of all affected drives. (loaded)");
						hd.enabled = false;
						hd.isEnabled = false;

						var existing = result[hd.part.flightID];
						if(hd.dataCapacity > 0) existing.dataCapacity += hd.dataCapacity;
						if(hd.sampleCapacity > 0) existing.sampleCapacity += hd.sampleCapacity;
					}
					else
					{
						if (DB.drives.ContainsKey(hd.hdId))
							result.Add(hd.part.flightID, DB.drives[hd.hdId]);
					}
				}
			}
			else
			{
				foreach (var p in vessel.protoVessel.protoPartSnapshots)
				{
					foreach(var pm in Lib.FindModules(p, "HardDrive"))
					{
						var isEnabled = Lib.Proto.GetBool(pm, "isEnabled", true);
						var hdId = Lib.Proto.GetUInt(pm, "hdId", 0);
						if (!isEnabled || hdId == 0) continue;

						if (result.ContainsKey(p.flightID))
						{
							Lib.Log("WARNING: you have multiple hard drives in one part. This is not supported and an issue in your configuration. I will combine the capacities of all affected drives. (unloaded)");
							Lib.Proto.Set(pm, "enabled", false);
							Lib.Proto.Set(pm, "isEnabled", false);

							var dataCapacity = Lib.Proto.GetFloat(pm, "dataCapacity", 0);
							var sampleCapacity = Lib.Proto.GetInt(pm, "sampleCapacity", 0);

							var existing = result[p.flightID];
							if (dataCapacity > 0) existing.dataCapacity += dataCapacity;
							if (sampleCapacity > 0) existing.sampleCapacity += sampleCapacity;
						}

						if (DB.drives.ContainsKey(hdId))
							result.Add(p.flightID, DB.drives[hdId]);
					}
				}
			}

			Cache.SetVesselObjectsCache(vessel, "drives", result);
			return result;
		}

		public static List<Drive> GetDrives(Vessel vessel, bool include_private = false)
		{
			List<Drive> result = new List<Drive>();
			foreach(var d in GetDriveParts(vessel).Values)
			{
				if (!d.is_private || include_private)
					result.Add(d);
			}
			return result;
		}

		public static void GetCapacity(Vessel vessel, out double free_capacity, out double total_capacity)
		{
			free_capacity = 0;
			total_capacity = 0;
			if (Features.Science)
			{
				foreach (var drive in GetDrives(vessel))
				{
					if (drive.dataCapacity < 0 || free_capacity < 0)
					{
						free_capacity = -1;
					}
					else
					{
						free_capacity += drive.FileCapacityAvailable();
						total_capacity += drive.dataCapacity;
					}
				}

				if (free_capacity < 0)
				{
					free_capacity = double.MaxValue;
					total_capacity = double.MaxValue;
				}
			}
		}

		public static Drive FileDrive(Vessel vessel, double size = 0)
		{
			Drive result = null;
			foreach (var drive in GetDrives(vessel))
			{
				if (result == null)
				{
					result = drive;
					if (size > double.Epsilon && result.FileCapacityAvailable() >= size)
						return result;
					continue;
				}

				if (size > double.Epsilon && drive.FileCapacityAvailable() >= size)
				{
					return drive;
				}

				// if we're not looking for a minimum capacity, look for the biggest drive
				if (drive.dataCapacity > result.dataCapacity)
				{
					result = drive;
				}
			}
			if (result == null)
			{
				// vessel has no drive.
				return new Drive("Broken", 0, 0);
			}
			return result;
		}

		public static Drive SampleDrive(Vessel vessel, double size = 0, string filename = "")
		{
			Drive result = null;
			foreach (var drive in GetDrives(vessel))
			{
				if (result == null)
				{
					result = drive;
					continue;
				}

				double available = drive.SampleCapacityAvailable(filename);
				if (size > double.Epsilon && available < size)
					continue;
				if (available > result.SampleCapacityAvailable(filename))
					result = drive;
			}
			if (result == null)
			{
				// vessel has no drive.
				return new Drive("Broken", 0, 0);
			}
			return result;
		}

		public Dictionary<string, File> files;      // science files
		public Dictionary<string, Sample> samples;  // science samples
		public Dictionary<string, bool> fileSendFlags; // file send flags
		public double dataCapacity;
		public int sampleCapacity;
		public string name = String.Empty;
		public bool is_private = false;
	}


} // KERBALISM

