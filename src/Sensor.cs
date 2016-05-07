// ===================================================================================================================
// an alternative to the stock ModuleEnviroSensor that show our environmental readings
// ===================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public class Sensor : PartModule
{
  // type of sensor
  [KSPField] public string type;

  // show the readings on RMB ui
  [KSPField(guiActive = true, guiName = "Sensor")] public string Status;

  // pseudo-ctor
  public override void OnStart(StartState state)
  {
    switch(type)
    {
      case "temperature": Fields["Status"].guiName = "Temperature"; break;
      case "radiation": Fields["Status"].guiName = "Radiation"; break;
    }
  }

  // called every frame
  public void Update()
  {
    if (HighLogic.LoadedSceneIsEditor) return;
    vessel_info vi = Cache.VesselInfo(this.vessel);
    switch(type)
    {
      case "temperature": Status = Lib.HumanReadableTemp(vi.temperature); break;
      case "radiation": Status = vi.env_radiation > double.Epsilon ? Lib.HumanReadableRadiationRate(vi.env_radiation) : "nominal"; break;
    }
  }
}


} // KERBALISM