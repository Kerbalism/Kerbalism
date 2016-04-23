// ====================================================================================================================
// consume & produce resources for unloaded vessels
// ====================================================================================================================


using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;


namespace KERBALISM {


[KSPAddon(KSPAddon.Startup.MainMenu, true)]
public class Background : MonoBehaviour
{
  // keep it alive
  Background() { DontDestroyOnLoad(this); }


  // return solar panel EC output
  // note: we ignore temperature curve, and make sure it is not relavant in the MM patch
  static double PanelOutput(Vessel vessel, ProtoPartSnapshot part, ModuleDeployableSolarPanel panel, Vector3d sun_dir, double sun_dist, double atmo_factor)
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

    // reduce solar flux inside atmosphere
    solar_flux *= atmo_factor;

    // finally, calculate output
    return panel.chargeRate * cosine_factor * (panel.useCurve ? panel.powerCurve.Evaluate((float)sun_dist) : solar_flux / Sim.SolarFluxAtHome());
  }


  static double CurvedPanelOutput(Vessel vessel, ProtoPartSnapshot part, Part prefab, Vector3d sun_dir, double sun_dist, double atmo_factor)
  {
    // if, for whatever reason, sun_dist is zero (or negative), we do not return any output
    if (sun_dist <= double.Epsilon) return 0.0;

    // shortcuts
    Quaternion rot = part.rotation;

    // get values from part
    string transform_name = part.partData.GetValue("PanelTransformName");
    float k = Convert.ToSingle(part.partData.GetValue("chargePerTransform"));

    // get components
    Transform[] components = prefab.FindModelTransforms(transform_name);
    if (components.Length == 0) return 0.0;

    // calculate solar flux
    double solar_flux = Sim.SolarFlux(sun_dist);

    // reduce solar flux inside atmosphere
    solar_flux *= atmo_factor;

    // normalize against solar flux at home
    solar_flux /= Sim.SolarFluxAtHome();
    solar_flux *= k;

    // for each one of the components the curved panel is composed of
    double output = 0.0;
    foreach(Transform t in prefab.FindModelTransforms(transform_name))
    {
      double cosine_factor = Math.Max(Vector3d.Dot(sun_dir, (vessel.transform.rotation * rot * t.forward).normalized), 0.0);
      output += cosine_factor * solar_flux;
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
    foreach(Vessel vessel in FlightGlobals.Vessels)
    {
      // skip invalid vessels
      if (!Lib.IsVessel(vessel)) continue;

      // skip loaded vessels
      if (vessel.loaded) continue;

      // get vessel info from the cache
      vessel_info info = Cache.VesselInfo(vessel);

      // calculate atmospheric factor (proportion of flux not blocked by atmosphere)
      double atmo_factor = Sim.AtmosphereFactor(vessel.mainBody, info.position, info.sun_dir);

      // for each part
      foreach(ProtoPartSnapshot part in vessel.protoVessel.protoPartSnapshots)
      {
        // get part prefab (required for module properties)
        Part part_prefab = PartLoader.getPartInfoByName(part.partName).partPrefab;

        // store index of ModuleResourceConverter to process
        // rationale: a part can contain multiple resource converters
        int converter_index = 0;

        // for each module
        foreach(ProtoPartModuleSnapshot module in part.modules)
        {
          // command module
          if (module.moduleName == "ModuleCommand")
          {
            // get module from prefab
            ModuleCommand command = part_prefab.Modules.GetModules<ModuleCommand>()[0];

            // do not consume if this is a MCM with no crew
            // rationale: for consistency, the game doesn't consume resources for MCM without crew in loaded vessels
            //            this make some sense: you left a vessel with some battery and nobody on board, you expect it to not consume EC
            if (command.minimumCrew == 0 || part.protoModuleCrew.Count > 0)
            {
              // for each input resource
              foreach(ModuleResource ir in command.inputResources)
              {
                // consume the resource
                Lib.RequestResource(vessel, ir.name, ir.rate * TimeWarp.fixedDeltaTime);
              }
            }
          }
          // solar panel
          else if (module.moduleName == "ModuleDeployableSolarPanel")
          {
            // determine if extended
            bool extended = module.moduleValues.GetValue("stateString") == ModuleDeployableSolarPanel.panelStates.EXTENDED.ToString();

            // if in sunlight and extended
            if (info.sunlight && extended)
            {
              // get module from prefab
              ModuleDeployableSolarPanel panel = part_prefab.Modules.GetModules<ModuleDeployableSolarPanel>()[0];

              // produce electric charge
              Lib.RequestResource(vessel, "ElectricCharge", -PanelOutput(vessel, part, panel, info.sun_dir, info.sun_dist, atmo_factor) * TimeWarp.fixedDeltaTime * Malfunction.Penalty(part));
            }
          }
          // generator
          // note: assume generators require all input
          else if (module.moduleName == "ModuleGenerator")
          {
            // determine if active
            bool activated = Convert.ToBoolean(module.moduleValues.GetValue("generatorIsActive"));

            // if active
            if (activated)
            {
              // get module from prefab
              ModuleGenerator generator = part_prefab.Modules.GetModules<ModuleGenerator>()[0];

              // determine if vessel is full of all output resources
              bool full = true;
              foreach(var or in generator.outputList)
              {
                double amount = Lib.GetResourceAmount(vessel, or.name);
                double capacity = Lib.GetResourceCapacity(vessel, or.name);
                double perc = capacity > 0.0 ? amount / capacity : 0.0;
                full &= (perc >= 1.0 - double.Epsilon);
              }

              // if not full
              if (!full)
              {
                // calculate worst required resource percentual
                double worst_input = 1.0;
                foreach(var ir in generator.inputList)
                {
                  double required = ir.rate * TimeWarp.fixedDeltaTime;
                  double amount = Lib.GetResourceAmount(vessel, ir.name);
                  worst_input = Math.Min(worst_input, amount / required);
                }

                // for each input resource
                foreach(var ir in generator.inputList)
                {
                  // consume the resource
                  Lib.RequestResource(vessel, ir.name, ir.rate * worst_input * TimeWarp.fixedDeltaTime);
                }

                // for each output resource
                foreach(var or in generator.outputList)
                {
                  // produce the resource
                  Lib.RequestResource(vessel, or.name, -or.rate * worst_input * TimeWarp.fixedDeltaTime * Malfunction.Penalty(part));
                }
              }
            }
          }
          // converter
          // note: support multiple resource converters
          // note: ignore stock temperature mechanic of converters
          // note: ignore autoshutdown
          // note: ignore crew experience bonus (seem that stock ignore it too)
          // note: 'undo' stock behaviour by forcing lastUpdateTime to now (to minimize overlapping calculations from this and stock post-facto simulation)
          else if (module.moduleName == "ModuleResourceConverter")
          {
            // determine if active
            bool activated = Convert.ToBoolean(module.moduleValues.GetValue("IsActivated"));

            // if active
            if (activated)
            {
              // get module from prefab
              ModuleResourceConverter converter = part_prefab.Modules.GetModules<ModuleResourceConverter>()[converter_index++];

              // determine if vessel is full of all output resources
              bool full = true;
              foreach(var or in converter.outputList)
              {
                double amount = Lib.GetResourceAmount(vessel, or.ResourceName);
                double capacity = Lib.GetResourceCapacity(vessel, or.ResourceName);
                double perc = capacity > 0.0 ? amount / capacity : 0.0;
                full &= (perc >= converter.FillAmount - double.Epsilon);
              }

              // if not full
              if (!full)
              {
                // calculate worst required resource percentual
                double worst_input = 1.0;
                foreach(var ir in converter.inputList)
                {
                  double required = ir.Ratio * TimeWarp.fixedDeltaTime;
                  double amount = Lib.GetResourceAmount(vessel, ir.ResourceName);
                  worst_input = Math.Min(worst_input, amount / required);
                }

                // for each input resource
                foreach(var ir in converter.inputList)
                {
                  // consume the resource
                  Lib.RequestResource(vessel, ir.ResourceName, ir.Ratio * worst_input * TimeWarp.fixedDeltaTime);
                }

                // for each output resource
                foreach(var or in converter.outputList)
                {
                  // produce the resource
                  Lib.RequestResource(vessel, or.ResourceName, -or.Ratio * worst_input * TimeWarp.fixedDeltaTime * Malfunction.Penalty(part));
                }
              }

              // undo stock behaviour by forcing last_update_time to now
              module.moduleValues.SetValue("lastUpdateTime", Planetarium.GetUniversalTime().ToString());
            }
          }
          // drill
          // note: ignore stock temperature mechanic of harvesters
          // note: ignore autoshutdown
          // note: ignore depletion (stock seem to do the same)
          // note: 'undo' stock behaviour by forcing lastUpdateTime to now (to minimize overlapping calculations from this and stock post-facto simulation)
          else if (module.moduleName == "ModuleResourceHarvester")
          {
            // determine if active
            bool activated = Convert.ToBoolean(module.moduleValues.GetValue("IsActivated"));

            // if active
            if (activated)
            {
              // get module from prefab
              ModuleResourceHarvester harvester = part_prefab.Modules.GetModules<ModuleResourceHarvester>()[0];

              // deduce crew bonus
              double experience_bonus = 0.0;
              if (harvester.UseSpecialistBonus)
              {
                foreach(ProtoCrewMember c in vessel.protoVessel.GetVesselCrew())
                {
                  experience_bonus = Math.Max(experience_bonus, (c.trait == harvester.Specialty) ? (double)c.experienceLevel : 0.0);
                }
              }
              double crew_bonus = harvester.SpecialistBonusBase + (experience_bonus + 1.0) * harvester.SpecialistEfficiencyFactor;

              // detect amount of ore in the ground
              AbundanceRequest request = new AbundanceRequest
              {
                Altitude = vessel.altitude,
                BodyId = vessel.mainBody.flightGlobalsIndex,
                CheckForLock = false,
                Latitude = vessel.latitude,
                Longitude = vessel.longitude,
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
                  double amount = Lib.GetResourceAmount(vessel, ir.ResourceName);
                  worst_input = Math.Min(worst_input, amount / required);
                }

                // for each input resource
                foreach(var ir in harvester.inputList)
                {
                  // consume the resource
                  Lib.RequestResource(vessel, ir.ResourceName, ir.Ratio * worst_input * TimeWarp.fixedDeltaTime);
                }

                // determine resource produced
                double res = abundance * harvester.Efficiency * crew_bonus * worst_input * Malfunction.Penalty(part);

                // accumulate ore
                Lib.RequestResource(vessel, harvester.ResourceName, -res * TimeWarp.fixedDeltaTime);
              }

              // undo stock behaviour by forcing last_update_time to now
              module.moduleValues.SetValue("lastUpdateTime", Planetarium.GetUniversalTime().ToString());
            }
          }
          // asteroid drill
          // note: untested
          // note: ignore stock temperature mechanic of asteroid drills
          // note: ignore autoshutdown
          // note: 'undo' stock behaviour by forcing lastUpdateTime to now (to minimize overlapping calculations from this and stock post-facto simulation)
          else if (module.moduleName == "ModuleAsteroidDrill")
          {
            // determine if active
            bool activated = Convert.ToBoolean(module.moduleValues.GetValue("IsActivated"));

            // if active
            if (activated)
            {
              // get module from prefab
              ModuleAsteroidDrill asteroid_drill = part_prefab.Modules.GetModules<ModuleAsteroidDrill>()[0];

              // deduce crew bonus
              double experience_bonus = 0.0;
              if (asteroid_drill.UseSpecialistBonus)
              {
                foreach(ProtoCrewMember c in vessel.protoVessel.GetVesselCrew())
                {
                  experience_bonus = Math.Max(experience_bonus, (c.trait == asteroid_drill.Specialty) ? (double)c.experienceLevel : 0.0);
                }
              }
              double crew_bonus = asteroid_drill.SpecialistBonusBase + (experience_bonus + 1.0) * asteroid_drill.SpecialistEfficiencyFactor;

              // get asteroid data
              ProtoPartModuleSnapshot asteroid_info = null;
              ProtoPartModuleSnapshot asteroid_resource = null;
              foreach(ProtoPartSnapshot p in vessel.protoVessel.protoPartSnapshots)
              {
                if (asteroid_info == null) asteroid_info = p.modules.Find(k => k.moduleName == "ModuleAsteroidInfo");
                if (asteroid_resource == null) asteroid_resource = p.modules.Find(k => k.moduleName == "ModuleAsteroidResource");
              }

              // if there is actually an asteroid attached to this active asteroid drill (it should)
              if (asteroid_info != null && asteroid_resource != null)
              {
                // get some data
                double mass_threshold = Convert.ToDouble(asteroid_info.moduleValues.GetValue("massThresholdVal"));
                double mass = Convert.ToDouble(asteroid_info.moduleValues.GetValue("currentMassVal"));
                double abundance = Convert.ToDouble(asteroid_resource.moduleValues.GetValue("abundance"));
                string res_name = asteroid_resource.moduleValues.GetValue("resourceName");
                double res_density = PartResourceLibrary.Instance.GetDefinition(res_name).density;

                // if asteroid isn't depleted
                if (mass > mass_threshold && abundance > double.Epsilon)
                {
                  // consume EC
                  double ec_required = asteroid_drill.PowerConsumption * TimeWarp.fixedDeltaTime;
                  double ec_consumed = Lib.RequestResource(vessel, "ElectricCharge", ec_required);
                  double ec_ratio = ec_consumed / ec_required;

                  // determine resource extracted
                  double res_amount = abundance * asteroid_drill.Efficiency * crew_bonus * ec_ratio * TimeWarp.fixedDeltaTime;

                  // produce mined resource
                  Lib.RequestResource(vessel, res_name, -res_amount);

                  // consume asteroid mass
                  asteroid_info.moduleValues.SetValue("currentMassVal", (mass - res_density * res_amount).ToString());
                }
              }

              // undo stock behaviour by forcing last_update_time to now
              module.moduleValues.SetValue("lastUpdateTime", Planetarium.GetUniversalTime().ToString());
            }
          }
          // SCANSAT support (new version)
          // TODO: enable better SCANsat support
          /*else if (module.moduleName == "SCANsat" || module.moduleName == "ModuleSCANresourceScanner")
          {
            // get ec consumption rate
            PartModule scansat = part_prefab.Modules[module.moduleName];
            double power = Lib.ReflectionValue<float>(scansat, "power");
            double ec_required = power * TimeWarp.fixedDeltaTime;

            // if it was scanning
            if (SCANsat.wasScanning(module))
            {
              // if there is enough ec
              double ec_amount = Lib.GetResourceAmount(vessel, "ElectricCharge");
              double ec_capacity = Lib.GetResourceCapacity(vessel, "ElectricCharge");
              if (ec_capacity > double.Epsilon && ec_amount / ec_capacity > 0.15) //< re-enable at 15% EC
              {
                // re-enable the scanner
                SCANsat.resumeScanner(vessel, module, part_prefab);

                // give the user some feedback
                if (DB.VesselData(vessel.id).cfg_ec == 1)
                  Message.Post(Severity.relax, "SCANsat> sensor on <b>" + vessel.vesselName + "</b> resumed operations", "we got enough ElectricCharge");
              }
            }

            // if it is scanning
            if (SCANsat.isScanning(module))
            {
              // consume ec
              double ec_consumed = Lib.RequestResource(vessel, "ElectricCharge", ec_required);

              // if there isn't enough ec
              if (ec_consumed < ec_required * 0.99 && ec_required > double.Epsilon)
              {
                // unregister scanner, and remember it
                SCANsat.stopScanner(vessel, module, part_prefab);

                // give the user some feedback
                if (DB.VesselData(vessel.id).cfg_ec == 1)
                  Message.Post(Severity.warning, "SCANsat sensor was disabled on <b>" + vessel.vesselName + "</b>", "for lack of ElectricCharge");
              }
            }
          }*/
          // SCANSAT support (old version)
          // note: this one doesn't support re-activation, is a bit slower and less clean
          //       waiting for DMagic to fix a little bug
          else if (module.moduleName == "SCANsat" || module.moduleName == "ModuleSCANresourceScanner")
          {
            // determine if scanning
            bool scanning = Convert.ToBoolean(module.moduleValues.GetValue("scanning"));

            // consume ec
            if (scanning)
            {
              // get ec consumption
              PartModule scansat = part_prefab.Modules[module.moduleName];
              double power = Lib.ReflectionValue<float>(scansat, "power");

              // consume ec
              double ec_required = power * TimeWarp.fixedDeltaTime;
              double ec_consumed = Lib.RequestResource(vessel, "ElectricCharge", ec_required);

              // if there isn't enough ec
              if (ec_consumed < ec_required * 0.99 && ec_required > double.Epsilon)
              {
                // unregister scanner using reflection
                foreach(var a in AssemblyLoader.loadedAssemblies)
                {
                  if (a.name == "SCANsat")
                  {
                    Type controller_type = a.assembly.GetType("SCANsat.SCANcontroller");
                    System.Object controller = controller_type.GetProperty("controller", BindingFlags.Public | BindingFlags.Static).GetValue(null, null);
                    controller_type.InvokeMember("removeVessel", BindingFlags.InvokeMethod | BindingFlags.NonPublic | BindingFlags.Instance, null, controller, new System.Object[]{vessel});
                  }
                }

                // disable scanning
                module.moduleValues.SetValue("scanning", false.ToString());

                // give the user some feedback
                if (DB.VesselData(vessel.id).cfg_ec == 1)
                  Message.Post(Severity.warning, "SCANsat sensor was disabled on <b>" + vessel.vesselName + "</b>", "for lack of ElectricCharge");
              }
            }
          }
          // NearFutureSolar support
          // note: we assume deployed, this is a current limitation
          else if (module.moduleName == "ModuleCurvedSolarPanel")
          {
            // [unused] determine if extended
            //string state = module.moduleValues.GetValue("SavedState");
            //bool extended = state == ModuleDeployableSolarPanel.panelStates.EXTENDED.ToString();

            // if in sunlight
            if (info.sunlight)
            {
              // produce electric charge
              double output = CurvedPanelOutput(vessel, part, part_prefab, info.sun_dir, info.sun_dist, atmo_factor) * Malfunction.Penalty(part);
              Lib.RequestResource(vessel, "ElectricCharge", -output * TimeWarp.fixedDeltaTime);
            }
          }
          // KERBALISM modules
          else if (module.moduleName == "Scrubber") { Scrubber.BackgroundUpdate(vessel, part.flightID); }
          else if (module.moduleName == "Greenhouse") { Greenhouse.BackgroundUpdate(vessel, part.flightID); }
          else if (module.moduleName == "Malfunction") { Malfunction.BackgroundUpdate(vessel, part.flightID); }
        }
      }
    }
  }
}


} // KERBALISM