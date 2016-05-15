// ====================================================================================================================
// deal with KSP events, and with things that can't be done elsewhere
// ====================================================================================================================


using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using CameraFXModules;
using KSP.UI;
using KSP.UI.Screens;
using UnityEngine;


namespace KERBALISM {


[KSPAddon(KSPAddon.Startup.MainMenu, true)]
public class Kerbalism : MonoBehaviour
{
  // mods to detect
  public class DetectedMods
  {
    public bool SCANsat;
    public bool CLS;
    public bool KIS;
    public bool CRP;
    public bool RemoteTech;
    public bool AntennaRange;
    public bool DangIt;
  }

  // features to enable or disable
  public class Features
  {
    public bool signal;
    public bool malfunction;
    public bool scrubber;
    public bool shielding;
  }

  // breakdown events
  public enum KerbalBreakdown
  {
    mumbling,         // do nothing (in case all conditions fail)
    fat_finger,       // data has been cancelled
    rage,             // components have been damaged
    depressed,        // food has been lost
    wrong_valve,      // oxygen has been lost
    argument          // stress increased for all the crew
  }

  // store detected mods
  public static DetectedMods detected_mods = new DetectedMods();

  // store enabled features
  public static Features features = new Features();

  // store all the rules
  public static Dictionary<string, Rule> rules = new Dictionary<string, Rule>();

  // only the 'supply' rules
  public static List<Rule> supply_rules = new List<Rule>();

  // the rule relative to EC, if any
  public static Rule ec_rule = null;


  // keep it alive
  Kerbalism() { DontDestroyOnLoad(this); }


  public void Start()
  {
    // shortcut to the resource library
    var reslib = PartResourceLibrary.Instance.resourceDefinitions;

    // length of a day in seconds
    double daylen = 60.0 * 60.0 * Lib.HoursInDay();


    // log version
    Lib.Log("version " + Assembly.GetExecutingAssembly().GetName().Version);

    // parse detected mods
    var mods = Lib.ParseConfig("Kerbalism/Patches/System/DetectedMods");
    detected_mods.SCANsat = Lib.ConfigValue(mods, "SCANsat", false);
    detected_mods.CLS = Lib.ConfigValue(mods, "CLS", false);
    detected_mods.KIS = Lib.ConfigValue(mods, "KIS", false);
    detected_mods.CRP = Lib.ConfigValue(mods, "CRP", false);
    detected_mods.RemoteTech = Lib.ConfigValue(mods, "RemoteTech", false);
    detected_mods.AntennaRange = Lib.ConfigValue(mods, "AntennaRange", false);
    detected_mods.DangIt = Lib.ConfigValue(mods, "DangIt", false);
    Lib.Log("detected:");
    Lib.Log("- SCANsat: " + detected_mods.SCANsat);
    Lib.Log("- CLS: " + detected_mods.CLS);
    Lib.Log("- KIS: " + detected_mods.KIS);
    Lib.Log("- CRP: " + detected_mods.CRP);
    Lib.Log("- RemoteTech: " + detected_mods.RemoteTech);
    Lib.Log("- AntennaRange: " + detected_mods.AntennaRange);
    Lib.Log("- DangIt: " + detected_mods.DangIt);

    // determine features
    var f_cfg = Lib.ParseConfig("Kerbalism/Patches/System/Features");
    features.signal = Lib.ConfigValue(f_cfg, "signal", false);
    features.malfunction = Lib.ConfigValue(f_cfg, "malfunction", false);
    features.scrubber = Lib.ConfigValue(f_cfg, "scrubber", false);
    features.shielding = Lib.ConfigValue(f_cfg, "shielding", false);
    Lib.Log("features:");
    Lib.Log("- signal: " + features.signal);
    Lib.Log("- malfunction: " + features.malfunction);
    Lib.Log("- scrubber: " + features.scrubber);
    Lib.Log("- shielding: " + features.shielding);

    // get all the rules
    var r_cfg = Lib.ParseConfigs("Rule");
    foreach(var node in r_cfg)
    {
      Rule r = new Rule(node);
      if (r.name.Length > 0 && !rules.ContainsKey(r.name))
      {
        rules.Add(r.name, r);
        if (r.resource_name == "ElectricCharge") ec_rule = r;
        else if (r.resource_name.Length > 0 && reslib.Contains(r.resource_name)) supply_rules.Add(r);
      }
    }
    Lib.Log("rules:");
    foreach(var p in rules)
    {
      Rule r = p.Value;
      string modifiers = r.modifier.Count > 0 ? string.Join(", ", r.modifier.ToArray()) : "none";
      Lib.Log("- " + r.name + " (modifier: " + modifiers + ")");
    }
    if (rules.Count == 0) Lib.Log("- none");

    // add resources to manned parts
    foreach(AvailablePart part in PartLoader.LoadedPartsList)
    {
      // get the prefab
      Part prefab = part.partPrefab;

      // avoid problems with some parts that don't have a resource container (EVA kerbals, flags)
      if (prefab.Resources == null) continue;

      // if manned
      if (prefab.CrewCapacity > 0)
      {
        double crew_capacity = (double)prefab.CrewCapacity;
        double extra_cost = 0.0;
        foreach(var p in rules)
        {
          Rule r = p.Value;

          // add rule's resource
          if (r.resource_name.Length > 0 && r.on_pod > 0.0 && reslib.Contains(r.resource_name) && !prefab.Resources.Contains(r.resource_name))
          {
            var res = new ConfigNode("RESOURCE");
            res.AddValue("name", r.resource_name);
            res.AddValue("amount", r.on_pod * crew_capacity);
            res.AddValue("maxAmount", r.on_pod * crew_capacity);
            prefab.Resources.Add(res);
            extra_cost += (double)reslib[r.resource_name].unitCost * r.on_pod * crew_capacity;
          }
        }
        // loop the rules a second time to give the resources a nice order
        foreach(var p in rules)
        {
          Rule r = p.Value;

          // add rule's waste resource
          double waste_per_day = (r.rate / (r.interval > 0.0 ? r.interval : 1.0)) * daylen * r.waste_ratio;
          double waste_amount = waste_per_day * r.waste_buffer * crew_capacity;
          if (r.waste_name.Length > 0 && waste_amount > double.Epsilon && reslib.Contains(r.waste_name) && !prefab.Resources.Contains(r.waste_name))
          {
            var res = new ConfigNode("RESOURCE");
            res.AddValue("name", r.waste_name);
            res.AddValue("amount", "0.0");
            res.AddValue("maxAmount", waste_amount);
            if (r.hide_waste)
            {
              res.AddValue("isTweakable", false.ToString());
              res.AddValue("isVisible", false.ToString());
            }
            prefab.Resources.Add(res);
            extra_cost += (double)reslib[r.waste_name].unitCost * waste_amount;
          }
        }

        // add shielding
        if (features.shielding && reslib.Contains("Shielding") && !prefab.Resources.Contains("Shielding"))
        {
          var res = new ConfigNode("RESOURCE");
          res.AddValue("name", "Shielding");
          res.AddValue("amount", "0.0");
          res.AddValue("maxAmount", crew_capacity);
          prefab.Resources.Add(res);
          extra_cost += (double)reslib["Shielding"].unitCost * crew_capacity;
        }

        // add the extra cost
        part.cost += (float)extra_cost;
        part.cost = Mathf.Round(part.cost + 0.5f);
      }
    }

    // set callbacks
    GameEvents.onCrewOnEva.Add(this.toEVA);
    GameEvents.onCrewBoardVessel.Add(this.fromEVA);
    GameEvents.onVesselRecovered.Add(this.vesselRecovered);
    GameEvents.onVesselTerminated.Add(this.vesselTerminated);
    GameEvents.onVesselWillDestroy.Add(this.vesselDestroyed);
    GameEvents.onPartCouple.Add(this.vesselDock);
    GameEvents.OnTechnologyResearched.Add(this.techResearched);
    GameEvents.onGUIEditorToolbarReady.Add(this.addEditorCategory);

    // add module to EVA vessel part prefab
    // note: try..catch travesty required to avoid spurious exception, that seem to have no negative effects
    // note: dummy test for null char required to avoid compiler warning
    try { PartLoader.getPartInfoByName("kerbalEVA").partPrefab.AddModule("EVA"); } catch(Exception ex) { if (ex.Message.Contains("\0")) {} }
    try { PartLoader.getPartInfoByName("kerbalEVAfemale").partPrefab.AddModule("EVA"); } catch(Exception ex) { if (ex.Message.Contains("\0")) {} }
  }


  void toEVA(GameEvents.FromToAction<Part, Part> data)
  {
    // determine if inside breathable atmosphere
    bool breathable = Cache.VesselInfo(data.from.vessel).breathable;

    // get total crew in the origin vessel
    double tot_crew = (double)data.from.vessel.GetVesselCrew().Count + 1.0;

    // EVA vessels start with 5 units of eva fuel, remove them
    data.to.RequestResource("EVA Propellant", 5.0);

    // determine how much MonoPropellant to get
    // note: never more that the 'share' of this kerbal
    double monoprop = Math.Min(Lib.GetResourceAmount(data.from.vessel, "MonoPropellant") / tot_crew, Settings.MonoPropellantOnEVA);

    // transfer monoprop
    data.to.RequestResource("EVA Propellant", -data.from.RequestResource("MonoPropellant", monoprop));

    // show warning if there isn't monoprop in the eva suit
    if (monoprop <= double.Epsilon && !Lib.Landed(data.from.vessel))
    {
      Message.Post(Severity.danger, "There isn't any <b>MonoPropellant</b> in the EVA suit", "Don't let the ladder go!");
    }

    // manage resources from rules
    foreach(var p in rules)
    {
      Rule r = p.Value;
      if (r.resource_name.Length == 0 || r.on_eva <= double.Epsilon) continue;

      // determine amount to take, never more that his own share
      double amount = Math.Min(Lib.GetResourceAmount(data.from.vessel, r.resource_name) / tot_crew, r.on_eva);

      // deal with breathable modifier
      if (Sim.Breathable(data.from.vessel) && r.modifier.Contains("breathable")) continue;

      // remove resource from the vessel
      amount = Lib.RequestResource(data.from.vessel, r.resource_name, amount);

      // create new resource in the eva kerbal
      Lib.SetupResource(data.to, r.resource_name, amount, r.on_eva);
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

    // mute messages for a couple seconds
    Message.MuteInternal();
    base.StartCoroutine(CallbackUtil.DelayedCallback(2.0f, Message.UnmuteInternal));

    // if vessel info is open, switch to the eva kerbal
    if (Info.IsOpen()) Info.Open(data.to.vessel);
  }


  void fromEVA(GameEvents.FromToAction<Part, Part> data)
  {
    // get any leftover monoprop (eva fuel) from EVA vessel
    double monoprop = data.from.Resources.list[0].amount;

    // add the leftover monoprop back to the pod
    data.to.RequestResource("MonoPropellant", -monoprop);

    // manage resources in rules
    foreach(var p in rules)
    {
      Rule r = p.Value;
      if (r.resource_name.Length == 0 || r.on_eva <= double.Epsilon) continue;

      double leftover = Lib.GetResourceAmount(data.from.vessel, r.resource_name);
      data.to.RequestResource(r.resource_name, -leftover);
    }

    // forget vessel data
    DB.ForgetVessel(data.from.vessel.id);

    // if vessel info is open, switch to the vessel
    if (Info.IsOpen()) Info.Open(data.to.vessel);
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
    if (DB.Ready()) DB.ForgetVessel(e.from.vessel.id);
  }


  void addEditorCategory()
  {
    if (PartLoader.LoadedPartsList.Find(k => k.tags.IndexOf("_kerbalism", StringComparison.Ordinal) >= 0) != null)
    {
      var icon = new RUI.Icons.Selectable.Icon("Kerbalism", Lib.GetTexture("category_normal"), Lib.GetTexture("category_selected"));
      PartCategorizer.Category category = PartCategorizer.Instance.filters.Find(k => k.button.categoryName == "Filter by Function");
      PartCategorizer.AddCustomSubcategoryFilter(category, "", icon, k => k.tags.IndexOf("_kerbalism", StringComparison.Ordinal) >= 0);
    }
  }


  void techResearched(GameEvents.HostTargetAction<RDTech, RDTech.OperationResult> data)
  {
    if (data.target != RDTech.OperationResult.Successful) return;
    const string title = "<color=cyan><b>PROGRESS</b></color>\n";
    if (features.scrubber && Array.IndexOf(Scrubber.scrubber_efficiency.techs, data.host.techID) >= 0)
    {
      Message.Post(title + "We have access to more efficient <b>CO2 scrubbers</b> now", "Our research efforts are paying off, after all");
    }
    if (features.malfunction && Array.IndexOf(Malfunction.manufacturing_quality.techs, data.host.techID) >= 0)
    {
      Message.Post(title + "Advances in material science have led to improved <b>manufacturing quality</b>", "New components will last longer in extreme environments");
    }
    if (features.signal && Array.IndexOf(Signal.signal_processing.techs, data.host.techID) >= 0)
    {
      Message.Post(title + "Our scientists just made a breakthrough in <b>signal processing</b>", "New and existing antennas range has improved");
    }
  }


  void techDescriptions()
  {
    var rnd = RDController.Instance;
    if (rnd == null) return;
    var selected = RDController.Instance.node_selected;
    if (selected == null) return;
    var techID = selected.tech.techID;
    if (rnd.node_description.text.IndexOf("\n\n", StringComparison.Ordinal) == -1)
    {
      if (features.scrubber && Scrubber.scrubber_efficiency.techs.IndexOf(techID) >= 0)
        rnd.node_description.text += "\n\n<color=cyan>Improve scrubbers efficiency</color>";
      if (features.malfunction && Malfunction.manufacturing_quality.techs.IndexOf(techID) >= 0)
        rnd.node_description.text += "\n\n<color=cyan>Improve manufacturing quality</color>";
      if (features.signal && Signal.signal_processing.techs.IndexOf(techID) >= 0)
        rnd.node_description.text += "\n\n<color=cyan>Improve signal processing</color>";
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
        var reslib = PartResourceLibrary.Instance.resourceDefinitions;
        var parts = Lib.GetPartsRecursively(v.rootPart); //< what's the reason for this?

        // give the vessel some monoprop
        string monoprop_name = v.isEVA ? "EVA Propellant" : "MonoPropellant";
        foreach(var part in parts)
        {
          if (part.Resources.list.Find(k => k.resourceName == monoprop_name) == null)
          {
            Lib.SetupResource(part, monoprop_name, 0.0, Settings.MonoPropellantOnResque);
          }
        }
        Lib.RequestResource(v, monoprop_name, -Settings.MonoPropellantOnResque);

        // give the vessel some supplies
        foreach(var p in rules)
        {
          Rule r = p.Value;
          if (r.resource_name.Length == 0 || r.on_resque <= double.Epsilon || !reslib.Contains(r.resource_name)) continue;
          foreach(var part in parts)
          {
            if (part.Resources.list.Find(k => k.resourceName == r.resource_name) == null)
            {
              Lib.SetupResource(part, r.resource_name, 0.0, r.on_resque);
            }
          }

          Lib.RequestResource(v, r.resource_name, -r.on_resque);
        }

        // flag the kerbal as non-resque
        // note: enable life support mechanics for the kerbal
        kd.resque = 0;

        // show a message
        Message.Post("We found <b>" + c.name + "</b>", (c.gender == ProtoCrewMember.Gender.Male ? "He" : "She") + "'s still alive!");
      }
    }
  }


  void beltWarnings(Vessel v, vessel_data vd)
  {
    // belt warnings
    // note: we only show it for manned vessels, but the first time we also show it for probes
    if (Lib.CrewCount(v) > 0 || DB.NotificationData().first_belt_crossing == 0)
    {
      bool inside_belt = Radiation.InsideBelt(v);
      if (inside_belt && vd.msg_belt < 1)
      {
        Message.Post("<b>" + v.vesselName + "</b> is crossing <i>" + v.mainBody.bodyName + " radiation belt</i>", "Exposed to extreme radiation");
        vd.msg_belt = 1;
        DB.NotificationData().first_belt_crossing = 1; //< record first belt crossing
      }
      else if (!inside_belt && vd.msg_belt > 0)
      {
        // no message after crossing the belt
        vd.msg_belt = 0;
      }
    }
  }


  void atmosphereDecay()
  {
    // [disabled] disable 'terminate' button in tracking station
    // note: we could forbid the user from terminating debris, if we make them decay (in atmosphere and not)
    // however there are still cases when is desiderable to terminate a vessel, so we leave it enabled
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


  void updateConnectedSpaces(Vessel v, ConnectedLivingSpace.ICLSAddon cls)
  {
    // calculate whole-space
    if (cls == null)
    {
      foreach(var c in v.loaded ? v.GetVesselCrew() : v.protoVessel.GetVesselCrew())
      {
        kerbal_data kd = DB.KerbalData(c.name);
        kd.living_space = QualityOfLife.LivingSpace((uint)Lib.CrewCount(v), (uint)Lib.CrewCapacity(v));
        kd.entertainment = QualityOfLife.Entertainment(v);
        kd.shielding = Radiation.Shielding(v);
        kd.space_name = "";
      }
    }
    // calculate connected-space
    // note: avoid problem at scene changes
    else if (v.loaded && cls.Vessel != null)
    {
      // calculate connected spaces spaces
      foreach(var space in cls.Vessel.Spaces)
      {
        foreach(var c in space.Crew)
        {
          kerbal_data kd = DB.KerbalData(c.Kerbal.name);
          kd.living_space = QualityOfLife.LivingSpace(space);
          kd.entertainment = QualityOfLife.Entertainment(space);
          kd.shielding = Radiation.Shielding(space);
          kd.space_name = space.Name;
        }
      }
    }
  }


  void externalSeats(Vessel v)
  {
    if (!v.loaded) return;

    var reslib = PartResourceLibrary.Instance.resourceDefinitions;
    foreach(KerbalSeat seat in v.FindPartModulesImplementing<KerbalSeat>())
    {
      foreach(var p in rules)
      {
        Rule r = p.Value;
        if (r.resource_name.Length > 0 && r.rate > 0.0 && reslib.Contains(r.resource_name))
        {
          var res = seat.Occupant.Resources.list.Find(k => k.resourceName == r.resource_name);
          if (res == null) continue;
          res.flowMode = PartResource.FlowMode.In;
        }
      }
    }
  }


  void applyRules(Vessel v, vessel_data vd, vessel_info vi)
  {
    // get crew
    List<ProtoCrewMember> crew = v.loaded ? v.GetVesselCrew() : v.protoVessel.GetVesselCrew();

    // for each rule
    foreach(var p in rules)
    {
      // shortcuts
      Rule r = p.Value;

      // if a resource is specified
      if (r.resource_name.Length > 0)
      {
        // get data
        vmon_data vmon = DB.VmonData(v.id, r.name);
        double amount = Lib.GetResourceAmount(v, r.resource_name);
        double capacity = Lib.GetResourceCapacity(v, r.resource_name);

        // message obey config
        bool show_msg = (r.resource_name == "ElectricCharge" ? vd.cfg_ec > 0 : vd.cfg_supply > 0);

        // no messages with no capacity
        if (capacity > double.Epsilon)
        {
          double level = amount / capacity;
          uint variant = crew.Count > 0 ? 0 : 1u; //< manned/probe variant

          // manage messages
          if (level <= double.Epsilon && vmon.message < 2)
          {
            if (r.empty_message.Length > 0 && show_msg) Message.Post(Severity.danger, Lib.ExpandMsg(r.empty_message, v, null, variant));
            vmon.message = 2;
          }
          else if (level < r.low_threshold && vmon.message < 1)
          {
            if (r.low_message.Length > 0 && show_msg) Message.Post(Severity.warning, Lib.ExpandMsg(r.low_message, v, null, variant));
            vmon.message = 1;
          }
          else if (level > r.low_threshold && vmon.message > 0)
          {
            if (r.refill_message.Length > 0 && show_msg) Message.Post(Severity.relax, Lib.ExpandMsg(r.refill_message, v, null, variant));
            vmon.message = 0;
          }
        }
      }
    }

    // for each crew
    foreach(ProtoCrewMember c in crew)
    {
      // get kerbal data
      kerbal_data kd = DB.KerbalData(c.name);

      // skip resque kerbals
      if (kd.resque == 1) continue;

      // skip disabled kerbals
      if (kd.disabled == 1) continue;

      // for each rule
      foreach(var p in rules)
      {
        // shortcuts
        Rule r = p.Value;

        // get supply data from db
        kmon_data kmon = DB.KmonData(c.name, r.name);

        // calculate variance
        double variance = Variance(c, r.variance);

        // get modifier
        double k = 1.0;
        foreach(string modifier in r.modifier)
        {
          switch(modifier)
          {
            case "breathable":
              k *= Sim.Breathable(v) ? 0.0 : 1.0;
              break;

            case "temperature":
              k *= v.altitude < 2000.0 && v.mainBody == FlightGlobals.GetHomeBody() ? 0.0 : Sim.TempDiff(vi.temperature);
              break;

            case "radiation":
              k *= vi.env_radiation * (1.0 - kd.shielding);
              break;

            case "qol":
              k *= 1.0 / QualityOfLife.Bonus(v, c.name);
              break;
          }
        }

        // meal-wise consumption and generation
        if (r.interval > double.Epsilon) k /= TimeWarp.fixedDeltaTime;

        // accumulate time
        kmon.time_since += TimeWarp.fixedDeltaTime;

        // if continuous, or if interval elapsed
        if (r.interval <= double.Epsilon || kmon.time_since > r.interval)
        {
          // reset time accumulator
          kmon.time_since = 0.0;

          // if there is a resource specified
          if (r.resource_name.Length > 0 && r.rate > double.Epsilon)
          {
            // consume resource
            double required = r.rate * TimeWarp.fixedDeltaTime * k;
            double consumed = Lib.RequestResource(v, r.resource_name, required);

            // produce waste
            if (r.waste_name.Length > 0)
            {
              Lib.RequestResource(v, r.waste_name, -consumed * r.waste_ratio);
            }

            // reset degeneration when consumed, or when not required at all
            if (consumed > required - 0.00000001)
            {
              // slowly recover instead of instant reset
              kmon.problem *= 1.0 / (1.0 + (r.interval > double.Epsilon ? r.interval : TimeWarp.fixedDeltaTime) / 500.0);
              kmon.problem = Math.Max(kmon.problem, 0.0);
            }
            else
            {
              // degenerate
              kmon.problem += r.degeneration * TimeWarp.fixedDeltaTime * k * variance;
            }
          }
          else
          {
            // degenerate
            kmon.problem += r.degeneration * TimeWarp.fixedDeltaTime * k * variance;
          }


          // determine message variant
          uint variant = vi.temperature < Settings.SurvivalTemperature ? 0 : 1u;


          // kill kerbal if necessary
          if (kmon.problem >= r.fatal_threshold)
          {
            if (r.fatal_message.Length > 0)
              Message.Post(r.breakdown ? Severity.breakdown : Severity.fatality, Lib.ExpandMsg(r.fatal_message, v, c, variant));

            if (r.breakdown)
            {
              Kerbalism.Breakdown(v, c);
              kmon.problem = r.danger_threshold * 1.01; //< move back to danger threshold
            }
            else
            {
              Kill(v, c);
            }
          }
          // show messages
          else if (kmon.problem >= r.danger_threshold && kmon.message < 2)
          {
            if (r.danger_message.Length > 0) Message.Post(Severity.danger, Lib.ExpandMsg(r.danger_message, v, c, variant));
            kmon.message = 2;
          }
          else if (kmon.problem >= r.warning_threshold && kmon.message < 1)
          {
            if (r.warning_message.Length > 0) Message.Post(Severity.warning, Lib.ExpandMsg(r.warning_message, v, c, variant));
            kmon.message = 1;
          }
          else if (kmon.problem < r.warning_threshold && kmon.message > 0)
          {
            if (r.relax_message.Length > 0) Message.Post(Severity.relax, Lib.ExpandMsg(r.relax_message, v, c, variant));
            kmon.message = 0;
          }
        }
      }
    }
  }


  public void Update()
  {
    // mute/unmute messages with keyboard
    // done in update because unity is a mess
    if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyUp(KeyCode.N))
    {
      if (!Message.IsMuted())
      {
        Message.Post("Messages muted", "Be careful out there");
        Message.Mute();
      }
      else
      {
        Message.Unmute();
        Message.Post("Messages unmuted");
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

    // do nothing if paused
    if (Lib.IsPaused()) return;

    // get CLS
    // note: this can't be cached
    ConnectedLivingSpace.ICLSAddon cls = CLS.GetCLS();

    // if there is an active vessel
    Vessel av = FlightGlobals.ActiveVessel;
    if (av != null)
    {
      // set control locks if necessary
      // note: this will lock control on unmanned debris
      setLocks(av);

      // manage resque mission mechanics
      if (Lib.IsVessel(av)) manageResqueMission(av);
    }

    // for each vessel
    foreach(Vessel v in FlightGlobals.Vessels)
    {
      // skip invalid vessels
      if (!Lib.IsVessel(v)) continue;

      // skip resque missions
      if (Lib.IsResqueMission(v)) continue;

      // skip dead eva kerbals
      if (EVA.IsDead(v)) continue;

      // get vessel data
      vessel_data vd = DB.VesselData(v.id);

      // get vessel info
      vessel_info vi = Cache.VesselInfo(v);

      // update connected spaces using CLS, for QoL and Radiation mechanics
      updateConnectedSpaces(v, cls);

      // show belt warnings
      beltWarnings(v, vd);

      // simulate rules
      applyRules(v, vd, vi);
    }

    // decay debris orbits
    atmosphereDecay();

    // add progress descriptions to technologies
    techDescriptions();
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


  // trigger a random breakdown event
  public static void Breakdown(Vessel v, ProtoCrewMember c)
  {
    // constants
    const double food_penalty = 0.2;        // proportion of food lost on 'depressed'
    const double oxygen_penalty = 0.2;      // proportion of oxygen lost on 'wrong_valve'

    // get info
    double food_amount = Lib.GetResourceAmount(v, "Food");
    double oxygen_amount = Lib.GetResourceAmount(v, "Oxygen");

    // compile list of events with condition satisfied
    List<KerbalBreakdown> events = new List<KerbalBreakdown>();
    events.Add(KerbalBreakdown.mumbling); //< do nothing, here so there is always something that can happen
    if (Lib.CrewCount(v) > 1) events.Add(KerbalBreakdown.argument); //< do nothing, add some variation to messages
    if (Lib.HasData(v)) events.Add(KerbalBreakdown.fat_finger);
    if (Malfunction.CanMalfunction(v)) events.Add(KerbalBreakdown.rage);
    if (food_amount > double.Epsilon) events.Add(KerbalBreakdown.depressed);
    if (oxygen_amount > double.Epsilon) events.Add(KerbalBreakdown.wrong_valve);

    // choose a breakdown event
    KerbalBreakdown breakdown = events[Lib.RandomInt(events.Count)];

    // generate message
    string text = "";
    string subtext = "";
    switch(breakdown)
    {
      case KerbalBreakdown.mumbling:    text = "$ON_VESSEL$KERBAL has been in space for too long"; subtext = "Mumbling incoherently"; break;
      case KerbalBreakdown.argument:    text = "$ON_VESSEL$KERBAL had an argument with the rest of the crew"; subtext = "Morale is degenerating at an alarming rate"; break;
      case KerbalBreakdown.fat_finger:  text = "$ON_VESSEL$KERBAL is pressing buttons at random on the control panel"; subtext = "Science data has been lost"; break;
      case KerbalBreakdown.rage:        text = "$ON_VESSEL$KERBAL is possessed by a blind rage"; subtext = "A component has been damaged"; break;
      case KerbalBreakdown.depressed:   text = "$ON_VESSEL$KERBAL is not respecting the rationing guidelines"; subtext = "Food has been lost"; break;
      case KerbalBreakdown.wrong_valve: text = "$ON_VESSEL$KERBAL opened the wrong valve"; subtext = "Oxygen has been lost"; break;
    }

    // post message first so this one is shown before malfunction message
    Message.Post(Severity.breakdown, Lib.ExpandMsg(text, v, c), subtext);

    // trigger the event
    switch(breakdown)
    {
      case KerbalBreakdown.mumbling: break; // do nothing
      case KerbalBreakdown.argument: break; // do nothing
      case KerbalBreakdown.fat_finger: Lib.RemoveData(v); break;
      case KerbalBreakdown.rage: Malfunction.CauseMalfunction(v); break;
      case KerbalBreakdown.depressed: Lib.RequestResource(v, "Food", food_amount * food_penalty); break;
      case KerbalBreakdown.wrong_valve: Lib.RequestResource(v, "Oxygen", oxygen_amount * oxygen_penalty); break;
    }

    // remove reputation
    if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
    {
      Reputation.Instance.AddReputation(-Settings.BreakdownReputationPenalty, TransactionReasons.Any);
    }
  }


  // return per-kerbal variance, in the range [1-variance,1+variance]
  public static double Variance(ProtoCrewMember c, double variance)
  {
    // get a value in [0..1] range associated with a kerbal
    double k = (double)Lib.Hash32(c.name.Replace(" Kerman", "")) / (double)UInt32.MaxValue;

    // move in [-1..+1] range
    k = k * 2.0 - 1.0;

    // return kerbal-specific variance in range [1-n .. 1+n]
    return 1.0 + variance * k;
  }


  // --------------------------------------------------------------------------
  // THE HOOKS
  // --------------------------------------------------------------------------
  // Feel free to call these directly by reflection.
  // Anything prefixed with 'hook_' is not going to change in new versions.


  // hook: Message()
  public static void hook_Message(string msg)
  {
    Message.Post(msg);
  }


  // hook: Kill()
  public static void hook_Kill(Vessel v, ProtoCrewMember c)
  {
    if (!Lib.IsVessel(v)) return;
    if (!DB.Ready()) return;
    if (!DB.Vessels().ContainsKey(v.id)) return;
    if (!DB.Kerbals().ContainsKey(c.name)) return;

    Kill(v, c);
  }


  // hook: Breakdown
  public static void hook_Breakdown(Vessel v, ProtoCrewMember c)
  {
    if (!Lib.IsVessel(v)) return;
    if (!DB.Ready()) return;
    if (!DB.Vessels().ContainsKey(v.id)) return;
    if (!DB.Kerbals().ContainsKey(c.name)) return;

    Breakdown(v, c);
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
    foreach(var p in Kerbalism.rules)
    {
      var r = p.Value;
      if (r.modifier.Contains("radiation"))
      {
        var kmon = DB.KmonData(k_name, r.name);
        kmon.problem = Math.Max(kmon.problem + amount, 0.0);
      }
    }
  }


  // hook: InSunlight()
  public static bool hook_InSunlight(Vessel v)
  {
    return Lib.IsVessel(v) && Cache.VesselInfo(v).sunlight;
  }


  // hook: Breathable()
  public static bool hook_Breathable(Vessel v)
  {
    return Lib.IsVessel(v) && Cache.VesselInfo(v).breathable;
  }


  // hook: RadiationLevel()
  public static double hook_RadiationLevel(Vessel v)
  {
    if (!Lib.IsVessel(v)) return 0.0;
    vessel_info vi = Cache.VesselInfo(v);
    return vi.cosmic_radiation + vi.storm_radiation + vi.belt_radiation;
  }


  // hook: LinkStatus()
  public static uint hook_LinkStatus(Vessel v)
  {
    link_data ld = Signal.Link(v);
    switch(ld.status)
    {
      case link_status.direct_link: return 2u;
      case link_status.indirect_link: return 1u;
      default: return 0; // no_antenna, no_link
    }
  }


  // hook: Malfunctions()
  public static uint hook_Malfunctions(Vessel v)
  {
    return Lib.IsVessel(v) ? Malfunction.MaxMalfunction(v) : 0;
  }


  // hook: StormIncoming()
  public static bool hook_StormIncoming(Vessel v)
  {
    return Lib.IsVessel(v) && Storm.Incoming(Lib.PlanetarySystem(v.mainBody));
  }


  // hook: StormInProgress()
  public static bool hook_StormInProgress(Vessel v)
  {
    return Lib.IsVessel(v) && Storm.InProgress(Lib.PlanetarySystem(v.mainBody));
  }


  // hook: InsideMagnetosphere()
  public static bool hook_InsideMagnetosphere(Vessel v)
  {
    return Lib.IsVessel(v) && Radiation.InsideMagnetosphere(v);
  }


  // hook: InsideBelt()
  public static bool hook_InsideBelt(Vessel v)
  {
    return Lib.IsVessel(v) && Radiation.InsideBelt(v);
  }


  // hook: LivingSpace()
  public static double hook_LivingSpace(string k_name)
  {
    if (!DB.Ready()) return 1.0;
    if (!DB.Kerbals().ContainsKey(k_name)) return 1.0;
    return DB.KerbalData(k_name).living_space;
  }


  // hook: Entertainment()
  public static double hook_Entertainment(string k_name)
  {
    if (!DB.Ready()) return 1.0;
    if (!DB.Kerbals().ContainsKey(k_name)) return 1.0;
    return DB.KerbalData(k_name).entertainment;
  }


  // hook: Shielding()
  public static double hook_Shielding(string k_name)
  {
    if (!DB.Ready()) return 0.0;
    if (!DB.Kerbals().ContainsKey(k_name)) return 0.0;
    return DB.KerbalData(k_name).shielding;
  }


  // hook: Malfunctioned()
  public static bool hook_Malfunctioned(Part part)
  {
    foreach(var m in part.FindModulesImplementing<Malfunction>())
    {
      if (m.malfunctions > 0) return true;
    }
    return false;
  }


  // hook: Repair()
  public static void hook_Repair(Part part)
  {
    foreach(var m in part.FindModulesImplementing<Malfunction>())
    {
      m.Repair();
    }
  }
}


} // KERBALISM
