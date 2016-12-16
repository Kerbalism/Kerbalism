using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ModuleWheels;
using UnityEngine;


namespace KERBALISM {


public sealed class Planner
{
  public Planner()
  {
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

    // quote style
    quote_style = new GUIStyle(HighLogic.Skin.label);
    quote_style.richText = true;
    quote_style.normal.textColor = Color.black;
    quote_style.stretchWidth = true;
    quote_style.stretchHeight = true;
    quote_style.fontSize = 11;
    quote_style.alignment = TextAnchor.LowerCenter;

    // center icon style
    icon_style = new GUIStyle();
    icon_style.alignment = TextAnchor.MiddleCenter;

    // set default body index & situation
    body_index = FlightGlobals.GetHomeBodyIndex();
    situation_index = 2;
    sunlight = true;

    // analyzers
    sim = new resource_simulator();
    env = new environment_analyzer();
    va = new vessel_analyzer();

    // resource panels
    panel_resource = new List<string>();
    Profile.supplies.FindAll(k => k.resource != "ElectricCharge").ForEach(k => panel_resource.Add(k.resource));

    // special panels
    panel_special = new List<string>();
    if (Features.LivingSpace) panel_special.Add("qol");
    if (Features.Radiation) panel_special.Add("radiation");
    if (Features.Reliability) panel_special.Add("reliability");
    if (Features.Signal) panel_special.Add("signal");

    // environment panels
    panel_environment = new List<string>();
    if (Features.Pressure || Features.Poisoning) panel_environment.Add("habitat");
    panel_environment.Add("environment");
  }


  void render_environment()
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
      "pressure\t\t<b>", Lib.HumanReadablePressure(env.body.atmospherePressureSeaLevel), "</b>\n",
      "light absorption\t\t<b>", Lib.HumanReadablePerc(1.0 - env.atmo_factor), "</b>\n",
      "gamma absorption\t<b>", Lib.HumanReadablePerc(1.0 - Sim.GammaTransparency(env.body, 0.0)), "</b>"
    );
    string shadowtime_str = Lib.HumanReadableDuration(env.shadow_period) + " (" + (env.shadow_time * 100.0).ToString("F0") + "%)";

    Panel.section("ENVIRONMENT", ref environment_index, panel_environment.Count);
    Panel.content("temperature", Lib.HumanReadableTemp(env.temperature), env.body.atmosphere && env.landed ? "atmospheric" : flux_tooltip);
    Panel.content("difference", Lib.HumanReadableTemp(env.temp_diff), "difference between external and survival temperature");
    Panel.content("atmosphere", env.body.atmosphere ? "yes" : "no", atmosphere_tooltip);
    Panel.content("shadow time", shadowtime_str, "the time in shadow\nduring the orbit");
    Panel.space();
  }


  void render_ec()
  {
    // get simulated resource
    simulated_resource res = sim.resource("ElectricCharge");

    // create tooltip
    string tooltip = res.tooltip();

    // render the panel section
    Panel.section("ELECTRIC CHARGE");
    Panel.content("storage", Lib.HumanReadableAmount(res.storage), tooltip);
    Panel.content("consumed", Lib.HumanReadableRate(res.consumed), tooltip);
    Panel.content("produced", Lib.HumanReadableRate(res.produced), tooltip);
    Panel.content("duration", Lib.HumanReadableDuration(res.lifetime()));
    Panel.space();
  }


  void render_resource(string res_name)
  {
    // get simulated resource
    simulated_resource res = sim.resource(res_name);

    // create tooltip
    string tooltip = res.tooltip();

    // render the panel section
    Panel.section(Lib.SpacesOnCaps(res_name).ToUpper(), ref resource_index, panel_resource.Count);
    Panel.content("storage", Lib.HumanReadableAmount(res.storage), tooltip);
    Panel.content("consumed", Lib.HumanReadableRate(res.consumed), tooltip);
    Panel.content("produced", Lib.HumanReadableRate(res.produced), tooltip);
    Panel.content("duration", Lib.HumanReadableDuration(res.lifetime()));
    Panel.space();
  }


  void render_stress()
  {
    // get first living space rule
    // - guaranteed to exist, as this panel is not rendered without Feature.LivingSpace (that's detected by living space modifier)
    // - even without crew, it is safe to evaluate the modifiers that use it
    Rule rule = Profile.rules.Find(k => k.modifiers.Contains("living_space"));

    // render title
    Panel.section("STRESS", ref special_index, panel_special.Count);

    // render living space data
    // generate details tooltips
    string living_space_tooltip = Lib.BuildString
    (
      "volume per-capita: <b>", Lib.HumanReadableVolume(va.volume / (double)Math.Max(va.crew_count, 1)), "</b>\n",
      "ideal living space: <b>", Lib.HumanReadableVolume(Settings.IdealLivingSpace), "</b>"
    );
    Panel.content("living space", Habitat.living_space_to_string(va.living_space), living_space_tooltip);

    // render comfort data
    if (rule.modifiers.Contains("comfort"))
    {
      /*string comfort_tooltip = Lib.BuildString
      (
        "firm-ground: <b><color=", va.comforts.Contains("firm-ground") ? "<color=#00ff00>yes</color>" : "<color=#ff0000>no</color>", "</b>\n",
        "exercise: <b><color=", va.comforts.Contains("exercise") ? "<color=#00ff00>yes</color>" : "<color=#ff0000>no</color>", "</b>\n",
        "not-alone: <b><color=", va.comforts.Contains("not-alone") ? "<color=#00ff00>yes</color>" : "<color=#ff0000>no</color>", "</b>\n",
        "call-home: <b><color=", va.comforts.Contains("call-home") ? "<color=#00ff00>yes</color>" : "<color=#ff0000>no</color>", "</b>\n",
        "panorama: <b><color=", va.comforts.Contains("panorama") ? "<color=#00ff00>yes</color>" : "<color=#ff0000>no</color>", "</b>"
      );*/
      //Panel.content("comfort", Comfort.factor_to_string(va.comfort), comfort_tooltip);
      Panel.content("comfort", va.comforts.summary(), va.comforts.tooltip());
    }
    else
    {
      Panel.content("comfort", "n/a");
    }

    // render pressure data
    if (rule.modifiers.Contains("pressure"))
    {
      string pressure_tooltip = va.pressurized
        ? "Free roaming in a pressurized environment is\nvastly superior to living in a suit."
        : "Being forced inside a suit all the time greatly\nreduce the crew quality of life.\nThe worst part is the diaper.";
      Panel.content("pressurized", va.pressurized ? "yes" : "no", pressure_tooltip);
    }
    else
    {
      Panel.content("pressurized", "n/a");
    }

    // render life estimate
    double mod = Modifiers.evaluate(env, va, sim, rule.modifiers);
    Panel.content("duration", Lib.HumanReadableDuration(rule.fatal_threshold / (rule.degeneration * mod)));

    // render spacing
    Panel.space();
  }


  void render_radiation()
  {
    // get first radiation rule
    // - guaranteed to exist, as this panel is not rendered without Feature.Radiation (that's detected by radiation modifier)
    // - even without crew, it is safe to evaluate the modifiers that use it
    Rule rule = Profile.rules.Find(k => k.modifiers.Contains("radiation"));

    // detect if it use shielding
    bool use_shielding = rule.modifiers.Contains("shielding");

    // calculate various radiation levels
    var levels = new []
    {
      Math.Max(Radiation.Nominal, (env.surface_rad + va.emitted)),        // surface
      Math.Max(Radiation.Nominal, (env.magnetopause_rad + va.emitted)),   // inside magnetopause
      Math.Max(Radiation.Nominal, (env.inner_rad + va.emitted)),          // inside inner belt
      Math.Max(Radiation.Nominal, (env.outer_rad + va.emitted)),          // inside outer belt
      Math.Max(Radiation.Nominal, (env.heliopause_rad + va.emitted)),     // interplanetary
      Math.Max(Radiation.Nominal, (env.extern_rad + va.emitted)),         // interstellar
      Math.Max(Radiation.Nominal, (env.storm_rad + va.emitted))           // storm
    };

    // evaluate modifiers (except radiation)
    List<string> modifiers_except_radiation = new List<string>();
    foreach(string s in rule.modifiers) { if (s != "radiation") modifiers_except_radiation.Add(s); }
    double mod = Modifiers.evaluate(env, va, sim, modifiers_except_radiation);

    // calculate life expectancy at various radiation levels
    var estimates = new double[7];
    for(int i=0; i<7; ++i)
    {
      estimates[i] = rule.fatal_threshold / (rule.degeneration * mod * levels[i]);
    }

    // generate tooltip
    var mf = Radiation.Info(env.body).model;
    string tooltip = Lib.BuildString
    (
      "<align=left />",
      "surface\t\t<b>", Lib.HumanReadableDuration(estimates[0]), "</b>\n",
      mf.has_pause ? Lib.BuildString("magnetopause\t<b>", Lib.HumanReadableDuration(estimates[1]), "</b>\n") : "",
      mf.has_inner ? Lib.BuildString("inner belt\t<b>", Lib.HumanReadableDuration(estimates[2]), "</b>\n") : "",
      mf.has_outer ? Lib.BuildString("outer belt\t<b>", Lib.HumanReadableDuration(estimates[3]), "</b>\n") : "",
      "interplanetary\t<b>", Lib.HumanReadableDuration(estimates[4]), "</b>\n",
      "interstellar\t<b>", Lib.HumanReadableDuration(estimates[5]), "</b>\n",
      "storm\t\t<b>", Lib.HumanReadableDuration(estimates[6]), "</b>"
    );

    // render the panel
    Panel.section("RADIATION", ref special_index, panel_special.Count);
    Panel.content("surface", Lib.HumanReadableRadiation(env.surface_rad + va.emitted), tooltip);
    Panel.content("orbit", Lib.HumanReadableRadiation(env.magnetopause_rad), tooltip);
    if (va.emitted >= 0.0) Panel.content("emission", Lib.HumanReadableRadiation(va.emitted), tooltip);
    else Panel.content("active shielding", Lib.HumanReadableRadiation(-va.emitted), tooltip);
    Panel.content("shielding", rule.modifiers.Contains("shielding") ? Habitat.shielding_to_string(va.shielding) : "n/a", tooltip);
    Panel.space();
  }


  void render_reliability()
  {
    // evaluate redundancy metric
    // - 0: no redundancy
    // - 0.5: all groups have 2 elements
    // - 1.0: all groups have 3 or more elements
    double redundancy_metric = 0.0;
    foreach(var p in va.redundancy)
    {
      switch(p.Value)
      {
        case 1:  break;
        case 2:  redundancy_metric += 0.5 / (double)va.redundancy.Count; break;
        default: redundancy_metric += 1.0 / (double)va.redundancy.Count; break;
      }
    }

    // traduce the redundancy metric to string
    string redundancy_str = string.Empty;
    if (redundancy_metric <= 0.1) redundancy_str = "none";
    else if (redundancy_metric <= 0.33) redundancy_str = "poor";
    else if (redundancy_metric <= 0.66) redundancy_str = "okay";
    else redundancy_str = "great";

    // generate redundancy tooltip
    string redundancy_tooltip = string.Empty;
    if (va.redundancy.Count > 0)
    {
      StringBuilder sb = new StringBuilder();
      foreach(var p in va.redundancy)
      {
        if (sb.Length > 0) sb.Append("\n");
        sb.Append("<b>");
        switch(p.Value)
        {
          case 1: sb.Append("<color=red>"); break;
          case 2: sb.Append("<color=yellow>"); break;
          default: sb.Append("<color=green>"); break;
        }
        sb.Append(p.Value.ToString());
        sb.Append("</color></b>\t");
        sb.Append(p.Key);
      }
      redundancy_tooltip = Lib.BuildString("<align=left />", sb.ToString());
    }

    // generate repair string and tooltip
    string repair_str = "none";
    string repair_tooltip = string.Empty;
    if (va.crew_engineer)
    {
      repair_str = "engineer";
      repair_tooltip = "The engineer on board should\nbe able to handle all repairs";
    }
    else if (va.crew_capacity == 0)
    {
      repair_str = "safemode";
      repair_tooltip = "We have a chance of repairing\nsome of the malfunctions remotely";
    }

    // render panel
    Panel.section("RELIABILITY", ref special_index, panel_special.Count);
    Panel.content("malfunctions", Lib.HumanReadableAmount(va.failure_year, "/y"), "average case estimate\nfor the whole vessel");
    Panel.content("high quality", Lib.HumanReadablePerc(va.high_quality), "percentage of high quality components");
    Panel.content("redundancy", redundancy_str, redundancy_tooltip);
    Panel.content("repair", repair_str, repair_tooltip);
    Panel.space();
  }


  void render_signal()
  {
    // range tooltip
    string range_tooltip = "";
    if (va.direct_dist > double.Epsilon)
    {
      if (va.direct_dist < va.home_dist_min) range_tooltip = "<color=#ff0000>out of range</color>";
      else if (va.direct_dist < va.home_dist_max) range_tooltip = "<color=#ffff00>partially out of range</color>";
      else range_tooltip = "<color=#00ff00>in range</color>";
      if (va.home_dist_max > double.Epsilon) //< if not landed at home
      {
        if (Math.Abs(va.home_dist_min - va.home_dist_max) <= double.Epsilon)
        {
          range_tooltip += Lib.BuildString("\ntarget distance: <b>", Lib.HumanReadableRange(va.home_dist_min), "</b>");
        }
        else
        {
          range_tooltip += Lib.BuildString
          (
            "\ntarget distance (min): <b>", Lib.HumanReadableRange(va.home_dist_min), "</b>",
            "\ntarget distance (max): <b>", Lib.HumanReadableRange(va.home_dist_max), "</b>"
          );
        }
      }
    }
    else if (va.crew_capacity == 0)
    {
      range_tooltip = "<color=#ff0000>no antenna on unmanned vessel</color>";
    }

    // data rate tooltip
    string rate_tooltip = va.direct_rate > double.Epsilon
      ? Lib.BuildString
      (
        "<align=left />",
        "<i>data transmission rate at target distance</i>\n\n",
        "<b>data size</b>\t<b>transmission time</b>",
        "\n250Mb\t\t", Lib.HumanReadableDuration(250.0 / va.direct_rate),
        "\n500Mb\t\t", Lib.HumanReadableDuration(500.0 / va.direct_rate),
        "\n1Gb\t\t", Lib.HumanReadableDuration(1000.0 / va.direct_rate),
        "\n2Gb\t\t", Lib.HumanReadableDuration(2000.0 / va.direct_rate),
        "\n4Gb\t\t", Lib.HumanReadableDuration(4000.0 / va.direct_rate),
        "\n8Gb\t\t", Lib.HumanReadableDuration(8000.0 / va.direct_rate)
      ) : string.Empty;

    // transmission cost tooltip
    string cost_tooltip = va.direct_cost > double.Epsilon
      ? "the <b>ElectricCharge</b> per-second consumed\nfor data transmission directly to <b>DSN</b>"
      : string.Empty;

    // indirect tooltip
    string indirect_tooltip = va.indirect_dist > double.Epsilon
      ? Lib.BuildString
      (
        "<align=left />",
        "<i>inter-vessel communication capabilities</i>\n\n",
        "range (max)\t<b>", Lib.HumanReadableRange(va.indirect_dist), "</b>\n",
        "rate (best)\t<b>", Lib.HumanReadableDataRate(va.indirect_rate), "</b>\n",
        "cost\t\t<b>", va.indirect_cost.ToString("F2"), " EC/s", "</b>"
      ) : string.Empty;

    // render the panel
    Panel.section("SIGNAL", ref special_index, panel_special.Count);
    Panel.content("range", Lib.HumanReadableRange(va.direct_dist), range_tooltip);
    Panel.content("rate", Lib.HumanReadableDataRate(va.direct_rate), rate_tooltip);
    Panel.content("cost", va.direct_cost > double.Epsilon ? Lib.BuildString(va.direct_cost.ToString("F2"), " EC/s") : "none", cost_tooltip);
    Panel.content("inter-vessel", va.indirect_dist > double.Epsilon ? "yes" : "no", indirect_tooltip);
    Panel.space();
  }


  void render_habitat()
  {
    simulated_resource atmo_res = sim.resource("Atmosphere");
    simulated_resource waste_res = sim.resource("WasteAtmosphere");

    // generate tooltips
    string atmo_tooltip = atmo_res.tooltip();
    string waste_tooltip = waste_res.tooltip(true);

    // generate status string for scrubbing
    string waste_status = !Features.Poisoning                   //< feature disabled
      ? "n/a"
      : waste_res.produced <= double.Epsilon                    //< unnecessary
      ? "not required"
      : waste_res.consumed <= double.Epsilon                    //< no scrubbing
      ? "<color=#ffff00>none</color>"
      : waste_res.produced > waste_res.consumed * 1.001         //< insufficient scrubbing
      ? "<color=#ffff00>inadequate</color>"
      : "good";                                                 //< sufficient scrubbing

    // generate status string for pressurization
    string atmo_status = !Features.Pressure                     //< feature disabled
      ? "n/a"
      : atmo_res.consumed <= double.Epsilon                     //< unnecessary
      ? "not required"
      : atmo_res.produced <= double.Epsilon                     //< no pressure control
      ? "none"
      : atmo_res.consumed > atmo_res.produced * 1.001           //< insufficient pressure control
      ? "<color=#ffff00>inadequate</color>"
      : "good";                                                 //< sufficient pressure control

    Panel.section("HABITAT", ref environment_index, panel_environment.Count);
    Panel.content("volume", Lib.HumanReadableVolume(va.volume), "volume of enabled habitats");
    Panel.content("surface", Lib.HumanReadableSurface(va.surface), "surface of enabled habitats");
    Panel.content("scrubbing", waste_status, waste_tooltip);
    Panel.content("pressurization", atmo_status, atmo_tooltip);
    Panel.space();
  }


  public float width()
  {
    return 260.0f;
  }


  public float height()
  {
    if (EditorLogic.RootPart != null)
    {
      // calculate panels count
      uint panels_count = 2u
        + (panel_resource.Count > 0 ? 1u : 0u)
        + (panel_special.Count > 0 ? 1u : 0u);

      return 30.0f + Panel.height(4) * (float)panels_count;
    }
    else
    {
      return 66.0f; // quote-only
    }
  }


  public void render()
  {
    // if there is something in the editor
    if (EditorLogic.RootPart != null)
    {
      // get body, situation and altitude multiplier
      CelestialBody body = FlightGlobals.Bodies[body_index];
      string situation = situations[situation_index];
      double altitude_mult = altitude_mults[situation_index];

      // get parts recursively
      List<Part> parts = Lib.GetPartsRecursively(EditorLogic.RootPart);

      // analyze
      env.analyze(body, altitude_mult, sunlight);
      sim.analyze(parts, env, va);
      va.analyze(parts, sim, env);

      // start header
      GUILayout.BeginHorizontal(Styles.title_container);

      // body selector
      GUILayout.Label(new GUIContent(body.name, "Target body"), leftmenu_style);
      if (Lib.IsClicked()) { body_index = (body_index + 1) % FlightGlobals.Bodies.Count; if (body_index == 0) ++body_index; }
      else if (Lib.IsClicked(1)) { body_index = (body_index - 1) % FlightGlobals.Bodies.Count; if (body_index == 0) body_index = FlightGlobals.Bodies.Count - 1; }

      // sunlight selector
      GUILayout.Label(new GUIContent(icon_sunlight[sunlight ? 1 : 0], "In sunlight/shadow"), icon_style);
      if (Lib.IsClicked()) sunlight = !sunlight;

      // situation selector
      GUILayout.Label(new GUIContent(situation, "Target situation"), rightmenu_style);
      if (Lib.IsClicked()) { situation_index = (situation_index + 1) % situations.Length; }
      else if (Lib.IsClicked(1)) { situation_index = (situation_index == 0 ? situations.Length : situation_index) - 1; }

      // end header
      GUILayout.EndHorizontal();


      // ec panel
      render_ec();

      // resource panel
      if (panel_resource.Count > 0)
      {
        render_resource(panel_resource[resource_index]);
      }

      // special panel
      if (panel_special.Count > 0)
      {
        switch(panel_special[special_index])
        {
          case "qol":         render_stress();      break;
          case "radiation":   render_radiation();   break;
          case "reliability": render_reliability(); break;
          case "signal":      render_signal();      break;
        }
      }

      // environment panel
      switch(panel_environment[environment_index])
      {
        case "habitat":       render_habitat();     break;
        case "environment":   render_environment(); break;
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

  // store situations and altitude multipliers
  string[] situations = { "Landed", "Low Orbit", "Orbit", "High Orbit" };
  double[] altitude_mults = { 0.0, 0.33, 1.0, 3.0 };

  // sunlight selector textures
  Texture[] icon_sunlight = { Lib.GetTexture("sun-black"), Lib.GetTexture("sun-white") };

  // styles
  GUIStyle leftmenu_style;
  GUIStyle rightmenu_style;
  GUIStyle quote_style;
  GUIStyle icon_style;

  // analyzers
  resource_simulator sim = new resource_simulator();
  environment_analyzer env = new environment_analyzer();
  vessel_analyzer va = new vessel_analyzer();

  // panel arrays
  List<string> panel_resource;
  List<string> panel_special;
  List<string> panel_environment;

  // body/situation/sunlight indexes
  int body_index;
  int situation_index;
  bool sunlight;

  // panel indexes
  int resource_index;
  int special_index;
  int environment_index;
}


// analyze the environment
public sealed class environment_analyzer
{
  public void analyze(CelestialBody body, double altitude_mult, bool sunlight)
  {
    // shortcuts
    CelestialBody sun = FlightGlobals.Bodies[0];

    this.body = body;
    altitude = body.Radius * altitude_mult;
    landed = altitude <= double.Epsilon;
    breathable = body == FlightGlobals.GetHomeBody() && body.atmosphereContainsOxygen && landed;
    atmo_factor = Sim.AtmosphereFactor(body, 0.7071);
    sun_dist = Sim.Apoapsis(Lib.PlanetarySystem(body)) - sun.Radius - body.Radius;
    Vector3d sun_dir = (sun.position - body.position).normalized;
    solar_flux = sunlight ? Sim.SolarFlux(sun_dist) * (landed ? atmo_factor : 1.0) : 0.0;
    albedo_flux = sunlight ? Sim.AlbedoFlux(body, body.position + sun_dir * (body.Radius + altitude)) : 0.0;
    body_flux = Sim.BodyFlux(body, altitude);
    total_flux = solar_flux + albedo_flux + body_flux + Sim.BackgroundFlux();
    temperature = !landed || !body.atmosphere ? Sim.BlackBodyTemperature(total_flux) : body.GetTemperature(0.0);
    temp_diff = Sim.TempDiff(temperature, body, landed);
    orbital_period = Sim.OrbitalPeriod(body, altitude);
    shadow_period = Sim.ShadowPeriod(body, altitude);
    shadow_time = shadow_period / orbital_period;

    var rb = Radiation.Info(body);
    var sun_rb = Radiation.Info(sun);
    gamma_transparency = Sim.GammaTransparency(body, 0.0);
    extern_rad = Settings.ExternRadiation ;
    heliopause_rad = extern_rad + sun_rb.radiation_pause;
    magnetopause_rad = heliopause_rad + rb.radiation_pause;
    inner_rad = magnetopause_rad + rb.radiation_inner;
    outer_rad = magnetopause_rad + rb.radiation_outer;
    surface_rad = magnetopause_rad * gamma_transparency;
    storm_rad = heliopause_rad + Settings.StormRadiation * (solar_flux > double.Epsilon ? 1.0 : 0.0);
  }


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

  public double gamma_transparency;                     // proportion of radiation not blocked by atmosphere
  public double extern_rad;                             // environment radiation outside the heliopause
  public double heliopause_rad;                         // environment radiation inside the heliopause
  public double magnetopause_rad;                       // environment radiation inside the magnetopause
  public double inner_rad;                              // environment radiation inside the inner belt
  public double outer_rad;                              // environment radiation inside the outer belt
  public double surface_rad;                            // environment radiation on the surface of the body
  public double storm_rad;                              // environment radiation during a solar storm, inside the heliopause
}


// analyze the vessel (excluding resource-related stuff)
public sealed class vessel_analyzer
{
  public void analyze(List<Part> parts, resource_simulator sim, environment_analyzer env)
  {
    analyze_crew(parts);
    analyze_habitat(sim);
    analyze_radiation(parts, sim);
    analyze_reliability(parts);
    analyze_signal(parts, env);
    analyze_qol(parts, sim, env);
  }


  void analyze_crew(List<Part> parts)
  {
    // get number of kerbals assigned to the vessel in the editor
    // note: crew manifest is not reset after root part is deleted
    var manifest = KSP.UI.CrewAssignmentDialog.Instance.GetManifest();
    List<ProtoCrewMember> crew = manifest.GetAllCrew(false).FindAll(k => k != null);
    crew_count = (uint)crew.Count;
    crew_engineer = crew.Find(k => k.trait == "Engineer") != null;
    crew_scientist = crew.Find(k => k.trait == "Scientist") != null;
    crew_pilot = crew.Find(k => k.trait == "Pilot") != null;

    // scan the parts
    crew_capacity = 0;
    foreach(Part p in parts)
    {
      // accumulate crew capacity
      crew_capacity += (uint)p.CrewCapacity;
    }

    // if the user press ALT, the planner consider the vessel crewed at full capacity
    if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) crew_count = crew_capacity;
  }


  void analyze_habitat(resource_simulator sim)
  {
    // calculate total volume
    volume = sim.resource("Atmosphere").capacity;

    // calculate total surface
    surface = sim.resource("Shielding").capacity;

    // determine if the vessel has pressure control capabilities
    pressurized = sim.resource("Atmosphere").produced > 0.0;

    // determine if the vessel has scrubbing capabilities
    scrubbed = sim.resource("WasteAtmosphere").consumed > 0.0;
  }


  void analyze_radiation(List<Part> parts, resource_simulator sim)
  {
    // scan the parts
    emitted = 0.0;
    foreach(Part p in parts)
    {
      // for each module
      foreach(PartModule m in p.Modules)
      {
        // skip disabled modules
        if (!m.isEnabled) continue;

        // accumulate emitter radiation
        if (m.moduleName == "Emitter")
        {
          Emitter emitter = m as Emitter;

          emitted += emitter.running ? emitter.radiation : 0.0;
        }
      }
    }

    // calculate shielding factor
    double amount = sim.resource("Shielding").amount;
    double capacity = sim.resource("Shielding").capacity;
    shielding = (capacity > double.Epsilon ? amount / capacity : 1.0) * Settings.ShieldingEfficiency;
  }


  void analyze_reliability(List<Part> parts)
  {
    // reset data
    high_quality = 0.0;
    components = 0;
    failure_year = 0.0;
    redundancy = new Dictionary<string, int>();

    // scan the parts
    double year_time = 60.0 * 60.0 * Lib.HoursInDay() * Lib.DaysInYear();
    foreach(Part p in parts)
    {
      // for each module
      foreach(PartModule m in p.Modules)
      {
        // skip disabled modules
        if (!m.isEnabled) continue;

        // malfunctions
        if (m.moduleName == "Reliability")
        {
          Reliability reliability = m as Reliability;

          // calculate mtbf
          double mtbf = reliability.mtbf * (reliability.quality ? Settings.QualityScale : 1.0);

          // accumulate failures/y
          failure_year += year_time / mtbf;

          // accumulate high quality percentage
          high_quality += reliability.quality ? 1.0 : 0.0;

          // accumulate number of components
          ++components;

          // compile redundancy data
          if (reliability.redundancy.Length > 0)
          {
            int count = 0;
            if (redundancy.TryGetValue(reliability.redundancy, out count))
            {
              redundancy[reliability.redundancy] = count + 1;
            }
            else
            {
              redundancy.Add(reliability.redundancy, 1);
            }
          }

        }
      }
    }

    // calculate high quality percentage
    high_quality /= (double)Math.Max(components, 1u);
  }


  void analyze_signal(List<Part> parts, environment_analyzer env)
  {
    // approximate min/max distance between home and target body
    CelestialBody home = FlightGlobals.GetHomeBody();
    home_dist_min = 0.0;
    home_dist_max = 0.0;
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

    // scan the parts
    direct_dist = 0.0;
    direct_rate = 0.0;
    direct_cost = 0.0;
    indirect_dist = 0.0;
    indirect_rate = 0.0;
    indirect_cost = 0.0;
    foreach(Part p in parts)
    {
      // for each module
      foreach(PartModule m in p.Modules)
      {
        // skip disabled modules
        if (!m.isEnabled) continue;

        // antenna
        // - we consider them even if not extended, for ease of use
        //   and because of module animator behaviour in editor
        if (m.moduleName == "Antenna")
        {
          Antenna antenna = m as Antenna;

          // calculate direct range/rate/cost
          direct_dist = Math.Max(direct_dist, antenna.dist);
          direct_rate += Antenna.calculate_rate(home_dist_min, antenna.dist, antenna.rate);
          direct_cost += antenna.cost;

          // calculate indirect range/rate/cost
          if (antenna.type == AntennaType.low_gain)
          {
            indirect_dist = Math.Max(indirect_dist, antenna.dist);
            indirect_rate += antenna.rate; //< best case
            indirect_cost += antenna.cost;
          }
        }
      }
    }
  }


  void analyze_qol(List<Part> parts, resource_simulator sim, environment_analyzer env)
  {
    // calculate living space factor
    living_space = Lib.Clamp((volume / (double)Math.Max(crew_count, 1u)) / Settings.IdealLivingSpace, 0.1, 1.0);

    // calculate comfort factor
    comforts = new Comforts(parts, env.landed, crew_count > 1, direct_rate > 0.0 || !Features.Signal);
  }


  // general
  public uint   crew_count;                             // crew member on board
  public uint   crew_capacity;                          // crew member capacity
  public bool   crew_engineer;                          // true if an engineer is among the crew
  public bool   crew_scientist;                         // true if a scientist is among the crew
  public bool   crew_pilot;                             // true if a pilot is among the crew

  // habitat
  public double volume;                                 // total volume in m^3
  public double surface;                                // total surface in m^2
  public bool   pressurized;                            // true if the vessel has pressure control capabilities
  public bool   scrubbed;                               // true if the vessel has co2 scrubbing capabilities

  // radiation related
  public double emitted;                                // amount of radiation emitted by components
  public double shielding;                              // shielding factor

  // quality-of-life related
  public double living_space;                           // living space factor
  public Comforts comforts;                             // comfort info

  // reliability-related
  public uint   components;                             // number of components that can fail
  public double high_quality;                           // percentual of high quality components
  public double failure_year;                           // estimated failures per-year, averaged per-component
  public Dictionary<string, int> redundancy;            // number of components per redundancy group

  // signal-related
  public double direct_dist;                            // max comm range to DSN
  public double direct_rate;                            // data transmission rate to DSN from target destination
  public double direct_cost;                            // ec required for transmission to DSN
  public double indirect_dist;                          // max comm range to other vessels
  public double indirect_rate;                          // best-case data transmission rate to other vessels
  public double indirect_cost;                          // ec required for transmission to other vessels
  public double home_dist_min;                          // best-case distance from target to home body
  public double home_dist_max;                          // worst-case distance from target to home body
}




// simulate resource consumption & production
public class resource_simulator
{
  public void analyze(List<Part> parts, environment_analyzer env, vessel_analyzer va)
  {
    // note: resource analysis require vessel analysis, but at the same time vessel analysis
    // require resource analysis, so we are using vessel analysis from previous frame (that's okay)

    // clear previous resource state
    resources.Clear();

    // get amount and capacity from parts
    foreach(Part p in parts)
    {
      for(int i=0; i < p.Resources.Count; ++i)
      {
        process_part(p, p.Resources[i].resourceName);
      }
    }

    // process all rules
    foreach(Rule r in Profile.rules)
    {
      if (r.input.Length > 0 && r.rate > 0.0)
      {
        process_rule(r, env, va);
      }
    }

    // process all processes
    foreach(Process p in Profile.processes)
    {
      process_process(p, env, va);
    }

    // process all modules
    foreach(Part p in parts)
    {
      // get planner controller in the part
      PlannerController ctrl = p.FindModuleImplementing<PlannerController>();

      // ignore all modules in the part if specified in controller
      if (ctrl != null && !ctrl.considered) continue;

      // for each module
      foreach(PartModule m in p.Modules)
      {
        // skip disabled modules
        // rationale: the Selector disable non-selected modules in this way
        if (!m.isEnabled) continue;

        switch(m.moduleName)
        {
          case "Greenhouse":                   process_greenhouse(m as Greenhouse, env, va);            break;
          case "GravityRing":                  process_ring(m as GravityRing);                          break;
          case "Emitter":                      process_emitter(m as Emitter);                           break;
          case "Harvester":                    process_harvester(m as Harvester);                       break;
          case "Laboratory":                   process_laboratory(m as Laboratory);                     break;
          case "Antenna":                      process_antenna(m as Antenna);                           break;
          case "ModuleCommand":                process_command(m as ModuleCommand);                     break;
          case "ModuleDeployableSolarPanel":   process_panel(m as ModuleDeployableSolarPanel, env);     break;
          case "ModuleGenerator":              process_generator(m as ModuleGenerator, p);              break;
          case "ModuleResourceConverter":      process_converter(m as ModuleResourceConverter);         break;
          case "ModuleKPBSConverter":          process_converter(m as ModuleResourceConverter);         break;
          case "ModuleResourceHarvester":      process_harvester(m as ModuleResourceHarvester);         break;
          case "ModuleScienceConverter":       process_stocklab(m as ModuleScienceConverter);           break;
          case "ModuleActiveRadiator":         process_radiator(m as ModuleActiveRadiator);             break;
          case "ModuleWheelMotor":             process_wheel_motor(m as ModuleWheelMotor);              break;
          case "ModuleWheelMotorSteering":     process_wheel_steering(m as ModuleWheelMotorSteering);   break;
          case "ModuleLight":                  process_light(m as ModuleLight);                         break;
          case "ModuleColoredLensLight":       process_light(m as ModuleLight);                         break;
          case "ModuleMultiPointSurfaceLight": process_light(m as ModuleLight);                         break;
          case "SCANsat":                      process_scanner(m);                                      break;
          case "ModuleSCANresourceScanner":    process_scanner(m);                                      break;
          case "ModuleCurvedSolarPanel":       process_curved_panel(p, m, env);                         break;
          case "FissionGenerator":             process_fission_generator(p, m);                         break;
          case "ModuleRadioisotopeGenerator":  process_radioisotope_generator(p, m);                    break;
          case "ModuleCryoTank":               process_cryotank(p, m);                                  break;
        }
      }
    }

    // execute all possible recipes
    bool executing = true;
    while(executing)
    {
      executing = false;
      for(int i=0; i<recipes.Count; ++i)
      {
        simulated_recipe recipe = recipes[i];
        if (recipe.left > double.Epsilon)
        {
          executing |= recipe.execute(this);
        }
      }
    }
    recipes.Clear();

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


  void process_rule(Rule r, environment_analyzer env, vessel_analyzer va)
  {
    // deduce rate per-second
    double rate = (double)va.crew_count * (r.interval > 0.0 ? r.rate / r.interval : r.rate);

    // evaluate modifiers
    double k = Modifiers.evaluate(env, va, this, r.modifiers);

    // prepare recipe
    if (r.output.Length == 0)
    {
      resource(r.input).consume(rate * k, r.name);
    }
    else if (rate > double.Epsilon)
    {
      // note: rules always dump excess overboard (because it is waste)
      simulated_recipe recipe = new simulated_recipe(r.name, true);
      recipe.input(r.input, rate * k);
      recipe.output(r.output, rate * k * r.ratio);
      recipes.Add(recipe);
    }
  }


  void process_process(Process p, environment_analyzer env, vessel_analyzer va)
  {
    // evaluate modifiers
    double k = Modifiers.evaluate(env, va, this, p.modifiers);

    // prepare recipe
    simulated_recipe recipe = new simulated_recipe(p.name, p.dump);
    foreach(var input in p.inputs)
    {
      recipe.input(input.Key, input.Value * k);
    }
    foreach(var output in p.outputs)
    {
      recipe.output(output.Key, output.Value * k);
    }
    recipes.Add(recipe);
  }


  void process_greenhouse(Greenhouse g, environment_analyzer env, vessel_analyzer va)
  {
    // skip disabled greenhouses
    if (!g.active) return;

    // shortcut to resources
    simulated_resource ec = resource("ElectricCharge");
    simulated_resource res = resource(g.crop_resource);

    // calculate natural and artificial lighting
    double natural = env.solar_flux;
    double artificial = Math.Max(g.light_tolerance - natural, 0.0);

    // if lamps are on and artificial lighting is required
    if (artificial > 0.0)
    {
      // consume ec for the lamps
      ec.consume(g.ec_rate * (artificial / g.light_tolerance), "greenhouse");
    }

    // execute recipe
    simulated_recipe recipe = new simulated_recipe("greenhouse", true);
    foreach(ModuleResource input in g.resHandler.inputResources) recipe.input(input.name, input.rate);
    foreach(ModuleResource output in g.resHandler.outputResources) recipe.output(output.name, output.rate);
    recipes.Add(recipe);

    // determine environment conditions
    bool lighting = natural + artificial >= g.light_tolerance;
    bool pressure = va.pressurized || env.breathable || g.pressure_tolerance <= double.Epsilon;
    bool radiation = (env.landed ? env.surface_rad : env.magnetopause_rad) * (1.0 - va.shielding) < g.radiation_tolerance;

    // if all conditions apply
    // note: we are assuming the inputs are satisfied, we can't really do otherwise here
    if (lighting && pressure && radiation)
    {
      // produce food
      res.produce(g.crop_size * g.crop_rate, "greenhouse");

      // add harvest info
      res.harvests.Add(Lib.BuildString(g.crop_size.ToString("F0"), " in ", Lib.HumanReadableDuration(1.0 / g.crop_rate)));
    }
  }


  void process_ring(GravityRing ring)
  {
    if (ring.deployed) resource("ElectricCharge").consume(ring.ec_rate, "gravity ring");
  }


  void process_emitter(Emitter emitter)
  {
    if (emitter.running) resource("ElectricCharge").consume(emitter.ec_rate, "emitter");
  }


  void process_harvester(Harvester harvester)
  {
    if (harvester.running)
    {
      simulated_recipe recipe = new simulated_recipe("harvester", true);
      if (harvester.ec_rate > double.Epsilon) recipe.input("ElectricCharge", harvester.ec_rate);
      recipe.output(harvester.resource, harvester.rate);
      recipes.Add(recipe);
    }
  }


  void process_laboratory(Laboratory lab)
  {
    // note: we are not checking if there is a scientist in the part
    if (lab.running)
    {
      resource("ElectricCharge").consume(lab.ec_rate, "laboratory");
    }
  }


  void process_antenna(Antenna antenna)
  {
    resource("ElectricCharge").consume(antenna.cost, "transmission");
  }


  void process_command(ModuleCommand command)
  {
    foreach(ModuleResource res in command.resHandler.inputResources)
    {
      resource(res.name).consume(res.rate, "command");
    }
  }


  void process_panel(ModuleDeployableSolarPanel panel, environment_analyzer env)
  {
    double generated = panel.resHandler.outputResources[0].rate * env.solar_flux / Sim.SolarFluxAtHome();
    resource("ElectricCharge").produce(generated, "solar panel");
  }


  void process_generator(ModuleGenerator generator, Part p)
  {
     // skip launch clamps, that include a generator
     if (Lib.PartName(p) == "launchClamp1") return;

     simulated_recipe recipe = new simulated_recipe("generator");
     foreach(ModuleResource res in generator.resHandler.inputResources)
     {
       recipe.input(res.name, res.rate);
     }
     foreach(ModuleResource res in generator.resHandler.outputResources)
     {
       recipe.output(res.name, res.rate);
     }
     recipes.Add(recipe);
  }


  void process_converter(ModuleResourceConverter converter)
  {
    simulated_recipe recipe = new simulated_recipe("converter");
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
    simulated_recipe recipe = new simulated_recipe("harvester");
    foreach(ResourceRatio res in harvester.inputList)
    {
      recipe.input(res.ResourceName, res.Ratio);
    }
    recipe.output(harvester.ResourceName, harvester.Efficiency);
    recipes.Add(recipe);
  }


  void process_stocklab(ModuleScienceConverter lab)
  {
    resource("ElectricCharge").consume(lab.powerRequirement, "lab");
  }


  void process_radiator(ModuleActiveRadiator radiator)
  {
    // note: IsCooling is not valid in the editor, for deployable radiators,
    // we will have to check if the related deploy module is deployed
    // we use PlannerController instead
    foreach(var res in radiator.resHandler.inputResources)
    {
      resource(res.name).consume(res.rate, "radiator");
    }
  }


  void process_wheel_motor(ModuleWheelMotor motor)
  {
    foreach(var res in motor.resHandler.inputResources)
    {
      resource(res.name).consume(res.rate, "wheel");
    }
  }


  void process_wheel_steering(ModuleWheelMotorSteering steering)
  {
    foreach(var res in steering.resHandler.inputResources)
    {
      resource(res.name).consume(res.rate, "wheel");
    }
  }


  void process_light(ModuleLight light)
  {
    if (light.useResources && light.isOn)
    {
      resource("ElectricCharge").consume(light.resourceAmount, "light");
    }
  }


  void process_scanner(PartModule m)
  {
    resource("ElectricCharge").consume(SCANsat.EcConsumption(m), "SCANsat");
  }


  void process_curved_panel(Part p, PartModule m, environment_analyzer env)
  {
    // note: assume half the components are in sunlight, and average inclination is half

    // get total rate
    double tot_rate = Lib.ReflectionValue<float>(m, "TotalEnergyRate");

    // get number of components
    int components = p.FindModelTransforms(Lib.ReflectionValue<string>(m, "PanelTransformName")).Length;

    // approximate output
    // 0.7071: average clamped cosine
    resource("ElectricCharge").produce(tot_rate * 0.7071 * env.solar_flux / Sim.SolarFluxAtHome(), "curved panel");
  }


  void process_fission_generator(Part p, PartModule m)
  {
    double max_rate = Lib.ReflectionValue<float>(m, "PowerGeneration");

    // get fission reactor tweakable, will default to 1.0 for other modules
    var reactor = p.FindModuleImplementing<ModuleResourceConverter>();
    double tweakable = reactor == null ? 1.0 : Lib.ReflectionValue<float>(reactor, "CurrentPowerPercent") * 0.01f;

    resource("ElectricCharge").produce(max_rate * tweakable, "fission generator");
  }


  void process_radioisotope_generator(Part p, PartModule m)
  {
    double max_rate = Lib.ReflectionValue<float>(m, "BasePower");

    resource("ElectricCharge").produce(max_rate, "radioisotope generator");
  }


  void process_cryotank(Part p, PartModule m)
  {
     // note: assume cooling is active
     double cooling_cost = Lib.ReflectionValue<float>(m, "CoolingCost");
     string fuel_name = Lib.ReflectionValue<string>(m, "FuelName");

     resource("ElectricCharge").consume(cooling_cost * Lib.Capacity(p, fuel_name) * 0.001, "cryotank");
  }


  Dictionary<string, simulated_resource> resources = new Dictionary<string, simulated_resource>();
  List<simulated_recipe> recipes = new List<simulated_recipe>();
}


public sealed class simulated_resource
{
  public simulated_resource()
  {
    consumers = new Dictionary<string, wrapper>();
    producers = new Dictionary<string, wrapper>();
    harvests = new List<string>();
  }

  public void consume(double quantity, string name)
  {
    if (quantity >= double.Epsilon)
    {
      amount -= quantity;
      consumed += quantity;

      if (!consumers.ContainsKey(name)) consumers.Add(name, new wrapper());
      consumers[name].value += quantity;
    }
  }

  public void produce(double quantity, string name)
  {
    if (quantity >= double.Epsilon)
    {
      amount += quantity;
      produced += quantity;

      if (!producers.ContainsKey(name)) producers.Add(name, new wrapper());
      producers[name].value += quantity;
    }
  }

  public void clamp()
  {
    amount = Lib.Clamp(amount, 0.0, capacity);
  }

  public double lifetime()
  {
    double rate = produced - consumed;
    return amount <= double.Epsilon ? 0.0 : rate > -1e-10 ? double.NaN : amount / -rate;
  }

  public string tooltip(bool invert=false)
  {
    var green = !invert ? producers : consumers;
    var red = !invert ? consumers : producers;

    var sb = new StringBuilder();
    foreach(var pair in green)
    {
      if (sb.Length > 0) sb.Append("\n");
      sb.Append("<b><color=#00ff00>");
      sb.Append(Lib.HumanReadableRate(pair.Value.value));
      sb.Append("</color></b>\t");
      sb.Append(pair.Key);
    }
    foreach(var pair in red)
    {
      if (sb.Length > 0) sb.Append("\n");
      sb.Append("<b><color=#ff0000>");
      sb.Append(Lib.HumanReadableRate(pair.Value.value));
      sb.Append("</color></b>\t");
      sb.Append(pair.Key);
    }
    if (harvests.Count > 0)
    {
      sb.Append("\n\n<b>Harvests</b>");
      foreach(string s in harvests)
      {
        sb.Append("\n");
        sb.Append(s);
      }
    }
    return Lib.BuildString("<align=left />", sb.ToString());
  }

  public double storage;                        // amount stored (at the start of simulation)
  public double capacity;                       // storage capacity
  public double amount;                         // amount stored (during simulation)
  public double consumed;                       // total consumption rate
  public double produced;                       // total production rate
  public List<string> harvests;                 // some extra data about harvests

  public class wrapper { public double value; }
  public Dictionary<string, wrapper> consumers; // consumers metadata
  public Dictionary<string, wrapper> producers; // producers metadata
}


public sealed class simulated_recipe
{
  public simulated_recipe(string name, bool dump = false)
  {
    this.name = name;
    this.inputs = new List<resource_recipe.entry>();
    this.outputs = new List<resource_recipe.entry>();
    this.dump = dump;
    this.left = 1.0;
  }

  // add an input to the recipe
  public void input(string resource_name, double quantity)
  {
    if (quantity > double.Epsilon) //< avoid division by zero
    {
      inputs.Add(new resource_recipe.entry(resource_name, quantity));
    }
  }

  // add an output to the recipe
  public void output(string resource_name, double quantity)
  {
    if (quantity > double.Epsilon) //< avoid division by zero
    {
      outputs.Add(new resource_recipe.entry(resource_name, quantity));
    }
  }

  // execute the recipe
  public bool execute(resource_simulator sim)
  {
    // determine worst input ratio
    double worst_input = left;
    if (outputs.Count > 0)
    {
      for(int i=0; i<inputs.Count; ++i)
      {
        var e = inputs[i];
        simulated_resource res = sim.resource(e.name);
        worst_input = Lib.Clamp(res.amount * e.inv_quantity, 0.0, worst_input);
      }
    }

    // determine worst output ratio
    double worst_output = left;
    if (inputs.Count > 0 && !dump) //< ignore if dumping overboard
    {
      for(int i=0; i<outputs.Count; ++i)
      {
        var e = outputs[i];
        simulated_resource res = sim.resource(e.name);
        worst_output = Lib.Clamp((res.capacity - res.amount) * e.inv_quantity, 0.0, worst_output);
      }
    }

    // determine worst-io
    double worst_io = Math.Min(worst_input, worst_output);

    // consume inputs
    for(int i=0; i<inputs.Count; ++i)
    {
      var e = inputs[i];
      simulated_resource res = sim.resource(e.name);
      res.consume(e.quantity * worst_io, name);
    }

    // produce outputs
    for(int i=0; i<outputs.Count; ++i)
    {
      var e = outputs[i];
      simulated_resource res = sim.resource(e.name);
      res.produce(e.quantity * worst_io, name);
    }

    // update amount left to execute
    left -= worst_io;

    // the recipe was executed, at least partially
    return worst_io > double.Epsilon;
  }

  // store inputs and outputs
  public string name;                         // name used for consumer/producer tooltip
  public List<resource_recipe.entry> inputs;  // set of input resources
  public List<resource_recipe.entry> outputs; // set of output resources
  public bool dump;                           // dump excess output if true
  public double left;                         // what proportion of the recipe is left to execute
}


} // KERBALISM
