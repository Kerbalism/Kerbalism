using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{
	// Note (Got) : this whole thing :
	// - should not be using it's own warp buffer, but use the transmit buffer drive instead
	// - should not disable itself when there is no ec, but stay enabled and scale output by ec.AvailabilityFactor
	// The current implementation will cause EC resource sim destabilization by putting it into a produce/not produce cycle
	// it works by stopping/starting the scansat module dependning on ec availability
	// Instead we should keep it enabled but reflection-scale the "covered %" by ec.AvailabilityFactor (and drive / warpdrive space)
	// Not sure how feasible this is, but I fear the changes to the resource sim will cause this to work even more unreliably than before.
	// I don't have time now to rewrite and test it, but that probably need to be done.

	public class KerbalismScansat : PartModule
	{
		[KSPField] public string experimentType = string.Empty;
		[KSPField] public double ec_rate = 0.0;

		[KSPField(isPersistant = true)] private int sensorType = 0;
		[KSPField(isPersistant = true)] private string body_name = string.Empty;
		[KSPField(isPersistant = true)] private double body_coverage = 0.0;
		[KSPField(isPersistant = true)] private double warp_buffer = 0.0;


		private PartModule scanner = null;
		ExperimentInfo expInfo;
		public bool IsScanning { get; internal set; }

		public override void OnStart(StartState state)
		{
			if (Lib.DisableScenario(this)) return;
			if (Lib.IsEditor()) return;

			foreach(var module in part.Modules)
			{
				if(module.moduleName == "SCANsat" || module.moduleName == "ModuleSCANresourceScanner")
				{
					scanner = module;
					break;
				}
			}

			if (scanner == null) return;
			sensorType = Lib.ReflectionValue<int>(scanner, "sensorType");
			expInfo = ScienceDB.GetExperimentInfo(experimentType);
		}

		public void FixedUpdate()
		{
			if (scanner == null) return;
			if (!Features.Science) return;

			IsScanning = SCANsat.IsScanning(scanner);
			double new_coverage = SCANsat.Coverage(sensorType, vessel.mainBody);

			if(body_name == vessel.mainBody.name && new_coverage < body_coverage)
			{
				// SCANsat sometimes reports a coverage of 0, which is wrong
				new_coverage = body_coverage;
			}

			if (vessel.mainBody.name != body_name)
			{
				body_name = vessel.mainBody.name;
				body_coverage = new_coverage;
			}
			else
			{
				double coverage_delta = new_coverage - body_coverage;
				body_coverage = new_coverage;
				VesselData vd = vessel.KerbalismData();

				if (IsScanning)
				{
					Situation scanSatSituation = new Situation(vessel.mainBody.flightGlobalsIndex, ScienceSituation.InSpaceHigh);
					SubjectData subject = ScienceDB.GetSubjectData(expInfo, scanSatSituation);
					if (subject == null)
						return;

					double size = expInfo.DataSize * coverage_delta / 100.0; // coverage is 0-100%
					size += warp_buffer;
					size = Drive.StoreFile(vessel, subject, size);
					if (size > double.Epsilon)
					{
						// we filled all drives up to the brim but were unable to store everything
						if (warp_buffer < double.Epsilon)
						{
							// warp buffer is empty, so lets store the rest there
							warp_buffer = size;
							size = 0;
						}
						else
						{
							// warp buffer not empty. that's ok if we didn't get new data
							if (coverage_delta < double.Epsilon)
							{
								size = 0;
							}
							// else we're scanning too fast. stop.
						}

						// cancel scanning and annoy the user
						if (size > double.Epsilon)
						{
							warp_buffer = 0;
							StopScan();
							vd.scansat_id.Add(part.flightID);
							Message.Post(Lib.Color(Local.Scansat_Scannerhalted, Lib.Kolor.Red, true), Local.Scansat_Scannerhalted_text.Format("<b>" + vessel.vesselName + "</b>"));//"Scanner halted""Scanner halted on <<1>>. No storage left on vessel."
						}
					}
				}
				else if(vd.scansat_id.Contains(part.flightID))
				{
					
					if (vd.DrivesFreeSpace / vd.DrivesCapacity > 0.9) // restart when 90% of capacity is available 
					{
						StartScan();
						vd.scansat_id.Remove(part.flightID);
						if (vd.cfg_ec) Message.Post(Local.Scansat_sensorresumed.Format("<b>" + vessel.vesselName + "</b>"));//Lib.BuildString("SCANsat sensor resumed operations on <<1>>)
					}
				}
			}
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

		public static void BackgroundUpdate(Vessel vessel, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, KerbalismScansat kerbalismScansat,
		                                    Part part_prefab, VesselData vd, IResource ec, double elapsed_s)
		{
			List<ProtoPartModuleSnapshot> scanners = Cache.VesselObjectsCache<List<ProtoPartModuleSnapshot>>(vessel, "scansat_" + p.flightID);
			if(scanners == null)
			{
				scanners = Lib.FindModules(p, "SCANsat");
				if (scanners.Count == 0) scanners = Lib.FindModules(p, "ModuleSCANresourceScanner");
				Cache.SetVesselObjectsCache(vessel, "scansat_" + p.flightID, scanners);
			}

			if (scanners.Count == 0) return;
			var scanner = scanners[0];

			bool is_scanning = Lib.Proto.GetBool(scanner, "scanning");
			if(is_scanning && kerbalismScansat.ec_rate > double.Epsilon)
				ec.Consume(kerbalismScansat.ec_rate * elapsed_s, ResourceBroker.Scanner);

			if (!Features.Science)
			{
				if(is_scanning && ec.Amount < double.Epsilon)
				{					
					SCANsat.StopScanner(vessel, scanner, part_prefab);
					is_scanning = false;

					// remember disabled scanner
					vd.scansat_id.Add(p.flightID);

					// give the user some feedback
					if (vd.cfg_ec) Message.Post(Local.Scansat_sensordisabled.Format("<b>"+vessel.vesselName+"</b>"));//Lib.BuildString("SCANsat sensor was disabled on <<1>>)
				}
				else if (vd.scansat_id.Contains(p.flightID))
				{
					// if there is enough ec
					// note: comparing against amount in previous simulation step
					// re-enable at 25% EC
					if (ec.Level > 0.25)
					{
						// re-enable the scanner
						SCANsat.ResumeScanner(vessel, m, part_prefab);
						is_scanning = true;

						// give the user some feedback
						if (vd.cfg_ec) Message.Post(Local.Scansat_sensorresumed.Format("<b>"+vessel.vesselName+"</b>"));//Lib.BuildString("SCANsat sensor resumed operations on <<1>>)
					}
				}

				// forget active scanners
				if (is_scanning) vd.scansat_id.Remove(p.flightID);

				return;
			} // if(!Feature.Science)

			string body_name = Lib.Proto.GetString(m, "body_name");
			int sensorType = (int)Lib.Proto.GetUInt(m, "sensorType");
			double body_coverage = Lib.Proto.GetDouble(m, "body_coverage");
			double warp_buffer = Lib.Proto.GetDouble(m, "warp_buffer");

			double new_coverage = SCANsat.Coverage(sensorType, vessel.mainBody);

			if (body_name == vessel.mainBody.name && new_coverage < body_coverage)
			{
				// SCANsat sometimes reports a coverage of 0, which is wrong
				new_coverage = body_coverage;
			}

			if (vessel.mainBody.name != body_name)
			{
				body_name = vessel.mainBody.name;
				body_coverage = new_coverage;
			}
			else
			{
				double coverage_delta = new_coverage - body_coverage;
				body_coverage = new_coverage;

				if (is_scanning)
				{
					ExperimentInfo expInfo = ScienceDB.GetExperimentInfo(kerbalismScansat.experimentType);
					SubjectData subject = ScienceDB.GetSubjectData(expInfo, vd.VesselSituations.GetExperimentSituation(expInfo));
					if (subject == null)
						return;

					double size = expInfo.DataSize * coverage_delta / 100.0; // coverage is 0-100%
					size += warp_buffer;

					if (size > double.Epsilon)
					{
						// store what we can
						foreach (var d in Drive.GetDrives(vd))
						{
							var available = d.FileCapacityAvailable();
							var chunk = Math.Min(size, available);
							if (!d.Record_file(subject, chunk, true))
								break;
							size -= chunk;

							if (size < double.Epsilon)
								break;
						}
					}

					if (size > double.Epsilon)
					{
						// we filled all drives up to the brim but were unable to store everything
						if (warp_buffer < double.Epsilon)
						{
							// warp buffer is empty, so lets store the rest there
							warp_buffer = size;
							size = 0;
						}
						else
						{
							// warp buffer not empty. that's ok if we didn't get new data
							if (coverage_delta < double.Epsilon)
							{
								size = 0;
							}
							// else we're scanning too fast. stop.
						}
					}

					// we filled all drives up to the brim but were unable to store everything
					// cancel scanning and annoy the user
					if (size > double.Epsilon || ec.Amount < double.Epsilon)
					{
						warp_buffer = 0;
						SCANsat.StopScanner(vessel, scanner, part_prefab);
						vd.scansat_id.Add(p.flightID);
						if (vd.cfg_ec) Message.Post(Local.Scansat_sensordisabled.Format("<b>"+vessel.vesselName+"</b>"));//Lib.BuildString("SCANsat sensor was disabled on <<1>>)
					}
				}
				else if (vd.scansat_id.Contains(p.flightID))
				{
					if (ec.Level >= 0.25 && (vd.DrivesFreeSpace / vd.DrivesCapacity > 0.9))
					{
						SCANsat.ResumeScanner(vessel, scanner, part_prefab);
						vd.scansat_id.Remove(p.flightID);
						if (vd.cfg_ec) Message.Post(Local.Scansat_sensorresumed.Format("<b>"+vessel.vesselName+"</b>"));//Lib.BuildString("SCANsat sensor resumed operations on <<1>>)
					}
				}
			}

			Lib.Proto.Set(m, "warp_buffer", warp_buffer);
			Lib.Proto.Set(m, "body_coverage", body_coverage);
			Lib.Proto.Set(m, "body_name", body_name);
		}
	}
}
