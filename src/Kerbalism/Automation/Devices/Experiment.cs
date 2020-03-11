using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;
using System.Text;

namespace KERBALISM
{
	public sealed class ExperimentDevice : LoadedDevice<ModuleKsmExperiment>
	{
		private readonly DeviceIcon icon;
		private StringBuilder sb;
		private string scienceValue;

		public ExperimentDevice(ModuleKsmExperiment module) : base(module)
		{
			icon = new DeviceIcon(module.ExpInfo.SampleMass > 0.0 ? Textures.sample_scicolor : Textures.file_scicolor, "open experiment window", () => new ExperimentPopup(module.vessel, module, PartId, PartName));
			sb = new StringBuilder();
			OnUpdate();
		}

		public override void OnUpdate()
		{
			scienceValue = ModuleKsmExperiment.ScienceValue(module.Subject);
		}

		public override string Name => module.experiment_id;

		public override string DisplayName
		{
			get
			{
				sb.Length = 0;
				sb.Append(Lib.EllipsisMiddle(module.ExpInfo.Title, 28));
				sb.Append(": ");
				sb.Append(scienceValue);

				if (module.Status == ModuleKsmExperiment.ExpStatus.Running)
				{
					sb.Append(" ");
					sb.Append(ModuleKsmExperiment.RunningCountdown(module.ExpInfo, module.Subject, module.data_rate));
				}
				else if (module.Subject != null && module.Status == ModuleKsmExperiment.ExpStatus.Forced)
				{
					sb.Append(" ");
					sb.Append(module.Subject.PercentCollectedTotal.ToString("P0"));
				}
				return sb.ToString();
			}
		}

		public override string Status => ModuleKsmExperiment.StatusInfo(module.Status, module.issue);

		public override string Tooltip
		{
			get
			{
				sb.Length = 0;
				if (module.Subject != null)
					sb.Append(module.Subject.FullTitle);
				else
					sb.Append(module.ExpInfo.Title);
				sb.Append("\n");
				sb.Append(Local.Experiment_on);//on
				sb.Append(" ");
				sb.Append(module.part.partInfo.title);
				sb.Append("\n");
				sb.Append(Local.Experiment_status);//status :
				sb.Append(" ");
				sb.Append(ModuleKsmExperiment.StatusInfo(module.Status));

				if (module.Status == ModuleKsmExperiment.ExpStatus.Issue)
				{
					sb.Append("\n");
					sb.Append(Local.Experiment_issue);//issue :
					sb.Append(" ");
					sb.Append(Lib.Color(module.issue, Lib.Kolor.Orange));
				}
				sb.Append("\n");
				sb.Append(Local.Experiment_sciencevalue);//science value :
				sb.Append(" ");
				sb.Append(scienceValue);

				if (module.Status == ModuleKsmExperiment.ExpStatus.Running)
				{
					sb.Append("\n");
					sb.Append(Local.Experiment_completion);//completion :
					sb.Append(" ");
					sb.Append(ModuleKsmExperiment.RunningCountdown(module.ExpInfo, module.Subject, module.data_rate, false));
				}
				else if (module.Subject != null && module.Status == ModuleKsmExperiment.ExpStatus.Forced)
				{
					sb.Append("\n");
					sb.Append(Local.Experiment_completion);//completion :
					sb.Append(" ");
					sb.Append(module.Subject.PercentCollectedTotal.ToString("P0"));
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

	public sealed class ProtoExperimentDevice : ProtoDevice<ModuleKsmExperiment>
	{
		private readonly Vessel vessel;

		private readonly DeviceIcon icon;

		private string issue;
		private ExperimentInfo expInfo;
		private ModuleKsmExperiment.ExpStatus status;
		private SubjectData subject;
		private string scienceValue;

		private StringBuilder sb;

		public ProtoExperimentDevice(ModuleKsmExperiment prefab, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule, Vessel vessel)
			: base(prefab, protoPart, protoModule)
		{
			this.vessel = vessel;
			expInfo = ScienceDB.GetExperimentInfo(prefab.experiment_id);
			icon = new DeviceIcon(expInfo.SampleMass > 0f ? Textures.sample_scicolor : Textures.file_scicolor, "open experiment info", () => new ExperimentPopup(vessel, prefab, protoPart.flightID, prefab.part.partInfo.title, protoModule));
			sb = new StringBuilder();

			OnUpdate();
		}

		public override void OnUpdate()
		{
			issue = Lib.Proto.GetString(protoModule, "issue");
			status = Lib.Proto.GetEnum(protoModule, "status", ModuleKsmExperiment.ExpStatus.Stopped);
			subject = ScienceDB.GetSubjectData(expInfo, Lib.Proto.GetInt(protoModule, "situationId"));
			scienceValue = ModuleKsmExperiment.ScienceValue(subject);
		}

		public override string Name => prefab.experiment_id;

		public override string DisplayName
		{
			get
			{
				sb.Length = 0;
				sb.Append(Lib.EllipsisMiddle(expInfo.Title, 28));
				sb.Append(": ");
				sb.Append(scienceValue);

				if (status == ModuleKsmExperiment.ExpStatus.Running)
				{
					sb.Append(" ");
					sb.Append(ModuleKsmExperiment.RunningCountdown(expInfo, subject, prefab.data_rate));
				}
				else if (subject != null && status == ModuleKsmExperiment.ExpStatus.Forced)
				{
					sb.Append(" ");
					sb.Append(subject.PercentCollectedTotal.ToString("P0"));
				}
				return sb.ToString();
			}
		}

		public override string Status => ModuleKsmExperiment.StatusInfo(status, issue);

		public override string Tooltip
		{
			get
			{
				sb.Length = 0;
				if (subject != null && ModuleKsmExperiment.IsRunning(status))
					sb.Append(subject.FullTitle);
				else
					sb.Append(expInfo.Title);
				sb.Append("\n");
				sb.Append(Local.Experiment_on);//on
				sb.Append(" ");
				sb.Append(prefab.part.partInfo.title);
				sb.Append("\n");
				sb.Append(Local.Experiment_status);//status :
				sb.Append(" ");
				sb.Append(ModuleKsmExperiment.StatusInfo(status));

				if (status == ModuleKsmExperiment.ExpStatus.Issue)
				{
					sb.Append("\n");
					sb.Append(Local.Experiment_issue);//issue :
					sb.Append(" ");
					sb.Append(Lib.Color(issue, Lib.Kolor.Orange));
				}
				sb.Append("\n");
				sb.Append(Local.Experiment_sciencevalue);//science value :
				sb.Append(" ");
				sb.Append(scienceValue);

				if (status == ModuleKsmExperiment.ExpStatus.Running)
				{
					sb.Append("\n");
					sb.Append(Local.Experiment_completion);//completion :
					sb.Append(" ");
					sb.Append(ModuleKsmExperiment.RunningCountdown(expInfo, subject, prefab.data_rate, false));
				}
				else if (subject != null && status == ModuleKsmExperiment.ExpStatus.Forced)
				{
					sb.Append("\n");
					sb.Append(Local.Experiment_completion);//completion :
					sb.Append(" ");
					sb.Append(subject.PercentCollectedTotal.ToString("P0"));
				}

				return sb.ToString();
			}
		}

		public override DeviceIcon Icon => icon;

		public override void Ctrl(bool value)
		{
			if (value != ModuleKsmExperiment.IsRunning(status)) ModuleKsmExperiment.ProtoToggle(vessel, prefab, protoModule);
		}

		public override void Toggle()
		{
			ModuleKsmExperiment.ProtoToggle(vessel, prefab, protoModule);
		}
	}
} // KERBALISM

