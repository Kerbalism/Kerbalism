// ====================================================================================================================
// life support mechanics
// ====================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


[KSPAddon(KSPAddon.Startup.MainMenu, true)]
public class LifeSupport : MonoBehaviour
{
  // keep it alive
  LifeSupport() { DontDestroyOnLoad(this); }


  // implement life support mechanics
  public void FixedUpdate()
  {
    // avoid case when DB isn't ready for whatever reason
    if (!DB.Ready()) return;

    // do nothing in the editors and the menus
    if (!Lib.SceneIsGame()) return;

    // do nothing if paused
    if (Lib.IsPaused()) return;

    // get time elapsed from last update
    double elapsed_s = TimeWarp.fixedDeltaTime;

    // for each vessel
    foreach(Vessel v in FlightGlobals.Vessels)
    {
      // skip invalid vessels
      if (!Lib.IsVessel(v)) continue;

      // skip dead eva kerbals
      if (EVA.IsDead(v)) continue;

      // get crew
      List<ProtoCrewMember> crew = v.loaded ? v.GetVesselCrew() : v.protoVessel.GetVesselCrew();

      // get vessel info from the cache
      vessel_info info = Cache.VesselInfo(v);

      // get temperature difference
      // note: for gameplay reasons, climatization doesn't consume anything landed at home
      bool landed_home = Lib.Landed(v) && v.mainBody == FlightGlobals.GetHomeBody();
      double temp_diff = landed_home ? 0.0 : Math.Abs(info.temperature - Settings.SurvivalTemperature);
      double temp_sign = landed_home ? 1.0 : info.temperature > Settings.SurvivalTemperature ? 1.0 : -1.0;

      // determine if inside breathable atmosphere
      bool breathable = BreathableAtmosphere(v);


      // for each crew
      foreach(ProtoCrewMember c in crew)
      {
        // get kerbal data
        kerbal_data kd = DB.KerbalData(c.name);

        // skip resque kerbals
        if (kd.resque == 1) continue;

        // skip disabled kerbals
        if (kd.disabled == 1) continue;

        // consume ec for climate control
        double ec_required = temp_diff * Settings.ElectricChargePerSecond * elapsed_s;
        double ec_consumed = Lib.RequestResource(v, "ElectricCharge", ec_required);
        double ec_perc = ec_required > 0.0 ? ec_consumed / ec_required : 0.0;

        // reset kerbal temperature, if necessary
        if (ec_required <= double.Epsilon || ec_perc >= 1.0 - double.Epsilon)
        {
          kd.temperature = 0.0;
        }
        else
        {
          // degenerate kerbal temperature
          kd.temperature += Settings.TemperatureDegradationRate * elapsed_s * (1.0 - ec_perc) * temp_diff * temp_sign;

          // kill kerbal if necessary
          if (kd.temperature <= -Settings.TemperatureFatalThreshold)
          {
            Message.Post(Severity.fatality, KerbalEvent.climate_low, v, c);
            Kerbalism.Kill(v, c);
          }
          else if (kd.temperature >= Settings.TemperatureFatalThreshold)
          {
            Message.Post(Severity.fatality, KerbalEvent.climate_high, v, c);
            Kerbalism.Kill(v, c);
          }
          // show warnings
          else if (kd.temperature <= -Settings.TemperatureDangerThreshold && kd.msg_freezing < 2)
          {
            Message.Post(Severity.danger, KerbalEvent.climate_low, v, c);
            kd.msg_freezing = 2;
          }
          else if (kd.temperature <= -Settings.TemperatureWarningThreshold && kd.msg_freezing < 1)
          {
            Message.Post(Severity.warning, KerbalEvent.climate_low, v, c);
            kd.msg_freezing = 1;
          }
          else if (kd.temperature > -Settings.TemperatureWarningThreshold && kd.msg_freezing > 0)
          {
            Message.Post(Severity.relax, KerbalEvent.climate_low, v, c);
            kd.msg_freezing = 0;
          }
          else if (kd.temperature >= Settings.TemperatureDangerThreshold && kd.msg_burning < 2)
          {
            Message.Post(Severity.danger, KerbalEvent.climate_high, v, c);
            kd.msg_burning = 2;
          }
          else if (kd.temperature >= Settings.TemperatureWarningThreshold && kd.msg_burning < 1)
          {
            Message.Post(Severity.warning, KerbalEvent.climate_high, v, c);
            kd.msg_burning = 1;
          }
          else if (kd.temperature < Settings.TemperatureWarningThreshold && kd.msg_burning > 0)
          {
            Message.Post(Severity.relax, KerbalEvent.climate_high, v, c);
            kd.msg_burning = 0;
          }
        }


        // if its meal time for this kerbal
        kd.time_since_food += elapsed_s;
        if (kd.time_since_food >= Settings.MealFrequency)
        {
          // consume food
          double food_consumed = Lib.RequestResource(v, "Food", Settings.FoodPerMeal);

          // reset kerbal starvation, if necessary
          if (food_consumed > double.Epsilon)
          {
            kd.starved = 0.0;
            kd.time_since_food = 0.0;
          }
          else
          {
            // degenerate kerbal starvation
            kd.starved += Settings.StarvedDegradationRate * elapsed_s;

            // kill kerbal if necessary
            if (kd.starved >= Settings.StarvedFatalThreshold)
            {
              Message.Post(Severity.fatality, KerbalEvent.food, v, c);
              Kerbalism.Kill(v, c);
            }
            // show warnings
            else if (kd.starved >= Settings.StarvedDangerThreshold && kd.msg_starved < 2)
            {
              Message.Post(Severity.danger, KerbalEvent.food, v, c);
              kd.msg_starved = 2;
            }
            else if (kd.starved >= Settings.StarvedWarningThreshold && kd.msg_starved < 1)
            {
              Message.Post(Severity.warning, KerbalEvent.food, v, c);
              kd.msg_starved = 1;
            }
            else if (kd.starved < Settings.StarvedWarningThreshold && kd.msg_starved > 0)
            {
              Message.Post(Severity.relax, KerbalEvent.food, v, c);
              kd.msg_starved = 0;
            }
          }

          // produce waste
          Lib.RequestResource(v, "Waste", -food_consumed);
        }

        // if not inside a breathable atmosphere
        if (!breathable)
        {
          // consume oxygen
          double oxygen_consumed = Lib.RequestResource(v, "Oxygen", Settings.OxygenPerSecond * elapsed_s);

          // reset kerbal deprivation, if necessary
          if (oxygen_consumed > double.Epsilon)
          {
            kd.deprived = 0.0;
          }
          else
          {
            // degenerate kerbal deprivation
            kd.deprived += Settings.DeprivedDegradationRate * elapsed_s;

            // kill kerbal if necessary
            if (kd.deprived >= Settings.DeprivedFatalThreshold)
            {
              Message.Post(Severity.fatality, KerbalEvent.oxygen, v, c);
              Kerbalism.Kill(v, c);
            }
            // show warnings
            else if (kd.deprived >= Settings.DeprivedDangerThreshold && kd.msg_deprived < 2)
            {
              Message.Post(Severity.danger, KerbalEvent.oxygen, v, c);
              kd.msg_deprived = 2;
            }
            else if (kd.deprived >= Settings.DeprivedWarningThreshold && kd.msg_deprived < 1)
            {
              Message.Post(Severity.warning, KerbalEvent.oxygen, v, c);
              kd.msg_deprived = 1;
            }
            else if (kd.deprived < Settings.DeprivedWarningThreshold && kd.msg_deprived > 0)
            {
              Message.Post(Severity.relax, KerbalEvent.oxygen, v, c);
              kd.msg_deprived = 0;
            }
          }

          // produce CO2
          Lib.RequestResource(v, "CO2", -oxygen_consumed);
        }
      }
    }
  }


  // return true if inside a breathable atmosphere
  public static bool BreathableAtmosphere(Vessel v)
  {
    return v.mainBody.atmosphereContainsOxygen && v.mainBody.GetPressure(v.altitude) > 25.0;
  }


  // estimate time to depletion for food
  // - food_consumption: food consumed per-second
  // - food_level: proportion of amount to capacity, or 1 if no capacity
  // - return: seconds to depletion
  public static double TimeToDepletionFood(Vessel v, out double consumption, out double level)
  {
    // get food info
    double amount = Lib.GetResourceAmount(v, "Food");
    double capacity = Lib.GetResourceCapacity(v, "Food");
    level = capacity > 0.0 ? amount / capacity : 1.0;

    // calculate consumption
    int crew_count = Lib.CrewCount(v);
    consumption = (double)crew_count * Settings.FoodPerMeal / Settings.MealFrequency;

    // return time to depletion
    return crew_count > 0 ? amount / consumption : double.NaN;
  }


  // estimate time to depletion for oxygen
  // - food_consumption: food consumed per-second
  // - food_level: proportion of amount to capacity, or 1 if no capacity
  // - return: seconds to depletion
  public static double TimeToDepletionOxygen(Vessel v, out double consumption, out double level)
  {
    // get scrubbers
    var scrubbers = Scrubber.GetScrubbers(v);

    // get oxygen info
    double amount = Lib.GetResourceAmount(v, "Oxygen");
    double capacity = Lib.GetResourceCapacity(v, "Oxygen");
    level = capacity > 0.0 ? amount / capacity : 1.0;

    // get oxygen recycled by scrubbers
    double recycled = 0.0;
    double ec_left = Lib.GetResourceAmount(v, "ElectricCharge");
    double co2_left = Settings.OxygenPerSecond * (double)Lib.CrewCount(v); //< potential co2, to avoid problem with 'perfect' co2 consumption leaving amount at 0.0
    foreach(Scrubber scrubber in scrubbers)
    {
      if (scrubber.is_enabled)
      {
        double co2_consumed = Math.Min(co2_left, scrubber.co2_rate);
        ec_left -= scrubber.ec_rate;
        co2_left -= co2_consumed;
        if (ec_left > -double.Epsilon && co2_left > -double.Epsilon) recycled += co2_consumed * scrubber.efficiency;
        else break;
      }
    }

    // calculate consumption
    int crew_count = Lib.CrewCount(v);
    consumption = !LifeSupport.BreathableAtmosphere(v) ? (double)crew_count * Settings.OxygenPerSecond - recycled : 0.0;
    return crew_count > 0 ? amount / consumption : double.NaN;
  }
}


} // KERBALISM