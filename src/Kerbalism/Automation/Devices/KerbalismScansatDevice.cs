using System.Text;
using UnityEngine;

namespace KERBALISM
{
	public sealed class KerbalismScansatDevice : LoadedDevice<KerbalismScansat>
	{
		private readonly DeviceIcon icon;
		private readonly StringBuilder sb = new StringBuilder();

		public KerbalismScansatDevice(KerbalismScansat module) : base(module)
		{
			ExperimentInfo expInfo = module.ExpInfo ?? ScienceDB.GetExperimentInfo(module.experimentType);
			Texture2D tex = expInfo != null && expInfo.SampleMass > 0.0 ? Textures.sample_scicolor : Textures.file_scicolor;
			icon = new DeviceIcon(tex, Local.SCIENCEARCHIVE_showexperimentinfo, () =>
				new ScanExperimentPopup(module.vessel, module, PartId, PartName));
		}

		public override string Name => module.experimentType;

		public override string DisplayName
		{
			get
			{
				ExperimentInfo expInfo = module.ExpInfo ?? ScienceDB.GetExperimentInfo(module.experimentType);
				sb.Length = 0;
				sb.Append(Lib.EllipsisMiddle(expInfo != null ? expInfo.Title : module.experimentType, 28));
				sb.Append(": ");
				sb.Append(module.BodyCoveragePercent.ToString("F1"));
				sb.Append("%");
				return sb.ToString();
			}
		}

		public override string Status
		{
			get
			{
				if (!string.IsNullOrEmpty(module.Issue))
					return Lib.Color(module.Issue, Lib.Kolor.Orange);
				return Lib.Color(module.IsScanning, Local.Generic_ENABLED, Lib.Kolor.Green, Local.Generic_DISABLED, Lib.Kolor.Yellow);
			}
		}

		public override string Tooltip
		{
			get
			{
				return BuildDetail(module);
			}
		}

		public override DeviceIcon Icon => icon;

		public override void Ctrl(bool value)
		{
			if (value) module.StartScan();
			else module.StopScan();
		}

		public override void Toggle()
		{
			if (module.IsScanning) module.StopScan();
			else module.StartScan();
		}

		public override string PartName => module.part.partInfo.title;

		private static string BuildDetail(KerbalismScansat module)
		{
			ExperimentInfo expInfo = module.ExpInfo ?? ScienceDB.GetExperimentInfo(module.experimentType);
			var sb = new StringBuilder();
			if (module.CurrentSubject != null)
				sb.Append(module.CurrentSubject.FullTitle);
			else if (expInfo != null)
				sb.Append(expInfo.Title);
			else
				sb.Append(module.experimentType);
			sb.Append("\n");
			sb.Append(Local.Experiment_on);
			sb.Append(" ");
			sb.Append(module.part.partInfo.title);
			sb.Append("\n");
			sb.Append(Local.Experiment_status);
			sb.Append(" ");
			sb.Append(Lib.Color(module.IsScanning, Local.Generic_ENABLED, Lib.Kolor.Green, Local.Generic_DISABLED, Lib.Kolor.Yellow));
			if (!string.IsNullOrEmpty(module.Issue))
			{
				sb.Append("\n");
				sb.Append(Local.Experiment_issue);
				sb.Append(" ");
				sb.Append(Lib.Color(module.Issue, Lib.Kolor.Orange));
			}
			sb.Append("\n");
			sb.Append(Local.SCIENCEARCHIVE_bodycoverage);
			sb.Append(": ");
			sb.Append(module.BodyCoveragePercent.ToString("F2"));
			sb.Append("%");
			if (module.CurrentSubject != null)
			{
				sb.Append("\n");
				sb.Append(Local.Experiment_sciencevalue);
				sb.Append(" ");
				sb.Append(Experiment.ScienceValue(module.CurrentSubject));
			}
			return sb.ToString();
		}
	}

	public sealed class ProtoKerbalismScansatDevice : ProtoDevice<KerbalismScansat>
	{
		private readonly Vessel vessel;
		private readonly DeviceIcon icon;
		private readonly StringBuilder sb = new StringBuilder();

		public ProtoKerbalismScansatDevice(KerbalismScansat prefab, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule, Vessel vessel)
			: base(prefab, protoPart, protoModule)
		{
			this.vessel = vessel;
			ExperimentInfo expInfo = ScienceDB.GetExperimentInfo(prefab.experimentType);
			Texture2D tex = expInfo != null && expInfo.SampleMass > 0.0 ? Textures.sample_scicolor : Textures.file_scicolor;
			icon = new DeviceIcon(tex, Local.SCIENCEARCHIVE_showexperimentinfo, () =>
				new ScanExperimentPopup(vessel, prefab, protoPart.flightID, protoPart.partInfo.title, protoModule));
		}

		public override string Name => prefab.experimentType;

		public override string DisplayName
		{
			get
			{
				ExperimentInfo expInfo = ScienceDB.GetExperimentInfo(prefab.experimentType);
				ProtoPartModuleSnapshot scanner = GetScanner();
				int sensorType = SCANsat.ScienceSensorType(prefab.experimentType);
				if (sensorType == 0)
					sensorType = scanner != null
						? (int)Lib.Proto.GetUInt(scanner, "sensorType")
						: (int)Lib.Proto.GetUInt(protoModule, "sensorType");
				if (sensorType == 0)
					sensorType = prefab.sensorType;
				double coverage = 0.0;
				if (vessel != null && vessel.mainBody != null && sensorType != 0)
					coverage = SCANsat.Coverage(sensorType, vessel.mainBody);

				sb.Length = 0;
				sb.Append(Lib.EllipsisMiddle(expInfo != null ? expInfo.Title : prefab.experimentType, 28));
				sb.Append(": ");
				sb.Append(coverage.ToString("F1"));
				sb.Append("%");
				return sb.ToString();
			}
		}

		public override string Status
		{
			get
			{
				ProtoPartModuleSnapshot scanner = GetScanner();
				bool scanning = scanner != null && Lib.Proto.GetBool(scanner, "scanning");
				return Lib.Color(scanning, Local.Generic_ENABLED, Lib.Kolor.Green, Local.Generic_DISABLED, Lib.Kolor.Yellow);
			}
		}

		public override DeviceIcon Icon => icon;

		public override void Ctrl(bool value)
		{
			ProtoPartModuleSnapshot scanner = GetScanner();
			if (scanner == null)
				return;

			Lib.Proto.Set(protoModule, "power_disabled", false);
			Lib.Proto.Set(protoModule, "storage_disabled", false);
			vessel.KerbalismData().scansat_id.Remove(protoPart.flightID);
			if (value) SCANsat.ResumeScanner(vessel, scanner, prefab.part);
			else SCANsat.StopScanner(vessel, scanner, prefab.part);
		}

		public override void Toggle()
		{
			ProtoPartModuleSnapshot scanner = GetScanner();
			bool scanning = scanner != null && Lib.Proto.GetBool(scanner, "scanning");
			Ctrl(!scanning);
		}

		public override string PartName => protoPart.partInfo.title;

		private ProtoPartModuleSnapshot GetScanner()
		{
			int sensorType = (int)Lib.Proto.GetUInt(protoModule, "sensorType");
			if (sensorType == 0)
				sensorType = prefab.sensorType;
			return SCANsat.FindScanner(protoPart, prefab.experimentType, sensorType);
		}
	}
}
