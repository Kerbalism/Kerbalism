using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class Sensor : PartModule, ISpecifics, IModuleInfo
{
  // config
  [KSPField] public string type;  // type of sensor
  [KSPField] public string pin;   // pin animation

  // show the readings on RMB ui
  [KSPField(guiActive = true, guiName = "_")] public string Status;

  // float curve used to map reading to pin animation
  FloatCurve fc;

  // animation
  Animator pin_anim;


  public override void OnStart(StartState state)
  {
    // update ui
    switch(type)
    {
      case "temperature": Fields["Status"].guiName = "Temperature"; break;
      case "radiation":   Fields["Status"].guiName = "Radiation";   break;
      case "solar_flux":  Fields["Status"].guiName = "Solar flux";  break;
      case "albedo_flux": Fields["Status"].guiName = "Albedo flux"; break;
      case "body_flux":   Fields["Status"].guiName = "Body flux";   break;
    }

    // if a pin animation is used, generate float curve
    if (pin.Length > 0)
    {
      switch(type)
      {
        case "temperature":
          fc = new FloatCurve();
          fc.Add(0.0f, 0.0f);
          fc.Add(273.15f, 0.33f);
          fc.Add(500.0f, 0.66f);
          fc.Add(2500.0f, 1.0f);
          break;

        case "radiation":
          fc = new FloatCurve();
          fc.Add(0.0f, 0.0f);
          fc.Add(0.005f, 0.33f);
          fc.Add(1.0f, 0.66f);
          fc.Add(11.0f, 1.0f);
          break;

        case "solar_flux":
          fc = new FloatCurve();
          fc.Add(0.0f, 0.0f);
          fc.Add(2000.0f, 1.0f);
          break;

        case "albedo_flux":
          fc = new FloatCurve();
          fc.Add(0.0f, 0.0f);
          fc.Add(1000.0f, 1.0f);
          break;

        case "body_flux":
          fc.Add(0.0f, 0.0f);
          fc.Add(500.0f, 1.0f);
          break;
      }
    }

    // create animator
    pin_anim = new Animator(part, pin);
  }


  public void Update()
  {
    // in flight
    if (Lib.IsFlight())
    {
      // get info from cache
      vessel_info vi = Cache.VesselInfo(vessel);

      // do nothing if vessel is invalid
      if (!vi.is_valid) return;

      // update ui
      switch(type)
      {
        case "temperature": Status = Lib.HumanReadableTemp(vi.temperature); break;
        case "radiation":   Status = vi.radiation > double.Epsilon ? Lib.HumanReadableRadiation(vi.radiation) : "nominal"; break;
        case "solar_flux":  Status = Lib.HumanReadableFlux(vi.solar_flux);  break;
        case "albedo_flux": Status = Lib.HumanReadableFlux(vi.albedo_flux); break;
        case "body_flux":   Status = Lib.HumanReadableFlux(vi.body_flux);   break;
      }

      // if there is a pin animation
      if (pin.Length > 0)
      {
        // get reading
        double reading = 0.0f;;
        switch(type)
        {
          case "temperature": reading = vi.temperature;         break;
          case "radiation":   reading = vi.radiation * 3600.0;  break;
          case "solar_flux":  reading = vi.solar_flux;          break;
          case "albedo_flux": reading = vi.albedo_flux;         break;
          case "body_flux":   reading = vi.body_flux;           break;
        }

        // still-play animation
        pin_anim.still(Lib.Clamp(fc.Evaluate((float)reading), 0.0f, 1.0f));
      }
    }
  }


  // part tooltip
  public override string GetInfo()
  {
    return Specs().info(Lib.BuildString("Measure <b>", type.Replace('_', ' '), "</b>"));
  }


  // specifics support
  public Specifics Specs()
  {
    return new Specifics();
  }


  // module info support
  public string GetModuleTitle() { return Lib.BuildString(Lib.UppercaseFirst(type.Replace('_', ' ')), " sensor"); }
  public string GetPrimaryField() { return string.Empty; }
  public Callback<Rect> GetDrawModulePanelCallback() { return null; }
}


} // KERBALISM

