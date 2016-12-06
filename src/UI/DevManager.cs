using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;


namespace KERBALISM {


public sealed class DevManager : Window
{
  public DevManager()
  : base(260u, 80u, 80u, 20u, Styles.win)
  {
    // enable global access
    instance = this;
  }

  public override bool prepare()
  {
    // if there is a vessel id specified
    if (vessel_id != Guid.Empty)
    {
      // try to get the vessel
      Vessel v = FlightGlobals.Vessels.Find(k => k.id == vessel_id);

      // if the vessel doesn't exist, forget it
      if (v == null)
      {
        vessel_id = Guid.Empty;
      }
      // if the vessel is not valid, forget it
      else if (!Cache.VesselInfo(v).is_valid)
      {
        vessel_id = Guid.Empty;
      }
      // if the vessel is valid
      else
      {
        // simulate the vessel computer not responding
        resource_info ec = ResourceCache.Info(v, "ElectricCharge");
        vessel_info vi = Cache.VesselInfo(v);
        timed_out = ec.amount <= double.Epsilon || (vi.crew_count == 0 && !vi.connection.linked);

        // get list of devices
        devices = !timed_out ? Computer.boot(v) : new Dictionary<uint, Device>();
      }
    }

    // if there is no vessel selected, don't draw anything
    return vessel_id != Guid.Empty;
  }


  // draw the window
  public override void render()
  {
    // get vessel
    // - the id and the vessel are valid at this point
    Vessel v = FlightGlobals.Vessels.Find(k => k.id == vessel_id);

    // draw pseudo-title
    if (Panel.title(v.vesselName)) Close();

    // direct control
    if (script_index == 0)
    {
      // draw section title and desc
      Panel.section("DEVICES", ref script_index, (int)ScriptType.last);
      Panel.description(description());

      // for each device
      foreach(var p in devices)
      {
        // render device entry
        Device dev = p.Value;
        Panel.content(dev.name(), dev.info(), dev.toggle);

        // highlight part
        if (Lib.IsHover())
        {
          Highlighter.set(dev.part(), Color.cyan);
        }
      }
    }
    // script editor
    else
    {
      // get script
      ScriptType script_type = (ScriptType)script_index;
      string script_name = script_type.ToString().Replace('_', ' ').ToUpper();
      Script script = DB.Vessel(v).computer.get(script_type);

        // draw section title and desc
      Panel.section(script_name, ref script_index, (int)ScriptType.last);
      Panel.description(description());

      // for each device
      foreach(var p in devices)
      {
        // determine tribool state
        int state = !script.states.ContainsKey(p.Key)
          ? -1
          : !script.states[p.Key]
          ? 0
          : 1;

        // render device entry
        Device dev = p.Value;
        Panel.content
        (
          dev.name(),
          state == -1 ? "<color=#999999>don't care</color>" : state == 0 ? "<color=red>off</color>" : "<color=cyan>on</color>",
          () =>
          {
            switch(state)
            {
              case -1: script.set(dev, true);  break;
              case  0: script.set(dev, null);  break;
              case  1: script.set(dev, false); break;
            }
          }
        );

        // highlight part
        if (Lib.IsHover())
        {
          Highlighter.set(dev.part(), Color.cyan);
        }
      }
    }

    // no devices case
    if (devices.Count == 0 && !timed_out)
    {
      Panel.content("<i>no devices</i>", string.Empty);
    }

    // draw spacing
    Panel.space();
  }

  public override float height()
  {
    // get vessel
    // - the id and the vessel are valid at this point, checked in on_gui()
    Vessel v = FlightGlobals.Vessels.Find(k => k.id == vessel_id);

    // store computed height
    float h = 20.0f;

    // if it is responding
    if (!timed_out)
    {
      // devices
      h += Panel.height(Math.Max(devices.Count, 1));
    }

    // desc
    h += Panel.description_height(description());

    // finally, return the height
    return h;
  }

  // return short description of a script, or the time-out message
  public string description()
  {
    if (timed_out)                  return "<i>Connection timed out</i>";
    if (script_index == 0)          return "<i>Control vessel components directly</i>";
    switch((ScriptType)script_index)
    {
      case ScriptType.landed:       return "<i>Called on landing</i>";
      case ScriptType.atmo:         return "<i>Called on entering atmosphere</i>";
      case ScriptType.space:        return "<i>Called on reaching space</i>";
      case ScriptType.sunlight:     return "<i>Called when sun became visible</i>";
      case ScriptType.shadow:       return "<i>Called when sun became occluded</i>";
      case ScriptType.power_high:   return "<i>Called when EC level goes above 15%</i>";
      case ScriptType.power_low:    return "<i>Called when EC level goes below 15%</i>";
      case ScriptType.rad_high:     return "<i>Called when radiation exceed 0.05 rad/h</i>";
      case ScriptType.rad_low:      return "<i>Called when radiation goes below 0.05 rad/h</i>";
      case ScriptType.linked:       return "<i>Called when signal is regained</i>";
      case ScriptType.unlinked:     return "<i>Called when signal is lost</i>";
      case ScriptType.eva_out:      return "<i>Called when going out on EVA</i>";
      case ScriptType.eva_in:       return "<i>Called when returning from EVA</i>";
      case ScriptType.action1:      return "<i>Called by pressing <b>1</b> on the keyboard</i>";
      case ScriptType.action2:      return "<i>Called by pressing <b>2</b> on the keyboard</i>";
      case ScriptType.action3:      return "<i>Called by pressing <b>3</b> on the keyboard</i>";
      case ScriptType.action4:      return "<i>Called by pressing <b>4</b> on the keyboard</i>";
      case ScriptType.action5:      return "<i>Called by pressing <b>5</b> on the keyboard</i>";
    }
    return string.Empty;
  }

  // show the window
  public static void Open(Vessel v)
  {
    // setting vessel id show the window
    instance.vessel_id = v.id;
  }

  // close the window
  public static void Close()
  {
    // resetting vessel id hide the window
    instance.vessel_id = Guid.Empty;
  }

  // toggle the window
  public static void Toggle(Vessel v)
  {
    // if vessel is different, show it
    // if vessel is the same, hide it
    instance.vessel_id = (instance.vessel_id == v.id ? Guid.Empty : v.id);
  }

  // return true if the window is open
  public static bool IsOpen()
  {
    return instance.vessel_id != Guid.Empty;
  }

  // selected vessel, if any
  Guid vessel_id;

  // mode/script index
  int script_index;

  // set of devices
  Dictionary<uint, Device> devices;

  // true if there is no ec on the vessel, or if it unmanned and without a link
  bool timed_out;

  // permit global access
  static DevManager instance;
}


} // KERBALISM

