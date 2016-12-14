using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class ExperimentInfo
{
  public ExperimentInfo(string subject_id)
  {
    // get experiment id out of subject id
    int i = subject_id.IndexOf('@');
    id = i > 0 ? subject_id.Substring(0, i) : subject_id;

    // get experiment definition
    // - available even in sandbox
    expdef = ResearchAndDevelopment.GetExperiment(id);

    // get subject handler
    // - not available in sandbox
    subject = ResearchAndDevelopment.GetSubjectByID(subject_id);

    // deduce short name for the subject
    name = expdef != null ? expdef.experimentTitle : Lib.UppercaseFirst(id);

    // deduce full name for the subject
    fullname = subject != null ? subject.title : name;

    // extract situation from full name
    situation = fullname.Replace(name, string.Empty).Replace(" from", string.Empty).Replace(" while", string.Empty);

    // deduce max data amount
    max_amount = expdef != null ? expdef.scienceCap * expdef.dataScale : double.MaxValue;
  }

  public double value(double size)
  {
    return subject != null
      ? ResearchAndDevelopment.GetScienceValue((float)size, subject)
      * HighLogic.CurrentGame.Parameters.Career.ScienceGainMultiplier
      : 0.0;
  }

  public IScienceDataContainer container(Part p)
  {
    // first try to get a stock experiment module with the right experiment id
    // - this support parts with multiple experiment modules, like eva kerbal
    foreach(ModuleScienceExperiment exp in p.FindModulesImplementing<ModuleScienceExperiment>())
    {
      if (exp.experimentID == id) return exp;
    }

    // if none was found, default to the first module implementing the science data container interface
    // - this support third-party modules that implement IScienceDataContainer, but don't derive from ModuleScienceExperiment
    return p.FindModuleImplementing<IScienceDataContainer>();
  }

  public string id;                 // experiment identifier
  public ScienceExperiment expdef;  // experiment definition
  public ScienceSubject subject;    // science subject
  public string name;               // short description of the experiment
  public string fullname;           // full description of the experiment
  public string situation;          // description of the situation
  public double max_amount;         // max data amount for the experiment
}


} // KERBALISM

