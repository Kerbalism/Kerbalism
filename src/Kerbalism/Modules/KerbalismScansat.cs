using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;

namespace KERBALISM
{
	public class KerbalismScansat : PartModule
	{
		[KSPField] public string experimentType = string.Empty;
		[KSPField] public double ec_rate = 0.0;

		[KSPField(isPersistant = true)] public int sensorType = 0;

		// Legacy fields kept for save compatibility (pre-rewrite coverage-delta path / fail-open).
		[KSPField(isPersistant = true)] private string body_name = string.Empty;
		[KSPField(isPersistant = true)] private double body_coverage = 0.0;
		[KSPField(isPersistant = true)] private double warp_buffer = 0.0; // retained so old saves load cleanly

		private PartModule scanner;
		private ExperimentInfo expInfo;
		private bool storageWarningPosted;
		private int storageUnavailableTicks;

		public int SensorType => sensorType;
		public ExperimentInfo ExpInfo => expInfo;
		public bool IsScanning { get; private set; }
		public string Issue { get; private set; } = string.Empty;
		public double PendingCoveragePercent { get; private set; }
		public SubjectData CurrentSubject { get; private set; }

		public override void OnStart(StartState state)
		{
			if (Lib.DisableScenario(this)) return;
			if (Lib.IsEditor()) return;

			foreach (PartModule module in part.Modules)
			{
				if (module.moduleName == "SCANsat" || module.moduleName == "ModuleSCANresourceScanner")
				{
					scanner = module;
					break;
				}
			}

			if (scanner != null)
				sensorType = Lib.ReflectionValue<int>(scanner, "sensorType");

			expInfo = ScienceDB.GetExperimentInfo(experimentType);
			ScanCoverageStore.InvalidateDivertMask(vessel.id);
		}

		public void OnDestroy()
		{
			if (vessel != null)
			{
				ScanCoverageStore.RemoveCaptureState(vessel.id, part.flightID);
				ScanCoverageStore.InvalidateDivertMask(vessel.id);
			}
		}

		public void FixedUpdate()
		{
			if (!Features.Science || vessel == null || !vessel.loaded)
				return;

			if (scanner != null)
				IsScanning = SCANsat.IsScanning(scanner);

			VesselData vd = vessel.KerbalismData();
			ResourceInfo ec = ResourceCache.GetResource(vessel, "ElectricCharge");
			RunningUpdate(vessel, vd, ec, TimeWarp.fixedDeltaTime);
		}

		public static void BackgroundUpdate(Vessel vessel, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, KerbalismScansat prefab,
			Part part_prefab, VesselData vd, ResourceInfo ec, double elapsed_s)
		{
			if (!Features.Science)
			{
				BackgroundNoScience(vessel, p, prefab, ec, elapsed_s);
				return;
			}

			List<ProtoPartModuleSnapshot> scanners = Cache.VesselObjectsCache<List<ProtoPartModuleSnapshot>>(vessel, "scansat_" + p.flightID);
			if (scanners == null)
			{
				scanners = Lib.FindModules(p, "SCANsat");
				if (scanners.Count == 0)
					scanners = Lib.FindModules(p, "ModuleSCANresourceScanner");
				Cache.SetVesselObjectsCache(vessel, "scansat_" + p.flightID, scanners);
			}

			bool isScanning = false;
			if (scanners.Count > 0)
				isScanning = Lib.Proto.GetBool(scanners[0], "scanning");

			// Ensure divert mask uses persisted sensorType.
			if (prefab.sensorType == 0)
			{
				int persisted = (int)Lib.Proto.GetUInt(m, "sensorType");
				if (persisted != 0)
					Lib.Proto.Set(m, "sensorType", (uint)persisted);
			}

			int sensorType = (int)Lib.Proto.GetUInt(m, "sensorType");
			if (sensorType == 0 && scanners.Count > 0)
			{
				sensorType = (int)Lib.Proto.GetUInt(scanners[0], "sensorType");
				if (sensorType != 0)
					Lib.Proto.Set(m, "sensorType", (uint)sensorType);
			}

			if (!SCANsatHarmony.InterceptEnabled)
			{
				// Background fail-open: consume EC only; coverage science needs loaded path / intercept.
				ScanCoverageStore.RemoveCaptureState(vessel.id, p.flightID);
				if (isScanning && prefab.ec_rate > double.Epsilon)
					ec.Consume(prefab.ec_rate * elapsed_s, ResourceBroker.Scanner);
				return;
			}

			RunningUpdateStatic(vessel, vd, ec, elapsed_s, prefab.experimentType, prefab.ec_rate,
				sensorType, isScanning, p.flightID, null);
		}

		private void RunningUpdate(Vessel vessel, VesselData vd, ResourceInfo ec, double elapsed_s)
		{
			if (!SCANsatHarmony.InterceptEnabled)
			{
				ScanCoverageStore.RemoveCaptureState(vessel.id, part.flightID);
				FallbackCoverageUpdate(vessel, vd);
				return;
			}

			RunningUpdateStatic(vessel, vd, ec, elapsed_s, experimentType, ec_rate, sensorType, IsScanning, part.flightID, this);
		}

		/// <summary>
		/// Fail-open path when Harmony coverage intercept is unavailable:
		/// keep recording science from public coverage %, without deferring the map.
		/// </summary>
		private void FallbackCoverageUpdate(Vessel vessel, VesselData vd)
		{
			if (scanner == null || !IsScanning || expInfo == null)
				return;

			double newCoverage = SCANsat.Coverage(sensorType, vessel.mainBody);
			if (body_name == vessel.mainBody.name && newCoverage < body_coverage)
				newCoverage = body_coverage;

			if (vessel.mainBody.name != body_name)
			{
				body_name = vessel.mainBody.name;
				body_coverage = newCoverage;
				return;
			}

			double coverageDelta = newCoverage - body_coverage;
			body_coverage = newCoverage;
			if (coverageDelta <= double.Epsilon)
				return;

			Situation situation = new Situation(vessel.mainBody.flightGlobalsIndex, ScienceSituation.InSpaceHigh);
			SubjectData subject = ScienceDB.GetSubjectData(expInfo, situation);
			if (subject == null)
				return;

			CurrentSubject = subject;
			// SCANsat's native FixedUpdate already paid EC in fail-open mode.
			double size = expInfo.DataSize * coverageDelta / 100.0 + warp_buffer;
			double left = Drive.StoreFile(vessel, subject, size);
			warp_buffer = left > double.Epsilon ? left : 0.0;
			Issue = left > double.Epsilon ? Local.Module_Experiment_issue11 : string.Empty;
			PendingCoveragePercent = 0.0;
		}

		private static void RunningUpdateStatic(Vessel vessel, VesselData vd, ResourceInfo ec, double elapsed_s,
			string experimentType, double ecRate, int sensorType, bool isScanning, uint partId, KerbalismScansat loaded)
		{
			if (vessel == null || vd == null || elapsed_s <= 0.0)
				return;

			if (sensorType == 0)
			{
				ScanCoverageStore.RemoveCaptureState(vessel.id, partId);
				return;
			}

			ExperimentInfo expInfo = loaded != null ? loaded.expInfo : ScienceDB.GetExperimentInfo(experimentType);
			if (expInfo == null)
			{
				ScanCoverageStore.RemoveCaptureState(vessel.id, partId);
				if (loaded != null)
				{
					loaded.Issue = "unknown experiment";
					loaded.CurrentSubject = null;
					loaded.PendingCoveragePercent = 0.0;
				}
				return;
			}

			Situation situation = new Situation(vessel.mainBody.flightGlobalsIndex, ScienceSituation.InSpaceHigh);
			SubjectData subject = ScienceDB.GetSubjectData(expInfo, situation);
			short mask = (short)sensorType;
			int bodyIndex = vessel.mainBody.flightGlobalsIndex;

			// Repair saves that ballooned file size by recounting the same diverted cells.
			if (subject != null)
				ScanCoverageStore.ReconcileScanFileSize(vd, subject, mask);
			ScanCoverageStore.StripStoredFromPending(vd, vessel.id, bodyIndex, mask);

			Int16[,] pending = ScanCoverageStore.GetPending(vessel.id, bodyIndex, false);
			double pendingPercent = ScanGrid.CoveragePercent(pending, mask);
			Int16[,] claimed = ScanCoverageStore.GetClaimedCoverage(vessel, bodyIndex);
			double claimedPercent = ScanGrid.CoveragePercent(claimed, mask);
			bool coverageComplete = claimedPercent >= 100.0 - 1e-6;

			if (loaded != null)
			{
				loaded.expInfo = expInfo;
				loaded.CurrentSubject = subject;
				loaded.PendingCoveragePercent = pendingPercent;
				loaded.Issue = string.Empty;
			}

			// Pay scanner EC while it is active. The resulting factor is attached to newly
			// intercepted map cells, so a pending backlog has already paid its scanning cost.
			double captureFactor = isScanning ? 1.0 : 0.0;
			if (coverageComplete)
				captureFactor = 0.0;
			else if (isScanning && ecRate > double.Epsilon)
			{
				double ecNeed = ecRate * elapsed_s;
				double ecAvailable = Math.Max(0.0, ec.Amount + ec.Deferred);
				captureFactor = ecNeed <= double.Epsilon ? 0.0 : Math.Min(1.0, ecAvailable / ecNeed);
				if (captureFactor > double.Epsilon)
					ec.Consume(ecNeed * captureFactor, ResourceBroker.Scanner);
			}

			double persistentFree = 0.0;
			foreach (Drive drive in Drive.GetDrives(vd))
			{
				double available = drive.FileCapacityAvailable();
				if (available == double.MaxValue)
				{
					persistentFree = double.MaxValue;
					break;
				}
				persistentFree += available;
			}

			if (persistentFree <= double.Epsilon)
				captureFactor = 0.0;
			else if (loaded != null)
				loaded.storageUnavailableTicks = 0;

			ScanCoverageStore.SetCaptureState(vessel.id, partId, mask, captureFactor);

			if (isScanning && captureFactor <= double.Epsilon && !coverageComplete)
			{
				if (loaded != null)
					loaded.Issue = persistentFree <= double.Epsilon
						? Local.Module_Experiment_issue11
						: Local.Module_Experiment_issue4;
			}

			if (ScanGrid.IsEmpty(pending) || subject == null)
				return;

			double fullSize = expInfo.DataSize * ScanGrid.AreaWeight(pending, mask)
				/ (ScanGrid.FullSphereWeight * Math.Max(1, ScanGrid.CountBits(mask)));
			double sizeBudget = Math.Min(fullSize, persistentFree);

			if (sizeBudget <= double.Epsilon)
			{
				if (loaded != null)
				{
					loaded.Issue = Local.Module_Experiment_issue11; // no storage space
					loaded.storageUnavailableTicks++;
					// PartModule OnStart ordering can briefly expose an empty PartData drive list.
					// Require several consecutive physics ticks before notifying the player.
					if (loaded.storageUnavailableTicks >= 5 && !loaded.storageWarningPosted)
					{
						loaded.storageWarningPosted = true;
						Message.Post(
							Lib.Color(Local.Module_Experiment_issue_title, Lib.Kolor.Orange, true),
							Lib.BuildString("<b>", vessel.vesselName, "</b>: ", Local.Module_Experiment_issue11));
					}
				}
				return;
			}

			if (loaded != null)
				loaded.storageWarningPosted = false;

			Int16[,] taken = ScanGrid.TakeBudget(pending, mask, expInfo.DataSize, sizeBudget, out double takenSize);
			if (ScanGrid.IsEmpty(taken) || takenSize <= double.Epsilon)
				return;

			Int16[,] unstored = Drive.StoreScanFile(vd, subject, takenSize, taken, bodyIndex, mask);
			if (!ScanGrid.IsEmpty(unstored))
			{
				// Put only the cells that no persisted drive accepted back into pending.
				ScanGrid.OrMasked(pending, unstored, mask);
				if (loaded != null)
					loaded.Issue = Local.Module_Experiment_issue11;
			}
			else if (loaded != null)
			{
				loaded.PendingCoveragePercent = ScanGrid.CoveragePercent(pending, mask);
			}
		}

		internal static List<File> TakePendingRecoveryFiles(ProtoVessel protoVessel)
		{
			var files = new List<File>();
			if (protoVessel == null)
				return files;

			List<int> bodyIndices = ScanCoverageStore.GetPendingBodyIndices(protoVessel.vesselID);
			if (bodyIndices.Count == 0)
				return files;

			var handledExperiments = new HashSet<string>();
			foreach (ProtoPartSnapshot part in protoVessel.protoPartSnapshots)
			{
				ProtoPartModuleSnapshot sidecar = null;
				ProtoPartModuleSnapshot scanner = null;
				foreach (ProtoPartModuleSnapshot module in part.modules)
				{
					if (module.moduleName == "KerbalismScansat")
						sidecar = module;
					else if (module.moduleName == "SCANsat" || module.moduleName == "ModuleSCANresourceScanner")
						scanner = module;
				}

				if (sidecar == null || scanner == null)
					continue;

				KerbalismScansat prefab = part.partPrefab.FindModuleImplementing<KerbalismScansat>();
				if (prefab == null)
					continue;

				int sensorType = (int)Lib.Proto.GetUInt(sidecar, "sensorType");
				if (sensorType == 0)
					sensorType = (int)Lib.Proto.GetUInt(scanner, "sensorType");
				if (sensorType == 0)
					continue;

				ExperimentInfo info = ScienceDB.GetExperimentInfo(prefab.experimentType);
				if (info == null)
					continue;

				short mask = (short)sensorType;
				foreach (int bodyIndex in bodyIndices)
				{
					string key = bodyIndex + "|" + info.ExperimentId + "|" + sensorType;
					if (!handledExperiments.Add(key))
						continue;

					SubjectData subject = ScienceDB.GetSubjectData(
						info,
						new Situation(bodyIndex, ScienceSituation.InSpaceHigh));
					if (subject == null)
						continue;

					Int16[,] payload = ScanCoverageStore.TakePending(protoVessel.vesselID, bodyIndex, mask);
					if (ScanGrid.IsEmpty(payload))
						continue;

					double size = info.DataSize * ScanGrid.AreaWeight(payload, mask)
						/ (ScanGrid.FullSphereWeight * Math.Max(1, ScanGrid.CountBits(mask)));
					if (size <= double.Epsilon)
						continue;

					var file = new File(subject, size);
					file.MergeScanCoverage(payload, bodyIndex);
					subject.AddDataCollectedInFlight(size);
					files.Add(file);
				}
			}

			// Preserve any cells whose source module can no longer be identified.
			ScanCoverageStore.QueueRemainingRecoveredPending(protoVessel.vesselID);
			return files;
		}

		private static void BackgroundNoScience(Vessel vessel, ProtoPartSnapshot p, KerbalismScansat prefab, ResourceInfo ec, double elapsed_s)
		{
			// Without FeatureScience, leave SCANsat map handling alone; only simulate background EC.
			List<ProtoPartModuleSnapshot> scanners = Lib.FindModules(p, "SCANsat");
			if (scanners.Count == 0)
				scanners = Lib.FindModules(p, "ModuleSCANresourceScanner");
			if (scanners.Count == 0)
				return;

			bool isScanning = Lib.Proto.GetBool(scanners[0], "scanning");
			if (isScanning && prefab.ec_rate > double.Epsilon)
				ec.Consume(prefab.ec_rate * elapsed_s, ResourceBroker.Scanner);
		}

		internal void StopScan()
		{
			if (scanner == null) return;
			SCANsat.StopScan(scanner);
			IsScanning = SCANsat.IsScanning(scanner);
		}

		internal void StartScan()
		{
			if (scanner == null) return;
			SCANsat.StartScan(scanner);
			IsScanning = SCANsat.IsScanning(scanner);
		}

		public override string GetInfo()
		{
			ExperimentInfo info = ScienceDB.GetExperimentInfo(experimentType);
			string title = info != null ? info.Title : experimentType;
			return Lib.BuildString(
				Lib.Color(title, Lib.Kolor.Cyan, true),
				"\n", Local.Experimentinfo_Datasize, ": ",
				info != null ? Lib.HumanReadableDataSize(info.DataSize) : "?",
				"\nEC: ",
				Lib.HumanOrSIRate(ec_rate, Lib.ECResID));
		}
	}
}
