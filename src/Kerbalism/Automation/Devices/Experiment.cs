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
			this.exp_name = (exp.sample_mass < float.Epsilon ? "sensor" : "experiment")
				+ ": " + Lib.SpacesOnCaps(ResearchAndDevelopment.GetExperiment(exp.experiment_id).experimentTitle).ToLower().Replace("e v a", "eva");
		}

		public override string Name()
		{
			return exp_name;
		}

		public override uint Part()
		{
			return experiment.part.flightID;
		}

		public override string Info()
		{
			ExperimentInfo expInfo = Science.Experiment(experiment.last_subject_id);
			return Experiment.StateInfo(Experiment.GetState(expInfo, experiment.issue, experiment.recording, experiment.forcedRun), expInfo, experiment.data_rate, experiment.issue);
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
			this.title = Lib.SpacesOnCaps(ResearchAndDevelopment.GetExperiment(prefab.experiment_id).experimentTitle).Replace("E V A", "EVA");
			this.exp_name = (prefab.sample_mass < float.Epsilon ? "sensor" : "experiment") + ": " + title.ToLower();
			this.vessel_name = vessel_name;
		}

		public override string Name()
		{
			return exp_name;
		}

		public override uint Part()
		{
			return part_id;
		}

		public override string Info()
		{
			bool recording = Lib.Proto.GetBool(proto, "recording");
			bool forcedRun = Lib.Proto.GetBool(proto, "forcedRun");
			string issue = Lib.Proto.GetString(proto, "issue");
			string subject_id = Lib.Proto.GetString(proto, "last_subject_id", prefab.experiment_id);
			if (string.IsNullOrEmpty(subject_id)) subject_id = prefab.experiment_id;

			ExperimentInfo expInfo = Science.Experiment(subject_id);
			return Experiment.StateInfo(Experiment.GetState(expInfo, issue, recording, forcedRun), expInfo, prefab.data_rate, issue);
		}

		public override void Ctrl(bool value)
		{
			bool recording = Lib.Proto.GetBool(proto, "recording");
			bool forcedRun = Lib.Proto.GetBool(proto, "forcedRun");
			string issue = Lib.Proto.GetString(proto, "issue");
			string subject_id = Lib.Proto.GetString(proto, "last_subject_id", prefab.experiment_id);
			if (string.IsNullOrEmpty(subject_id)) subject_id = prefab.experiment_id;

			var state = Experiment.GetState(Science.Experiment(subject_id), issue, recording, forcedRun);


			if (state == Experiment.State.WAITING)
			{
				Lib.Proto.Set(proto, "forcedRun", true);
				return;
			}
			   
			if (value)
			{
				// The same experiment must run only once on a vessel
				foreach (var pair in allExperiments)
				{
					var e = pair.Key;
					var p = pair.Value;
					if (e.experiment_id != prefab.experiment_id) continue;
					if (!e.isEnabled || !e.enabled) continue;
					// if (p.part.flightID == prefab.part.flightID) continue;
					if (recording)
					{
						Experiment.PostMultipleRunsMessage(title, vessel_name);
						return;
					}
				}
			}

			Lib.Proto.Set(proto, "recording", value);
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

