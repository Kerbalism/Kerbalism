// ====================================================================================================================
// a simple computer to automate operations on vessel components, and to manage files
// ====================================================================================================================


using System;
using System.Collections.Generic;
using System.Text;
using LibNoise.Unity.Operator;
using UnityEngine;


namespace KERBALISM {


public sealed class Computer
{
  static string[] default_files = new string[]
  {
    "doc/notes",
    "auto/situation_landed",
    "auto/situation_atmo",
    "auto/situation_space",
    "auto/sun_visible",
    "auto/sun_occluded",
    "auto/power_nominal",
    "auto/power_low",
    "auto/radiation_nominal",
    "auto/radiation_high",
    "auto/signal_linked",
    "auto/signal_unlinked",
    "auto/eva_out",
    "auto/eva_in"
  };

  // create a new computer
  public Computer()
  {
    // create default files
    this.files = new Dictionary<string, File>(32);
    foreach(string s in default_files) this.files.Add(s, new File());

    // create initial state
    prev_situation = Vessel.Situations.PRELAUNCH;
    prev_sunlight = true;
    prev_power = true;
    prev_radiation = true;
    prev_signal = true;
  }


  // de-serialize a computer
  public Computer(ConfigNode node)
  {
    // load files
    files = new Dictionary<string, File>(32);
    foreach(var file_node in node.GetNodes())
    {
      files.Add(file_node.name, new File(file_node));
    }

    // add default files, that may have been removed by the empty-file-optimization
    foreach(string s in default_files)
    {
      if (!files.ContainsKey(s)) files.Add(s, new File());
    }

    // load previous state
    prev_situation = (Vessel.Situations)Enum.Parse(typeof(Vessel.Situations), node.GetValue("prev_situation"));
    prev_sunlight = node.GetValue("prev_sunlight") == "1";
    prev_power = node.GetValue("prev_power") == "1";
    prev_radiation = node.GetValue("prev_radiation") == "1";
    prev_signal = node.GetValue("prev_signal") == "1";
  }


  // serialize a computer
  public void save(ConfigNode node)
  {
    // save files
    foreach(var pair in files)
    {
      // skip device files, and empty-file-optimization
      if (pair.Value.device == null && pair.Value.content.Length > 0)
      {
        ConfigNode file_node = node.AddNode(pair.Key);
        pair.Value.save(file_node);
      }
    }

    // save previous state
    node.AddValue("prev_situation", prev_situation.ToString());
    node.AddValue("prev_sunlight", prev_sunlight ? "1" : "0");
    node.AddValue("prev_power", prev_power ? "1" : "0");
    node.AddValue("prev_radiation", prev_radiation ? "1" : "0");
    node.AddValue("prev_signal", prev_signal ? "1" : "0");
  }


  // call automation scripts and transfer data
  public void update(Vessel environment, double elapsed_s)
  {
    // do nothing if there is no EC left on the vessel
    resource_info ec = ResourceCache.Info(environment, "ElectricCharge");
    if (ec.amount <= double.Epsilon)
    {
      return;
    }

    // get current states
    vessel_info vi = Cache.VesselInfo(environment);
    Vessel.Situations situation = environment.situation;
    bool sunlight = vi.sunlight > double.Epsilon;
    bool power = ec.level >= 0.15; //< 15%
    bool radiation = vi.radiation <= 0.00001388; //< 0.05 rad/h
    bool signal = vi.link.linked;

    // check for state changes and call scripts
    if (situation != prev_situation)
    {
      switch(situation)
      {
        case Vessel.Situations.LANDED:
        case Vessel.Situations.SPLASHED:
          execute("run", "auto/situation_landed", string.Empty, environment);
          break;

        case Vessel.Situations.FLYING:
          execute("run", "auto/situation_atmo", string.Empty, environment);
          break;

        case Vessel.Situations.ORBITING:
        case Vessel.Situations.SUB_ORBITAL:
        case Vessel.Situations.ESCAPING:
          execute("run", "auto/situation_space", string.Empty, environment);
          break;
      }
    }

    if (sunlight != prev_sunlight)
    {
      execute("run", sunlight ? "auto/sun_visible" : "auto/sun_occluded", string.Empty, environment);
    }

    if (power != prev_power)
    {
      execute("run", power ? "auto/power_nominal" : "auto/power_low", string.Empty, environment);
    }

    if (radiation != prev_radiation)
    {
      execute("run", radiation ? "auto/radiation_nominal" : "auto/radiation_high", string.Empty, environment);
    }

    if (signal != prev_signal)
    {
      execute("run", signal ? "auto/signal_linked" : "auto/signal_unlinked", string.Empty, environment);
    }

    // remember previous state
    prev_situation = situation;
    prev_sunlight = sunlight;
    prev_power = power;
    prev_signal = signal;
    prev_radiation = radiation;


    // create network devices
    boot_network(environment);

    // transmit all files flagged for transmission to their destination
    List<string> to_remove = new List<string>();
    foreach(var pair in files)
    {
      // if file must be sent
      string filename = pair.Key;
      File file = pair.Value;
      if (file.send.Length > 0)
      {
        // get target
        File target;
        if (files.TryGetValue(file.send, out target))
        {
          // target is alive, get it
          NetworkDevice nd = target.device as NetworkDevice;
          Computer remote_machine = DB.VesselData(nd.id()).computer;

          // zero-data files are transmitted instantly
          if (file.data <= double.Epsilon)
          {
            // add file to remote machine, overwrite as necessary
            if (remote_machine.files.ContainsKey(filename))
            {
              remote_machine.files.Remove(filename);
            }
            remote_machine.files.Add(filename, new File(file.content));

            // flag local file for removal
            to_remove.Add(filename);
          }
          // science data files are transmitted over time
          else
          {
            // TODO: send science data file
            // remember: subtract amount from file.value until it is zero, then remove the file if reach zero
          }
        }
        else
        {
          // the target doesn't exist, don't send but keep it in case it comes alive later
        }
      }
    }

    // delete local files flagged for removal
    foreach(string s in to_remove) files.Remove(s);

    // delete network devices
    cleanup();

  }


  // execute a command string on the computer
  public void execute(string cmd, Vessel environment, bool remote = false)
  {
    // parse commandline
    char[] chars = cmd.ToCharArray();
    bool inSingleQuote = false;
    bool inDoubleQuote = false;
    for (int index = 0; index < chars.Length; ++index)
    {
      if (chars[index] == '"' && !inSingleQuote)
      {
        inDoubleQuote = !inDoubleQuote;
        chars[index] = '\n';
      }
      if (chars[index] == '\'' && !inDoubleQuote)
      {
        inSingleQuote = !inSingleQuote;
        chars[index] = '\n';
      }
      if (!inSingleQuote && !inDoubleQuote && chars[index] == ' ')
      {
        chars[index] = '\n';
      }
    }
    var args = (new string(chars)).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

    // do nothing with no arguments
    if (args.Length == 0) return;

    // execute command
    execute
    (
      args[0],
      args.Length > 1 ? args[1] : string.Empty,
      args.Length > 2 ? args[2] : string.Empty,
      environment,
      remote
    );
  }


  // execute a command on the computer
  public void execute(string cmd, string arg0, string arg1, Vessel environment, bool remote = false)
  {
    // do nothing if cmd is empty
    if (cmd.Length == 0) return;

    // check if there is EC on the vessel
    if (ResourceCache.Info(environment, "ElectricCharge").amount <= double.Epsilon)
    {
      error("no electric charge");
      return;
    }

    // check if there is signal in case the command is remote
    vessel_info vi = Cache.VesselInfo(environment);
    if (remote && !vi.link.linked && vi.crew_count == 0)
    {
      error("no signal");
      return;
    }

    // add 'device files' from all the drivers
    if (environment != null)
    {
      boot_crew(environment);
      boot_network(environment);
      boot_devices(environment);
    }

    // execute the command
    switch(cmd)
    {
      case "list":    list(arg0);               break;
      case "info":    info(arg0);               break;
      case "ctrl":    ctrl(arg0, arg1);         break;
      case "run":     /* do nothing */          break;
      case "send":    send(arg0, arg1);         break;
      case "copy":    copy(arg0, arg1);         break;
      case "move":    move(arg0, arg1);         break;
      case "del":     del(arg0);                break;
      case "switch":  switch_(arg0);            break;
      case "edit":    edit(arg0);               break;
      case "log":     log(arg0);                break;
      case "msg":     msg(arg0, arg1);          break;
      case "help":    help(arg0);               break;
      case "clear":   clear();                  break;
      case "exit":    exit();                   break;
      default:        error("unknown command"); break;
    }

    // remove all device files
    cleanup();

    // execute script in a clear environment
    if (cmd == "run") run(arg0, environment);
  }


  // record science data in the computer
  public void record(string subject_id, double amount)
  {
    string filename = Lib.BuildString("data/", subject_id);
    File file;
    if (!files.TryGetValue(filename, out file))
    {
      file = new File();
      file.content = "Some result description here";
      file.data = amount;
      files.Add(filename, file);
    }
    else
    {
      file.data += amount;
    }
  }


  // ### COMMANDS #############################################################


  // command: list {directory}
  void list(string dir)
  {
    StringBuilder sb = new StringBuilder(64);
    if (dir.Length == 0)
    {
      HashSet<string> directories = new HashSet<string>();
      foreach(var pair in files)
      {
        directories.Add(pair.Key.Split('/')[0]);
      }
      List<string> sorted_directories = new List<string>();
      sorted_directories.AddRange(directories);
      sorted_directories.Sort();
      foreach(string s in sorted_directories)
      {
        if (sb.Length > 0) sb.Append("\n");
        sb.Append("<color=cyan>");
        sb.Append(s);
        sb.Append("</color>");
      }
    }
    else
    {
      List<string> sorted_files = new List<string>();
      foreach(var pair in files)
      {
        var path = pair.Key.Split('/');
        if (path[0] == dir)
        {
          sorted_files.Add(pair.Key);
        }
      }
      sorted_files.Sort();
      foreach(string s in sorted_files)
      {
        if (sb.Length > 0) sb.Append("\n");
        sb.Append("<color=cyan>");
        sb.Append(s);
        sb.Append("</color>");
        File file = files[s];
        if (file.send.Length > 0)
        {
          sb.Append("#sending to ");
          sb.Append(file.send);
        }
      }
    }
    success(sb.ToString());
  }


  // command: info [#]
  void info(string filename)
  {
    // check argument
    if (filename.Length == 0)
    {
      error("no file specified");
      return;
    }

    // determine path
    var path = filename.Split('/');

    // single file
    if (path.Length == 2)
    {
      File file;
      if (!files.TryGetValue(filename, out file))
      {
        error("file not found");
      }
      else if (file.device == null)
      {
        error("not a device");
      }
      else
      {
        success(file.device.info());
      }
    }
    // directory
    else if (path.Length == 1)
    {
      StringBuilder sb = new StringBuilder(64);
      List<string> sorted_files = new List<string>();
      foreach(var p in files)
      {
        if (p.Key.Split('/')[0] == path[0] && p.Value.device != null)
        {
          sorted_files.Add(p.Key);
        }
      }
      sorted_files.Sort();
      foreach(string s in sorted_files)
      {
        if (sb.Length > 0) sb.Append("\n");
        sb.Append(s);
        sb.Append("#");
        sb.Append(files[s].device.info());
      }
      success(sb.ToString());
    }
    // malformed path
    else
    {
      error("invalid filename");
    }
  }

  // command: ctrl [#] [value]
  void ctrl(string filename, string value)
  {
    // check arguments
    if (filename.Length == 0)
    {
      error("no file specified");
      return;
    }
    if (value.Length == 0)
    {
      error("no value specified");
      return;
    }
    double v;
    value = value.Replace("on", "1");
    value = value.Replace("off", "0");
    if (!double.TryParse(value, out v))
    {
      error("not a valid value");
      return;
    }

    // determine path
    var path = filename.Split('/');

    // single file
    if (path.Length == 2)
    {
      File file;
      if (!files.TryGetValue(filename, out file))
      {
        error("file not found");
      }
      else if (file.device == null)
      {
        error("not a device");
      }
      else
      {
        file.device.ctrl(v);
        success();
      }
    }
    // directory
    else if (path.Length == 1)
    {
      foreach(var p in files)
      {
        if (p.Key.Split('/')[0] == path[0] && p.Value.device != null)
        {
          p.Value.device.ctrl(v);
        }
      }
      success();
    }
    // malformed path
    else
    {
      error("invalid filename");
    }
  }

  // command: run [#]
  void run(string filename, Vessel environment)
  {
    File file;
    if (filename.Length == 0)
    {
      error("no file specified");
    }
    else if (!files.TryGetValue(filename, out file))
    {
      error("file not found");
    }
    else
    {
      // set sane status now, to deal with the case where the file is empty,
      // or each line is either empty or a comment
      success();

      // for each individual line
      var lines = file.content.Split('\n');
      for(int i = 0; i < lines.Length; ++i)
      {
        // strip spaces
        string line = lines[i].Trim();

        // skip empty lines
        if (line.Length == 0) continue;

        // skip comments
        if (line.IndexOf("//", StringComparison.Ordinal) == 0) continue;

        // remove comments on the right
        int right_comment = line.IndexOf("//", StringComparison.Ordinal);
        if (right_comment >= 0) line = line.Substring(0, right_comment);

        // execute the line
        execute(line, environment);

        // if this line failed, stop execution and add some info to the error msg
        // note: it may be desiderable for a script to keep running in case a statement fail
        //       maybe we can add a 'pragma' to control this, at the top of the script file
        if (!status)
        {
          output = Lib.BuildString("script execution failed at line ", i.ToString(), ":\n  ", output);
          return;
        }
      }
    }
  }

  // command: send [#] [#]
  void send(string filename, string devname)
  {
    // check arguments
    if (filename.Length == 0)
    {
      error("no file specified");
      return;
    }
    if (devname.Length > 0)
    {
      File dev;
      if (!files.TryGetValue(devname, out dev))
      {
        error("target not found");
        return;
      }
      else if (dev.device == null)
      {
        error("target is not a device");
        return;
      }
      else if (!(dev.device is NetworkDevice))
      {
        error("target is not a network device");
      }
    }

    // determine path
    var path = filename.Split('/');

    // single file
    if (path.Length == 2)
    {
      File file;
      if (!files.TryGetValue(filename, out file))
      {
        error("file not found");
      }
      else if (file.device != null)
      {
        error("file is a device");
      }
      else
      {
        file.send = devname;
        success();
      }
    }
    // directory
    else if (path.Length == 1)
    {
      foreach(var p in files)
      {
        if (p.Key.Split('/')[0] == path[0] && p.Value.device == null)
        {
          p.Value.send = devname;
        }
      }
      success();
    }
    // malformed path
    else
    {
      error("invalid filename");
    }
  }

  // command: copy [#] [#]
  void copy(string src_filename, string dst_filename)
  {
    // check arguments
    if (src_filename.Length == 0)
    {
      error("no source specified");
      return;
    }
    if (dst_filename.Length == 0)
    {
      error("no destination specified");
      return;
    }

    // determine path
    var src_path = src_filename.Split('/');
    var dst_path = dst_filename.Split('/');

    // we can copy a directory into another, or a file into another, but not the other variants
    if (src_path.Length != dst_path.Length)
    {
      error("can only copy file to file, or directory to directory");
      return;
    }

    // single file
    if (src_path.Length == 2)
    {
      File src;
      if (!files.TryGetValue(src_filename, out src))
      {
        error("file not found");
      }
      else if (src.device != null)
      {
        error("can't copy a device");
      }
      else
      {
        files.Add(dst_filename, new File(src.content));
        success();
      }
    }
    // directory
    else if (src_path.Length == 1)
    {
      Dictionary<string,File> to_add = new Dictionary<string, File>();
      foreach(var p in files)
      {
        if (p.Key.Split('/')[0] == src_path[0] && p.Value.device == null)
        {
          to_add.Add(Lib.BuildString(dst_path[0], "/", p.Key.Split('/')[1]), new File(p.Value.content));
        }
      }
      foreach(var p in to_add) files.Add(p.Key, p.Value);
      success();
    }
    // malformed path
    else
    {
      error("invalid filename");
    }
  }

  // command: move [#] [#]
  void move(string src_filename, string dst_filename)
  {
    // check arguments
    if (src_filename.Length == 0)
    {
      error("no source specified");
      return;
    }
    if (dst_filename.Length == 0)
    {
      error("no destination specified");
      return;
    }

    // determine path
    var src_path = src_filename.Split('/');
    var dst_path = dst_filename.Split('/');

    // we can move a directory into another, or a file into another, but not the other variants
    if (src_path.Length != dst_path.Length)
    {
      error("can only move file to file, or directory to directory");
      return;
    }

    // single file
    if (src_path.Length == 2)
    {
      File src;
      if (!files.TryGetValue(src_filename, out src))
      {
        error("file not found");
      }
      else if (src.device != null)
      {
        error("can't move a device");
      }
      else
      {
        files.Remove(src_filename);
        files.Add(dst_filename, src);
        success();
      }
    }
    // directory
    else if (src_path.Length == 1)
    {
      List<string> to_remove = new List<string>();
      Dictionary<string,File> to_add = new Dictionary<string, File>();
      foreach(var p in files)
      {
        if (p.Key.Split('/')[0] == src_path[0] && p.Value.device == null)
        {
          to_remove.Add(p.Key);
          to_add.Add(Lib.BuildString(dst_path[0], "/", p.Key.Split('/')[1]), p.Value);
        }
      }
      foreach(string s in to_remove) files.Remove(s);
      foreach(var p in to_add) files.Add(p.Key, p.Value);
      success();
    }
    // malformed path
    else
    {
      error("invalid filename");
    }
  }

  // command: del [#]
  void del(string filename)
  {
    // check arguments
    if (filename.Length == 0)
    {
      error("no file specified");
      return;
    }

    // determine path
    var path = filename.Split('/');

    // single file
    if (path.Length == 2)
    {
      File file;
      if (!files.TryGetValue(filename, out file))
      {
        error("file not found");
      }
      else if (file.device != null)
      {
        error("can't delete a device");
      }
      else
      {
        files.Remove(filename);
        success();
      }
    }
    // directory
    else if (path.Length == 1)
    {
      List<string> to_remove = new List<string>();
      foreach(var p in files)
      {
        if (p.Key.Split('/')[0] == path[0] && p.Value.device == null)
        {
          to_remove.Add(p.Key);
        }
      }
      foreach(string s in to_remove) files.Remove(s);
      success();
    }
    // malformed
    else
    {
      error("invalid filename");
    }
  }

  // command: log [txt]
  void log(string txt)
  {
    if (txt.Length == 0)
    {
      error("no content specified");
    }
    else
    {
      File file;
      if (!files.TryGetValue("doc/log", out file))
      {
        file = new File();
        files.Add("doc/log", file);
      }
      file.content = Lib.BuildString(file.content, Lib.PlanetariumTimestamp(), " ", txt, "\n");
      success();
    }
  }

  // command: msg [txt]
  void msg(string txt, string subtxt)
  {
    if (txt.Length == 0)
    {
      error("no content specified");
    }
    else
    {
      if (subtxt.Length == 0) Message.Post(txt);
      else Message.Post(txt, subtxt);
      success();
    }
  }


  // command: help
  void help(string cmd)
  {
    StringBuilder sb = new StringBuilder(64);
    sb.Append("\n");

    if (cmd.Length == 0)
    {
      sb.Append("  <b>list</b>\t{dir}\n");
      sb.Append("  <b>info</b>\t[file|dir]\n");
      sb.Append("  <b>ctrl</b>\t[file|dir] [value]\n");
      sb.Append("  <b>run</b>\t[file]\n");
      sb.Append("  <b>send</b>\t[file|dir] {target}\n");
      sb.Append("  <b>copy</b>\t[file|dir] [file|dir]\n");
      sb.Append("  <b>move</b>\t[file|dir] [file|dir]\n");
      sb.Append("  <b>del</b>\t[file|dir]\n");
      sb.Append("  <b>switch</b>\t[target]\n");
      sb.Append("  <b>edit</b>\t[file]\n");
      sb.Append("  <b>log</b>\t[text]\n");
      sb.Append("  <b>msg</b>\t[text] {subtext}\n");
      sb.Append("  <b>help</b>\t[command]\n");
      sb.Append("  <b>clear</b>\n");
      sb.Append("  <b>exit</b>");
    }
    else
    {
      switch(cmd)
      {
        case "list":
          sb.Append("<b>list</b> {dir}\n");
          sb.Append("<i>show a list of files or directories</i>");
          break;

        case "info":
          sb.Append("<b>info</b> [file|dir]\n");
          sb.Append("<i>query a device for status</i>");
          break;

        case "ctrl":
          sb.Append("<b>ctrl</b> [file|dir] [value]\n");
          sb.Append("<i>set status in a device</i>");
          break;

        case "run":
          sb.Append("<b>run</b> [file]\n");
          sb.Append("<i>execute a text file as a script</i>");
          break;

        case "send":
          sb.Append("<b>send</b> [file|dir] {target}\n");
          sb.Append("<i>flag files for transmission</i>");
          break;

        case "copy":
          sb.Append("<b>copy</b> [file|dir] [file|dir]\n");
          sb.Append("<i>copy a file</i>");
          break;

        case "move":
          sb.Append("<b>move</b> [file|dir] [file|dir]\n");
          sb.Append("<i>move or rename a file</i>");
          break;

        case "del":
          sb.Append("<b>del</b> [file|dir]\n");
          sb.Append("<i>delete files or directories</i>");
          break;

        case "log":
          sb.Append("<b>log</b> [text]\n");
          sb.Append("<i>append a timestamped line to doc/log</i>");
          break;

        case "msg":
          sb.Append("<b>msg</b> [text] {subtext}\n");
          sb.Append("<i>show a message on screen</i>");
          break;

        case "switch":
          sb.Append("<b>switch</b> [target]\n");
          sb.Append("<i>move the console to another machine</i>");
          break;

        case "edit":
          sb.Append("<b>edit</b> [file]\n");
          sb.Append("<i>edit the content of a file</i>");
          break;

        case "help":
          sb.Append("<i>contact a doctor</i>");
          break;

        case "clear":
          sb.Append("<b>clear</b>\n");
          sb.Append("<i>clear the console buffer</i>");
          break;

        case "exit":
          sb.Append("<b>exit</b>\n");
          sb.Append("<i>close the session</i>");
          break;
      }
    }
    success(sb.ToString());
  }


  void switch_(string filename)
  {
    File file;
    if (filename.Length == 0)
    {
      error("no file specified");
    }
    else if (!files.TryGetValue(filename, out file))
    {
      error("file not found");
    }
    else if (file.device == null)
    {
      error("not a device");
    }
    else if (!(file.device is NetworkDevice))
    {
      error("not a network device");
    }
    else
    {
      NetworkDevice nd = file.device as NetworkDevice;
      success(Lib.BuildString("!SWITCH ", nd.id().ToString()));
    }
  }


  void edit(string filename)
  {
    if (filename.Length == 0)
    {
      error("no file specified");
      return;
    }

    File file;
    if (!files.TryGetValue(filename, out file))
    {
      file = new File();
      files.Add(filename, file);
    }

    if (file.device != null)
    {
      error("file is a device");
    }
    else
    {
      success(Lib.BuildString("!EDIT ", filename));
    }
  }


  void clear()
  {
    success("!CLEAR");
  }


  void exit()
  {
    success("!EXIT");
  }


  void success()
  {
    output = string.Empty;
    status = true;
  }


  void success(string txt)
  {
    output = txt;
    status = true;
  }


  void error(string txt)
  {
    output = txt;
    status = false;
  }


  // ### BOOT #################################################################


  void boot_crew(Vessel v)
  {
    string filename;
    Device device;
    List<ProtoCrewMember> crew = v.loaded ? v.GetVesselCrew() : v.protoVessel.GetVesselCrew();
    foreach(ProtoCrewMember c in crew)
    {
      filename = Lib.BuildString("crew/", c.name.Replace(" Kerman", string.Empty));
      device = new CrewDevice(c);
      files.Add(filename, new File(device));
    }
  }


  void boot_network(Vessel v)
  {
    vessel_info vi = Cache.VesselInfo(v);
    if (!vi.is_valid) return;
    if (!vi.link.linked) return;

    // [disabled]
    //files.Add("net/home", new File(new NetworkDevice(null)));

    foreach(Vessel w in FlightGlobals.Vessels)
    {
      if (v == w) continue;
      if (w.isEVA) continue;
      vessel_info wi = Cache.VesselInfo(w);
      if (wi.is_valid && wi.link.linked)
      {
        files.Add(Lib.BuildString("net/", w.vesselName), new File(new NetworkDevice(w)));
      }
    }
  }


  void boot_devices(Vessel v)
  {
    // store stuff
    string filename;
    Device device;

    // index counters
    int scrubber_i = 0;
    int recycler_i = 0;
    int greenhouse_i = 0;
    int ring_i = 0;
    int emitter_i = 0;
    int light_i = 0;
    int panel_i = 0;
    int generator_i = 0;
    int converter_i = 0;
    int drill_i = 0;

    // loaded vessel
    if (v.loaded)
    {
      foreach(PartModule m in v.FindPartModulesImplementing<PartModule>())
      {
        switch(m.moduleName)
        {
          case "Scrubber":
            filename = Lib.BuildString("scrubber/", scrubber_i++.ToString());
            device = new ScrubberDevice(m as Scrubber);
            break;

          case "Recycler":
            filename = Lib.BuildString("recycler/", recycler_i++.ToString());
            device = new RecyclerDevice(m as Recycler);
            break;

          case "Greenhouse":
            filename = Lib.BuildString("greenhouse/", greenhouse_i++.ToString());
            device = new GreenhouseDevice(m as Greenhouse);
            break;

          case "GravityRing":
            filename = Lib.BuildString("ring/", ring_i++.ToString());
            device = new RingDevice(m as GravityRing);
            break;

          case "Emitter":
            if ((m as Emitter).ec_rate <= double.Epsilon) continue; //< skip non-tweakable emitters
            filename = Lib.BuildString("emitter/", emitter_i++.ToString());
            device = new EmitterDevice(m as Emitter);
            break;

          case "ModuleDeployableSolarPanel":
            filename = Lib.BuildString("panel/", panel_i++.ToString());
            device = new PanelDevice(m as ModuleDeployableSolarPanel);
            break;

          case "ModuleGenerator":
            filename = Lib.BuildString("generator/", generator_i++.ToString());
            device = new GeneratorDevice(m as ModuleGenerator);
            break;

          case "ModuleResourceConverter":
          case "ModuleKPBSConverter":
          case "FissionReactor":
            filename = Lib.BuildString("converter/", converter_i++.ToString());
            device = new ConverterDevice(m as ModuleResourceConverter);
            break;

          case "ModuleResourceHarvester":
            filename = Lib.BuildString("drill/", drill_i++.ToString());
            device = new DrillDevice(m as ModuleResourceHarvester);
            break;

          case "ModuleLight":
          case "ModuleColoredLensLight":
          case "ModuleMultiPointSurfaceLight":
            filename = Lib.BuildString("light/", light_i++.ToString());
            device = new LightDevice(m as ModuleLight);
            break;

          default: continue;
        }

        // add device file
        files.Add(filename, new File(device));
      }
    }
    // unloaded vessel
    else
    {
      foreach(ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
      {
        // a part can contain multiple resource converters
        int converter_index = 0;

        // get part prefab
        Part part_prefab = PartLoader.getPartInfoByName(p.partName).partPrefab;

        // for each modules
        foreach(ProtoPartModuleSnapshot m in p.modules)
        {
          // get the module prefab, skip if prefab doesn't contain the module
          PartModule module_prefab = Lib.FindModule(part_prefab, m.moduleName);
          if (!module_prefab) continue;

          // depending on module name
          switch(m.moduleName)
          {
            case "Scrubber":
              filename = Lib.BuildString("scrubber/", scrubber_i++.ToString());
              device = new ProtoScrubberDevice(m);
              break;

            case "Recycler":
              filename = Lib.BuildString("recycler/", recycler_i++.ToString());
              device = new ProtoRecyclerDevice(m);
              break;

            case "Greenhouse":
              filename = Lib.BuildString("greenhouse/", greenhouse_i++.ToString());
              device = new ProtoGreenhouseDevice(m);
              break;

            case "GravityRing":
              filename = Lib.BuildString("ring/", ring_i++.ToString());
              device = new ProtoRingDevice(m);
              break;

            case "Emitter":
              if ((module_prefab as Emitter).ec_rate <= double.Epsilon) continue; //< skip non-tweakable emitters
              filename = Lib.BuildString("emitter/", emitter_i++.ToString());
              device = new ProtoEmitterDevice(m);
              break;

            case "ModuleDeployableSolarPanel":
              filename = Lib.BuildString("panel/", panel_i++.ToString());
              device = new ProtoPanelDevice(m, module_prefab as ModuleDeployableSolarPanel);
              break;

            case "ModuleGenerator":
              filename = Lib.BuildString("generator/", generator_i++.ToString());
              device = new ProtoGeneratorDevice(m, module_prefab as ModuleGenerator);
              break;

            case "ModuleResourceConverter":
            case "ModuleKPBSConverter":
            case "FissionReactor":
              var converter_prefabs = part_prefab.Modules.GetModules<ModuleResourceConverter>();
              if (converter_index >= converter_prefabs.Count) continue;
              module_prefab = converter_prefabs[converter_index++];
              filename = Lib.BuildString("converter/", converter_i++.ToString());
              device = new ProtoConverterDevice(m, module_prefab as ModuleResourceConverter);
              break;

            case "ModuleResourceHarvester":
              ProtoPartModuleSnapshot deploy = p.modules.Find(k => k.moduleName == "ModuleAnimationGroup");
              filename = Lib.BuildString("drill/", drill_i++.ToString());
              device = new ProtoDrillDevice(m, module_prefab as ModuleResourceHarvester, deploy);
              break;

            case "ModuleLight":
            case "ModuleColoredLensLight":
            case "ModuleMultiPointSurfaceLight":
              filename = Lib.BuildString("light/", light_i++.ToString());
              device = new ProtoLightDevice(m);
              break;

            default: continue;
          }

          // add device file
          files.Add(filename, new File(device));
        }
      }
    }
  }

  // remove all device files
  void cleanup()
  {
    Dictionary<string, File> _files = new Dictionary<string, File>();
    foreach(var pair in files)
    {
      if (pair.Value.device == null) _files.Add(pair.Key, pair.Value);
    }
    files = _files;
  }


  // ### DATA #################################################################


  public Dictionary<string, File>  files;         // the computer storage archive
  public string                    output;        // output of last command
  public bool                      status;        // success status of last command

  // used to keep track of previous states
  Vessel.Situations prev_situation;
  bool prev_sunlight;
  bool prev_power;
  bool prev_signal;
  bool prev_radiation;
}


} // KERBALISM