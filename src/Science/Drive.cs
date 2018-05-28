﻿using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;

namespace KERBALISM
{


	public sealed class Drive
	{
		public Drive()
		{
			files = new Dictionary<string, File>();
			samples = new Dictionary<string, Sample>();
			location = 0;
		}

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

			// parse preferred location
			location = Lib.ConfigValue(node, "location", 0u);
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

			// save preferred location
			node.AddValue("location", location);
		}

		// add science data, creating new file or incrementing existing one
		public void Record_file(string subject_id, double amount)
		{
			// create new data or get existing one
			File file;
			if (!files.TryGetValue(subject_id, out file))
			{
				file = new File();
				files.Add(subject_id, file);
			}

			// increase amount of data stored in the file
			file.size += amount;

			// clamp file size to max amount that can be collected
			file.size = Math.Min(file.size, Science.Experiment(subject_id).max_amount);
		}

		// add science sample, creating new sample or incrementing existing one
		public void Record_sample(string subject_id, double amount)
		{
			// create new data or get existing one
			Sample sample;
			if (!samples.TryGetValue(subject_id, out sample))
			{
				sample = new Sample();
				samples.Add(subject_id, sample);
			}

			// increase amount of data stored in the sample
			sample.size += amount;

			// clamp file size to max amount that can be collected
			sample.size = Math.Min(sample.size, Science.Experiment(subject_id).max_amount);
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
		public void Move(Drive destination)
		{
			// copy files
			foreach (var p in files)
			{
				destination.Record_file(p.Key, p.Value.size);
				destination.files[p.Key].buff += p.Value.buff; //< move the buffer along with the size
			}

			// copy samples
			foreach (var p in samples)
			{
				destination.Record_sample(p.Key, p.Value.size);
			}

			// clear source drive
			files.Clear();
			samples.Clear();
		}


		// return size of data stored in Mb (including samples)
		public double Size()
		{
			double amount = 0.0;
			foreach (var p in files)
			{
				amount += p.Value.size;
			}
			foreach (var p in samples)
			{
				amount += p.Value.size;
			}
			return amount;
		}


		// transfer data between two vessels
		public static void Transfer(Vessel src, Vessel dst)
		{
			// get drives
			Drive a = DB.Vessel(src).drive;
			Drive b = DB.Vessel(dst).drive;

			// get size of data being transfered
			double amount = a.Size();

			// if there is data
			if (amount > double.Epsilon)
			{
				// transfer the data
				a.Move(b);

				// inform the user
				Message.Post
				(
				  Lib.BuildString(Lib.HumanReadableDataSize(amount), " ", Localizer.Format("#KERBALISM_Science_ofdatatransfer")),
				  Lib.BuildString(Localizer.Format("#KERBALISM_Generic_FROM"), " <b>", src.vesselName, "</b> ", Localizer.Format("#KERBALISM_Generic_TO"), " <b>", dst.vesselName, "</b>")
				);
			}
		}


		public Dictionary<string, File> files;      // science files
		public Dictionary<string, Sample> samples;  // science samples
		public uint location;                       // where the data is stored specifically, optional
	}


} // KERBALISM

