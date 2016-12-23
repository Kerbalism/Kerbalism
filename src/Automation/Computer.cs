using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public enum ScriptType
{
  landed      = 1,      // called on landing
  atmo        = 2,      // called on entering atmosphere
  space       = 3,      // called on reaching space
  sunlight    = 4,      // called when sun rise
  shadow      = 5,      // called when sun set
  power_high  = 6,      // called when ec level goes above 15%
  power_low   = 7,      // called when ec level goes below 15%
  rad_low     = 8,      // called when radiation goes below 0.05 rad/h
  rad_high    = 9,      // called when radiation goes above 0.05 rad/h
  linked      = 10,      // called when signal is regained
  unlinked    = 11,     // called when signal is lost
  eva_out     = 12,     // called when going out on eva
  eva_in      = 13,     // called when coming back from eva
  action1     = 14,     // called when pressing 1
  action2     = 15,     // called when pressing 2
  action3     = 16,     // called when pressing 3
  action4     = 17,     // called when pressing 4
  action5     = 18,     // called when pressing 5
  last        = 19
}


public sealed class Computer
{
  public Computer()
  {
    scripts = new Dictionary<ScriptType, Script>();
  }


  public Computer(ConfigNode node)
  {
    // load scripts
    scripts = new Dictionary<ScriptType, Script>();
    foreach(var script_node in node.GetNode("scripts").GetNodes())
    {
      scripts.Add((ScriptType)Lib.Parse.ToUInt(script_node.name), new Script(script_node));
    }
  }


  public void save(ConfigNode node)
  {
    // save scripts
    var scripts_node = node.AddNode("scripts");
    foreach(var p in scripts)
    {
      if (p.Value.states.Count == 0) continue; //< empty-script optimization
      p.Value.save(scripts_node.AddNode(((uint)p.Key).ToString()));
    }
  }


  // get a script
  public Script get(ScriptType type)
  {
    if (!scripts.ContainsKey(type)) scripts.Add(type, new Script());
    return scripts[type];
  }


  // execute a script
  public void execute(Vessel v, ScriptType type)
  {
    // do nothing if there is no EC left on the vessel
    resource_info ec = ResourceCache.Info(v, "ElectricCharge");
    if (ec.amount <= double.Epsilon) return;

    // get the script
    Script script;
    if (scripts.TryGetValue(type, out script))
    {
      // execute the script
      script.execute( boot(v) );

      // show message to the user
      // - unless the script is empty (can happen when being edited)
      if (script.states.Count > 0 && DB.Vessel(v).cfg_script)
      {
        Message.Post(Lib.BuildString("Script called on vessel <b>", v.vesselName, "</b>"));
      }
    }
  }


  // call scripts automatically when conditions are met
  public void automate(Vessel v, vessel_info vi, vessel_resources resources)
  {
    // do nothing if automation is disabled
    if (!Features.Automation) return;

    // do nothing if there is no EC left on the vessel
    resource_info ec = resources.Info(v, "ElectricCharge");
    if (ec.amount <= double.Epsilon) return;

    // get current states
    bool sunlight = vi.sunlight > double.Epsilon;
    bool power = ec.level >= 0.15; //< 15%
    bool radiation = vi.radiation >= 0.00001388; //< 0.05 rad/h
    bool signal = vi.connection.linked;

    // get current situation
    string situation = string.Empty;
    switch(v.situation)
    {
      case Vessel.Situations.LANDED:
      case Vessel.Situations.SPLASHED:
        situation = "landed";
        break;

      case Vessel.Situations.FLYING:
        situation = "atmo";
        break;

      case Vessel.Situations.SUB_ORBITAL:
      case Vessel.Situations.ORBITING:
      case Vessel.Situations.ESCAPING:
        situation = "space";
        break;
    }


    // compile list of scripts that need to be called
    var to_exec = new List<Script>();
    foreach(var p in scripts)
    {
      ScriptType type = p.Key;
      Script script = p.Value;
      if (script.states.Count == 0) continue; //< skip empty scripts (may happen during editing)

      switch(type)
      {
        case ScriptType.landed:
          if (situation == "landed" && script.prev != "landed" && script.prev.Length > 0) to_exec.Add(script);
          script.prev = situation;
          break;

        case ScriptType.atmo:
          if (situation == "atmo" && script.prev != "atmo" && script.prev.Length > 0) to_exec.Add(script);
          script.prev = situation;
          break;

        case ScriptType.space:
          if (situation == "space" && script.prev != "space" && script.prev.Length > 0) to_exec.Add(script);
          script.prev = situation;
          break;

        case ScriptType.sunlight:
          if (sunlight && script.prev == "0") to_exec.Add(script);
          script.prev = sunlight ? "1" : "0";
          break;

        case ScriptType.shadow:
          if (!sunlight && script.prev == "1") to_exec.Add(script);
          script.prev = sunlight ? "1" : "0";
          break;

        case ScriptType.power_high:
          if (power && script.prev == "0") to_exec.Add(script);
          script.prev = power ? "1" : "0";
          break;

        case ScriptType.power_low:
          if (!power && script.prev == "1") to_exec.Add(script);
          script.prev = power ? "1" : "0";
          break;

        case ScriptType.rad_low:
          if (!radiation && script.prev == "1") to_exec.Add(script);
          script.prev = radiation ? "1" : "0";
          break;

        case ScriptType.rad_high:
          if (radiation && script.prev == "0") to_exec.Add(script);
          script.prev = radiation ? "1" : "0";
          break;

        case ScriptType.linked:
          if (signal && script.prev == "0") to_exec.Add(script);
          script.prev = signal ? "1" : "0";
          break;

        case ScriptType.unlinked:
          if (!signal && script.prev == "1") to_exec.Add(script);
          script.prev = signal ? "1" : "0";
          break;
      }
    }

    // if there are scripts to call
    if (to_exec.Count > 0)
    {
      // get list of devices
      // - we avoid creating it when there are no scripts to be executed, making its overall cost trivial
      var devices = boot(v);

      // execute all scripts
      foreach(Script script in to_exec)
      {
        script.execute(devices);
      }

      // show message to the user
      if (DB.Vessel(v).cfg_script)
      {
        Message.Post(Lib.BuildString("Script called on vessel <b>", v.vesselName, "</b>"));
      }
    }
  }


  // return set of devices on a vessel
  // - the list is only valid for a single simulation step
  public static Dictionary<uint, Device> boot(Vessel v)
  {
    // store all devices
    var devices = new Dictionary<uint, Device>();

    // store device being added
    Device dev;

    // loaded vessel
    if (v.loaded)
    {
      foreach(PartModule m in Lib.FindModules<PartModule>(v))
      {
        switch(m.moduleName)
        {
          case "ProcessController":             dev = new ProcessDevice(m as ProcessController);          break;
          case "Greenhouse":                    dev = new GreenhouseDevice(m as Greenhouse);              break;
          case "GravityRing":                   dev = new RingDevice(m as GravityRing);                   break;
          case "Emitter":                       dev = new EmitterDevice(m as Emitter);                    break;
          case "Harvester":                     dev = new HarvesterDevice(m as Harvester);                break;
          case "Laboratory":                    dev = new LaboratoryDevice(m as Laboratory);              break;
          case "Antenna":                       dev = new AntennaDevice(m as Antenna);                    break;
          case "ModuleDeployableSolarPanel":    dev = new PanelDevice(m as ModuleDeployableSolarPanel);   break;
          case "ModuleGenerator":               dev = new GeneratorDevice(m as ModuleGenerator);          break;
          case "ModuleResourceConverter":       dev = new ConverterDevice(m as ModuleResourceConverter);  break;
          case "ModuleKPBSConverter":           dev = new ConverterDevice(m as ModuleResourceConverter);  break;
          case "FissionReactor":                dev = new ConverterDevice(m as ModuleResourceConverter);  break;
          case "ModuleResourceHarvester":       dev = new DrillDevice(m as ModuleResourceHarvester);      break;
          case "ModuleLight":                   dev = new LightDevice(m as ModuleLight);                  break;
          case "ModuleColoredLensLight":        dev = new LightDevice(m as ModuleLight);                  break;
          case "ModuleMultiPointSurfaceLight":  dev = new LightDevice(m as ModuleLight);                  break;
          default: continue;
        }

        // add the device
        devices.Add(dev.id(), dev);
      }
    }
    // unloaded vessel
    else
    {
      // store data required to support multiple modules of same type in a part
      var PD = new Dictionary<string, Lib.module_prefab_data>();

      // for each part
      foreach(ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
      {
        // get part prefab (required for module properties)
        Part part_prefab = PartLoader.getPartInfoByName(p.partName).partPrefab;

        // get all module prefabs
        var module_prefabs = part_prefab.FindModulesImplementing<PartModule>();

        // clear module indexes
        PD.Clear();

        // for each module
        foreach(ProtoPartModuleSnapshot m in p.modules)
        {
          // get the module prefab
          // if the prefab doesn't contain this module, skip it
          PartModule module_prefab = Lib.ModulePrefab(module_prefabs, m.moduleName, PD);
          if (!module_prefab) continue;

          // if the module is disabled, skip it
          // note: this must be done after ModulePrefab is called, so that indexes are right
          if (!Lib.Proto.GetBool(m, "isEnabled")) continue;

          // depending on module name
          switch(m.moduleName)
          {
            case "ProcessController":             dev = new ProtoProcessDevice(m, module_prefab as ProcessController, p.flightID);          break;
            case "Greenhouse":                    dev = new ProtoGreenhouseDevice(m, p.flightID);                                           break;
            case "GravityRing":                   dev = new ProtoRingDevice(m, p.flightID);                                                 break;
            case "Emitter":                       dev = new ProtoEmitterDevice(m, p.flightID);                                              break;
            case "Harvester":                     dev = new ProtoHarvesterDevice(m, module_prefab as Harvester, p.flightID);                break;
            case "Laboratory":                    dev = new ProtoLaboratoryDevice(m, p.flightID);                                           break;
            case "Antenna":                       dev = new ProtoAntennaDevice(m, p.flightID);                                              break;
            case "ModuleDeployableSolarPanel":    dev = new ProtoPanelDevice(m, module_prefab as ModuleDeployableSolarPanel, p.flightID);   break;
            case "ModuleGenerator":               dev = new ProtoGeneratorDevice(m, module_prefab as ModuleGenerator, p.flightID);          break;
            case "ModuleResourceConverter":       dev = new ProtoConverterDevice(m, module_prefab as ModuleResourceConverter, p.flightID);  break;
            case "ModuleKPBSConverter":           dev = new ProtoConverterDevice(m, module_prefab as ModuleResourceConverter, p.flightID);  break;
            case "FissionReactor":                dev = new ProtoConverterDevice(m, module_prefab as ModuleResourceConverter, p.flightID);  break;
            case "ModuleResourceHarvester":       dev = new ProtoDrillDevice(m, module_prefab as ModuleResourceHarvester, p.flightID);      break;
            case "ModuleLight":                   dev = new ProtoLightDevice(m, p.flightID);                                                break;
            case "ModuleColoredLensLight":        dev = new ProtoLightDevice(m, p.flightID);                                                break;
            case "ModuleMultiPointSurfaceLight":  dev = new ProtoLightDevice(m, p.flightID);                                                break;
            default: continue;
          }

          // add the device
          devices.Add(dev.id(), dev);
        }
      }
    }

    // return all devices found
    return devices;
  }


  Dictionary<ScriptType, Script> scripts;
}


} // KERBALISM

