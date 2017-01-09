using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class ExperimentDevice : Device
{
  public ExperimentDevice(Experiment exp)
  {
    this.exp = exp;
    this.exp_name = Lib.SpacesOnCaps(ResearchAndDevelopment.GetExperiment(exp.experiment).experimentTitle).ToLower().Replace("e v a", "eva");
  }

  public override string name()
  {
    return exp_name;
  }

  public override uint part()
  {
    return exp.part.flightID;
  }

  public override string info()
  {
    return !exp.recording
      ? "<color=red>stopped</color>"
      : exp.issue.Length == 0
      ? "<color=cyan>recording</color>"
      : Lib.BuildString("<color=yellow>", exp.issue, "</color>");
  }

  public override void ctrl(bool value)
  {
    if (value != exp.recording) exp.Toggle();
  }

  public override void toggle()
  {
    ctrl(!exp.recording);
  }

  Experiment exp;
  string exp_name;
}


public sealed class ProtoExperimentDevice : Device
{
  public ProtoExperimentDevice(ProtoPartModuleSnapshot exp, Experiment prefab, uint part_id)
  {
    this.exp = exp;
    this.prefab = prefab;
    this.part_id = part_id;
    this.exp_name = Lib.SpacesOnCaps(ResearchAndDevelopment.GetExperiment(prefab.experiment).experimentTitle).ToLower().Replace("e v a", "eva");
  }

  public override string name()
  {
    return exp_name;
  }

  public override uint part()
  {
    return part_id;
  }

  public override string info()
  {
    bool recording = Lib.Proto.GetBool(exp, "recording");
    string issue = Lib.Proto.GetString(exp, "issue");
    return !recording
      ? "<color=red>stopped</color>"
      : issue.Length == 0
      ? "<color=cyan>recording</color>"
      : Lib.BuildString("<color=yellow>", issue, "</color>");
  }

  public override void ctrl(bool value)
  {
    Lib.Proto.Set(exp, "recording", value);
  }

  public override void toggle()
  {
    ctrl(!Lib.Proto.GetBool(exp, "recording"));
  }

  ProtoPartModuleSnapshot exp;
  Experiment prefab;
  uint part_id;
  string exp_name;
}


} // KERBALISM

