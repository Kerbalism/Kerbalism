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
			this.exp_name = Lib.BuildString((exp.sample_mass < float.Epsilon ? "sensor" : "experiment"), ": ", ResearchAndDevelopment.GetExperiment(exp.experiment_id).experimentTitle);
		}

		public override string Name()
		{
			return Lib.BuildString(exp_name, " - ", Experiment.DoneInfo(Science.Experiment(string.IsNullOrEmpty(experiment.last_subject_id) ? experiment.experiment_id : experiment.last_subject_id), experiment.data_rate));
		}

		public override uint Part()
		{
			return experiment.part.flightID;
		}

		public override string Info()
		{
			ExperimentInfo expInfo = Science.Experiment(string.IsNullOrEmpty(experiment.last_subject_id) ? experiment.experiment_id : experiment.last_subject_id);
			return Experiment.StateInfoShort(Experiment.GetState(expInfo, experiment.issue, experiment.recording, experiment.forcedRun), experiment.forcedRun, experiment.issue);
		}

		public override void Ctrl(bool value)
		{
			if (value != experiment.recording) experiment.Toggle();
		}

		public override void Toggle()
		{
			Ctrl(!experiment.recording);
		}

		private Experiment experiment;
		private readonly string exp_name;
	}


	public sealed class ProtoExperimentDevice : Device
	{
		public ProtoExperimentDevice(ProtoPartModuleSnapshot proto, Experiment prefab, uint part_id, string vessel_name,
		                             List<KeyValuePair<Experiment, ProtoPartModuleSnapshot>> allExperiments)
		{
			this.proto = proto;
			this.prefab = prefab;
			this.part_id = part_id;
			this.allExperiments = allExperiments;
			this.title = ResearchAndDevelopment.GetExperiment(prefab.experiment_id).experimentTitle;
			this.exp_name = Lib.BuildString((prefab.sample_mass < float.Epsilon ? "sensor" : "experiment"), ": ", title);
			this.vessel_name = vessel_name;
		}

		public override string Name()
		{
			string subject_id = Lib.Proto.GetString(proto, "last_subject_id", prefab.experiment_id);
			if (string.IsNullOrEmpty(subject_id)) subject_id = prefab.experiment_id;
			return Lib.BuildString(exp_name, " - ", Experiment.DoneInfo(Science.Experiment(subject_id), prefab.data_rate));
		}

		public override uint Part()
		{
			return part_id;
		}

		public override string Info()
		{
			string issue = Lib.Proto.GetString(proto, "issue");
			bool forcedRun = Lib.Proto.GetBool(proto, "forcedRun");
			string subject_id = Lib.Proto.GetString(proto, "last_subject_id", prefab.experiment_id);
			if (string.IsNullOrEmpty(subject_id)) subject_id = prefab.experiment_id;

			ExperimentInfo expInfo = Science.Experiment(subject_id);

			return Experiment.StateInfoShort(
				Experiment.GetState(expInfo, issue,  Lib.Proto.GetBool(proto, "recording"), forcedRun),
				forcedRun,
				issue);
		}

		public override void Ctrl(bool value)
		{
			if (value)
			{
				// The same experiment must run only once on a vessel
				foreach (var pair in allExperiments)
				{
					if (pair.Key.experiment_id != prefab.experiment_id) continue; // check if this is the same experiment
					if (pair.Value == proto) continue; // check if this is the same module
					//if (!prefab.isEnabled || !prefab.enabled) continue;
					if (Lib.Proto.GetBool(pair.Value, "recording"))
					{
						Experiment.PostMultipleRunsMessage(title, vessel_name);
						return;
					}
				}
			}

			if (!value)
			{
				if (!Lib.Proto.GetBool(proto, "forcedRun"))
				{
					Lib.Proto.Set(proto, "forcedRun", true);
					return;
				}
				else
				{
					Lib.Proto.Set(proto, "forcedRun", false);
				}
			}

			Lib.Proto.Set(proto, "recording", value);
			return;
		}

		public override void Toggle()
		{
			Ctrl(!Lib.Proto.GetBool(proto, "recording"));
		}

		private readonly ProtoPartModuleSnapshot proto;
		private readonly Experiment prefab;
		private readonly uint part_id;
		private readonly string exp_name;
		private readonly string title;
		private readonly List<KeyValuePair<Experiment, ProtoPartModuleSnapshot>> allExperiments;
		private readonly string vessel_name;
	}


} // KERBALISM

