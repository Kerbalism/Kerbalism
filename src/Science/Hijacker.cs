using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.UI.Screens.Flight.Dialogs;


namespace KERBALISM {


// Remove the data from experiments (and set them inoperable) as soon as the
// science dialog is opened, and store the data in the vessel drive.
// This method support any module that set an appropriate OnDiscardData() callback
// when opening the science dialog, this include stock science experiments and others.
// Hiding the science dialog can be used by who doesn't want it.
public sealed class MiniHijacker : MonoBehaviour
{
  void Start()
  {
    // get dialog
    dialog = gameObject.GetComponentInParent<ExperimentsResultDialog>();
    if (dialog == null) { Destroy(gameObject); return; }

    // prevent rendering
    dialog.gameObject.SetActive(false);

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

  ExperimentsResultDialog dialog;
}


// Manipulate science dialog callbacks to remove the data from the experiment
// (rendering it inoperable) and store it in the vessel drive. The same data
// capture method as in MiniHijacker is used, but the science dialog is not hidden.
// Any event closing the dialog (like going on eva, or recovering) will act as
// if the 'keep' button was pressed for each page.
public sealed class Hijacker : MonoBehaviour
{
  void Start()
  {
    dialog = gameObject.GetComponentInParent<ExperimentsResultDialog>();
    if (dialog == null) { Destroy(gameObject); return; }
  }

  void Update()
  {
    var page = dialog.currentPage;
    page.OnKeepData = (ScienceData data) => hijack(data, false);
    page.OnTransmitData = (ScienceData data) => hijack(data, true);
    page.showTransmitWarning = false; //< mom's spaghetti
  }

  void hijack(ScienceData data, bool send)
  {
    // shortcut
    ExperimentResultDialogPage page = dialog.currentPage;

    // collect and deduce all data necessary just once
    MetaData meta = new MetaData(data, page.host);

    // hijack the dialog
    if (!meta.is_rerunnable)
    {
      popup = Lib.Popup
      (
        "Warning!",
        "Recording the data will render this module inoperable.\n\nRestoring functionality will require a scientist.",
        new DialogGUIButton("Record data", () => record(meta, data, send)),
        new DialogGUIButton("Discard data", () => dismiss(data))
      );
    }
    else
    {
      record(meta, data, send);
    }
  }


  void record(MetaData meta, ScienceData data, bool send)
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
    dismiss(data);

    // inform the user
    Message.Post
    (
      Lib.BuildString("<b>", Science.experiment(data.subjectID).fullname, "</b> recorded"),
      !meta.is_rerunnable ? "The experiment is now inoperable, resetting will require a <b>Scientist</b>" : string.Empty
    );
  }


  void dismiss(ScienceData data)
  {
    // shortcut
    ExperimentResultDialogPage page = dialog.currentPage;

    // dump the data
    page.OnDiscardData(data);

    // close the confirm popup, if it is open
    if (popup != null)
    {
      popup.Dismiss();
      popup = null;
    }
  }

  ExperimentsResultDialog dialog;
  PopupDialog popup;
}


public sealed class MetaData
{
  public MetaData(ScienceData data, Part host)
  {
    // find the part containing the data
    part = host;

    // get the vessel
    vessel = part.vessel;

    // get the container module storing the data
    container = Science.container(part, Science.experiment(data.subjectID).id);

    // get the stock experiment module storing the data (if that's the case)
    experiment = container != null ? container as ModuleScienceExperiment : null;

    // determine if this is a sample (non-transmissible)
    // - if this is a third-party data container/experiment module, we assume it is transmissible
    // - stock experiment modules are considered sample if xmit scalar is below a threshold instead
    is_sample = experiment != null && experiment.xmitDataScalar < 0.666f;

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


} // KERBALISM

