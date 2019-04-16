using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{
	public sealed class KerbalismScansat : PartModule
	{
		[KSPField] public string experimentType = string.Empty;

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
				if(Background.ModuleType(module.name) == Background.Module_type.Scanner)
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
				double coverage_delta = body_coverage - new_coverage;
				body_coverage = new_coverage;

				if(IsScanning)
				{
					var subject_id = Science.Generate_subject_id(experimentType, vessel);
					var exp = Science.Experiment(subject_id);
					double size = exp.max_amount * coverage_delta;

					var drive = DB.Vessel(vessel).FileDrive(size);
					var stored = drive.Record_file(subject_id, size, true, true);
					if (!stored) StopScan();
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
	}
}
