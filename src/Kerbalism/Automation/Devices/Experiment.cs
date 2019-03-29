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
			this.exp_name = exp.sample_mass < float.Epsilon ? "sensor" : "experiment"
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
			var exp = ResearchAndDevelopment.GetExperiment(experiment.experiment_id);
			var sampleSize = (exp.scienceCap * exp.dataScale);
			var recordedPercent = Lib.HumanReadablePerc(experiment.dataSampled / sampleSize);
			var eta = experiment.data_rate < double.Epsilon || experiment.dataSampled >= sampleSize ? " done" : " T-" + Lib.HumanReadableDuration((sampleSize - experiment.dataSampled) / experiment.data_rate);

			return !experiment.recording
			  ? "<color=red>" + Localizer.Format("#KERBALISM_Generic_DISABLED") + " </color>"
			  : experiment.issue.Length == 0 ? "<color=cyan>" + Lib.BuildString(Localizer.Format("#KERBALISM_Generic_RECORDING"), " ", recordedPercent, eta) + "</color>"
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
		public ProtoExperimentDevice(ProtoPartModuleSnapshot proto, Experiment prefab, uint part_id)
		{
			this.proto = proto;
			this.prefab = prefab;
			this.part_id = part_id;
			this.exp_name = prefab.sample_mass < float.Epsilon ? "sensor" : "experiment"
				+ ": " + Lib.SpacesOnCaps(ResearchAndDevelopment.GetExperiment(prefab.experiment_id).experimentTitle).ToLower().Replace("e v a", "eva");
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
			string issue = Lib.Proto.GetString(proto, "issue");
			double dataSampled = Lib.Proto.GetDouble(proto, "dataSampled");
			double data_rate = Lib.Proto.GetDouble(proto, "data_rate");

			var exp = ResearchAndDevelopment.GetExperiment(prefab.experiment_id);
			var sampleSize = (exp.scienceCap * exp.dataScale);
			var recordedPercent = Lib.HumanReadablePerc(dataSampled / sampleSize);
			var eta = data_rate < double.Epsilon || dataSampled >= sampleSize ? " done" : " T-" + Lib.HumanReadableDuration((sampleSize - dataSampled) / data_rate);

			return !recording
			  ? "<color=red>" + Localizer.Format("#KERBALISM_Generic_STOPPED") + " </color>"
			  : issue.Length == 0 ? "<color=cyan>" + Lib.BuildString(Localizer.Format("#KERBALISM_Generic_RECORDING"), " ", recordedPercent, eta) + "</color>"
			  : Lib.BuildString("<color=yellow>", issue.ToLower(), "</color>");
		}

		public override void Ctrl(bool value)
		{
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
	}


} // KERBALISM

