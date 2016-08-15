// ====================================================================================================================
// the vessel planner
// ====================================================================================================================


using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    public double atmo_factor;                            // proportion of sun flux not absorbed by the atmosphere
    public double sun_dist;                               // distance from the sun
    public double solar_flux;                             // flux received from the sun (consider atmospheric absorption)
    public double albedo_flux;                            // solar flux reflected from the body
    public double body_flux;                              // infrared radiative flux from the body
    public double total_flux;                             // total flux at vessel position
    public double temperature;                            // vessel temperature
    public double temp_diff;                              // average difference from survival temperature
    public double orbital_period;                         // length of orbit
    public double shadow_period;                          // length of orbit in shadow
    public double shadow_time;                            // proportion of orbit that is in shadow
  }


  public class crew_data
  {
    public uint count;                                    // number of crew on board
    public uint capacity;                                 // crew capacity of the vessel
    public bool engineer;                                 // true if an engineer is on board
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
    public double shielding;                              // amount vs capacity of radiation shielding on the vessel
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


  // ctor
  public Planner()
  {
    // set default body index & situation
    body_index = FlightGlobals.GetHomeBodyIndex();
    situation_index = 2;
    sunlight = true;

    // left menu style
    leftmenu_style = new GUIStyle(HighLogic.Skin.label);
    leftmenu_style.richText = true;
    leftmenu_style.normal.textColor = new Color(0.0f, 0.0f, 0.0f, 1.0f);
    leftmenu_style.fixedWidth = 80.0f;
    leftmenu_style.stretchHeight = true;
    leftmenu_style.fontSize = 10;
    leftmenu_style.alignment = TextAnchor.MiddleLeft;

    // right menu style
    rightmenu_style = new GUIStyle(leftmenu_style);
    rightmenu_style.alignment = TextAnchor.MiddleRight;

    // title container
    title_container_style = new GUIStyle();
    title_container_style.stretchWidth = true;
    title_container_style.fixedHeight = 16.0f;
    title_container_style.normal.background = Lib.GetTexture("black-background");
    title_container_style.margin.bottom = 4;
    title_container_style.margin.top = 4;

    // title label
    title_label_style = new GUIStyle(HighLogic.Skin.label);
    title_label_style.fontSize = 12;
    title_label_style.alignment = TextAnchor.MiddleCenter;
    title_label_style.normal.textColor = Color.white;
    title_label_style.stretchWidth = true;
    title_label_style.stretchHeight = true;

    // row style
    row_style = new GUIStyle();
    row_style.stretchWidth = true;
    row_style.fixedHeight = 16.0f;

    // label style
    label_style = new GUIStyle(HighLogic.Skin.label);
    label_style.richText = true;
    label_style.normal.textColor = Color.white;
    label_style.stretchWidth = true;
    label_style.stretchHeight = true;
    label_style.fontSize = 12;
    label_style.alignment = TextAnchor.MiddleLeft;

    // value style
    value_style = new GUIStyle(HighLogic.Skin.label);
    value_style.richText = true;
    value_style.normal.textColor = Color.white;
    value_style.stretchWidth = true;
    value_style.stretchHeight = true;
    value_style.fontSize = 12;
    value_style.alignment = TextAnchor.MiddleRight;
    value_style.fontStyle = FontStyle.Bold;

    // quote style
    quote_style = new GUIStyle(HighLogic.Skin.label);
    quote_style.richText = true;
    quote_style.normal.textColor = Color.white;
    quote_style.stretchWidth = true;
    quote_style.stretchHeight = true;
    quote_style.fontSize = 11;
    quote_style.alignment = TextAnchor.LowerCenter;

    // icon style
    icon_style = new GUIStyle();
    icon_style.alignment = TextAnchor.MiddleCenter;
  }


  static environment_data analyze_environment(CelestialBody body, double altitude_mult, bool sunlight)
  {
    // shortcuts
    CelestialBody sun = FlightGlobals.Bodies[0];

    // calculate data
    environment_data env = new environment_data();
    env.body = body;
    env.altitude = body.Radius * altitude_mult;
    env.landed = env.altitude <= double.Epsilon;
    env.breathable = env.landed && body.atmosphereContainsOxygen;
    env.atmo_factor = Sim.AtmosphereFactor(body, 0.7071);
    env.sun_dist = Sim.Apoapsis(Lib.PlanetarySystem(body)) - sun.Radius - body.Radius;
    Vector3d sun_dir = (sun.position - body.position).normalized;
    env.solar_flux = sunlight ? Sim.SolarFlux(env.sun_dist) * env.atmo_factor : 0.0;
    env.albedo_flux = sunlight ? Sim.AlbedoFlux(body, body.position + sun_dir * (body.Radius + env.altitude)) : 0.0;
    env.body_flux = Sim.BodyFlux(body, env.altitude);
    env.total_flux = env.solar_flux + env.albedo_flux + env.body_flux + Sim.BackgroundFlux();
    env.temperature = !env.landed || !body.atmosphere ? Sim.BlackBodyTemperature(env.total_flux) : body.GetTemperature(0.0);
    env.temp_diff = Sim.TempDiff(env.temperature);
    env.orbital_period = Sim.OrbitalPeriod(body, env.altitude);
    env.shadow_period = Sim.ShadowPeriod(body, env.altitude);
    env.shadow_time = env.shadow_period / env.orbital_period;

    // return data
    return env;
  }


  static crew_data analyze_crew(List<Part> parts)
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

    // if the user press ALT, the planner consider the vessel crewed at full capacity
    if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) crew.count = crew.capacity;

    // return data
    return crew;
  }



  static qol_data analyze_qol(List<Part> parts, environment_data env, crew_data crew, signal_data signal, Rule rule_qol)
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


  static radiation_data analyze_radiation(List<Part> parts, environment_data env, crew_data crew, Rule rule_radiation)
  {
    // store data
    radiation_data radiation = new radiation_data();

    // scan the parts
    double amount = 0.0;
    double capacity = 0.0;
    double rad = 0.0;
    foreach(Part p in parts)
    {
      // accumulate shielding amount and capacity
      amount += Lib.Amount(p, "Shielding");
      capacity += Lib.Capacity(p, "Shielding");

      // accumulate emitter radiation
      foreach(Emitter m in p.FindModulesImplementing<Emitter>())
      {
        rad += m.radiation * m.intensity;
      }
    }

    // calculate radiation data
    radiation.shielding = Radiation.Shielding(amount, capacity);
    if (crew.capacity > 0)
    {
      var rb = Radiation.Info(env.body);
      var sun = Radiation.Info(FlightGlobals.Bodies[0]);
      double extern_rad = Settings.ExternRadiation + rad;
      double heliopause_rad = extern_rad + sun.radiation_pause;
      double magnetopause_rad = heliopause_rad + rb.radiation_pause;
      double inner_rad = magnetopause_rad + rb.radiation_inner;
      double outer_rad = magnetopause_rad + rb.radiation_outer;
      double surface_rad = magnetopause_rad * Sim.GammaTransparency(env.body, 0.0);
      double storm_rad = heliopause_rad + Settings.StormRadiation * (env.solar_flux > double.Epsilon ? 1.0 : 0.0);
      radiation.life_expectancy = new double[]
      {
        rule_radiation.fatal_threshold / Math.Max(Radiation.Nominal, surface_rad * (1.0 - radiation.shielding)),        // surface radiation
        rule_radiation.fatal_threshold / Math.Max(Radiation.Nominal, magnetopause_rad * (1.0 - radiation.shielding)),   // inside magnetopause
        rule_radiation.fatal_threshold / Math.Max(Radiation.Nominal, inner_rad * (1.0 - radiation.shielding)),          // inside inner belt
        rule_radiation.fatal_threshold / Math.Max(Radiation.Nominal, outer_rad * (1.0 - radiation.shielding)),          // inside outer belt
        rule_radiation.fatal_threshold / Math.Max(Radiation.Nominal, heliopause_rad * (1.0 - radiation.shielding)),     // interplanetary
        rule_radiation.fatal_threshold / Math.Max(Radiation.Nominal, extern_rad * (1.0 - radiation.shielding)),         // interstellar
        rule_radiation.fatal_threshold / Math.Max(Radiation.Nominal, storm_rad * (1.0 - radiation.shielding))           // storm
      };
    }
    else
    {
      //radiation.rate = new double[]{0.0, 0.0, 0.0, 0.0, 0.0, 0.0};
      radiation.life_expectancy = new double[]{double.NaN, double.NaN, double.NaN, double.NaN, double.NaN};
    }

    // return data
    return radiation;
  }


  static reliability_data analyze_reliability(List<Part> parts, signal_data signal, resource_simulator sim)
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
    simulated_resource ec = sim.resource("ElectricCharge");
    double ec_redundancy =  ec.best_producer < ec.produced ? (ec.produced - ec.best_producer) / ec.produced : 0.0;
    double antenna_redundancy = signal.second_best_range > 0.0 ? signal.second_best_range / signal.range : 0.0;
    List<string> redundancies = new List<string>();
    if (ec_redundancy >= 0.5) redundancies.Add("ec");
    if (antenna_redundancy >= 0.99) redundancies.Add("antenna");
    if (redundancies.Count == 0) redundancies.Add("none");
    reliability.redundancy = String.Join(", ", redundancies.ToArray());

    // return data
    return reliability;
  }


  static signal_data analyze_signal(List<Part> parts)
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
    GUILayout.BeginHorizontal(title_container_style);
    GUILayout.Label(title, title_label_style);
    GUILayout.EndHorizontal();
  }


  void render_content(string desc, string value, string tooltip="")
  {
    GUILayout.BeginHorizontal(row_style);
    GUILayout.Label(desc, label_style);
    GUILayout.Label(new GUIContent(value, tooltip.Length > 0 ? Lib.BuildString("<i>", tooltip, "</i>") : ""), value_style);
    GUILayout.EndHorizontal();
  }


  void render_space()
  {
    GUILayout.Space(10.0f);
  }


  void render_environment(environment_data env)
  {
    string flux_tooltip = Lib.BuildString
    (
      "<align=left /><b>source\t\tflux\t\ttemp</b>\n",
      "solar\t\t", env.solar_flux > 0.0 ? Lib.HumanReadableFlux(env.solar_flux) : "none\t", "\t", Lib.HumanReadableTemp(Sim.BlackBodyTemperature(env.solar_flux)), "\n",
      "albedo\t\t", env.albedo_flux > 0.0 ? Lib.HumanReadableFlux(env.albedo_flux) : "none\t", "\t", Lib.HumanReadableTemp(Sim.BlackBodyTemperature(env.albedo_flux)), "\n",
      "body\t\t", env.body_flux > 0.0 ? Lib.HumanReadableFlux(env.body_flux) : "none\t", "\t", Lib.HumanReadableTemp(Sim.BlackBodyTemperature(env.body_flux)), "\n",
      "background\t", Lib.HumanReadableFlux(Sim.BackgroundFlux()), "\t", Lib.HumanReadableTemp(Sim.BlackBodyTemperature(Sim.BackgroundFlux())), "\n",
      "total\t\t", Lib.HumanReadableFlux(env.total_flux), "\t", Lib.HumanReadableTemp(Sim.BlackBodyTemperature(env.total_flux))
    );
    string atmosphere_tooltip = Lib.BuildString
    (
      "<align=left />",
      "breathable\t\t<b>", (env.body.atmosphereContainsOxygen ? "yes" : "no"), "</b>\n",
      "pressure\t\t<b>", env.body.atmospherePressureSeaLevel.ToString("F0"), " kPa</b>\n",
      "visible absorption\t<b>", Lib.HumanReadablePerc(1.0 - env.atmo_factor), "</b>\n",
      "gamma absorption\t<b>", Lib.HumanReadablePerc(1.0 - Sim.GammaTransparency(env.body, 0.0)), "</b>"
    );
    string shadowtime_str = Lib.HumanReadableDuration(env.shadow_period) + " (" + (env.shadow_time * 100.0).ToString("F0") + "%)";

    render_title("ENVIRONMENT");
    render_content("temperature", Lib.HumanReadableTemp(env.temperature), env.body.atmosphere ? "atmospheric" : flux_tooltip);
    render_content("temp diff", Lib.HumanReadableTemp(env.temp_diff), "difference between external and survival temperature");
    render_content("atmosphere", env.body.atmosphere ? "yes" : "no", atmosphere_tooltip);
    render_content("shadow time", shadowtime_str, "the time in shadow\nduring the orbit");
    render_space();
  }


  void render_resource(string resource_name, bool breakdown, resource_simulator sim)
  {
    simulated_resource res = sim.resource(resource_name);

    render_title(resource_name.AddSpacesOnCaps().ToUpper());
    render_content("storage", Lib.ValueOrNone(res.storage));
    render_content("consumed", Lib.HumanReadableRate(res.consumed));
    if (!res.has_greenhouse)
    {
      string label = "produced";
      if (resource_name == "ElectricCharge") label = "generated";
      else if (res.has_recycler) label = "recycled";
      render_content(label, Lib.HumanReadableRate(res.produced));
    }
    else
    {
      string harvest_tooltip = Lib.BuildString(res.harvest_size.ToString("F0"), " in ", Lib.HumanReadableDuration(res.time_to_harvest));
      render_content("harvest", res.time_to_harvest <= double.Epsilon ? "none" : harvest_tooltip);
    }
    render_content(breakdown ? "time to instability" : "life expectancy", Lib.HumanReadableDuration(res.lifetime()));
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
    RadiationBody rb = Radiation.Info(env.body);
    RadiationModel mf = rb.model;
    string levels_tooltip = crew.capacity == 0 ? "" : Lib.BuildString
    (
      "<align=left />",
      "surface\t\t<b>", Lib.HumanReadableDuration(radiation.life_expectancy[0]), "</b>\n",
      mf.has_pause ? Lib.BuildString("magnetopause\t<b>", Lib.HumanReadableDuration(radiation.life_expectancy[1]), "</b>\n") : "",
      mf.has_inner ? Lib.BuildString("inner belt\t<b>", Lib.HumanReadableDuration(radiation.life_expectancy[2]), "</b>\n") : "",
      mf.has_outer ? Lib.BuildString("outer belt\t<b>", Lib.HumanReadableDuration(radiation.life_expectancy[3]), "</b>\n") : "",
      "interplanetary\t<b>", Lib.HumanReadableDuration(radiation.life_expectancy[4]), "</b>\n",
      "interstellar\t<b>", Lib.HumanReadableDuration(radiation.life_expectancy[5]), "</b>\n",
      "storm\t\t<b>", Lib.HumanReadableDuration(radiation.life_expectancy[6]), "</b>"
    );


    render_title("RADIATION");
    if (env.landed || !mf.has_pause) render_content("surface", Lib.HumanReadableDuration(radiation.life_expectancy[0]), levels_tooltip);
    else render_content("magnetopause", rb.model.has_pause ? Lib.HumanReadableDuration(radiation.life_expectancy[1]) : "no", levels_tooltip);
    render_content("interplanetary", Lib.HumanReadableDuration(radiation.life_expectancy[4]), levels_tooltip);
    render_content("storm", Lib.HumanReadableDuration(radiation.life_expectancy[6]), levels_tooltip);
    render_content("shielding", Radiation.ShieldingToString(radiation.shielding), levels_tooltip);
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
    return 260.0f;
  }


  public float height()
  {
    // detect page layout once
    detect_layout();

    // deduce height
    return 26.0f                              // header + margin
         + 100.0f * (float)panels_per_page;   // panels
  }


  public void render()
  {
    // detect page layout once
    detect_layout();

    // if there is something in the editor
    if (EditorLogic.RootPart != null)
    {
      // get body, situation and altitude multiplier
      CelestialBody body = FlightGlobals.Bodies[body_index];
      string situation = situations[situation_index];
      double altitude_mult = altitude_mults[situation_index];

      // get parts recursively
      List<Part> parts = Lib.GetPartsRecursively(EditorLogic.RootPart);

      // analyze stuff
      environment_data env = analyze_environment(body, altitude_mult, sunlight);
      crew_data crew = analyze_crew(parts);
      signal_data signal = analyze_signal(parts);
      qol_data qol = Kerbalism.qol_rule != null ? analyze_qol(parts, env, crew, signal, Kerbalism.qol_rule) : null;
      resource_simulator sim = new resource_simulator(parts, Kerbalism.rules, env, qol, crew);

      // start header
      GUILayout.BeginHorizontal(row_style);

      // body selector
      GUILayout.Label(body.name, leftmenu_style);
      if (Lib.IsClicked()) { body_index = (body_index + 1) % FlightGlobals.Bodies.Count; if (body_index == 0) ++body_index; }
      else if (Lib.IsClicked(1)) { body_index = (body_index - 1) % FlightGlobals.Bodies.Count; if (body_index == 0) body_index = FlightGlobals.Bodies.Count - 1; }

      // previous page button
      if (pages_count > 1)
      {
        GUILayout.Label(arrow_left, icon_style);
        if (Lib.IsClicked()) page = (page == 0 ? pages_count : page) - 1u;
      }

      // sunlight selector
      GUILayout.Label(icon_sunlight[sunlight ? 1 : 0], icon_style);
      if (Lib.IsClicked()) sunlight = !sunlight;

      // next page button
      if (pages_count > 1)
      {
        GUILayout.Label(arrow_right, icon_style);
        if (Lib.IsClicked()) page = (page + 1) % pages_count;
      }

      // situation selector
      GUILayout.Label(situation, rightmenu_style);
      if (Lib.IsClicked()) { situation_index = (situation_index + 1) % situations.Length; }
      else if (Lib.IsClicked(1)) { situation_index = (situation_index == 0 ? situations.Length : situation_index) - 1; }

      // end header
      GUILayout.EndHorizontal();


      // ec
      uint panel_index = 0;
      if (panel_index / panels_per_page == page)
      {
        render_resource("ElectricCharge", Kerbalism.ec_rule != null && Kerbalism.ec_rule.breakdown, sim);
      }
      ++panel_index;

      // supplies
      foreach(Rule r in Kerbalism.supply_rules.FindAll(k => k.degeneration > 0.0))
      {
        if (panel_index / panels_per_page == page)
        {
          render_resource(r.resource_name, r.breakdown, sim);
        }
        ++panel_index;
      }

      // qol
      if (Kerbalism.qol_rule != null)
      {
        if (panel_index / panels_per_page == page)
        {
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
          reliability_data reliability = analyze_reliability(parts, signal, sim);
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

  // store situations and altitude multipliers
  string[] situations = { "Landed", "Low Orbit", "Orbit", "High Orbit" };
  double[] altitude_mults = { 0.0, 0.33, 1.0, 3.0 };

  // sunlight selector textures
  Texture[] icon_sunlight = { Lib.GetTexture("sun-black"), Lib.GetTexture("sun-white") };

  // arrow icons
  Texture arrow_left = Lib.GetTexture("left-black");
  Texture arrow_right = Lib.GetTexture("right-black");

  // styles
  GUIStyle leftmenu_style;
  GUIStyle rightmenu_style;
  GUIStyle title_container_style;
  GUIStyle title_label_style;
  GUIStyle row_style;
  GUIStyle label_style;
  GUIStyle value_style;
  GUIStyle quote_style;
  GUIStyle icon_style;

  // body/situation/sunlight indexes
  int body_index;
  int situation_index;
  bool sunlight;

  // current planner page
  uint page;

  // automatic page layout of panels
  bool layout_detected;
  uint panels_count;
  uint panels_per_page;
  uint pages_count;
}


// simulate resource consumption & production
public class resource_simulator
{
  public resource_simulator(List<Part> parts, List<Rule> rules, Planner.environment_data env, Planner.qol_data qol, Planner.crew_data crew)
  {
    // get amount and capacity from parts
    foreach(Part p in parts)
    {
      foreach(PartResource res in p.Resources.list)
      {
        process_part(p, res.resourceName);
      }
    }

    // process all rules
    foreach(Rule r in rules)
    {
      if (r.resource_name.Length > 0 && r.rate > 0.0)
      {
        process_rule(r, env, qol, crew);
      }
    }

    // remember if we already considered a resource converter module
    // rationale: we assume only the first module in a converter is active
    bool first_converter = true;

    // process all modules
    foreach(Part p in parts)
    {
      foreach(PartModule m in p.Modules)
      {
        switch(m.moduleName)
        {
          case "Scrubber":                    process_scrubber(m as Scrubber, env);                                 break;
          case "Recycler":                    process_recycler(m as Recycler);                                      break;
          case "Greenhouse":                  process_greenhouse(m as Greenhouse, env);                             break;
          case "GravityRing":                 process_ring(m as GravityRing);                                       break;
          case "Antenna":                     process_antenna(m as Antenna);                                        break;
          case "Emitter":                     process_emitter(m as Emitter);                                        break;
          case "ModuleCommand":               process_command(m as ModuleCommand);                                  break;
          case "ModuleDeployableSolarPanel":  process_panel(m as ModuleDeployableSolarPanel, env);                  break;
          case "ModuleGenerator":             process_generator(m as ModuleGenerator, p);                           break;
          case "ModuleResourceConverter":     process_converter(m as ModuleResourceConverter, ref first_converter); break;
          case "ModuleKPBSConverter":         process_converter(m as ModuleResourceConverter, ref first_converter); break;
          case "ModuleResourceHarvester":     process_harvester(m as ModuleResourceHarvester);                      break;
          case "ModuleActiveRadiator":        process_radiator(m as ModuleActiveRadiator);                          break;
          case "ModuleWheelMotor":            process_wheel_motor(m as ModuleWheelMotor);                           break;
          case "ModuleWheelMotorSteering":    process_wheel_steering(m as ModuleWheelMotorSteering);                break;
          case "SCANsat":                     process_scanner(p, m);                                                break;
          case "ModuleSCANresourceScanner":   process_scanner(p, m);                                                break;
          case "ModuleCurvedSolarPanel":      process_curved_panel(p, m, env);                                      break;
          case "FissionGenerator":            process_fission_generator(p, m);                                      break;
          case "ModuleRadioisotopeGenerator": process_radioisotope_generator(p, m);                                 break;
          case "ModuleCryoTank":              process_cryotank(p, m);                                               break;
        }
      }
    }

    // execute all recipes in order of priority
    recipes.Sort(simulated_recipe.compare);
    foreach(simulated_recipe recipe in recipes) recipe.execute(this);

    // clamp all resources
    foreach(var pair in resources) pair.Value.clamp();
  }


  public simulated_resource resource(string name)
  {
    simulated_resource res;
    if (!resources.TryGetValue(name, out res))
    {
      res = new simulated_resource();
      resources.Add(name, res);
    }
    return res;
  }


  void process_part(Part p, string res_name)
  {
    simulated_resource res = resource(res_name);
    res.storage += Lib.Amount(p, res_name);
    res.amount += Lib.Amount(p, res_name);
    res.capacity += Lib.Capacity(p, res_name);
  }


  void process_rule(Rule r, Planner.environment_data env, Planner.qol_data qol, Planner.crew_data crew)
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

    if (r.waste_name.Length == 0)
    {
      resource(r.resource_name).consume(rate * k);
    }
    else if (rate > double.Epsilon)
    {
      simulated_recipe recipe = new simulated_recipe(simulated_recipe.rule_priority);
      recipe.input(r.resource_name, rate * k);
      recipe.output(r.waste_name, rate * r.waste_ratio * k);
      recipes.Add(recipe);
    }
  }


  void process_scrubber(Scrubber scrubber, Planner.environment_data env)
  {
    simulated_resource res = resource(scrubber.resource_name);

    if (env.breathable)
    {
      res.produce(scrubber.intake_rate);
    }
    else if (scrubber.is_enabled)
    {
      simulated_recipe recipe = new simulated_recipe(simulated_recipe.scrubber_priority);
      recipe.input(scrubber.waste_name, scrubber.co2_rate);
      recipe.input("ElectricCharge", scrubber.ec_rate);
      recipe.output(scrubber.resource_name, scrubber.co2_rate * scrubber.co2_ratio * Scrubber.DeduceEfficiency());
      recipes.Add(recipe);
    }
    res.has_recycler = true;
  }


  void process_recycler(Recycler recycler)
  {
    simulated_resource res = resource(recycler.resource_name);

    if (recycler.is_enabled)
    {
      bool has_filter = recycler.filter_name.Length > 0 && recycler.filter_rate > 0.0;
      double efficiency = recycler.use_efficiency ? Scrubber.DeduceEfficiency() : 1.0;

      simulated_recipe recipe = new simulated_recipe(simulated_recipe.scrubber_priority);
      recipe.input(recycler.waste_name, recycler.waste_rate);
      recipe.input("ElectricCharge", recycler.ec_rate);
      if (has_filter) recipe.input(recycler.filter_name, recycler.filter_rate);
      recipe.output(recycler.resource_name, recycler.waste_rate * recycler.waste_ratio * efficiency);
      recipes.Add(recipe);
    }
    res.has_recycler = true;
  }


  void process_greenhouse(Greenhouse greenhouse, Planner.environment_data env)
  {
    // get resource handlers
    simulated_resource ec = resource("ElectricCharge");
    simulated_resource waste = resource(greenhouse.waste_name);

    // consume ec
    double ec_k = Math.Min(1.0, ec.amount / (greenhouse.ec_rate * greenhouse.lamps));
    ec.consume(greenhouse.ec_rate * greenhouse.lamps);

    // calculate natural lighting
    double natural_lighting = env.solar_flux / Sim.SolarFluxAtHome();

    // calculate lighting
    double lighting = natural_lighting * (greenhouse.door_opened ? 1.0 : 0.0) + greenhouse.lamps * ec_k;

    // consume waste
    double waste_k = 0.0;
    if (lighting > double.Epsilon)
    {
      waste_k = Math.Min(1.0, waste.amount / greenhouse.waste_rate);
      waste.consume(greenhouse.waste_rate);
    }

    // calculate growth bonus
    double growth_bonus = 0.0;
    growth_bonus += greenhouse.soil_bonus * (env.landed ? 1.0 : 0.0);
    growth_bonus += greenhouse.waste_bonus * waste_k;

    // calculate growth factor
    double growth_factor = (greenhouse.growth_rate * (1.0 + growth_bonus)) * lighting;

    // produce food
    simulated_resource res = resource(greenhouse.resource_name);
    res.produce(greenhouse.harvest_size * growth_factor);

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
    if (ring.opened) resource("ElectricCharge").consume(ring.ec_rate * ring.speed);
  }


  void process_antenna(Antenna antenna)
  {
    if (antenna.relay) resource("ElectricCharge").consume(antenna.relay_cost);
  }


  void process_emitter(Emitter emitter)
  {
    resource("ElectricCharge").consume(emitter.ec_rate * emitter.intensity);
  }


  void process_command(ModuleCommand command)
  {
    foreach(ModuleResource res in command.inputResources)
    {
      resource(res.name).consume(res.rate);
    }
  }


  void process_panel(ModuleDeployableSolarPanel panel, Planner.environment_data env)
  {
    double generated = panel.chargeRate * env.solar_flux / Sim.SolarFluxAtHome();
    resource("ElectricCharge").produce(generated);
  }


  void process_generator(ModuleGenerator generator, Part p)
  {
     // skip launch clamps, that include a generator
     if (p.partInfo.name == "launchClamp1") return;

     simulated_recipe recipe = new simulated_recipe(simulated_recipe.converter_priority);
     foreach(ModuleResource res in generator.inputList)
     {
       recipe.input(res.name, res.rate);
     }
     foreach(ModuleResource res in generator.outputList)
     {
       recipe.output(res.name, res.rate);
     }
     recipes.Add(recipe);
  }


  void process_converter(ModuleResourceConverter converter, ref bool first_converter)
  {
    // only consider the first converter in a part
    if (!first_converter) return;
    first_converter = false;

    simulated_recipe recipe = new simulated_recipe(simulated_recipe.converter_priority);
    foreach(ResourceRatio res in converter.inputList)
    {
      recipe.input(res.ResourceName, res.Ratio);
    }
    foreach(ResourceRatio res in converter.outputList)
    {
      recipe.output(res.ResourceName, res.Ratio);
    }
    recipes.Add(recipe);
  }


  void process_harvester(ModuleResourceHarvester harvester)
  {
    simulated_recipe recipe = new simulated_recipe(simulated_recipe.harvester_priority);
    foreach(ResourceRatio res in harvester.inputList)
    {
      recipe.input(res.ResourceName, res.Ratio);
    }
    recipe.output(harvester.ResourceName, double.MaxValue); //< token value, to make the converters work
    recipes.Add(recipe);
  }


  void process_radiator(ModuleActiveRadiator radiator)
  {
    if (radiator.IsCooling)
    {
      foreach(var res in radiator.inputResources)
      {
        resource(res.name).consume(res.rate);
      }
    }
  }


  void process_wheel_motor(ModuleWheelMotor motor)
  {
    if (motor.motorEnabled)
    {
      resource(motor.inputResource.name).consume(motor.inputResource.rate);
    }
  }


  void process_wheel_steering(ModuleWheelMotorSteering steering)
  {
    if (steering.motorEnabled)
    {
      resource(steering.inputResource.name).consume(steering.inputResource.rate);
    }
  }


  void process_scanner(Part p, PartModule m)
  {
    // consume ec if deployed
    if (SCANsat.isDeployed(p, m))
    {
      resource("ElectricCharge").consume(SCANsat.EcConsumption(m));
    }
  }


  void process_curved_panel(Part p, PartModule m, Planner.environment_data env)
  {
    // note: assume half the components are in sunlight, and average inclination is half

    // get total rate
    double tot_rate = Lib.ReflectionValue<float>(m, "TotalEnergyRate");

    // get number of components
    int components = p.FindModelTransforms(Lib.ReflectionValue<string>(m, "PanelTransformName")).Length;

    // approximate output
    // 0.7071: average clamped cosine
    resource("ElectricCharge").produce(tot_rate * 0.7071 * env.solar_flux / Sim.SolarFluxAtHome());
  }


  void process_fission_generator(Part p, PartModule m)
  {
    double max_rate = Lib.ReflectionValue<float>(m, "PowerGeneration");

    // get fission reactor tweakable, will default to 1.0 for other modules
    var reactor = p.FindModuleImplementing<ModuleResourceConverter>();
    double tweakable = reactor == null ? 1.0 : Lib.ReflectionValue<float>(reactor, "CurrentPowerPercent") * 0.01f;

    resource("ElectricCharge").produce(max_rate * tweakable);
  }


  void process_radioisotope_generator(Part p, PartModule m)
  {
    double max_rate = Lib.ReflectionValue<float>(m, "BasePower");

    resource("ElectricCharge").produce(max_rate);
  }


  void process_cryotank(Part p, PartModule m)
  {
     // note: assume cooling is active
     double cooling_cost = Lib.ReflectionValue<float>(m, "CoolingCost");
     string fuel_name = Lib.ReflectionValue<string>(m, "FuelName");

     resource("ElectricCharge").consume(cooling_cost * Lib.Capacity(p, fuel_name) * 0.001);
  }


  Dictionary<string, simulated_resource> resources = new Dictionary<string, simulated_resource>(32);
  List<simulated_recipe> recipes = new List<simulated_recipe>(32);
}


public sealed class simulated_resource
{
  public void consume(double quantity)
  {
    amount -= quantity;
    consumed += quantity;
  }

  public void produce(double quantity)
  {
    amount += quantity;
    produced += quantity;
    best_producer = Math.Max(best_producer, quantity);
  }

  public void clamp()
  {
    this.amount = Lib.Clamp(this.amount, 0.0, this.capacity);
  }

  public double lifetime()
  {
    double rate = produced - consumed;
    return amount <= double.Epsilon ? 0.0 : rate > -1e-10 ? double.NaN : amount / -rate;
  }

  public double storage;                      // amount stored (at the start of simulation)
  public double capacity;                     // storage capacity
  public double amount;                       // amount stored (during simulation)
  public double consumed;                     // total consumption rate
  public double produced;                     // total production rate
  public double best_producer;                // rate of best producer
  public bool   has_recycler;                 // true if a recycler was processed
  public bool   has_greenhouse;               // true if a greenhouse was processed
  public double time_to_harvest;              // seconds until harvest
  public double harvest_size;                 // harvest size
}


public sealed class simulated_recipe
{
  // hard-coded priorities
  public const int rule_priority = 0;
  public const int scrubber_priority = 1;
  public const int harvester_priority = 2;
  public const int converter_priority = 3;

  // ctor
  public simulated_recipe(int priority)
  {
    this.priority = priority;
  }

  // add an input to the recipe
  public void input(string resource_name, double quantity)
  {
    inputs[resource_name] = quantity;
  }

  // add an output to the recipe
  public void output(string resource_name, double quantity)
  {
    outputs[resource_name] = quantity;
  }

  // execute the recipe
  public void execute(resource_simulator sim)
  {
    // determine worst input ratio
    double worst_input = 1.0;
    foreach(var pair in inputs)
    {
      if (pair.Value > double.Epsilon) //< avoid division by zero
      {
        simulated_resource res = sim.resource(pair.Key);
        worst_input = Math.Min(worst_input, Math.Max(0.0, res.amount / pair.Value));
      }
    }

    // consume inputs
    foreach(var pair in inputs)
    {
      simulated_resource res = sim.resource(pair.Key);
      res.consume(pair.Value * worst_input);
    }

    // produce outputs
    foreach(var pair in outputs)
    {
      simulated_resource res = sim.resource(pair.Key);
      res.produce(pair.Value * worst_input);
    }
  }

  // used to sort recipes by priority
  public static int compare(simulated_recipe a, simulated_recipe b)
  {
    return a.priority < b.priority ? -1 : a.priority == b.priority ? 0 : 1;
  }

  // store inputs and outputs
  public Dictionary<string, double> inputs = new Dictionary<string, double>();
  public Dictionary<string, double> outputs = new Dictionary<string, double>();
  public int priority;
}


} // KERBALISM
