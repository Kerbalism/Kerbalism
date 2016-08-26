// ====================================================================================================================
// solar panel device
// ====================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class PanelDevice : Device
{
  public PanelDevice(ModuleDeployableSolarPanel panel)
  {
    this.panel = panel;
  }

  public override string info()
  {
    if (!panel.sunTracking) return "<color=green>fixed</color>";
    switch(panel.panelState)
    {
      case ModuleDeployableSolarPanel.panelStates.EXTENDED: return "<color=green>extended</color>";
      case ModuleDeployableSolarPanel.panelStates.RETRACTED: return "<color=red>retracted</color>";
      case ModuleDeployableSolarPanel.panelStates.BROKEN: return "<color=red>broken</color>";
      case ModuleDeployableSolarPanel.panelStates.EXTENDING: return "extending";
      case ModuleDeployableSolarPanel.panelStates.RETRACTING: return "retracting";
    }
    return "unknown";
  }

  public override void ctrl(double value)
  {
    if (!panel.sunTracking) return;
    if (value <= double.Epsilon && !panel.retractable) return;
    if (panel.panelState == ModuleDeployableSolarPanel.panelStates.BROKEN) return;
    if (value > double.Epsilon) panel.Extend();
    else panel.Retract();
  }

  ModuleDeployableSolarPanel panel;
}


public sealed class ProtoPanelDevice : Device
{
  public ProtoPanelDevice(ProtoPartModuleSnapshot panel, ModuleDeployableSolarPanel prefab)
  {
    this.panel = panel;
    this.prefab = prefab;
  }

  public override string info()
  {
    if (!prefab.sunTracking) return "<color=green>fixed</color>";
    string state = Lib.Proto.GetString(panel, "stateString");
    switch(state)
    {
      case "EXTENDED": return "<color=green>extended</color>";
      case "RETRACTED": return "<color=red>retracted</color>";
    }
    return "unknown";
  }

  public override void ctrl(double value)
  {
    if (!prefab.sunTracking) return;
    if (value <= double.Epsilon && !prefab.retractable) return;
    if (Lib.Proto.GetString(panel, "stateString") == "BROKEN") return;
    Lib.Proto.Set(panel, "stateString", value > double.Epsilon ? "EXTENDED" : "RETRACTED");
  }

  ProtoPartModuleSnapshot panel;
  ModuleDeployableSolarPanel prefab;
}


} // KERBALISM