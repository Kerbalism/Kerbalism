using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class Script
{
  public Script()
  {
    states = new Dictionary<uint, bool>();
    prev = string.Empty;
  }

  public Script(ConfigNode node)
  {
    states = new Dictionary<uint, bool>();
    foreach(string s in node.GetValues("state"))
    {
      var tokens = Lib.Tokenize(s, '@');
      if (tokens.Count < 2) continue;
      states.Add(Lib.Parse.ToUInt(tokens[0]), Lib.Parse.ToBool(tokens[1]));
    }
    prev = Lib.ConfigValue(node, "prev", string.Empty);
  }

  public void save(ConfigNode node)
  {
    foreach(var p in states)
    {
      node.AddValue("state", Lib.BuildString(p.Key.ToString(), "@", p.Value.ToString()));
    }
    node.AddValue("prev", prev);
  }

  public void set(Device dev, bool? state)
  {
    states.Remove(dev.id());
    if (state != null)
    {
      states.Add(dev.id(), state == true);
    }
  }

  public void execute(Dictionary<uint, Device> devices)
  {
    Device dev;
    foreach(var p in states)
    {
      if (devices.TryGetValue(p.Key, out dev))
      {
        dev.ctrl(p.Value);
      }
    }
  }


  public Dictionary<uint, bool> states;
  public string prev;
}



} // KERBALISM

