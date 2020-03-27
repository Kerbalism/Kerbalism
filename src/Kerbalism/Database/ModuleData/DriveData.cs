using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;

namespace KERBALISM
{

	public sealed class DriveData : ModuleData<ModuleKsmDrive, DriveData>
	{
		#region FIELDS

		public Dictionary<SubjectData, File> files;      // science files
		public Dictionary<SubjectData, Sample> samples;  // science samples
		public Dictionary<string, bool> fileSendFlags; // file send flags
		public double dataCapacity;
		public int sampleCapacity;
		public bool isPrivate;

		#endregion

		#region LIFECYCLE

		public override void OnFirstInstantiate(ProtoPartModuleSnapshot protoModule, ProtoPartSnapshot protoPart)
		{
			files = new Dictionary<SubjectData, File>();
			samples = new Dictionary<SubjectData, Sample>();
			fileSendFlags = new Dictionary<string, bool>();

			// modulePrefab will be null for transmit buffer drives
			if (modulePrefab != null)
			{
				dataCapacity = modulePrefab.effectiveDataCapacity;
				sampleCapacity = modulePrefab.effectiveSampleCapacity;
				isPrivate = modulePrefab.IsPrivate();
			}
			else
			{
				dataCapacity = 0.0;
				sampleCapacity = 0;
				isPrivate = false;
			}
		}

		public override void OnLoad(ConfigNode node)
		{
			// parse science  files
			files = new Dictionary<SubjectData, File>();
			ConfigNode filesNode = node.GetNode("FILES");
			if (filesNode != null)
			{
				foreach (var file_node in filesNode.GetNodes())
				{
					string subject_id = DB.FromSafeKey(file_node.name);
					File file = File.Load(subject_id, file_node);
					if (file != null)
					{
						if (files.ContainsKey(file.subjectData))
						{
							Lib.Log("discarding duplicate subject " + file.subjectData, Lib.LogLevel.Warning);
						}
						else
						{
							files.Add(file.subjectData, file);
							file.subjectData.AddDataCollectedInFlight(file.size);
						}
					}
				}
			}

			// parse science samples
			samples = new Dictionary<SubjectData, Sample>();
			ConfigNode samplesNode = node.GetNode("SAMPLES");
			if (samplesNode != null)
			{
				foreach (var sample_node in samplesNode.GetNodes())
				{
					string subject_id = DB.FromSafeKey(sample_node.name);
					Sample sample = Sample.Load(subject_id, sample_node);
					if (sample != null)
					{
						samples.Add(sample.subjectData, sample);
						sample.subjectData.AddDataCollectedInFlight(sample.size);
					}
				}
			}

			isPrivate = Lib.ConfigValue(node, "isPrivate", false);

			// parse capacities. be generous with default values for backwards
			// compatibility (drives had unlimited storage before this)
			dataCapacity = Lib.ConfigValue(node, "dataCapacity", 100000.0);
			sampleCapacity = Lib.ConfigValue(node, "sampleCapacity", 1000);

			fileSendFlags = new Dictionary<string, bool>();
			string fileNames = Lib.ConfigValue(node, "sendFileNames", string.Empty);
			foreach (string fileName in Lib.Tokenize(fileNames, ','))
			{
				Send(fileName, true);
			}
		}

		public override void OnSave(ConfigNode node)
		{
			// save science files
			bool hasFiles = false;
			ConfigNode filesNode = new ConfigNode("FILES");
			foreach (File file in files.Values)
			{
				file.Save(filesNode.AddNode(DB.ToSafeKey(file.subjectData.Id)));
				hasFiles = true;
			}

			if (hasFiles)
				node.AddNode(filesNode);

			// save science samples
			bool hasSamples = false;
			ConfigNode samplesNode = new ConfigNode("SAMPLES");
			foreach (Sample sample in samples.Values)
			{
				sample.Save(samplesNode.AddNode(DB.ToSafeKey(sample.subjectData.Id)));
				hasSamples = true;
			}

			if (hasSamples)
				node.AddNode(samplesNode);

			node.AddValue("isPrivate", isPrivate);
			node.AddValue("dataCapacity", dataCapacity);
			node.AddValue("sampleCapacity", sampleCapacity);

			string fileNames = string.Empty;
			foreach (string subjectId in fileSendFlags.Keys)
			{
				if (fileNames.Length > 0) fileNames += ",";
				fileNames += subjectId;
			}
			node.AddValue("sendFileNames", fileNames);
		}

		public override void OnPartWillDie()
		{
			DeleteAllData();
		}

		#endregion

		public static double StoreFile(Vessel vessel, SubjectData subjectData, double size, bool include_private = false)
		{
			if (size <= 0.0)
				return 0.0;

			// store what we can
			TryStoreFile(Cache.TransmitBufferDrive(vessel), subjectData, ref size);

			if (size <= 0.0)
				return size;

			foreach (var d in GetDrives(vessel, include_private))
			{
				if (!TryStoreFile(d, subjectData, ref size))
					break;
			}

			return size;
		}

		private static bool TryStoreFile(DriveData drive, SubjectData subjectData, ref double size)
		{
			var available = drive.FileCapacityAvailable();
			var chunk = Math.Min(size, available);
			if (!drive.RecordFile(subjectData, chunk, true))
				return false;
			size -= chunk;

			if (size <= 0.0)
				return false;

			return true;
		}

		// add science data, creating new file or incrementing existing one
		public bool RecordFile(SubjectData subjectData, double amount, bool allowImmediateTransmission = true, bool useStockCrediting = false)
		{
			if (dataCapacity >= 0 && FilesSize() + amount > dataCapacity)
				return false;

			// create new data or get existing one
			File file;
			if (!files.TryGetValue(subjectData, out file))
			{
				file = new File(subjectData, 0.0, useStockCrediting);
				files.Add(subjectData, file);

				if (!allowImmediateTransmission) Send(subjectData.Id, false);
			}

			// increase amount of data stored in the file
			file.size += amount;

			// keep track of data collected
			subjectData.AddDataCollectedInFlight(amount);

			return true;
		}

		public void Send(string subjectId, bool send)
		{
			if (!fileSendFlags.ContainsKey(subjectId)) fileSendFlags.Add(subjectId, send);
			else fileSendFlags[subjectId] = send;
		}

		public bool GetFileSend(string subjectId)
		{
			if (!fileSendFlags.ContainsKey(subjectId)) return PreferencesScience.Instance.transmitScience;
			return fileSendFlags[subjectId];
		}

		// add science sample, creating new sample or incrementing existing one
		public bool RecordSample(SubjectData subjectData, double amount, double mass, bool useStockCrediting = false)
		{
			int currentSampleSlots = SamplesSize();
			if (sampleCapacity >= 0)
			{
				if (!samples.ContainsKey(subjectData) && currentSampleSlots >= sampleCapacity)
				{
					// can't take a new sample if we're already at capacity
					return false;
				}
			}

			Sample sample;
			if (samples.ContainsKey(subjectData) && sampleCapacity >= 0)
			{
				// test if adding the amount to the sample would exceed our capacity
				sample = samples[subjectData];

				int existingSampleSlots = Lib.SampleSizeToSlots(sample.size);
				int newSampleSlots = Lib.SampleSizeToSlots(sample.size + amount);
				if (currentSampleSlots - existingSampleSlots + newSampleSlots > sampleCapacity)
					return false;
			}

			// create new data or get existing one
			if (!samples.TryGetValue(subjectData, out sample))
			{
				sample = new Sample(subjectData, 0.0, useStockCrediting);
				sample.analyze = PreferencesScience.Instance.analyzeSamples;
				samples.Add(subjectData, sample);
			}

			// increase amount of data stored in the sample
			sample.size += amount;
			sample.mass += mass;

			// keep track of data collected
			subjectData.AddDataCollectedInFlight(amount);

			return true;
		}

		// remove science data, deleting the file when it is empty
		public void DeleteFile(SubjectData subjectData, double amount = 0.0)
		{
			// get data
			File file;
			if (files.TryGetValue(subjectData, out file))
			{
				// decrease amount of data stored in the file
				if (amount == 0.0)
					amount = file.size;
				else
					amount = Math.Min(amount, file.size);

				file.size -= amount;

				// keep track of data collected
				subjectData.RemoveDataCollectedInFlight(amount);

				// remove file if empty
				if (file.size <= 0.0) files.Remove(subjectData);
			}
		}

		// remove science sample, deleting the sample when it is empty
		public double DeleteSample(SubjectData subjectData, double amount = 0.0)
		{
			// get data
			Sample sample;
			if (samples.TryGetValue(subjectData, out sample))
			{
				// decrease amount of data stored in the sample
				if (amount == 0.0)
					amount = sample.size;
				else
					amount = Math.Min(amount, sample.size);

				double massDelta = sample.mass * amount / sample.size;
				sample.size -= amount;
				sample.mass -= massDelta;

				// keep track of data collected
				subjectData.RemoveDataCollectedInFlight(amount);

				// remove sample if empty
				if (sample.size <= 0.0) samples.Remove(subjectData);

				return massDelta;
			}
			return 0.0;
		}

		// set analyze flag for a sample
		public void Analyze(SubjectData subjectData, bool b)
		{
			Sample sample;
			if (samples.TryGetValue(subjectData, out sample))
			{
				sample.analyze = b;
			}
		}

		// move all data to another drive
		public bool Move(DriveData destination, bool moveSamples)
		{
			bool result = true;

			// copy files
			List<SubjectData> filesList = new List<SubjectData>();
			foreach (File file in files.Values)
			{
				double size = Math.Min(file.size, destination.FileCapacityAvailable());
				if (destination.RecordFile(file.subjectData, size, true, file.useStockCrediting))
				{
					file.size -= size;
					file.subjectData.RemoveDataCollectedInFlight(size);
					if (file.size < double.Epsilon)
					{
						filesList.Add(file.subjectData);
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
			foreach (SubjectData id in filesList) files.Remove(id);

			if (!moveSamples) return result;

			// move samples
			List<SubjectData> samplesList = new List<SubjectData>();
			foreach (Sample sample in samples.Values)
			{
				double size = Math.Min(sample.size, destination.SampleCapacityAvailable(sample.subjectData));
				if (size < double.Epsilon)
				{
					result = false;
					break;
				}

				double mass = sample.mass * (sample.size / size);
				if (destination.RecordSample(sample.subjectData, size, mass, sample.useStockCrediting))
				{
					sample.size -= size;
					sample.subjectData.RemoveDataCollectedInFlight(size);
					sample.mass -= mass;

					if (sample.size < double.Epsilon)
					{
						samplesList.Add(sample.subjectData);
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
			return Math.Max(dataCapacity - FilesSize(), 0.0); // clamp to 0 due to fp precision in FilesSize()
		}

		public double FilesSize()
		{
			double amount = 0.0;
			foreach (File file in files.Values)
			{
				amount += file.size;
			}
			return amount;
		}

		public double SampleCapacityAvailable(SubjectData subject = null)
		{
			if (sampleCapacity < 0) return double.MaxValue;

			double result = Lib.SlotsToSampleSize(sampleCapacity - SamplesSize());
			if (subject != null && samples.ContainsKey(subject))
			{
				int slotsForMyFile = Lib.SampleSizeToSlots(samples[subject].size);
				double amountLostToSlotting = Lib.SlotsToSampleSize(slotsForMyFile) - samples[subject].size;
				result += amountLostToSlotting;
			}
			return result;
		}

		public int SamplesSize()
		{
			int amount = 0;
			foreach (Sample sample in samples.Values)
			{
				amount += Lib.SampleSizeToSlots(sample.size);
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
		public static bool Transfer(Vessel src, DriveData dst, bool samples)
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
		public static bool Transfer(DriveData drive, Vessel dst, bool samples)
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
					Lib.HumanReadableDataSize(dataAmount) + " " + Local.Science_ofdatatransfer,
				 	Lib.BuildString(Local.Generic_FROM, " <b>", src.vesselName, "</b> ", Local.Generic_TO, " <b>", dst.vesselName, "</b>")
				);
			else
				Message.Post
				(
					Lib.Color(Lib.BuildString("WARNING: not evering copied"), Lib.Kolor.Red, true),
					Lib.BuildString(Local.Generic_FROM, " <b>", src.vesselName, "</b> ", Local.Generic_TO, " <b>", dst.vesselName, "</b>")
				);
		}

		/// <summary> delete all files/samples in the drive</summary>
		public void DeleteAllData()
		{
			foreach (File file in files.Values)
				file.subjectData.RemoveDataCollectedInFlight(file.size);

			foreach (Sample sample in samples.Values)
				sample.subjectData.RemoveDataCollectedInFlight(sample.size);

			files.Clear();
			samples.Clear();
		}

		/// <summary> delete all files/samples in the vessel drives</summary>
		public static void DeleteDrivesData(Vessel vessel)
		{
			if (vessel.TryGetVesselData(out VesselData vd))
			{
				foreach (DriveData driveData in vd.Parts.AllModulesOfType<DriveData>())
				{
					driveData.DeleteAllData();
				}
			}
		}

		public static IEnumerable<DriveData> GetDrives (VesselData vd, bool includePrivate = false)
		{
			if (!includePrivate)
			{
				return vd.Parts.AllModulesOfType<DriveData>(p => !p.isPrivate);
			}
			else
			{
				return vd.Parts.AllModulesOfType<DriveData>();
			}
		}

		public static IEnumerable<DriveData> GetDrives(Vessel v, bool includePrivate = false)
		{
			if (v.TryGetVesselData(out VesselData vd))
			{
				return GetDrives(vd, includePrivate);
			}

			return new DriveData[0];
		}

		public static IEnumerable<DriveData> GetDrives(ProtoVessel pv, bool includePrivate = false)
		{
			if (pv.TryGetVesselData(out VesselData vd))
			{
				return GetDrives(vd, includePrivate);
			}

			return new DriveData[0];
		}

		public static void GetCapacity(VesselData vesseldata, out double free_capacity, out double total_capacity)
		{
			free_capacity = 0;
			total_capacity = 0;
			if (Features.Science)
			{
				foreach (var drive in GetDrives(vesseldata))
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

		/// <summary> Get a drive for storing files. Will return null if there are no drives on the vessel </summary>
		public static DriveData FileDrive(VesselData vesselData, double size = 0.0)
		{
			DriveData result = null;
			foreach (var drive in GetDrives(vesselData))
			{
				if (result == null)
				{
					result = drive;
					if (size > 0.0 && result.FileCapacityAvailable() >= size)
						return result;
					continue;
				}

				if (size > 0.0 && drive.FileCapacityAvailable() >= size)
				{
					return drive;
				}

				// if we're not looking for a minimum capacity, look for the biggest drive
				if (drive.dataCapacity > result.dataCapacity)
				{
					result = drive;
				}
			}
			return result;
		}

		/// <summary> Get a drive for storing samples. Will return null if there are no drives on the vessel </summary>
		public static DriveData SampleDrive(VesselData vesselData, double size = 0, SubjectData subject = null)
		{
			DriveData result = null;
			foreach (var drive in GetDrives(vesselData))
			{
				if (result == null)
				{
					result = drive;
					continue;
				}

				double available = drive.SampleCapacityAvailable(subject);
				if (size > double.Epsilon && available < size)
					continue;
				if (available > result.SampleCapacityAvailable(subject))
					result = drive;
			}
			return result;
		}


	}


} // KERBALISM

