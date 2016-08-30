// ====================================================================================================================
// functions that deal with EVA kerbals
// ====================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public static class EVA
{
  public static void update(Vessel v)
  {
    // get kerbal data from db
    kerbal_data kd = KerbalData(v);

    // get KerbalEVA module
    KerbalEVA kerbal = v.FindPartModulesImplementing<KerbalEVA>()[0];

    // show/hide helmet, play nice with KIS
    if (!Kerbalism.detected_mods.KIS)
    {
      SetHelmet(kerbal, kd.has_helmet);
    }
    // synchronize has_helmet state with KIS (for the headlights)
    else
    {
      kd.has_helmet = HasHelmet(kerbal);
    }

    // get resource handler
    resource_info ec = ResourceCache.Info(v, "ElectricCharge");

    // consume EC for the headlamp
    if (kd.has_helmet && kerbal.lampOn) ec.Consume(Settings.HeadlightCost * Kerbalism.elapsed_s); //< ignore time dilation

    // force the headlamp lights on/off depending on ec amount left and if it has an helmet
    // synchronize helmet flares with headlamp state
    // support case when there is no ec rule (or no profile at all)
    bool b = kd.has_helmet && kerbal.lampOn && (ec.amount > double.Epsilon || ec.capacity <= double.Epsilon);
    SetHeadlamp(kerbal, b);
    SetFlares(kerbal, b);

    // if dead
    if (kd.eva_dead)
    {
      // enforce freezed state
      SetFreezed(kerbal);

      // remove plant flag action
      kerbal.flagItems = 0;

      // remove experiment actions (game engine keeps readding them)
      RemoveExperiments(kerbal);
    }
  }


  public static kerbal_data KerbalData(Vessel v)
  {
    var crew = v.loaded ? v.GetVesselCrew() : v.protoVessel.GetVesselCrew();
    return DB.KerbalData(crew[0].name);
  }


  // set headlamp on or off
  public static void SetHeadlamp(KerbalEVA kerbal, bool b)
  {
    kerbal.headLamp.GetComponent<Light>().intensity = b ? 1.0f : 0.0f;
  }


  // set helmet of a kerbal
  public static void SetHelmet(KerbalEVA kerbal, bool b)
  {
    // do not touch the helmet if the user has KIS installed
    if (Kerbalism.detected_mods.KIS) return;

    foreach (var comp in kerbal.GetComponentsInChildren<UnityEngine.Renderer>())
    {
      if (comp.name == "helmet" || comp.name == "visor" || comp.name == "flare1" || comp.name == "flare2")
      {
        comp.enabled = b;
      }
    }
  }

  // set helmet flares of a kerbal
  public static void SetFlares(KerbalEVA kerbal, bool b)
  {
    foreach(var comp in kerbal.GetComponentsInChildren<UnityEngine.Renderer>())
    {
      if (comp.name == "flare1" || comp.name == "flare2")
      {
        comp.enabled = b;
      }
    }
  }


  // return true if the helmet is visible
  public static bool HasHelmet(KerbalEVA kerbal)
  {
    foreach(var comp in kerbal.GetComponentsInChildren<UnityEngine.Renderer>())
    {
      if (comp.name == "helmet") return comp.enabled;
    }
    return false;
  }


  // remove experiments from kerbal
  public static void RemoveExperiments(KerbalEVA kerbal)
  {
    foreach(PartModule m in kerbal.part.FindModulesImplementing<ModuleScienceExperiment>())
    {
      kerbal.part.RemoveModule(m);
    }
  }


  // set kerbal to the 'freezed' unescapable state
  public static void SetFreezed(KerbalEVA kerbal)
  {
    // do nothing if already freezed
    if (kerbal.fsm.currentStateName != "freezed")
    {
      // create freezed state
      KFSMState freezed = new KFSMState("freezed");

      // create freeze event
      KFSMEvent eva_freeze = new KFSMEvent("EVAfreeze");
      eva_freeze.GoToStateOnEvent = freezed;
      eva_freeze.updateMode = KFSMUpdateMode.MANUAL_TRIGGER;
      kerbal.fsm.AddEvent(eva_freeze, kerbal.fsm.CurrentState);

      // trigger eva death event
      kerbal.fsm.RunEvent(eva_freeze);
    }

    // stop animations
    kerbal.GetComponent<Animation>().Stop();
  }
}


} // KERBALISM