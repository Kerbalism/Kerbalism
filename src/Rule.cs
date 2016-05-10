// ====================================================================================================================
// implement life support mechanics for a set of arbitrary resources
// ====================================================================================================================


using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;


namespace KERBALISM {


public class Rule
{
  // parse rule from config
  public Rule(ConfigNode node)
  {
    this.name = Lib.ConfigValue(node, "name", "");
    this.resource_name = Lib.ConfigValue(node, "resource_name", "");
    this.waste_name = Lib.ConfigValue(node, "waste_name", "");
    this.waste_ratio = Lib.ConfigValue(node, "waste_ratio", 1.0);
    this.rate = Lib.ConfigValue(node, "rate", 0.0);
    this.interval = Lib.ConfigValue(node, "interval", 0.0);
    this.degeneration = Lib.ConfigValue(node, "degeneration", 0.0);
    this.variance = Lib.ConfigValue(node, "variance", 0.0);
    this.modifier = Lib.ConfigValue(node, "modifier", "");
    this.on_pod = Lib.ConfigValue(node, "on_pod", 0.0);
    this.on_eva = Lib.ConfigValue(node, "on_eva", 0.0);
    this.on_resque = Lib.ConfigValue(node, "on_resque", 999.0);
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

  // estimate lifetime for the rule resource
  // note: this function must be called only once for simulation step
  // return 0 if no resource left, NaN if rate of change is positive, or seconds left in other cases
  public double EstimateLifetime(Vessel v)
  {
    // get prev amount for the vessel
    double prev_amount = 0.0;
    if (!prev_amounts.TryGetValue(v.id, out prev_amount)) prev_amount = 0.0;

    // calculate delta
    double amount = Lib.GetResourceAmount(v, this.resource_name);
    double meal_rate = this.interval > double.Epsilon ? this.rate / this.interval : 0.0;
    double delta = (amount - prev_amount) / TimeWarp.fixedDeltaTime - meal_rate * Lib.CrewCount(v);

    // remember prev amount
    prev_amounts[v.id] = amount;

    // return lifetime in seconds
    return amount <= double.Epsilon ? 0.0 : delta >= -double.Epsilon ? double.NaN : amount / -delta;
  }


  public string name;                               // rule name
  public string resource_name;                      // name of resource consumed, if any
  public string waste_name;                         // name of another resource produced from the one consumed, if any
  public double waste_ratio;                        // conversion ratio from resource to waste
  public double rate;                               // rate of resource consumption per-second (if interval is 0) or per-interval (if interval is more than 0)
  public double interval;                           // if 0, the resource is consumed constantly - if more than 0, the resource is consumed every 'interval' seconds
  public double degeneration;                       // rate to add to the accumulator per-second (if interval is 0) or per-interval (if iterval is more than 0)
  public double variance;                           // variance for degeneration, unique per-kerbal and in range [1.0-variance, 1.0+variance]
  public string modifier;                           // if specified, consumption and degeneration are influenced by a parameter from the environment
  public double on_pod;                             // how much resource to add to manned parts, per-kerbal
  public double on_eva;                             // how much resource to take on eva, if any
  public double on_resque;                          // how much resource to gift to resque missions
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
  Dictionary<Guid, double> prev_amounts = new Dictionary<Guid, double>(); // used to keep track of depletion time
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


// store vessel monitor cache
public class vmon_cache
{
  public double depletion;                          // seconds until depletion, or 0 if never deplete
  public double level;                              // percentual of capacity filled
}



} // KERBALISM