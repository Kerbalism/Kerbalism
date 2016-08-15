// ===================================================================================================================
// store and parse settings
// ===================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public static class Settings
{
  static Settings()
  {
    var cfg = Lib.ParseConfig("Kerbalism/Kerbalism");

    // temperature
    SurvivalTemperature         = Lib.ConfigValue(cfg, "SurvivalTemperature",         295.0);
    SurvivalRange               = Lib.ConfigValue(cfg, "SurvivalRange",               0.0);

    // quality-of-life
    QoL_FirmGround              = Lib.ConfigValue(cfg, "QoL_FirmGround",              1.0);
    QoL_PhoneHome               = Lib.ConfigValue(cfg, "QoL_PhoneHome",               0.5);
    QoL_NotAlone                = Lib.ConfigValue(cfg, "QoL_NotAlone",                1.5);

    // radiation
    ShieldingEfficiency         = Lib.ConfigValue(cfg, "ShieldingEfficiency",         0.95);
    StormRadiation              = Lib.ConfigValue(cfg, "StormRadiation",              3.0) / 3600.0;  // 3.0 rad/h
    ExternRadiation             = Lib.ConfigValue(cfg, "ExternRadiation",             0.06) / 3600.0; // 0.06 rad/h

    // storm
    StormMinTime                = Lib.ConfigValue(cfg, "StormMinTime",                2160000.0); // 100 days
    StormMaxTime                = Lib.ConfigValue(cfg, "StormMaxTime",                8640000.0); // 400 days
    StormDuration               = Lib.ConfigValue(cfg, "StormDuration",               21600.0);   // 1 day
    StormEjectionSpeed          = Lib.ConfigValue(cfg, "StormEjectionSpeed",          1000000.0); // 0.33% c

    // misc
    RemoteControlLink           = Lib.ConfigValue(cfg, "RemoteControlLink",           true);
    MonoPropellantOnEVA         = Lib.ConfigValue(cfg, "MonoPropellantOnEVA",         5.0);
    MonoPropellantOnResque      = Lib.ConfigValue(cfg, "MonoPropellantOnResque",      5.0);
    HeadlightCost               = Lib.ConfigValue(cfg, "HeadlightCost",               0.005);
    DeathReputationPenalty      = Lib.ConfigValue(cfg, "DeathReputationPenalty",      50.0f);
    BreakdownReputationPenalty  = Lib.ConfigValue(cfg, "BreakdownReputationPenalty",  10.0f);
    MessageLength               = Lib.ConfigValue(cfg, "MessageLength",               6.66f);
    AtmosphereDecay             = Lib.ConfigValue(cfg, "AtmosphereDecay",             true);
    ShowFlux                    = Lib.ConfigValue(cfg, "ShowFlux",                    false);
    ShowRates                   = Lib.ConfigValue(cfg, "ShowRates",                   false);
    RelativisticTime            = Lib.ConfigValue(cfg, "RelativisticTime",            false);
    LightSpeedScale             = Lib.ConfigValue(cfg, "LightSpeedScale",             1.0);
    LowQualityFieldRendering    = Lib.ConfigValue(cfg, "LowQualityFieldRendering",    false);
  }

  // temperature
  public static double SurvivalTemperature;               // ideal living temperature
  public static double SurvivalRange;                     // sweet spot around survival temperature

  // quality-of-life
  public static double QoL_FirmGround;                    // bonus to apply to quality-of-life when landed
  public static double QoL_PhoneHome;                     // bonus to apply to quality-of-life when linked
  public static double QoL_NotAlone;                      // bonus to apply to quality-of-life when crew count is >= 2

  // radiation
  public static double ShieldingEfficiency;               // proportion of radiation blocked by shielding (at max amount)
  public static double StormRadiation;                    // radiation during a solar storm
  public static double ExternRadiation;                   // radiation outside the heliopause

  // storm
  public static double StormMinTime;                      // minimum interval between storms over a system
  public static double StormMaxTime;                      // maximum interval between storms over a system
  public static double StormDuration;                     // how long a storm last once it hit
  public static double StormEjectionSpeed;                // cme speed in m/s

  // misc
  public static bool   RemoteControlLink;                 // if true, a link home is required to control probes
  public static double MonoPropellantOnEVA;               // how much monopropellant to take on EVA
  public static double MonoPropellantOnResque;            // how much monopropellant to gift to resque missions
  public static double HeadlightCost;                     // EC/s cost if eva headlights are on
  public static float  DeathReputationPenalty;            // reputation to remove in case of death
  public static float  BreakdownReputationPenalty;        // reputation to remove in case of breakdown
  public static float  MessageLength;                     // duration of messages on screen in seconds
  public static bool   AtmosphereDecay;                   // if true, unloaded vessel orbits inside atmosphere will decay
  public static bool   ShowFlux;                          // if true, show solar/albedo/body flux in vessel info window
  public static bool   ShowRates;                         // if true, show consumption/production rates in vessel info window
  public static bool   RelativisticTime;                  // if true, time on vessel dilate according to special relativity
  public static double LightSpeedScale;                   // used to scale the speed of light for gameplay purposes
  public static bool   LowQualityFieldRendering;          // use less particles to render the magnetic fields
}


} // KERBALISM