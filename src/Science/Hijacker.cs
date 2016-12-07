using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.UI.Screens.Flight.Dialogs;


namespace KERBALISM {


public static class Hijacker
{
  // hijack the science dialog
  public static void update()
  {
    // do nothing if science system is disabled
    if (!Features.Science) return;

    var dialog = ExperimentsResultDialog.Instance;
    if (dialog != null)
    {
      var page = dialog.currentPage;
      page.OnKeepData = (ScienceData data) => hijack(dialog, page, data, false);
      page.OnTransmitData = (ScienceData data) => hijack(dialog, page, data, true);
      page.showTransmitWarning = false; //< mom's spaghetti
    }
  }


  static void hijack(ExperimentsResultDialog dialog, ExperimentResultDialogPage page, ScienceData data, bool send)
  {
    // get the right experiment module
    // - this support parts with multiple experiment modules, like eva kerbals
    string experiment_id = Lib.Tokenize(data.subjectID, '@')[0];
    var exp = FlightGlobals.FindPartByID(data.container)
              .FindModulesImplementing<ModuleScienceExperiment>()
              .Find(k => k.experimentID == experiment_id);
    if (exp == null) throw new Exception("can't find the experiment module during data hijacking");

    // hijack the dialog
    if (!exp.rerunnable)
    {
      Lib.Popup
      (
        "Warning!",
        "Recording the data will render this module inoperable.\n\nRestoring functionality will require a scientist.",
        new DialogGUIButton("Record data", () => record(dialog, page, exp, data, send)),
        new DialogGUIButton("Discard data", () => dismiss(dialog, page, data))
      );
    }
    else
    {
      record(dialog, page, exp, data, send);
    }
  }


  static void record(ExperimentsResultDialog dialog, ExperimentResultDialogPage page, ModuleScienceExperiment exp, ScienceData data, bool send)
  {
    // get amount of data
    double amount = data.dataAmount;

    // determine if the data is a file or a sample
    bool is_sample = exp.xmitDataScalar <= float.Epsilon;

    // if amount is zero, warn the user and do nothing else
    if (amount <= double.Epsilon)
    {
      Message.Post("There is no more useful data here");
      return;
    }

    // if this is a sample and we are trying to send it, warn the user and do nothing else
    if (is_sample && send)
    {
      Message.Post("We can't transmit a sample", "Need to be recovered, or analyzed in a lab");
      return;
    }
    
    // get the max data size that can be collected about this experiment situation   
    double max_amount = exp.experiment.scienceCap * exp.experiment.dataScale;

    // record data in the drive
    Drive drive = DB.Vessel(exp.vessel).drive;
    if (!is_sample)
    {
      drive.record_file(data.subjectID, amount, max_amount);
    }
    else
    {
      drive.record_sample(data.subjectID, amount, max_amount);
    }

    // flag for sending if specified
    if (!is_sample && send) drive.send(data.subjectID, true);

    // render experiment inoperable if necessary
    if (!exp.rerunnable) exp.SetInoperable();

    // dismiss the dialog and popups
    dismiss(dialog, page, data);

    // inform the user
    Message.Post(!is_sample ? "Data has been recorded" : "Sample has been acquired");
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

    // we need to force-close all popups, who knows why
    PopupDialog.ClearPopUps();
  }
}


} // KERBALISM

