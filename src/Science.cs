// ====================================================================================================================
// wip
// ====================================================================================================================

#if false


using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.UI.Screens;


namespace KERBALISM {



    // TODO science test
    /*if (!start && ResearchAndDevelopment.Instance != null)
    {
      start = true;
      var subject = Science.Subject("crewReport", "KuiperBelt", "Crew Report from Kuiper Belt", FlightGlobals.GetHomeBody());
      Science.Receive(subject, 1.0f);
    }
    Science.FilterResearchArchive(new string[]{"KuiperBelt","SomethingElse"});*/



public static class Science
{
  // add filters for a set of tag-based situations
  public static void FilterResearchArchive(string[] situations)
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
          item = ctrl.dropdownListContainer.lists[1].AddItem(situation, situation.AddSpacesOnCaps(), count == 1 ? "1 report" : count + " reports");
          items.Remove(situation);
          items.Add(situation, item);
        }
      }
    }
  }

  // return a tagged science subject
  public static ScienceSubject Subject(string experiment, string tag, string title, CelestialBody body)
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
  }

  public static ScienceData Data(ScienceSubject subject, float amount)
  {
    return new ScienceData
    (
      amount,
      1.0f,       // xmit value
      0.0f,       // lab boost
      subject.id, // subject id string
      ""          // title
    );
  }

  // add science subject data to the stock system
  public static void Receive(ScienceSubject subject, float amount)
  {
    ResearchAndDevelopment.Instance.SubmitScienceData
    (
      amount,   // data amount,
      subject,  // science subject
      1.0f,     // xmit scalar
      null,     // ProtoVessel source
      false     // reverse engineered (from recovered vessel)
    );
  }

  static Dictionary<string, KSP.UI.UIListItem> items = new Dictionary<string, KSP.UI.UIListItem>();

}


} // KERBALISM


#endif