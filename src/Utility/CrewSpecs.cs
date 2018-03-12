using System;
using System.Collections.Generic;


namespace KERBALISM {


public sealed class CrewSpecs
{
  public CrewSpecs(string value)
  {
    // if empty or false: not enabled
    if (value.Length == 0 || value.ToLower() == "false")
    {
      trait = string.Empty;
      level = 0;
      enabled = false;
    }
    // if true: enabled, any trait
    else if (value.ToLower() == "true")
    {
      trait = string.Empty;
      level = 0;
      enabled = true;
    }
    // all other cases: enabled, specified trait and experience
    else
    {
      var tokens = Lib.Tokenize(value, '@');
      trait = tokens.Count > 0 ? tokens[0] : string.Empty;
      level = tokens.Count > 1 ? Lib.Parse.ToUInt(tokens[1]) : 0;
      enabled = true;
    }
  }

  // return true if the crew of active vessel satisfy the specs
  public bool check()
  {
    Vessel v = FlightGlobals.ActiveVessel;
    return v != null && check(v);
  }

  // return true if the crew of specified vessel satisfy the specs
  public bool check(Vessel v)
  {
    return check(Lib.CrewList(v));
  }

  // return true if the specified crew satisfy the specs
  public bool check(List<ProtoCrewMember> crew)
  {
    for(int i=0; i < crew.Count; ++i)
    {
      if (check(crew[i])) return true;
    }
    return false;
  }

  // return true if the specified crew member satisfy the specs
  public bool check(ProtoCrewMember c)
  {
    return trait.Length == 0 || (c.trait == trait && c.experienceLevel >= level);
  }

  // generate a string for use in warning messages
  public string warning()
  {
    return Lib.BuildString
    (
      "<b>",
      (trait.Length == 0 ? "Crew" : trait),
      "</b> ",
      (level == 0 ? string.Empty : "of level <b>" + level + "</b> "),
      "is required"
    );
  }

  // generate a string for use in part tooltip
  public string info()
  {
    if (!enabled) return "no";
    else if (trait.Length == 0) return "anyone";
    else return Lib.BuildString(trait, (level == 0 ? string.Empty : " (level: " + level + ")"));
  }

  // can check if enabled by bool comparison
  public static implicit operator bool(CrewSpecs ct)
  {
    return ct.enabled;
  }

  public string trait;    // trait specified, or empty for any trait
  public uint   level;    // experience level specified
  public bool   enabled;  // can also specify 'disabled' state
}


} // KERBALISM

