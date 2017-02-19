using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class Harvester : PartModule, IAnimatedModule, IModuleInfo, ISpecifics, IContractObjectiveModule
{
  // config
  [KSPField] public string title = string.Empty;            // name to show on ui
  [KSPField] public int    type = 0;                        // type of resource
  [KSPField] public string resource = string.Empty;         // resource to extract
  [KSPField] public double min_abundance = 0.0;             // minimal abundance required, in percentual
  [KSPField] public double min_pressure = 0.0;              // minimal pressure required, in kPA
  [KSPField] public double rate = 0.0;                      // rate of resource to extract at 100% abundance
  [KSPField] public double ec_rate = 0.0;                   // rate of ec consumption per-second, irregardless of abundance
  [KSPField] public string drill = string.Empty;            // the drill transform


  // persistence
  [KSPField(isPersistant = true)] public bool deployed;     // true if the harvester is deployed
  [KSPField(isPersistant = true)] public bool running;      // true if the harvester is running
  [KSPField(isPersistant = true)] public bool starved;      // true if the resource can't be harvested


  // show abundance level
  [KSPField(guiActive = false, guiName = "_")] public string Abundance;


  public override void OnStart(StartState state)
  {
    // assume deployed if there is no animator
    deployed |= part.FindModuleImplementing<ModuleAnimationGroup>() == null;
  }


  public void Update()
  {
    // in editor and flight, update ui button label
    Events["Toggle"].guiName = Lib.StatusToggle(title, running ? "running" : "stopped");

    // if in flight, and the stock planet resource system is online
    if (Lib.IsFlight() && ResourceMap.Instance != null)
    {
      // shortcut
      CelestialBody body = vessel.mainBody;

      // determine if overall situation is valid for extraction
      bool valid = false;
      if (deployed)
      {
        switch(type)
        {
          case 0: valid = vessel.Landed && GroundContact(); break;
          case 1: valid = body.ocean && (vessel.Splashed || vessel.altitude < 0.0); break;
          case 2: valid = body.atmosphere && vessel.altitude < body.atmosphereDepth; break;
          case 3: valid = vessel.altitude > body.atmosphereDepth || !body.atmosphere; break;
        }
      }

      // if situation is valid
      double abundance = 0.0;
      if (valid)
      {
        // get abundance
        AbundanceRequest request = new AbundanceRequest
        {
          ResourceType = (HarvestTypes)type,
          ResourceName = resource,
          BodyId = body.flightGlobalsIndex,
          Latitude = vessel.latitude,
          Longitude = vessel.longitude,
          Altitude = vessel.altitude,
          CheckForLock = false
        };
        abundance = ResourceMap.Instance.GetAbundance(request);
      }

      // determine if resources can be harvested
      // - invalid conditions
      // - abundance below threshold
      // - pressure below threshold
      starved = !valid || abundance <= min_abundance || (type == 2 && body.GetPressure(vessel.altitude) <= min_pressure);

      // update ui
      Events["Toggle"].guiActive = valid;
      Fields["Abundance"].guiActive = valid;
      Fields["Abundance"].guiName = Lib.BuildString(resource, " abundance");
      Abundance = abundance > double.Epsilon ? Lib.HumanReadablePerc(abundance, "F2") : "none";
    }
  }


  public void FixedUpdate()
  {
    if (Lib.IsEditor()) return;

    if (deployed && running && !starved)
    {
      resource_recipe recipe = new resource_recipe();
      recipe.Input("ElectricCharge", ec_rate * Kerbalism.elapsed_s);
      recipe.Output(resource, rate * Kerbalism.elapsed_s, true);
      ResourceCache.Transform(vessel, recipe);
    }
  }


  public static void BackgroundUpdate(Vessel v, ProtoPartModuleSnapshot m, Harvester harvester, double elapsed_s)
  {
    if (Lib.Proto.GetBool(m, "deployed") && Lib.Proto.GetBool(m, "running") && !Lib.Proto.GetBool(m, "starved"))
    {
      resource_recipe recipe = new resource_recipe();
      recipe.Input("ElectricCharge", harvester.ec_rate * elapsed_s);
      recipe.Output(harvester.resource, harvester.rate * elapsed_s, true);
      ResourceCache.Transform(v, recipe);
    }
  }


  [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "_", active = true)]
  public void Toggle()
  {
    running = !running;
  }


  bool GroundContact()
  {
    // hard-coded margin
    const double margin = 0.2;

    // if there is no drill transform specified, assume ground contact
    if (drill.Length == 0) return true;

    // get terrain height below vessel center
    double terrain_height = Lib.TerrainHeight(vessel);

    // get drill transform position in world space
    Transform drill_transform = part.FindModelComponent<Transform>(drill);
    if (drill_transform == null) return true;

    // calculate drill distance from zero altitude surface
    double drill_altitude = (drill_transform.position - vessel.mainBody.position).magnitude - vessel.mainBody.Radius;

    // we have ground contact if the drill altitude is less than terrain height
    return terrain_height + margin > drill_altitude;
  }


  // action groups
  [KSPAction("Start/Stop Harvester")] public void Action(KSPActionParam param) { Toggle(); }


  // part tooltip
  public override string GetInfo()
  {
    // generate description
    string source = string.Empty;
    switch(type)
    {
      case 0: source = "the surface"; break;
      case 1: source = "the ocean"; break;
      case 2: source = "the atmosphere"; break;
      case 3: source = "space"; break;
    }
    string desc = Lib.BuildString("Extract ", resource, " from ", source);

    // generate tooltip info
    return Specs().info(desc);
  }

  // specifics support
  public Specifics Specs()
  {
    Specifics specs = new Specifics();
    specs.add("type", ((HarvestTypes)type).ToString());
    specs.add("resource", resource);
    if (min_abundance > double.Epsilon) specs.add("min abundance", Lib.HumanReadablePerc(min_abundance, "F2"));
    if (type == 2 && min_pressure > double.Epsilon) specs.add("min pressure", Lib.HumanReadablePressure(min_pressure));
    specs.add("extraction rate", Lib.HumanReadableRate(rate));
    if (ec_rate > double.Epsilon) specs.add("ec consumption", Lib.HumanReadableRate(ec_rate));
    return specs;
  }

  // animation group support
  public void EnableModule()      { deployed = true; }
  public void DisableModule()     { deployed = false; running = false; }
  public bool ModuleIsActive()    { return running && !starved; }
  public bool IsSituationValid()  { return true; }

  // module info support
  public string GetModuleTitle()  { return title; }
  public string GetPrimaryField() { return string.Empty; }
  public Callback<Rect> GetDrawModulePanelCallback() { return null; }

  // contract objective support
  public bool CheckContractObjectiveValidity()  { return true; }
  public string GetContractObjectiveType()      { return "Harvester"; }
}



} // KERBALISM




