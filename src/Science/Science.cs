using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.UI.Screens;


namespace KERBALISM {


public static class Science
{
  // consume EC for transmission, and transmit science data
  public static void update(Vessel v, vessel_info vi, VesselData vd, vessel_resources resources, double elapsed_s)
  {
    // do nothing if science system is disabled
    if (!Features.Science) return;

    // avoid corner-case when RnD isn't live during scene changes
    // - this avoid losing science if the buffer reach threshold during a scene change
    if (HighLogic.CurrentGame.Mode != Game.Modes.SANDBOX && ResearchAndDevelopment.Instance == null) return;

    // get connection info
    ConnectionInfo conn = vi.connection;

    // consume ec if data is transmitted or relayed
    if (vi.transmitting.Length > 0 || vi.relaying.Length > 0)
    {
      resources.Consume(v, "ElectricCharge", conn.cost * elapsed_s);
    }

    // get filename of data being downloaded
    string filename = vi.transmitting;

    // if some data is being downloaded
    // - avoid cornercase at scene changes
    if (filename.Length > 0 && vd.drive.files.ContainsKey(filename))
    {
      // get file
      File file = vd.drive.files[filename];

      // determine how much data is transmitted
      double transmitted = Math.Min(file.size, conn.rate * elapsed_s);

      // consume data in the file
      file.size -= transmitted;

      // collect the science data
      Science.credit(filename, transmitted, true, v.protoVessel);

      // if file was transmitted completely
      if (file.size <= double.Epsilon)
      {
        // remove the file
        vd.drive.files.Remove(filename);

        // inform the user
        Message.Post
        (
          Lib.BuildString("<color=cyan><b>DATA RECEIVED</b></color>\nTransmission of <b>", Science.experiment_name(filename), "</b> completed"),
          Lib.TextVariant("Our researchers will jump on it right now", "The checksum is correct, data must be valid")
        );
      }
    }
  }

  // return name of file being transmitted from vessel specified
  public static string transmitting(Vessel v, bool linked)
  {
    // never transmitting if science system is disabled
    if (!Features.Science) return string.Empty;

    // not transmitting if unlinked
    if (!linked) return string.Empty;

    // not transmitting if there is no ec left
    if (ResourceCache.Info(v, "ElectricCharge").amount <= double.Epsilon) return string.Empty;

    // get vessel drive
    Drive drive = DB.Vessel(v).drive;

    // get first file flagged for transmission
    foreach(var p in drive.files)
    {
      if (p.Value.send) return p.Key;
    }

    // no file flagged for transmission
    return string.Empty;
  }


  public static double credit(string subject_id, double size, bool transmitted, ProtoVessel pv)
  {
    // there is no science in sandbox mode
    if (HighLogic.CurrentGame.Mode == Game.Modes.SANDBOX) return 0.0;

    // get subject
    ScienceSubject subject = ResearchAndDevelopment.GetSubjectByID(subject_id);

    // get science value
    // - the stock system 'degrade' science value after each credit, we don't
    float R = ResearchAndDevelopment.GetReferenceDataValue((float)size, subject);
    float S = subject.science;
    float C = subject.scienceCap;
    float value = Mathf.Max(Mathf.Min(S + Mathf.Min(R, C), C) - S, 0.0f);

    // credit the science
    subject.science += value;
    subject.scientificValue = ResearchAndDevelopment.GetSubjectValue(subject.science, subject);
    value *= HighLogic.CurrentGame.Parameters.Career.ScienceGainMultiplier;
    ResearchAndDevelopment.Instance.AddScience(value, transmitted ? TransactionReasons.ScienceTransmission : TransactionReasons.VesselRecovery);

    // fire game event
    // - this could be slow or a no-op, depending on the number of listeners
    //   i am only aware of active science collection contracts listening to this
    GameEvents.OnScienceRecieved.Fire((float)size, subject, pv, false);

    // return amount of science credited
    return value;
  }


  // get experiment name, without situation
  public static string experiment_name(string subject_id)
  {
    var experiment = ResearchAndDevelopment.GetExperiment(Lib.Tokenize(subject_id, '@')[0]);
    return experiment.experimentTitle;
  }

  // get experiment situation, without name
  public static string experiment_situation(string subject_id)
  {
    // subject will be null in sandbox, we default to nothing in that case
    if (HighLogic.CurrentGame.Mode == Game.Modes.SANDBOX) return string.Empty;
    string title = ResearchAndDevelopment.GetSubjectByID(subject_id).title;
    int i = title.IndexOf(" from ", StringComparison.OrdinalIgnoreCase) + 6;
    if (i < 6)
    {
      i = title.IndexOf(" while ", StringComparison.OrdinalIgnoreCase) + 7;
      if (i < 7) return string.Empty;
    }
    return title.Substring(i);
  }


  // get experiment full name, inclusive of situation
  public static string experiment_fullname(string subject_id)
  {
    // subject will be null in sandbox, we default to short name in that case
    return HighLogic.CurrentGame.Mode == Game.Modes.SANDBOX
      ? experiment_name(subject_id)
      : ResearchAndDevelopment.GetSubjectByID(subject_id).title;
  }


  // get science points an experiment is worth
  public static double experiment_value(string subject_id, double size)
  {
    // there is no science in sandbox mode
    if (HighLogic.CurrentGame.Mode == Game.Modes.SANDBOX) return 0.0;

    ScienceSubject subject = ResearchAndDevelopment.GetSubjectByID(subject_id);
    return ResearchAndDevelopment.GetScienceValue((float)size, subject)
      * HighLogic.CurrentGame.Parameters.Career.ScienceGainMultiplier;
  }


  // get max data size an experiment can generate
  public static double experiment_size(string subject_id)
  {
    ScienceExperiment exp = ResearchAndDevelopment.GetExperiment(Lib.Tokenize(subject_id, '@')[0]);
    return exp.scienceCap * exp.dataScale;
  }


  // return the container of a particular experiment
  public static IScienceDataContainer experiment_container(Part p, string experiment_id)
  {
    // first try to get a stock experiment module with the right experiment id
    // - this support parts with multiple experiment modules, like eva kerbal
    foreach(ModuleScienceExperiment exp in p.FindModulesImplementing<ModuleScienceExperiment>())
    {
      if (exp.experimentID == experiment_id) return exp;
    }

    // if none was found, default to the first module implementing the science data container interface
    // - this support third-party modules that implement IScienceDataContainer, but don't derive from ModuleScienceExperiment
    return p.FindModuleImplementing<IScienceDataContainer>();
  }


  // [disabled] EXPERIMENTAL
  // return a tagged science subject
  /*public static ScienceSubject TaggedSubject(string experiment, string tag, string title, CelestialBody body)
  {
    var exp = ResearchAndDevelopment.GetExperiment(experiment);
    var subject = ResearchAndDevelopment.GetExperimentSubject
    (
      exp,                            // science experiment definition
      ExperimentSituations.SrfLanded, // placeholder situation
      body,                           // celestial body
      ""                              // no biome
    );
    subject.id = Lib.BuildString(exp.id, "@", body.name, tag);
    subject.title = title;
    subject.scienceCap = exp.scienceCap; //< note: no body/situation multiplier
    return subject;
  }*/


  // [disabled] EXPERIMENTAL
  // add filters for a set of tag-based situations
  // note: call it every frame
  /*public static void FilterResearchArchive(string[] situations)
  {
    // note: cache the controller, because FindObjectOfType is slow as hell
    RDArchivesController ctrl = (UnityEngine.Object.FindObjectOfType(typeof(RDArchivesController)) as RDArchivesController);
    if (ctrl != null && ctrl.dropdownListContainer != null && ctrl.dropdownListContainer.lists != null && ctrl.dropdownListContainer.lists.Length >= 3)
    {
      foreach(string situation in situations)
      {
        // try to get the item
        KSP.UI.UIListItem item;
        items.TryGetValue(situation, out item);

        // if there is no item, or the list was reset
        if (items == null || !ctrl.dropdownListContainer.lists[1].scrollList.Contains(item))
        {
          // get number of matching reports
          int count = ResearchAndDevelopment.GetSubjects().FindAll(k => k.id.Contains(situation)).Count;

          // if there is no report, do nothing
          if (count == 0) return;

          // create and add it
          item = ctrl.dropdownListContainer.lists[1].AddItem(situation, Lib.SpacesOnCaps(situation), count == 1 ? "1 report" : count + " reports");
          items.Remove(situation);
          items.Add(situation, item);
        }
      }
    }
  }
  static Dictionary<string, KSP.UI.UIListItem> items = new Dictionary<string, KSP.UI.UIListItem>();*/
}


} // KERBALISM

