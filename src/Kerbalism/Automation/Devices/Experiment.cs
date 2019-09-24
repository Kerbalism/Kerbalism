using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{


	public sealed class ExperimentDevice : Device
	{
		public ExperimentDevice(Experiment exp)
		{
			this.experiment = exp;
			expInfo = Science.Experiment(string.IsNullOrEmpty(experiment.last_subject_id) ? experiment.experiment_id : experiment.last_subject_id);
			icon = new DeviceIcon(exp.sample_mass > 0f ? Icons.sample_scicolor : Icons.file_scicolor, "open experiment window", () => new SciencePopup(exp.vessel, exp));
		}

		public override string Name()
		{
			return Lib.BuildString(" ", expInfo.Name);
		}

		public override uint Part()
		{
			return experiment.part.flightID;
		}

		public override string Status()
		{
			return Experiment.StatusInfo(experiment.Status, experiment.issue);
		}

		public override string Tooltip()
		{
			return Lib.BuildString(experiment.part.partInfo.title, "\nissue : ", experiment.issue);
		}

		public override DeviceIcon Icon => icon;

		public override void Ctrl(bool value)
		{
			if (value != experiment.Running) Toggle();
		}

		public override void Toggle()
		{
			experiment.Toggle();
		}

		private ExperimentInfo expInfo;
		private Experiment experiment;
		private readonly DeviceIcon icon;
	}

	public sealed class ProtoExperimentDevice : Device
	{
		public ProtoExperimentDevice(ProtoPartModuleSnapshot proto, Experiment prefab, uint part_id, Vessel vessel)
		{
			this.proto = proto;
			this.prefab = prefab;
			this.part_id = part_id;
			this.vessel = vessel;

			isSample = prefab.sample_mass > 0f;
			icon = new DeviceIcon(prefab.sample_mass > 0f ? Icons.sample_scicolor : Icons.file_scicolor, "open experiment window", () => new SciencePopup(vessel, prefab, proto));

			OnUpdate();
		}

		public override string Name()
		{
			return Lib.BuildString(" ", expInfo.Name);
		}

		public override uint Part()
		{
			return part_id;
		}

		public override string Status()
		{
			return Experiment.StatusInfo(status);
		}

		public override string Tooltip()
		{
			return issue;
		}

		public override DeviceIcon Icon => icon;

		public override void Ctrl(bool value)
		{
			if (value != Experiment.IsRunning(status)) Experiment.ProtoToggle(vessel, prefab, proto);
		}

		public override void Toggle()
		{
			Experiment.ProtoToggle(vessel, prefab, proto);
		}

		public override void OnUpdate()
		{
			issue = Lib.Proto.GetString(proto, "issue");
			subject_id = Lib.Proto.GetString(proto, "last_subject_id", prefab.experiment_id);
			if (string.IsNullOrEmpty(subject_id)) subject_id = prefab.experiment_id;
			expInfo = Science.Experiment(subject_id);
			status = Lib.Proto.GetEnum(proto, "status", Experiment.ExpStatus.Stopped);
		}

		private readonly ProtoPartModuleSnapshot proto;
		private readonly Experiment prefab;
		private readonly uint part_id;
		private readonly Vessel vessel;

		private readonly DeviceIcon icon;
		private readonly bool isSample;

		private string issue;
		private string subject_id;
		private ExperimentInfo expInfo;
		private Experiment.ExpStatus status;
	}


} // KERBALISM

