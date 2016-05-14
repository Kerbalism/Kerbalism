// ===================================================================================================================
// Greenhouse module
// ===================================================================================================================



using System;
using System.Collections.Generic;
using UnityEngine;

namespace KERBALISM {


public class Greenhouse : PartModule
{
  // .cfg
  // note: persistent because required in background processing
  [KSPField(isPersistant = true)] public string resource_name = "Food";       // resource produced
  [KSPField(isPersistant = true)] public string waste_name = "Crap";          // resource used for waste
  [KSPField(isPersistant = true)] public string input_name = "";              // optional input resource required
  [KSPField(isPersistant = true)] public double ec_rate;                      // EC consumption rate per-second, normalized for lamps=1.0
  [KSPField(isPersistant = true)] public double waste_rate;                   // waste consumption rate per-second, to provide waste bonus
  [KSPField(isPersistant = true)] public double input_rate;                   // input consumption rate per-second
  [KSPField(isPersistant = true)] public double harvest_size;                 // amount of food produced at harvest time
  [KSPField(isPersistant = true)] public double growth_rate;                  // growth speed in average lighting conditions
  [KSPField(isPersistant = true)] public double waste_bonus = 0.2;            // bonus applied to growth if waste is available
  [KSPField(isPersistant = true)] public double soil_bonus = 0.5;             // bonus applied to growth if landed

  [KSPField] public string animation_name;                                    // name of animation to play for the shutters, optional
  [KSPField] public string emissive_object;                                   // name of an object with an emissive texture to use for the lamps, optional

  // persistence
  // note: also configurable per-part
  [KSPField(isPersistant = true)] public bool door_opened = false;            // if the door is opened
  [KSPField(isPersistant = true)] public double growth = 0.0;                 // current growth level

  // artifical lighting tweakable
  [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Lamps"),
   UI_FloatRange(minValue=0.0f, maxValue=1.0f, stepIncrement=0.01f)]
  public float lamps = 0.0f;

  // store current lighting conditions
  // note: here so the gui is able to display a status color for the greenhouse
  // note: persistant so it is accessible from proto vessel
  [KSPField(isPersistant = true)] public double lighting = 0.0;

  // store current growing speed per-second
  [KSPField(isPersistant = true)] public double growing = 0.0;

  // rmb status
  [KSPField(guiActive = true, guiName = "Growth")] public string GrowthStatus;        // growth percentual)
  [KSPField(guiActive = true, guiName = "Light")] public string LightStatus;          // lighting conditions
  [KSPField(guiActive = true, guiName = "Input")] public string InputStatus;          // input resource conditions
  [KSPField(guiActive = true, guiName = "Waste")] public string WasteStatus;          // waste bonus
  [KSPField(guiActive = true, guiName = "Soil")] public string SoilStatus;            // soil bonus
  [KSPField(guiActive = true, guiName = "Time to harvest")] public string TTAStatus;  // soil bonus

  // rmb open door
  [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Open shutters", active = false)]
  public void OpenDoor()
  {
    door_opened = true;

    if (animation_name.Length > 0)
    {
      Events["OpenDoor"].active = false;
      Events["CloseDoor"].active = true;

      Animation[] anim = this.part.FindModelAnimators(animation_name);
      if (anim.Length > 0)
      {
        anim[0][animation_name].normalizedTime = 0.0f;
        anim[0][animation_name].speed = Math.Abs(anim[0][animation_name].speed);
        anim[0].Play(animation_name);
      }
    }
  }

  // rmb close door
  [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Close shutters", active = false)]
  public void CloseDoor()
  {
    door_opened = false;

    if (animation_name.Length > 0)
    {
      Events["OpenDoor"].active = true;
      Events["CloseDoor"].active = false;

      Animation[] anim = this.part.FindModelAnimators(animation_name);
      if (anim.Length > 0)
      {
        anim[0][animation_name].normalizedTime = 1.0f;
        anim[0][animation_name].speed = -Math.Abs(anim[0][animation_name].speed);
        anim[0].Play(animation_name);
      }
    }
  }

  // rmb emergency harvest
  [KSPEvent(guiActive = true, guiName = "Emergency harvest", active = false)]
  public void EmergencyHarvest()
  {
    // calculate reduced harvest size
    double reduced_harvest = harvest_size * growth * 0.5;

    // produce reduced quantity of food, proportional to current growth
    part.RequestResource(resource_name, -reduced_harvest);

    // reset growth
    growth = 0.0;

    // show message
    Message.Post("On <color=FFFFFF>" + vessel.vesselName + "</color> an emergency harved produced <color=FFFFFF>" + reduced_harvest.ToString("F0") + " " + resource_name + "</color>");
  }

  // pseudo-ctor
  public override void OnStart(StartState state)
  {
    if (animation_name.Length > 0)
    {
      // enable/disable rmb ui events based on initial door state as per .cfg files
      Events["OpenDoor"].active = !door_opened;
      Events["CloseDoor"].active = door_opened;

      // set shutter animation to beginning or end
      Animation[] anim = this.part.FindModelAnimators(animation_name);
      if (anim.Length > 0)
      {
        if (door_opened)
        {
          anim[0][animation_name].normalizedTime = 1.0f;
          anim[0][animation_name].speed = Math.Abs(anim[0][animation_name].speed);
          anim[0].Play(animation_name);
        }
        else
        {
          anim[0][animation_name].normalizedTime = 0.0f;
          anim[0][animation_name].speed = -Math.Abs(anim[0][animation_name].speed);
          anim[0].Play(animation_name);
        }
      }
    }
  }

  // editor/r&d info
  public override string GetInfo()
  {
    string input_str = input_name.Length > 0 && input_rate > 0.0
      ? "\n\n<color=cyan>Require:</color>\n- " + input_name + ": <b>" + Lib.HumanReadableRate(input_rate) + "</b>"
      : "";

    return "Grow food in space.\n\n"
         + "- Harvest size: <b>" + harvest_size + " " + resource_name + "</b>\n"
         + "- Harvest time: <b>" + Lib.HumanReadableDuration(1.0 / growth_rate) + "</b>\n"
         + "- Lamps EC rate: <b> " + Lib.HumanReadableRate(ec_rate) + "</b>"
         + input_str;
  }

  // implement greenhouse mechanics
  public void FixedUpdate()
  {
    // set emissive intensity from lamp tweakable
    if (emissive_object.Length > 0)
    {
      foreach(Renderer rdr in part.GetComponentsInChildren<Renderer>())
      {
        if (rdr.name == emissive_object) { rdr.material.SetColor("_EmissiveColor", new Color(lamps, lamps, lamps, 1.0f)); break; }
      }
    }

    // do nothing else in the editor
    if (HighLogic.LoadedSceneIsEditor) return;

    // get vessel info from the cache
    vessel_info info = Cache.VesselInfo(vessel);

    // get time elapsed from last update
    double elapsed_s = TimeWarp.fixedDeltaTime;

    // consume ec for lighting
    double ec_light_perc = 0.0;
    if (lamps > float.Epsilon)
    {
      double ec_light_required = ec_rate * elapsed_s * lamps;
      double ec_light = part.RequestResource("ElectricCharge", ec_light_required);
      ec_light_perc = ec_light / ec_light_required;

      // if there isn't enough ec for lighting
      if (ec_light <= double.Epsilon)
      {
        // shut down the light
        lamps = 0.0f;
      }
    }

    // determine lighting conditions
    // note: we ignore sun direction for gameplay reasons: else the user must reorient the greenhouse as the planets dance over time
    // - natural light depend on: distance from sun, direct sunlight, door status
    // - artificial light depend on: lamps tweakable and ec available, door status
    lighting = NaturalLighting(info.sun_dist) * (info.sunlight ? 1.0 : 0.0) * (door_opened ? 1.0 : 0.0) + lamps * ec_light_perc;

    // consume input resource if any
    double input_perc = 1.0;
    if (input_name.Length > 0 && input_rate > 0.0 && lighting > double.Epsilon)
    {
      double input_required = input_rate * elapsed_s;
      double input = part.RequestResource(input_name, input_required);
      input_perc = input / input_required;
    }

    // consume waste
    // note: not consumed when there is no lighting
    double waste_perc = 0.0;
    if (waste_name.Length > 0 && lighting > double.Epsilon)
    {
      double waste_required = waste_rate * elapsed_s;
      double waste = part.RequestResource(waste_name, waste_required);
      waste_perc = waste / waste_required;
    }

    // determine growth bonus
    double growth_bonus = 0.0;
    growth_bonus += soil_bonus * (Lib.Landed(vessel) ? 1.0 : 0.0);
    growth_bonus += waste_bonus * waste_perc;

    // grow the crop
    growing = growth_rate * (1.0 + growth_bonus) * input_perc * lighting;
    growth += elapsed_s * growing;

    // if it is harvest time
    if (growth >= 1.0)
    {
      // reset growth
      growth = 0.0;

      // produce food
      part.RequestResource(resource_name, -harvest_size);

      // show a message to the user
      Message.Post("On <color=FFFFFF>" + vessel.vesselName + "</color> the crop harvest produced <color=FFFFFF>" + harvest_size.ToString("F0") + " " + resource_name + "</color>");
    }

    // set rmb ui status
    GrowthStatus = (growth * 100.0).ToString("F0") + "%";
    LightStatus = (lighting * 100.0).ToString("F0") + "%";
    InputStatus = (input_perc * 100.0).ToString("F0") + "%";
    WasteStatus = (waste_perc * 100.0).ToString("F0") + "%";
    SoilStatus = Lib.Landed(vessel) ? "yes" : "no";
    TTAStatus = Lib.HumanReadableDuration(growing > double.Epsilon ? 1.0 / growing : 0.0);
    Fields["InputStatus"].guiName = input_name;
    Fields["InputStatus"].guiActive = input_name.Length > 0 && input_rate > 0.0;


    // enable/disable emergency harvest
    Events["EmergencyHarvest"].active = (growth >= 0.5);
  }

  // implement greenhouse mechanics for unloaded vessels
  public static void BackgroundUpdate(Vessel vessel, uint flight_id)
  {
    // get data
    ProtoPartModuleSnapshot m = Lib.GetProtoModule(vessel, flight_id, "Greenhouse");
    string resource_name = Lib.GetProtoValue(m, "resource_name", "Food");  //< support versions before 0.9.9.5
    string waste_name = Lib.GetProtoValue(m, "waste_name", "Crap");  //< support versions before 0.9.9.5
    string input_name = Lib.GetProtoValue(m, "input_name", ""); //< from version 0.9.9.8
    double ec_rate = Lib.GetProtoValue<double>(m, "ec_rate");
    double waste_rate = Lib.GetProtoValue<double>(m, "waste_rate");
    double input_rate = Lib.GetProtoValue<double>(m, "input_rate", 0.0); //< from version 0.9.9.8
    double harvest_size = Lib.GetProtoValue<double>(m, "harvest_size");
    double growth_rate = Lib.GetProtoValue<double>(m, "growth_rate");
    double waste_bonus = Lib.GetProtoValue<double>(m, "waste_bonus", 0.2); //< support versions before 0.9.9.5
    double soil_bonus = Lib.GetProtoValue<double>(m, "soil_bonus", 0.5);  //< support versions before 0.9.9.5

    bool door_opened = Lib.GetProtoValue<bool>(m, "door_opened");
    double growth = Lib.GetProtoValue<double>(m, "growth");
    float lamps = Lib.GetProtoValue<float>(m, "lamps");
    double lighting = Lib.GetProtoValue<double>(m, "lighting");

    // get time elapsed from last update
    double elapsed_s = TimeWarp.fixedDeltaTime;

    // get vessel info from the cache
    vessel_info info = Cache.VesselInfo(vessel);

    // consume ec for lighting
    double ec_light_perc = 0.0;
    if (lamps > float.Epsilon)
    {
      double ec_light_required = ec_rate * elapsed_s * lamps;
      double ec_light = Lib.RequestResource(vessel, "ElectricCharge", ec_light_required);
      ec_light_perc = ec_light / ec_light_required;

      // if there isn't enough ec for lighting
      if (ec_light <= double.Epsilon)
      {
        // shut down the light
        lamps = 0.0f;
      }
    }

    // determine lighting conditions
    // note: we ignore sun direction for gameplay reasons: else the user must reorient the greenhouse as the planets dance over time
    // - natural light depend on: distance from sun, direct sunlight, door status
    // - artificial light depend on: lamps tweakable and ec available, door status
    lighting = NaturalLighting(info.sun_dist) * (info.sunlight ? 1.0 : 0.0) * (door_opened ? 1.0 : 0.0) + lamps * ec_light_perc;

    // consume input resource if any
    double input_perc = 1.0;
    if (input_name.Length > 0 && input_rate > 0.0 && lighting > double.Epsilon)
    {
      double input_required = input_rate * elapsed_s;
      double input = Lib.RequestResource(vessel, input_name, input_required);
      input_perc = input / input_required;
    }

    // consume waste
    // note: not consumed when there is no lighting
    double waste_perc = 0.0;
    if (waste_name.Length > 0 && lighting > double.Epsilon)
    {
      double waste_required = waste_rate * elapsed_s;
      double waste = Lib.RequestResource(vessel, waste_name, waste_required);
      waste_perc = waste / waste_required;
    }

    // determine growth bonus
    double growth_bonus = 0.0;
    growth_bonus += soil_bonus * (Lib.Landed(vessel) ? 1.0 : 0.0);
    growth_bonus += waste_bonus * waste_perc;

    // grow the crop
    double growing = growth_rate * (1.0 + growth_bonus) * input_perc * lighting;
    growth += elapsed_s * growing;

    // if it is harvest time
    if (growth >= 1.0)
    {
      // reset growth
      growth = 0.0;

      // produce food
      Lib.RequestResource(vessel, resource_name, -harvest_size);

      // show a message to the user
      Message.Post("On <color=FFFFFF>" + vessel.vesselName + "</color> the crop harvest produced <color=FFFFFF>" + harvest_size.ToString("F0") + " " + resource_name + "</color>");
    }

    // store data
    Lib.SetProtoValue(m, "growth", growth);
    Lib.SetProtoValue(m, "lamps", lamps);
    Lib.SetProtoValue(m, "lighting", lighting);
    Lib.SetProtoValue(m, "growth_diff", growing);
  }


  // return normalized natural lighting at specified distance from the sun (1 in the home world)
  public static double NaturalLighting(double sun_dist)
  {
    // return natural lighting
    // note: should be 1 at kerbin
    return Sim.SolarFlux(sun_dist) / Sim.SolarFluxAtHome();
  }


  // return read-only list of greenhouses in a vessel
  public static List<Greenhouse> GetGreenhouses(Vessel v, string resource_name="")
  {
    if (v.loaded)
    {
      var ret = v.FindPartModulesImplementing<Greenhouse>();
      if (resource_name.Length > 0) ret = ret.FindAll(k => k.resource_name == resource_name);
      return ret == null ? new List<Greenhouse>() : ret;
    }
    else
    {
      List<Greenhouse> ret = new List<Greenhouse>();
      foreach(ProtoPartSnapshot part in v.protoVessel.protoPartSnapshots)
      {
        foreach(ProtoPartModuleSnapshot module in part.modules)
        {
          if (module.moduleName == "Greenhouse")
          {
            Greenhouse greenhouse = new Greenhouse();
            greenhouse.resource_name = Lib.GetProtoValue(module, "resource_name", "Food");  //< support versions before 0.9.9.5
            greenhouse.waste_name = Lib.GetProtoValue(module, "waste_name", "Crap");  //< support versions before 0.9.9.5
            greenhouse.input_name = Lib.GetProtoValue(module, "input_name", "");  //< from version 0.9.9.8
            greenhouse.ec_rate = Lib.GetProtoValue<double>(module, "ec_rate");
            greenhouse.waste_rate = Lib.GetProtoValue<double>(module, "waste_rate");
            greenhouse.input_rate = Lib.GetProtoValue<double>(module, "input_rate", 0.0);
            greenhouse.harvest_size = Lib.GetProtoValue<double>(module, "harvest_size");
            greenhouse.growth_rate = Lib.GetProtoValue<double>(module, "growth_rate");
            greenhouse.waste_bonus = Lib.GetProtoValue(module, "waste_bonus", 0.2); //< support versions before 0.9.9.5
            greenhouse.soil_bonus = Lib.GetProtoValue(module, "soil_bonus", 0.5);  //< support versions before 0.9.9.5
            greenhouse.door_opened = Lib.GetProtoValue<bool>(module, "door_opened");
            greenhouse.growth = Lib.GetProtoValue<double>(module, "growth");
            greenhouse.lamps = Lib.GetProtoValue<float>(module, "lamps");
            greenhouse.lighting = Lib.GetProtoValue<double>(module, "lighting");
            greenhouse.growing = Lib.GetProtoValue<double>(module, "growing");
            if (resource_name.Length == 0 || greenhouse.resource_name == resource_name) ret.Add(greenhouse);
          }
        }
      }
      return ret;
    }
  }
}


} // KERBALISM