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
			this.exp = exp;
			this.exp_name = Lib.SpacesOnCaps(ResearchAndDevelopment.GetExperiment(exp.experiment_id).experimentTitle).ToLower().Replace("e v a", "eva");
		}

		public override string Name()
		{
			return exp_name;
		}

		public override uint Part()
		{
			return exp.part.flightID;
		}

		public override string Info()
		{
			return !exp.recording
			  ? "<color=red>" + Localizer.Format("#KERBALISM_Generic_DISABLED") + " </color>"
			  : exp.issue.Length == 0
			  ? "<color=cyan>" + Localizer.Format("#KERBALISM_Generic_RECORDING") + "</color>"
			  : Lib.BuildString("<color=yellow>", exp.issue.ToLower(), "</color>");
		}

		public override void Ctrl(bool value)
		{
			if (value != exp.recording) exp.Toggle();
		}

		public override void Toggle()
		{
			Ctrl(!exp.recording);
		}

		private Experiment exp;
		private readonly string exp_name;
	}


	public sealed class ProtoExperimentDevice : Device
	{
		public ProtoExperimentDevice(ProtoPartModuleSnapshot exp, Experiment prefab, uint part_id)
		{
			this.exp = exp;
			this.prefab = prefab;
			this.part_id = part_id;
			this.exp_name = Lib.SpacesOnCaps(ResearchAndDevelopment.GetExperiment(prefab.experiment_id).experimentTitle).ToLower().Replace("e v a", "eva");
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
			bool recording = Lib.Proto.GetBool(exp, "recording");
			string issue = Lib.Proto.GetString(exp, "issue");
			return !recording
			  ? "<color=red>" + Localizer.Format("#KERBALISM_Generic_STOPPED") + " </color>"
			  : issue.Length == 0
			  ? "<color=cyan>" + Localizer.Format("#KERBALISM_Generic_RECORDING") + "</color>"
			  : Lib.BuildString("<color=yellow>", issue.ToLower(), "</color>");
		}

		public override void Ctrl(bool value)
		{
			Lib.Proto.Set(exp, "recording", value);
		}

		public override void Toggle()
		{
			Ctrl(!Lib.Proto.GetBool(exp, "recording"));
		}

		private readonly ProtoPartModuleSnapshot exp;
		private readonly Experiment prefab;
		private readonly uint part_id;
		private readonly string exp_name;
	}


} // KERBALISM

