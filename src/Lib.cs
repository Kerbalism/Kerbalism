// ====================================================================================================================
// collection of utility functions
// ====================================================================================================================


using System;
using System.Collections.Generic;
using System.Net.Sockets;
using KSPAchievements;
using LibNoise.Unity.Operator;
using UnityEngine;


namespace KERBALISM {


public static class Lib
{
  // clamp a value
  public static double Clamp(double value, double min, double max)
  {
    return Math.Max(min, Math.Min(value, max));
  }

  // clamp a value
  public static float Clamp(float value, float min, float max)
  {
    return Math.Max(min, Math.Min(value, max));
  }

  // blend between two values
  public static double Mix(double a, double b, double k)
  {
    return a * (1.0 - k) + b * k;
  }

  // return string limited to len, with ... at the end
  public static string Epsilon(string s, uint len)
  {
    len = Math.Max(len, 3u);
    return s.Length <= len ? s : s.Substring(0, (int)len - 3) + "...";
  }

  // return amount of a resource in a part
  public static double GetResourceAmount(Part part, string resource_name)
  {
    foreach(PartResource res in part.Resources)
    {
      if (res.flowState && res.resourceName == resource_name) return res.amount;
    }
    return 0.0;
  }

  // return amount of a resource in a proto part
  public static double GetResourceAmount(ProtoPartSnapshot pps, string resource_name)
  {
    foreach(ProtoPartResourceSnapshot pprs in pps.resources)
    {
      if (pprs.resourceName == resource_name && Convert.ToBoolean(pprs.resourceValues.GetValue("flowState")))
        return Convert.ToDouble(pprs.resourceValues.GetValue("amount"));
    }
    return 0.0;
  }

  // return amount of a resource in a vessel
  public static double GetResourceAmount(Vessel vessel, string resource_name)
  {
    double amount = 0.0;
    if (vessel.loaded)
    {
      foreach(Part part in vessel.Parts)
      {
        amount += GetResourceAmount(part, resource_name);
      }
    }
    else
    {
      foreach(ProtoPartSnapshot pps in vessel.protoVessel.protoPartSnapshots)
      {
        amount += GetResourceAmount(pps, resource_name);
      }
    }
    return amount;
  }


  // return capacity of a resource in a part
  public static double GetResourceCapacity(Part part, string resource_name)
  {
    foreach(PartResource res in part.Resources)
    {
      if (res.flowState && res.resourceName == resource_name) return res.maxAmount;
    }
    return 0.0;
  }

  // return capacity of a resource in a proto part
  public static double GetResourceCapacity(ProtoPartSnapshot pps, string resource_name)
  {
    foreach(ProtoPartResourceSnapshot pprs in pps.resources)
    {
      if (pprs.resourceName == resource_name && Convert.ToBoolean(pprs.resourceValues.GetValue("flowState")))
        return Convert.ToDouble(pprs.resourceValues.GetValue("maxAmount"));
    }
    return 0.0;
  }

  // return capacity of a resource in a vessel
  public static double GetResourceCapacity(Vessel vessel, string resource_name)
  {
    double max_amount = 0.0;
    if (vessel.loaded)
    {
      foreach(Part part in vessel.Parts)
      {
        max_amount += GetResourceCapacity(part, resource_name);
      }
    }
    else
    {
      foreach(ProtoPartSnapshot pps in vessel.protoVessel.protoPartSnapshots)
      {
        max_amount += GetResourceCapacity(pps, resource_name);
      }
    }
    return max_amount;
  }

  // return proto part from a vessel
  public static ProtoPartSnapshot GetProtoPart(Vessel vessel, uint partFlightID)
  {
    foreach(ProtoPartSnapshot pps in vessel.protoVessel.protoPartSnapshots)
    {
      if (pps.flightID == partFlightID)
      {
        return pps;
      }
    }
    return null;
  }

  // return proto module from a vessel
  public static ProtoPartModuleSnapshot GetProtoModule(Vessel vessel, uint partFlightID, string module_name)
  {
    ProtoPartSnapshot pps = GetProtoPart(vessel, partFlightID);
    foreach(ProtoPartModuleSnapshot ppms in pps.modules)
    {
      if (ppms.moduleName == module_name) return ppms;
    }
    return null;
  }

  // return a value from a proto module
  public static T GetProtoValue<T>(ProtoPartModuleSnapshot module, string value_name)
  {
    return (T)Convert.ChangeType(module.moduleValues.GetValue(value_name), typeof(T));
  }

  // set a value in a proto module
  public static void SetProtoValue<T>(ProtoPartModuleSnapshot module, string value_name, T value)
  {
    module.moduleValues.SetValue(value_name, value.ToString(), true);
  }

  // get list of parts recursively, useful from the editors
  public static List<Part> GetPartsRecursively(Part root)
  {
    List<Part> ret = new List<Part>();
    ret.Add(root);
    foreach(Part p in root.children)
    {
      ret.AddRange(GetPartsRecursively(p));
    }
    return ret;
  }

  // create a new resource in a part
  public static void SetupResource(Part part, string resource_name, double amount, double max_amount)
  {
    PartResource res = part.gameObject.AddComponent<PartResource>();
    res.SetInfo(PartResourceLibrary.Instance.resourceDefinitions[resource_name]);
    res.amount = amount;
    res.maxAmount = max_amount;
    res.flowMode = PartResource.FlowMode.Both;
    res.flowState = true;
    res.part = part;
    part.Resources.list.Add(res);
  }

  // pretty-print a resource rate
  // - rate: rate per second, must be positive
  public static string HumanReadableRate(double rate)
  {
    if (rate <= double.Epsilon) return "none";
    if (rate >= 0.01) return rate.ToString("F2") + "/s";
    rate *= 60.0; // per-minute
    if (rate >= 0.01) return rate.ToString("F2") + "/m";
    rate *= 60.0; // per-hour
    if (rate >= 0.01) return rate.ToString("F2") + "/h";
    rate *= HoursInDay();  // per-day
    if (rate >= 0.01) return rate.ToString("F2") + "/d";
    return (rate * DaysInYear()).ToString("F2") + "/y";
  }

  // pretty-print a duration
  // - duration: duration in seconds, must be positive
  public static string HumanReadableDuration(double duration)
  {
    if (duration <= double.Epsilon) return "none";
    if (double.IsInfinity(duration) || double.IsNaN(duration)) return "perpetual";

    double hours_in_day = HoursInDay();
    double days_in_year = DaysInYear();

    // seconds
    if (duration < 60.0) return duration.ToString("F0") + "s";

    // minutes + seconds
    double duration_min = Math.Floor(duration / 60.0);
    duration -= duration_min * 60.0;
    if (duration_min < 60.0) return duration_min.ToString("F0") + "m" + (duration < 1.0 ? "" : " " + duration.ToString("F0") + "s");

    // hours + minutes
    double duration_h = Math.Floor(duration_min / 60.0);
    duration_min -= duration_h * 60.0;
    if (duration_h < hours_in_day) return duration_h.ToString("F0") + "h" + (duration_min < 1.0 ? "" : " " + duration_min.ToString("F0") + "m");

    // days + hours
    double duration_d = Math.Floor(duration_h / hours_in_day);
    duration_h -= duration_d * hours_in_day;
    if (duration_d < days_in_year) return duration_d.ToString("F0") + "d" + (duration_h < 1.0 ? "" : " " + duration_h.ToString("F0") + "h");

    // years + days
    double duration_y = Math.Floor(duration_d / days_in_year);
    duration_d -= duration_y * days_in_year;
    return duration_y.ToString("F0") + "y" + (duration_d < 1.0 ? "" : " " + duration_d.ToString("F0") + "d");
  }


  // pretty-print a range
  // - range: range in meters, must be positive
  public static string HumanReadableRange(double range)
  {
    if (range <= double.Epsilon) return "none";
    if (range < 1000.0) return range.ToString("F1") + " m";
    range /= 1000.0;
    if (range < 1000.0) return range.ToString("F1") + " Km";
    range /= 1000.0;
    if (range < 1000.0) return range.ToString("F1") + " Mm";
    range /= 1000.0;
    return range.ToString("F1") + " Gm";
  }


  // pretty-print temperature
  public static string HumanReadableTemp(double temp)
  {
    return temp.ToString("F0") + "K";
  }


  // format a value, or return 'none'
  public static string ValueOrNone(double value, string append = "")
  {
    return (Math.Abs(value) <= double.Epsilon ? "none" : value.ToString("F2") + append);
  }


  // return hours in a day
  public static double HoursInDay()
  {
    return GameSettings.KERBIN_TIME ? 6.0 : 24.0;
  }


  // return year length
  public static double DaysInYear()
  {
    if (!FlightGlobals.ready) return 426.0;
    return Math.Floor(FlightGlobals.GetHomeBody().orbit.period / (HoursInDay() * 60.0 * 60.0));
  }


  // stop time warping
  // TODO: test if in 1.1 is now safe to stop warp all at once
  public static void StopWarp()
  {
    // note: instant switching from 10^n speed to 1 speed changes orbit!!!
    if (TimeWarp.CurrentRateIndex > 4) TimeWarp.SetRate(4, true);
    if (TimeWarp.CurrentRateIndex > 0) TimeWarp.SetRate(0, false);
  }


  // combine two guid, irregardless of their order (eg: Combine(a,b) == Combine(b,a))
  public static Guid CombineGuid(Guid a, Guid b)
  {
    byte[] a_buf = a.ToByteArray();
    byte[] b_buf = b.ToByteArray();
    byte[] c_buf = new byte[16];
    for(int i=0; i<16; ++i) c_buf[i] = (byte)(a_buf[i] ^ b_buf[i]);
    return new Guid(c_buf);
  }


  // combine two guid, in a non-commutative way
  public static Guid OrderedCombineGuid(Guid a, Guid b)
  {
    byte[] a_buf = a.ToByteArray();
    byte[] b_buf = b.ToByteArray();
    byte[] c_buf = new byte[16];
    for(int i=0; i<16; ++i) c_buf[i] = (byte)(a_buf[i] & ~b_buf[i]);
    return new Guid(c_buf);
  }


  // return true if landed somewhere
  public static bool Landed(Vessel vessel)
  {
    if (vessel.loaded) return vessel.Landed || vessel.Splashed;
    else return vessel.protoVessel.landed || vessel.protoVessel.splashed;
  }


  // return vessel position
  public static Vector3d VesselPosition(Vessel vessel)
  {
    // if simulated (or landed, or orbit is invalid - read below)
    if (vessel.loaded || Landed(vessel) || double.IsNaN(vessel.orbit.inclination))
    {
      return vessel.GetWorldPos3D();
    }
    // if not simulated
    else
    {
      // GetWorldPos3D work when not simulated, with an exception:
      //   when orbiting the sun, and switching from flight to space center scene,
      //   for a single tick the position returned is the same as the sun!
      // so we resolve the orbit directly, that is reliable in all cases
      // note: landed vessels have non-valid orbits
      // note: we don't use the orbit if invalid, for whatever reason
      return vessel.orbit.getPositionAtUT(Planetarium.GetUniversalTime());
    }
  }


  // store the random number generator
  static System.Random rng = new System.Random((int)DateTime.Now.Ticks);


  // return random integer
  public static int RandomInt(int max_value)
  {
    return rng.Next(max_value);
  }


  // return random double [0..1]
  public static double RandomDouble()
  {
    return rng.NextDouble();
  }


  // return true if the current scene is not the menus nor the editors
  public static bool SceneIsGame()
  {
    return HighLogic.LoadedScene == GameScenes.FLIGHT || HighLogic.LoadedScene == GameScenes.SPACECENTER || HighLogic.LoadedScene == GameScenes.TRACKSTATION;
  }


  // return crew count of a vessel, even if not loaded
  public static int CrewCount(Vessel v)
  {
    return v.isEVA ? 1 : v.loaded ? v.GetCrewCount() : v.protoVessel.GetVesselCrew().Count;
  }


  // return crew capacity of a vessel, even if not loaded
  public static int CrewCapacity(Vessel v)
  {
    if (v.isEVA) return 1;
    if (v.loaded) return v.GetCrewCapacity();
    else
    {
      int capacity = 0;
      foreach(ProtoPartSnapshot part in v.protoVessel.protoPartSnapshots)
      {
        capacity += Convert.ToInt32(part.partInfo.partConfig.GetValue("CrewCapacity"));
      }
      return capacity;
    }
  }


  // return a value from a module using reflection
  // note: useful when the module is from another assembly, unknown at build time
  // note: useful when the value isn't persistent
  public static T ReflectionValue<T>(PartModule m, string value_name)
  {
    return (T)m.GetType().GetField(value_name).GetValue(m);
  }


  // set a value from a module using reflection
  // note: useful when the module is from another assembly, unknown at build time
  // note: useful when the value isn't persistent
  public static void ReflectionValue<T>(PartModule m, string value_name, T value)
  {
    m.GetType().GetField(value_name).SetValue(m, value);
  }


  // return true if game is paused
  public static bool IsPaused()
  {
    return FlightDriver.Pause || Planetarium.Pause;
  }


  // produce or consume a resource on a vessel, irregardless if loaded or not
  public static double RequestResource(Vessel vessel, string resource_name, double quantity)
  {
    if (vessel.loaded)
    {
      return vessel.rootPart.RequestResource(resource_name, quantity, ResourceFlowMode.ALL_VESSEL_BALANCE);
    }
    else
    {
      double diff = quantity;
      double amount = 0.0;
      double capacity = 0.0;
      foreach(ProtoPartSnapshot part in vessel.protoVessel.protoPartSnapshots)
      {
        foreach(ProtoPartResourceSnapshot res in part.resources)
        {
          if (res.resourceName == resource_name && Convert.ToBoolean(res.resourceValues.GetValue("flowState")))
          {
            amount = Convert.ToDouble(res.resourceValues.GetValue("amount"));
            capacity = Convert.ToDouble(res.resourceValues.GetValue("maxAmount"));
            double new_amount = Lib.Clamp(amount - diff, 0.0, capacity);
            res.resourceValues.SetValue("amount", new_amount.ToString());
            diff -= amount - new_amount;
            if (Math.Abs(diff) <= double.Epsilon) return quantity;
          }
        }
      }
      return quantity - diff;
    }
  }


  // return true if there is experiment data on the vessel
  public static bool HasData(Vessel v)
  {
    // if vessel is loaded
    if (v.loaded)
    {
      // iterate over all science containers/experiments
      foreach(IScienceDataContainer m in v.FindPartModulesImplementing<IScienceDataContainer>())
      {
        // if there is data, return true
        if (m.GetData().Length > 0) return true;
      }
    }
    // if not loaded
    else
    {
      // iterate over all science containers/experiments proto modules
      foreach(ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
      {
        foreach(ProtoPartModuleSnapshot m in p.modules)
        {
          if (m.moduleName == "ModuleScienceContainer" || m.moduleName == "ModuleScienceExperiment")
          {
            // if there is data, return true
            if (m.moduleValues.GetNodes("ScienceData").Length > 0) return true;
          }
        }
      }
    }

    // there was no data
    return false;
  }


  // remove one experiment at random from the vessel
  public static void RemoveData(Vessel v)
  {
    // if vessel is loaded
    if (v.loaded)
    {
      // iterate over all science containers/experiments
      foreach(IScienceDataContainer m in v.FindPartModulesImplementing<IScienceDataContainer>())
      {
        // if there is data
        ScienceData[] data = m.GetData();
        if (data.Length > 0)
        {
          // remove data at random
          ScienceData random_data = data[Lib.RandomInt(data.Length)];
          m.DumpData(random_data);
          return;
        }
      }
    }
    // if not loaded
    else
    {
      // iterate over all science containers/experiments proto modules
      foreach(ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
      {
        foreach(ProtoPartModuleSnapshot m in p.modules)
        {
          if (m.moduleName == "ModuleScienceContainer" || m.moduleName == "ModuleScienceExperiment")
          {
            // if there is data
            ConfigNode[] data = m.moduleValues.GetNodes("ScienceData");
            if (data.Length > 0)
            {
              // remove data at random
              ConfigNode random_data = data[Lib.RandomInt(data.Length)];
              m.moduleValues.RemoveNode(random_data);
              return;
            }
          }
        }
      }
    }
  }


  // return true if this is a 'vessel'
  public static bool IsVessel(Vessel v)
  {
    // if the vessel is in DEAD status, we consider it invalid
    if (v.state == Vessel.State.DEAD) return false;

    // when going to eva (and possibly other occasions), for a single update the vessel is not properly set
    // this can be detected by vessel.distanceToSun being 0 (an impossibility otherwise)
    // in this case, just wait a tick for the data being set by the game engine
    if (v.loaded && v.distanceToSun <= double.Epsilon) return false;

    // if the vessel is a debris, a flag or an asteroid, ignore it
    // note: the user can change vessel type, in that case he is actually disabling this mod for the vessel
    // the alternative is to scan the vessel for ModuleCommand, but that is slower, and resque vessels have no module command
    if (v.vesselType == VesselType.Debris || v.vesselType == VesselType.Flag || v.vesselType == VesselType.SpaceObject) return false;

    // the vessel is valid
    return true;
  }


  // return true if the vessel is a resque mission
  public static bool IsResqueMission(Vessel v)
  {
    // if db isn't ready, assume a resque mission
    if (!DB.Ready()) return true;

    // avoid re-creating dead eva kerbals in the db
    // note: no extra cost if vessel is not eva
    if (EVA.IsDead(v)) return true;

    // if at least one of the crew is flagged as resque, consider it a resque mission
    var crew_members = v.loaded ? v.GetVesselCrew() : v.protoVessel.GetVesselCrew();
    foreach(var c in crew_members)
    {
      kerbal_data kd = DB.KerbalData(c.name);
      if (kd.resque == 1) return true;
    }

    // not a resque mission
    return false;
  }


  // return true if last GUIlayout element was clicked
  public static bool IsClicked(int button=0)
  {
    return Event.current.type == EventType.MouseDown
        && Event.current.button == 0
        && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition);
  }


  // return reference body of the planetary system that contain the specified body
  public static CelestialBody PlanetarySystem(CelestialBody body)
  {
    if (body.flightGlobalsIndex == 0) return body;
    return body.referenceBody.flightGlobalsIndex == 0 ? body : body.referenceBody;
  }


  // get a texture
  public static Texture2D GetTexture(string name)
  {
    return GameDatabase.Instance.GetTexture("Kerbalism/Textures/" + name, false);
  }


  // get 32bit FNV-1a hash of a string
  public static UInt32 Hash32(string s)
  {
    // offset basis
    UInt32 h = 2166136261u;

    // for each byte of the buffer
    for(int i=0; i < s.Length; ++i)
    {
      // xor the bottom with the current octet
      h ^= Convert.ToUInt32(s[i]);

      // equivalent to h *= 16777619 (FNV magic prime mod 2^32)
      h += (h << 1) + (h << 4) + (h << 7) + (h << 8) + (h << 24);
    }

    //return the hash
    return h;
  }


  // return number of techs researched among the list specified
  public static int CountTechs(string[] techs)
  {
    int n = 0;
    foreach(string tech in techs) n += ResearchAndDevelopment.GetTechnologyState(tech) == RDTech.State.Available ? 1 : 0;
    return n;
  }


  // write a message to the log
  public static void Log(string msg)
  {
    MonoBehaviour.print("[Kerbalism] " + msg);
  }


  // render a textfield with placeholder
  // - id: an unique name for the textfield
  // - text: the previous textfield content
  // - placeholder: the text to show if the content is empty
  // - style: GUIStyle to use for the textfield
  public static string TextFieldPlaceholder(string id, string text, string placeholder, GUIStyle style)
  {
    GUI.SetNextControlName(id);
    text = GUILayout.TextField(text, style);

    if (Event.current.type == EventType.Repaint)
    {
      if (GUI.GetNameOfFocusedControl() == id)
      {
        if (text == placeholder) text = "";
      }
      else
      {
        if (text.Length == 0) text = placeholder;
      }
    }
    return text;
  }


  // get a set of values from the config system
  public static ConfigNode ParseConfig(string path)
  {
    ConfigNode node = GameDatabase.Instance.GetConfigNode(path);
    return node != null ? node : new ConfigNode();
  }


  // get a value from config
  public static T ConfigValue<T>(ConfigNode cfg, string key, T def_value)
  {
    return cfg.HasValue(key) ? (T)Convert.ChangeType(cfg.GetValue(key), typeof(T)) : def_value;
  }
}


} // KERBALISM

