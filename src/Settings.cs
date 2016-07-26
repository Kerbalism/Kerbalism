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
    CosmicRadiation             = Lib.ConfigValue(cfg, "CosmicRadiation",             0.0000055555); // 0.02 rad/h
    StormRadiation              = Lib.ConfigValue(cfg, "StormRadiation",              0.0005555555); // 2.0 rad/h
    BeltRadiation               = Lib.ConfigValue(cfg, "BeltRadiation",               0.0055555555); // 20.0 rad/h
    ShieldingEfficiency         = Lib.ConfigValue(cfg, "ShieldingEfficiency",         0.95);
    MagnetosphereFalloff        = Lib.ConfigValue(cfg, "MagnetosphereFalloff",        0.33);
    BeltFalloff                 = Lib.ConfigValue(cfg, "BeltFalloff",                 0.1);

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
    RTGDecay                    = Lib.ConfigValue(cfg, "RTGDecay",                    true);
    ShowFlux                    = Lib.ConfigValue(cfg, "ShowFlux",                    true);
    RelativisticTime            = Lib.ConfigValue(cfg, "RelativisticTime",            false);
    LightSpeedScale             = Lib.ConfigValue(cfg, "LightSpeedScale",             1.0);
  }

  // temperature
  public static double SurvivalTemperature;               // ideal living temperature
  public static double SurvivalRange;                     // sweet spot around survival temperature

  // quality-of-life
  public static double QoL_FirmGround;                    // bonus to apply to quality-of-life when landed
  public static double QoL_PhoneHome;                     // bonus to apply to quality-of-life when linked
  public static double QoL_NotAlone;                      // bonus to apply to quality-of-life when crew count is >= 2

  // radiation
  public static double CosmicRadiation;                   // radiation outside a magnetosphere
  public static double StormRadiation;                    // radiation during a magnetic storm
  public static double BeltRadiation;                     // radiation inside a belt
  public static double ShieldingEfficiency;               // proportion of radiation blocked by shielding (at max amount)
  public static double MagnetosphereFalloff;              // the magnetosphere intensity fade out gradually outside it
  public static double BeltFalloff;                       // the bel intensity fade in/out gradually when crossing it

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
  public static bool   RTGDecay;                          // if true, radioisotope thermoelectric generator output will decay over time
  public static bool   ShowFlux;                          // if true, show solar/albedo/body flux in vessel info window
  public static bool   RelativisticTime;                  // if true, time on vessel dilate according to special relativity
  public static double LightSpeedScale;                   // used to scale the speed of light for gameplay purposes
}


} // KERBALISM