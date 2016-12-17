using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.UI.Screens.Flight.Dialogs;


namespace KERBALISM {


public static class Hijacker
{
  public static void update()
  {
    // do nothing if science system is disabled
    if (!Features.Science) return;

    // get the science dialog
    var dialog = ExperimentsResultDialog.Instance;

    // if it is open
    if (dialog != null)
    {
      // for each page
      // - some mod may collect multiple experiments at once
      while(dialog.pages.Count > 0)
      {
        // get page
        var page = dialog.pages[0];

        // get science data
        ScienceData data = page.pageData;

        // collect and deduce all info necessary
        MetaData meta = new MetaData(data, page.host);

        // record data in the drive
        Drive drive = DB.Vessel(meta.vessel).drive;
        if (!meta.is_sample)
        {
          drive.record_file(data.subjectID, data.dataAmount);
        }
        else
        {
          drive.record_sample(data.subjectID, data.dataAmount);
        }

        // render experiment inoperable if necessary
        if (!meta.is_rerunnable)
        {
          meta.experiment.SetInoperable();
        }

        // dump the data
        page.OnDiscardData(data);

        // inform the user
        Message.Post
        (
          Lib.BuildString("<b>", Science.experiment(data.subjectID).fullname, "</b> recorded"),
          !meta.is_rerunnable ? "The experiment is now inoperable, resetting will require a <b>Scientist</b>" : string.Empty
        );
      }

      // dismiss the dialog
      dialog.Dismiss();
    }
  }


  sealed class MetaData
  {
    public MetaData(ScienceData data, Part host)
    {
      // find the part containing the data
      part = host;

      // get the vessel
      vessel = part.vessel;

      // get the container module storing the data
      container = Science.container(part, Science.experiment(data.subjectID).id);
      if (container == null) throw new Exception("can't find the data container during data hijacking");

      // get the stock experiment module storing the data (if that's the case)
      experiment = container as ModuleScienceExperiment;

      // determine if this is a sample (non-transmissible)
      // - if this is a third-party data container/experiment, we assume it is transmissible
      is_sample = experiment != null && experiment.xmitDataScalar < 1.0f;

      // determine if the container/experiment can collect the data multiple times
      // - if this is a third-party data container/experiment, we assume it can collect multiple times
      is_rerunnable = experiment == null || experiment.rerunnable;
    }

    public Part part;                               // part storing the data
    public Vessel vessel;                           // vessel storing the data
    public IScienceDataContainer container;         // module containing the data
    public ModuleScienceExperiment experiment;      // module containing the data, as a stock experiment module
    public bool is_sample;                          // true if the data can't be transmitted
    public bool is_rerunnable;                      // true if the container/experiment can collect data multiple times
  }
}


// [disabled] the old data hijacker
// This was disabled because it was impossible to assure all the data was in our system, at all time.
// For instance, there was a 'limbo' between the dialog opened and keep/transmit was pressed.
// If anything closed the dialog in that moment, then the data was stuck inside the experiment.
/*public static class Hijacker
{
  public static void update()
  {
    // do nothing if science system is disabled
    if (!Features.Science) return;

    // get the science dialog
    var dialog = ExperimentsResultDialog.Instance;

    // if it is open, hijack it
    if (dialog != null)
    {
      var page = dialog.currentPage;
      page.OnKeepData = (ScienceData data) => hijack(dialog, page, data, false);
      page.OnTransmitData = (ScienceData data) => hijack(dialog, page, data, true);
      page.showTransmitWarning = false; //< mom's spaghetti
    }
    // if it is closed
    else
    {
      // if the confirm popup is still open, close it
      // - this deal with corner cases when something else close the science dialog
      if (popup != null) popup.Dismiss();
    }
  }


  static void hijack(ExperimentsResultDialog dialog, ExperimentResultDialogPage page, ScienceData data, bool send)
  {
    // collect and deduce all data necessary just once
    MetaData meta = new MetaData(data, page.host);

    // hijack the dialog
    if (!meta.is_rerunnable)
    {
      popup = Lib.Popup
      (
        "Warning!",
        "Recording the data will render this module inoperable.\n\nRestoring functionality will require a scientist.",
        new DialogGUIButton("Record data", () => record(dialog, page, meta, data, send)),
        new DialogGUIButton("Discard data", () => dismiss(dialog, page, data))
      );
    }
    else
    {
      record(dialog, page, meta, data, send);
    }
  }


  static void record(ExperimentsResultDialog dialog, ExperimentResultDialogPage page, MetaData meta, ScienceData data, bool send)
  {
    // if amount is zero, warn the user and do nothing else
    if (data.dataAmount <= double.Epsilon)
    {
      Message.Post("There is no more useful data here");
      return;
    }

    // if this is a sample and we are trying to send it, warn the user and do nothing else
    if (meta.is_sample && send)
    {
      Message.Post("We can't transmit a sample", "Need to be recovered, or analyzed in a lab");
      return;
    }

    // record data in the drive
    Drive drive = DB.Vessel(meta.vessel).drive;
    if (!meta.is_sample)
    {
      drive.record_file(data.subjectID, data.dataAmount);
    }
    else
    {
      drive.record_sample(data.subjectID, data.dataAmount);
    }

    // flag for sending if specified
    if (!meta.is_sample && send) drive.send(data.subjectID, true);

    // render experiment inoperable if necessary
    if (!meta.is_rerunnable) meta.experiment.SetInoperable();

    // dismiss the dialog and popups
    dismiss(dialog, page, data);

    // inform the user
    Message.Post(!meta.is_sample ? "Data has been recorded" : "Sample has been acquired");
  }


  static void dismiss(ExperimentsResultDialog dialog, ExperimentResultDialogPage page, ScienceData data)
  {
    // dump the data
    page.OnDiscardData(data);

    // close this page
    dialog.pages.Remove(page);

    // if there are other pages
    if (dialog.pages.Count > 0)
    {
      // move to next page
      ExperimentsResultDialog.DisplayResult(dialog.pages[0]);
    }
    // if this was the last one
    else
    {
      // close the dialog
      dialog.Dismiss();
    }

    // close the confirm popup, if it is open
    if (popup != null)
    {
      popup.Dismiss();
      popup = null;
    }
  }

  // popup dialog handler
  static PopupDialog popup;
}*/


} // KERBALISM

