// ====================================================================================================================
// consume & produce resources for unloaded vessels
// ====================================================================================================================


using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;


namespace KERBALISM {


public sealed class Background
{
  // called at every simulation step
  public static void update(Vessel v, vessel_info vi, vessel_data vd, vessel_resources resources, double elapsed_s)
  {
    // this vessel is not properly set up by the game engine, skip it
    if (vi.sun_dist <= double.Epsilon) return;

    // get most used resource handlers
    resource_info ec = resources.Info(v, "ElectricCharge");

    // for each part
    foreach(ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
    {
      // a part can contain multiple resource converters
      int converter_index = 0;

      // get part prefab (required for module properties)
      Part part_prefab = PartLoader.getPartInfoByName(p.partName).partPrefab;

      // for each module
      foreach(ProtoPartModuleSnapshot m in p.modules)
      {
        // get the module prefab
        PartModule module_prefab = Lib.FindModule(part_prefab, m.moduleName);

        // if the prefab doesn't contain this module, skip it
        if (!module_prefab) continue;

        // process modules
        switch(m.moduleName)
        {
          case "Malfunction":                 Malfunction.BackgroundUpdate(v, m, module_prefab as Malfunction, elapsed_s);                break;
          case "Scrubber":                    Scrubber.BackgroundUpdate(v, m, module_prefab as Scrubber, vi, resources, elapsed_s);       break;
          case "Recycler":                    Recycler.BackgroundUpdate(v, m, module_prefab as Recycler, resources, elapsed_s);           break;
          case "Greenhouse":                  Greenhouse.BackgroundUpdate(v, m, module_prefab as Greenhouse, vi, resources, elapsed_s);   break;
          case "GravityRing":                 GravityRing.BackgroundUpdate(v, m, module_prefab as GravityRing, resources, elapsed_s);     break;
          case "ModuleCommand":               ProcessCommand(v, p, m, module_prefab as ModuleCommand, resources, elapsed_s);              break;
          case "ModuleDeployableSolarPanel":  ProcessPanel(v, p, m, module_prefab as ModuleDeployableSolarPanel, vi, ec, elapsed_s);      break;
          case "ModuleGenerator":             ProcessGenerator(v, p, m, module_prefab as ModuleGenerator, resources, elapsed_s);          break;
          case "ModuleResourceConverter":
          case "ModuleKPBSConverter":
          case "FissionReactor":              ProcessConverter(v, p, m, part_prefab, converter_index++, resources, elapsed_s);            break;
          case "ModuleResourceHarvester":     ProcessHarvester(v, p, m, module_prefab as ModuleResourceHarvester, resources, elapsed_s);  break;
          case "ModuleAsteroidDrill":         ProcessAsteroidDrill(v, p, m, module_prefab as ModuleAsteroidDrill, resources, elapsed_s);  break;
          case "ModuleScienceConverter":      ProcessLab(v, p, m, module_prefab as ModuleScienceConverter, ec, elapsed_s);                break;
          case "SCANsat":
          case "ModuleSCANresourceScanner":   ProcessScanner(v, p, m, module_prefab, part_prefab, vd, ec, elapsed_s);                     break;
          case "ModuleCurvedSolarPanel":      ProcessCurvedPanel(v, p, m, module_prefab, part_prefab, vi, ec, elapsed_s);                 break;
          case "FissionGenerator":            ProcessFissionGenerator(v, p, m, module_prefab, ec, elapsed_s);                             break;
          case "ModuleRadioisotopeGenerator": ProcessRadioisotopeGenerator(v, p, m, module_prefab, ec, elapsed_s);                        break;
          case "ModuleCryoTank":              ProcessCryoTank(v, p, m, module_prefab, resources, elapsed_s);                              break;
        }
      }
    }
  }


  static void ProcessCommand(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, ModuleCommand command, vessel_resources resources, double elapsed_s)
  {
    // play nice with BackgroundProcessing
    if (Kerbalism.detected_mods.BackgroundProcessing) return;

    // do not consume if this is a MCM with no crew
    // rationale: for consistency, the game doesn't consume resources for MCM without crew in loaded vessels
    //            this make some sense: you left a vessel with some battery and nobody on board, you expect it to not consume EC
    if (command.minimumCrew == 0 || p.protoModuleCrew.Count > 0)
    {
      // for each input resource
      foreach(ModuleResource ir in command.inputResources)
      {
        // consume the resource
        resources.Consume(v, ir.name, ir.rate * elapsed_s);
      }
    }
  }


  static void ProcessPanel(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, ModuleDeployableSolarPanel panel, vessel_info info, resource_info ec, double elapsed_s)
  {
    // note: we ignore temperature curve, and make sure it is not relavant in the MM patch
    // note: we ignore power curve, that is used by no panel as far as I know

    // play nice with BackgroundProcessing
    if (Kerbalism.detected_mods.BackgroundProcessing) return;

    // if in sunlight and extended
    if (info.sunlight > double.Epsilon && m.moduleValues.GetValue("stateString") == "EXTENDED")
    {
      // get panel normal direction
      Vector3d normal = panel.part.FindModelComponent<Transform>(panel.raycastTransformName).forward;

      // calculate cosine factor
      // note: for gameplay reasons, we ignore tracking panel pivots
      double cosine_factor = panel.sunTracking ? 1.0 : Math.Max(Vector3d.Dot(info.sun_dir, (v.transform.rotation * p.rotation * normal).normalized), 0.0);

      // calculate normalized solar flux
      // note: this include fractional sunlight if integrated over orbit
      // note: this include atmospheric absorption if inside an atmosphere
      double norm_solar_flux = info.solar_flux / Sim.SolarFluxAtHome();

      // calculate output
      double output = panel.chargeRate                                      // nominal panel charge rate at 1 AU
                    * norm_solar_flux                                       // normalized flux at panel distance from sun
                    * cosine_factor                                         // cosine factor of panel orientation
                    * Malfunction.Penalty(p);                               // malfunctioned panel penalty

      // produce EC
      ec.Produce(output * elapsed_s);
    }
  }


  static void ProcessGenerator(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, ModuleGenerator generator, vessel_resources resources, double elapsed_s)
  {
    // play nice with BackgroundProcessing
    if (Kerbalism.detected_mods.BackgroundProcessing) return;

    // if active
    if (Lib.Proto.GetBool(m, "generatorIsActive"))
    {
      // get malfunction penalty
      double penalty = Malfunction.Penalty(p);

      // create and commit recipe
      resource_recipe recipe = new resource_recipe(resource_recipe.converter_priority);
      foreach(var ir in generator.inputList)
      {
        recipe.Input(ir.name, ir.rate * elapsed_s);
      }
      foreach(var or in generator.outputList)
      {
        recipe.Output(or.name, or.rate * penalty * elapsed_s);
      }
      resources.Transform(recipe);
    }
  }


  static void ProcessConverter(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, Part part_prefab, int index, vessel_resources resources, double elapsed_s)
  {
    // note: support multiple resource converters
    // note: ignore stock temperature mechanic of converters
    // note: ignore autoshutdown
    // note: using hard-coded crew bonus values from the wiki because the module data make zero sense (DERP ALERT)
    // note: non-mandatory resources 'dynamically scale the ratios', that is exactly what mandatory resources do too (DERP ALERT)
    // note: 'undo' stock behaviour by forcing lastUpdateTime to now (to minimize overlapping calculations from this and stock post-facto simulation)
    // note: support PlanetaryBaseSystem converters
    // note: support NearFuture reactors
    // note: assume FillAmount is 1 (completely full)

    // get converter
    var converter_prefabs = part_prefab.Modules.GetModules<ModuleResourceConverter>();
    if (index >= converter_prefabs.Count) return;
    ModuleResourceConverter converter = converter_prefabs[index] as ModuleResourceConverter;

    // if active
    if (Lib.Proto.GetBool(m, "IsActivated"))
    {
      // get malfunction penalty
      double penalty = Malfunction.Penalty(p);

      // deduce crew bonus
      int exp_level = -1;
      if (converter.UseSpecialistBonus)
      {
        foreach(ProtoCrewMember c in v.protoVessel.GetVesselCrew())
          exp_level = Math.Max(exp_level, c.trait == converter.Specialty ? c.experienceLevel : -1);
      }
      double exp_bonus = exp_level < 0 ? 1.0 : 5.0 + (double)exp_level * 4.0;

      // create and commit recipe
      resource_recipe recipe = new resource_recipe(resource_recipe.converter_priority);
      foreach(var ir in converter.inputList)
      {
        recipe.Input(ir.ResourceName, ir.Ratio * elapsed_s);
      }
      foreach(var or in converter.outputList)
      {
        recipe.Output(or.ResourceName, or.Ratio * penalty * exp_bonus * elapsed_s);
      }
      resources.Transform(recipe);

      // undo stock behaviour by forcing last_update_time to now
      Lib.Proto.Set(m, "lastUpdateTime", Planetarium.GetUniversalTime());
    }
  }


  static void ProcessHarvester(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, ModuleResourceHarvester harvester, vessel_resources resources, double elapsed_s)
  {
    // note: ignore stock temperature mechanic of harvesters
    // note: ignore autoshutdown
    // note: ignore depletion (stock seem to do the same)
    // note: using hard-coded crew bonus values from the wiki because the module data make zero sense (DERP ALERT)
    // note: 'undo' stock behaviour by forcing lastUpdateTime to now (to minimize overlapping calculations from this and stock post-facto simulation)

    // if active
    if (Lib.Proto.GetBool(m, "IsActivated"))
    {
      // get malfunction penalty
      double penalty = Malfunction.Penalty(p);

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
        // create and commit recipe
        resource_recipe recipe = new resource_recipe(resource_recipe.harvester_priority);
        foreach(var ir in harvester.inputList)
        {
          recipe.Input(ir.ResourceName, ir.Ratio * elapsed_s);
        }
        recipe.Output(harvester.ResourceName, abundance * harvester.Efficiency * exp_bonus * penalty * elapsed_s);
        resources.Transform(recipe);
      }

      // undo stock behaviour by forcing last_update_time to now
      Lib.Proto.Set(m, "lastUpdateTime", Planetarium.GetUniversalTime());
    }
  }


  static void ProcessAsteroidDrill(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, ModuleAsteroidDrill asteroid_drill, vessel_resources resources, double elapsed_s)
  {
    // note: untested
    // note: ignore stock temperature mechanic of asteroid drills
    // note: ignore autoshutdown
    // note: using hard-coded crew bonus values from the wiki because the module data make zero sense (DERP ALERT)
    // note: 'undo' stock behaviour by forcing lastUpdateTime to now (to minimize overlapping calculations from this and stock post-facto simulation)

    // if active
    if (Lib.Proto.GetBool(m, "IsActivated"))
    {
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
          // deduce crew bonus
          int exp_level = -1;
          if (asteroid_drill.UseSpecialistBonus)
          {
            foreach(ProtoCrewMember c in v.protoVessel.GetVesselCrew())
            exp_level = Math.Max(exp_level, c.trait == asteroid_drill.Specialty ? c.experienceLevel : -1);
          }
          double exp_bonus = exp_level < 0 ? 1.0 : 5.0 + (double)exp_level * 4.0;

          // determine resource extracted
          double res_amount = abundance * asteroid_drill.Efficiency * exp_bonus * elapsed_s;

          // transform EC into mined resource
          resource_recipe recipe = new resource_recipe(resource_recipe.harvester_priority);
          recipe.Input("ElectricCharge", asteroid_drill.PowerConsumption * elapsed_s);
          recipe.Output(res_name, res_amount);
          resources.Transform(recipe);

          // if there was ec
          // note: comparing against amount in previous simulation step
          if (resources.Info(v, "ElectricCharge").amount > double.Epsilon)
          {
            // consume asteroid mass
            Lib.Proto.Set(asteroid_info, "currentMassVal", (mass - res_density * res_amount));
          }
        }
      }

      // undo stock behaviour by forcing last_update_time to now
      Lib.Proto.Set(m, "lastUpdateTime", Planetarium.GetUniversalTime());
    }
  }


  static void ProcessLab(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, ModuleScienceConverter lab, resource_info ec, double elapsed_s)
  {
    // note: we are only simulating the EC consumption
    // note: there is no easy way to 'stop' the lab when there isn't enough EC

    // if active
    if (Lib.Proto.GetBool(m, "IsActivated"))
    {
      // consume ec
      ec.Consume(lab.powerRequirement * elapsed_s);
    }
  }


  static void ProcessScanner(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, PartModule scanner, Part part_prefab, vessel_data vd, resource_info ec, double elapsed_s)
  {
    // get ec consumption rate
    double power = Lib.ReflectionValue<float>(scanner, "power");

    // if the scanner doesn't require power to operate, we aren't interested in simulating it
    if (power <= double.Epsilon) return;

    // get scanner state
    bool is_scanning = Lib.Proto.GetBool(m, "scanning");

    // if its scanning
    if (is_scanning)
    {
      // consume ec
      ec.Consume(power * elapsed_s);

      // if there isn't ec
      // note: comparing against amount in previous simulation step
      if (ec.amount <= double.Epsilon)
      {
        // unregister scanner
        SCANsat.stopScanner(v, m, part_prefab);
        is_scanning = false;

        // remember disabled scanner
        vd.scansat_id.Add(p.flightID);

        // give the user some feedback
        if (vd.cfg_ec == 1) Message.Post(Lib.BuildString("SCANsat sensor was disabled on <b>", v.vesselName, "</b>"));
      }
    }
    // if it was disabled in background
    else if (vd.scansat_id.Contains(p.flightID))
    {
      // if there is enough ec
      // note: comparing against amount in previous simulation step
      if (ec.level > 0.25) //< re-enable at 25% EC
      {
        // re-enable the scanner
        SCANsat.resumeScanner(v, m, part_prefab);
        is_scanning = true;

        // give the user some feedback
        if (vd.cfg_ec == 1) Message.Post(Lib.BuildString("SCANsat sensor resumed operations on <b>", v.vesselName, "</b>"));
      }
    }

    // forget active scanners
    if (is_scanning) vd.scansat_id.Remove(p.flightID);
  }


  static void ProcessCurvedPanel(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, PartModule curved_panel, Part part_prefab, vessel_info info, resource_info ec, double elapsed_s)
  {
    // note: we assume deployed, this is a current limitation

    // if in sunlight
    if (info.sunlight > double.Epsilon)
    {
      // get values from module
      string transform_name = Lib.ReflectionValue<string>(curved_panel, "PanelTransformName");
      float tot_rate = Lib.ReflectionValue<float>(curved_panel, "TotalEnergyRate");

      // get components
      Transform[] components = part_prefab.FindModelTransforms(transform_name);
      if (components.Length == 0) return;

      // calculate normalized solar flux
      // note: this include fractional sunlight if integrated over orbit
      // note: this include atmospheric absorption if inside an atmosphere
      double norm_solar_flux = info.solar_flux / Sim.SolarFluxAtHome();

      // calculate rate per component
      double rate = (double)tot_rate / (double)components.Length;

      // calculate world-space part rotation quaternion
      Quaternion rot = v.transform.rotation * p.rotation;

      // calculate output of all components
      double output = 0.0;
      foreach(Transform t in components)
      {
        output += rate                                                                     // nominal rate per-component at 1 AU
                * norm_solar_flux                                                          // normalized solar flux at panel distance from sun
                * Math.Max(Vector3d.Dot(info.sun_dir, (rot * t.forward).normalized), 0.0); // cosine factor of component orientation
      }
      output *= Malfunction.Penalty(p);                                                    // malfunctioned panel penalty

      // produce EC
      ec.Produce(output * elapsed_s);
    }
  }


  static void ProcessFissionGenerator(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, PartModule fission_generator, resource_info ec, double elapsed_s)
  {
    // note: ignore heat

    double power = Lib.ReflectionValue<float>(fission_generator, "PowerGeneration");
    var reactor = p.modules.Find(k => k.moduleName == "FissionReactor");
    double tweakable = reactor == null ? 1.0 : Lib.ConfigValue(reactor.moduleValues, "CurrentPowerPercent", 100.0) * 0.01;
    ec.Produce(power * tweakable * elapsed_s);
  }


  static void ProcessRadioisotopeGenerator(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, PartModule radioisotope_generator, resource_info ec, double elapsed_s)
  {
    // note: doesn't support easy mode

    double power = Lib.ReflectionValue<float>(radioisotope_generator, "BasePower");
    double remaining = 1.0;
    if (Settings.RTGDecay)
    {
      double half_life = Lib.ReflectionValue<float>(radioisotope_generator, "HalfLife");
      double mission_time = v.missionTime / (3600.0 * Lib.HoursInDay() * Lib.DaysInYear());
      remaining = Math.Pow(2.0, (-mission_time) / half_life);
    }
    ec.Produce(power * remaining * elapsed_s);
  }


  static void ProcessCryoTank(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, PartModule simple_boiloff, vessel_resources resources, double elapsed_s)
  {
    // note: cryotank module already does a post-facto simulation of background boiling, and we could use that for the boiling
    // however, it also does simulate the ec consumption that way, so we have to disable the post-facto simulation

    // get fuel name
    string fuel_name = Lib.ReflectionValue<string>(simple_boiloff, "FuelName");

    // get resource handlers
    resource_info ec = resources.Info(v, "ElectricCharge");
    resource_info fuel = resources.Info(v, fuel_name);


    // if there is some fuel
    // note: comparing against amount in previous simulation step
    if (fuel.amount > double.Epsilon)
    {
      // if cooling is enabled and there was enough ec
      // note: comparing against amount in previous simulation step
      if (Lib.Proto.GetBool(m, "CoolingEnabled") && ec.amount > double.Epsilon)
      {
        // get cooling ec cost per 1000 units of fuel, per-second
        double cooling_cost = Lib.ReflectionValue<float>(simple_boiloff, "CoolingCost");

        // consume ec
        // note: using fuel amount from previous simulation step
        ec.Consume(cooling_cost * fuel.amount * 0.001 * elapsed_s);
      }
      // if there wasn't ec, or if cooling is disabled
      else
      {
        // get boiloff rate in proportion to fuel amount, per-second
        double boiloff_rate = Lib.ReflectionValue<float>(simple_boiloff, "BoiloffRate") * 0.00000277777;

        // calculate amount to boil
        // note: using amount from previous simulation step
        double to_boil = fuel.amount * (1.0 - Math.Pow(1.0 - boiloff_rate, elapsed_s));

        // let it boil off
        fuel.Consume(to_boil * elapsed_s);
      }
    }

    // disable post-facto simulation
    Lib.Proto.Set(m, "LastUpdateTime", v.missionTime);
  }
}


/* 32bit hash of moduleName [unused]
--------------------------------------------------------------
  const UInt32 id_ModuleCommand                = 3134432346u;
  const UInt32 id_ModuleDeployableSolarPanel   = 2507815787u;
  const UInt32 id_ModuleGenerator              = 1063091850u;
  const UInt32 id_ModuleResourceConverter      = 2892431223u;
  const UInt32 id_ModuleKPBSConverter          = 361067095u;
  const UInt32 id_FissionReactor               = 822103862u;
  const UInt32 id_ModuleResourceHarvester      = 323451101u;
  const UInt32 id_ModuleAsteroidDrill          = 3473780657u;
  const UInt32 id_ModuleScienceConverter       = 3498484783u;
  const UInt32 id_SCANsat                      = 297618428u;
  const UInt32 id_ModuleSCANresourceScanner    = 2406003354u;
  const UInt32 id_ModuleCurvedSolarPanel       = 2130113745u;
  const UInt32 id_FissionGenerator             = 447456541u;
  const UInt32 id_ModuleRadioisotopeGenerator  = 195610716u;
  const UInt32 id_ModuleCryoTank               = 2363543940u;
  const UInt32 id_Scrubber                     = 1508775713u;
  const UInt32 id_Recycler                     = 3861547956u;
  const UInt32 id_Greenhouse                   = 2233558176u;
  const UInt32 id_GravityRing                  = 1565524331u;
  const UInt32 id_Malfunction                  = 2152559097u;
*/


} // KERBALISM

