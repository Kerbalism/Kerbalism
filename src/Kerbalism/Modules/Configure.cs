using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{


	/// <summary>
	/// Modules can implement this interface in case they need to do something
	/// when enabled/disabled by Configure. This is the case, for example, for
	/// all those modules that add resources dynamically (like Process or Habitat).
	/// </summary>
	public interface IConfigurable
	{
		// configure the module
		void Configure(bool enable);
	}


	public sealed class Configure : PartModule, IPartCostModifier, IPartMassModifier, IModuleInfo, ISpecifics, IConfigurable
	{
		// config
		[KSPField] public string title = string.Empty;           // short description
		[KSPField] public string data = string.Empty;            // store setups as serialized data
		[KSPField] public uint slots = 1;                        // how many setups can be selected
		[KSPField] public string reconfigure = string.Empty;     // true if it can be reconfigured in flight
		[KSPField] public bool symmetric = false;                // true if all setups in the same symmetry group should be the same

		// persistence
		[KSPField(isPersistant = true)] public string cfg;        // selected setups names
		[KSPField(isPersistant = true)] public string prev_cfg;   // previously selected setups names

		// data
		// - selected and prev_selected are public so that the auto-magical
		//   part copy/symmetry serialization can see them
		List<ConfigureSetup> setups;                              // all setups
		List<ConfigureSetup> unlocked;                            // unlocked setups
		public List<string> selected;                             // selected setups names
		public List<string> prev_selected;                        // previously selected setups names
		double extra_cost;                                        // extra cost for selected setups, including resources
		double extra_mass;                                        // extra mass for selected setups, excluding resources
		bool initialized;                                         // keep track of first configuration
		CrewSpecs reconfigure_cs;                                 // in-flight reconfiguration crew specs
		Dictionary<int, int> changes;                             // store 'deferred' changes to avoid problems with unity gui

		// used to avoid infinite recursion when dealing with symmetry group
		static bool avoid_inf_recursion;


		public override void OnStart(StartState state)
		{
			// don't break tutorial scenarios
			if (Lib.DisableScenario(this)) return;

			InitSetups();

			selected = new List<string>();
			if (!string.IsNullOrEmpty(cfg))
			{
				selected = Archive.string2list(cfg);
			}

			prev_selected = new List<string>();
			if (!string.IsNullOrEmpty(prev_cfg))
			{
				prev_selected = Archive.string2list(prev_cfg);
			}

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

		private static readonly Dictionary<string, List<ConfigureSetup>> _all_setups = new Dictionary<string, List<ConfigureSetup>>();

		public static Dictionary<string, List<ConfigureSetup>> AllSetups()
		{
			return _all_setups;
		}

		public override void OnLoad(ConfigNode node)
		{
			// setups data from structured config node is only available at part compilation
			// for this reason, we parse it and then re-serialize it as a string
			if (HighLogic.LoadedScene == GameScenes.LOADING)
			{
				if (!_all_setups.ContainsKey(name)) _all_setups.Add(name, new List<ConfigureSetup>());

				var _setups = _all_setups[name];
				_setups.Clear();

				foreach (var setup_node in node.GetNodes("SETUP"))
				{
					_setups.Add(new ConfigureSetup(setup_node, this));
				}
			}
		}

		private void InitSetups()
		{
			if (setups != null) return;

			if (!_all_setups.ContainsKey(name))
			{
				Lib.Log("Configure Error: " + name + " has no known setups");
				return;
			}

			setups = new List<ConfigureSetup>();
			foreach (var setup in _all_setups[name])
				setups.Add(new ConfigureSetup(setup));
		}

		void IConfigurable.Configure(bool enable)
		{
			enabled = enable;
			isEnabled = enable;
			DoConfigure();
		}

		public void DoConfigure()
		{
			if (setups == null) return;

			// shortcut to resource library
			var reslib = PartResourceLibrary.Instance.resourceDefinitions;

			// reset extra cost and mass
			extra_cost = 0.0;
			extra_mass = 0.0;

			// find modules unlocked by tech
			unlocked = new List<ConfigureSetup>();
			foreach (ConfigureSetup setup in setups)
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
			// - we do it only in the editor
			// - we avoid corner case when cfg was never set up (because craft was never in VAB)
			if (Lib.IsEditor() || selected.Count == 0)
			{
				while (selected.Count < Math.Min(slots, (uint)unlocked.Count))
				{
					selected.Add(unlocked.Find(k => selected.IndexOf(k.name) == -1).name);
				}
			}

			// for each setup
			foreach (ConfigureSetup setup in setups)
			{
				// detect if the setup is selected in multiple slots
				int count = (selected.FindAll(x => x == setup.name)).Count;

				// detect if the setup is selected
				bool active = count > 0;
				active &= enabled;

				// detect if the setup was previously selected in multiple slots
				int prev_count = (prev_selected.FindAll(x => x == setup.name)).Count;

				// detect if the setup was previously selected
				bool prev_active = prev_count > 0;

				// for each module specification in the setup
				foreach (ConfigureModule cm in setup.modules)
				{
					// try to find the module
					PartModule m = Find_module(cm);

					// if the module exist
					if (m != null)
					{
						// call configure/deconfigure functions on module if available
						if (m is IConfigurable configurable_module)
							configurable_module.Configure(active);

						// enable/disable the module
						m.isEnabled = active;
						m.enabled = active;
					}
				}

				// for each resource specification in the setup
				foreach (ConfigureResource cr in setup.resources)
				{
					// ignore non-existing resources
					if (!reslib.Contains(cr.name)) continue;

					// get resource unit cost
					double unit_cost = reslib[cr.name].unitCost;

					// parse resource amount and capacity
					double amount = Lib.Parse.ToDouble(cr.amount);
					double capacity = Lib.Parse.ToDouble(cr.maxAmount);

					// add/remove resource
					if ((prev_active != (active && capacity > 0.0)) || (reconfigure_cs && initialized) || (count != prev_count))
					{
						// if previously selected
						if (prev_active)
						{
							// remove the resources
							prev_count = prev_count == 0 ? 1 : prev_count;
							Lib.RemoveResource(part, cr.name, amount * prev_count, capacity * prev_count);
						}

						// if selected
						if (active && capacity > 0.0)
						{
							// add the resources
							// - in flight, do not add amount
							Lib.AddResource(part, cr.name, Lib.IsFlight() ? 0.0 : amount * count, capacity * count);
						}
					}

					// add resource cost
					if (active) extra_cost += amount * unit_cost * count;
				}

				// add setup extra cost and mass
				if (active)
				{
					extra_cost += setup.cost * count;
					extra_mass += setup.mass * count;
				}
			}

			// remember previously selected setups
			prev_selected.Clear();
			foreach (string s in selected) prev_selected.Add(s);

			// save configuration
			cfg = Archive.list2str(selected);

			// save previous configuration
			prev_cfg = Archive.list2str(prev_selected);

			// in the editor
			if (Lib.IsEditor() && !avoid_inf_recursion && symmetric)
			{
				avoid_inf_recursion = true;
				foreach (Part p in part.symmetryCounterparts)
				{
					// get the Configure module
					Configure c = p.FindModulesImplementing<Configure>().Find(k => k.title == title);

					// both modules will share configuration
					c.selected = selected;

					// re-configure the other module
					c.DoConfigure();
				}
				avoid_inf_recursion = false;
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
				DoConfigure();
			}

			// if this is the last gui event
			if (Event.current.type == EventType.Repaint)
			{
				// apply changes
				foreach (var p in changes)
				{
					// change setup
					selected[p.Key] = unlocked[p.Value].name;

					// reconfigure
					DoConfigure();
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
				if (!reconfigure_cs.Check(v))
				{
					Message.Post(Localizer.Format("#KERBALISM_Configure_noconfigure"), reconfigure_cs.Warning());
					return;
				}

				// warn the user about potential resource loss
				if (Resource_loss())
				{
					Message.Post(Severity.warning, Localizer.Format("#KERBALISM_Configure_dumpexcess"));
				}
			}

			// open the window
			UI.Open(Window_body);
		}


		bool Resource_loss()
		{
			// detect if any of the setup deal with resources
			// - we are ignoring resources that configured modules may generate on-the-fly
			//   this is okay for our current IConfigurable modules (habitat, process controller, harvester)
			//   however this will not be okay for something like a Container module, for example
			//   if the need arise, add a function bool change_resources() to the IConfigurable interface
			foreach (ConfigureSetup setup in setups)
			{
				foreach (ConfigureResource res in setup.resources)
				{
					if (Lib.Amount(part, res.name, true) > double.Epsilon) return true;
				}
			}
			return false;
		}


		// part tooltip
		public override string GetInfo()
		{
			return Specs().Info();
		}


		// specifics support
		public Specifics Specs()
		{
			InitSetups();

			Specifics specs = new Specifics();
			specs.Add("Slots", slots.ToString());
			specs.Add("Reconfigure", new CrewSpecs(reconfigure).Info());
			specs.Add(string.Empty);
			specs.Add("Setups:");

			// organize setups by tech required, and add the ones without tech
			Dictionary<string, List<string>> org = new Dictionary<string, List<string>>();
			foreach (ConfigureSetup setup in setups)
			{
				if (setup.tech.Length > 0)
				{
					if (!org.ContainsKey(setup.tech)) org.Add(setup.tech, new List<string>());
					org[setup.tech].Add(setup.name);
				}
				else
				{
					specs.Add(Lib.BuildString("• <b>", setup.name, "</b>"));
				}
			}

			// add setups grouped by tech
			foreach (var pair in org)
			{
				// shortcuts
				string tech_id = pair.Key;
				List<string> setup_names = pair.Value;

				// get tech title
				// note: non-stock technologies will return empty titles, so we use tech-id directly in that case

				// this works in KSP 1.6
				string tech_title = Localizer.Format(ResearchAndDevelopment.GetTechnologyTitle(tech_id));
				tech_title = !string.IsNullOrEmpty(tech_title) ? tech_title : tech_id;

				// this seems to have worked for KSP < 1.6
				if (tech_title.StartsWith("#", StringComparison.Ordinal))
					tech_title = Localizer.Format(ResearchAndDevelopment.GetTechnologyTitle(tech_id.ToLower()));

				// safeguard agains having #autoloc_1234 texts in the UI
				if (tech_title.StartsWith("#", StringComparison.Ordinal)) tech_title = tech_id;

				// add tech name
				specs.Add(string.Empty);
				specs.Add(Lib.BuildString("<color=#00ffff>", tech_title, ":</color>"));

				// add setup names
				foreach (string setup_name in setup_names)
				{
					specs.Add(Lib.BuildString("• <b>", setup_name, "</b>"));
				}
			}

			return specs;
		}


		public PartModule Find_module(ConfigureModule cm)
		{
			// for each module in the part
			int index = 0;
			foreach (PartModule m in part.Modules)
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
		void Window_body(Panel p)
		{
			// outside the editor
			if (!Lib.IsEditor())
			{
				// if part doesn't exist anymore
				if (FlightGlobals.FindPartByID(part.flightID) == null) return;
			}
			// inside the editor
			else
			{
				// if the part doesn't exist anymore (eg: removed, user hit undo)
				if (GetInstanceID() == 0) return;
			}

			// for each selected setup
			for (int selected_i = 0; selected_i < selected.Count; ++selected_i)
			{
				// find index in unlocked setups
				for (int setup_i = 0; setup_i < unlocked.Count; ++setup_i)
				{
					if (unlocked[setup_i].name == selected[selected_i])
					{
						// commit panel
						Render_panel(p, unlocked[setup_i], selected_i, setup_i);
					}
				}
			}

			// set metadata
			p.Title(Lib.BuildString("Configure <color=#cccccc>", Lib.Ellipsis(title, Styles.ScaleStringLength(40)), "</color>"));
			p.Width(Styles.ScaleWidthFloat(300.0f));
		}

		void Render_panel(Panel p, ConfigureSetup setup, int selected_i, int setup_i)
		{
			// generate details, just once
			// note: details were once elegantly serialized among all the other setup data,
			//       see comment inside generate_details() to understand why this was necessary instead
			setup.Generate_details(this);

			// render panel title
			// only allow reconfiguration if there are more setups than slots
			if (unlocked.Count <= selected.Count)
			{
				p.AddSection(Lib.Ellipsis(setup.name, Styles.ScaleStringLength(70)), setup.desc);
			}
			else
			{
				p.AddSection(Lib.Ellipsis(setup.name, Styles.ScaleStringLength(70)), setup.desc, () => Change_setup(-1, selected_i, ref setup_i), () => Change_setup(1, selected_i, ref setup_i));
			}

			// render details
			foreach (var det in setup.details)
			{
				p.AddContent(det.label, det.value);
			}
		}

		// utility, used as callback in panel select
		void Change_setup(int change, int selected_i, ref int setup_i)
		{
			if (setup_i + change == unlocked.Count) setup_i = 0;
			else if (setup_i + change < 0) setup_i = unlocked.Count - 1;
			else setup_i += change;
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
		public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) { return (float)extra_mass; }
		public ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
		public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

		// module info support
		public string GetModuleTitle() { return Lib.BuildString("<size=1><color=#00000000>00</color></size>Configurable ", title); } // attempt to display at the top
		public override string GetModuleDisplayName() { return Lib.BuildString("<size=1><color=#00000000>00</color></size>Configurable ", title); } // attempt to display at the top
		public string GetPrimaryField() { return Lib.BuildString("<size=1><color=#00000000>00</color></size>Configurable ", title); } // attempt to display at the top
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
			foreach (var module_node in node.GetNodes("MODULE"))
			{
				modules.Add(new ConfigureModule(module_node));
			}

			// parse resources
			resources = new List<ConfigureResource>();
			foreach (var res_node in node.GetNodes("RESOURCE"))
			{
				resources.Add(new ConfigureResource(res_node));
			}
		}

		public ConfigureSetup(ConfigureSetup other)
		{
			name = other.name;
			desc = other.desc;
			tech = other.tech;
			cost = other.cost;
			mass = other.mass;

			modules = new List<ConfigureModule>();
			foreach (var m in other.modules)
				modules.Add(new ConfigureModule(m));	

			// load resources
			resources = new List<ConfigureResource>();
			foreach (var r in other.resources)
				resources.Add(new ConfigureResource(r));	
		}

		public void Generate_details(Configure cfg)
		{
			// If a setup component is defined after the Configure module in the ConfigNode,
			// then it is not present in the part during Configure::OnLoad (at precompilation time)
			// so, find_module() will fail in that situation, resulting in no component details
			// being added to the Configure window. Therefore we are forced to generate the details
			// at first use every time the module is loaded, instead of generating them only once.

			// already generated
			if (details != null) return;

			// generate module details
			details = new List<Detail>();
			foreach (ConfigureModule cm in modules)
			{
				// find module, skip if it doesn't exist
				PartModule m = cfg.Find_module(cm);
				if (m == null) continue;

				// get title
				string title = m is IModuleInfo module_info ? module_info.GetModuleTitle() : cm.type;
				if (title.Length == 0) continue;

				// get specs, skip if not implemented by module
				if (!(m is ISpecifics specifics))
					continue;
				Specifics specs = specifics.Specs();
				if (specs.entries.Count == 0) continue;

				// add title to details
				details.Add(new Detail(Lib.BuildString("<b><color=#00ffff>", title, "</color></b>")));

				// add specs to details
				foreach (Specifics.Entry e in specs.entries)
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
				foreach (ConfigureResource cr in visible_resources)
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

		public class Detail
		{
			public Detail()
			{ }

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
			type = Lib.ConfigValue(node, "type", string.Empty);
			id_field = Lib.ConfigValue(node, "id_field", string.Empty);
			id_value = Lib.ConfigValue(node, "id_value", string.Empty);
			id_index = Lib.ConfigValue(node, "id_index", 0);
		}

		public ConfigureModule(ConfigureModule other)
		{
			type = other.type;
			id_field = other.id_field;
			id_value = other.id_value;
			id_index = other.id_index;
		}

		public string type;
		public string id_field;
		public string id_value;
		public int id_index;
	}


	public sealed class ConfigureResource
	{
		public ConfigureResource(ConfigNode node)
		{
			name = Lib.ConfigValue(node, "name", string.Empty);
			amount = Lib.ConfigValue(node, "amount", string.Empty);
			maxAmount = Lib.ConfigValue(node, "maxAmount", string.Empty);
		}

		public ConfigureResource(ConfigureResource other)
		{
			name = other.name;
			amount = other.amount;
			maxAmount = other.maxAmount;
		}

		public string name;
		public string amount;
		public string maxAmount;
	}

} // KERBALISM

