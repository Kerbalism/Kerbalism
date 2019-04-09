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
			if (experiment.recording && experiment.scienceValue < double.Epsilon && PreferencesScience.Instance.smartScience)
				return "waiting...";

			var exp = ResearchAndDevelopment.GetExperiment(experiment.experiment_id);
			var sampleSize = (exp.baseValue * exp.dataScale);
			var recordedPercent = Lib.HumanReadablePerc(experiment.dataSampled / sampleSize);
			var eta = experiment.data_rate < double.Epsilon || experiment.dataSampled >= sampleSize ? " done" : " " + Lib.HumanReadableCountdown((sampleSize - experiment.dataSampled) / experiment.data_rate);

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
		public ProtoExperimentDevice(ProtoPartModuleSnapshot proto, Experiment prefab, uint part_id,
		                             List<KeyValuePair<Experiment, ProtoPartModuleSnapshot>> allExperiments)
		{
			this.proto = proto;
			this.prefab = prefab;
			this.part_id = part_id;
			this.allExperiments = allExperiments;
			this.title = Lib.SpacesOnCaps(ResearchAndDevelopment.GetExperiment(prefab.experiment_id).experimentTitle).Replace("E V A", "EVA");
			this.exp_name = (prefab.sample_mass < float.Epsilon ? "sensor" : "experiment") + ": " + title.ToLower();
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
			double scienceValue = Lib.Proto.GetDouble(proto, "scienceValue");

			if (recording && scienceValue < double.Epsilon && PreferencesScience.Instance.smartScience)
				return "waiting...";

			string issue = Lib.Proto.GetString(proto, "issue");
			double dataSampled = Lib.Proto.GetDouble(proto, "dataSampled");
			double data_rate = Lib.Proto.GetDouble(proto, "data_rate");

			var exp = ResearchAndDevelopment.GetExperiment(prefab.experiment_id);
			var sampleSize = (exp.baseValue * exp.dataScale);
			var recordedPercent = Lib.HumanReadablePerc(dataSampled / sampleSize);
			var eta = data_rate < double.Epsilon || dataSampled >= sampleSize ? " done" : " " + Lib.HumanReadableCountdown((sampleSize - dataSampled) / data_rate);

			return !recording
			  ? "<color=red>" + Localizer.Format("#KERBALISM_Generic_STOPPED") + " </color>"
			  : issue.Length == 0 ? "<color=cyan>" + Lib.BuildString(recordedPercent, eta) + "</color>"
			  : Lib.BuildString("<color=yellow>", issue.ToLower(), "</color>");
		}

		public override void Ctrl(bool value)
		{
			if (value)
			{
				// The same experiment must run only once on a vessel
				foreach (var pair in allExperiments)
				{
					var e = pair.Key;
					var p = pair.Value;
					if (e.experiment_id != prefab.experiment_id) continue;
					if (Lib.Proto.GetBool(p, "recording", false))
					{
						Experiment.PostMultipleRunsMessage(title);
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
	}


} // KERBALISM

