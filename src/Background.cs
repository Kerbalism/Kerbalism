// ====================================================================================================================
// consume & produce resources for unloaded vessels
// ====================================================================================================================


using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;


namespace KERBALISM {


[KSPAddon(KSPAddon.Startup.MainMenu, true)]
public sealed class Background : MonoBehaviour
{
  // keep it alive
  Background() { DontDestroyOnLoad(this); }


  // return solar panel EC output
  // note: we ignore temperature curve, and make sure it is not relavant in the MM patch
  static double PanelOutput(Vessel vessel, ProtoPartSnapshot part, ModuleDeployableSolarPanel panel, Vector3d sun_dir, double sun_dist)
  {
    // if, for whatever reason, sun_dist is zero (or negative), we do not return any output
    if (sun_dist <= double.Epsilon) return 0.0;

    // shortcuts
    Quaternion rot = part.rotation;
    Vector3d normal = panel.part.FindModelComponent<Transform>(panel.raycastTransformName).forward;

    // calculate cosine factor
    // note: for gameplay reasons, we ignore tracking panel pivots
    double cosine_factor = panel.sunTracking ? 1.0 : Math.Max(Vector3d.Dot(sun_dir, (vessel.transform.rotation * rot * normal).normalized), 0.0);

    // calculate solar flux
    double solar_flux = Sim.SolarFlux(sun_dist);

    // finally, calculate output
    return panel.chargeRate * cosine_factor * (panel.useCurve ? panel.powerCurve.Evaluate((float)sun_dist) : solar_flux / Sim.SolarFluxAtHome());
  }


  static double CurvedPanelOutput(Vessel vessel, ProtoPartSnapshot part, Part prefab, PartModule m, Vector3d sun_dir, double sun_dist)
  {
    // if, for whatever reason, sun_dist is zero (or negative), we do not return any output
    if (sun_dist <= double.Epsilon) return 0.0;

    // shortcuts
    Quaternion rot = part.rotation;

    // get values from part
    string transform_name = Lib.ReflectionValue<string>(m, "PanelTransformName");
    float tot_rate = Lib.ReflectionValue<float>(m, "TotalEnergyRate");

    // get components
    Transform[] components = prefab.FindModelTransforms(transform_name);
    if (components.Length == 0) return 0.0;

    // calculate solar flux
    double solar_flux = Sim.SolarFlux(sun_dist);

    // for each one of the components the curved panel is composed of
    double output = 0.0;
    foreach(Transform t in components)
    {
      double cosine_factor = Math.Max(Vector3d.Dot(sun_dir, (vessel.transform.rotation * rot * t.forward.normalized).normalized), 0.0);
      output += (double)tot_rate / (double)components.Length * cosine_factor * solar_flux / Sim.SolarFluxAtHome();
    }
    return output;
  }





  // called at every simulation step
  public void FixedUpdate()
  {
    // do nothing if paused
    if (Lib.IsPaused()) return;

    // do nothing if DB isn't ready
    if (!DB.Ready()) return;

    // for each vessel
    foreach(Vessel v in FlightGlobals.Vessels)
    {
      // get vessel info from the cache
      vessel_info info = Cache.VesselInfo(v);

      // skip invalid vessels
      if (!info.is_vessel) continue;

      // skip loaded vessels
      if (v.loaded) continue;

      // get vessel data from the db
      vessel_data vd = DB.VesselData(v.id);

      // for each part
      foreach(ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
      {
        // store index of ModuleResourceConverter to process
        // rationale: a part can contain multiple resource converters
        int converter_index = 0;

        // get part prefab (required for module properties)
        Part part_prefab = PartLoader.getPartInfoByName(p.partName).partPrefab;

        // for each module
        foreach(ProtoPartModuleSnapshot m in p.modules)
        {
          // something weird is going on, skip this
          if (!part_prefab.Modules.Contains(m.moduleName)) continue;

          // process modules
          switch(m.moduleName)
          {
            case "ModuleCommand":
              ModuleCommand command = part_prefab.Modules[m.moduleName] as ModuleCommand;
              if (command != null) ProcessCommand(v, p, m, command);
              break;

            case "ModuleDeployableSolarPanel":
              ModuleDeployableSolarPanel panel = part_prefab.Modules[m.moduleName] as ModuleDeployableSolarPanel;
              if (panel != null) ProcessPanel(v, p, m, panel, info);
              break;

            case "ModuleGenerator":
              ModuleGenerator generator = part_prefab.Modules[m.moduleName] as ModuleGenerator;
              if (generator != null) ProcessGenerator(v, p, m, generator);
              break;

            case "ModuleResourceConverter":
            case "ModuleKPBSConverter":
            case "FissionReactor":
              var module_prefabs = part_prefab.Modules.GetModules<ModuleResourceConverter>();
              if (converter_index < module_prefabs.Count)
              {
                ModuleResourceConverter converter = module_prefabs[converter_index++] as ModuleResourceConverter;
                if (converter != null) ProcessConverter(v, p, m, converter);
              }
              break;

            case "ModuleResourceHarvester":
              ModuleResourceHarvester harvester = part_prefab.Modules[m.moduleName] as ModuleResourceHarvester;
              if (harvester != null) ProcessHarvester(v, p, m, harvester);
              break;

            case "ModuleAsteroidDrill":
              ModuleAsteroidDrill drill = part_prefab.Modules[m.moduleName] as ModuleAsteroidDrill;
              if (drill != null) ProcessAsteroidDrill(v, p, m, drill);
              break;

            case "ModuleScienceConverter":
              ModuleScienceConverter lab = part_prefab.Modules[m.moduleName] as ModuleScienceConverter;
              if (lab != null) ProcessLab(v, p, m, lab);
              break;

            case "SCANsat":
            case "ModuleSCANresourceScanner":
              ProcessScanner(v, p, m, part_prefab.Modules[m.moduleName], part_prefab, vd);
              break;

            case "ModuleCurvedSolarPanel":
              ProcessCurvedPanel(v, p, m, part_prefab.Modules[m.moduleName], part_prefab, info);
              break;

            case "FissionGenerator":
              ProcessFissionGenerator(v, p, m, part_prefab.Modules[m.moduleName]);
              break;

            case "ModuleRadioisotopeGenerator":
              ProcessRadioisotopeGenerator(v, p, m, part_prefab.Modules[m.moduleName]);
              break;

            case "Scrubber":
              Scrubber scrubber = part_prefab.Modules[m.moduleName] as Scrubber;
              if (scrubber != null) Scrubber.BackgroundUpdate(v, m, scrubber);
              break;

            case "Recycler":
              Recycler recycler = part_prefab.Modules[m.moduleName] as Recycler;
              if (recycler != null) Recycler.BackgroundUpdate(v, m, recycler);
              break;

            case "Greenhouse":
              Greenhouse greenhouse = part_prefab.Modules[m.moduleName] as Greenhouse;
              if (greenhouse != null) Greenhouse.BackgroundUpdate(v, m, greenhouse);
              break;

            case "GravityRing":
              GravityRing ring = part_prefab.Modules[m.moduleName] as GravityRing;
              if (ring != null) GravityRing.BackgroundUpdate(v, m, ring);
              break;

            case "Malfunction":
              Malfunction malfunction = part_prefab.Modules[m.moduleName] as Malfunction;
              if (malfunction != null) Malfunction.BackgroundUpdate(v, m, malfunction);
              break;
          }
        }
      }
    }
  }


  static void ProcessCommand(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, ModuleCommand command)
  {
    // do not consume if this is a MCM with no crew
    // rationale: for consistency, the game doesn't consume resources for MCM without crew in loaded vessels
    //            this make some sense: you left a vessel with some battery and nobody on board, you expect it to not consume EC
    if (command.minimumCrew == 0 || p.protoModuleCrew.Count > 0)
    {
      // for each input resource
      foreach(ModuleResource ir in command.inputResources)
      {
        // consume the resource
        Lib.Resource.Request(v, ir.name, ir.rate * TimeWarp.fixedDeltaTime);
      }
    }
  }


  static void ProcessPanel(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, ModuleDeployableSolarPanel panel, vessel_info info)
  {
    // determine if extended
    bool extended = m.moduleValues.GetValue("stateString") == ModuleDeployableSolarPanel.panelStates.EXTENDED.ToString();

    // if in sunlight and extended
    if (info.sunlight > double.Epsilon && extended)
    {
      // produce electric charge
      double output = PanelOutput(v, p, panel, info.sun_dir, info.sun_dist) * info.atmo_factor * info.sunlight;
      Lib.Resource.Request(v, "ElectricCharge", -output * TimeWarp.fixedDeltaTime * Malfunction.Penalty(p));
    }
  }


  static void ProcessGenerator(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, ModuleGenerator generator)
  {
    // determine if active
    bool activated = Lib.Proto.GetBool(m, "generatorIsActive");

    // if active
    if (activated)
    {
      // determine if vessel is full of all output resources
      bool full = true;
      foreach(var or in generator.outputList)
      {
        full &= (Cache.ResourceInfo(v, or.name).level >= 1.0 - double.Epsilon);
      }

      // if not full
      if (!full)
      {
        // calculate worst required resource percentual
        double worst_input = 1.0;
        foreach(var ir in generator.inputList)
        {
          double required = ir.rate * TimeWarp.fixedDeltaTime;
          double amount = Cache.ResourceInfo(v, ir.name).amount;
          worst_input = Math.Min(worst_input, amount / required);
        }

        // for each input resource
        foreach(var ir in generator.inputList)
        {
          // consume the resource
          Lib.Resource.Request(v, ir.name, ir.rate * worst_input * TimeWarp.fixedDeltaTime);
        }

        // for each output resource
        foreach(var or in generator.outputList)
        {
          // produce the resource
          Lib.Resource.Request(v, or.name, -or.rate * worst_input * TimeWarp.fixedDeltaTime * Malfunction.Penalty(p));
        }
      }
    }
  }


  static void ProcessConverter(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, ModuleResourceConverter converter)
  {
    // note: support multiple resource converters
    // note: ignore stock temperature mechanic of converters
    // note: ignore autoshutdown
    // note: using hard-coded crew bonus values from the wiki because the module data make zero sense (DERP ALERT)
    // note: non-mandatory resources 'dynamically scale the ratios', that is exactly what mandatory resources do too (DERP ALERT)
    // note: 'undo' stock behaviour by forcing lastUpdateTime to now (to minimize overlapping calculations from this and stock post-facto simulation)
    // note: support PlanetaryBaseSystem converters
    // note: support NearFuture reactors

    // determine if active
    bool activated = Lib.Proto.GetBool(m, "IsActivated");

    // if active
    if (activated)
    {
      // deduce crew bonus
      int exp_level = -1;
      if (converter.UseSpecialistBonus)
      {
        foreach(ProtoCrewMember c in v.protoVessel.GetVesselCrew())
          exp_level = Math.Max(exp_level, c.trait == converter.Specialty ? c.experienceLevel : -1);
      }
      double exp_bonus = exp_level < 0 ? 1.0 : 5.0 + (double)exp_level * 4.0;

      // determine if vessel is full of all output resources
      bool full = true;
      foreach(var or in converter.outputList)
      {
        full &= (Cache.ResourceInfo(v, or.ResourceName).level >= converter.FillAmount - double.Epsilon);
      }

      // if not full
      if (!full)
      {
        // calculate worst required resource percentual
        double worst_input = 1.0;
        foreach(var ir in converter.inputList)
        {
          double required = ir.Ratio * TimeWarp.fixedDeltaTime;
          double amount = Cache.ResourceInfo(v, ir.ResourceName).amount;
          worst_input = Math.Min(worst_input, amount / required);
        }

        // for each input resource
        foreach(var ir in converter.inputList)
        {
          // consume the resource
          Lib.Resource.Request(v, ir.ResourceName, ir.Ratio * worst_input * TimeWarp.fixedDeltaTime);
        }

        // for each output resource
        foreach(var or in converter.outputList)
        {
          // produce the resource
          Lib.Resource.Request(v, or.ResourceName, -or.Ratio * worst_input * TimeWarp.fixedDeltaTime * exp_bonus * Malfunction.Penalty(p));
        }
      }

      // undo stock behaviour by forcing last_update_time to now
      Lib.Proto.Set(m, "lastUpdateTime", Planetarium.GetUniversalTime());
    }
  }


  static void ProcessHarvester(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, ModuleResourceHarvester harvester)
  {
    // note: ignore stock temperature mechanic of harvesters
    // note: ignore autoshutdown
    // note: ignore depletion (stock seem to do the same)
    // note: using hard-coded crew bonus values from the wiki because the module data make zero sense (DERP ALERT)
    // note: 'undo' stock behaviour by forcing lastUpdateTime to now (to minimize overlapping calculations from this and stock post-facto simulation)

    // determine if active
    bool activated = Lib.Proto.GetBool(m, "IsActivated");

    // if active
    if (activated)
    {
      // deduce crew bonus
      int exp_level = -1;
      if (harvester.UseSpecialistBonus)
      {
        foreach(ProtoCrewMember c in v.protoVessel.GetVesselCrew())
          exp_level = Math.Max(exp_level, c.trait == harvester.Specialty ? c.experienceLevel : -1);
      }
      double exp_bonus = exp_level < 0 ? 1.0 : 5.0 + (double)exp_level * 4.0;

      // detect amount of ore in the ground
      AbundanceRequest request = new AbundanceRequest
      {
        Altitude = v.altitude,
        BodyId = v.mainBody.flightGlobalsIndex,
        CheckForLock = false,
        Latitude = v.latitude,
        Longitude = v.longitude,
        ResourceType = (HarvestTypes)harvester.HarvesterType,
        ResourceName = harvester.ResourceName
      };
      double abundance = ResourceMap.Instance.GetAbundance(request);

      // if there is actually something (should be if active when unloaded)
      if (abundance > harvester.HarvestThreshold)
      {
        // calculate worst required resource percentual
        double worst_input = 1.0;
        foreach(var ir in harvester.inputList)
        {
          double required = ir.Ratio * TimeWarp.fixedDeltaTime;
          double amount = Cache.ResourceInfo(v, ir.ResourceName).amount;
          worst_input = Math.Min(worst_input, amount / required);
        }

        // for each input resource
        foreach(var ir in harvester.inputList)
        {
          // consume the resource
          Lib.Resource.Request(v, ir.ResourceName, ir.Ratio * worst_input * TimeWarp.fixedDeltaTime);
        }

        // determine resource produced
        double res = abundance * harvester.Efficiency * exp_bonus * worst_input * Malfunction.Penalty(p);

        // accumulate ore
        Lib.Resource.Request(v, harvester.ResourceName, -res * TimeWarp.fixedDeltaTime);
      }

      // undo stock behaviour by forcing last_update_time to now
      Lib.Proto.Set(m, "lastUpdateTime", Planetarium.GetUniversalTime());
    }
  }


  static void ProcessAsteroidDrill(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, ModuleAsteroidDrill asteroid_drill)
  {
    // note: untested
    // note: ignore stock temperature mechanic of asteroid drills
    // note: ignore autoshutdown
    // note: using hard-coded crew bonus values from the wiki because the module data make zero sense (DERP ALERT)
    // note: 'undo' stock behaviour by forcing lastUpdateTime to now (to minimize overlapping calculations from this and stock post-facto simulation)

    // determine if active
    bool activated = Lib.Proto.GetBool(m, "IsActivated");

    // if active
    if (activated)
    {
      // deduce crew bonus
      int exp_level = -1;
      if (asteroid_drill.UseSpecialistBonus)
      {
        foreach(ProtoCrewMember c in v.protoVessel.GetVesselCrew())
          exp_level = Math.Max(exp_level, c.trait == asteroid_drill.Specialty ? c.experienceLevel : -1);
      }
      double exp_bonus = exp_level < 0 ? 1.0 : 5.0 + (double)exp_level * 4.0;

      // get asteroid data
      ProtoPartModuleSnapshot asteroid_info = null;
      ProtoPartModuleSnapshot asteroid_resource = null;
      foreach(ProtoPartSnapshot pp in v.protoVessel.protoPartSnapshots)
      {
        if (asteroid_info == null) asteroid_info = pp.modules.Find(k => k.moduleName == "ModuleAsteroidInfo");
        if (asteroid_resource == null) asteroid_resource = pp.modules.Find(k => k.moduleName == "ModuleAsteroidResource");
      }

      // if there is actually an asteroid attached to this active asteroid drill (it should)
      if (asteroid_info != null && asteroid_resource != null)
      {
        // get some data
        double mass_threshold = Lib.Proto.GetDouble(asteroid_info, "massThresholdVal");
        double mass = Lib.Proto.GetDouble(asteroid_info, "currentMassVal");
        double abundance = Lib.Proto.GetDouble(asteroid_resource, "abundance");
        string res_name = Lib.Proto.GetString(asteroid_resource, "resourceName");
        double res_density = PartResourceLibrary.Instance.GetDefinition(res_name).density;

        // if asteroid isn't depleted
        if (mass > mass_threshold && abundance > double.Epsilon)
        {
          // consume EC
          double ec_required = asteroid_drill.PowerConsumption * TimeWarp.fixedDeltaTime;
          double ec_consumed = Lib.Resource.Request(v, "ElectricCharge", ec_required);
          double ec_ratio = ec_consumed / ec_required;

          // determine resource extracted
          double res_amount = abundance * asteroid_drill.Efficiency * exp_bonus * ec_ratio * TimeWarp.fixedDeltaTime;

          // produce mined resource
          Lib.Resource.Request(v, res_name, -res_amount);

          // consume asteroid mass
          Lib.Proto.Set(asteroid_info, "currentMassVal", (mass - res_density * res_amount));
        }
      }

      // undo stock behaviour by forcing last_update_time to now
      Lib.Proto.Set(m, "lastUpdateTime", Planetarium.GetUniversalTime());
    }
  }


  static void ProcessLab(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, ModuleScienceConverter lab)
  {
    // note: we are only simulating the EC consumption
    // note: there is no easy way to 'stop' the lab when there isn't enough EC

    // determine if active
    bool activated = Lib.Proto.GetBool(m, "IsActivated");

    // if active
    if (activated)
    {
      Lib.Resource.Request(v, "ElectricCharge", lab.powerRequirement * TimeWarp.fixedDeltaTime);
    }
  }


  static void ProcessScanner(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, PartModule scanner, Part part_prefab, vessel_data vd)
  {
    // get ec consumption rate
    double power = Lib.ReflectionValue<float>(scanner, "power");
    double ec_required = power * TimeWarp.fixedDeltaTime;
    bool is_scanning = Lib.Proto.GetBool(m, "scanning");
    bool was_disabled = vd.scansat_id.Contains(p.flightID);

    // if its scanning
    if (is_scanning)
    {
      // consume ec
      double ec_consumed = Lib.Resource.Request(v, "ElectricCharge", ec_required);

      // if there isn't enough ec
      if (ec_consumed < ec_required * 0.99 && ec_required > double.Epsilon)
      {
        // unregister scanner
        SCANsat.stopScanner(v, m, part_prefab);
        is_scanning = false;

        // remember disabled scanner
        vd.scansat_id.Add(p.flightID);

        // give the user some feedback
        if (DB.VesselData(v.id).cfg_ec == 1)
          Message.Post(Lib.BuildString("SCANsat sensor was disabled on <b>", v.vesselName, "</b>"));
      }
    }
    // if it was disabled
    else if (vd.scansat_id.Contains(p.flightID))
    {
      // if there is enough ec
      if (Cache.ResourceInfo(v, "ElectricCharge").level > 0.25) //< re-enable at 25% EC
      {
        // re-enable the scanner
        SCANsat.resumeScanner(v, m, part_prefab);
        is_scanning = true;

        // give the user some feedback
        if (DB.VesselData(v.id).cfg_ec == 1)
          Message.Post(Lib.BuildString("SCANsat sensor resumed operations on <b>", v.vesselName, "</b>"));
      }
    }

    // forget active scanners
    if (is_scanning) vd.scansat_id.Remove(p.flightID);
  }


  static void ProcessCurvedPanel(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, PartModule curved_panel, Part part_prefab, vessel_info info)
  {
    // note: we assume deployed, this is a current limitation

    // if in sunlight
    if (info.sunlight > double.Epsilon)
    {
      double output = CurvedPanelOutput(v, p, part_prefab, curved_panel, info.sun_dir, info.sun_dist) * info.atmo_factor * info.sunlight;
      Lib.Resource.Request(v, "ElectricCharge", -output * TimeWarp.fixedDeltaTime * Malfunction.Penalty(p));
    }
  }


  static void ProcessFissionGenerator(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, PartModule fission_generator)
  {
    // note: ignore heat

    double power = Lib.ReflectionValue<float>(fission_generator, "PowerGeneration");
    var reactor = p.modules.Find(k => k.moduleName == "FissionReactor");
    double tweakable = reactor == null ? 1.0 : Lib.ConfigValue(reactor.moduleValues, "CurrentPowerPercent", 100.0) * 0.01;
    Lib.Resource.Request(v, "ElectricCharge", -power * tweakable * TimeWarp.fixedDeltaTime);
  }


  static void ProcessRadioisotopeGenerator(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, PartModule radioisotope_generator)
  {
    // note: doesn't support easy mode

    double half_life = Lib.ReflectionValue<float>(radioisotope_generator, "HalfLife");
    double power = Lib.ReflectionValue<float>(radioisotope_generator, "BasePower");
    double mission_time = v.missionTime / (3600.0 * Lib.HoursInDay() * Lib.DaysInYear());
    double remaining = Math.Pow(2.0, (-mission_time) / half_life);
    Lib.Resource.Request(v, "ElectricCharge", -power * remaining * TimeWarp.fixedDeltaTime);
  }
}


} // KERBALISM

