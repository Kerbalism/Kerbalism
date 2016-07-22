// ===================================================================================================================
// grow food in space
// ===================================================================================================================



using System;
using System.Collections.Generic;
using UnityEngine;

namespace KERBALISM {


public sealed class Greenhouse : PartModule
{
  // config
  [KSPField] public string resource_name = "Food";       // resource produced
  [KSPField] public string waste_name = "Crap";          // resource used for waste
  [KSPField] public double ec_rate;                      // EC consumption rate per-second, normalized for lamps=1.0
  [KSPField] public double waste_rate;                   // waste consumption rate per-second, to provide waste bonus
  [KSPField] public double harvest_size;                 // amount of food produced at harvest time
  [KSPField] public double growth_rate;                  // growth speed in average lighting conditions
  [KSPField] public double waste_bonus = 0.2;            // bonus applied to growth if waste is available
  [KSPField] public double soil_bonus = 0.5;             // bonus applied to growth if landed
  [KSPField] public string animation_name;               // name of animation to play for the shutters, optional
  [KSPField] public string emissive_object;              // name of an object with an emissive texture to use for the lamps, optional

  // persistence
  [KSPField(isPersistant = true)] public bool door_opened = false;            // if the door is opened
  [KSPField(isPersistant = true)] public double growth = 0.0;                 // current growth level

  // artifical lighting tweakable
  [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Lamps"),
   UI_FloatRange(minValue=0.0f, maxValue=1.0f, stepIncrement=0.01f)]
  public float lamps = 0.0f;

  // store current lighting conditions
  // note: persistant so it is accessible from proto vessel
  [KSPField(isPersistant = true)] public double lighting = 0.0;

  // store current growing speed per-second
  [KSPField(isPersistant = true)] public double growing = 0.0;

  // rmb status
  [KSPField(guiActive = true, guiName = "Growth")] public string GrowthStatus;        // growth percentual)
  [KSPField(guiActive = true, guiName = "Light")] public string LightStatus;          // lighting conditions
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
    ResourceCache.Produce(vessel, resource_name, reduced_harvest);

    // reset growth
    growth = 0.0;

    // show message
    Message.Post(Lib.BuildString("On <color=FFFFFF>", vessel.vesselName, "</color> an emergency harved produced <color=FFFFFF>",
      reduced_harvest.ToString("F0"), " ", resource_name, "</color>"));

    // record first harvest
    if (!Lib.Landed(vessel) && DB.Ready()) DB.NotificationData().first_space_harvest = 1;
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
    else
    {
      Events["OpenDoor"].active = false;
      Events["CloseDoor"].active = false;
      door_opened = true;
    }
  }

  // editor/r&d info
  public override string GetInfo()
  {
    return Lib.BuildString
    (
      "Grow food in space.\n\n",
      "- Harvest size: <b>", harvest_size.ToString("F0"), " ", resource_name, "</b>\n",
      "- Harvest time: <b>", Lib.HumanReadableDuration(1.0 / growth_rate), "</b>\n",
      "- Lamps EC rate: <b> ", Lib.HumanReadableRate(ec_rate), "</b>"
    );
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

    // get resource cache
    vessel_resources resources = ResourceCache.Get(vessel);

    // if lamp is on
    if (lamps > float.Epsilon)
    {
      // get resource handler
      resource_info ec = resources.Info(vessel, "ElectricCharge");

      // consume ec
      ec.Consume(ec_rate * lamps * Kerbalism.elapsed_s);

      // shut down the light if there isn't enough ec
      // note: comparing against amount at previous simulation step
      if (ec.amount <= double.Epsilon) lamps = 0.0f;
    }

    // determine lighting conditions
    // note: we ignore sun direction for gameplay reasons: else the user must reorient the greenhouse as the planets dance over time
    // - natural light depend on: distance from sun, direct sunlight, door status
    // - artificial light depend on: lamps tweakable and ec available, door status
    lighting = NaturalLighting(info.sun_dist) * info.sunlight * (door_opened ? 1.0 : 0.0) + lamps;

    // if can use waste, and there is some lighting
    double waste_perc = 0.0;
    if (waste_name.Length > 0 && lighting > double.Epsilon)
    {
      // get resource handler
      resource_info waste = resources.Info(vessel, waste_name);

      // consume waste
      waste.Consume(waste_rate * Kerbalism.elapsed_s);

      // determine waste bonus
      // note: comparing against amount from previous simulation step
      waste_perc = waste.amount / waste_rate;
    }

    // determine growth bonus
    double growth_bonus = soil_bonus * (info.landed ? 1.0 : 0.0) + waste_bonus * waste_perc;

    // grow the crop
    growing = growth_rate * (1.0 + growth_bonus) * lighting;
    growth += Kerbalism.elapsed_s * growing;

    // if it is harvest time
    if (growth >= 1.0)
    {
      // reset growth
      growth = 0.0;

      // produce food
      resources.Produce(vessel, resource_name, harvest_size);

      // show a message to the user
      Message.Post(Lib.BuildString("On <color=FFFFFF>", vessel.vesselName, "</color> the crop harvest produced <color=FFFFFF>",
        harvest_size.ToString("F0"), " ", resource_name, "</color>"));

      // record first space harvest
      if (!info.landed && DB.Ready()) DB.NotificationData().first_space_harvest = 1;
    }

    // set rmb ui status
    GrowthStatus = Lib.HumanReadablePerc(growth);
    LightStatus = Lib.HumanReadablePerc(lighting);
    WasteStatus = Lib.HumanReadablePerc(waste_perc);
    SoilStatus = info.landed ? "yes" : "no";
    TTAStatus = Lib.HumanReadableDuration(growing > double.Epsilon ? (1.0 - growth) / growing : 0.0);


    // enable/disable emergency harvest
    Events["EmergencyHarvest"].active = (growth >= 0.5);
  }

  // implement greenhouse mechanics for unloaded vessels
  public static void BackgroundUpdate(Vessel vessel, ProtoPartModuleSnapshot m, Greenhouse greenhouse, vessel_info info, vessel_resources resources, double elapsed_s)
  {
    // get protomodule data
    bool door_opened = Lib.Proto.GetBool(m, "door_opened");
    double growth = Lib.Proto.GetDouble(m, "growth");
    float lamps = Lib.Proto.GetFloat(m, "lamps");
    double lighting = Lib.Proto.GetDouble(m, "lighting");

    // if lamp is on
    if (lamps > float.Epsilon)
    {
      // get resource handler
      resource_info ec = resources.Info(vessel, "ElectricCharge");

      // consume ec
      ec.Consume(greenhouse.ec_rate * lamps * elapsed_s);

      // shut down the light if there isn't enough ec
      // note: comparing against amount at previous simulation step
      if (ec.amount <= double.Epsilon) lamps = 0.0f;
    }

    // determine lighting conditions
    // note: we ignore sun direction for gameplay reasons: else the user must reorient the greenhouse as the planets dance over time
    // - natural light depend on: distance from sun, direct sunlight, door status
    // - artificial light depend on: lamps tweakable and ec available, door status
    lighting = NaturalLighting(info.sun_dist) * info.sunlight * (door_opened ? 1.0 : 0.0) + lamps;

    // if can use waste, and there is some lighting
    double waste_perc = 0.0;
    if (greenhouse.waste_name.Length > 0 && lighting > double.Epsilon)
    {
      // get resource handler
      resource_info waste = resources.Info(vessel, greenhouse.waste_name);

      // consume waste
      waste.Consume(greenhouse.waste_rate * elapsed_s);

      // determine waste bonus
      // note: comparing against amount from previous simulation step
      waste_perc = waste.amount / greenhouse.waste_rate;
    }

    // determine growth bonus
    double growth_bonus = greenhouse.soil_bonus * (info.landed ? 1.0 : 0.0) + greenhouse.waste_bonus * waste_perc;

    // grow the crop
    double growing = greenhouse.growth_rate * (1.0 + growth_bonus) * lighting;
    growth += elapsed_s * growing;

    // if it is harvest time
    if (growth >= 1.0)
    {
      // reset growth
      growth = 0.0;

      // produce food
      resources.Produce(vessel, greenhouse.resource_name, greenhouse.harvest_size);

      // show a message to the user
      Message.Post(Lib.BuildString("On <color=FFFFFF>", vessel.vesselName, "</color> the crop harvest produced <color=FFFFFF>",
        greenhouse.harvest_size.ToString("F0"), " ", greenhouse.resource_name, "</color>"));

      // record first space harvest
      if (!info.landed && DB.Ready()) DB.NotificationData().first_space_harvest = 1;
    }

    // store data
    Lib.Proto.Set(m, "growth", growth);
    Lib.Proto.Set(m, "lamps", lamps);
    Lib.Proto.Set(m, "lighting", lighting);
    Lib.Proto.Set(m, "growth_diff", growing);
  }


  // return normalized natural lighting at specified distance from the sun (1 in the home world)
  public static double NaturalLighting(double sun_dist)
  {
    // return natural lighting
    // note: should be 1 at kerbin
    return Sim.SolarFlux(sun_dist) / Sim.SolarFluxAtHome();
  }


  // return true if at least a greenhouse is not growing in the specified vessel
  public static bool NoGrowth(Vessel v)
  {
    if (v.loaded)
    {
      foreach(var greenhouse in v.FindPartModulesImplementing<Greenhouse>())
      {
        if (greenhouse.growing <= double.Epsilon) return true;
      }
    }
    else
    {
      foreach(ProtoPartSnapshot part in v.protoVessel.protoPartSnapshots)
      {
        foreach(ProtoPartModuleSnapshot module in part.modules)
        {
          if (module.moduleName == "Greenhouse")
          {
            double growing = Lib.Proto.GetDouble(module, "growth");
            if (growing <= double.Epsilon) return true;
          }
        }
      }
    }
    return false;
  }


  // return partial data about greenhouses in a vessel
  public class partial_data { public double lighting; public double growing; public double growth; }
  public static List<partial_data> PartialData(Vessel v)
  {
    List<partial_data> ret = new List<partial_data>();
    if (v.loaded)
    {
      foreach(var greenhouse in v.FindPartModulesImplementing<Greenhouse>())
      {
        var data = new partial_data();
        data.lighting = greenhouse.lighting;
        data.growing = greenhouse.growing;
        data.growth = greenhouse.growth;
        ret.Add(data);
      }
    }
    else
    {
      foreach(ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
      {
        foreach(ProtoPartModuleSnapshot m in p.modules)
        {
          if (m.moduleName == "Greenhouse")
          {
            var data = new partial_data();
            data.lighting = Lib.Proto.GetDouble(m, "lighting");
            data.growing = Lib.Proto.GetDouble(m, "growing");
            data.growth = Lib.Proto.GetDouble(m, "growth");
            ret.Add(data);
          }
        }
      }
    }
    return ret;
  }
}


} // KERBALISM