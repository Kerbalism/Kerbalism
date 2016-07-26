// ====================================================================================================================
// functions that deal with EVA kerbals
// ====================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class EVA : PartModule
{
  [KSPField(isPersistant = true)] public bool has_helmet = true;          // indicate if eva kerbal has an helmet (and oxygen)
  [KSPField(isPersistant = true)] public bool is_dead = false;            // indicate if eva kerbal is dead


  public void FixedUpdate()
  {
    // get vessel info from the cache
    vessel_info vi = Cache.VesselInfo(vessel);

    // get KerbalEVA module
    KerbalEVA kerbal = part.FindModuleImplementing<KerbalEVA>();

    // show/hide helmet, play nice with KIS
    if (!Kerbalism.detected_mods.KIS)
    {
      SetHelmet(kerbal, has_helmet);
    }
    // synchronize has_helmet state with KIS (for the headlights)
    else
    {
      has_helmet = HasHelmet(kerbal);
    }

    // get resource handler
    resource_info ec = ResourceCache.Info(vessel, "ElectricCharge");

    // consume EC for the headlamp
    if (has_helmet && kerbal.lampOn) ec.Consume(Settings.HeadlightCost * Kerbalism.elapsed_s * vi.time_dilation);

    // force the headlamp lights on/off depending on ec amount left and if it has an helmet
    SetHeadlamp(kerbal, has_helmet && kerbal.lampOn && ec.amount > double.Epsilon);

    // synchronize helmet flares with headlamp state
    SetFlares(kerbal, has_helmet && kerbal.lampOn && ec.amount > double.Epsilon);

    // if dead
    if (is_dead)
    {
      // enforce freezed state
      SetFreezed(kerbal);

      // remove plant flag action
      kerbal.flagItems = 0;

      // remove experiment actions (game engine keeps readding them)
      RemoveExperiments(kerbal);
    }
  }


  // return true if a vessel is a dead EVA kerbal
  public static bool IsDead(Vessel vessel)
  {
    if (!vessel.isEVA) return false;
    if (vessel.loaded)
    {
      var eva = vessel.FindPartModulesImplementing<EVA>();
      if (eva == null || eva.Count == 0) return false; //< assume alive for resque EVA, that don't have our module
      return eva[0].is_dead;
    }
    foreach(ProtoPartSnapshot part in vessel.protoVessel.protoPartSnapshots)
    {
      foreach(ProtoPartModuleSnapshot module in part.modules)
      {
        if (module.moduleName == "EVA") return Lib.Proto.GetBool(module, "is_dead");
      }
    }
    return false;
  }


  // set the kerbal as dead
  public static void Kill(Vessel vessel)
  {
    if (!vessel.isEVA) return;
    if (vessel.loaded)
    {
      var eva = vessel.FindPartModulesImplementing<EVA>();
      if (eva == null || eva.Count == 0) return; //< do nothing for resque EVA, that don't have our module
      eva[0].is_dead = true;
    }
    else
    {
      foreach(ProtoPartSnapshot part in vessel.protoVessel.protoPartSnapshots)
      {
        foreach(ProtoPartModuleSnapshot module in part.modules)
        {
          if (module.moduleName == "EVA") Lib.Proto.Set(module, "is_dead", true);
        }
      }
    }
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

    foreach (var comp in kerbal.GetComponentsInChildren<Renderer>())
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
    foreach(var comp in kerbal.GetComponentsInChildren<Renderer>())
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
    foreach(var comp in kerbal.GetComponentsInChildren<Renderer>())
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