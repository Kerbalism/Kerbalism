using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{
	public sealed class ExperimentDevice : LoadedDevice<Experiment>
	{
		public ExperimentDevice(Experiment module) : base(module)
		{
			expInfo = Science.Experiment(string.IsNullOrEmpty(module.last_subject_id) ? module.experiment_id : module.last_subject_id);
			icon = new DeviceIcon(module.sample_mass > 0f ? Icons.sample_scicolor : Icons.file_scicolor, "open experiment window", () => new SciencePopup(module.vessel, module));
		}

		public override string Name => expInfo.Name;

		public override string Status => Experiment.StatusInfo(module.Status, module.issue);

		public override string Tooltip => Lib.BuildString(base.Tooltip, "\nissue : ", module.issue);

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

		private ExperimentInfo expInfo;
		private readonly DeviceIcon icon;
	}

	public sealed class ProtoExperimentDevice : ProtoDevice<Experiment>
	{
		private readonly Vessel vessel;

		private readonly DeviceIcon icon;
		private readonly bool isSample;

		private string issue;
		private string subject_id;
		private ExperimentInfo expInfo;
		private Experiment.ExpStatus status;

		public ProtoExperimentDevice(Experiment prefab, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule, Vessel vessel)
			: base(prefab, protoPart, protoModule)
		{
			this.vessel = vessel;

			isSample = prefab.sample_mass > 0f;
			icon = new DeviceIcon(prefab.sample_mass > 0f ? Icons.sample_scicolor : Icons.file_scicolor, "open experiment window", () => new SciencePopup(vessel, prefab, protoModule));

			OnUpdate();
		}

		public override string Name => expInfo.Name;

		public override string Status => Experiment.StatusInfo(status);

		public override string Tooltip => issue;

		public override DeviceIcon Icon => icon;

		public override void Ctrl(bool value)
		{
			if (value != Experiment.IsRunning(status)) Experiment.ProtoToggle(vessel, prefab, protoModule);
		}

		public override void Toggle()
		{
			Experiment.ProtoToggle(vessel, prefab, protoModule);
		}

		public override void OnUpdate()
		{
			issue = Lib.Proto.GetString(protoModule, "issue");
			subject_id = Lib.Proto.GetString(protoModule, "last_subject_id", prefab.experiment_id);
			if (string.IsNullOrEmpty(subject_id)) subject_id = prefab.experiment_id;
			expInfo = Science.Experiment(subject_id);
			status = Lib.Proto.GetEnum(protoModule, "status", Experiment.ExpStatus.Stopped);
		}
	}


} // KERBALISM

