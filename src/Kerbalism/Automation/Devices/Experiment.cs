using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;
using System.Text;

namespace KERBALISM
{
	public sealed class ExperimentDevice : LoadedDevice<Experiment>
	{
		private ExperimentInfo expInfo;
		private readonly DeviceIcon icon;
		private StringBuilder sb;
		private string scienceValue;

		public ExperimentDevice(Experiment module) : base(module)
		{
			expInfo = Science.Experiment(string.IsNullOrEmpty(module.last_subject_id) ? module.experiment_id : module.last_subject_id);
			icon = new DeviceIcon(module.sample_mass > 0f ? Textures.sample_scicolor : Textures.file_scicolor, "open experiment window", () => new SciencePopup(module.vessel, module));
			sb = new StringBuilder();
			OnUpdate();
		}

		public override void OnUpdate()
		{
			scienceValue = Experiment.ScienceValue(module.ExpInfo);
		}

		public override string Name
		{
			get
			{
				sb.Length = 0;
				sb.Append(expInfo.Name);
				sb.Append(": ");
				sb.Append(scienceValue);

				if (module.Status == Experiment.ExpStatus.Running)
				{
					sb.Append(" ");
					sb.Append(Experiment.RunningCountdown(module.ExpInfo, module.data_rate));
				}
				else if (module.Status == Experiment.ExpStatus.Forced)
				{
					sb.Append(" ");
					sb.Append(module.ExpInfo.SubjectPercentCollectedTotal.ToString("P0"));
				}
				return sb.ToString();
			}
		}

		public override string Status => Experiment.StatusInfo(module.Status, module.issue);

		public override string Tooltip
		{
			get
			{
				sb.Length = 0;
				if (module.Running)
					sb.Append(expInfo.SubjectName);
				else
					sb.Append(expInfo.Name);
				sb.Append("\non ");
				sb.Append(module.part.partInfo.title);
				sb.Append("\nstatus : ");
				sb.Append(Experiment.StatusInfo(module.Status));

				if (module.Status == Experiment.ExpStatus.Issue)
				{
					sb.Append("\nissue : ");
					sb.Append(Lib.Color(module.issue, Lib.KColor.Orange));
				}
				sb.Append("\nscience value : ");
				sb.Append(scienceValue);

				if (module.Status == Experiment.ExpStatus.Running)
				{
					sb.Append("\ncompletion : ");
					sb.Append(Experiment.RunningCountdown(module.ExpInfo, module.data_rate, false));
				}
				else if (module.Status == Experiment.ExpStatus.Forced)
				{
					sb.Append("\ncompletion : ");
					sb.Append(module.ExpInfo.SubjectPercentCollectedTotal.ToString("P0"));
				}

				return sb.ToString();
			}
		}

		public override DeviceIcon Icon => icon;

		public override void Ctrl(bool value)
		{
			if (value != module.Running) Toggle();
		}

		public override void Toggle()
		{
			module.Toggle();
		}

		public override string PartName => module.part.partInfo.title;
	}

	public sealed class ProtoExperimentDevice : ProtoDevice<Experiment>
	{
		private readonly Vessel vessel;

		private readonly DeviceIcon icon;

		private string issue;
		private string subject_id;
		private ExperimentInfo expInfo;
		private Experiment.ExpStatus status;
		private string scienceValue;

		private StringBuilder sb;

		public ProtoExperimentDevice(Experiment prefab, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule, Vessel vessel)
			: base(prefab, protoPart, protoModule)
		{
			this.vessel = vessel;
			icon = new DeviceIcon(prefab.sample_mass > 0f ? Textures.sample_scicolor : Textures.file_scicolor, "open experiment info", () => new SciencePopup(vessel, prefab, protoModule));

			sb = new StringBuilder();

			OnUpdate();
		}

		public override void OnUpdate()
		{
			issue = Lib.Proto.GetString(protoModule, "issue");
			subject_id = Lib.Proto.GetString(protoModule, "last_subject_id", prefab.experiment_id);
			if (string.IsNullOrEmpty(subject_id)) subject_id = prefab.experiment_id;
			expInfo = Science.Experiment(subject_id);
			status = Lib.Proto.GetEnum(protoModule, "status", Experiment.ExpStatus.Stopped);
			scienceValue = Experiment.ScienceValue(expInfo);
		}

		public override string Name
		{
			get
			{
				sb.Length = 0;
				sb.Append(expInfo.Name);
				sb.Append(": ");
				sb.Append(scienceValue);

				if (status == Experiment.ExpStatus.Running)
				{
					sb.Append(" ");
					sb.Append(Experiment.RunningCountdown(expInfo, prefab.data_rate));
				}
				else if (status == Experiment.ExpStatus.Forced)
				{
					sb.Append(" ");
					sb.Append(expInfo.SubjectPercentCollectedTotal.ToString("P0"));
				}
				return sb.ToString();
			}
		}

		public override string Status => Experiment.StatusInfo(status, issue);

		public override string Tooltip
		{
			get
			{
				sb.Length = 0;
				if (Experiment.IsRunning(status))
					sb.Append(expInfo.SubjectName);
				else
					sb.Append(expInfo.Name);
				sb.Append("\non ");
				sb.Append(prefab.part.partInfo.title);
				sb.Append("\nstatus : ");
				sb.Append(Experiment.StatusInfo(status));

				if (status == Experiment.ExpStatus.Issue)
				{
					sb.Append("\nissue : ");
					sb.Append(Lib.Color(issue, Lib.KColor.Orange));
				}
				sb.Append("\nscience value : ");
				sb.Append(scienceValue);

				if (status == Experiment.ExpStatus.Running)
				{
					sb.Append("\ncompletion : ");
					sb.Append(Experiment.RunningCountdown(expInfo, prefab.data_rate, false));
				}
				else if (status == Experiment.ExpStatus.Forced)
				{
					sb.Append("\ncompletion : ");
					sb.Append(expInfo.SubjectPercentCollectedTotal.ToString("P0"));
				}

				return sb.ToString();
			}
		}

		public override DeviceIcon Icon => icon;

		public override void Ctrl(bool value)
		{
			if (value != Experiment.IsRunning(status)) Experiment.ProtoToggle(vessel, prefab, protoModule);
		}

		public override void Toggle()
		{
			Experiment.ProtoToggle(vessel, prefab, protoModule);
		}
	}
} // KERBALISM

