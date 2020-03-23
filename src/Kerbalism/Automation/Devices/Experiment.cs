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
		private ExperimentData data;

		public ExperimentDevice(ModuleKsmExperiment module) : base(module)
		{
			data = module.moduleData;
			icon = new DeviceIcon(data.ModuleDefinition.Info.SampleMass > 0.0 ? Textures.sample_scicolor : Textures.file_scicolor, "open experiment window", () => new ExperimentPopup(data));
			sb = new StringBuilder();
			OnUpdate();
		}

		public override void OnUpdate()
		{
			scienceValue = ModuleKsmExperiment.ScienceValue(data.Subject);
		}

		public override string Name => data.ExperimentID;

		public override string DisplayName
		{
			get
			{
				sb.Length = 0;
				sb.Append(Lib.EllipsisMiddle(data.ModuleDefinition.Info.Title, 28));
				sb.Append(": ");
				sb.Append(scienceValue);

				if (data.Status == ExperimentData.ExpStatus.Running)
				{
					sb.Append(" ");
					sb.Append(ModuleKsmExperiment.RunningCountdown(data.ModuleDefinition.Info, data.Subject, data.ModuleDefinition.DataRate));
				}
				else if (data.Subject != null && data.Status == ExperimentData.ExpStatus.Forced)
				{
					sb.Append(" ");
					sb.Append(data.Subject.PercentCollectedTotal.ToString("P0"));
				}
				return sb.ToString();
			}
		}

		public override string Status => ModuleKsmExperiment.StatusInfo(data.Status, data.issue);

		public override string Tooltip
		{
			get
			{
				sb.Length = 0;
				if (data.Subject != null)
					sb.Append(data.Subject.FullTitle);
				else
					sb.Append(data.ModuleDefinition.Info.Title);
				sb.Append("\n");
				sb.Append(Local.Experiment_on);//on
				sb.Append(" ");
				sb.Append(module.part.partInfo.title);
				sb.Append("\n");
				sb.Append(Local.Experiment_status);//status :
				sb.Append(" ");
				sb.Append(ModuleKsmExperiment.StatusInfo(data.Status));

				if (data.Status == ExperimentData.ExpStatus.Issue)
				{
					sb.Append("\n");
					sb.Append(Local.Experiment_issue);//issue :
					sb.Append(" ");
					sb.Append(Lib.Color(data.issue, Lib.Kolor.Orange));
				}
				sb.Append("\n");
				sb.Append(Local.Experiment_sciencevalue);//science value :
				sb.Append(" ");
				sb.Append(scienceValue);

				if (data.Status == ExperimentData.ExpStatus.Running)
				{
					sb.Append("\n");
					sb.Append(Local.Experiment_completion);//completion :
					sb.Append(" ");
					sb.Append(ModuleKsmExperiment.RunningCountdown(data.ModuleDefinition.Info, data.Subject, data.ModuleDefinition.DataRate, false));
				}
				else if (data.Subject != null && data.Status == ExperimentData.ExpStatus.Forced)
				{
					sb.Append("\n");
					sb.Append(Local.Experiment_completion);//completion :
					sb.Append(" ");
					sb.Append(data.Subject.PercentCollectedTotal.ToString("P0"));
				}

				return sb.ToString();
			}
		}

		public override DeviceIcon Icon => icon;

		public override void Ctrl(bool value)
		{
			if (value != data.IsRunningRequested) Toggle();
		}

		public override void Toggle()
		{
			ModuleKsmExperiment.Toggle(data);
		}

		public override string PartName => module.part.partInfo.title;
	}

	public sealed class ProtoExperimentDevice : ProtoDevice<ModuleKsmExperiment>
	{
		private readonly DeviceIcon icon;
		private StringBuilder sb;
		private string scienceValue;
		private ExperimentData data;

		public ProtoExperimentDevice(ModuleKsmExperiment prefab, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule, Vessel vessel)
			: base(prefab, protoPart, protoModule)
		{
			ModuleData.TryGetModuleData<ModuleKsmExperiment, ExperimentData>(protoModule, out data);
			icon = new DeviceIcon(data.ModuleDefinition.Info.SampleMass > 0.0 ? Textures.sample_scicolor : Textures.file_scicolor, "open experiment window", () => new ExperimentPopup(data));
			sb = new StringBuilder();
			OnUpdate();
		}

		public override void OnUpdate()
		{
			scienceValue = ModuleKsmExperiment.ScienceValue(data.Subject);
		}

		public override string Name => data.ExperimentID;

		public override string DisplayName
		{
			get
			{
				sb.Length = 0;
				sb.Append(Lib.EllipsisMiddle(data.ModuleDefinition.Info.Title, 28));
				sb.Append(": ");
				sb.Append(scienceValue);

				if (data.Status == ExperimentData.ExpStatus.Running)
				{
					sb.Append(" ");
					sb.Append(ModuleKsmExperiment.RunningCountdown(data.ModuleDefinition.Info, data.Subject, data.ModuleDefinition.DataRate));
				}
				else if (data.Subject != null && data.Status == ExperimentData.ExpStatus.Forced)
				{
					sb.Append(" ");
					sb.Append(data.Subject.PercentCollectedTotal.ToString("P0"));
				}
				return sb.ToString();
			}
		}

		public override string Status => ModuleKsmExperiment.StatusInfo(data.Status, data.issue);

		public override string Tooltip
		{
			get
			{
				sb.Length = 0;
				if (data.Subject != null)
					sb.Append(data.Subject.FullTitle);
				else
					sb.Append(data.ModuleDefinition.Info.Title);
				sb.Append("\n");
				sb.Append(Local.Experiment_on);//on
				sb.Append(" ");
				sb.Append(data.partData.Title);
				sb.Append("\n");
				sb.Append(Local.Experiment_status);//status :
				sb.Append(" ");
				sb.Append(ModuleKsmExperiment.StatusInfo(data.Status));

				if (data.Status == ExperimentData.ExpStatus.Issue)
				{
					sb.Append("\n");
					sb.Append(Local.Experiment_issue);//issue :
					sb.Append(" ");
					sb.Append(Lib.Color(data.issue, Lib.Kolor.Orange));
				}
				sb.Append("\n");
				sb.Append(Local.Experiment_sciencevalue);//science value :
				sb.Append(" ");
				sb.Append(scienceValue);

				if (data.Status == ExperimentData.ExpStatus.Running)
				{
					sb.Append("\n");
					sb.Append(Local.Experiment_completion);//completion :
					sb.Append(" ");
					sb.Append(ModuleKsmExperiment.RunningCountdown(data.ModuleDefinition.Info, data.Subject, data.ModuleDefinition.DataRate, false));
				}
				else if (data.Subject != null && data.Status == ExperimentData.ExpStatus.Forced)
				{
					sb.Append("\n");
					sb.Append(Local.Experiment_completion);//completion :
					sb.Append(" ");
					sb.Append(data.Subject.PercentCollectedTotal.ToString("P0"));
				}

				return sb.ToString();
			}
		}

		public override DeviceIcon Icon => icon;

		public override void Ctrl(bool value)
		{
			if (value != data.IsRunningRequested) Toggle();
		}

		public override void Toggle()
		{
			ModuleKsmExperiment.Toggle(data);
		}

		public override string PartName => data.partData.Title;
	}
} // KERBALISM

