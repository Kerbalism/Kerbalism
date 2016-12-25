using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public enum UnlinkedCtrl
{
  none,     // disable all controls
  limited,  // disable all controls except full/zero throttle and staging
  full      // do not disable controls at all
}


public static class Settings
{
  public static void parse()
  {
    var cfg = Lib.ParseConfig("Kerbalism/Kerbalism");

    // profile used
    Profile                     = Lib.ConfigValue(cfg, "Profile",                     string.Empty);

    // user-defined features
    Reliability                 = Lib.ConfigValue(cfg, "Reliability",                 false);
    Signal                      = Lib.ConfigValue(cfg, "Signal",                      false);
    Science                     = Lib.ConfigValue(cfg, "Science",                     false);
    SpaceWeather                = Lib.ConfigValue(cfg, "SpaceWeather",                false);
    Automation                  = Lib.ConfigValue(cfg, "Automation",                  false);

    // temperature
    SurvivalTemperature         = Lib.ConfigValue(cfg, "SurvivalTemperature",         295.0);
    SurvivalRange               = Lib.ConfigValue(cfg, "SurvivalRange",               5.0);

    // quality-of-life
    IdealLivingSpace            = Lib.ConfigValue(cfg, "IdealLivingSpace",            40.0);
    ComfortFirmGround           = Lib.ConfigValue(cfg, "ComfortFirmGround",           0.4);
    ComfortExercise             = Lib.ConfigValue(cfg, "ComfortExercise",             0.2);
    ComfortNotAlone             = Lib.ConfigValue(cfg, "ComfortNotAlone",             0.1);
    ComfortCallHome             = Lib.ConfigValue(cfg, "ComfortCallHome",             0.1);
    ComfortPanorama             = Lib.ConfigValue(cfg, "ComfortPanorama",             0.1);

    // pressure
    PressureFactor              = Lib.ConfigValue(cfg, "PressureFactor",              10.0);
    PressureThreshold           = Lib.ConfigValue(cfg, "PressureThreshold",           0.9);

    // poisoning
    PoisoningFactor             = Lib.ConfigValue(cfg, "PoisoningFactor",             0.0);
    PoisoningThreshold          = Lib.ConfigValue(cfg, "PoisoningThreshold",          0.02);

    // radiation
    ShieldingEfficiency         = Lib.ConfigValue(cfg, "ShieldingEfficiency",         0.9);
    StormRadiation              = Lib.ConfigValue(cfg, "StormRadiation",              5.0) / 3600.0;  // 5.0 rad/h
    ExternRadiation             = Lib.ConfigValue(cfg, "ExternRadiation",             0.04) / 3600.0; // 0.04 rad/h

    // storm
    StormMinTime                = Lib.ConfigValue(cfg, "StormMinTime",                2160000.0); // 100 days
    StormMaxTime                = Lib.ConfigValue(cfg, "StormMaxTime",                8640000.0); // 400 days
    StormDuration               = Lib.ConfigValue(cfg, "StormDuration",               21600.0);   // 1 day
    StormEjectionSpeed          = Lib.ConfigValue(cfg, "StormEjectionSpeed",          1000000.0); // 0.33% c

    // signal
    UnlinkedControl             = Lib.ConfigEnum (cfg, "UnlinkedControl",             UnlinkedCtrl.none);
    ExtendedAntenna             = Lib.ConfigValue(cfg, "ExtendedAntenna",             true);
    ControlRate                 = Lib.ConfigValue(cfg, "ControlRate",                 0.000001); // 1 bps

    // science
    ScienceDialog               = Lib.ConfigValue(cfg, "ScienceDialog",               true);

    // reliability
    QualityScale                = Lib.ConfigValue(cfg, "QualityScale",                4.0);
    CriticalChance              = Lib.ConfigValue(cfg, "CriticalChance",              0.25);
    SafeModeChance              = Lib.ConfigValue(cfg, "SafeModeChance",              0.5);
    IncentiveRedundancy         = Lib.ConfigValue(cfg, "IncentiveRedundancy",         false);

    // misc
    TrackingPivot               = Lib.ConfigValue(cfg, "TrackingPivot",               true);
    HeadLampsCost               = Lib.ConfigValue(cfg, "HeadLampsCost",               0.002);
    DeathReputation             = Lib.ConfigValue(cfg, "DeathReputation",             100.0f);
    BreakdownReputation         = Lib.ConfigValue(cfg, "BreakdownReputation",         10.0f);
    StockMessages               = Lib.ConfigValue(cfg, "StockMessages",               false);
    MessageLength               = Lib.ConfigValue(cfg, "MessageLength",               4.0f);
    LowQualityRendering         = Lib.ConfigValue(cfg, "LowQualityRendering",         false);
  }


  // profile used
  public static string Profile;                           // name of profile to use, if any

  // user-defined features
  public static bool   Reliability;                       // component malfunctions and critical failures
  public static bool   Signal;                            // communications using low-gain and high-gain antennas
  public static bool   Science;                           // science data storage, transmission and analysis
  public static bool   SpaceWeather;                      // coronal mass ejections
  public static bool   Automation;                        // control vessel components using scripts

  // temperature
  public static double SurvivalTemperature;               // ideal living temperature
  public static double SurvivalRange;                     // sweet spot around survival temperature

  // quality-of-life
  public static double IdealLivingSpace;                  // ideal living space per-capita in m^3
  public static double ComfortFirmGround;                 // firm-ground comfort factor
  public static double ComfortExercise;                   // exercise comfort factor
  public static double ComfortNotAlone;                   // not-alone comfort factor
  public static double ComfortCallHome;                   // call-home comfort factor
  public static double ComfortPanorama;                   // panorama comfort factor

  // pressure
  public static double PressureFactor;                    // pressurized modifier value for vessels below the threshold
  public static double PressureThreshold;                 // level of atmosphere resource that determine pressurized status

  // poisoning
  public static double PoisoningFactor;                   // poisoning modifier value for vessels below threshold
  public static double PoisoningThreshold;                // level of waste atmosphere resource that determine co2 poisoning status

  // radiation
  public static double ShieldingEfficiency;               // proportion of radiation blocked by shielding (at max amount)
  public static double StormRadiation;                    // radiation during a solar storm
  public static double ExternRadiation;                   // radiation outside the heliopause

  // storm
  public static double StormMinTime;                      // minimum interval between storms over a system
  public static double StormMaxTime;                      // maximum interval between storms over a system
  public static double StormDuration;                     // how long a storm last once it hit
  public static double StormEjectionSpeed;                // cme speed in m/s

  // signal
  public static UnlinkedCtrl UnlinkedControl;             // available control for unlinked vessels: 'none', 'limited' or 'full'
  public static bool   ExtendedAntenna;                   // antenna only work if extended
  public static double ControlRate;                       // data rate required for control, in Mb/s

  // science
  public static bool   ScienceDialog;                     // keep showing the stock science dialog

  // reliability
  public static double QualityScale;                      // scale applied to MTBF for high-quality components
  public static double CriticalChance;                    // proportion of malfunctions that lead to critical failures
  public static double SafeModeChance;                    // proportion of malfunctions fixed remotely for unmanned vessels
  public static bool   IncentiveRedundancy;               // if true, each malfunction will increase the MTBF of components in the same redundancy group

  // misc
  public static bool   TrackingPivot;                     // simulate tracking solar panel around the pivot
  public static double HeadLampsCost;                     // EC/s cost if eva headlamps are on
  public static float  DeathReputation;                   // reputation to remove in case of death
  public static float  BreakdownReputation;               // reputation to remove in case of breakdown
  public static bool   StockMessages;                     // use the stock messages instead of our own message box
  public static float  MessageLength;                     // duration of messages on screen in seconds
  public static bool   LowQualityRendering;               // use less particles to render the magnetic fields
}


} // KERBALISM