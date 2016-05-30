// ====================================================================================================================
// implement life support mechanics for a set of arbitrary resources
// ====================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


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


  // calculate depletion time for the specified vessel
  // note: need to be called once per simulation step
  public void CalculateDepletion(Vessel v)
  {
    // get 32bit vessel id
    UInt32 id = Lib.VesselID(v);

    // create depletion info the first time this function is called for a particular vessel
    if (!depletions.ContainsKey(id)) depletions.Add(id, new depletion_info());

    // get depletion info entry
    depletion_info di = depletions[id];

    // get resource amount
    double amount = Cache.ResourceInfo(v, this.resource_name).amount;

    // calculate delta
    double meal_rate = this.interval > double.Epsilon ? this.rate / this.interval : 0.0;
    double delta = (amount - di.prev_amount) / TimeWarp.fixedDeltaTime - meal_rate * Lib.CrewCount(v);

    // remember prev amount
    di.prev_amount = amount;

    // return lifetime in seconds
    di.depletion = amount <= double.Epsilon ? 0.0 : delta >= -double.Epsilon ? double.NaN : amount / -delta;
  }


  // return depletion time for the specified vessel
  public double Depletion(Vessel v)
  {
    depletion_info di;
    return depletions.TryGetValue(Lib.VesselID(v), out di) ? di.depletion : 0.0;
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

  // store data for depletion estimate
  class depletion_info
  {
    public double depletion;
    public double prev_amount;
  }
  Dictionary<UInt32, depletion_info> depletions = new Dictionary<UInt32, depletion_info>();
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