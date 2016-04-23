// ====================================================================================================================
// deal with KSP events, and with things that can't be done elsewhere
// ====================================================================================================================


using System;
using System.Collections.Generic;
using System.Reflection;
using CameraFXModules;
using UnityEngine;


namespace KERBALISM {


[KSPAddon(KSPAddon.Startup.MainMenu, true)]
public class Kerbalism : MonoBehaviour
{
  // keep it alive
  Kerbalism() { DontDestroyOnLoad(this); }


  public void Start()
  {
    // log version
    Lib.Log("version " + Assembly.GetExecutingAssembly().GetName().Version);

    // set callbacks
    GameEvents.onCrewOnEva.Add(this.toEVA);
    GameEvents.onCrewBoardVessel.Add(this.fromEVA);
    GameEvents.onVesselRecovered.Add(this.vesselRecovered);
    GameEvents.onVesselTerminated.Add(this.vesselTerminated);
    GameEvents.onVesselWillDestroy.Add(this.vesselDestroyed);
    GameEvents.onPartCouple.Add(this.vesselDock);
    GameEvents.OnTechnologyResearched.Add(this.techResearched);

    // add module to EVA vessel part prefab
    // note: try..catch travesty required to avoid spurious exception, that seem to have no negative effects
    // note: dummy test for null char required to avoid compiler warning
    try { PartLoader.getPartInfoByName("kerbalEVA").partPrefab.AddModule("EVA"); } catch(Exception ex) { if (ex.Message.Contains("\0")) {} }
    try { PartLoader.getPartInfoByName("kerbalEVAfemale").partPrefab.AddModule("EVA"); } catch(Exception ex) { if (ex.Message.Contains("\0")) {} }
  }


  void toEVA(GameEvents.FromToAction<Part, Part> data)
  {
    // determine if inside breathable atmosphere
    bool breathable = LifeSupport.BreathableAtmosphere(data.from.vessel);

    // get total crew in the origin vessel
    double tot_crew = (double)data.from.vessel.GetVesselCrew().Count + 1.0;

    // add resource definitions to EVA vessel part
    Lib.SetupResource(data.to, "ElectricCharge", 0.0, Settings.ElectricChargeOnEVA);
    if (!breathable) Lib.SetupResource(data.to, "Oxygen", 0.0, Settings.OxygenOnEVA);


    // determine how much MonoPropellant to get
    // note: never more that the 'share' of this kerbal
    double monoprop = Math.Min(Lib.GetResourceAmount(data.from.vessel, "MonoPropellant") / tot_crew, Settings.MonoPropellantOnEVA);

    // determine how much ElectricCharge to get
    // note: never more that the 'share' of this kerbal
    // note: always keep half the ec in the vessel
    double ec = Math.Min(Lib.GetResourceAmount(data.from.vessel, "ElectricCharge") / (tot_crew * 2.0), Settings.ElectricChargeOnEVA);


    // EVA vessels start with 5 units of eva fuel, remove them
    data.to.RequestResource("EVA Propellant", 5.0);

    // transfer monoprop
    data.to.RequestResource("EVA Propellant", -data.from.RequestResource("MonoPropellant", monoprop));

    // transfer ec
    data.to.RequestResource("ElectricCharge", -data.from.RequestResource("ElectricCharge", ec));


    // if outside breathable atmosphere
    if (!breathable)
    {
      // determine how much Oxygen to get
      // note: never more that the 'share' of this kerbal
      double oxygen = Math.Min(Lib.GetResourceAmount(data.from.vessel, "Oxygen") / tot_crew, Settings.OxygenOnEVA);

      // transfer oxygen
      data.to.RequestResource("Oxygen", -data.from.RequestResource("Oxygen", oxygen));
    }


    // get KerbalEVA
    KerbalEVA kerbal = data.to.FindModuleImplementing<KerbalEVA>();

    // turn off headlamp light, to avoid stock bug that show the light for a split second when going on eva
    EVA.SetHeadlamp(kerbal, false);
    EVA.SetFlares(kerbal, false);

    // remove the helmet if inside breathable atmosphere
    // note: done in EVA::FixedUpdate(), but also done here avoid 'popping' of the helmet when going on eva
    EVA.SetHelmet(kerbal, !breathable);

    // remember if the kerbal has an helmet in the EVA module
    data.to.FindModuleImplementing<EVA>().has_helmet = !breathable;


    // show warning if there isn't monoprop in the eva suit
    if (monoprop <= double.Epsilon && !Lib.Landed(data.from.vessel))
    {
      Message.Post(Severity.danger, "There isn't any <b>MonoPropellant</b> in the EVA suit", "Don't let the ladder go!");
    }
  }


  void fromEVA(GameEvents.FromToAction<Part, Part> data)
  {
    // get any leftover monoprop (eva fuel in reality) from EVA vessel
    double monoprop = data.from.Resources.list[0].amount;

    // get any leftover ec
    double ec = data.from.Resources.list[1].amount;


    // add the leftover monoprop back to the pod
    data.to.RequestResource("MonoPropellant", -monoprop);

    // add the leftover ec back to the pod
    data.to.RequestResource("ElectricCharge", -ec);


    // if oxygen was transfered
    if (data.from.FindModuleImplementing<EVA>().has_helmet)
    {
      // get any leftover oxygen
      double oxygen = data.from.RequestResource("Oxygen", Settings.OxygenOnEVA);

      // add the leftover oxygen back to the pod
      data.to.RequestResource("Oxygen", -oxygen);
    }

    // forget vessel data
    DB.ForgetVessel(data.from.vessel.id);
  }


  void vesselRecovered(ProtoVessel vessel, bool b)
  {
    // note: this is called multiple times when a vessel is recovered, but its safe

    // find out if this was an EVA kerbal and if it was dead
    bool is_eva_dead = false;
    foreach(ProtoPartSnapshot p in vessel.protoPartSnapshots)
    {
      foreach(ProtoPartModuleSnapshot m in p.modules)
      {
        is_eva_dead |= (m.moduleName == "EVA" && Lib.GetProtoValue<bool>(m, "is_dead"));
      }
    }

    // set roster status of eva dead kerbals
    if (is_eva_dead)
    {
      vessel.GetVesselCrew()[0].rosterStatus = ProtoCrewMember.RosterStatus.Dead;
    }
    // forget kerbal data of recovered kerbals
    else
    {
      foreach(ProtoCrewMember c in vessel.GetVesselCrew())
      {
        DB.ForgetKerbal(c.name);
      }
    }

    // forget vessel data
    DB.ForgetVessel(vessel.vesselID);
  }


  void vesselTerminated(ProtoVessel vessel)
  {
    // forget all kerbals data
    foreach(ProtoCrewMember c in vessel.GetVesselCrew()) DB.ForgetKerbal(c.name);

    // forget vessel data
    DB.ForgetVessel(vessel.vesselID);
  }


  void vesselDestroyed(Vessel vessel)
  {
    // forget vessel data
    DB.ForgetVessel(vessel.id);

    // rescan the damn kerbals
    // note: vessel crew is empty at destruction time
    // note: we can't even use the flightglobal roster, because sometimes it isn't updated yet at this point
    HashSet<string> kerbals_alive = new HashSet<string>();
    HashSet<string> kerbals_dead = new HashSet<string>();
    foreach(Vessel v in FlightGlobals.Vessels)
    {
      List<ProtoCrewMember> crew = v.loaded ? v.GetVesselCrew() : v.protoVessel.GetVesselCrew();
      foreach(ProtoCrewMember c in crew) kerbals_alive.Add(c.name);
    }
    foreach(var p in DB.Kerbals())
    {
      if (!kerbals_alive.Contains(p.Key)) kerbals_dead.Add(p.Key);
    }
    foreach(string n in kerbals_dead) DB.ForgetKerbal(n);
  }


  void vesselDock(GameEvents.FromToAction<Part, Part> e)
  {
    // forget vessel being docked
    DB.ForgetVessel(e.from.vessel.id);
  }


  void techResearched(GameEvents.HostTargetAction<RDTech, RDTech.OperationResult> data)
  {
    if (data.target != RDTech.OperationResult.Successful) return;
    const string title = "<color=cyan><b>PROGRESS</b></color>\n";
    if (Array.IndexOf(Scrubber.scrubber_efficiency.techs, data.host.techID) >= 0)
    {
      Message.Post(title + "We have access to more efficient <b>CO2 scrubbers</b> now", "Our research efforts are paying off, after all");
    }
    if (Array.IndexOf(Malfunction.manufacturing_quality.techs, data.host.techID) >= 0)
    {
      Message.Post(title + "Advances in material science have led to improved <b>manufacturing quality</b>", "New components will last longer in extreme environments");
    }
    if (Array.IndexOf(Signal.signal_processing.techs, data.host.techID) >= 0)
    {
      Message.Post(title + "Our scientists just made a breakthrough in <b>signal processing</b>", "New and existing antennas range has improved");
    }
  }


  void clearLocks()
  {
    // remove control locks
    InputLockManager.RemoveControlLock("eva_dead_lock");
    InputLockManager.RemoveControlLock("no_signal_lock");
  }


  void setLocks(Vessel v)
  {
    // lock controls for EVA death
    if (v.isEVA && EVA.IsDead(v))
    {
      InputLockManager.SetControlLock(ControlTypes.EVA_INPUT, "eva_dead_lock");
    }

    // lock controls for probes without signal
    if (v.GetCrewCount() == 0 && !Signal.Link(v).linked)
    {
      InputLockManager.SetControlLock(ControlTypes.ALL_SHIP_CONTROLS, "no_signal_lock");
      FlightInputHandler.state.mainThrottle = 0.0f;
    }
  }


  void updateThermometers(Vessel v)
  {
    // get temperature
    double temp = Cache.VesselInfo(v).temperature;

    // get amount of ec
    double ec_amount = Lib.GetResourceAmount(v, "ElectricCharge");

    // replace thermometer sensor readings with our own, to give the user some feedback about climate control mechanic
    foreach(ModuleEnviroSensor m in v.FindPartModulesImplementing<ModuleEnviroSensor>())
    {
      if (m.sensorType == "TEMP" && m.sensorActive)
      {
        if (ec_amount <= double.Epsilon) m.readoutInfo = "NO POWER!";
        else
        {
          m.readoutInfo = Lib.HumanReadableTemp(temp);
        }
      }
    }
  }


  void manageResqueMission(Vessel v)
  {
    // skip eva dead kerbals
    // rationale: getting the kerbal data will create it again, leading to spurious resque mission detection
    if (EVA.IsDead(v)) return;

    // deal with resque missions
    foreach(ProtoCrewMember c in v.GetVesselCrew())
    {
      // get kerbal data
      kerbal_data kd = DB.KerbalData(c.name);

      // flag the kerbal as not resque at prelaunch
      if (v.situation == Vessel.Situations.PRELAUNCH) kd.resque = 0;

      // if the kerbal belong to a resque mission
      if (kd.resque == 1)
      {
        // give the vessel some supply
        Lib.RequestResource(v, v.isEVA ? "EVA Propellant" : "MonoPropellant", -Settings.ResqueMonoPropellant);
        Lib.RequestResource(v, "ElectricCharge", -Settings.ResqueElectricCharge);
        Lib.RequestResource(v, "Food", -Settings.ResqueFood);
        Lib.RequestResource(v, "Oxygen", -Settings.ResqueOxygen);

        // flag the kerbal as non-resque
        // note: enable life support mechanics for the kerbal
        kd.resque = 0;

        // show a message
        Message.Post("We found <b>" + c.name + "</b>", (c.gender == ProtoCrewMember.Gender.Male ? "He" : "She") + "'s still alive!");
      }
    }
  }


  void resourceWarnings()
  {
    // for each vessel
    foreach(Vessel v in FlightGlobals.Vessels)
    {
      // skip invalid vessels
      if (!Lib.IsVessel(v)) continue;

      // skip resque missions
      if (Lib.IsResqueMission(v)) continue;

      // skip dead eva kerbal
      if (EVA.IsDead(v)) continue;

      // get vessel data
      vessel_data vd = DB.VesselData(v.id);

      // get EC amount and capacity
      double ec_amount = Lib.GetResourceAmount(v, "ElectricCharge");
      double ec_capacity = Lib.GetResourceCapacity(v, "ElectricCharge");
      double ec_perc = ec_capacity > 0.0 ? ec_amount / ec_capacity : 0.0;

      // if it has EC capacity
      if (ec_capacity > 0.0)
      {
        // check EC thresholds and show messages
        if (ec_perc <= Settings.ResourceDangerThreshold && vd.msg_ec < 2)
        {
          if (vd.cfg_ec == 1) Message.Post(Severity.danger, VesselEvent.ec, v);
          vd.msg_ec = 2;
        }
        else if (ec_perc <= Settings.ResourceWarningThreshold && vd.msg_ec < 1)
        {
          if (vd.cfg_ec == 1) Message.Post(Severity.warning, VesselEvent.ec, v);
          vd.msg_ec = 1;
        }
        else if (ec_perc > Settings.ResourceWarningThreshold && vd.msg_ec > 0)
        {
          if (vd.cfg_ec == 1) Message.Post(Severity.relax, VesselEvent.ec, v);
          vd.msg_ec = 0;
        }
      }

      // get food amount and capacity
      double food_amount = Lib.GetResourceAmount(v, "Food");
      double food_capacity = Lib.GetResourceCapacity(v, "Food");
      double food_perc = food_capacity > 0.0 ? food_amount / food_capacity : 0.0;

      // if it has food capacity
      if (food_capacity > 0.0)
      {
        // check food thresholds and show messages
        // note: no warnings at prelaunch
        if (food_perc <= Settings.ResourceDangerThreshold && vd.msg_food < 2)
        {
          if (vd.cfg_supply == 1 && v.situation != Vessel.Situations.PRELAUNCH) Message.Post(Severity.danger, VesselEvent.food, v);
          vd.msg_food = 2;
        }
        else if (food_perc <= Settings.ResourceWarningThreshold && vd.msg_food < 1)
        {
          if (vd.cfg_supply == 1 && v.situation != Vessel.Situations.PRELAUNCH) Message.Post(Severity.warning, VesselEvent.food, v);
          vd.msg_food = 1;
        }
        else if (food_perc > Settings.ResourceWarningThreshold && vd.msg_food > 0)
        {
          if (vd.cfg_supply == 1 && v.situation != Vessel.Situations.PRELAUNCH) Message.Post(Severity.relax, VesselEvent.food, v);
          vd.msg_food = 0;
        }
      }

      // get oxygen amount and capacity
      double oxygen_amount = Lib.GetResourceAmount(v, "Oxygen");
      double oxygen_capacity = Lib.GetResourceCapacity(v, "Oxygen");
      double oxygen_perc = oxygen_capacity > 0.0 ? oxygen_amount / oxygen_capacity : 0.0;

      // if it has oxygen capacity
      if (oxygen_capacity > 0.0)
      {
        // check oxygen thresholds and show messages
        // note: no warnings at prelaunch
        if (oxygen_perc <= Settings.ResourceDangerThreshold && vd.msg_oxygen < 2)
        {
          if (vd.cfg_supply == 1 && v.situation != Vessel.Situations.PRELAUNCH) Message.Post(Severity.danger, VesselEvent.oxygen, v);
          vd.msg_oxygen = 2;
        }
        else if (oxygen_perc <= Settings.ResourceWarningThreshold && vd.msg_oxygen < 1)
        {
          if (vd.cfg_supply == 1 && v.situation != Vessel.Situations.PRELAUNCH) Message.Post(Severity.warning, VesselEvent.oxygen, v);
          vd.msg_oxygen = 1;
        }
        else if (oxygen_perc > Settings.ResourceWarningThreshold && vd.msg_oxygen > 0)
        {
          if (vd.cfg_supply == 1 && v.situation != Vessel.Situations.PRELAUNCH) Message.Post(Severity.relax, VesselEvent.oxygen, v);
          vd.msg_oxygen = 0;
        }
      }
    }
  }


  void atmosphereDecay()
  {
    // [disabled] disable 'terminate' button in tracking station
    // note: we could forbid the user from terminating debris, if we make them decay (in atmosphere and not)
    // however there are still cases when it is desiderable to terminate a vessel, so we leave it enabled
    //HighLogic.CurrentGame.Parameters.TrackingStation.CanAbortVessel = false;

    // decay unloaded vessels inside atmosphere
    foreach(Vessel v in FlightGlobals.Vessels.FindAll(k => !k.loaded && !Lib.Landed(k)))
    {
      // get pressure
      double p = v.mainBody.GetPressure(v.altitude);

      // if inside some kind of atmosphere
      if (p > 0.0)
      {
        // calculate decay speed to be 1km/s per-kPa
        double decay_speed = 1000.0 * p;

        // decay the orbit
        v.orbit.semiMajorAxis -= decay_speed * TimeWarp.fixedDeltaTime;
      }
    }
  }


  public void FixedUpdate()
  {
    // remove control locks in any case
    clearLocks();

    // do nothing else if db isn't ready
    if (!DB.Ready()) return;

    // do nothing else in the editors and the menus
    if (!Lib.SceneIsGame()) return;

    // if there is an active vessel
    Vessel v = FlightGlobals.ActiveVessel;
    if (v != null && Lib.IsVessel(v))
    {
      // set control locks if necessary
      setLocks(v);

      // update thermometer readings
      updateThermometers(v);

      // manage resque mission mechanics
      manageResqueMission(v);
    }

    // show vessel resource warnings
    resourceWarnings();

    // decay debris orbits
    atmosphereDecay();


    // FIXME: forcing warp rate here essentially stop the 'slow warp change' done in Lib.StopWarp(), temporarely disabled
    // [disabled] detect if there are any serious warnings active
    // note: we consider serious only the ones that have a time-to-death shorther than 1 day
    //bool warnings = false;
    //foreach(var p in DB.Kerbals())
    //{
    //  kerbal_data kd = p.Value;
    //  warnings |= kd.msg_freezing > 0;
    //  warnings |= kd.msg_burning > 0;
    //  warnings |= kd.msg_deprived > 0;
    //}
    // if there is a warning active, do not allow the user to warp at high speed
    // rationale: at high warp speed, even stopping warping don't give the user time to react, kerbals just die
    //if (warnings && TimeWarp.CurrentRateIndex > 4) TimeWarp.SetRate(4, true);
  }


  // kill a kerbal
  public static void Kill(Vessel v, ProtoCrewMember c)
  {
    // forget kerbal data
    DB.ForgetKerbal(c.name);

    // if on pod
    if (!v.isEVA)
    {
      // if vessel is loaded
      if (v.loaded)
      {
        // find part
        Part part = null;
        foreach(Part p in v.parts)
        {
          if (p.protoModuleCrew.Find(k => k.name == c.name) != null) { part = p; break; }
        }

        // remove kerbal and kill it
        part.RemoveCrewmember(c);
        c.Die();
      }
      // if vessel is not loaded
      else
      {
        // find proto part
        ProtoPartSnapshot part = null;
        foreach(ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
        {
          if (p.HasCrew(c.name)) { part = p; break; }
        }

        // remove from vessel
        part.RemoveCrew(c.name);

        // flag as dead
        c.rosterStatus = ProtoCrewMember.RosterStatus.Dead;

        // register background death manually for death report notifications
        Notifications.RegisterDeath();
      }
    }
    // else it must be an eva death
    else
    {
      // flag as eva death
      EVA.Kill(v);

      // rename vessel
      v.vesselName = c.name + "'s body";

      // register eva death manually for death report notifications
      Notifications.RegisterDeath();
    }

    // remove reputation
    if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
    {
      Reputation.Instance.AddReputation(-Settings.DeathReputationPenalty, TransactionReasons.Any);
    }
  }


  // hook: DisableKerbal()
  public static void hook_DisableKerbal(string k_name, bool disabled)
  {
    if (!DB.Ready()) return;
    if (!DB.Kerbals().ContainsKey(k_name)) return;
    DB.KerbalData(k_name).disabled = disabled ? 1u : 0;
  }


  // hook: InjectRadiation()
  public static void hook_InjectRadiation(string k_name, double amount)
  {
    if (!DB.Ready()) return;
    if (!DB.Kerbals().ContainsKey(k_name)) return;
    kerbal_data kd = DB.KerbalData(k_name);
    kd.radiation = Math.Max(kd.radiation + amount, 0.0);
  }
}


} // KERBALISM


#if false
// CONFIG
//ConfigNode settings_node = GameDatabase.Instance.GetConfigNode("Kerbalism/KerbalismSettings");
//launcher_btn.onRightClick = ()=> Message.Post("Right Click");

// LINE/SPLINE RENDERING EXPERIMENTS

class something
{
  void somefunc()
  {
    // # draw line

    // destroy previous line if any
    if (line != null) Vector.DestroyLine(ref line);

    Vector3d a = FlightGlobals.GetHomeBody().position;
    Vector3d b = FlightGlobals.Bodies[2].position;

    // tranform points in scaled space
    a = ScaledSpace.LocalToScaledSpace(a);
    b = ScaledSpace.LocalToScaledSpace(b);

    // detect if one is behind
    Vector3d look = MapView.MapCamera.transform.forward;
    double k = Math.Min(Vector3d.Dot(a, look), Vector3d.Dot(b, look));
    if (k > 0.05) //< avoid glitches
    {
      line = new VectorLine("l_i_n_e", new Vector3[]{a, b}, MapView.OrbitLinesMaterial, 5.0f);
      Vector.SetColor(line, Color.red);
      Vector.DrawLine(line);
    }

    // # draw spline
    if (spline != null) Vector.DestroyLine(ref spline);

    Vector3d p0 = FlightGlobals.Bodies[1].position;
    Vector3d p1 = FlightGlobals.Bodies[2].position;
    Vector3d p2 = FlightGlobals.Bodies[3].position;

    // tranform points in scaled space
    p0 = ScaledSpace.LocalToScaledSpace(p0);
    p1 = ScaledSpace.LocalToScaledSpace(p1);
    p2 = ScaledSpace.LocalToScaledSpace(p2);

    k = Math.Min(Vector3d.Dot(p0, look), Math.Min(Vector3d.Dot(p1, look), Vector3d.Dot(p2, look)));
    if (k > 0.05) //< avoid glitches
    {
      spline = new VectorLine("s_p_l_i_n_e", new Vector3[128], MapView.OrbitLinesMaterial, 5.0f);
      Vector.SetColor(spline, Color.cyan);
      Vector.MakeSplineInLine(spline, new Vector3[]{p0,p1,p2});
      Vector.DrawLine(spline);
    }
  }

  VectorLine line;
  VectorLine spline;
}

#endif