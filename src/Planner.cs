// ====================================================================================================================
// the vessel planner
// ====================================================================================================================


using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using ModuleWheels;
using UnityEngine;


namespace KERBALISM {


public sealed class Planner
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


  public class qol_data
  {
    public double living_space;                           // living space per-crew
    public double entertainment = 1.0;                    // store multiplication of all entertainment from parts
    public string factors;                                // description of other quality-of-life factors
    public double bonus;                                  // the final quality-of-life factor
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

  // automatic page layout of panels
  bool layout_detected;
  uint panels_count;
  uint panels_per_page;
  uint pages_count;


  // ctor
  public Planner()
  {
    // set default body index & situation
    body_index = FlightGlobals.GetHomeBodyIndex();
    situation_index = 1;

    // left menu style
    leftmenu_style = new GUIStyle(HighLogic.Skin.label);
    leftmenu_style.richText = true;
    leftmenu_style.normal.textColor = new Color(0.0f, 0.0f, 0.0f, 1.0f);
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
      ? Sim.TempDiff(env.atmo_temp)
      : Lib.Mix(Sim.TempDiff(env.light_temp), Sim.TempDiff(env.shadow_temp), env.shadow_time);
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


  public static ec_data analyze_ec(List<Part> parts, environment_data env, crew_data crew, Rule rule_temp, resource_simulator sim)
  {
    // store data
    ec_data ec = new ec_data();

    // calculate climate cost
    ec.consumed = rule_temp != null ? (double)crew.count * env.temp_diff * rule_temp.rate : 0.0;

    // scan the parts
    foreach(Part p in parts)
    {
      // accumulate EC storage
      ec.storage += Lib.Resource.Amount(p, "ElectricCharge");

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
        // note: support PlanetaryBaseSystem converters
        else if ((m.moduleName == "ModuleResourceConverter" || m.moduleName == "ModuleKPBSConverter") && first_converter)
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
        // NearFutureElectrical support
        else if (m.moduleName == "FissionGenerator")
        {
          double max_rate = Lib.ReflectionValue<float>(m, "PowerGeneration");

          // get fission reactor tweakable, will default to 1.0 for other modules
          var reactor = p.FindModuleImplementing<ModuleResourceConverter>();
          double tweakable = reactor == null ? 1.0 : Lib.ReflectionValue<float>(reactor, "CurrentPowerPercent") * 0.01f;

          ec.generated_sunlight += max_rate * tweakable;
          ec.generated_shadow += max_rate * tweakable;
        }
        else if (m.moduleName == "ModuleRadioisotopeGenerator")
        {
          double max_rate = Lib.ReflectionValue<float>(m, "BasePower");

          ec.generated_sunlight += max_rate;
          ec.generated_shadow += max_rate;
        }
        else if (m.moduleName == "ModuleCryoTank")
        {
          // note: assume cooling is active
          double cooling_cost = Lib.ReflectionValue<float>(m, "CoolingCost");
          string fuel_name = Lib.ReflectionValue<string>(m, "FuelName");

          ec.consumed += cooling_cost * Lib.Resource.Amount(p, fuel_name) * 0.001;
        }
      }
    }

    // consider EC consumed from the resource simulator
    ec.consumed += sim.get_resource("ElectricCharge").consumed;

    // finally, calculate life expectancy of ec
    ec.life_expectancy_sunlight = ec.storage / Math.Max(ec.consumed - ec.generated_sunlight, 0.0);
    ec.life_expectancy_shadow = ec.storage / Math.Max(ec.consumed - ec.generated_shadow, 0.0);

    // return data
    return ec;
  }


  public static qol_data analyze_qol(List<Part> parts, environment_data env, crew_data crew, signal_data signal, Rule rule_qol)
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
        else if (m.moduleName == "GravityRing")
        {
          GravityRing mm = (GravityRing)m;
          qol.entertainment *= 1.0 + (mm.entertainment_rate - 1.0) * mm.speed;
        }
      }
    }
    qol.entertainment = Math.Min(qol.entertainment, QualityOfLife.MaxEntertainmnent);

    // calculate Quality-Of-Life bonus
    // note: ignore kerbal-specific variance
    if (crew.capacity > 0)
    {
      bool linked = signal.range > 0.0 || !Kerbalism.features.signal;

      qol.living_space = QualityOfLife.LivingSpace(crew.count, crew.capacity);
      qol.bonus = QualityOfLife.Bonus(qol.living_space, qol.entertainment, env.landed, linked, crew.count == 1);

      qol.time_to_instability = qol.bonus / rule_qol.degeneration;
      List<string> factors = new List<string>();
      if (crew.count > 1) factors.Add("not-alone");
      if (linked) factors.Add("call-home");
      if (env.landed) factors.Add("firm-ground");
      if (factors.Count == 0) factors.Add("none");
      qol.factors = String.Join(", ", factors.ToArray());
    }
    else
    {
      qol.living_space = 0.0;
      qol.bonus = 1.0;
      qol.time_to_instability = double.NaN;
      qol.factors = "none";
    }

    // return data
    return qol;
  }


  public static radiation_data analyze_radiation(List<Part> parts, environment_data env, crew_data crew, Rule rule_radiation)
  {
    // store data
    radiation_data radiation = new radiation_data();

    // scan the parts
    foreach(Part p in parts)
    {
      // accumulate shielding amount and capacity
      radiation.shielding_amount += Lib.Resource.Amount(p, "Shielding");
      radiation.shielding_capacity += Lib.Resource.Capacity(p, "Shielding");
    }

    // calculate radiation data
    double shielding = Radiation.Shielding(radiation.shielding_amount, radiation.shielding_capacity);
    double belt_strength = Settings.BeltRadiation * Radiation.Dynamo(env.body) * 0.5; //< account for the 'ramp'
    if (crew.capacity > 0)
    {
      radiation.life_expectancy = new double[]
      {
        rule_radiation.fatal_threshold / (Settings.CosmicRadiation * (1.0 - shielding)),
        rule_radiation.fatal_threshold / (Settings.StormRadiation * (1.0 - shielding)),
        Radiation.HasBelt(env.body) ? rule_radiation.fatal_threshold / (belt_strength * (1.0 - shielding)) : double.NaN
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
    double year_time = 60.0 * 60.0 * Lib.HoursInDay() * Lib.DaysInYear();
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
          reliability.failure_year += year_time / avg_lifetime;
        }
      }
    }

    // calculate reliability data
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
    GUILayout.Label(new GUIContent(Lib.BuildString(desc, ": <b>", value, "</b>"), tooltip.Length > 0 ? Lib.BuildString("<i>", tooltip, "</i>") : ""), content_style);
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
      : Lib.BuildString
      (
        "sunlight / shadow\n",
        "solar: <b>", Lib.HumanReadableTemp(env.sun_temp), "</b>\n",
        "albedo (sunlight): <b>", Lib.HumanReadableTemp(env.body_temp), "</b>\n",
        "albedo (shadow): <b>", Lib.HumanReadableTemp(env.body_back_temp), "</b>\n",
        "background: <b>", Lib.HumanReadableTemp(env.background_temp), "</b>"
      );
    string atmosphere_tooltip = in_atmosphere
      ? Lib.BuildString
      (
        "light absorption: <b>", Lib.HumanReadablePerc(1.0 - env.atmo_factor), "</b>\n",
        "pressure: <b>", env.body.atmospherePressureSeaLevel.ToString("F0"), " kPa</b>\n",
        "breathable: <b>", (env.body.atmosphereContainsOxygen ? "yes" : "no"), "</b>"
      )
      : "";
    string shadowtime_str = Lib.HumanReadableDuration(env.shadow_period) + " (" + (env.shadow_time * 100.0).ToString("F0") + "%)";

    render_title("ENVIRONMENT");
    render_content("temperature", temperature_str, temperature_tooltip);
    render_content("temp diff", Lib.HumanReadableTemp(env.temp_diff), "average difference between\nexternal and survival temperature");
    render_content("inside atmosphere", in_atmosphere ? "yes" : "no", atmosphere_tooltip);
    render_content("shadow time", shadowtime_str, "the time in shadow\nduring the orbit");
    render_space();
  }


  void render_ec(ec_data ec)
  {
    bool shadow_different = Math.Abs(ec.generated_sunlight - ec.generated_shadow) > double.Epsilon;
    string generated_str = Lib.BuildString(Lib.HumanReadableRate(ec.generated_sunlight), (shadow_different ? Lib.BuildString("</b> / <b>", Lib.HumanReadableRate(ec.generated_shadow)) : ""));
    string life_str = Lib.BuildString(Lib.HumanReadableDuration(ec.life_expectancy_sunlight), (shadow_different ? Lib.BuildString("</b> / <b>", Lib.HumanReadableDuration(ec.life_expectancy_shadow)) : ""));

    render_title("ELECTRIC CHARGE");
    render_content("storage", Lib.ValueOrNone(ec.storage));
    render_content("consumed", Lib.HumanReadableRate(ec.consumed));
    render_content("generated", generated_str, "sunlight / shadow");
    render_content("life expectancy", life_str, "sunlight / shadow");
    render_space();
  }


  void render_resource(Rule r, resource_simulator sim)
  {
    var res = sim.get_resource(r.resource_name);
    render_title(r.resource_name.ToUpper());
    render_content("storage", Lib.ValueOrNone(res.storage));
    render_content("consumed", Lib.HumanReadableRate(res.consumed));
    if (res.has_greenhouse) render_content("harvest", res.time_to_harvest <= double.Epsilon ? "none" : Lib.BuildString(res.harvest_size.ToString("F0"), " in ", Lib.HumanReadableDuration(res.time_to_harvest)));
    else if (res.has_recycler) render_content("recycled", Lib.HumanReadableRate(res.produced));
    else render_content("produced", Lib.HumanReadableRate(res.produced));
    render_content(r.breakdown ? "time to instability" : "life expectancy", Lib.HumanReadableDuration(res.lifetime()));
    render_space();
  }


  void render_qol(qol_data qol, Rule rule)
  {
    render_title("QUALITY OF LIFE");
    render_content("living space", QualityOfLife.LivingSpaceToString(qol.living_space));
    render_content("entertainment", QualityOfLife.EntertainmentToString(qol.entertainment));
    render_content("other factors", qol.factors);
    render_content(rule.breakdown ? "time to instability" : "life expectancy", Lib.HumanReadableDuration(qol.time_to_instability));
    render_space();
  }


  void render_radiation(radiation_data radiation, environment_data env, crew_data crew)
  {
    string magnetosphere_str = Radiation.HasMagnetosphere(env.body) ? Lib.HumanReadableRange(Radiation.MagnAltitude(env.body)) : "none";
    string belt_strength_str = Radiation.HasBelt(env.body) ? Lib.BuildString(" (", (Radiation.Dynamo(env.body) * Settings.BeltRadiation * (60.0 * 60.0)).ToString("F0"), " rad/h)") : "";
    string belt_str = Radiation.HasBelt(env.body) ? Lib.HumanReadableRange(Radiation.BeltAltitude(env.body)) : "none";
    string shield_str = Radiation.ShieldingToString(radiation.shielding_amount, radiation.shielding_capacity);
    string shield_tooltip = radiation.shielding_capacity > 0 ? "average over the vessel" : "";
    string life_str = Lib.BuildString(Lib.HumanReadableDuration(radiation.life_expectancy[0]), "</b> / <b>", Lib.HumanReadableDuration(radiation.life_expectancy[1]));
    string life_tooltip = "cosmic / storm";
    if (Radiation.HasBelt(env.body))
    {
      life_str += Lib.BuildString("</b> / <b>", Lib.HumanReadableDuration(radiation.life_expectancy[2]));
      life_tooltip += " / belt";
    }

    render_title("RADIATION");
    render_content("magnetosphere", magnetosphere_str, "protect from cosmic radiation");
    render_content("radiation belt", belt_str, Lib.BuildString("abnormal radiation zone", belt_strength_str));
    render_content("shielding", shield_str, shield_tooltip);
    render_content("life expectancy", crew.capacity > 0 ? life_str : "perpetual", crew.capacity > 0 ? life_tooltip : "");
    render_space();
  }


  void render_reliability(reliability_data reliability, crew_data crew)
  {
    render_title("RELIABILITY");
    render_content("malfunctions", Lib.ValueOrNone(reliability.failure_year, "/y"), "average case estimate\nfor the whole vessel");
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
        range_tooltip += Lib.BuildString
        (
          "\nbody distance (min): <b>", Lib.HumanReadableRange(home_dist_min), "</b>",
          "\nbody distance (max): <b>", Lib.HumanReadableRange(home_dist_max), "</b>"
        );
      }
    }
    else if (crew.capacity == 0) range_tooltip = "<color=#ff0000>no antenna on unmanned vessel</color>";

    // calculate transmission cost
    double cost = signal.range > double.Epsilon
      ? signal.transmission_cost_min + (signal.transmission_cost_max - signal.transmission_cost_min) * Math.Min(home_dist_max, signal.range) / signal.range
      : 0.0;
    string cost_str = signal.range > double.Epsilon ? Lib.BuildString(cost.ToString("F1"), " EC/Mbit") : "none";

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
      ? Lib.BuildString
      (
        "<align=left /><b>ecc</b>\t<b>range</b>",
        "\n15%\t", deduce_color(signal_15, home_dist_min, home_dist_max), Lib.HumanReadableRange(signal_15), "</color>",
        "\n33%\t", deduce_color(signal_33, home_dist_min, home_dist_max), Lib.HumanReadableRange(signal_33), "</color>",
        "\n66%\t", deduce_color(signal_66, home_dist_min, home_dist_max), Lib.HumanReadableRange(signal_66), "</color>",
        "\n100%\t", deduce_color(signal_100,home_dist_min, home_dist_max), Lib.HumanReadableRange(signal_100), "</color>"
      )
      : "";


    render_title("SIGNAL");
    render_content("range", Lib.HumanReadableRange(signal.range), range_tooltip);
    render_content("relay", signal.relay_range <= double.Epsilon ? "none" : signal.relay_range < signal.range ? Lib.HumanReadableRange(signal.relay_range) : "yes");
    render_content("transmission", cost_str, "worst case data transmission cost");
    render_content("error correction", Lib.HumanReadablePerc(signal.ecc), ecc_tooltip);
    render_space();
  }


  public float width()
  {
    return 300.0f;
  }


  public float height()
  {
    // detect page layout once
    detect_layout();

    return 26.0f + 100.0f * (float)panels_per_page;
  }


  public void render()
  {
    // detect page layout once
    detect_layout();

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

      // analyze stuff
      environment_data env = analyze_environment(body, altitude_mult);
      crew_data crew = analyze_crew(parts);
      signal_data signal = analyze_signal(parts);
      qol_data qol = Kerbalism.qol_rule != null ? analyze_qol(parts, env, crew, signal, Kerbalism.qol_rule) : null;
      resource_simulator sim = new resource_simulator(parts, Kerbalism.supply_rules, env, qol, crew);
      ec_data ec = analyze_ec(parts, env, crew, Kerbalism.temp_rule, sim);

      // render menu
      GUILayout.BeginHorizontal(row_style);
      GUILayout.Label(body.name, leftmenu_style);
      if (Lib.IsClicked()) { body_index = (body_index + 1) % FlightGlobals.Bodies.Count; if (body_index == 0) ++body_index; }
      else if (Lib.IsClicked(1)) { body_index = (body_index - 1) % FlightGlobals.Bodies.Count; if (body_index == 0) body_index = FlightGlobals.Bodies.Count - 1; }
      GUILayout.Label(Lib.BuildString("[", (page + 1).ToString(), "/", pages_count.ToString(), "]"), midmenu_style);
      if (Lib.IsClicked()) { page = (page + 1) % pages_count; }
      else if (Lib.IsClicked(1)) { page = (page == 0 ? pages_count : page) - 1u; }
      GUILayout.Label(situation, rightmenu_style);
      if (Lib.IsClicked()) { situation_index = (situation_index + 1) % situations.Length; }
      else if (Lib.IsClicked(1)) { situation_index = (situation_index == 0 ? situations.Length : situation_index) - 1; }
      GUILayout.EndHorizontal();

      uint panel_index = 0;

      // ec
      if (panel_index / panels_per_page == page)
      {
        render_ec(ec);
      }
      ++panel_index;

      // supplies
      foreach(Rule r in Kerbalism.supply_rules.FindAll(k => k.degeneration > 0.0))
      {
        if (panel_index / panels_per_page == page)
        {
          render_resource(r, sim);
        }
        ++panel_index;
      }

      // qol
      if (Kerbalism.qol_rule != null)
      {
        if (panel_index / panels_per_page == page)
        {
          //qol_data qol = analyze_qol(parts, env, crew, signal, Kerbalism.qol_rule);
          render_qol(qol, Kerbalism.qol_rule);
        }
        ++panel_index;
      }

      // radiation
      if (Kerbalism.rad_rule != null)
      {
        if (panel_index / panels_per_page == page)
        {
          radiation_data radiation = analyze_radiation(parts, env, crew, Kerbalism.rad_rule);
          render_radiation(radiation, env, crew);
        }
        ++panel_index;
      }

      // reliability
      if (Kerbalism.features.malfunction)
      {
        if (panel_index / panels_per_page == page)
        {
          reliability_data reliability = analyze_reliability(parts, ec, signal);
          render_reliability(reliability, crew);
        }
        ++panel_index;
      }

      // signal
      if (Kerbalism.features.signal)
      {
        if (panel_index / panels_per_page == page)
        {
          render_signal(signal, env, crew);
        }
        ++panel_index;
      }

      // environment
      if (panel_index / panels_per_page == page)
      {
        render_environment(env);
      }
      ++panel_index;
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


  void detect_layout()
  {
    if (layout_detected) return;
    layout_detected = true;

    // guess number of panels
    panels_count = 2u
                 + (uint)Kerbalism.supply_rules.FindAll(k => k.degeneration > 0.0).Count
                 + (Kerbalism.qol_rule != null ? 1u : 0)
                 + (Kerbalism.rad_rule != null ? 1u : 0)
                 + (Kerbalism.features.malfunction ? 1u : 0)
                 + (Kerbalism.features.signal ? 1u : 0);

    // calculate number of panels per page and number of pages
    switch(panels_count)
    {
      case 2u: panels_per_page = 2u; break;
      case 3u: panels_per_page = 3u; break;
      case 4u: panels_per_page = 4u; break;
      case 5u: panels_per_page = 3u; break;
      case 6u: panels_per_page = 3u; break;
      case 7u: panels_per_page = 4u; break;
      case 8u: panels_per_page = 4u; break;
      case 9u: panels_per_page = 3u; break;
      default: panels_per_page = 4u; break;
    }

    // calculate number of pages
    pages_count = (panels_count - 1u) / panels_per_page + 1u;
  }


  // simulate resource consumption & production
  public class resource_simulator
  {
    public resource_simulator(List<Part> parts, List<Rule> rules, environment_data env, qol_data qol, crew_data crew)
    {
      foreach(Part p in parts)
      {
        foreach(PartResource res in p.Resources.list)
        {
          process_part(p, res.resourceName);
        }
      }

      foreach(Rule r in Kerbalism.supply_rules)
      {
        process_rule(r, env, qol, crew);
      }

      foreach(Part p in parts)
      {
        foreach(PartModule m in p.Modules)
        {
          switch(m.moduleName)
          {
            case "Scrubber":    process_scrubber(m as Scrubber, env);     break;
            case "Recycler":    process_recycler(m as Recycler);          break;
            case "Greenhouse":  process_greenhouse(m as Greenhouse, env); break;
            case "GravityRing": process_ring(m as GravityRing);           break;
            case "Antenna":     process_antenna(m as Antenna);            break;
          }
        }
      }
    }

    public resource_data get_resource(string name)
    {
      resource_data res;
      if (!resources.TryGetValue(name, out res))
      {
        res = new resource_data();
        resources.Add(name, res);
      }
      return res;
    }

    void process_part(Part p, string res_name)
    {
      resource_data res = get_resource(res_name);
      res.storage += Lib.Resource.Amount(p, res_name);
      res.amount += Lib.Resource.Amount(p, res_name);
      res.capacity += Lib.Resource.Capacity(p, res_name);
    }

    void process_rule(Rule r, environment_data env, qol_data qol, crew_data crew)
    {
      double rate = (double)crew.count * (r.interval > 0.0 ? r.rate / r.interval : r.rate);

      double k = 1.0;
      foreach(string modifier in r.modifier)
      {
        switch (modifier)
        {
          case "breathable":  k *= env.breathable ? 0.0 : 1.0;          break;
          case "temperature": k *= env.temp_diff;                       break;
          case "qol":         k *= qol != null ? 1.0 / qol.bonus : 1.0; break;
        }
      }

      k *= get_resource(r.resource_name).consume(rate * k);
      if (r.waste_name.Length > 0) get_resource(r.waste_name).produce(rate * r.waste_ratio * k);
    }

    void process_scrubber(Scrubber scrubber, environment_data env)
    {
      resource_data res = get_resource(scrubber.resource_name);
      if (env.breathable)
      {
        res.produce(scrubber.intake_rate);
      }
      else if (scrubber.is_enabled)
      {
        double k = get_resource(scrubber.waste_name).consume(scrubber.co2_rate);
        k *= get_resource("ElectricCharge").consume(scrubber.ec_rate * k);
        res.produce(scrubber.co2_rate * k * Scrubber.DeduceEfficiency());
      }
      res.has_recycler = true;
    }

    void process_recycler(Recycler recycler)
    {
      resource_data res = get_resource(recycler.resource_name);
      if (recycler.is_enabled)
      {
        double k = get_resource(recycler.waste_name).consume(recycler.waste_rate);
        k *= get_resource("ElectricCharge").consume(recycler.ec_rate * k);
        k *= recycler.filter_name.Length > 0 ? get_resource(recycler.filter_name).consume(recycler.filter_rate * k) : 1.0;
        res.produce(recycler.waste_rate * recycler.waste_ratio * k);
        if (recycler.filter_name.Length > 0) res.depends.Add(get_resource(recycler.filter_name));
      }
      res.has_recycler = true;
    }

    void process_greenhouse(Greenhouse greenhouse, environment_data env)
    {
      // consume ec
      double ec_k = get_resource("ElectricCharge").consume(greenhouse.ec_rate * greenhouse.lamps);

      // calculate natural lighting
      double natural_lighting = Greenhouse.NaturalLighting(env.sun_dist);

      // calculate lighting
      double lighting = natural_lighting * (greenhouse.door_opened ? 1.0 : 0.0) + greenhouse.lamps * ec_k;

      // consume waste
      double waste_k = lighting > double.Epsilon ? get_resource(greenhouse.waste_name).consume(greenhouse.waste_rate) : 0.0;

      // calculate growth bonus
      double growth_bonus = 0.0;
      growth_bonus += greenhouse.soil_bonus * (env.landed ? 1.0 : 0.0);
      growth_bonus += greenhouse.waste_bonus * waste_k;

      // calculate growth factor
      double growth_factor = (greenhouse.growth_rate * (1.0 + growth_bonus)) * lighting;

      // consume input resource
      double input_k = greenhouse.input_name.Length > 0 ? get_resource(greenhouse.input_name).consume(greenhouse.input_rate * growth_factor) : 1.0;

      // produce food
      resource_data res = get_resource(greenhouse.resource_name);
      res.produce(Math.Min(greenhouse.harvest_size, res.capacity) * growth_factor * input_k);

      // calculate time to harvest
      double tta = growth_factor > double.Epsilon ? 1.0 / growth_factor : 0.0;
      if (res.time_to_harvest <= double.Epsilon || (tta > double.Epsilon && tta < res.time_to_harvest))
      {
        res.time_to_harvest = tta;
        res.harvest_size = Math.Min(greenhouse.harvest_size, res.capacity);
      }
      res.has_greenhouse = true;
    }

    void process_ring(GravityRing ring)
    {
      if (ring.opened) get_resource("ElectricCharge").consume(ring.ec_rate * ring.speed);
    }

    void process_antenna(Antenna antenna)
    {
      if (antenna.relay) get_resource("ElectricCharge").consume(antenna.relay_cost);
    }

    public class resource_data
    {
      public double consume(double amount)
      {
        double correct_amount = Math.Min(amount, this.amount);
        this.amount -= correct_amount;
        this.consumed += correct_amount;
        return amount > 0.0 ? correct_amount / amount : 0.0;
      }

      public double produce(double amount)
      {
        double correct_amount = Math.Min(amount, this.capacity - this.amount);
        this.amount += correct_amount;
        this.produced += correct_amount;
        return amount > 0.0 ? correct_amount / amount : 0.0;
      }

      public double lifetime()
      {
        double rate = produced - consumed;
        double time = amount <= double.Epsilon ? 0.0 : rate > -1e-10 ? double.NaN : amount / -rate;
        foreach(resource_data dep in depends)
        {
          double dep_time = dep.lifetime();
          if (!double.IsNaN(time) && !double.IsNaN(dep_time)) time = Math.Min(time, dep_time);
          else if (double.IsNaN(time)) time = dep_time;
        }
        return time;
      }

      public double storage;
      public double capacity;
      public double amount;
      public double consumed;
      public double produced;
      public bool   has_recycler;
      public bool   has_greenhouse;
      public double time_to_harvest;
      public double harvest_size;
      public List<resource_data> depends = new List<resource_data>();
    }

    Dictionary<string, resource_data> resources = new Dictionary<string, resource_data>();
  }
}


} // KERBALISM









// Barrin code
/*
  // === NEW CODE ===
  // store data about a resource
  public class resource_data
  {
    public double amount;                                  // initial resource supply
    public double capacity;                                // maximum amount of resources that can be stored
    public double draw;                                    // resources drained per second by rules and Kerbalism modules, when available
    public double consumed;                                // rate at which resources are being consumed, per second
    public double produced;                                // rate at which resources are being produced, per second
    public bool hasScrubber;
    public bool hasRecycler;
    public bool hasGreenhouse;
    public double harvestTime;
    public double harvestSize;
    public List<string> supplyRequires = new List<string>();
  }

  // resource_data dictionary wrapper
  public class resource_collection
  {
    public Dictionary<string, resource_data> resources = new Dictionary<string, resource_data>();

    public resource_data this[string name] { get { return resources[name]; } }

    // Add provided resource data to named resource
    public void Add(string name, resource_data r_data)
    {
      if (!resources.ContainsKey(name)) resources.Add(name, new resource_data());
      resource_data res = resources[name];
      res.amount += r_data.amount;
      res.capacity += r_data.capacity;
      res.draw += r_data.draw;
      res.consumed += r_data.consumed;
      res.produced += r_data.produced;
      res.hasScrubber |= r_data.hasScrubber;
      res.hasRecycler |= r_data.hasRecycler;
      res.hasGreenhouse |= r_data.hasGreenhouse;
      res.harvestTime = Math.Max(res.harvestTime, r_data.harvestTime);
      res.harvestSize = Math.Max(res.harvestSize, r_data.harvestSize);
      res.supplyRequires = res.supplyRequires.Union(r_data.supplyRequires).ToList();
    }

    //Add new empty resource by name if resource is not currently in dictionary
    public void Add(string name)
    {
      if(!resources.ContainsKey(name)) resources.Add(name, new resource_data());
    }

    public void AddDraw(string name, double draw)
    {
      if (!resources.ContainsKey(name)) resources.Add(name, new resource_data());
      resources[name].draw += draw;
    }

    public void AddConsumption(string name, double consumption)
    {
      if (!resources.ContainsKey(name)) resources.Add(name, new resource_data());
      resources[name].consumed += consumption;
    }

    public void AddProduction(string name, double production)
    {
      if (!resources.ContainsKey(name)) resources.Add(name, new resource_data());
      resources[name].produced += production;
    }

    public void ApplyConsumption(resource_collection consumption)
    {
      foreach(var pair in consumption.resources)
      {
        if (resources.ContainsKey(pair.Key))
        {
          resources[pair.Key].consumed += pair.Value.consumed;
          resources[pair.Key].draw -= pair.Value.draw;
        }
      }
    }

    public void ApplyProduction(resource_collection production)
    {
      foreach(var pair in production.resources)
      {
        if (resources.ContainsKey(pair.Key)) resources[pair.Key].produced += pair.Value.produced;
      }
    }

    public double Availability(string name)
    {
      if (!resources.ContainsKey(name)) return 0.0;     //Resource not in dictionary
      if (resources[name].amount > 0.0) return 1.0;     //Resource has supply available, regardless of resupply rate.
      if (resources[name].draw <= 0.0) return 0.0;
      return Math.Max(Math.Min((resources[name].produced - resources[name].consumed) / resources[name].draw, 1.0), 0.0);
    }

    // Combine resource_collections appropriately with + operator
    public static resource_collection operator+(resource_collection a, resource_collection b)
    {
      resource_collection collSum = new resource_collection();
      foreach(var pair in a.resources) collSum.Add(pair.Key, pair.Value);
      foreach(var pair in b.resources) collSum.Add(pair.Key, pair.Value);
      return collSum;
    }

    public double lifeTime(string res_name, int n = 0)
    {
      resource_data res = resources[res_name];

      double timeLeft = 0.0;
      if (n > 3) return timeLeft; //If function has stepped through 4 resources each requiring the next, assume we're caught in a loop and break out.
      if (res.amount <= double.Epsilon)
      {
        return 0.0;
      }
      else if ((res.consumed - res.produced) > double.Epsilon)
      {
        timeLeft = res.amount / (res.consumed - res.produced); //If resource delta is negative, calculate time remaining until resource is exhausted.
      }
      else
      {
        timeLeft = double.NaN; //If resource delta is positive or zero, this resource should last indefinitely (barring dependency on some other resource with a limited lifetime.)
      }
      if (res.supplyRequires.Count > 0)
      {
        timeLeft = res.amount / res.consumed; // Lifetime sans production, once dependency runs out.
        double dependentTime = double.MaxValue;
        foreach (string dependency in res.supplyRequires)
        {
          dependentTime = Math.Min(dependentTime, lifeTime(dependency, n + 1));
        }
        timeLeft += dependentTime;
      }
      return timeLeft;
    }
  }

  public class resource_transaction
  {
    public object target;                                // Rule, Scrubber, Recycler, or Greenhouse object to be applied when this transaction is processed.
    public string type;                                  // Type of module/rule set as target. ("Rule", "Scrubber", etc.)
    public double baseRate;                              // Base rate of resource effects when transaction runs (set from Rule and Converter rates, with environmental modifiers.)
    public List<string> inputs = new List<string>();     // Resources that are consumed in this transaction.
    public List<string> output = new List<string>();     // Resources produced in this transaction.
    public bool hasRun;                                  // Flagged as true when this transaction has been processed.
    public bool hasSupply;                               // If all input resources have sufficient supply, set to true, and transaction runs regardless of blockingTransactions.

    List<resource_transaction> blockingTransactions = new List<resource_transaction>(); // Resource transactions affecting supply resource(s), which should run first, if possible.

    // Run this transaction on a given resource_collection, return a collection (delta) with resource values after the transaction.
    public resource_collection Run(resource_collection collection, environment_data env)
    {
      resource_collection delta = new resource_collection();

      // add placeholder resources
      foreach(var pair in collection.resources) delta.Add(pair.Key);

      switch(type)
      {
        case "Rule":
        {
          Rule rule = target as Rule;
          double rateMod = collection.Availability(rule.resource_name);
          if (rule.resource_name.Length > 0)
          {
            delta[rule.resource_name].consumed += baseRate * rateMod;
            delta[rule.resource_name].draw -= baseRate;
          }
          if (rule.waste_name.Length > 0)
          {
            delta[rule.waste_name].produced += baseRate * rateMod * rule.waste_ratio;
          }
        }
        break;

        case "Scrubber":
        {
          Scrubber scrubber = target as Scrubber;
          if (!env.breathable)
          {
            double rateMod = collection.Availability(scrubber.waste_name);
            delta[scrubber.waste_name].consumed += rateMod * baseRate;
            delta[scrubber.waste_name].draw -= baseRate;
            delta[scrubber.resource_name].produced += rateMod * baseRate * Scrubber.DeduceEfficiency();
            delta[scrubber.resource_name].hasScrubber = true;
          }
          else
          {
            delta[scrubber.resource_name].produced += scrubber.intake_rate;
            delta[scrubber.resource_name].hasScrubber = true;
          }
        }
        break;

        case "Recycler":
        {
          Recycler recycler = target as Recycler;
          double rateMod = collection.Availability(recycler.waste_name);
          if (recycler.filter_name.Length > 0) rateMod = Math.Min(rateMod, collection.Availability(recycler.filter_name));
          delta[recycler.waste_name].consumed += rateMod * baseRate;
          delta[recycler.waste_name].draw -= baseRate;
          delta[recycler.resource_name].produced += rateMod * baseRate * recycler.waste_ratio;
          delta[recycler.resource_name].hasRecycler = true;
          if (recycler.filter_name.Length > 0)
          {
            delta[recycler.filter_name].consumed += rateMod * recycler.filter_rate;
            delta[recycler.filter_name].draw -= recycler.filter_rate;
          }
        }
        break;

        case "Greenhouse":
        {
          Greenhouse greenhouse = target as Greenhouse;
          double rateMod = 1.0;
          if (greenhouse.input_name.Length > 0) rateMod = collection.Availability(greenhouse.input_name);
          double wastePerc = collection.Availability(greenhouse.waste_name);
          double natural_lighting = Greenhouse.NaturalLighting(env.sun_dist);
          double lighting = (greenhouse.door_opened ? natural_lighting : 0.0) + greenhouse.lamps;
          double growth_bonus = (env.landed ? greenhouse.soil_bonus : 0.0) + (greenhouse.waste_bonus * wastePerc);
          double growth_factor = (greenhouse.growth_rate * (1.0 + growth_bonus)) * lighting;
          if (growth_factor > double.Epsilon)
          {
            double cycleTime = rateMod / growth_factor;
            delta[greenhouse.waste_name].consumed += wastePerc * greenhouse.waste_rate;
            delta[greenhouse.waste_name].draw -= greenhouse.waste_rate;
            delta[greenhouse.resource_name].produced += Math.Min(greenhouse.harvest_size, collection[greenhouse.resource_name].capacity) / cycleTime;
            delta[greenhouse.resource_name].hasGreenhouse = true;
            delta[greenhouse.resource_name].harvestTime = cycleTime;
            delta[greenhouse.resource_name].harvestSize = Math.Min(greenhouse.harvest_size, collection[greenhouse.resource_name].capacity);
            if (greenhouse.input_name.Length > 0)
            {
              delta[greenhouse.input_name].consumed += rateMod * greenhouse.input_rate;
              delta[greenhouse.input_name].draw -= greenhouse.input_rate;
            }
          }
        }
        break;
      }
      hasRun = true;
      return delta;
    }

    public bool hasSomeSupply(resource_collection resourcePool)
    {
      if (type == "Rule")
      {
        Rule rule = target as Rule;
        if (resourcePool.Availability(rule.resource_name) <= double.Epsilon) return false;
      }
      else if (type == "Scrubber")
      {
        Scrubber scrubber = target as Scrubber;
        if (resourcePool.Availability(scrubber.waste_name) <= double.Epsilon) return false;
      }
      else if (type == "Recycler")
      {
        Recycler recycler = target as Recycler;
        if (resourcePool.Availability(recycler.waste_name) <= double.Epsilon) return false;
        if (recycler.filter_name.Length > 0 && resourcePool.Availability(recycler.filter_name) <= double.Epsilon) return false;
      }
      else if (type == "Greenhouse")
      {
        Greenhouse greenhouse = target as Greenhouse;
        if (greenhouse.input_name.Length > 0 && resourcePool.Availability(greenhouse.input_name) <= double.Epsilon) return false;
      }
      return true;
    }

    // Check if this transaction should run.
    public bool ShouldRun()
    {
      if (hasSupply) return true; // If we have sufficient supply to meet all input resource needs, there's no need to wait on blockingTransactions.
      foreach(resource_transaction rt in blockingTransactions)
      {
        if (!rt.hasRun) return false; // If any blockingTransaction has not run, this should not run.
      }
      return true;
    }

    public void SetBlocking(List<resource_transaction> transactions)
    {
      foreach(resource_transaction rt in transactions)
      {
        // A transform should not block itself, even if it has an input and output resource in common.
        if (rt != this)
        {
          foreach (string input in inputs)
          {
            // If an output in rt matches an input in this transaction and the blocking list does not already contain rt, add it.
            if (rt.output.Contains(input) && !blockingTransactions.Contains(rt)) blockingTransactions.Add(rt);
          }
        }
      }
    }
  }

  public static resource_collection analyze_resources(List<Rule> rules, List<Part> parts, environment_data env, crew_data crew, qol_data qol)
  {
    resource_collection collection = new resource_collection ();
    List<resource_transaction> transactionSet = new List<resource_transaction>();

    // Modeling resource lifetimes:
    // I.    Build resource dictionary and transaction list based on Rules and Converters (Scrubbers, Recyclers, etc.)
    // II.   Add amount/capacity of resources from List<Part> parts to resource dictionary.
    // III.  Use transaction list to set transaction blocking for each transaction.
    // IV.   Loop through transaction list, executing transactions, until all transactions have executed, or some set remain blocked because of looping inputs.
    // V.    If some transactions remain... well it's complicated. (See below)
    // VI.   With all resource values populated (resupply, consumption, etc.), it is now possible to calculate an accurate lifetime for each resource.

    // I. Build resource dictionary and transaction list based on Rules and Converters (Scrubbers, Recyclers, etc.)
    // I.a: Rules
    foreach (Rule r in rules.FindAll(k => k.rate > 0.0))
    {
      double rate = (double)crew.count * (r.interval > 0 ? r.rate / r.interval : r.rate);
      foreach(string modifier in r.modifier)
      {
        switch (modifier)
        {
          case "breathable":  rate *= env.breathable ? 0.0 : 1.0;          break;
          case "temperature": rate *= env.temp_diff;                       break;
          case "qol":         rate *= qol != null ? 1.0 / qol.bonus : 1.0; break;
        }
      }

      resource_transaction transaction = new resource_transaction();
      transaction.type = "Rule";
      transaction.target = r;
      transaction.baseRate = rate;
      if (r.resource_name.Length > 0 && r.rate > 0.0)
      {
        collection.Add(r.resource_name);
        collection[r.resource_name].draw += rate;
        transaction.inputs.Add(r.resource_name);
      }
      if (r.waste_name.Length > 0)
      {
        collection.Add(r.waste_name);
        collection[r.waste_name].draw -= rate * r.waste_ratio;
        transaction.output.Add(r.waste_name);
      }
      transactionSet.Add(transaction);
    }

    // I.b: Converters (Scrubbers, Recyclers, and Greenhouses)
    foreach(Part p in parts)
    {
      foreach(PartModule m in p.Modules)
      {
        switch (m.moduleName)
        {
          case "Scrubber":
          {
            Scrubber scrubber = m as Scrubber;
            collection.Add(scrubber.resource_name);
            collection.Add(scrubber.waste_name);
            if (!scrubber.is_enabled) break; //If module not enabled, break out early
            if (!env.breathable) collection[scrubber.waste_name].draw += scrubber.co2_rate; //No draw in breathable atmosphere
            // Add transactions
            resource_transaction transaction = new resource_transaction();
            transaction.type = "Scrubber";
            transaction.target = m;
            if (!env.breathable) transaction.baseRate = scrubber.co2_rate;
            transaction.output.Add(scrubber.resource_name);
            if (!env.breathable) transaction.inputs.Add(scrubber.waste_name);
            transactionSet.Add(transaction);
          }
          break;

          case "Recycler":
          {
            Recycler recycler = m as Recycler;
            collection.Add(recycler.resource_name);
            collection.Add(recycler.waste_name);
            if (recycler.filter_name.Length > 0) collection.Add(recycler.filter_name);
            if (!recycler.is_enabled) break; //If module not enabled, break out early
            collection[recycler.waste_name].draw += recycler.waste_rate;
            if (recycler.filter_name.Length > 0)
            {
              collection[recycler.filter_name].draw += recycler.filter_rate;
              collection[recycler.resource_name].supplyRequires.Add(recycler.filter_name);
            }
            // Add transactions
            resource_transaction transaction = new resource_transaction();
            transaction.type = "Recycler";
            transaction.target = m;
            transaction.baseRate = recycler.waste_rate;
            transaction.output.Add(recycler.resource_name);
            transaction.inputs.Add(recycler.waste_name);
            if (recycler.filter_name.Length > 0) transaction.inputs.Add(recycler.filter_name);
            transactionSet.Add(transaction);
          }
          break;

          case "Greenhouse":
          {
            Greenhouse greenhouse = m as Greenhouse;
            collection.Add(greenhouse.resource_name);
            collection.Add(greenhouse.waste_name);
            collection[greenhouse.waste_name].draw += greenhouse.waste_rate;
            if (greenhouse.input_name.Length > 0)
            {
              collection.Add(greenhouse.input_name);
              collection[greenhouse.input_name].draw += greenhouse.input_rate;
            }
            // Add transactions
            resource_transaction transaction = new resource_transaction();
            transaction.type = "Greenhouse";
            transaction.target = m;
            transaction.output.Add(greenhouse.resource_name);
            transaction.inputs.Add(greenhouse.waste_name);
            if (greenhouse.input_name.Length > 0) transaction.inputs.Add(greenhouse.input_name);
            transactionSet.Add(transaction);
          }
          break;
        }
      }
    }

    // II. Add amount/capacity of resources from List<Part> parts to resource dictionary.
    foreach(Part p in parts)
    {
      foreach(var pair in collection.resources)
      {
        pair.Value.amount += Lib.Resource.Amount(p, pair.Key);
        pair.Value.capacity += Lib.Resource.Capacity(p, pair.Key);
      }
    }

    // III.  Use transaction list to find/set blocking transactions for each transaction. Also, set hasSupply flag.
    foreach(resource_transaction rt in transactionSet)
    {
      rt.SetBlocking(transactionSet);
      rt.hasSupply = true;
      foreach(string inputResource in rt.inputs)
      {
        rt.hasSupply &= collection[inputResource].amount > double.Epsilon;
      }
    }

    // IV. Loop through transaction list, executing transactions, until all transactions have executed, or some set remain blocked because of looping inputs.
    bool done;
    do
    {
      done = true;
      foreach (resource_transaction transaction in transactionSet)
      {
        if (!transaction.hasRun && transaction.ShouldRun())
        {
          collection += transaction.Run(collection, env);
          done = false;
        }
      }
    }
    while(!done);

    // V. The remainder...
    // If there are any transactions left, they're either duds that will never run (because of some resource requirement that is never met) or they have looping inputs/outputs.
    // Duds we can ignore at this point, as they won't have any effect on anything else. Loops, though, require some special handling.
    //
    // Because of the feedback between looped rules/converters, the steady state a looping system quickly falls into is a function of the sum of an infinite geometric series,
    // based on the efficiency of the system, and the initial input. Thankfully, we only actually need to know the initial input and two iterations of the system to calculate
    // the final value at infinite iterations using this equation: The FinalRate will equal the BaseRate + (RateDelta1 / (1 - (RateDelta2/RateDelta1)))
    //
    // So, as an example, a system with a resupply rate of 8 units per second initially, 11/s after one iteration (delta1 = +3), and 12.125/s the next (delta2 = +1.125),
    // would have an actual rate of resupply of 8 + (3/(1-(1.125/3))) = 12.8 per second after a moment or two, and that's the value to use when calculating resource lifetime.
    //
    List<resource_transaction> blockedTransactions = transactionSet.FindAll(tr => tr.hasRun == false); //Transactions that did not run because they have looping inputs/ouputs.

    if (blockedTransactions.Count != 0)
    {
      resource_collection[] deltaSlice = new resource_collection[4];
      deltaSlice[0] = new resource_collection() + collection; //Initial state for iteration

      int iteration = 0;
      do
      {
        // initialize next iteration to baseline
        deltaSlice[iteration + 1] = new resource_collection();
        foreach(var pair in collection.resources)
          deltaSlice[iteration+1].AddDraw(pair.Key, pair.Value.draw);

        //Spend initial resource supply from previous iteration/baserate
        foreach(resource_transaction transaction in blockedTransactions.FindAll(tr => tr.hasSomeSupply(deltaSlice[iteration])))
          deltaSlice[iteration] += transaction.Run(deltaSlice[iteration], env);

        //Then loop through the transaction list looking for any transactions that are now unblocked, adding the result to the next iteration
        do
        {
          done = true;
          foreach (resource_transaction transaction in blockedTransactions)
          {
            if (transaction.ShouldRun() && !transaction.hasRun)
            {
              resource_collection delta = transaction.Run(deltaSlice[iteration], env);
              deltaSlice[iteration].ApplyConsumption(delta);
              deltaSlice[iteration+1].ApplyProduction(delta);
              done = false;
            }
          }
        }
        while(!done);

        //Reset hasRun flag for all transactions in blockedTransactions to prepare for next iteration
        foreach (resource_transaction transaction in blockedTransactions) transaction.hasRun = false;
        iteration++;
      }
      while(iteration < 3);

      // Calculate results and add them back to resources.
      resource_collection calculatedDelta = new resource_collection();

      foreach(var pair in deltaSlice[0].resources)
      {
        string res_name = pair.Key;
        resource_data res = pair.Value;

        double[] conDelta = new double[3];
        conDelta[0] = res.consumed;
        conDelta[1] = deltaSlice[1][res_name].consumed;
        conDelta[2] = deltaSlice[2][res_name].consumed;

        double[] prodDelta = new double[3];
        prodDelta[0] = res.produced;
        prodDelta[1] = deltaSlice[1][res_name].produced;
        prodDelta[2] = deltaSlice[2][res_name].produced;

        double consumption;
        if (conDelta[1] <= double.Epsilon)
        {
          consumption = conDelta[0];
        }
        else if (conDelta[2] >= conDelta[1])
        {
          consumption = collection[res_name].draw; //Infinite/accelerating growth, rate capped by draw
        }
        else
        {
          consumption = conDelta[0] + (conDelta[1] / (1.0 - (conDelta[2] / conDelta[1])));
        }

        double production;
        if (prodDelta[1] <= double.Epsilon)
        {
          production = prodDelta[0];
        }
        else if (prodDelta[2] >= prodDelta[1])
        {
          production = collection[res_name].draw; //Infinite/accelerating growth, rate capped by draw
        }
        else
        {
          production = prodDelta[0] + (prodDelta[1] / (1.0 - (prodDelta[2] / prodDelta[1])));
        }

        foreach(resource_transaction rt in blockedTransactions)
        {
          if (rt.inputs.Contains(res_name) || rt.output.Contains(res_name))
          {
            calculatedDelta.AddConsumption(res_name, consumption);
            calculatedDelta.AddProduction(res_name, production);
          }
        }
      }
      collection += calculatedDelta;
    }
    return collection;
  }

  void render_resource(resource_collection collection, string name, Rule rule)
  {
    resource_data resource = collection[name];
    render_title(rule.resource_name.ToUpper());
    render_content("storage", Lib.ValueOrNone(resource.capacity));
    render_content("consumed", Lib.HumanReadableRate(resource.consumed));
    if (resource.hasGreenhouse) render_content("time to harvest", Lib.HumanReadableDuration(resource.harvestTime), Lib.BuildString("quantity: <b>", resource.harvestSize.ToString("F0"), "</b>"));
    else if (resource.hasRecycler) render_content("recycled", Lib.HumanReadableRate(resource.produced));
    else if (resource.hasScrubber) render_content("scrubbed", Lib.HumanReadableRate(resource.produced));
    else render_content("produced", Lib.HumanReadableRate(resource.produced));
    render_content(rule.breakdown ? "time to instability" : "life expectancy", Lib.HumanReadableDuration(collection.lifeTime(name)));
    render_space();
  }
  */