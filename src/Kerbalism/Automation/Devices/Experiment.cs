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
			var state = Experiment.GetState(experiment.scienceValue, experiment.issue, experiment.recording, experiment.forcedRun);
			if (state == Experiment.State.WAITING) return "waiting...";
			var exp = Science.Experiment(experiment.experiment_id);
			var recordedPercent = Lib.HumanReadablePerc(experiment.dataSampled / exp.max_amount);
			var eta = experiment.data_rate < double.Epsilon || Experiment.Done(exp, experiment.dataSampled) ? " done" : " " + Lib.HumanReadableCountdown((exp.max_amount - experiment.dataSampled) / experiment.data_rate);

			return !experiment.recording
			  ? "<color=red>" + Localizer.Format("#KERBALISM_Generic_DISABLED") + " </color>"
			  : experiment.issue.Length == 0 ? "<color=cyan>" + Lib.BuildString(recordedPercent, eta) + "</color>"
			  : Lib.BuildString("<color=yellow>", experiment.issue.ToLower(), "</color>");
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
			double scienceValue = Lib.Proto.GetDouble(proto, "scienceValue");
			string issue = Lib.Proto.GetString(proto, "issue");

			var state = Experiment.GetState(scienceValue, issue, recording, forcedRun);
			if (state == Experiment.State.WAITING) return "waiting...";

			double dataSampled = Lib.Proto.GetDouble(proto, "dataSampled");
			double data_rate = Lib.Proto.GetDouble(proto, "data_rate");

			var exp = Science.Experiment(prefab.experiment_id);
			var recordedPercent = Lib.HumanReadablePerc(dataSampled / exp.max_amount);
			var eta = data_rate < double.Epsilon || Experiment.Done(exp, dataSampled) ? " done" : " " + Lib.HumanReadableCountdown((exp.max_amount - dataSampled) / data_rate);

			return !recording
			  ? "<color=red>" + Localizer.Format("#KERBALISM_Generic_STOPPED") + " </color>"
			  : issue.Length == 0 ? "<color=cyan>" + Lib.BuildString(recordedPercent, eta) + "</color>"
			  : Lib.BuildString("<color=yellow>", issue.ToLower(), "</color>");
		}

		public override void Ctrl(bool value)
		{
			bool recording = Lib.Proto.GetBool(proto, "recording");
			bool forcedRun = Lib.Proto.GetBool(proto, "forcedRun");
			double scienceValue = Lib.Proto.GetDouble(proto, "scienceValue");
			string issue = Lib.Proto.GetString(proto, "issue");
			var state = Experiment.GetState(scienceValue, issue, recording, forcedRun);


			if(state == Experiment.State.WAITING)
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
					if (e.part.flightID == prefab.part.flightID) continue;
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

