// ===================================================================================================================
// store settings
// ===================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public static class Settings
{
  // EVA related
  public const double MonoPropellantOnEVA         = 5.0;                                                            // keep the stock behaviour
  public const double ElectricChargeOnEVA         = 10.0;                                                           // about 15min autonomy (at average LKO temp diff)
  public const double OxygenOnEVA                 = 0.05;                                                           // about 20min autonomy
  public const double HeadlightCost               = 0.005;                                                          // Headlight EC cost per-second

  // climate mechanic
  public const double SurvivalTemperature         = 295;                                                            // ideal kerbal survival temperature
  public const double TempDiffLKO                 = 161.08;                                                         // average temp diff from survival range in 80km orbit
  public const double ElectricChargePerSecond     = 0.02 / TempDiffLKO;                                             // per-kelvin, 0.02 per-second (at average LKO temp diff)
  public const double TemperatureDegradationRate  = 1.0 / (60.0 * 30.0 * TempDiffLKO);                              // 30min time-to-death (at average LKO temp diff)

  // food mechanic
  public const double FoodPerMeal                 = 1.0;                                                            // 1 per-day (consumed per-meal)
  public const double MealFrequency               = 60.0 * 60.0 * 6.0;                                              // interval between meals (1 day)
  public const double StarvedDegradationRate      = 1.0 / (60.0 * 60.0 * 6.0 * 8.0);                                // 8days time-to-death

  // oxygen mechanic
  public const double OxygenPerSecond             = 1.0 / (60.0 * 60.0 * 6.0);                                      // 1 per-day
  public const double DeprivedDegradationRate     = 1.0 / (60.0 * 8.0);                                             // 8min time-to-death
  public const double IntakeOxygenRate            = 0.005;                                                          // Oxygen generated per-second from scrubbers when inside breathable atmo

  // quality-of-life mechanic
  public const double StressedDegradationRate     = 1.0 / (60.0 * 60.0 * 6.0 * 40.0);                               // 40days time-to-instability (in worse conditions)
  public const double QoL_LivingSpaceBonus        = 1.0;
  public const double QoL_FirmGroundBonus         = 0.5;                                                            // bonus applied when landed
  public const double QoL_PhoneHomeBonus          = 0.5;                                                            // bonus applied when linked
  public const double QoL_NotAloneBonus           = 0.5;                                                            // bonus applied when not alone
  public const double QoL_KerbalVariance          = 0.33;                                                           // kerbal-specific variance scale

  // Temperature thresholds
  public const double TemperatureWarningThreshold = 1.0 - TemperatureDegradationRate * 60.0 * 20.0 * TempDiffLKO;   // 20min time-to-death (at average LKO temp diff)
  public const double TemperatureDangerThreshold  = 1.0 - TemperatureDegradationRate * 60.0 * 10.0 * TempDiffLKO;   // 10min time-to-death (at average LKO temp diff)
  public const double TemperatureFatalThreshold   = 1.0;

  // Starved thresholds
  public const double StarvedWarningThreshold     = 1.0 - StarvedDegradationRate * 60.0 * 60.0 * 6.0 * 6.0;         // 6days time-to-death
  public const double StarvedDangerThreshold      = 1.0 - StarvedDegradationRate * 60.0 * 60.0 * 6.0 * 4.0;         // 4days time-to-death
  public const double StarvedFatalThreshold       = 1.0;

  // Deprived thresholds
  public const double DeprivedWarningThreshold    = 1.0 - DeprivedDegradationRate * 60.0 * 6.0;                     // 6min time-to-death
  public const double DeprivedDangerThreshold     = 1.0 - DeprivedDegradationRate * 60.0 * 4.0;                     // 4min time-to-death
  public const double DeprivedFatalThreshold      = 1.0;

  // Stress thresholds
  public const double StressedWarningThreshold    = 1.0 - StressedDegradationRate * 60.0 * 60.0 * 6.0 * 20.0;       // 20days time-to-instability
  public const double StressedDangerThreshold     = 1.0 - StressedDegradationRate * 60.0 * 60.0 * 6.0 * 10.0;       // 10days time-to-instability
  public const double StressedEventThreshold      = 1.0;                                                            // trigger stress event

  // Resources thresholds
  public const double ResourceWarningThreshold    = 0.20;                                                           // 20%
  public const double ResourceDangerThreshold     = 0.0001;                                                         // empty (this isn't double.Epsilon to fix some fp issues)

  // greenhouse
  public const double GreenhouseWasteBonus        = 0.2;                                                            // bonus applied to growth if waste is available
  public const double GreenhouseSoilBonus         = 1.0;                                                            // bonus applied to growth if landed
  public const double GreenhouseDoorBonus         = 0.2;                                                            // bonus applied to artificial lights if door closed

  // resque missions resupply
  public const double ResqueMonoPropellant        = 2.5;                                                            // monoprop to give to resque mission kerbals
  public const double ResqueElectricCharge        = 999.0;                                                          // ec to give to resque mission kerbals
  public const double ResqueFood                  = 999.0;                                                          // food to give to resque mission kerbals
  public const double ResqueOxygen                = 999.0;                                                          // oxygen to give to resque mission kerbals

  // space weather
  public const double StormMinTime                = 648000.0;                                                       // safe time between storms (at home body), 1 month
  public const double StormMaxTime                = 2592000.0;                                                      // time at which a storm is guaranteed (at home body), 4 months
  public const double StormDuration               = 21600.0;                                                        // storm length, 1 kerbin-day
  public const double StormEjectionSpeed          = 1000000.0;                                                      // CME speed in m/s

  // radiation
  public const double MagnetosphereFalloff        = 0.2;                                                            // falloff zone outside magnetosphere, proportional
  public const double BeltFalloff                 = 0.05;                                                           // falloff zone around radiation belt, proportional
  public const double CosmicRadiation             = 0.02 / (60.0 * 60.0);                                           // radiation outside a magnetosphere, in rad/s (0.02 rad/h)
  public const double BeltRadiation               = 20.0 / (60.0 * 60.0);                                           // radiation inside a belt, in rad/s (20.0 rad/h)
  public const double StormRadiation              = 2.0 / (60.0 * 60.0);                                            // radiation during a magnetic storm, in rad/s (2.0 rad/h)
  public const double RadiationWarningThreshold   = 15.0;                                                           // dose at which warning is displayed, in rad
  public const double RadiationDangerThreshold    = 22.5;                                                           // dose at which danger is displayed, in rad
  public const double RadiationFatalThreshold     = 30.0;                                                           // fatal dose, in rad
  public const double ShieldingEfficiency         = 0.95;                                                           // max proportion of radiations blocked by shielding

  // penalities
  public const float DeathReputationPenalty       = 50.0f;                                                          // penalty applied on deaths related to life support

  // screen messages
  public const float MessageLength                = 6.66f;                                                          // time duration of messages on screen, in seconds
}


} // KERBALISM