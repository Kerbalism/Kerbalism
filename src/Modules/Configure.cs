using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;


namespace KERBALISM {


// Modules can implement this interface in case they need to do something
// when enabled/disabled by Configure. This is the case, for example, for
// all those modules that add resources dynamically (like Process or Habitat).
public interface IConfigurable
{
  // (de)configure the module
  void Configure(bool enable);
}


public sealed class Configure : PartModule, IPartCostModifier, IPartMassModifier, IModuleInfo, ISpecifics
{
  // config
  [KSPField] public string title        = string.Empty;     // short description
  [KSPField] public string data         = string.Empty;     // store setups as serialized data
  [KSPField] public uint   slots        = 1;                // how many setups can be selected
  [KSPField] public string reconfigure  = string.Empty;     // true if it can be reconfigured in flight

  // persistence
  [KSPField(isPersistant = true)] public string cfg;        // selected setups names
  [KSPField(isPersistant = true)] public string prev_cfg;   // previously selected setups names

  // data
  // - selected and prev_selected are public so that the automagical
  //   part copy/symmetry serialization can see them
  List<ConfigureSetup> setups;                              // all setups
  List<ConfigureSetup> unlocked;                            // unlocked setups
  public List<string>  selected;                            // selected setups names
  public List<string>  prev_selected;                       // previously selected setups names
  double               extra_cost;                          // extra cost for selected setups, including resources
  double               extra_mass;                          // extra mass for selected setups, excluding resources
  bool                 initialized;                         // keep track of first configuration
  CrewSpecs            reconfigure_cs;                      // in-flight reconfiguration crew specs
  Dictionary<int, int>  changes;      // store 'deferred' changes to avoid problems with unity gui

  // used to avoid infinite recursion when dealing with symmetry group
  static bool avoid_inf_recursion;


  public override void OnStart(StartState state)
  {
    // parse all setups from string data
    var archive = new ReadArchive(data);
    int count;
    archive.load(out count);
    setups = new List<ConfigureSetup>(count);
    while(count-- > 0) setups.Add(new ConfigureSetup(archive));

    // parse configuration from string data
    archive = new ReadArchive(cfg);
    archive.load(out count);
    selected = new List<string>(count);
    while(count-- > 0) { string s; archive.load(out s); selected.Add(s); }

    // parse previous configuration from string data
    archive = new ReadArchive(prev_cfg);
    archive.load(out count);
    prev_selected = new List<string>(count);
    while(count-- > 0) { string s; archive.load(out s); prev_selected.Add(s); }

    // default title to part name
    if (title.Length == 0) title = Lib.PartName(part);

    // parse crew specs
    reconfigure_cs = new CrewSpecs(reconfigure);

    // set toggle window button label
    Events["ToggleWindow"].guiName = Lib.BuildString("Configure <b>", title, "</b>");

    // only show toggle in flight if this is reconfigurable
    Events["ToggleWindow"].active = Lib.IsEditor() || reconfigure_cs;

    // store configuration changes
    changes = new Dictionary<int, int>();
  }


  public override void OnLoad(ConfigNode node)
  {
    // setups data from structured config node is only available at part compilation
    // for this reason, we parse it and then re-serialize it as a string
    if (HighLogic.LoadedScene == GameScenes.LOADING)
    {
      // parse all setups from config node and generate details
      setups = new List<ConfigureSetup>();
      foreach(var setup_node in node.GetNodes("SETUP"))
      {
        setups.Add(new ConfigureSetup(setup_node, this));
      }

      // serialize the setups to string data
      var archive = new WriteArchive();
      archive.save(setups.Count);
      foreach(var setup in setups) setup.save(archive);
      data = archive.serialize();

      // serialize empty configuration to string data
      archive = new WriteArchive();
      archive.save(0);
      cfg = archive.serialize();

      // serialize empty previous configuration to string data
      archive = new WriteArchive();
      archive.save(0);
      prev_cfg = archive.serialize();
    }

    // special care for users of version 1.1.5pre1
    if (string.IsNullOrEmpty(prev_cfg))
    {
      var archive = new WriteArchive();
      archive.save(0);
      prev_cfg = archive.serialize();
    }
  }


  public void configure()
  {
    // shortcut to resource library
    var reslib = PartResourceLibrary.Instance.resourceDefinitions;

    // reset extra cost and mass
    extra_cost = 0.0;
    extra_mass = 0.0;

    // find modules unlocked by tech
    unlocked = new List<ConfigureSetup>();
    foreach(ConfigureSetup setup in setups)
    {
      // if unlocked
      if (setup.tech.Length == 0 || Lib.HasTech(setup.tech))
      {
        // unlock
        unlocked.Add(setup);
      }
    }

    // make sure configuration include all available slots
    // this also create default configuration
    if (Lib.IsEditor())
    {
      while(selected.Count < Math.Min(slots, (uint)unlocked.Count))
      {
        selected.Add(unlocked.Find(k => selected.IndexOf(k.name) == -1).name);
      }
    }

    // for each setup
    foreach(ConfigureSetup setup in setups)
    {
      // detect if the setup is selected
      bool active = selected.Contains(setup.name);

      // detect if the setup was previously selected
      bool prev_active = prev_selected.Contains(setup.name);

      // for each module specification in the setup
      foreach(ConfigureModule cm in setup.modules)
      {
        // try to find the module
        PartModule m = find_module(cm);

        // if the module exist
        if (m != null)
        {
          // call configure/deconfigure functions on module if available
          IConfigurable configurable_module = m as IConfigurable;
          if (configurable_module != null)
          {
            configurable_module.Configure(active);
          }

          // enable/disable the module
          m.isEnabled = active;
          m.enabled = active;
        }
      }

      // for each resource specification in the setup
      foreach(ConfigureResource cr in setup.resources)
      {
        // ignore non-existing resources
        if (!reslib.Contains(cr.name)) continue;

        // get resource unit cost
        double unit_cost = reslib[cr.name].unitCost;

        // parse resource amount and capacity
        double amount = Lib.Parse.ToDouble(cr.amount);
        double capacity = Lib.Parse.ToDouble(cr.maxAmount);

        // (de)install resource, but only if the following apply
        // - in the editor
        // - in flight, reconfigurable and not first time it is configured
        if (Lib.IsEditor() || (reconfigure_cs && initialized))
        {
          // if previously selected
          if (prev_active)
          {
            // remove the resources
            // - in flight, do not remove amount
            Lib.RemoveResource(part, cr.name, Lib.IsFlight() ? 0.0 : amount, capacity);
          }

          // if selected
          if (active && capacity > 0.0)
          {
            // add the resources
            // - in flight, do not add amount
            Lib.AddResource(part, cr.name, Lib.IsFlight() ? 0.0 : amount, capacity);
          }
        }

        // add resource cost
        if (active)
        {
          extra_cost += amount * unit_cost;
        }
      }

      // add setup extra cost and mass
      if (active)
      {
        extra_cost += setup.cost;
        extra_mass += setup.mass;
      }
    }

    // remember previously selected setups
    prev_selected.Clear();
    foreach(string s in selected) prev_selected.Add(s);

    // save configuration
    WriteArchive archive = new WriteArchive();
    archive.save(selected.Count);
    foreach(string s in selected) archive.save(s);
    cfg = archive.serialize();

    // save previous configuration
    archive = new WriteArchive();
    archive.save(prev_selected.Count);
    foreach(string s in prev_selected) archive.save(s);
    prev_cfg = archive.serialize();

    // in the editor
    if (Lib.IsEditor())
    {
      // for each part in the symmetry group (avoid infinite recursion)
      if (!avoid_inf_recursion)
      {
        avoid_inf_recursion = true;
        foreach(Part p in part.symmetryCounterparts)
        {
          // get the Configure module
          Configure c = p.FindModulesImplementing<Configure>().Find(k => k.title == title);

          // both modules will share configuration
          c.selected = selected;

          // re-configure the other module
          c.configure();
        }
        avoid_inf_recursion = false;
      }
    }

    // refresh this part ui
    MonoUtilities.RefreshContextWindows(part);

    // refresh VAB ui
    if (Lib.IsEditor()) GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);

    // this was configured at least once
    initialized = true;
  }

  void OnGUI()
  {
    // if never configured
    if (!initialized)
    {
      // configure the first time
      // note: done here, instead of OnStart, so that we are guaranteed to configure()
      // after the eventual configure(true) that some modules may call in their OnStart
      configure();
    }

    // if this is the last gui event
    if (Event.current.type == EventType.Repaint)
    {
      // apply changes
      foreach(var p in changes)
      {
        // change setup
        selected[p.Key] = unlocked[p.Value].name;

        // reconfigure
        configure();
      }
      changes.Clear();
    }
  }


  [KSPEvent(guiActive = true, guiActiveUnfocused = true, guiActiveEditor = true, guiName = "_", active = false)]
  public void ToggleWindow()
  {
    // in flight
    if (Lib.IsFlight())
    {
      // disable for dead eva kerbals
      Vessel v = FlightGlobals.ActiveVessel;
      if (v == null || EVA.IsDead(v)) return;

      // check trait
      if (!reconfigure_cs.check(v))
      {
        Message.Post("Can't reconfigure the component", reconfigure_cs.warning());
        return;
      }

      // warn the user about potential resource loss
      if (resource_loss())
      {
        Message.Post(Severity.warning, "Reconfiguring will dump resources in excess of capacity.");
      }
    }

    // open the window
    UI.open((p) => window_body(p));
  }


  bool resource_loss()
  {
    // detect if any of the setup deal with resources
    // - we are ignoring resources that configured modules may generate on-the-fly
    //   this is okay for our current IConfigurable modules (habitat, process controller, harvester)
    //   however this will not be okay for something like a Container module, for example
    //   if the need arise, add a function bool change_resources() to the IConfigurable interface
    foreach(ConfigureSetup setup in setups)
    {
      foreach(ConfigureResource res in setup.resources)
      {
        if (Lib.Amount(part, res.name, true) > double.Epsilon) return true;
      }
    }
    return false;
  }


  // part tooltip
  public override string GetInfo()
  {
    return Specs().info();
  }


  // specifics support
  public Specifics Specs()
  {
    Specifics specs = new Specifics();
    specs.add("slots", slots.ToString());
    specs.add("reconfigure", new CrewSpecs(reconfigure).info());
    specs.add(string.Empty);
    specs.add("setups:");

    // organize setups by tech required, and add the ones without tech
    Dictionary<string, List<string>> org = new Dictionary<string, List<string>>();
    foreach(ConfigureSetup setup in setups)
    {
      if (setup.tech.Length > 0)
      {
        if (!org.ContainsKey(setup.tech)) org.Add(setup.tech, new List<string>());
        org[setup.tech].Add(setup.name);
      }
      else
      {
        specs.add(Lib.BuildString("• <b>", setup.name, "</b>"));
      }
    }

    // add setups grouped by tech
    foreach(var pair in org)
    {
      // shortcuts
      string tech_id = pair.Key;
      List<string> setup_names = pair.Value;

      // add tech name
      specs.add(string.Empty);
      specs.add(Lib.BuildString("<color=#00ffff>", ResearchAndDevelopment.GetTechnologyTitle(tech_id).ToLower(), ":</color>"));

      // add setup names
      foreach(string setup_name in setup_names)
      {
        specs.add(Lib.BuildString("• <b>", setup_name, "</b>"));
      }
    }

    return specs;
  }


  public PartModule find_module(ConfigureModule cm)
  {
    // for each module in the part
    int index=0;
    foreach(PartModule m in part.Modules)
    {
      // if the module type match
      if (m.moduleName == cm.type)
      {
        // if the module field is not specified
        if (cm.id_field.Length == 0)
        {
          // search it by index
          if (index == cm.id_index) return m;
        }
        // if the module field match
        else
        {
          // get identifier
          string id = Lib.ReflectionValue<string>(m, cm.id_field);

          // if the identifier match
          if (id == cm.id_value)
          {
            // found it
            return m;
          }
        }
        ++index;
      }
    }

    // not found
    return null;
  }

  // to be called as window refresh function
  void window_body(Panel p)
  {
    // outside the editor
    if (!Lib.IsEditor())
    {
      // if part doesn't exist anymore
      if (FlightGlobals.FindPartByID(part.flightID) == null) return;
    }

    // for each selected setup
    for(int selected_i = 0; selected_i < selected.Count; ++selected_i)
    {
      // find index in unlocked setups
      for(int setup_i = 0; setup_i < unlocked.Count; ++setup_i)
      {
        if (unlocked[setup_i].name == selected[selected_i])
        {
          // commit panel
          render_panel(p, unlocked[setup_i], selected_i, setup_i);
        }
      }
    }

    // set metadata
    p.title(Lib.BuildString("Configure <color=#cccccc>", title, "</color>"));
  }

  void render_panel(Panel p, ConfigureSetup setup, int selected_i, int setup_i)
  {
    // render section title
    // only allow reconfiguration if there are more setups than slots
    if (unlocked.Count <= selected.Count)
    {
      p.section(setup.name);
    }
    else
    {
      string desc = setup.desc.Length > 0 ? Lib.BuildString("<i>", setup.desc, "</i>") : string.Empty;
      p.section(setup.name, desc, () => change_setup(-1, selected_i, ref setup_i), () => change_setup(1, selected_i, ref setup_i));
    }

    // render other content
    foreach(var det in setup.details)
    {
      p.content(det.label, det.value);
    }
  }

  // utility, used as callback in panel select
  void change_setup(int change, int selected_i, ref int setup_i)
  {
    do
    {
      setup_i = (setup_i + change + unlocked.Count) % unlocked.Count;
    }
    while(selected.Contains(unlocked[setup_i].name));
    changes.Add(selected_i, setup_i);
  }

  // access setups
  public List<ConfigureSetup> Setups()
  {
    return setups;
  }


  // module cost support
  public float GetModuleCost(float defaultCost, ModifierStagingSituation sit) { return (float)extra_cost; }

  // module mass support
  public float GetModuleMass(float defaultCost, ModifierStagingSituation sit) { return (float)extra_mass; }
  public ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
  public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

  // module info support
  public string GetModuleTitle() { return Lib.BuildString("# Configurable ", title); } //< make sure it is at the top
  public string GetPrimaryField() { return Lib.BuildString("Configurable ", title); }
  public Callback<Rect> GetDrawModulePanelCallback() { return null; }
}


public sealed class ConfigureSetup
{
  public ConfigureSetup(ConfigNode node, Configure cfg)
  {
    // parse basic data
    name = Lib.ConfigValue(node, "name", string.Empty);
    desc = Lib.ConfigValue(node, "desc", string.Empty);
    tech = Lib.ConfigValue(node, "tech", string.Empty);
    cost = Lib.ConfigValue(node, "cost", 0.0);
    mass = Lib.ConfigValue(node, "mass", 0.0);

    // parse modules
    modules = new List<ConfigureModule>();
    foreach(var module_node in node.GetNodes("MODULE"))
    {
      modules.Add(new ConfigureModule(module_node));
    }

    // parse resources
    resources = new List<ConfigureResource>();
    foreach(var res_node in node.GetNodes("RESOURCE"))
    {
      resources.Add(new ConfigureResource(res_node));
    }

    // generate module details
    details = new List<Detail>();
    foreach(ConfigureModule cm in modules)
    {
      // find module, skip if it doesn't exist
      PartModule m = cfg.find_module(cm);
      if (m == null) continue;

      // get title
      IModuleInfo module_info = m as IModuleInfo;
      string title = module_info != null ? module_info.GetModuleTitle() : cm.type;
      if (title.Length == 0) continue;

      // get specs, skip if not implemented by module
      ISpecifics specifics = m as ISpecifics;
      if (specifics == null) continue;
      Specifics specs = specifics.Specs();
      if (specs.entries.Count == 0) continue;

      // add title to details
      details.Add(new Detail(Lib.BuildString("<b><color=#00ffff>", title, "</color></b>")));

      // add specs to details
      foreach(Specifics.Entry e in specs.entries)
      {
        details.Add(new Detail(e.label, e.value));
      }
    }

    // get visible resources subset
    List<ConfigureResource> visible_resources = resources.FindAll(k => Lib.GetDefinition(k.name).isVisible);

    // generate resource details
    if (visible_resources.Count > 0)
    {
      // add resources title
      details.Add(new Detail("<b><color=#00ffff>Resources</color></b>"));

      // for each visible resource
      foreach(ConfigureResource cr in visible_resources)
      {
        // add capacity info
        details.Add(new Detail(cr.name, Lib.Parse.ToDouble(cr.maxAmount).ToString("F2")));
      }
    }

    // generate extra details
    if (mass > double.Epsilon || cost > double.Epsilon)
    {
      details.Add(new Detail("<b><color=#00ffff>Extra</color></b>"));
      if (mass > double.Epsilon) details.Add(new Detail("mass", Lib.HumanReadableMass(mass)));
      if (cost > double.Epsilon) details.Add(new Detail("cost", Lib.HumanReadableCost(cost)));
    }
  }

  public ConfigureSetup(ReadArchive archive)
  {
    // load basic data
    archive.load(out name);
    archive.load(out desc);
    archive.load(out tech);
    archive.load(out cost);
    archive.load(out mass);

    // load modules
    int count;
    archive.load(out count);
    modules = new List<ConfigureModule>(count);
    while(count-- > 0) modules.Add(new ConfigureModule(archive));

    // load resources
    archive.load(out count);
    resources = new List<ConfigureResource>(count);
    while(count-- > 0) resources.Add(new ConfigureResource(archive));

    // load details
    archive.load(out count);
    details = new List<Detail>(count);
    while(count-- > 0)
    {
      Detail det = new Detail();
      archive.load(out det.label);
      archive.load(out det.value);
      details.Add(det);
    }
  }

  public void save(WriteArchive archive)
  {
    // save basic data
    archive.save(name);
    archive.save(desc);
    archive.save(tech);
    archive.save(cost);
    archive.save(mass);

    // save modules
    archive.save(modules.Count);
    foreach(ConfigureModule m in modules) m.save(archive);

    // save resources
    archive.save(resources.Count);
    foreach(ConfigureResource r in resources) r.save(archive);

    // save details
    archive.save(details.Count);
    foreach(Detail det in details)
    {
      archive.save(det.label);
      archive.save(det.value);
    }
  }

  public class Detail
  {
    public Detail()
    {}

    public Detail(string label, string value = "")
    {
      this.label = label;
      this.value = value;
    }

    public string label = string.Empty;
    public string value = string.Empty;
  }

  public string name;
  public string desc;
  public string tech;
  public double cost;
  public double mass;
  public List<ConfigureModule> modules;
  public List<ConfigureResource> resources;
  public List<Detail> details;
}


public sealed class ConfigureModule
{
  public ConfigureModule(ConfigNode node)
  {
    type     = Lib.ConfigValue(node, "type",     string.Empty);
    id_field = Lib.ConfigValue(node, "id_field", string.Empty);
    id_value = Lib.ConfigValue(node, "id_value", string.Empty);
    id_index = Lib.ConfigValue(node, "id_index", 0);
  }

  public ConfigureModule(ReadArchive archive)
  {
    archive.load(out type);
    archive.load(out id_field);
    archive.load(out id_value);
    archive.load(out id_index);
  }

  public void save(WriteArchive archive)
  {
    archive.save(type);
    archive.save(id_field);
    archive.save(id_value);
    archive.save(id_index);
  }

  public string type;
  public string id_field;
  public string id_value;
  public int    id_index;
}


public sealed class ConfigureResource
{
  public ConfigureResource(ConfigNode node)
  {
    name      = Lib.ConfigValue(node, "name",      string.Empty);
    amount    = Lib.ConfigValue(node, "amount",    string.Empty);
    maxAmount = Lib.ConfigValue(node, "maxAmount", string.Empty);
  }

  public ConfigureResource(ReadArchive archive)
  {
    archive.load(out name);
    archive.load(out amount);
    archive.load(out maxAmount);
  }

  public void save(WriteArchive archive)
  {
    archive.save(name);
    archive.save(amount);
    archive.save(maxAmount);
  }

  public string name;
  public string amount;
  public string maxAmount;
}


} // KERBALISM


