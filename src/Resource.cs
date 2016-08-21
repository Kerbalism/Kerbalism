// ====================================================================================================================
// resource system
// ====================================================================================================================


using System;
using System.Collections.Generic;


namespace KERBALISM {


// store info about a resource in a vessel
public class resource_info
{
  public resource_info(Vessel v, string res_name)
  {
    // remember resource name
    resource_name = res_name;

    // get amount & capacity
    if (v.loaded)
    {
      foreach(Part p in v.Parts)
      {
        foreach(PartResource res in p.Resources)
        {
          if (res.flowState && res.resourceName == resource_name)
          {
            amount += res.amount;
            capacity += res.maxAmount;
          }
        }
      }
    }
    else
    {
      foreach(ProtoPartSnapshot pps in v.protoVessel.protoPartSnapshots)
      {
        foreach(ProtoPartResourceSnapshot pprs in pps.resources)
        {
          if (pprs.resourceName == resource_name && Lib.Parse.ToBool(pprs.resourceValues.GetValue("flowState")))
          {
            amount += Lib.Parse.ToDouble(pprs.resourceValues.GetValue("amount"));
            capacity += Lib.Parse.ToDouble(pprs.resourceValues.GetValue("maxAmount"));
          }
        }
      }
    }

    // calculate level
    level = capacity > double.Epsilon ? amount / capacity : 0.0;
  }

  // record a deferred production
  public void Produce(double quantity)
  {
    deferred += quantity;
  }

  // record a deferred consumption
  public void Consume(double quantity)
  {
    deferred -= quantity;
  }

  // synchronize amount from cache to vessel
  public void Sync(Vessel v, double elapsed_s)
  {
    // for loaded vessels
    if (v.loaded)
    {
      // syncronize the amount to the vessel
      v.rootPart.RequestResource(resource_name, -deferred, ResourceFlowMode.ALL_VESSEL);

      // get amount/capacity
      double new_amount = 0.0;
      capacity = 0.0;
      foreach(Part p in v.Parts)
      {
        foreach(PartResource r in p.Resources)
        {
          if (r.flowState && r.resourceName == resource_name)
          {
            new_amount += r.amount;
            capacity += r.maxAmount;
          }
        }
      }

      // calculate rate of change per-second
      // note: do not update rate during and immediately after warp blending
      // rationale: stock modules do not use our awesome resource system and
      // are subject to instabilities when time per-step change
      if (Kerbalism.warp_blending > 50) rate = (new_amount - amount) / elapsed_s;

      // update amount
      amount = new_amount;
    }
    // for unloaded vessels
    else
    {
      // if BackgroundProcessing was detected, we need to scan the parts and find
      // the current amount of resource, that may have changed in the meanwhile,
      // and add the difference with the amount stored at last update to deferred
      double ext = 0.0;
      if (Kerbalism.detected_mods.BackgroundProcessing)
      {
        foreach(ProtoPartSnapshot pps in v.protoVessel.protoPartSnapshots)
        {
          foreach(ProtoPartResourceSnapshot res in pps.resources)
          {
            if (res.resourceName == resource_name && Lib.Parse.ToBool(res.resourceValues.GetValue("flowState")))
            {
              ext += Lib.Parse.ToDouble(res.resourceValues.GetValue("amount"));
            }
          }
        }
        ext -= amount;
      }

      // apply all deferred requests
      amount = Lib.Clamp(amount + deferred + ext, 0.0, capacity);

      // calculate rate of change per-second
      rate = Lib.Clamp(deferred + ext, -amount, capacity - amount) / elapsed_s;

      // syncronize the amount to the vessel
      foreach(ProtoPartSnapshot pps in v.protoVessel.protoPartSnapshots)
      {
        foreach(ProtoPartResourceSnapshot res in pps.resources)
        {
          if (res.resourceName == resource_name && Lib.Parse.ToBool(res.resourceValues.GetValue("flowState")))
          {
            double pps_amount = Lib.Parse.ToDouble(res.resourceValues.GetValue("amount"));
            double pps_capacity = Lib.Parse.ToDouble(res.resourceValues.GetValue("maxAmount"));
            double new_amount = Lib.Clamp(pps_amount + deferred, 0.0, pps_capacity);
            res.resourceValues.SetValue("amount", new_amount.ToString());
            deferred -= new_amount - pps_amount;
            if (Math.Abs(deferred) < 0.0001) break;
          }
        }
      }
    }

    // recalculate level
    level = capacity > double.Epsilon ? amount / capacity : 0.0;

    // reset deferred consumption/production
    deferred = 0.0;
  }


  public string resource_name;        // associated resource name
  public double deferred;             // accumulate deferred requests
  public double amount;               // amount of resource
  public double capacity;             // storage capacity of resource
  public double level;                // amount vs capacity, or 0 if there is no capacity
  public double rate;                 // rate of change in amount per-second
}


public sealed class resource_recipe
{
  // hard-coded priorities
  public const int rule_priority = 0;
  public const int scrubber_priority = 1;
  public const int harvester_priority = 2;
  public const int converter_priority = 3;

  // ctor
  public resource_recipe(int priority)
  {
    this.priority = priority;
  }

  // add an input to the recipe
  public void Input(string resource_name, double quantity)
  {
    inputs[resource_name] = quantity;
  }

  // add an output to the recipe
  public void Output(string resource_name, double quantity)
  {
    outputs[resource_name] = quantity;
  }

  // execute the recipe
  public void Execute(Vessel v, vessel_resources resources)
  {
    // determine worst input ratio
    double worst_input = 1.0;
    foreach(var pair in inputs)
    {
      if (pair.Value > double.Epsilon) //< avoid division by zero
      {
        resource_info res = resources.Info(v, pair.Key);
        worst_input = Math.Min(worst_input, Math.Max(0.0, res.amount + res.deferred) / pair.Value);
      }
    }

    // consume inputs
    foreach(var pair in inputs)
    {
      resource_info res = resources.Info(v, pair.Key);
      res.Consume(pair.Value * worst_input);
    }

    // produce outputs
    foreach(var pair in outputs)
    {
      resource_info res = resources.Info(v, pair.Key);
      res.Produce(pair.Value * worst_input);
    }
  }

  // used to sort recipes by priority
  public static int Compare(resource_recipe a, resource_recipe b)
  {
    return a.priority < b.priority ? -1 : a.priority == b.priority ? 0 : 1;
  }

  // store inputs and outputs
  public Dictionary<string, double> inputs = new Dictionary<string, double>();
  public Dictionary<string, double> outputs = new Dictionary<string, double>();
  public int priority;
}



// the resource cache of a vessel
public sealed class vessel_resources
{
  // return a resource handler
  public resource_info Info(Vessel v, string resource_name)
  {
    // try to get existing entry if any
    resource_info res;
    if (resources.TryGetValue(resource_name, out res)) return res;

    // create new entry
    res = new resource_info(v, resource_name);

    // remember new entry
    resources.Add(resource_name, res);

    // return new entry
    return res;
  }

  // apply deferred requests for a vessel and synchronize the new amount in the vessel
  public void Sync(Vessel v, double elapsed_s)
  {
    // IDEA: priority heuristic
    // first pass: -outputs.size + inputs.size
    // second pass: += recipe.priority for all recipes producing one of the input

    // execute all recipes in order of priority
    recipes.Sort(resource_recipe.Compare);
    foreach(resource_recipe recipe in recipes) recipe.Execute(v, this);
    recipes.Clear();

    // apply all deferred requests and synchronize to vessel
    foreach(var pair in resources) pair.Value.Sync(v, elapsed_s);
  }

  // record deferred production of a resource (shortcut)
  public void Produce(Vessel v, string resource_name, double quantity)
  {
    Info(v, resource_name).Produce(quantity);
  }

  // record deferred consumption of a resource (shortcut)
  public void Consume(Vessel v, string resource_name, double quantity)
  {
    Info(v, resource_name).Consume(quantity);
  }

  // record deferred execution of a recipe
  public void Transform(resource_recipe recipe)
  {
    recipes.Add(recipe);
  }


  public Dictionary<string, resource_info> resources = new Dictionary<string, resource_info>(32);
  public List<resource_recipe> recipes = new List<resource_recipe>(4);
}


// manage per-vessel resource caches
public sealed class ResourceCache
{
  // ctor
  public ResourceCache()
  {
    // enable global access
    instance = this;
  }

  // return resource cache for a vessel
  public static vessel_resources Get(Vessel v)
  {
    // try to get existing entry if any
    vessel_resources entry;
    if (instance.entries.TryGetValue(v.id, out entry)) return entry;

    // create new entry
    entry = new vessel_resources();

    // remember new entry
    instance.entries.Add(v.id, entry);

    // return new entry
    return entry;
  }

  // return a resource handler (shortcut)
  public static resource_info Info(Vessel v, string resource_name)
  {
    return Get(v).Info(v, resource_name);
  }

  // remove a vessel from the resource cache
  public static void Purge(Guid vessel_id)
  {
    instance.entries.Remove(vessel_id);
  }

  // register deferred production of a resource (shortcut)
  public static void Produce(Vessel v, string resource_name, double quantity)
  {
    Info(v, resource_name).Produce(quantity);
  }

  // register deferred consumption of a resource (shortcut)
  public static void Consume(Vessel v, string resource_name, double quantity)
  {
    Info(v, resource_name).Consume(quantity);
  }

  // register deferred execution of a recipe (shortcut)
  public static void Transform(Vessel v, resource_recipe recipe)
  {
    Get(v).Transform(recipe);
  }


  // resource cache
  Dictionary<Guid, vessel_resources> entries = new Dictionary<Guid, vessel_resources>(512);

  // permit global access
  static ResourceCache instance;
}


} // KERBALISM

