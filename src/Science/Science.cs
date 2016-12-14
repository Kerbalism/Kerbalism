using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public static class Science
{
  // pseudo-ctor
  public static void init()
  {
    experiments = new Dictionary<string, ExperimentInfo>();
  }

  // consume EC for transmission, and transmit science data
  public static void update(Vessel v, vessel_info vi, VesselData vd, vessel_resources resources, double elapsed_s)
  {
    // hard-coded transmission buffer size in Mb
    const double buffer_capacity = 8.0;

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

      // accumulate in the buffer
      file.buff += transmitted;

      // if buffer is full, or file was transmitted completely
      if (file.size <= double.Epsilon || file.buff > buffer_capacity)
      {
        // collect the science data
        Science.credit(filename, file.buff, true, v.protoVessel);

        // reset the buffer
        file.buff = 0.0;
      }

      // if file was transmitted completely
      if (file.size <= double.Epsilon)
      {
        // remove the file
        vd.drive.files.Remove(filename);

        // inform the user
        Message.Post
        (
          Lib.BuildString("<color=cyan><b>DATA RECEIVED</b></color>\nTransmission of <b>", Science.experiment(filename).name, "</b> completed"),
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


  // credit science for the experiment subject specified
  public static double credit(string subject_id, double size, bool transmitted, ProtoVessel pv)
  {
    // get info on the experiment
    ExperimentInfo exp = Science.experiment(subject_id);

    // if there is no subject, we can't possibly credit the science
    // - we are probably in sandbox mode
    // - or the subject id is malformed, and the data is not triggered
    if (exp.subject == null) return 0.0;

    // get science value
    // - the stock system 'degrade' science value after each credit, we don't
    float R = ResearchAndDevelopment.GetReferenceDataValue((float)size, exp.subject);
    float S = exp.subject.science;
    float C = exp.subject.scienceCap;
    float value = Mathf.Max(Mathf.Min(S + Mathf.Min(R, C), C) - S, 0.0f);

    // credit the science
    exp.subject.science += value;
    exp.subject.scientificValue = ResearchAndDevelopment.GetSubjectValue(exp.subject.science, exp.subject);
    value *= HighLogic.CurrentGame.Parameters.Career.ScienceGainMultiplier;
    ResearchAndDevelopment.Instance.AddScience(value, transmitted ? TransactionReasons.ScienceTransmission : TransactionReasons.VesselRecovery);

    // fire game event
    // - this could be slow or a no-op, depending on the number of listeners
    //   in any case, we are buffering the transmitting data and calling this
    //   function only once in a while
    GameEvents.OnScienceRecieved.Fire((float)size, exp.subject, pv, false);

    // return amount of science credited
    return value;
  }


  public static ExperimentInfo experiment(string subject_id)
  {
    ExperimentInfo info;
    if (!experiments.TryGetValue(subject_id, out info))
    {
      info = new ExperimentInfo(subject_id);
      experiments.Add(subject_id, info);
    }
    return info;
  }


  // experiment info cache
  static Dictionary<string, ExperimentInfo> experiments;
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



} // KERBALISM

