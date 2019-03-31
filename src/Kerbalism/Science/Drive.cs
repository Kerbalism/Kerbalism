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
					files.Add(DB.From_safe_key(file_node.name), new File(file_node));
				}
			}

			// parse science samples
			samples = new Dictionary<string, Sample>();
			if (node.HasNode("samples"))
			{
				foreach (var sample_node in node.GetNode("samples").GetNodes())
				{
					samples.Add(DB.From_safe_key(sample_node.name), new Sample(sample_node));
				}
			}

			name = Lib.ConfigValue(node, "name", "DRIVE");

			// parse capacities. be generous with default values for backwards
			// compatibility (drives had unlimited storage before this)
			dataCapacity = Lib.ConfigValue(node, "dataCapacity", 100000.0);
			sampleCapacity = Lib.ConfigValue(node, "sampleCapacity", 1000);
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
			node.AddValue("dataCapacity", dataCapacity);
			node.AddValue("sampleCapacity", sampleCapacity);
		}

		// add science data, creating new file or incrementing existing one
		public bool Record_file(string subject_id, double amount, bool allowImmediateTransmission = true, bool silentTransmission = false)
		{
			if (FilesSize() + amount > dataCapacity)
				return false;

			// create new data or get existing one
			File file;
			if (!files.TryGetValue(subject_id, out file))
			{
				file = new File();
				if (!allowImmediateTransmission) file.send = false;
				file.silentTransmission = silentTransmission;
				files.Add(subject_id, file);
			}

			// increase amount of data stored in the file
			file.size += amount;

			// clamp file size to max amount that can be collected
			file.size = Math.Min(file.size, Science.Experiment(subject_id).max_amount);
			file.ts = Planetarium.GetUniversalTime();

			return true;
		}

		public void Transmit_file(string subject_id)
		{
			File file;
			if (!files.TryGetValue(subject_id, out file))
				return;
			file.send = true;
		}

		// add science sample, creating new sample or incrementing existing one
		public bool Record_sample(string subject_id, double amount, double mass)
		{
			int currentSampleSlots = SamplesSize();
			if(!samples.ContainsKey(subject_id) && currentSampleSlots >= sampleCapacity)
			{
				// can't take a new sample if we're already at capacity
				return false;
			}

			Sample sample;
			if (samples.ContainsKey(subject_id))
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
				sample = new Sample();
				samples.Add(subject_id, sample);
			}

			// increase amount of data stored in the sample,
			// but clamp file size to max amount that can be collected
			var maxSize = Science.Experiment(subject_id).max_amount;
			var sizeDelta = maxSize - sample.size;
			if(sizeDelta >= amount)
			{
				sample.size += amount;
				sample.mass += mass;
			}
			else
			{
				sample.size += sizeDelta;

				var f = sizeDelta / amount; // how much of the desired amount can we add
				sample.mass += mass * f; // add the proportional amount of mass
			}
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
				file.size -= amount;
				file.ts = Planetarium.GetUniversalTime();

				// remove file if empty
				if (file.size <= double.Epsilon) files.Remove(subject_id);
			}
		}

		// remove science sample, deleting the sample when it is empty
		public void Delete_sample(string subject_id, double amount)
		{
			// get data
			Sample sample;
			if (samples.TryGetValue(subject_id, out sample))
			{
				// decrease amount of data stored in the sample
				sample.size -= amount;

				// remove sample if empty
				if (sample.size <= double.Epsilon) samples.Remove(subject_id);
			}
		}

		// set send flag for a file
		public void Send(string subject_id, bool b)
		{
			File file;
			if (files.TryGetValue(subject_id, out file))
			{
				file.send = b;
			}
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
		public bool Move(Drive destination, bool moveSamples = false)
		{
			bool result = true;

			// copy files
			var filesList = new List<string>();
			foreach (var p in files)
			{
				if (destination.Record_file(p.Key, p.Value.size))
				{
					destination.files[p.Key].buff += p.Value.buff; //< move the buffer along with the size
					filesList.Add(p.Key);
				}
				else
					result = false;
			}
			foreach (var id in filesList) files.Remove(id);

			if(moveSamples)
			{
				// copy samples
				var samplesList = new List<string>();
				foreach (var p in samples)
				{
					if (destination.Record_sample(p.Key, p.Value.size, p.Value.mass))
						samplesList.Add(p.Key);
					else
						result = false;
				}
				foreach (var id in samplesList) samples.Remove(id);
			}

			return result; // true if everything was moved, false otherwise
		}

		public double FileCapacityAvailable()
		{
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
			double result = Lib.SlotsToSampleSize(sampleCapacity - SamplesSize());
			if(samples.ContainsKey(filename)) {
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
			return Lib.BuildString(Lib.HumanReadableDataSize(FilesSize()), "  ", Lib.HumanReadableSampleSize(SamplesSize()));
		}

		public bool Empty()
		{
			return FilesSize() < double.Epsilon && SamplesSize() == 0;
		}

		// transfer data between two vessels
		public static void Transfer(Vessel src, Vessel dst, bool samples = false)
		{
			double dataAmount = 0.0;
			int sampleSlots = 0;
			foreach (var drive in DB.Vessel(src).drives.Values)
			{
				dataAmount += drive.FilesSize();
				sampleSlots += drive.SamplesSize();
			}

			if (dataAmount < double.Epsilon && (sampleSlots == 0 || !samples))
				return;

			// get drives
			var allSrc = DB.Vessel(src).drives.Values;
			var allDst = DB.Vessel(dst).drives.Values;

			bool allMoved = true;
			foreach(var a in allSrc)
			{
				bool aMoved = false;
				foreach(var b in allDst)
				{
					if(a.Move(b, samples))
					{
						aMoved = true;
						break;
					}
				}
				allMoved &= aMoved;
			}

			// inform the user
			if (allMoved)
				Message.Post
				(
					Localizer.Format("#KERBALISM_Science_ofdatatransfer"),
				 	Lib.BuildString(Localizer.Format("#KERBALISM_Generic_FROM"), " <b>", src.vesselName, "</b> ", Localizer.Format("#KERBALISM_Generic_TO"), " <b>", dst.vesselName, "</b>")
				);
			else
				Message.Post
				(
					Lib.Color("red", Lib.BuildString("WARNING: not evering copied"), true),
					Lib.BuildString(Localizer.Format("#KERBALISM_Generic_FROM"), " <b>", src.vesselName, "</b> ", Localizer.Format("#KERBALISM_Generic_TO"), " <b>", dst.vesselName, "</b>")
				);
		}


		public Dictionary<string, File> files;      // science files
		public Dictionary<string, Sample> samples;  // science samples
		public double dataCapacity;
		public int sampleCapacity;
		public string name = String.Empty;
	}


} // KERBALISM

