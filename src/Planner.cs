// ====================================================================================================================
// the vessel planner
// ====================================================================================================================


using System;
using System.Collections.Generic;
using ModuleWheels;
using UnityEngine;


namespace KERBALISM {


public class Planner
{
  public class environment_data
  {
    public CelestialBody body;                            // target body
    public double altitude;                               // target altitude
    public bool landed;                                   // true if landed
    public bool breathable;                               // true if inside breathable atmosphere
    public double sun_dist;                               // distance from the sun
    public double sun_flux;                               // flux received from the sun
    public double body_flux;                              // flux received from albedo radiation of the body, in direction of the sun
    public double body_back_flux;                         // flux received from albedo radiation of the body, in direction opposite to the sun
    public double background_temp;                        // space background temperature
    public double sun_temp;                               // temperature of a blackbody emitting sun flux
    public double body_temp;                              // temperature of a blackbody emitting body flux
    public double body_back_temp;                         // temperature of a blackbody emitting body back flux
    public double light_temp;                             // temperature at sunlight, if outside atmosphere
    public double shadow_temp;                            // temperature in shadow, if outside atmosphere
    public double atmo_temp;                              // temperature inside atmosphere, if any
    public double orbital_period;                         // length of orbit
    public double shadow_period;                          // length of orbit in shadow
    public double shadow_time;                            // proportion of orbit that is in shadow
    public double temp_diff;                              // average difference from survival temperature
    public double atmo_factor;                            // proportion of sun flux not absorbed by the atmosphere
  }


  public class crew_data
  {
    public uint count;                                    // number of crew on board
    public uint capacity;                                 // crew capacity of the vessel
    public bool engineer;                                 // true if an engineer is on board
  }


  public class ec_data
  {
    public double storage;                                // ec stored
    public double consumed;                               // ec consumed
    public double generated_sunlight;                     // ec generated in sunlight
    public double generated_shadow;                       // ec generated in shadow
    public double life_expectancy_sunlight;               // time-to-death for lack of climatization in sunlight
    public double life_expectancy_shadow;                 // time-to-death for lack of climatization in shadow
    public double best_ec_generator;                      // rate of best generator (for redundancy calculation)
  }


  public class food_data
  {
    public double storage;                                // food stored
    public double consumed;                               // food consumed
    public double cultivated;                             // food cultivated
    public double life_expectancy;                        // time-to-death for lack of food
    public double greenhouse_cost;                        // ec cost of artificial lighting for the greenhouses
    public string cultivated_tooltip = "";                // contain time-to-harvest for all the greenhouses
  }


  public class oxygen_data
  {
    public double scrubber_efficiency;                    // tech-dependent scrubber efficiency
    public double storage;                                // oxygen stored
    public double consumed;                               // oxygen consumed
    public double recycled;                               // oxygen recycled
    public double life_expectancy;                        // time-to-death for lack of oxygen
    public double scrubber_cost;                          // ec cost of all scrubbers
  }


  public class qol_data
  {
    public double living_space;                           // living space per-crew
    public double entertainment = 1.0;                    // store multiplication of all entertainment from parts
    public string factors;                                // description of other quality-of-life factors
    public double time_to_instability;                    // time-to-instability for stress
  }


  public class radiation_data
  {
    public double shielding_amount;                       // capacity of radiation shielding on the vessel
    public double shielding_capacity;                     // amount of radiation shielding on the vessel
    public double[] life_expectancy;                      // time-to-death or time-to-safemode for radiations (cosmic/storm/belt levels)
  }


  public class reliability_data
  {
    public double quality;                                // manufacturing quality
    public double failure_year;                           // estimated failures per-year, averaged per-component
    public string redundancy;                             // verbose description of redundancies
  }


  public class signal_data
  {
    public double ecc;                                    // error correcting code efficiency
    public double range;                                  // range of best antenna, if any
    public double transmission_cost_min;                  // min data transmission cost of best antenna, if any
    public double transmission_cost_max;                  // max data transmission cost of best antenna, if any
    public double relay_range;                            // range of best relay antenna, if any
    public double relay_cost;                             // relay cost of best relay antenna, if any
    public double second_best_range;                      // range of second-best antenna (for reliability calculation)
  }


  // styles
  GUIStyle leftmenu_style;
  GUIStyle midmenu_style;
  GUIStyle rightmenu_style;
  GUIStyle row_style;
  GUIStyle title_style;
  GUIStyle content_style;
  GUIStyle quote_style;

  // body index & situation
  int body_index;
  int situation_index;

  // current planner page
  uint page;


  // ctor
  public Planner()
  {
    // set default body index & situation
    body_index = FlightGlobals.GetHomeBodyIndex();
    situation_index = 1;

    // left menu style
    leftmenu_style = new GUIStyle(HighLogic.Skin.label);
    leftmenu_style.richText = true;
    leftmenu_style.normal.textColor = new Color(0.75f, 0.75f, 0.75f, 1.0f);
    leftmenu_style.fixedWidth = 120.0f;
    leftmenu_style.stretchHeight = true;
    leftmenu_style.fontSize = 10;
    leftmenu_style.alignment = TextAnchor.MiddleLeft;

    // mid menu style
    midmenu_style = new GUIStyle(leftmenu_style);
    midmenu_style.fixedWidth = 0.0f;
    midmenu_style.stretchWidth = true;
    midmenu_style.alignment = TextAnchor.MiddleCenter;

    // right menu style
    rightmenu_style = new GUIStyle(leftmenu_style);
    rightmenu_style.alignment = TextAnchor.MiddleRight;

    // row style
    row_style = new GUIStyle();
    row_style.stretchWidth = true;
    row_style.fixedHeight = 16.0f;

    // title style
    title_style = new GUIStyle(HighLogic.Skin.label);
    title_style.normal.background = Lib.GetTexture("black-background");
    title_style.normal.textColor = Color.white;
    title_style.stretchWidth = true;
    title_style.stretchHeight = false;
    title_style.fixedHeight = 16.0f;
    title_style.fontSize = 12;
    title_style.border = new RectOffset(0, 0, 0, 0);
    title_style.padding = new RectOffset(3, 4, 3, 4);
    title_style.alignment = TextAnchor.MiddleCenter;

    // content style
    content_style = new GUIStyle(HighLogic.Skin.label);
    content_style.richText = true;
    content_style.normal.textColor = Color.white;
    content_style.stretchWidth = true;
    content_style.stretchHeight = true;
    content_style.fontSize = 12;
    content_style.alignment = TextAnchor.MiddleLeft;

    // quote style
    quote_style = new GUIStyle(HighLogic.Skin.label);
    quote_style.richText = true;
    quote_style.normal.textColor = Color.white;
    quote_style.stretchWidth = true;
    quote_style.stretchHeight = true;
    quote_style.fontSize = 11;
    quote_style.alignment = TextAnchor.LowerCenter;
  }


  public static environment_data analyze_environment(CelestialBody body, double altitude_mult)
  {
    // shortcuts
    CelestialBody sun = Sim.Sun();

    // calculate data
    environment_data env = new environment_data();
    env.body = body;
    env.altitude = body.Radius * altitude_mult;
    env.landed = env.altitude <= double.Epsilon;
    env.breathable = env.landed && body.atmosphereContainsOxygen;
    env.sun_dist = Sim.Apoapsis(Lib.PlanetarySystem(body)) - sun.Radius - body.Radius;
    Vector3d sun_dir = (sun.position - body.position).normalized;
    env.sun_flux = Sim.SolarFlux(env.sun_dist);
    env.body_flux = Sim.BodyFlux(body, body.position + sun_dir * (body.Radius + env.altitude));
    env.body_back_flux = Sim.BodyFlux(body, body.position - sun_dir * (body.Radius + env.altitude));
    env.background_temp = Sim.BackgroundTemperature();
    env.sun_temp = Sim.BlackBody(env.sun_flux);
    env.body_temp = Sim.BlackBody(env.body_flux);
    env.body_back_temp = Sim.BlackBody(env.body_back_flux);
    env.light_temp = env.background_temp + env.sun_temp + env.body_temp;
    env.shadow_temp = env.background_temp + env.body_back_temp;
    env.atmo_temp = body.GetTemperature(0.0);
    env.orbital_period = Sim.OrbitalPeriod(body, env.altitude);
    env.shadow_period = Sim.ShadowPeriod(body, env.altitude);
    env.shadow_time = env.shadow_period / env.orbital_period;
    env.temp_diff = env.landed && body.atmosphere
      ? Math.Abs(Settings.SurvivalTemperature - env.atmo_temp)
      : Lib.Mix(Math.Abs(Settings.SurvivalTemperature - env.light_temp), Math.Abs(Settings.SurvivalTemperature - env.shadow_temp), env.shadow_time);
    env.atmo_factor = env.landed ? Sim.AtmosphereFactor(body, 0.7071) : 1.0;

    // return data
    return env;
  }


  public static crew_data analyze_crew(List<Part> parts)
  {
    // store data
    crew_data crew = new crew_data();

    // get number of kerbals assigned to the vessel in the editor
    // note: crew manifest is not reset after root part is deleted
    var cad = KSP.UI.CrewAssignmentDialog.Instance;
    if (cad != null && cad.GetManifest() != null)
    {
      List<ProtoCrewMember> manifest = cad.GetManifest().GetAllCrew(false);
      crew.count = (uint)manifest.Count;
      crew.engineer = manifest.Find(k => k.trait == "Engineer") != null;
    }

    // scan the parts
    foreach(Part p in parts)
    {
      // accumulate crew capacity
      crew.capacity += (uint)p.CrewCapacity;
    }

    // return data
    return crew;
  }


  public static ec_data analyze_ec(List<Part> parts, environment_data env, crew_data crew, food_data food, oxygen_data oxygen, signal_data signal)
  {
    // store data
    ec_data ec = new ec_data();

    // calculate climate cost
    ec.consumed = (double)crew.count * env.temp_diff * Settings.ElectricChargePerSecond;

    // scan the parts
    foreach(Part p in parts)
    {
      // accumulate EC storage
      ec.storage += Lib.GetResourceAmount(p, "ElectricCharge");

      // remember if we already considered a resource converter module
      // rationale: we assume only the first module in a converter is active
      bool first_converter = true;

      // for each module
      foreach(PartModule m in p.Modules)
      {
        // command
        if (m.moduleName == "ModuleCommand")
        {
          ModuleCommand mm = (ModuleCommand)m;
          foreach(ModuleResource res in mm.inputResources)
          {
            if (res.name == "ElectricCharge")
            {
              ec.consumed += res.rate;
            }
          }
        }
        // solar panel
        else if (m.moduleName == "ModuleDeployableSolarPanel")
        {
          ModuleDeployableSolarPanel mm = (ModuleDeployableSolarPanel)m;
          double solar_k = (mm.useCurve ? mm.powerCurve.Evaluate((float)env.sun_dist) : env.sun_flux / Sim.SolarFluxAtHome());
          double generated = mm.chargeRate * solar_k * env.atmo_factor;
          ec.generated_sunlight += generated;
          ec.best_ec_generator = Math.Max(ec.best_ec_generator, generated);
        }
        // generator
        else if (m.moduleName == "ModuleGenerator")
        {
          // skip launch clamps, that include a generator
          if (p.partInfo.name == "launchClamp1") continue;

          ModuleGenerator mm = (ModuleGenerator)m;
          foreach(ModuleResource res in mm.inputList)
          {
            if (res.name == "ElectricCharge")
            {
              ec.consumed += res.rate;
            }
          }
          foreach(ModuleResource res in mm.outputList)
          {
            if (res.name == "ElectricCharge")
            {
              ec.generated_shadow += res.rate;
              ec.generated_sunlight += res.rate;
              ec.best_ec_generator = Math.Max(ec.best_ec_generator, res.rate);
            }
          }
        }
        // converter
        // note: only electric charge is considered for resource converters
        // note: we only consider the first resource converter in a part, and ignore the rest
        else if (m.moduleName == "ModuleResourceConverter" && first_converter)
        {
          ModuleResourceConverter mm = (ModuleResourceConverter)m;
          foreach(ResourceRatio rr in mm.inputList)
          {
            if (rr.ResourceName == "ElectricCharge")
            {
              ec.consumed += rr.Ratio;
            }
          }
          foreach(ResourceRatio rr in mm.outputList)
          {
            if (rr.ResourceName == "ElectricCharge")
            {
              ec.generated_shadow += rr.Ratio;
              ec.generated_sunlight += rr.Ratio;
              ec.best_ec_generator = Math.Max(ec.best_ec_generator, rr.Ratio);
            }
          }
          first_converter = false;
        }
        // harvester
        // note: only electric charge is considered for resource harvesters
        else if (m.moduleName == "ModuleResourceHarvester")
        {
          ModuleResourceHarvester mm = (ModuleResourceHarvester)m;
          foreach(ResourceRatio rr in mm.inputList)
          {
            if (rr.ResourceName == "ElectricCharge")
            {
              ec.consumed += rr.Ratio;
            }
          }
        }
        // active radiators
        else if (m.moduleName == "ModuleActiveRadiator")
        {
          ModuleActiveRadiator mm = (ModuleActiveRadiator)m;
          if (mm.IsCooling)
          {
            foreach(var rr in mm.inputResources)
            {
              if (rr.name == "ElectricCharge")
              {
                ec.consumed += rr.rate;
              }
            }
          }
        }
        // wheels
        else if (m.moduleName == "ModuleWheelMotor")
        {
          ModuleWheelMotor mm = (ModuleWheelMotor)m;
          if (mm.motorEnabled && mm.inputResource.name == "ElectricCharge")
          {
            ec.consumed += mm.inputResource.rate;
          }
        }
        else if (m.moduleName == "ModuleWheelMotorSteering")
        {
          ModuleWheelMotorSteering mm = (ModuleWheelMotorSteering)m;
          if (mm.motorEnabled && mm.inputResource.name == "ElectricCharge")
          {
            ec.consumed += mm.inputResource.rate;
          }
        }
        // SCANsat support
        else if (m.moduleName == "SCANsat" || m.moduleName == "ModuleSCANresourceScanner")
        {
          // include it in ec consumption, if deployed
          if (SCANsat.isDeployed(p, m)) ec.consumed += Lib.ReflectionValue<float>(m, "power");
        }
        // NearFutureSolar support
        // note: assume half the components are in sunlight, and average inclination is half
        else if (m.moduleName == "ModuleCurvedSolarPanel")
        {
          // get total rate
          double tot_rate = Lib.ReflectionValue<float>(m, "TotalEnergyRate");

          // get number of components
          int components = p.FindModelTransforms(Lib.ReflectionValue<string>(m, "PanelTransformName")).Length;

          // approximate output
          // 0.7071: average clamped cosine
          ec.generated_sunlight += 0.7071 * tot_rate;
        }
      }
    }

    // include cost from greenhouses artificial lighting
    ec.consumed += food.greenhouse_cost;

    // include cost from scrubbers
    ec.consumed += oxygen.scrubber_cost;

    // include relay cost for the best relay antenna
    ec.consumed += signal.relay_cost;

    // finally, calculate life expectancy of ec
    ec.life_expectancy_sunlight = ec.storage / Math.Max(ec.consumed - ec.generated_sunlight, 0.0);
    ec.life_expectancy_shadow = ec.storage / Math.Max(ec.consumed - ec.generated_shadow, 0.0);

    // return data
    return ec;
  }


  public static food_data analyze_food(List<Part> parts, environment_data env, crew_data crew)
  {
    // store data
    food_data food = new food_data();

    // calculate food consumed
    food.consumed = (double)crew.count * Settings.FoodPerMeal / Settings.MealFrequency;

    // deduce waste produced by the crew per-second
    double simulated_waste = food.consumed;

    // scan the parts
    foreach(Part p in parts)
    {
      // accumulate food storage
      food.storage += Lib.GetResourceAmount(p, "Food");

      // for each module
      foreach(PartModule m in p.Modules)
      {
        // greenhouse
        if (m.moduleName == "Greenhouse")
        {
          Greenhouse mm = (Greenhouse)m;

          // calculate natural lighting
          double natural_lighting = Greenhouse.NaturalLighting(env.sun_dist);

          // calculate ec consumed
          food.greenhouse_cost += mm.ec_rate * mm.lamps;

          // calculate lighting
          double lighting = natural_lighting * (mm.door_opened ? 1.0 : 0.0) + mm.lamps * (mm.door_opened ? 1.0 : 1.0 + Settings.GreenhouseDoorBonus);

          // calculate waste used
          double waste_used = Math.Min(simulated_waste, mm.waste_rate);
          double waste_perc = waste_used / mm.waste_rate;
          simulated_waste -= waste_used;

          // calculate growth bonus
          double growth_bonus = 0.0;
          growth_bonus += Settings.GreenhouseSoilBonus * (env.landed ? 1.0 : 0.0);
          growth_bonus += Settings.GreenhouseWasteBonus * waste_perc;

          // calculate growth factor
          double growth_factor = (mm.growth_rate * (1.0 + growth_bonus)) * lighting;

          // calculate food cultivated
          food.cultivated += mm.harvest_size * growth_factor;

          // calculate time-to-harvest
          if (growth_factor > double.Epsilon)
          {
            food.cultivated_tooltip += (food.cultivated_tooltip.Length > 0 ? "\n" : "")
              + "Time-to-harvest: <b>" + Lib.HumanReadableDuration(1.0 / growth_factor) + "</b>";
          }
        }
      }
    }

    // calculate life expectancy
    food.life_expectancy = food.storage / Math.Max(food.consumed - food.cultivated, 0.0);

    // add formatting to tooltip
    if (food.cultivated_tooltip.Length > 0) food.cultivated_tooltip = "<i>" + food.cultivated_tooltip + "</i>";

    // return data
    return food;
  }


  public static oxygen_data analyze_oxygen(List<Part> parts, environment_data env, crew_data crew)
  {
    // store data
    oxygen_data oxygen = new oxygen_data();

    // get scrubber efficiency
    oxygen.scrubber_efficiency = Scrubber.DeduceEfficiency();

    // calculate oxygen consumed
    oxygen.consumed = !env.breathable ? (double)crew.count * Settings.OxygenPerSecond : 0.0;

    // deduce co2 produced by the crew per-second
    double simulated_co2 = oxygen.consumed;

    // scan the parts
    foreach(Part p in parts)
    {
      // accumulate food storage
      oxygen.storage += Lib.GetResourceAmount(p, "Oxygen");

      // for each module
      foreach(PartModule m in p.Modules)
      {
        // scrubber
        if (m.moduleName == "Scrubber")
        {
          Scrubber mm = (Scrubber)m;

          // do nothing inside breathable atmosphere
          if (mm.is_enabled && !env.breathable)
          {
            double co2_scrubbed = Math.Min(simulated_co2, mm.co2_rate);
            if (co2_scrubbed > double.Epsilon)
            {
              oxygen.scrubber_cost += mm.ec_rate * (co2_scrubbed / mm.co2_rate);
              oxygen.recycled += co2_scrubbed * oxygen.scrubber_efficiency;
              simulated_co2 -= co2_scrubbed;
            }
          }
        }
      }
    }

    // calculate life expectancy
    oxygen.life_expectancy = !env.breathable ? oxygen.storage / Math.Max(oxygen.consumed - oxygen.recycled, 0.0) : double.NaN;

    // return data
    return oxygen;
  }


  public static qol_data analyze_qol(List<Part> parts, environment_data env, crew_data crew, signal_data signal)
  {
    // store data
    qol_data qol = new qol_data();

    // scan the parts
    foreach(Part p in parts)
    {
      // for each module
      foreach(PartModule m in p.Modules)
      {
        // entertainment
        if (m.moduleName == "Entertainment")
        {
          Entertainment mm = (Entertainment)m;
          qol.entertainment *= mm.rate;
        }
      }
    }

    // calculate Quality-Of-Life bonus
    // note: ignore kerbal-specific variance
    if (crew.capacity > 0)
    {
      double bonus = QualityOfLife.Bonus(crew.count, crew.capacity, qol.entertainment, env.landed, signal.range > 0.0);
      qol.living_space = QualityOfLife.LivingSpace(crew.count, crew.capacity);
      qol.time_to_instability = bonus / Settings.StressedDegradationRate;
      List<string> factors = new List<string>();
      if (crew.count > 1) factors.Add("not-alone");
      if (signal.range > 0.0) factors.Add("call-home");
      if (env.landed) factors.Add("firm-ground");
      if (factors.Count == 0) factors.Add("none");
      qol.factors = String.Join(", ", factors.ToArray());
    }
    else
    {
      qol.living_space = 0.0;
      qol.time_to_instability = double.NaN;
      qol.factors = "none";
    }

    // return data
    return qol;
  }


  public static radiation_data analyze_radiation(List<Part> parts, environment_data env, crew_data crew)
  {
    // store data
    radiation_data radiation = new radiation_data();

    // scan the parts
    foreach(Part p in parts)
    {
      // accumulate shielding amount and capacity
      radiation.shielding_amount += Lib.GetResourceAmount(p, "Shielding");
      radiation.shielding_capacity += Lib.GetResourceCapacity(p, "Shielding");
    }

    // calculate radiation data
    double shielding = Radiation.Shielding(radiation.shielding_amount, radiation.shielding_capacity);
    double belt_strength = Settings.BeltRadiation * Radiation.Dynamo(env.body) * 0.5; //< account for the 'ramp'
    if (crew.capacity > 0)
    {
      radiation.life_expectancy = new double[]
      {
        Settings.RadiationFatalThreshold / (Settings.CosmicRadiation * (1.0 - shielding)),
        Settings.RadiationFatalThreshold / (Settings.StormRadiation * (1.0 - shielding)),
        Radiation.HasBelt(env.body) ? Settings.RadiationFatalThreshold / (belt_strength * (1.0 - shielding)) : double.NaN
      };
    }
    else
    {
      radiation.life_expectancy = new double[]{double.NaN, double.NaN, double.NaN};
    }

    // return data
    return radiation;
  }


  public static reliability_data analyze_reliability(List<Part> parts, ec_data ec, signal_data signal)
  {
    // store data
    reliability_data reliability = new reliability_data();

    // get manufacturing quality
    reliability.quality = Malfunction.DeduceQuality();

    // count parts that can fail
    uint components = 0;

    // scan the parts
    foreach(Part p in parts)
    {
      // for each module
      foreach(PartModule m in p.Modules)
      {
        // malfunctions
        if (m.moduleName == "Malfunction")
        {
          Malfunction mm = (Malfunction)m;
          ++components;
          double avg_lifetime = (mm.min_lifetime + mm.max_lifetime) * 0.5 * reliability.quality;
          reliability.failure_year += (60.0 * 60.0 * Lib.HoursInDay() * Lib.DaysInYear()) / avg_lifetime;
        }
      }
    }

    // calculate reliability data
    if (components > 0) reliability.failure_year /= (double)components;
    double ec_redundancy = ec.best_ec_generator < ec.generated_sunlight ? (ec.generated_sunlight - ec.best_ec_generator) / ec.generated_sunlight : 0.0;
    double antenna_redundancy = signal.second_best_range > 0.0 ? signal.second_best_range / signal.range : 0.0;
    List<string> redundancies = new List<string>();
    if (ec_redundancy >= 0.5) redundancies.Add("ec");
    if (antenna_redundancy >= 0.99) redundancies.Add("antenna");
    if (redundancies.Count == 0) redundancies.Add("none");
    reliability.redundancy = String.Join(", ", redundancies.ToArray());

    // return data
    return reliability;
  }


  public static signal_data analyze_signal(List<Part> parts)
  {
    // store data
    signal_data signal = new signal_data();

    // get error correcting code factor
    signal.ecc = Signal.ECC();

    // scan the parts
    foreach(Part p in parts)
    {
      // for each module
      foreach(PartModule m in p.Modules)
      {
        // antenna
        if (m.moduleName == "Antenna")
        {
          Antenna mm = (Antenna)m;

          // calculate actual range
          double range = Signal.Range(mm.scope, mm.penalty, signal.ecc);

          // maintain 2nd best antenna
          signal.second_best_range = range > signal.range ? signal.range : Math.Max(signal.second_best_range, range);

          // keep track of best antenna
          if (range > signal.range)
          {
            signal.range = range;
            signal.transmission_cost_min = mm.min_transmission_cost;
            signal.transmission_cost_max = mm.max_transmission_cost;
          }

          // keep track of best relay antenna
          if (mm.relay && range > signal.relay_range)
          {
            signal.relay_range = range;
            signal.relay_cost = mm.relay_cost;
          }
        }
      }
    }

    // return data
    return signal;
  }


  void render_title(string title)
  {
    GUILayout.BeginHorizontal();
    GUILayout.Label(title, title_style);
    GUILayout.EndHorizontal();
  }


  void render_content(string desc, string value, string tooltip="")
  {
    GUILayout.BeginHorizontal(row_style);
    GUILayout.Label(new GUIContent(desc + ": <b>" + value + "</b>", tooltip.Length > 0 ? "<i>" + tooltip + "</i>" : ""), content_style);
    GUILayout.EndHorizontal();
  }


  void render_space()
  {
    GUILayout.Space(10.0f);
  }


  void render_environment(environment_data env)
  {
    bool in_atmosphere = env.landed && env.body.atmosphere;
    string temperature_str = in_atmosphere
      ? Lib.HumanReadableTemp(env.atmo_temp)
      : Lib.HumanReadableTemp(env.light_temp) + "</b> / <b>"
      + Lib.HumanReadableTemp(env.shadow_temp);
    string temperature_tooltip = in_atmosphere
      ? "atmospheric"
      : "sunlight / shadow\n"
      + "solar: <b>" + Lib.HumanReadableTemp(env.sun_temp) + "</b>\n"
      + "albedo (sunlight): <b>" + Lib.HumanReadableTemp(env.body_temp) + "</b>\n"
      + "albedo (shadow): <b>" + Lib.HumanReadableTemp(env.body_back_temp) + "</b>\n"
      + "background: <b>" + Lib.HumanReadableTemp(env.background_temp) + "</b>";
    string atmosphere_tooltip = in_atmosphere
      ? "light absorption: <b>" + ((1.0 - env.atmo_factor) * 100.0).ToString("F0") + "%</b>\n"
      + "pressure: <b>" + env.body.atmospherePressureSeaLevel.ToString("F0") + " kPa</b>\n"
      + "breathable: <b>" + (env.body.atmosphereContainsOxygen ? "yes" : "no") + "</b>"
      : "";
    string shadowtime_str = Lib.HumanReadableDuration(env.shadow_period) + " (" + (env.shadow_time * 100.0).ToString("F0") + "%)";

    render_title("ENVIRONMENT");
    render_content("temperature", temperature_str, temperature_tooltip);
    render_content("temp diff", env.temp_diff.ToString("F0") + "K", "average difference between\nexternal and survival temperature");
    render_content("inside atmosphere", in_atmosphere ? "yes" : "no", atmosphere_tooltip);
    render_content("shadow time", shadowtime_str);
    render_space();
  }


  void render_ec(ec_data ec)
  {
    bool shadow_different = Math.Abs(ec.generated_sunlight - ec.generated_shadow) > double.Epsilon;
    string generated_str = Lib.HumanReadableRate(ec.generated_sunlight) + (shadow_different ? "</b> / <b>" + Lib.HumanReadableRate(ec.generated_shadow) : "");
    string life_str = Lib.HumanReadableDuration(ec.life_expectancy_sunlight) + (shadow_different ? "</b> / <b>" + Lib.HumanReadableDuration(ec.life_expectancy_shadow) : "");

    render_title("ELECTRIC CHARGE");
    render_content("storage", Lib.ValueOrNone(ec.storage));
    render_content("consumed", Lib.HumanReadableRate(ec.consumed));
    render_content("generated", generated_str, "sunlight / shadow");
    render_content("life expectancy", life_str, "sunlight / shadow");
    render_space();
  }


  void render_food(food_data food)
  {
    render_title("FOOD");
    render_content("storage", Lib.ValueOrNone(food.storage));
    render_content("consumed", Lib.HumanReadableRate(food.consumed));
    render_content("cultivated", Lib.HumanReadableRate(food.cultivated), food.cultivated_tooltip);
    render_content("life expectancy", Lib.HumanReadableDuration(food.life_expectancy));
    render_space();
  }


  void render_oxygen(oxygen_data oxygen)
  {
    string recycled_tooltip = "efficiency: " + (oxygen.scrubber_efficiency * 100.0).ToString("F0") + "%";

    render_title("OXYGEN");
    render_content("storage", Lib.ValueOrNone(oxygen.storage));
    render_content("consumed", Lib.HumanReadableRate(oxygen.consumed));
    render_content("recycled", Lib.HumanReadableRate(oxygen.recycled), recycled_tooltip);
    render_content("life expectancy", Lib.HumanReadableDuration(oxygen.life_expectancy));
    render_space();
  }


  void render_qol(qol_data qol)
  {
    render_title("QUALITY OF LIFE");
    render_content("living space", QualityOfLife.LivingSpaceToString(qol.living_space));
    render_content("entertainment", QualityOfLife.EntertainmentToString(qol.entertainment));
    render_content("other factors", qol.factors);
    render_content("time to instability", Lib.HumanReadableDuration(qol.time_to_instability));
    render_space();
  }


  void render_radiation(radiation_data radiation, environment_data env, crew_data crew)
  {
    string magnetosphere_str = Radiation.HasMagnetosphere(env.body) ? Lib.HumanReadableRange(Radiation.MagnAltitude(env.body)) : "none";
    string belt_strength_str = Radiation.HasBelt(env.body) ? " (" + (Radiation.Dynamo(env.body) * Settings.BeltRadiation * (60.0 * 60.0)).ToString("F0") + " rad/h)" : "";
    string belt_str = Radiation.HasBelt(env.body) ? Lib.HumanReadableRange(Radiation.BeltAltitude(env.body)) : "none";
    string shield_str = Radiation.ShieldingToString(radiation.shielding_amount, radiation.shielding_capacity);
    string shield_tooltip = radiation.shielding_capacity > 0 ? "average over the vessel" : "";
    string life_str = Lib.HumanReadableDuration(radiation.life_expectancy[0]) + "</b> / <b>" + Lib.HumanReadableDuration(radiation.life_expectancy[1]);
    string life_tooltip = "cosmic / storm";
    if (Radiation.HasBelt(env.body))
    {
      life_str += "</b> / <b>" + Lib.HumanReadableDuration(radiation.life_expectancy[2]);
      life_tooltip += " / belt";
    }

    render_title("RADIATION");
    render_content("magnetosphere", magnetosphere_str, "protect from cosmic radiation");
    render_content("radiation belt", belt_str, "abnormal radiation zone" + belt_strength_str);
    render_content("shielding", shield_str, shield_tooltip);
    render_content("life expectancy", crew.capacity > 0 ? life_str : "perpetual", crew.capacity > 0 ? life_tooltip : "");
    render_space();
  }


  void render_reliability(reliability_data reliability, crew_data crew)
  {
    render_title("RELIABILITY");
    render_content("malfunctions", Lib.ValueOrNone(reliability.failure_year, "/y"), "per-component average case estimate");
    render_content("redundancy", reliability.redundancy);
    render_content("quality", Malfunction.QualityToString(reliability.quality), "manufacturing quality");
    render_content("engineer", crew.engineer ? "yes" : "no");
    render_space();
  }


  void render_signal(signal_data signal, environment_data env, crew_data crew)
  {
    // approximate min/max distance between home and target body
    CelestialBody home = FlightGlobals.GetHomeBody();
    double home_dist_min = 0.0;
    double home_dist_max = 0.0;
    if (env.body == home)
    {
      home_dist_min = env.altitude;
      home_dist_max = env.altitude;
    }
    else if (env.body.referenceBody == home)
    {
      home_dist_min = Sim.Periapsis(env.body);
      home_dist_max = Sim.Apoapsis(env.body);
    }
    else
    {
      double home_p = Sim.Periapsis(Lib.PlanetarySystem(home));
      double home_a = Sim.Apoapsis(Lib.PlanetarySystem(home));
      double body_p = Sim.Periapsis(Lib.PlanetarySystem(env.body));
      double body_a = Sim.Apoapsis(Lib.PlanetarySystem(env.body));
      home_dist_min = Math.Min(Math.Abs(home_a - body_p), Math.Abs(home_p - body_a));
      home_dist_max = home_a + body_a;
    }

    // calculate if antenna is out of range from target body
    string range_tooltip = "";
    if (signal.range > double.Epsilon)
    {
      if (signal.range < home_dist_min) range_tooltip = "<color=#ff0000>out of range</color>";
      else if (signal.range < home_dist_max) range_tooltip = "<color=#ffff00>partially out of range</color>";
      else range_tooltip = "<color=#00ff00>in range</color>";
      if (home_dist_max > double.Epsilon) //< if not landed at home
      {
        range_tooltip += "\nbody distance (min): <b>" + Lib.HumanReadableRange(home_dist_min) + "</b>"
        + "\nbody distance (max): <b>" + Lib.HumanReadableRange(home_dist_max) + "</b>";
      }
    }
    else if (crew.capacity == 0) range_tooltip = "<color=#ff0000>no antenna on unmanned vessel</color>";

    // calculate transmission cost
    double cost = signal.range > double.Epsilon
      ? signal.transmission_cost_min + (signal.transmission_cost_max - signal.transmission_cost_min) * Math.Min(home_dist_max, signal.range) / signal.range
      : 0.0;
    string cost_str = signal.range > double.Epsilon ? cost.ToString("F1") + " EC/Mbit" : "none";

    // generate ecc table
    Func<double, double, double, string> deduce_color = (double range, double dist_min, double dist_max) =>
    {
      if (range < dist_min) return "<color=#ff0000>";
      else if (range < dist_max) return "<color=#ffff00>";
      else return "<color=#ffffff>";
    };
    double signal_100 = signal.range / signal.ecc;
    double signal_15 = signal_100 * 0.15;
    double signal_33 = signal_100 * 0.33;
    double signal_66 = signal_100 * 0.66;
    string ecc_tooltip = signal.range > double.Epsilon
      ? "<align=left /><b>ecc</b>\t<b>range</b>"
      + "\n15%\t"  + deduce_color(signal_15, home_dist_min, home_dist_max) + Lib.HumanReadableRange(signal_15) + "</color>"
      + "\n33%\t"  + deduce_color(signal_33, home_dist_min, home_dist_max) + Lib.HumanReadableRange(signal_33) + "</color>"
      + "\n66%\t"  + deduce_color(signal_66, home_dist_min, home_dist_max) + Lib.HumanReadableRange(signal_66) + "</color>"
      + "\n100%\t" + deduce_color(signal_100,home_dist_min, home_dist_max) + Lib.HumanReadableRange(signal_100) + "</color>"
      : "";


    render_title("SIGNAL");
    render_content("range", Lib.HumanReadableRange(signal.range), range_tooltip);
    render_content("relay", signal.relay_range <= double.Epsilon ? "none" : signal.relay_range < signal.range ? Lib.HumanReadableRange(signal.relay_range) : "yes");
    render_content("transmission", cost_str, "worst case data transmission cost\nfrom destination body");
    render_content("error correction", (signal.ecc * 100.0).ToString("F0") + "%", ecc_tooltip);
    render_space();
  }


  public float width()
  {
    return 300.0f;
  }


  public float height()
  {
    return 425.0f;
  }


  public void render()
  {
    // if there is something in the editor
    if (EditorLogic.RootPart != null)
    {
      // store situations and altitude multipliers
      string[] situations = {"Landed", "Low Orbit", "Orbit", "High Orbit"};
      double[] altitude_mults = {0.0, 0.33, 1.0, 3.0};

      // get body, situation and altitude multiplier
      CelestialBody body = FlightGlobals.Bodies[body_index];
      string situation = situations[situation_index];
      double altitude_mult = altitude_mults[situation_index];

      // get parts recursively
      List<Part> parts = Lib.GetPartsRecursively(EditorLogic.RootPart);

      // analyze
      environment_data env = analyze_environment(body, altitude_mult);
      crew_data crew = analyze_crew(parts);
      food_data food = analyze_food(parts, env, crew);
      oxygen_data oxygen = analyze_oxygen(parts, env, crew);
      signal_data signal = analyze_signal(parts);
      qol_data qol = analyze_qol(parts, env, crew, signal);
      radiation_data radiation = analyze_radiation(parts, env, crew);
      ec_data ec = analyze_ec(parts, env, crew, food, oxygen, signal);
      reliability_data reliability = analyze_reliability(parts, ec, signal);

      // render menu
      GUILayout.BeginHorizontal(row_style);
      if (GUILayout.Button(body.name, leftmenu_style)) { body_index = (body_index + 1) % FlightGlobals.Bodies.Count; if (body_index == 0) ++body_index; }
      if (GUILayout.Button("["+ (page + 1) + "/2]", midmenu_style)) { page = (page + 1) % 2; }
      if (GUILayout.Button(situation, rightmenu_style)) { situation_index = (situation_index + 1) % situations.Length; }
      GUILayout.EndHorizontal();

      // page 1/2
      if (page == 0)
      {
        // render
        render_ec(ec);
        render_food(food);
        render_oxygen(oxygen);
        render_qol(qol);
      }
      // page 2/2
      else
      {
        // render
        render_radiation(radiation, env, crew);
        render_reliability(reliability, crew);
        render_signal(signal, env, crew);
        render_environment(env);
      }
    }
    // if there is nothing in the editor
    else
    {
      // render quote
      GUILayout.FlexibleSpace();
      GUILayout.BeginHorizontal();
      GUILayout.Label("<i>In preparing for space, I have always found that\nplans are useless but planning is indispensable.\nWernher von Kerman</i>", quote_style);
      GUILayout.EndHorizontal();
      GUILayout.Space(10.0f);
    }
  }
}


} // KERBALISM