using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{
	public sealed class KerbalismScansat : PartModule
	{
		[KSPField] public string experimentType = string.Empty;
		[KSPField] public double ec_rate = 0.0;

		[KSPField(isPersistant = true)] private double min_capacity = 0.25; // TODO <- set this to something reasonable
		[KSPField(isPersistant = true)] private int sensorType = 0;
		[KSPField(isPersistant = true)] private string body_name = string.Empty;
		[KSPField(isPersistant = true)] private double body_coverage = 0.0;

		private PartModule scanner = null;
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
		}

		public void FixedUpdate()
		{
			if (scanner == null) return;
			if (!Features.Science) return;

			IsScanning = SCANsat.IsScanning(scanner);

			double new_coverage = SCANsat.Coverage(sensorType, vessel.mainBody);

			if (vessel.mainBody.name != body_name)
			{
				body_name = vessel.mainBody.name;
				body_coverage = new_coverage;
			}
			else
			{
				double coverage_delta = new_coverage - body_coverage;
				body_coverage = new_coverage;

				var vd = DB.Vessel(vessel);

				if(IsScanning)
				{
					Science.Generate_subject(experimentType, vessel);
					var subject_id = Science.Generate_subject_id(experimentType, vessel);
					var exp = Science.Experiment(subject_id);
					double size = exp.max_amount * coverage_delta / 100.0; // coverage is 0-100%

					if(size > double.Epsilon)
					{
						// store what we can
						foreach (var d in vd.drives.Values)
						{
							if (d.is_private)
								continue;
							
							var available = d.FileCapacityAvailable();
							var chunk = Math.Min(size, available);
							if (!d.Record_file(subject_id, chunk, true, true))
								break;
							size -= chunk;

							if (size < double.Epsilon)
								break;
						}

						// we filled all drives up to the brim but were unable to store everything
						// cancel scanning and annoy the user
						if(size > double.Epsilon)
						{
							StopScan();
							vd.scansat_id.Add(part.flightID);
							Message.Post(Lib.Color("red", "Scanner halted", true), "Scanner halted on <b>" + vessel.vesselName + "</b>. No storage left on vessel.");
						}
					}
				}
				else if(vd.scansat_id.Contains(part.flightID))
				{
					double available = 0;
					foreach (var drive in vd.drives.Values)
						if (!drive.is_private) available += drive.FileCapacityAvailable();
					if(available >= min_capacity)
					{
						StartScan();
						vd.scansat_id.Remove(part.flightID);
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
		                                    Part part_prefab, VesselData vd, Resource_info ec, double elapsed_s)
		{
			List<ProtoPartModuleSnapshot> scanners = Lib.FindModules(p, "SCANsat");
			if (scanners.Count == 0) scanners = Lib.FindModules(p, "ModuleSCANresourceScanner");
			if (scanners.Count == 0) return;
			var scanner = scanners[0];

			bool is_scanning = Lib.Proto.GetBool(scanner, "scanning");
			if(is_scanning && kerbalismScansat.ec_rate > double.Epsilon)
				ec.Consume(kerbalismScansat.ec_rate * elapsed_s, "scanner");

			if (!Features.Science)
			{
				if(is_scanning && ec.amount < double.Epsilon)
				{					
					SCANsat.StopScanner(vessel, scanner, part_prefab);
					is_scanning = false;

					// remember disabled scanner
					vd.scansat_id.Add(p.flightID);

					// give the user some feedback
					if (vd.cfg_ec) Message.Post(Lib.BuildString("SCANsat sensor was disabled on <b>", vessel.vesselName, "</b>"));
				}
				else if (vd.scansat_id.Contains(p.flightID))
				{
					// if there is enough ec
					// note: comparing against amount in previous simulation step
					// re-enable at 25% EC
					if (ec.level > 0.25)
					{
						// re-enable the scanner
						SCANsat.ResumeScanner(vessel, m, part_prefab);
						is_scanning = true;

						// give the user some feedback
						if (vd.cfg_ec) Message.Post(Lib.BuildString("SCANsat sensor resumed operations on <b>", vessel.vesselName, "</b>"));
					}
				}

				// forget active scanners
				if (is_scanning) vd.scansat_id.Remove(p.flightID);

				return;
			} // if(!Feature.Science)

			string body_name = Lib.Proto.GetString(m, "body_name");
			int sensorType = (int)Lib.Proto.GetUInt(m, "sensorType");
			double body_coverage = Lib.Proto.GetDouble(m, "body_coverage");
			double min_capacity = Lib.Proto.GetDouble(m, "min_capacity");

			double new_coverage = SCANsat.Coverage(sensorType, vessel.mainBody);

			if (vessel.mainBody.name != body_name)
			{
				body_name = vessel.mainBody.name;
				body_coverage = new_coverage;
				Lib.Proto.Set(m, "body_name", body_name);
				Lib.Proto.Set(m, "body_coverage", body_coverage);
			}
			else
			{
				double coverage_delta = new_coverage - body_coverage;
				body_coverage = new_coverage;
				Lib.Proto.Set(m, "body_coverage", body_coverage);

				if (is_scanning)
				{
					Science.Generate_subject(kerbalismScansat.experimentType, vessel);
					var subject_id = Science.Generate_subject_id(kerbalismScansat.experimentType, vessel);
					var exp = Science.Experiment(subject_id);
					double size = exp.max_amount * coverage_delta / 100.0; // coverage is 0-100%

					if (size > double.Epsilon)
					{
						// store what we can
						foreach (var d in vd.drives.Values)
						{
							if (d.is_private)
								continue;

							var available = d.FileCapacityAvailable();
							var chunk = Math.Min(size, available);
							if (!d.Record_file(subject_id, chunk, true, true))
								break;
							size -= chunk;

							if (size < double.Epsilon)
								break;
						}
					}

					// we filled all drives up to the brim but were unable to store everything
					// cancel scanning and annoy the user
					if (size > double.Epsilon || ec.amount < double.Epsilon)
					{
						SCANsat.StopScanner(vessel, scanner, part_prefab);
						vd.scansat_id.Add(p.flightID);
						if (vd.cfg_ec) Message.Post(Lib.BuildString("SCANsat sensor was disabled on <b>", vessel.vesselName, "</b>"));
					}
				}
				else if (vd.scansat_id.Contains(p.flightID))
				{
					double available = 0;
					foreach (var drive in vd.drives.Values)
						if (!drive.is_private) available += drive.FileCapacityAvailable();
					
					if (available >= min_capacity && ec.level >= 0.25)
					{
						SCANsat.ResumeScanner(vessel, scanner, part_prefab);
						vd.scansat_id.Remove(p.flightID);
						if (vd.cfg_ec) Message.Post(Lib.BuildString("SCANsat sensor resumed operations on <b>", vessel.vesselName, "</b>"));
					}
				}
			}
		}
	}
}
