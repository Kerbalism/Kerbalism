// ===================================================================================================================
// add range and signal mechanics to the data transmitter
// ===================================================================================================================



using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class Antenna : ModuleDataTransmitter, IScienceDataTransmitter
{
  // configuration
  [KSPField] public string scope;                           // descriptive scope of the antenna (orbit, home, near, far)
  [KSPField] public double relay_cost;                      // ec consumption rate per-second for relaying data
  [KSPField] public double min_transmission_cost;           // transmission cost per-packet at 0 range
  [KSPField] public double max_transmission_cost;           // transmission cost per-packet at max range

  // persistence
  [KSPField(isPersistant = true)] public bool relay;        // specify if this antenna will relay link to other vessels

  // data
  public bool can_transmit;                                 // enable or disable data transmission
  public double penalty = 1.0;                              // damage penalty applied to range


  // rmb ui status
  [KSPField(guiActive = true, guiName = "Relay", guiActiveEditor = true)] public string RelayStatus;
  [KSPField(guiActive = true, guiName = "Range", guiActiveEditor = true)] public string RangeStatus;
  [KSPField(guiActive = true, guiName = "Rate")] public string CostStatus;


  // rmb enable relay
  [KSPEvent(guiActive = true, guiName = "Enable Relay", active = false)]
  public void ActivateRelayEvent()
  {
    Events["ActivateRelayEvent"].active = false;
    Events["DeactivateRelayEvent"].active = true;
    relay = true;
  }


  // rmb disable relay
  [KSPEvent(guiActive = true, guiName = "Disable Relay", active = false)]
  public void DeactivateRelayEvent()
  {
    Events["ActivateRelayEvent"].active = true;
    Events["DeactivateRelayEvent"].active = false;
    relay = false;
  }


  // editor toggle relay
  [KSPEvent(guiActiveEditor = true, guiName = "Toggle Relay", active = true)]
  public void ToggleRelayInEditorEvent()
  {
    relay = !relay;
  }


  // editor/r&d info
  public override string GetInfo()
  {
    return Lib.BuildString
    (
      "Send data and provide a link to the space center and to other vessels.\n\n",
      "<color=#99FF00>Costs:</color>\n",
      " - Transmission (min): <b>", min_transmission_cost.ToString("F1"), " EC/Mbit</b>\n",
      " - Transmission (max): <b>", max_transmission_cost.ToString("F1"), " EC/Mbit</b>\n",
      " - Relay: <b>", relay_cost.ToString("F2"), " EC/s</b>"
    );
  }


  // pseudo-ctor
  public override void OnStart(StartState state)
  {
    // call base class pseudo-ctor
    base.OnStart(state);

    // normalize packet interval/size, force EC as required resource
    // note: done here to simplify the addition of new antenna parts
    this.packetInterval = 1.0f;
    this.packetSize = 1.0f;
    this.requiredResource = "ElectricCharge";

    // enable/disable rmb ui events based on initial relay state as per .cfg files
    Events["ActivateRelayEvent"].active = !relay;
    Events["DeactivateRelayEvent"].active = relay;
  }


  public void Update()
  {
    // get range
    double range = Signal.Range(scope, penalty, Signal.ECC());

    // update rmb ui status
    RangeStatus = Lib.HumanReadableRange(range);
    RelayStatus = relay ? "Active" : "Disabled";

    // when in flight
    if (HighLogic.LoadedSceneIsFlight)
    {
      // remove incomplete data toggle
      Events["TransmitIncompleteToggle"].active = false;

      // get link state
      link_data link = Cache.VesselInfo(vessel).link;

      // enable/disable science transmission
      can_transmit = link.linked;

      // determine currect packet cost
      // note: we set it to max float if out of range, to indirectly specify antenna score
      if (link.distance <= range)
      {
        this.packetResourceCost = (float)(min_transmission_cost + (max_transmission_cost - min_transmission_cost) * link.distance / range);
        CostStatus = Lib.BuildString(this.packetResourceCost.ToString("F2"), " EC/Mbit");
      }
      else
      {
        this.packetResourceCost = float.MaxValue;
        CostStatus = "";
      }
    }
  }


  void IScienceDataTransmitter.TransmitData(List<ScienceData> dataQueue)
  {
    // if there is no signal
    if (!can_transmit)
    {
      // show a message to the user
      Message.Post(Severity.warning, "No signal", "We can't send the data");

      // return data to the containers
      ReturnData(dataQueue);

      // do not transmit the data
      return;
    }

    // calculate total ec cost of transmission
    double total_amount = 0.0;
    foreach(ScienceData sd in dataQueue) total_amount += sd.dataAmount;
    double total_cost = total_amount * this.packetResourceCost;

    // if there is no EC to transmit the data
    // note: comparing against amount in previous simulation step
    if (total_cost > ResourceCache.Info(vessel, "ElectricCharge").amount)
    {
      // show a message to the user
      Message.Post(Severity.warning, Lib.BuildString("Not enough power, <b>", total_cost.ToString("F0"), " ElectricCharge</b> required"), "We can't send the data");

      // return data to the containers
      ReturnData(dataQueue);

      // do not transmit the data
      return;
    }

    // transmit the data
    ModuleDataTransmitter transmitter = (ModuleDataTransmitter)this;
    transmitter.TransmitData(dataQueue);
  }


  void ReturnData(List<ScienceData> dataQueue)
  {
    // note: returning data to its original container/experiment has multiple problems:
    // - experiment.ReturnData() seems to do nothing
    // - a part can have multiple container/experiments and is impossible to discern the right one from the part id only

    // return data to the first available container
    // note: this work only if there is a data container on the vessel, excluding experiments
    //       for this reason, a data container has been added to probe cores using MM
    //       transmitting is only possible if vessel can be controlled, therefore a pod or probe core
    //       must be present, and so in this way we are sure there is a data container on the vessel
    foreach(ScienceData data in dataQueue)
    {
      foreach(ModuleScienceContainer container in vessel.FindPartModulesImplementing<ModuleScienceContainer>())
      {
        // add the data to the container
        container.ReturnData(data);

        // if, for some reasons, it wasn't possible to add the data, try the next container
        // note: this also deal with multiple versions of same data in the entire vessel
        if (!container.HasData(data)) continue;

        // data was added, process the next data
        break;
      }
    }
  }
}


} // KERBALISM