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

  // store pin animator
  Animation pinanim = null;

  // float curve used to map radiation to pin animation
  FloatCurve pinfc = null;

  // pseudo-ctor
  public override void OnStart(StartState state)
  {
    switch(type)
    {
      case "temperature": Fields["Status"].guiName = "Temperature"; break;
      case "radiation": Fields["Status"].guiName = "Radiation"; break;
    }


    if (Lib.SceneIsGame() && type == "radiation")
    {
      Animation[] anim = this.part.FindModelAnimators("pinanim");
      if (anim.Length > 0)
      {
        pinanim = anim[0];
        pinanim["pinanim"].normalizedTime = 0.0f;
        pinanim["pinanim"].speed = 0.00001f;
        pinanim.Play();

        pinfc = new FloatCurve();
        pinfc.Add(0.0f, 0.0f);
        pinfc.Add((float)Settings.CosmicRadiation, 0.33f);
        pinfc.Add((float)Settings.StormRadiation, 0.66f);
        pinfc.Add((float)Settings.BeltRadiation, 1.0f);
      }
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

    if (pinanim != null && type == "radiation")
    {
      pinanim["pinanim"].normalizedTime = pinfc.Evaluate((float)vi.env_radiation);
    }
  }
}


} // KERBALISM