using System;
using KSP.Localization;

namespace KERBALISM
{
	public class KerbalismScansat : PartModule
	{
		[KSPField] public string experimentType = string.Empty;
		[KSPField] public double ec_rate = 0.0;

		[KSPField(isPersistant = true)] public int sensorType = 0;

		// Persisted for save compatibility / UI; science watermark lives in ScanCoverageStore.
		[KSPField(isPersistant = true)] private string body_name = string.Empty;
		[KSPField(isPersistant = true)] private double body_coverage = 0.0;
		[KSPField(isPersistant = true)] private double warp_buffer = 0.0; // retained so old saves load cleanly
		[KSPField(isPersistant = true)] private bool power_disabled = false;

		private PartModule scanner;
		private ExperimentInfo expInfo;
		private bool storageWarningPosted;
		private int storageUnavailableTicks;

		public int SensorType => sensorType;
		public ExperimentInfo ExpInfo => expInfo;
		public bool IsScanning { get; private set; }
		public string Issue { get; private set; } = string.Empty;
		public double BodyCoveragePercent { get; private set; }
		public SubjectData CurrentSubject { get; private set; }

		public override void OnStart(StartState state)
		{
			if (Lib.DisableScenario(this)) return;
			if (Lib.IsEditor()) return;

			expInfo = ScienceDB.GetExperimentInfo(experimentType);
			scanner = SCANsat.FindScanner(part, experimentType, sensorType);
			if (scanner != null)
			{
				int scienceSensorType = SCANsat.ScienceSensorType(experimentType);
				sensorType = scienceSensorType != 0 ? scienceSensorType : SCANsat.SensorType(scanner);
				IsScanning = SCANsat.IsScanning(scanner);
				if (IsScanning)
					power_disabled = false;
				else if (SCANsat.HasPowerProblem(scanner))
					power_disabled = true;
			}
		}

		public void FixedUpdate()
		{
			if (!Features.Science || vessel == null || !vessel.loaded)
				return;

			if (scanner == null)
				scanner = SCANsat.FindScanner(part, experimentType, sensorType);
			if (scanner == null)
				return;

			VesselData vd = vessel.KerbalismData();
			ResourceInfo ec = ResourceCache.GetResource(vessel, "ElectricCharge");
			// Consume the old part-level marker once. New saves use the per-module field.
			bool legacyAutoDisabled = vd.scansat_id.Remove(part.flightID);
			IsScanning = SCANsat.IsScanning(scanner);
			if (!IsScanning && legacyAutoDisabled)
			{
				// Main-branch saves used this vessel-level list for automatic SCANsat stops.
				power_disabled = true;
			}

			if (IsScanning)
			{
				if (ec_rate > double.Epsilon)
				{
					double ecNeed = ec_rate * TimeWarp.fixedDeltaTime;
					double ecAvailable = Math.Max(0.0, ec.Amount + ec.Deferred);
					if (ecAvailable + double.Epsilon < ecNeed * 0.9)
					{
						if (ecAvailable > double.Epsilon)
							ec.Consume(Math.Min(ecNeed, ecAvailable), ResourceBroker.Scanner);
						SCANsat.StopScan(scanner);
						IsScanning = SCANsat.IsScanning(scanner);
						power_disabled = true;
						if (vd.cfg_ec)
							Message.Post(Local.Scansat_sensordisabled.Format("<b>" + vessel.vesselName + "</b>"));
					}
					else
					{
						ec.Consume(ecNeed, ResourceBroker.Scanner);
						power_disabled = false;
					}
				}
				else
				{
					power_disabled = false;
				}
			}
			else if (power_disabled && (ec_rate <= double.Epsilon || ec.Level >= 0.25))
			{
				SCANsat.StartScan(scanner);
				IsScanning = SCANsat.IsScanning(scanner);
				if (IsScanning)
				{
					power_disabled = false;
					if (vd.cfg_ec)
						Message.Post(Local.Scansat_sensorresumed.Format("<b>" + vessel.vesselName + "</b>"));
				}
			}

			RunningUpdate(vessel, vd, experimentType, sensorType, IsScanning, this);
		}

		public static void BackgroundUpdate(Vessel vessel, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, KerbalismScansat prefab,
			Part part_prefab, VesselData vd, ResourceInfo ec, double elapsed_s)
		{
			if (vessel == null || p == null || m == null || prefab == null || vd == null || elapsed_s <= 0.0)
				return;

			int sensorType = (int)Lib.Proto.GetUInt(m, "sensorType");
			ProtoPartModuleSnapshot scanner = SCANsat.FindScanner(p, prefab.experimentType, sensorType);
			if (scanner == null)
				return;

			int scienceSensorType = SCANsat.ScienceSensorType(prefab.experimentType);
			int coverageSensorType = scienceSensorType != 0
				? scienceSensorType
				: (int)Lib.Proto.GetUInt(scanner, "sensorType");
			if (coverageSensorType != 0 && sensorType != coverageSensorType)
			{
				sensorType = coverageSensorType;
				Lib.Proto.Set(m, "sensorType", (uint)sensorType);
			}

			bool isScanning = Lib.Proto.GetBool(scanner, "scanning");
			bool legacyAutoDisabled = vd.scansat_id.Remove(p.flightID);
			bool powerDisabled = Lib.Proto.GetBool(m, "power_disabled")
				|| (!isScanning && legacyAutoDisabled);
			bool insufficientPower = false;

			if (isScanning)
			{
				if (prefab.ec_rate > double.Epsilon)
				{
					double ecNeed = prefab.ec_rate * elapsed_s;
					double ecAvailable = Math.Max(0.0, ec.Amount + ec.Deferred);
					if (ecAvailable + double.Epsilon < ecNeed * 0.9)
					{
						if (ecAvailable > double.Epsilon)
							ec.Consume(Math.Min(ecNeed, ecAvailable), ResourceBroker.Scanner);
						SCANsat.StopScanner(vessel, scanner, part_prefab);
						isScanning = false;
						powerDisabled = true;
						insufficientPower = true;
						if (vd.cfg_ec)
							Message.Post(Local.Scansat_sensordisabled.Format("<b>" + vessel.vesselName + "</b>"));
					}
					else
					{
						ec.Consume(ecNeed, ResourceBroker.Scanner);
					}
				}

				if (isScanning)
				{
					powerDisabled = false;
				}
			}
			else if (powerDisabled && (prefab.ec_rate <= double.Epsilon || ec.Level >= 0.25))
			{
				if (SCANsat.ResumeScanner(vessel, scanner, part_prefab))
				{
					isScanning = true;
					powerDisabled = false;
					if (vd.cfg_ec)
						Message.Post(Local.Scansat_sensorresumed.Format("<b>" + vessel.vesselName + "</b>"));
				}
			}

			Lib.Proto.Set(m, "power_disabled", powerDisabled);

			if (!Features.Science || sensorType == 0)
				return;

			ExperimentInfo info = ScienceDB.GetExperimentInfo(prefab.experimentType);
			if (info != null)
			{
				double legacyWarpBuffer = Lib.Proto.GetDouble(m, "warp_buffer");
				if (legacyWarpBuffer > double.Epsilon && vessel.mainBody != null)
				{
					ScanCoverageStore.AddPendingSize(
						LegacyBodyIndex(Lib.Proto.GetString(m, "body_name"), vessel.mainBody),
						prefab.experimentType,
						sensorType,
						legacyWarpBuffer);
					Lib.Proto.Set(m, "warp_buffer", 0.0);
				}
			}

			if (insufficientPower)
				return;

			RunningUpdate(vessel, vd, prefab.experimentType, sensorType, isScanning, null);
		}

		private static void RunningUpdate(Vessel vessel, VesselData vd, string experimentType,
			int sensorType, bool isScanning, KerbalismScansat loaded)
		{
			if (vessel == null || vd == null || sensorType == 0)
				return;

			ExperimentInfo expInfo = loaded != null ? loaded.expInfo : ScienceDB.GetExperimentInfo(experimentType);
			if (expInfo == null)
			{
				if (loaded != null)
				{
					loaded.Issue = Local.ExperimentInfo_Unknown;
					loaded.CurrentSubject = null;
					loaded.BodyCoveragePercent = 0.0;
				}
				return;
			}

			CelestialBody body = vessel.mainBody;
			if (body == null)
				return;

			int bodyIndex = body.flightGlobalsIndex;
			Situation situation = new Situation(bodyIndex, ScienceSituation.InSpaceHigh);
			SubjectData subject = ScienceDB.GetSubjectData(expInfo, situation);
			double currentCoverage = SCANsat.Coverage(sensorType, body);

			if (loaded != null && loaded.warp_buffer > double.Epsilon)
			{
				ScanCoverageStore.AddPendingSize(
					LegacyBodyIndex(loaded.body_name, body),
					experimentType,
					sensorType,
					loaded.warp_buffer);
				loaded.warp_buffer = 0.0;
			}

			if (loaded != null)
			{
				loaded.expInfo = expInfo;
				loaded.CurrentSubject = subject;
				loaded.BodyCoveragePercent = currentCoverage;
				loaded.body_name = body.name;
				loaded.body_coverage = currentCoverage;
				loaded.Issue = !isScanning && loaded.power_disabled
					? Local.Module_Experiment_issue4
					: string.Empty;
			}

			if (!isScanning)
				return;

			ScanCoverageStore.ObserveCoverage(bodyIndex, experimentType, sensorType, currentCoverage, expInfo.DataSize);

			if (subject == null)
				return;

			double sizeBudget = ScanCoverageStore.PendingSize(bodyIndex, experimentType, sensorType);
			if (sizeBudget <= double.Epsilon)
				return;

			double left = Drive.StoreFile(vessel, subject, sizeBudget);
			double stored = sizeBudget - left;
			if (stored > double.Epsilon)
				ScanCoverageStore.CommitStoredSize(bodyIndex, experimentType, sensorType, stored);

			if (loaded == null)
				return;

			if (left > double.Epsilon)
			{
				loaded.Issue = Local.Module_Experiment_issue11;
				loaded.storageUnavailableTicks++;
				if (loaded.storageUnavailableTicks >= 5 && !loaded.storageWarningPosted)
				{
					loaded.storageWarningPosted = true;
					Message.Post(
						Lib.Color(Local.Module_Experiment_issue_title, Lib.Kolor.Orange, true),
						Lib.BuildString("<b>", vessel.vesselName, "</b>: ", Local.Module_Experiment_issue11));
				}
			}
			else
			{
				loaded.storageUnavailableTicks = 0;
				loaded.storageWarningPosted = false;
			}
		}

		private static int LegacyBodyIndex(string bodyName, CelestialBody fallback)
		{
			if (!string.IsNullOrEmpty(bodyName) && FlightGlobals.Bodies != null)
			{
				foreach (CelestialBody body in FlightGlobals.Bodies)
				{
					if (body != null && body.name == bodyName)
						return body.flightGlobalsIndex;
				}
			}
			return fallback != null ? fallback.flightGlobalsIndex : -1;
		}

		internal void StopScan()
		{
			if (scanner == null) return;
			power_disabled = false;
			if (vessel != null)
				vessel.KerbalismData().scansat_id.Remove(part.flightID);
			SCANsat.StopScan(scanner);
			IsScanning = SCANsat.IsScanning(scanner);
		}

		internal void StartScan()
		{
			if (scanner == null) return;
			power_disabled = false;
			if (vessel != null)
				vessel.KerbalismData().scansat_id.Remove(part.flightID);
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
				"\n", Local.Module_Experiment_Specifics_info9, ": ",
				Lib.HumanOrSIRate(ec_rate, Lib.ECResID));
		}
	}
}
