// ====================================================================================================================
// detect mods and features, parse the rules, manage the callbacks, provide the hooks, and more
// ====================================================================================================================


using System;
using System.Collections.Generic;
using System.Reflection;
using KSP.UI.Screens;
using UnityEngine;


namespace KERBALISM {


[KSPAddon(KSPAddon.Startup.MainMenu, true)]
public sealed class Kerbalism : MonoBehaviour
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
    public bool BackgroundProcessing;
    public bool RealFuels;
    public bool DeepFreeze;
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
  public static List<Rule> rules = new List<Rule>(32);

  // all the rules with a resource different than Electric Charge
  public static List<Rule> supply_rules = new List<Rule>(32);

  // the rule relative to EC, if any
  public static Rule ec_rule = null;

  // the rule relative to temperature, if any
  public static Rule temp_rule = null;

  // the rule relative to qol, if any
  public static Rule qol_rule = null;

  // the rule relative to radiation, if any
  public static Rule rad_rule = null;

  // 1 AU: distance from home body to sun
  public static double AU = 0.0;

  // equivalent to TimeWarp.fixedDeltaTime
  // note: stored here to avoid converting it to double every time
  public static double elapsed_s;

  // number of steps from last warp blending
  public static uint warp_blending;


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
    detected_mods.BackgroundProcessing = Lib.ConfigValue(mods, "BackgroundProcessing", false);
    detected_mods.RealFuels = Lib.ConfigValue(mods, "RealFuels", false);
    detected_mods.DeepFreeze = Lib.ConfigValue(mods, "DeepFreeze", false);
    Lib.Log("detected:");
    Lib.Log("- SCANsat: " + detected_mods.SCANsat);
    Lib.Log("- CLS: " + detected_mods.CLS);
    Lib.Log("- KIS: " + detected_mods.KIS);
    Lib.Log("- CRP: " + detected_mods.CRP);
    Lib.Log("- RemoteTech: " + detected_mods.RemoteTech);
    Lib.Log("- AntennaRange: " + detected_mods.AntennaRange);
    Lib.Log("- DangIt: " + detected_mods.DangIt);
    Lib.Log("- BackgroundProcessing: " + detected_mods.BackgroundProcessing);
    Lib.Log("- RealFuels: " + detected_mods.RealFuels);
    Lib.Log("- DeepFreeze: " + detected_mods.DeepFreeze);

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
      if (r.name.Length > 0 && rules.Find(k => k.name == r.name) == null)
      {
        rules.Add(r);
        if (r.resource_name == "ElectricCharge") ec_rule = r;
        if (r.modifier.Contains("temperature") && r.resource_name == "ElectricCharge") temp_rule = r;
        if (r.modifier.Contains("qol")) qol_rule = r;
        if (r.modifier.Contains("radiation")) rad_rule = r;
        if (r.resource_name.Length > 0 && reslib.Contains(r.resource_name) && r.resource_name != "ElectricCharge") supply_rules.Add(r);
      }
    }
    Lib.Log("rules:");
    foreach(Rule r in rules)
    {
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
        foreach(Rule r in rules)
        {
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
        foreach(Rule r in rules)
        {
          // add rule's waste resource
          double waste_per_day = (r.rate / (r.interval > 0.0 ? r.interval : 1.0)) * daylen * r.waste_ratio;
          double waste_amount = waste_per_day * r.waste_buffer * crew_capacity;
          if (r.waste_name.Length > 0 && waste_amount > double.Epsilon && reslib.Contains(r.waste_name) && !prefab.Resources.Contains(r.waste_name))
          {
            var res = new ConfigNode("RESOURCE");
            res.AddValue("name", r.waste_name);
            res.AddValue("amount", "0.0");
            res.AddValue("maxAmount", waste_amount);
            if (r.hidden_waste)
            {
              res.AddValue("isTweakable", false.ToString());
              res.AddValue("isVisible", false.ToString());
            }
            if (r.massless_waste)
            {
              res.AddValue("density", 0.0.ToString());
              res.AddValue("unitCost", 0.0.ToString());
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

    // add modules to EVA vessel part prefab
    // note: try..catch travesty required to avoid spurious exception, that seem to have no negative effects
    // note: dummy test for null char required to avoid compiler warning
    try { PartLoader.getPartInfoByName("kerbalEVA").partPrefab.AddModule("EVA"); } catch(Exception ex) { if (ex.Message.Contains("\0")) {} }
    try { PartLoader.getPartInfoByName("kerbalEVAfemale").partPrefab.AddModule("EVA"); } catch(Exception ex) { if (ex.Message.Contains("\0")) {} }

    // precompute 1 AU
    AU = Lib.PlanetarySystem(FlightGlobals.GetHomeBody()).orbit.semiMajorAxis;
  }


  void toEVA(GameEvents.FromToAction<Part, Part> data)
  {
    // use Hydrazine instead of MonoPropellant if RealFuel is installed
    string monoprop_name = detected_mods.RealFuels ? "Hydrazine" : "MonoPropellant";

    // determine if inside breathable atmosphere
    // note: the user can force the helmet + oxygen by pressing shift when going on eva
    bool breathable = Sim.Breathable(data.from.vessel) && !(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));

    // get total crew in the origin vessel
    double tot_crew = (double)data.from.vessel.GetVesselCrew().Count + 1.0;

    // EVA vessels start with 5 units of eva fuel, remove them
    data.to.RequestResource("EVA Propellant", 5.0);

    // determine how much MonoPropellant to get
    // note: never more that the 'share' of this kerbal
    double monoprop = Math.Min(ResourceCache.Info(data.from.vessel, monoprop_name).amount / tot_crew, Settings.MonoPropellantOnEVA);

    // get monoprop from the vessel
    monoprop = data.from.RequestResource(monoprop_name, monoprop);

    // transfer monoprop to the EVA kerbal
    data.to.RequestResource("EVA Propellant", -monoprop);

    // show warning if there isn't monoprop in the eva suit
    if (monoprop <= double.Epsilon && !Lib.Landed(data.from.vessel))
    {
      Message.Post(Severity.danger, Lib.BuildString("There isn't any <b>", monoprop_name, "</b> in the EVA suit", "Don't let the ladder go!"));
    }

    // manage resources from rules
    foreach(Rule r in rules)
    {
      if (r.resource_name.Length == 0 || r.on_eva <= double.Epsilon) continue;

      // determine amount to take, never more that his own share
      double amount = Math.Min(ResourceCache.Info(data.from.vessel, r.resource_name).amount / tot_crew, r.on_eva);

      // deal with breathable modifier
      if (breathable && r.modifier.Contains("breathable")) continue;

      // remove resource from the vessel
      amount = data.from.RequestResource(r.resource_name, amount);

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

    // execute script on vessel computer
    if (DB.Ready()) DB.VesselData(data.from.vessel.id).computer.execute("run", "auto/eva_out", string.Empty, data.from.vessel);

    // mute messages for a couple seconds to avoid warning messages from the vessel resource amounts
    Message.MuteInternal();
    base.StartCoroutine(CallbackUtil.DelayedCallback(2.0f, Message.UnmuteInternal));

    // if vessel info is open, switch to the eva kerbal
    // note: for a single tick, the EVA vessel is not valid (sun_dist is zero)
    // this make IsVessel() return false, that in turn close the vessel info instantly
    // for this reason, we wait a small amount of time before switching the info window
    if (Info.IsOpen()) Info.Open(data.to.vessel);
  }


  void fromEVA(GameEvents.FromToAction<Part, Part> data)
  {
    // use Hydrazine instead of MonoPropellant if RealFuel is installed
    string monoprop_name = detected_mods.RealFuels ? "Hydrazine" : "MonoPropellant";

    // get any leftover EVA Fuel from EVA vessel
    double monoprop = data.from.Resources.list[0].amount;

    // add the leftover monoprop back to the pod
    data.to.RequestResource(monoprop_name, -monoprop);

    // manage resources in rules
    foreach(Rule r in rules)
    {
      if (r.resource_name.Length == 0 || r.on_eva <= double.Epsilon) continue;
      double leftover = ResourceCache.Info(data.from.vessel, r.resource_name).amount;
      data.to.RequestResource(r.resource_name, -leftover);
    }

    // forget vessel data
    DB.ForgetVessel(data.from.vessel.id);

    // purge vessel from resource cache
    ResourceCache.Purge(data.from.vessel.id);

    // execute script on vessel computer
    if (DB.Ready()) DB.VesselData(data.to.vessel.id).computer.execute("run", "auto/eva_in", string.Empty, data.to.vessel);

    // mute messages for a couple seconds to avoid warning messages from the vessel resource amounts
    Message.MuteInternal();
    base.StartCoroutine(CallbackUtil.DelayedCallback(2.0f, Message.UnmuteInternal));

    // if vessel info is open, switch to the vessel
    if (Info.IsOpen()) Info.Open(data.to.vessel);
  }


  void vesselRecovered(ProtoVessel vessel, bool b)
  {
    // note: this is called multiple times when a vessel is recovered

    // find out if this was an EVA kerbal and if it was dead
    bool is_eva_dead = false;
    foreach(ProtoPartSnapshot p in vessel.protoPartSnapshots)
    {
      foreach(ProtoPartModuleSnapshot m in p.modules)
      {
        is_eva_dead |= (m.moduleName == "EVA" && Lib.Proto.GetBool(m, "is_dead"));
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

    // TODO: add science data from recovered vessel
    // beware of double calls to this function

    // forget vessel data
    DB.ForgetVessel(vessel.vesselID);

    // purge vessel from resource cache
    ResourceCache.Purge(vessel.vesselID);
  }


  void vesselTerminated(ProtoVessel vessel)
  {
    // forget all kerbals data
    foreach(ProtoCrewMember c in vessel.GetVesselCrew()) DB.ForgetKerbal(c.name);

    // forget vessel data
    DB.ForgetVessel(vessel.vesselID);

    // purge vessel from resource cache
    ResourceCache.Purge(vessel.vesselID);
  }


  void vesselDestroyed(Vessel vessel)
  {
    // forget vessel data
    DB.ForgetVessel(vessel.id);

    // purge vessel from resource cache
    ResourceCache.Purge(vessel.id);

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
    // TODO: merge computer data from vessel A to vessel B on docking

    // forget vessel being docked
    if (DB.Ready()) DB.ForgetVessel(e.from.vessel.id);

    // purge vessel from resource cache
    ResourceCache.Purge(e.from.vessel.id);
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
      Message.Post(Lib.BuildString(title, "We have access to more efficient <b>CO2 scrubbers</b> and <b>recyclers</b> now"), "Our research efforts are paying off, after all");
    }
    if (features.malfunction && Array.IndexOf(Malfunction.manufacturing_quality.techs, data.host.techID) >= 0)
    {
      Message.Post(Lib.BuildString(title, "Advances in material science have led to improved <b>manufacturing quality</b>"), "New components will last longer in extreme environments");
    }
    if (features.signal && Array.IndexOf(Signal.signal_processing.techs, data.host.techID) >= 0)
    {
      Message.Post(Lib.BuildString(title, "Our scientists just made a breakthrough in <b>signal processing</b>"), "New and existing antennas range has improved");
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
        rnd.node_description.text += "\n\n<color=cyan>Improve scrubbers and recyclers efficiency</color>";
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


  void setLocks(Vessel v, vessel_info vi)
  {
    // lock controls for EVA death
    if (v.isEVA && vi.is_eva_dead)
    {
      InputLockManager.SetControlLock(ControlTypes.EVA_INPUT, "eva_dead_lock");
    }

    // lock controls for probes without signal
    if (vi.is_valid && !vi.link.linked && Settings.RemoteControlLink && v.GetCrewCount() == 0)
    {
      InputLockManager.SetControlLock(ControlTypes.ALL_SHIP_CONTROLS, "no_signal_lock");
      FlightInputHandler.state.mainThrottle = 0.0f;
    }
  }


  void manageResqueMission(Vessel v)
  {
    // true if we detected this was a resque mission vessel
    bool detected = false;

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
        // remember it
        detected = true;

        // flag the kerbal as non-resque
        // note: enable life support mechanics for the kerbal
        kd.resque = 0;

        // show a message
        Message.Post(Lib.BuildString("We found <b>", c.name, "</b>"), Lib.BuildString((c.gender == ProtoCrewMember.Gender.Male ? "He" : "She"), "'s still alive!"));
      }
    }

    // gift resources
    if (detected)
    {
      var reslib = PartResourceLibrary.Instance.resourceDefinitions;
      var parts = Lib.GetPartsRecursively(v.rootPart); //< what's the reason for this?

      // give the vessel some monoprop
      string monoprop_name = v.isEVA ? "EVA Propellant" : detected_mods.RealFuels ? "Hydrazine" : "MonoPropellant";
      foreach(var part in parts)
      {
        if (part.CrewCapacity > 0 || part.FindModuleImplementing<KerbalEVA>() != null)
        {
          if (part.Resources.list.Find(k => k.resourceName == monoprop_name) == null)
          {
            Lib.SetupResource(part, monoprop_name, 0.0, Settings.MonoPropellantOnResque);
          }
          break;
        }
      }
      ResourceCache.Produce(v, monoprop_name, Settings.MonoPropellantOnResque);

      // give the vessel some supplies
      foreach(Rule r in rules)
      {
        if (r.resource_name.Length == 0 || r.on_resque <= double.Epsilon || !reslib.Contains(r.resource_name)) continue;
        foreach(var part in parts)
        {
          if (part.CrewCapacity > 0 || part.FindModuleImplementing<KerbalEVA>() != null)
          {
            if (part.Resources.list.Find(k => k.resourceName == r.resource_name) == null)
            {
              Lib.SetupResource(part, r.resource_name, 0.0, r.on_resque);
            }
            break;
          }
        }
        ResourceCache.Produce(v, r.resource_name, r.on_resque);
      }
    }
  }


  void updateConnectedSpaces(Vessel v, vessel_info vi)
  {
    // get CLS handler
    var cls = CLS.get();

    // calculate whole-space
    if (cls == null)
    {
      double living_space = QualityOfLife.LivingSpace((uint)vi.crew_count, (uint)vi.crew_capacity);
      double entertainment = QualityOfLife.Entertainment(v);
      double shielding = Radiation.Shielding(v);

      foreach(var c in v.loaded ? v.GetVesselCrew() : v.protoVessel.GetVesselCrew())
      {
        kerbal_data kd = DB.KerbalData(c.name);
        kd.living_space = living_space;
        kd.entertainment = entertainment;
        kd.shielding = shielding;
        kd.space_name = "";
      }
    }
    // calculate connected-space
    // note: avoid problem at scene change
    else if (cls.Vessel != null && cls.Vessel.Spaces.Count > 0)
    {
      // calculate internal spaces
      foreach(var space in cls.Vessel.Spaces)
      {
        double living_space = QualityOfLife.LivingSpace(space);
        double entertainment = QualityOfLife.Entertainment(space);
        double shielding = Radiation.Shielding(space);

        foreach(var c in space.Crew)
        {
          kerbal_data kd = DB.KerbalData(c.Kerbal.name);
          kd.living_space = living_space;
          kd.entertainment = entertainment;
          kd.shielding = shielding;
          kd.space_name = space.Name;
        }
      }
    }
  }


  public void Update()
  {
    bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
    bool alt  = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.LeftAlt);

    // mute/unmute messages with keyboard
    // done in update because unity is a mess
    if (ctrl && Input.GetKeyDown(KeyCode.N))
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

    // toggle body info window with keyboard
    if (alt && Input.GetKeyDown(KeyCode.N))
    {
      BodyInfo.Toggle();
    }

    // add progress descriptions to technologies
    techDescriptions();
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

    // maintain elapsed_s, converting to double only once
    // and detect warp blending
    double fixedDeltaTime = TimeWarp.fixedDeltaTime;
    if (Math.Abs(fixedDeltaTime - elapsed_s) > double.Epsilon) warp_blending = 0;
    else ++warp_blending;
    elapsed_s = fixedDeltaTime;

    // if there is an active vessel
    Vessel v = FlightGlobals.ActiveVessel;
    if (v != null)
    {
      // get info from cache
      vessel_info vi = Cache.VesselInfo(v);

      // set control locks if necessary
      // note: this will lock control on unmanned debris
      setLocks(v, vi);

      // skip debris/asteroids/flags/...
      // skip eva dead kerbals (rationale: getting the kerbal data will create it again, leading to spurious resque mission detection)
      if (vi.is_vessel && !vi.is_eva_dead)
      {
        // manage resque mission mechanics
        manageResqueMission(v);
      }

      // skip invalid vessels
      if (vi.is_valid)
      {
        // update connected spaces using CLS, for QoL and Radiation mechanics
        updateConnectedSpaces(v, vi);
      }
    }
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
    const double res_penalty = 0.1;        // proportion of food lost on 'depressed' and 'wrong_valve'

    // get info
    Rule supply = supply_rules.Count > 0 ? supply_rules[Lib.RandomInt(supply_rules.Count)] : null;
    resource_info res = supply != null ? ResourceCache.Info(v, supply.resource_name) : null;

    // compile list of events with condition satisfied
    List<KerbalBreakdown> events = new List<KerbalBreakdown>();
    events.Add(KerbalBreakdown.mumbling); //< do nothing, here so there is always something that can happen
    if (Lib.CrewCount(v) > 1) events.Add(KerbalBreakdown.argument); //< do nothing, add some variation to messages
    if (Lib.HasData(v)) events.Add(KerbalBreakdown.fat_finger);
    if (Malfunction.CanMalfunction(v)) events.Add(KerbalBreakdown.rage);
    if (supply != null && res.amount > double.Epsilon)
    {
      events.Add(KerbalBreakdown.depressed);
      events.Add(KerbalBreakdown.wrong_valve);
    }

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
      case KerbalBreakdown.depressed:   text = "$ON_VESSEL$KERBAL is not respecting the rationing guidelines"; subtext = supply.resource_name + " has been lost"; break;
      case KerbalBreakdown.wrong_valve: text = "$ON_VESSEL$KERBAL opened the wrong valve"; subtext = supply.resource_name + " has been lost"; break;
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
      case KerbalBreakdown.depressed:
      case KerbalBreakdown.wrong_valve: res.Consume(res.amount * res_penalty); break;
    }

    // remove reputation
    if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
    {
      Reputation.Instance.AddReputation(-Settings.BreakdownReputationPenalty, TransactionReasons.Any);
    }
  }


  // decay unloaded vessels inside atmosphere
  public static void atmosphereDecay(Vessel v, vessel_info vi, double elapsed_s)
  {
    CelestialBody body = v.mainBody;
    if (Settings.AtmosphereDecay && body.atmosphere && v.altitude < body.atmosphereDepth && !vi.landed)
    {
      // get pressure
      double p = body.GetPressure(v.altitude);

      // if inside some kind of atmosphere
      if (p > double.Epsilon)
      {
        // calculate decay speed to be 1km/s per-kPa
        double decay_speed = 1000.0 * p;

        // decay the orbit
        v.orbit.semiMajorAxis -= decay_speed * elapsed_s;
      }
    }
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
    if (!Cache.VesselInfo(v).is_valid) return;
    if (!DB.Ready()) return;
    if (!DB.Vessels().ContainsKey(v.id)) return;
    if (!DB.Kerbals().ContainsKey(c.name)) return;

    Kill(v, c);
  }

  // hook: Breakdown
  public static void hook_Breakdown(Vessel v, ProtoCrewMember c)
  {
    if (!Cache.VesselInfo(v).is_valid) return;
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
    foreach(Rule r in Kerbalism.rules)
    {
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
    return Cache.VesselInfo(v).sunlight > double.Epsilon;
  }

  // hook: Breathable()
  public static bool hook_Breathable(Vessel v)
  {
    return Cache.VesselInfo(v).breathable;
  }

  // hook: RadiationLevel()
  public static double hook_RadiationLevel(Vessel v)
  {
    vessel_info vi = Cache.VesselInfo(v);
    return vi.radiation;
  }

  // hook: LinkStatus()
  public static uint hook_LinkStatus(Vessel v)
  {
    vessel_info vi = Cache.VesselInfo(v);
    if (!vi.is_valid) return 0;
    switch(vi.link.status)
    {
      case link_status.direct_link: return 2u;
      case link_status.indirect_link: return 1u;
      default: return 0; // no_antenna, no_link
    }
  }

  // hook: Malfunctions()
  public static uint hook_Malfunctions(Vessel v)
  {
    return Cache.VesselInfo(v).max_malfunction;
  }

  // hook: StormIncoming()
  public static bool hook_StormIncoming(Vessel v)
  {
    return Cache.VesselInfo(v).is_valid && Storm.Incoming(v);
  }

  // hook: StormInProgress()
  public static bool hook_StormInProgress(Vessel v)
  {
    return Cache.VesselInfo(v).is_valid && Storm.InProgress(v);
  }

  // hook: InsideMagnetosphere()
  public static bool hook_InsideMagnetosphere(Vessel v)
  {
    // note: this doesn't consider the sun heliopause
    vessel_info vi = Cache.VesselInfo(v);
    return vi.is_valid && vi.inside_pause;
  }

  // hook: InsideBelt()
  public static bool hook_InsideBelt(Vessel v)
  {
    vessel_info vi = Cache.VesselInfo(v);
    return vi.is_valid && vi.inside_belt;
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
