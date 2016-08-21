// ===================================================================================================================
// emit radiation at the vessel level
// ===================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class Emitter : PartModule
{
  // config
  [KSPField(isPersistant = true)] public double radiation;  // radiation in rad/s
  [KSPField] public double ec_rate;                         // EC consumption rate per-second (optional)
  [KSPField] public string tooltip = "";                    // short description for part tooltip
  [KSPField] public string animation_name;                  // name of animation to play

  // tweakable
  [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Intensity"),
   UI_FloatRange(minValue=0.0f, maxValue=1.0f, stepIncrement=0.01f)]
  public float intensity = 1.0f;

  // rmb status
  [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Radiation")] public string Status;  // rate of radiation emitted/shielded

  // other data
  bool green_status;

  // pseudo-ctor
  public override void OnStart(StartState state)
  {
    // update RMB ui
    Fields["Status"].guiName = radiation >= 0.0 ? "Radiation" : "Active shielding";

    // disable tweakable without ec consumption
    if (ec_rate <= double.Epsilon)
    {
      Fields["intensity"].guiActive = false;
      Fields["intensity"].guiActiveEditor = false;
    }

    // set animation initial state
    Animation[] anim = this.part.FindModelAnimators(animation_name);
    if (anim.Length > 0)
    {
      if (intensity <= 0.005)
      {
        anim[0][animation_name].normalizedTime = 1.0f;
        anim[0][animation_name].speed = Math.Abs(anim[0][animation_name].speed);
        anim[0].Play(animation_name);
        green_status = false;
      }
      else
      {
        anim[0][animation_name].normalizedTime = 0.0f;
        anim[0][animation_name].speed = -Math.Abs(anim[0][animation_name].speed);
        anim[0].Play(animation_name);
        green_status = true;
      }
    }
  }


  public void Update()
  {
    // set animation
    Animation[] anim = this.part.FindModelAnimators(animation_name);
    if (anim.Length > 0)
    {
      if (intensity < 0.005 && green_status)
      {
        anim[0][animation_name].normalizedTime = 0.0f;
        anim[0][animation_name].speed = Math.Abs(anim[0][animation_name].speed);
        anim[0].Play(animation_name);
        green_status = false;
      }
      if (intensity > 0.005 && !green_status)
      {
        anim[0][animation_name].normalizedTime = 1.0f;
        anim[0][animation_name].speed = -Math.Abs(anim[0][animation_name].speed);
        anim[0].Play(animation_name);
        green_status = true;
      }
    }
  }



  public void FixedUpdate()
  {
    // in any scene: update the RMB ui
    Status = Lib.HumanReadableRadiationRate(Math.Abs(radiation) * intensity);

    // do nothing else in the editor
    if (HighLogic.LoadedSceneIsEditor) return;

    // if there is ec consumption
    if (ec_rate > double.Epsilon)
    {
      // get vessel info from the cache
      vessel_info vi = Cache.VesselInfo(vessel);

      // do nothing if vessel is invalid
      if (!vi.is_valid) return;

      // get resource cache
      resource_info ec = ResourceCache.Info(vessel, "ElectricCharge");

      // get elapsed time
      double elapsed_s = Kerbalism.elapsed_s * vi.time_dilation;

      // if there is enough EC
      // note: comparing against amount in previous simulation step
      if (ec.amount > double.Epsilon)
      {
        // consume EC
        ec.Consume(ec_rate * intensity * elapsed_s);
      }
      // else disable it
      else
      {
        intensity = 0.0f;
      }
    }
  }


  public static void BackgroundUpdate(Vessel vessel, ProtoPartModuleSnapshot m, Emitter emitter, resource_info ec, double elapsed_s)
  {
    // if there is enough EC
    // note: comparing against amount in previous simulation step
    if (ec.amount > double.Epsilon)
    {
      // get intensity
      double intensity = Lib.Proto.GetDouble(m, "intensity");

      // consume EC
      ec.Consume(emitter.ec_rate * intensity * elapsed_s);
    }
    // else disable it
    else
    {
      Lib.Proto.Set(m, "intensity", 0.0);
    }
  }

  // editor/r&d info
  public override string GetInfo()
  {
    return Lib.BuildString
    (
      Lib.Specifics(tooltip),
      Lib.Specifics(ec_rate > double.Epsilon, "ElectricCharge", Lib.HumanReadableRate(ec_rate)),
      Lib.Specifics(true, radiation >= 0.0 ? "Ionizing radiation" : "Active shielding", Lib.HumanReadableRadiationRate(Math.Abs(radiation)))
    );
  }


  // return total radiation emitted in a vessel
  public static double Total(Vessel v)
  {
    double tot = 0.0;
    if (v.loaded)
    {
      foreach(var emitter in v.FindPartModulesImplementing<Emitter>())
      {
        tot += emitter.radiation * emitter.intensity;
      }
    }
    else
    {
      foreach(ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
      {
        foreach(ProtoPartModuleSnapshot m in p.modules)
        {
          if (m.moduleName == "Emitter") tot += Lib.Proto.GetDouble(m, "radiation") * Lib.Proto.GetDouble(m, "intensity");
        }
      }
    }
    return tot;
  }
}


} // KERBALISM