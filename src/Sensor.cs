// ===================================================================================================================
// an alternative to the stock ModuleEnviroSensor that show our environmental readings
// ===================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class Sensor : PartModule
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
      case "solar_flux": Fields["Status"].guiName = "Solar flux"; break;
      case "albedo_flux": Fields["Status"].guiName = "Albedo flux"; break;
      case "body_flux": Fields["Status"].guiName = "Body flux"; break;
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
        pinfc.Add(0.005f, 0.33f);
        pinfc.Add(1.0f, 0.66f);
        pinfc.Add(11.0f, 1.0f);
      }
    }
  }

  // called every frame
  public void Update()
  {
    if (HighLogic.LoadedSceneIsEditor) return;

    // get info from cache
    vessel_info vi = Cache.VesselInfo(this.vessel);

    // do nothing if vessel is invalid
    if (!vi.is_valid) return;

    // set reading
    switch(type)
    {
      case "temperature": Status = Lib.HumanReadableTemp(vi.temperature); break;
      case "radiation": Status = vi.radiation > double.Epsilon ? Lib.HumanReadableRadiationRate(vi.radiation) : "nominal"; break;
      case "solar_flux": Status = Lib.HumanReadableFlux(vi.solar_flux); break;
      case "albedo_flux": Status = Lib.HumanReadableFlux(vi.albedo_flux); break;
      case "body_flux": Status = Lib.HumanReadableFlux(vi.body_flux); break;
    }

    // manage pin animation on geiger counter
    if (pinanim != null && type == "radiation")
    {
      pinanim["pinanim"].normalizedTime = Lib.Clamp(pinfc.Evaluate((float)(vi.radiation * 3600.0)), 0.0f, 1.0f);
      pinanim.Play();
    }
  }
}


} // KERBALISM