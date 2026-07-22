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
			{
				Message.Post(Lib.Color(expInfo != null ? expInfo.Title : module.experimentType, Lib.Kolor.Cyan, true), BuildDetail(module));
			});
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
				sb.Append(module.PendingCoveragePercent.ToString("F1"));
				sb.Append("% pending");
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
			sb.Append("\nPending map data: ");
			sb.Append(module.PendingCoveragePercent.ToString("F2"));
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
		private readonly StringBuilder sb = new StringBuilder();

		public ProtoKerbalismScansatDevice(KerbalismScansat prefab, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule, Vessel vessel)
			: base(prefab, protoPart, protoModule)
		{
			this.vessel = vessel;
		}

		public override string Name => prefab.experimentType;

		public override string DisplayName
		{
			get
			{
				ExperimentInfo expInfo = ScienceDB.GetExperimentInfo(prefab.experimentType);
				int sensorType = (int)Lib.Proto.GetUInt(protoModule, "sensorType");
				double pending = 0.0;
				if (vessel != null && vessel.mainBody != null)
					pending = ScanCoverageStore.PendingCoveragePercent(vessel.id, vessel.mainBody.flightGlobalsIndex, (short)sensorType);

				sb.Length = 0;
				sb.Append(Lib.EllipsisMiddle(expInfo != null ? expInfo.Title : prefab.experimentType, 28));
				sb.Append(": ");
				sb.Append(pending.ToString("F1"));
				sb.Append("% pending");
				return sb.ToString();
			}
		}

		public override string Status
		{
			get
			{
				bool scanning = false;
				foreach (ProtoPartModuleSnapshot module in protoPart.modules)
				{
					if (module.moduleName == "SCANsat" || module.moduleName == "ModuleSCANresourceScanner")
					{
						scanning = Lib.Proto.GetBool(module, "scanning");
						break;
					}
				}
				return Lib.Color(scanning, Local.Generic_ENABLED, Lib.Kolor.Green, Local.Generic_DISABLED, Lib.Kolor.Yellow);
			}
		}

		public override void Ctrl(bool value)
		{
			foreach (ProtoPartModuleSnapshot module in protoPart.modules)
			{
				if (module.moduleName != "SCANsat" && module.moduleName != "ModuleSCANresourceScanner")
					continue;

				if (value) SCANsat.ResumeScanner(vessel, module, prefab.part);
				else SCANsat.StopScanner(vessel, module, prefab.part);
				break;
			}
		}

		public override void Toggle()
		{
			bool scanning = false;
			foreach (ProtoPartModuleSnapshot module in protoPart.modules)
			{
				if (module.moduleName == "SCANsat" || module.moduleName == "ModuleSCANresourceScanner")
				{
					scanning = Lib.Proto.GetBool(module, "scanning");
					break;
				}
			}
			Ctrl(!scanning);
		}

		public override string PartName => protoPart.partInfo.title;
	}
}
