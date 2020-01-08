using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;


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
			public Drive drive;
			public bool isInWarpCache;
			public File realDriveFile; // reference to the "real" file for files in the warp cache

			public XmitFile(File file, Drive drive, double sciencePerMB, bool isInWarpCache, File realDriveFile = null)
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
			ec.Consume(vd.Connection.ec_idle * elapsed_s, "comms (idle)");

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
				foreach (Drive drive in Drive.GetDrives(vd, true))
					foreach (File f in drive.files.Values)
						f.transmitRate = 0.0;

				// do nothing else
				return;
			}
			
			double totalTransmitCapacity = vd.Connection.rate * elapsed_s;
			double remainingTransmitCapacity = totalTransmitCapacity;
			double scienceCredited = 0.0;

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

				// fire subject completed events
				int timesCompleted = xmitFile.file.subjectData.UpdateSubjectCompletion(xmitScienceValue);
				if (timesCompleted > 0)
					SubjectXmitCompleted(xmitFile.file, timesCompleted, v);

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

				// clamp science value to subject max value
				xmitScienceValue = Math.Min(xmitScienceValue, xmitFile.file.subjectData.ScienceRemainingToRetrieve);

				if (xmitScienceValue > 0.0)
				{
					// add credits
					scienceCredited += xmitScienceValue;

					// credit the subject
					xmitFile.file.subjectData.AddScienceToRnDSubject(xmitScienceValue);
				}
			}

			UnityEngine.Profiling.Profiler.EndSample();

			vd.scienceTransmitted += scienceCredited;

			// consume EC cost for transmission (ec_idle is consumed above)
			double transmittedCapacity = totalTransmitCapacity - remainingTransmitCapacity;
			double transmissionCost = (vd.Connection.ec - vd.Connection.ec_idle) * (transmittedCapacity / vd.Connection.rate);
			ec.Consume(transmissionCost, "comms (xmit)");

			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.Science.Update-AddScience");

			// Add science points, but wait until we have at least 0.1 points to add because AddScience is VERY slow
			// We don't use "TransactionReasons.ScienceTransmission" because AddScience fire multiple events not meant to be fired continuously
			// this avoid many side issues (ex : chatterer transmit sound playing continously, strategia "+0.0 science" popup...)
			ScienceDB.uncreditedScience += scienceCredited;
			if (ScienceDB.uncreditedScience > 0.1)
			{
				if (GameHasRnD)
					ResearchAndDevelopment.Instance.AddScience((float)ScienceDB.uncreditedScience, TransactionReasons.None);
				
				ScienceDB.uncreditedScience = 0.0;
			}

			UnityEngine.Profiling.Profiler.EndSample();
		}

		private static void GetFilesToTransmit(Vessel v, VesselData vd)
		{
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.Science.GetFilesToTransmit");
			Drive warpCache = Cache.WarpCache(v);

			xmitFiles.Clear();
			List<SubjectData> filesToRemove = new List<SubjectData>();

			foreach (Drive drive in Drive.GetDrives(vd, true))
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



		private static void SubjectXmitCompleted(File file, int timesCompleted, Vessel v)
		{
			if (!GameHasRnD)
				return;

			// fire science transmission game event. This is used by stock contracts and a few other things.
			GameEvents.OnScienceRecieved.Fire(timesCompleted == 1 ? (float)file.subjectData.ScienceMaxValue : 0f, file.subjectData.RnDSubject, v.protoVessel, false);

			// fire our API event
			// Note (GOT) : disabled, nobody is using it and i'm not sure what is the added value compared to the stock event,
			// unless we fire it for every transmission tick, and in this case this is a very bad idea from a performance standpoint
			// API.OnScienceReceived.Notify(credits, subject, pv, true);

			// notify the player
			string subjectResultText;
			if (string.IsNullOrEmpty(file.resultText))
			{
				subjectResultText = Lib.TextVariant(
					"Our researchers will jump on it right now",//
					"This cause some excitement",//
					"These results are causing a brouhaha in R&D",//
					"Our scientists look very confused",//
					"The scientists won't believe these readings");//
			}
			else
			{
				subjectResultText = file.resultText;
			}
			subjectResultText = Lib.WordWrapAtLength(subjectResultText, 70);
			Message.Post(Lib.BuildString(
				file.subjectData.FullTitle,
				" transmitted\n",
				timesCompleted == 1 ? Lib.HumanReadableScience(file.subjectData.ScienceMaxValue, false) : Lib.Color("no science gain : we already had this data", Lib.Kolor.Orange, true)),//
				subjectResultText);
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
					  "Our researchers will jump on it right now",//
					  "This cause some excitement",//
					  "These results are causing a brouhaha in R&D",//
					  "Our scientists look very confused",//
					  "The scientists won't believe these readings");//
			}
			return result;
		}
	}

} // KERBALISM

