using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{

	public static class Science
	{
		public static bool GameHasRnD
		{
			get
			{
				switch (HighLogic.CurrentGame.Mode)
				{
					case Game.Modes.CAREER:
					case Game.Modes.SCIENCE_SANDBOX:
						return true;
					default:
						return false;
				}
			}
		}

		/// <summary> When converting stock experiments / data, if the XmitDataScalar is less than this, the data will be converted as a sample.</summary>
		public const float maxXmitDataScalarForSample = 0.001f;

		// science points from transmission won't be credited until they reach this amount
		public const double minCreditBuffer = 0.1;

		// a subject will be completed (gamevent fired and popup shown) when there is less than this value to retrieve in RnD
		// this is needed because of floating point imprecisions in the in-flight science count (due to a gazillion adds of very small values)
		public const double scienceLeftForSubjectCompleted = 0.01;

		// utility things
		static readonly List<XmitFile> xmitFiles = new List<XmitFile>();

		private class XmitFile
		{
			public File file;
			public double sciencePerMB; // caching this because it's slow to get
			public PartDrive drive;
			public bool isInWarpCache;
			public File realDriveFile; // reference to the "real" file for files in the warp cache

			public XmitFile(File file, PartDrive drive, double sciencePerMB, bool isInWarpCache, File realDriveFile = null)
			{
				this.file = file;
				this.drive = drive;
				this.sciencePerMB = sciencePerMB;
				this.isInWarpCache = isInWarpCache;
				this.realDriveFile = realDriveFile;
			}
		}

		// pseudo-ctor
		public static void Init()
		{
			if (!Features.Science)
				return;

			// Add our hijacker to the science dialog prefab
			GameObject prefab = AssetBase.GetPrefab("ScienceResultsDialog");
			if (Settings.ScienceDialog)
				prefab.gameObject.AddOrGetComponent<Hijacker>();
			else
				prefab.gameObject.AddOrGetComponent<MiniHijacker>();
		}

		// consume EC for transmission, and transmit science data
		public static void Update(Vessel v, VesselData vd, ResourceInfo ec, double elapsed_s)
		{
			// do nothing if science system is disabled
			if (!Features.Science) return;

			// consume ec for transmitters
			ec.Consume(vd.Connection.ec_idle * elapsed_s, ResourceBroker.CommsIdle);

			// avoid corner-case when RnD isn't live during scene changes
			// - this avoid losing science if the buffer reach threshold during a scene change
			if (HighLogic.CurrentGame.Mode != Game.Modes.SANDBOX && ResearchAndDevelopment.Instance == null)
				return;

			// clear list of files transmitted
			vd.filesTransmitted.Clear();

			// check connection
			if (vd.Connection == null
				|| !vd.Connection.linked
				|| vd.Connection.rate <= 0.0
				|| !vd.deviceTransmit
				|| ec.Amount < vd.Connection.ec_idle * elapsed_s)
			{
				// reset all files transmit rate
				foreach (PartDrive drive in PartDrive.GetDrives(vd, true))
					foreach (File f in drive.files.Values)
						f.transmitRate = 0.0;

				// do nothing else
				return;
			}
			
			double totalTransmitCapacity = vd.Connection.rate * elapsed_s;
			double remainingTransmitCapacity = totalTransmitCapacity;

			GetFilesToTransmit(v, vd);

			if (xmitFiles.Count == 0)
				return;

			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.Science.Update-Loop");

			// traverse the list in reverse because :
			// - warp cache files are at the end, and they are always transmitted regerdless of transmit capacity
			// - others files are next, sorted in science value per MB ascending order 
			for (int i = xmitFiles.Count - 1; i >= 0; i--)
			{
				XmitFile xmitFile = xmitFiles[i];

				if (xmitFile.file.size == 0.0)
					continue;

				// always transmit everything in the warp cache
				if (!xmitFile.isInWarpCache && remainingTransmitCapacity <= 0.0)
					break;

				// determine how much data is transmitted
				double transmitted = xmitFile.isInWarpCache ? xmitFile.file.size : Math.Min(xmitFile.file.size, remainingTransmitCapacity);

				if (transmitted == 0.0)
					continue;

				// consume transmit capacity
				remainingTransmitCapacity -= transmitted;

				// get science value
				double xmitScienceValue = transmitted * xmitFile.sciencePerMB;

				// consume data in the file
				xmitFile.file.size -= transmitted;

				// remove science collected (ignoring final science value clamped to subject completion)
				xmitFile.file.subjectData.RemoveScienceCollectedInFlight(xmitScienceValue);

				// save transmit rate for the file, and add it to the VesselData list of files being transmitted
				if (xmitFile.isInWarpCache && xmitFile.realDriveFile != null)
				{
					xmitFile.realDriveFile.transmitRate = transmitted / elapsed_s;
					vd.filesTransmitted.Add(xmitFile.realDriveFile);
				}
				else
				{
					xmitFile.file.transmitRate = transmitted / elapsed_s;
					vd.filesTransmitted.Add(xmitFile.file);
				}

				if (xmitScienceValue > 0.0)
				{
					// add science to the subject (and eventually included subjects), trigger completion events, credit the science, return how much has been credited.
					vd.scienceTransmitted += xmitFile.file.subjectData.RetrieveScience(xmitScienceValue, true, v.protoVessel, xmitFile.file);
				}
			}

			UnityEngine.Profiling.Profiler.EndSample();

			// consume EC cost for transmission (ec_idle is consumed above)
			double transmittedCapacity = totalTransmitCapacity - remainingTransmitCapacity;
			double transmissionCost = (vd.Connection.ec - vd.Connection.ec_idle) * (transmittedCapacity / vd.Connection.rate);
			ec.Consume(transmissionCost, ResourceBroker.CommsXmit);
		}

		private static void GetFilesToTransmit(Vessel v, VesselData vd)
		{
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.Science.GetFilesToTransmit");
			PartDrive warpCache = Cache.WarpCache(v);

			xmitFiles.Clear();
			List<SubjectData> filesToRemove = new List<SubjectData>();

			foreach (PartDrive drive in PartDrive.GetDrives(vd, true))
			{
				foreach (File f in drive.files.Values)
				{
					// always reset transmit rate
					f.transmitRate = 0.0;

					// delete empty files that aren't being transmitted
					// note : this won't work in case the same subject is split over multiple files (on different drives)
					if (f.size <= 0.0 && (!warpCache.files.ContainsKey(f.subjectData) || warpCache.files[f.subjectData].size <= 0.0))
					{
						filesToRemove.Add(f.subjectData);
						continue;
					}

					// get files tagged for transmit
					if (drive.GetFileSend(f.subjectData.Id))
					{
						xmitFiles.Add(new XmitFile(f, drive, f.subjectData.SciencePerMB, false));
					}
				}

				// delete found empty files from the drive
				foreach (SubjectData fileToRemove in filesToRemove)
					drive.files.Remove(fileToRemove);

				filesToRemove.Clear();
			}

			// sort files by science value per MB ascending order so high value files are transmitted first
			// because XmitFile list is processed from end to start
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.Science.GetFilesToTransmit-Sort");
			xmitFiles.Sort((x, y) => x.sciencePerMB.CompareTo(y.sciencePerMB));
			UnityEngine.Profiling.Profiler.EndSample();

			// add all warpcache files to the beginning of the XmitFile list
			foreach (File f in warpCache.files.Values)
			{
				// don't transmit empty files
				if (f.size <= 0.0)
					continue;

				// find the file on a "real" drive that correspond to this warpcache file
				// this allow to use the real file for displaying transmit info and saving state (filemanager, monitor, vesseldata...)
				int driveFileIndex = xmitFiles.FindIndex(df => df.file.subjectData == f.subjectData);
				if (driveFileIndex >= 0)
					xmitFiles.Add(new XmitFile(f, warpCache, f.subjectData.SciencePerMB, true, xmitFiles[driveFileIndex].file));
				else
					xmitFiles.Add(new XmitFile(f, warpCache, f.subjectData.SciencePerMB, true)); // should not be happening, but better safe than sorry

			}
			UnityEngine.Profiling.Profiler.EndSample();
		}

		// return module acting as container of an experiment
		public static IScienceDataContainer Container(Part p, string experiment_id)
		{
			// first try to get a stock experiment module with the right experiment id
			// - this support parts with multiple experiment modules, like eva kerbal
			foreach (ModuleScienceExperiment exp in p.FindModulesImplementing<ModuleScienceExperiment>())
			{
				if (exp.experimentID == experiment_id) return exp;
			}

			// if none was found, default to the first module implementing the science data container interface
			// - this support third-party modules that implement IScienceDataContainer, but don't derive from ModuleScienceExperiment
			return p.FindModuleImplementing<IScienceDataContainer>();
		}

		/// <summary>
		/// Return the result description (Experiment definition RESULTS node) for the subject_id.
		/// Same as the stock ResearchAndDevelopment.GetResults(subject_id) but can be forced to return a non-randomized result
		/// </summary>
		/// <param name="randomized">If true the result can be different each this is called</param>
		/// <param name="useGenericIfNotFound">If true, a generic text will be returned if no RESULTS{} definition exists</param>
		public static string SubjectResultDescription(string subject_id, bool useGenericIfNotFound = true)
		{
			string result = ResearchAndDevelopment.GetResults(subject_id);
			if (result == null) result = string.Empty;
			if (result == string.Empty && useGenericIfNotFound)
			{
				result = Lib.TextVariant(
					  Local.SciencresultText1,//"Our researchers will jump on it right now"
					  Local.SciencresultText2,//"This cause some excitement"
					  Local.SciencresultText3,//"These results are causing a brouhaha in R&D"
					  Local.SciencresultText4,//"Our scientists look very confused"
					  Local.SciencresultText5);//"The scientists won't believe these readings"
			}
			return result;
		}
	}

} // KERBALISM

