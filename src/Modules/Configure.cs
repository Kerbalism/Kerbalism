// ===================================================================================================================
// Permit to select between a set of setups, each one containing an arbitrary set of modules and resources
// ===================================================================================================================


using System;
using System.Collections.Generic;


namespace KERBALISM {


public sealed class Configure : PartModule, IPartCostModifier, IPartMassModifier
{
  // config
  [KSPField] public string title  = string.Empty;           // short description
  [KSPField] public string data   = string.Empty;           // store setups as serialized data
  [KSPField] public uint   slots  = 1;                      // how many setups can be selected

  // persistence
  [KSPField(isPersistant = true)] public string cfg;        // selected setups names

  // data
  List<ConfigureSetup> setups;                              // all setups
  List<ConfigureSetup> unlocked;                            // unlocked setups
  List<string>         selected;                            // selected setups names
  PanelWindow          window;                              // the configuration/details window
  double               extra_cost;                          // extra cost for selected setups, including resources
  double               extra_mass;                          // extra mass for selected setups, excluding resources
  bool                 initialized;                         // used to initialize only once


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

    // default title to part name
    if (title.Length == 0) title = part.partInfo.name;

    // set toggle window button label
    Events["ToggleWindow"].guiName = state == StartState.Editor
      ? Lib.BuildString("Configure <b>", title, "</b>")
      : Lib.BuildString("<b>", title, "</b> details");
  }


  public override void OnLoad(ConfigNode node)
  {
    // panels data from structured config node is only available at part compilation
    // for this reason, we parse it and then re-serialize it as a string
    if (HighLogic.LoadedScene == GameScenes.LOADING)
    {
      // parse all setups from config node
      setups = new List<ConfigureSetup>();
      foreach(var setup_node in node.GetNodes("SETUP"))
      {
        setups.Add(new ConfigureSetup(setup_node));
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
    }
  }


  void configure()
  {
    // store list of modules to remove
    var module_to_remove = new List<PartModule>();

    // store list of resources to remove
    var resource_to_remove = new List<PartResource>();

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
    if (HighLogic.LoadedSceneIsEditor)
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

      // for each module specification in the setup
      foreach(ConfigureModule cm in setup.modules)
      {
        // try to find the module
        PartModule m = find_module(cm);

        // if the module exist
        if (m != null)
        {
          // in the editor, enable/disable it
          if (HighLogic.LoadedSceneIsEditor)
          {
            m.isEnabled = active;
          }
          // in flight, add to list of modules to remove
          else if (Lib.SceneIsGame())
          {
            if (!active) module_to_remove.Add(m);
          }
        }
      }

      // only in the editor
      if (HighLogic.LoadedSceneIsEditor)
      {
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

          // get resources to remove first
          resource_to_remove.Clear();
          foreach(PartResource res in part.Resources)
          {
            if (res.resourceName == cr.name)
            {
              resource_to_remove.Add(res);
            }
          }

          // remove resources
          foreach(PartResource r in resource_to_remove)
          {
            if (part.Resources.list.Contains(r))
            {
              part.Resources.list.Remove(r);
              Destroy(r);
            }
          }

          // if selected
          if (active && capacity > 0.0)
          {
            // add the resource
            ConfigNode res = new ConfigNode("RESOURCE");
            res.AddValue("name", cr.name);
            res.AddValue("amount", cr.amount);
            res.AddValue("maxAmount", cr.maxAmount);
            res.AddValue("isVisible", true);
            res.AddValue("isTweakable", true);
            part.Resources.Add(res);

            // add extra cost
            extra_cost += amount * unit_cost;
          }
        }
      }

      // add setup extra cost and mass
      if (active)
      {
        extra_cost += setup.cost;
        extra_mass += setup.mass;
      }
    }

    // remove non-selected modules
    foreach(PartModule m in module_to_remove)
    {
      part.RemoveModule(m);
    }

    // save configuration
    WriteArchive archive = new WriteArchive();
    archive.save(selected.Count);
    foreach(string s in selected) archive.save(s);
    cfg = archive.serialize();
  }


  void Update()
  {
    // do nothing if tech is not ready
    if (!Lib.TechReady()) return;

    // do only once
    if (initialized) return;
    initialized = true;

    // if we remove a module inside Start(), the stock system start
    // throwing exceptions so we do it here, that is totally fine
    configure();
  }


  void OnGUI()
  {
    // if window is opened
    if (window != null)
    {
      // clear window
      window.clear();

      // for each selected setup
      for(int selected_i = 0; selected_i < selected.Count; ++selected_i)
      {
        // find index in unlocked setups
        for(int setup_i = 0; setup_i < unlocked.Count; ++setup_i)
        {
          if (unlocked[setup_i].name == selected[selected_i])
          {
            // commit panel
            commit_panel(unlocked[setup_i], selected_i, setup_i);
          }
        }
      }

      // draw window
      window.draw();
    }
  }


  void commit_panel(ConfigureSetup setup, int selected_i, int setup_i)
  {
    // create panel section
    PanelSection ps = new PanelSection(setup.name);
    ps.add(Lib.BuildString("<i>", setup.desc.Length > 0 ? setup.desc : "no description available", "</i>"));
    foreach(var det in setup.details)
    {
      ps.add(det.label, det.value);
    }

    // commit it
    if (Lib.SceneIsGame() || unlocked.Count <= selected.Count)
    {
      window.add(ps);
    }
    else
    {
      window.add(ps, (int i) =>
      {
        do
        {
          setup_i = (setup_i + unlocked.Count + i) % unlocked.Count;
        }
        while(selected.Contains(unlocked[setup_i].name));

        // update selected
        selected[selected_i] = unlocked[setup_i].name;

        // reconfigure
        configure();
      });
    }
  }


  [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_")]
  public void ToggleWindow()
  {
    window = (window == null ? new PanelWindow(title) : null);
  }


  PartModule find_module(ConfigureModule cm)
  {
    // for each module in the part
    foreach(PartModule m in part.Modules)
    {
      // if the module type match
      if (m.moduleName == cm.type)
      {
        // if the module field is not necessary
        if (cm.id_field.Length == 0)
        {
          // found it
          return m;
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
      }
    }

    // not found
    return null;
  }


  public float GetModuleCost(float defaultCost, ModifierStagingSituation sit) { return (float)extra_cost; }
  public float GetModuleMass(float defaultCost, ModifierStagingSituation sit) { return (float)extra_mass; }
  public ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
  public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
}


public sealed class ConfigureSetup
{
  public ConfigureSetup(ConfigNode node)
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

    // parse details
    details = new List<Detail>();
    ConfigNode details_node = node.GetNode("DETAILS");
    if (details_node != null)
    {
      string[] split;
      foreach(string s in details_node.GetValues())
      {
        split = s.Split('|');
        Detail det = new Detail();
        det.label = split.Length > 0 ? split[0].Trim() : string.Empty;
        det.value = split.Length > 1 ? split[1].Trim() : string.Empty;
        details.Add(det);
      }
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
  }

  public ConfigureModule(ReadArchive archive)
  {
    archive.load(out type);
    archive.load(out id_field);
    archive.load(out id_value);
  }

  public void save(WriteArchive archive)
  {
    archive.save(type);
    archive.save(id_field);
    archive.save(id_value);
  }

  public string type;
  public string id_field;
  public string id_value;
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


