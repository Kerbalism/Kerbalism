// ====================================================================================================================
// implement life-support-like mechanics that can be influenced by environmental factors
// ====================================================================================================================


using System;
using System.Collections.Generic;


namespace KERBALISM {


public sealed class Rule
{
  public Rule(ConfigNode node)
  {
    // parse rule from config
    this.name = Lib.ConfigValue(node, "name", "");
    this.resource_name = Lib.ConfigValue(node, "resource_name", "");
    this.waste_name = Lib.ConfigValue(node, "waste_name", "");
    this.waste_ratio = Lib.ConfigValue(node, "waste_ratio", 1.0);
    this.rate = Lib.ConfigValue(node, "rate", 0.0);
    this.interval = Lib.ConfigValue(node, "interval", 0.0);
    this.degeneration = Lib.ConfigValue(node, "degeneration", 0.0);
    this.variance = Lib.ConfigValue(node, "variance", 0.0);
    this.modifier =  Lib.Tokenize(Lib.ConfigValue(node, "modifier", ""), ',');
    this.on_pod = Lib.ConfigValue(node, "on_pod", 0.0);
    this.on_eva = Lib.ConfigValue(node, "on_eva", 0.0);
    this.on_resque = Lib.ConfigValue(node, "on_resque", 999.0);
    this.waste_buffer = Lib.ConfigValue(node, "waste_buffer", 10.0);
    this.hidden_waste = Lib.ConfigValue(node, "hidden_waste", false);
    this.massless_waste = Lib.ConfigValue(node, "massless_waste", false);
    this.breakdown = Lib.ConfigValue(node, "breakdown", false);
    this.warning_threshold = Lib.ConfigValue(node, "warning_threshold", 0.33);
    this.danger_threshold = Lib.ConfigValue(node, "danger_threshold", 0.66);
    this.fatal_threshold = Lib.ConfigValue(node, "fatal_threshold", 1.0);
    this.warning_message = Lib.ConfigValue(node, "warning_message", "");
    this.danger_message = Lib.ConfigValue(node, "danger_message", "");
    this.fatal_message = Lib.ConfigValue(node, "fatal_message", "");
    this.relax_message = Lib.ConfigValue(node, "relax_message", "");
    this.low_threshold = Lib.ConfigValue(node, "low_threshold", 0.15);
    this.low_message = Lib.ConfigValue(node, "low_message", "");
    this.empty_message = Lib.ConfigValue(node, "empty_message", "");
    this.refill_message = Lib.ConfigValue(node, "refill_message", "");
  }


  // return seconds until depletion, 0.0 if there is no resource and NaN if it will never deplete
  public double Depletion(Vessel v, resource_info res)
  {
    // [unused] Newton–Raphson, 1 step
    // C = amount
    // R = delta
    // A = change in delta
    // t0 = C / -R              // eyeball estimate
    // F' = 0.5 * t0 * t0       // differential of F
    // F = C + R * t0 + A * F'  // equation of motion, constant acceleration
    // t1 = t0 - F / F'         // time until depletion

    // calculate rate of change from interval-based rule
    double meal_rate = interval > double.Epsilon ? rate / interval : 0.0;

    // calculate total rate of change
    double delta = res.rate - meal_rate * Lib.CrewCount(v);

    // return depletion
    return res.amount <= double.Epsilon ? 0.0 : delta >= -0.000001 ? double.NaN : res.amount / -delta;
  }


  // return per-kerbal variance, in the range [1-variance,1+variance]
  static double Variance(ProtoCrewMember c, double variance)
  {
    // get a value in [0..1] range associated with a kerbal
    double k = (double)Lib.Hash32(c.name.Replace(" Kerman", "")) / (double)UInt32.MaxValue;

    // move in [-1..+1] range
    k = k * 2.0 - 1.0;

    // return kerbal-specific variance in range [1-n .. 1+n]
    return 1.0 + variance * k;
  }


  public static void applyRules(Vessel v, vessel_info vi, vessel_data vd, vessel_resources resources, double elapsed_s)
  {
    // get crew
    List<ProtoCrewMember> crew = v.loaded ? v.GetVesselCrew() : v.protoVessel.GetVesselCrew();

    // get breathable modifier
    double breathable = vi.breathable ? 0.0 : 1.0;

    // get temp diff modifier
    double temp_diff = v.altitude < 2000.0 && v.mainBody == FlightGlobals.GetHomeBody() ? 0.0 : Sim.TempDiff(vi.temperature);

    // for each rule
    foreach(Rule r in Kerbalism.rules)
    {
      // get resource handler
      resource_info res = r.resource_name.Length > 0 ? resources.Info(v, r.resource_name) : null;

      // if a resource is specified
      if (res != null)
      {
        // get data from db
        vmon_data vmon = DB.VmonData(v.id, r.name);

        // message obey user config
        bool show_msg = (r.resource_name == "ElectricCharge" ? vd.cfg_ec > 0 : vd.cfg_supply > 0);

        // no messages with no capacity
        if (res.capacity > double.Epsilon)
        {
          uint variant = crew.Count > 0 ? 0 : 1u; //< manned/probe variant

          // manage messages
          if (res.level <= double.Epsilon && vmon.message < 2)
          {
            if (r.empty_message.Length > 0 && show_msg) Message.Post(Severity.danger, Lib.ExpandMsg(r.empty_message, v, null, variant));
            vmon.message = 2;
          }
          else if (res.level < r.low_threshold && vmon.message < 1)
          {
            if (r.low_message.Length > 0 && show_msg) Message.Post(Severity.warning, Lib.ExpandMsg(r.low_message, v, null, variant));
            vmon.message = 1;
          }
          else if (res.level > r.low_threshold && vmon.message > 0)
          {
            if (r.refill_message.Length > 0 && show_msg) Message.Post(Severity.relax, Lib.ExpandMsg(r.refill_message, v, null, variant));
            vmon.message = 0;
          }
        }
      }

      // for each crew
      foreach(ProtoCrewMember c in crew)
      {
        // get kerbal data
        kerbal_data kd = DB.KerbalData(c.name);

        // skip resque kerbals
        if (kd.resque == 1) continue;

        // skip disabled kerbals
        if (kd.disabled == 1) continue;

        // get supply data from db
        kmon_data kmon = DB.KmonData(c.name, r.name);


        // get product of all environment modifiers
        double k = 1.0;
        foreach(string modifier in r.modifier)
        {
          switch(modifier)
          {
            case "breathable":  k *= breathable;                              break;
            case "temperature": k *= temp_diff;                               break;
            case "radiation":   k *= vi.env_radiation * (1.0 - kd.shielding); break;
            case "qol":         k /= QualityOfLife.Bonus(kd.living_space, kd.entertainment, vi.landed, vi.link.linked, vi.crew_count == 1); break;
          }
        }


        // if continuous
        double step;
        if (r.interval <= double.Epsilon)
        {
          // influence consumption by elapsed time
          step = elapsed_s;
        }
        // if interval-based
        else
        {
          // accumulate time
          kmon.time_since += elapsed_s;

          // determine number of steps
          step = Math.Floor(kmon.time_since / r.interval);

          // consume time
          kmon.time_since -= step * r.interval;
        }


        // if continuous, or if one or more intervals elapsed
        if (step > double.Epsilon)
        {
          // indicate if we must degenerate
          bool must_degenerate = true;

          // if there is a resource specified, and this isn't just a monitoring rule
          if (res != null && r.rate > double.Epsilon)
          {
            // determine amount of resource to consume
            double required = r.rate          // rate per-second or per interval
                            * k               // product of environment modifiers
                            * step;           // seconds elapsed or by number of steps

            // if there is no waste
            if (r.waste_name.Length == 0)
            {
              // simply consume (that is faster)
              res.Consume(required);

            }
            // if there is waste
            else
            {
              // transform resource into waste
              resource_recipe recipe = new resource_recipe(resource_recipe.rule_priority);
              recipe.Input(r.resource_name, required);
              recipe.Output(r.waste_name, required * r.waste_ratio);
              resources.Transform(recipe);
            }

            // reset degeneration when consumed, or when not required at all
            // note: evaluating amount from previous simulation step
            if (required <= double.Epsilon || res.amount > double.Epsilon)
            {
              // slowly recover instead of instant reset
              kmon.problem *= 1.0 / (1.0 + Math.Max(r.interval, 1.0) * step * 0.002);
              kmon.problem = Math.Max(kmon.problem, 0.0);

              // do not degenerate
              must_degenerate = false;
            }
          }

          // degenerate if this rule is resource-less, or if there was not enough resource in the vessel
          if (must_degenerate)
          {
            kmon.problem += r.degeneration            // degeneration rate per-second or per-interval
                          * k                         // product of environment modifiers
                          * step                      // seconds elapsed or by number of steps
                          * Variance(c, r.variance);  // kerbal-specific variance
          }


          // determine message variant
          uint variant = vi.temperature < Settings.SurvivalTemperature ? 0 : 1u;

          // kill kerbal if necessary
          if (kmon.problem >= r.fatal_threshold)
          {
            if (r.fatal_message.Length > 0)
              Message.Post(r.breakdown ? Severity.breakdown : Severity.fatality, Lib.ExpandMsg(r.fatal_message, v, c, variant));

            if (r.breakdown)
            {
              Kerbalism.Breakdown(v, c);
              kmon.problem = r.danger_threshold * 1.01; //< move back to danger threshold
            }
            else
            {
              Kerbalism.Kill(v, c);
            }
          }
          // show messages
          else if (kmon.problem >= r.danger_threshold && kmon.message < 2)
          {
            if (r.danger_message.Length > 0) Message.Post(Severity.danger, Lib.ExpandMsg(r.danger_message, v, c, variant));
            kmon.message = 2;
          }
          else if (kmon.problem >= r.warning_threshold && kmon.message < 1)
          {
            if (r.warning_message.Length > 0) Message.Post(Severity.warning, Lib.ExpandMsg(r.warning_message, v, c, variant));
            kmon.message = 1;
          }
          else if (kmon.problem < r.warning_threshold && kmon.message > 0)
          {
            if (r.relax_message.Length > 0) Message.Post(Severity.relax, Lib.ExpandMsg(r.relax_message, v, c, variant));
            kmon.message = 0;
          }
        }
      }
    }
  }



  public string name;                               // rule name
  public string resource_name;                      // name of resource consumed, if any
  public string waste_name;                         // name of another resource produced from the one consumed, if any
  public double waste_ratio;                        // conversion ratio from resource to waste
  public double rate;                               // rate of resource consumption per-second (if interval is 0) or per-interval (if interval is more than 0)
  public double interval;                           // if 0, the resource is consumed constantly - if more than 0, the resource is consumed every 'interval' seconds
  public double degeneration;                       // rate to add to the accumulator per-second (if interval is 0) or per-interval (if iterval is more than 0)
  public double variance;                           // variance for degeneration, unique per-kerbal and in range [1.0-variance, 1.0+variance]
  public List<string> modifier = new List<string>();// if specified, consumption and degeneration are influenced by a parameter from the environment
  public double on_pod;                             // how much resource to add to manned parts, per-kerbal
  public double on_eva;                             // how much resource to take on eva, if any
  public double on_resque;                          // how much resource to gift to resque missions
  public double waste_buffer;                       // how many days worth of waste capacity to add to pods
  public bool   hidden_waste;                       // if true, hide the waste resource on pods, if false visibility is determined by resource definition
  public bool   massless_waste;                     // if true, set waste to be massless, if false density is determined by resource definition
  public bool   breakdown;                          // if true, trigger a breakdown instead of killing the kerbal
  public double warning_threshold;                  // threshold of degeneration used to show warning messages and yellow status color
  public double danger_threshold;                   // threshold of degeneration used to show danger messages and red status color
  public double fatal_threshold;                    // threshold of degeneration used to show fatal messages and kill/breakdown the kerbal
  public string warning_message;                    // messages shown on degeneration threshold crossings
  public string danger_message;                     // .
  public string fatal_message;                      // .
  public string relax_message;                      // .
  public double low_threshold;                      // threshold of resource level used to show low messages and yellow status color
  public string low_message;                        // messages shown on resource level threshold crossings
  public string empty_message;                      // .
  public string refill_message;                     // .
}


// store rule data per-kerbal, serialized
public class kmon_data
{
  public double problem = 0.0;                      // accumulator for the rule
  public uint   message = 0;                        // used to avoid sending messages multiple times
  public double time_since = 0.0;                   // time since last meal, if ls.interval > 0
}


// store vessel monitor data, serialized
public class vmon_data
{
  public uint message = 0;                          // used to avoid sending messages multiple times
}


} // KERBALISM