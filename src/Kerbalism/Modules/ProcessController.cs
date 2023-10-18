using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;
using Smooth.Algebraics;
using System.Diagnostics;


namespace KERBALISM
{

	public class ProcessController: PartModule, IModuleInfo, IAnimatedModule, ISpecifics, IConfigurable
	{
		// config
		[KSPField] public string resource = string.Empty; // pseudo-resource to control
		[KSPField] public string title = string.Empty;    // name to show on ui
		[KSPField] public string desc = string.Empty;     // description to show on tooltip
		[KSPField] public double capacity = 1.0;          // amount of associated pseudo-resource
		[KSPField] public bool toggle = true;             // show the enable/disable toggle button

		// persistence/config
		// note: the running state doesn't need to be serialized, as it can be deduced from resource flow
		// but we find useful to set running to true in the cfg node for some processes, and not others
		[KSPField(isPersistant = true)] public bool running;

		// index of the default dump valve
		[KSPField] public int valve_i = 0;

		// caching of GetInfo() for automation tooltip
		public string ModuleInfo { get; private set; }

		private DumpSpecs.ActiveValve dumpValve;
		private int persistentValveIndex = -1;
		private bool broken = false;
		private bool isConfigurable = false;

		public override void OnLoad(ConfigNode node)
		{
			// Set the process default valve according to the default valve defined in the module.
			// This is silly and can cause multiple module configs to fight over what the default
			// value should be. This config setting should really be in the process definition,
			// but it's in the module for backward compatibility reasons.
			if (valve_i > 0 && HighLogic.LoadedScene == GameScenes.LOADING)
			{
				Process process = Profile.processes.Find(x => x.modifiers.Contains(resource));
				process.defaultDumpValve.ValveIndex = valve_i;
				process.defaultDumpValveIndex = process.defaultDumpValve.ValveIndex;
			}

			node.TryGetValue(nameof(persistentValveIndex), ref persistentValveIndex);
		}

		public override void OnSave(ConfigNode node)
		{
			if (dumpValve != null)
				node.AddValue(nameof(persistentValveIndex), dumpValve.ValveIndex);
		}

		public void Start()
		{
			// don't break tutorial scenarios
			if (Lib.DisableScenario(this))
				return;

			// configure on start, must be executed with enabled true on parts first load.
			Configure(true);

			// The dump valve instance is acquired either from the game-wide instance in the editor
			// or from the persisted VesselData instance in flight.
			// Since we have no means to persist vessel-level dump settings in a ShipConstruct
			// (aka in the editor), we persist the dump valve index in the module and use it unless
			// the VesselData ActiveValve instance exists already.
			// Note that this system can lead to weirdness in the editor. Since dump spec is global
			// and never reset, new vessels will use whatever valve was last active. Also, loading
			// a subassembly or merging a craft will result in the current craft to be updated to
			// whatever the setting is on the loaded craft. This isn't really fixable without
			// implementing a vessel-level persistence mechanism for ShipConstructs.
			Process process = Profile.processes.Find(x => x.modifiers.Contains(resource));
			if (Lib.IsEditor() || vessel == null)
			{
				dumpValve = process.defaultDumpValve;
				if (persistentValveIndex > -1)
					dumpValve.ValveIndex = persistentValveIndex;
			}
			else
			{
				VesselData vd = vessel.GetVesselData();
				if (!vd.dumpValves.TryGetValue(process, out dumpValve))
				{
					dumpValve = new DumpSpecs.ActiveValve(process.dump);
					dumpValve.ValveIndex = persistentValveIndex > -1 ? persistentValveIndex : valve_i;
					vd.dumpValves.Add(process, dumpValve);
				}
			}

			// set dump valve ui button
			Events["DumpValve"].active = dumpValve.CanSwitchValves;

			// set action group ui
			Actions["Action"].guiName = Lib.BuildString(Local.ProcessController_Start_Stop, " ", title);//"Start/Stop

			// hide toggle if specified
			Events["Toggle"].active = toggle;
			Actions["Action"].active = toggle;

			// deal with non-togglable processes
			if (!toggle)
				running = true;

			// set processes enabled state
			Lib.SetProcessEnabledDisabled(part, resource, broken ? false : running, capacity);
		}

		///<summary> Called by Configure.cs. Configures the controller to settings passed from the configure module</summary>
		public void Configure(bool enable)
		{
			if (enable)
			{
				// if never set
				// - this is the case in the editor, the first time, or in flight
				//   in the case the module was added post-launch, or EVA kerbals
				if (!part.Resources.Contains(resource))
				{
					// add the resource
					// - always add the specified amount, even in flight
					Lib.AddResource(part, resource, (!broken && running) ? capacity : 0.0, capacity);
				}
			}
			else
				Lib.RemoveResource(part, resource, 0.0, capacity);
		}

		public void ModuleIsConfigured() => isConfigurable = true;

		///<summary> Call this when process controller breaks down or is repaired </summary>
		public void ReliablityEvent(bool breakdown)
		{
			broken = breakdown;
			Lib.SetProcessEnabledDisabled(part, resource, broken ? false : running, capacity);
		}

		public void Update()
		{
			if (!part.IsPAWVisible())
				return;

			// update rmb ui
			Events["Toggle"].guiName = Lib.StatusToggle(title, broken ? Local.ProcessController_broken : running ? Local.ProcessController_running : Local.ProcessController_stopped);//"broken""running""stopped"
			Events["DumpValve"].guiName = Lib.StatusToggle(Local.ProcessController_Dump, dumpValve.ValveTitle);//"Dump"
		}

		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = true, groupName = "Processes", groupDisplayName = "#KERBALISM_Group_Processes")]//Processes
		public void Toggle()
		{
			SetRunning(!running);
		}

		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "#KERBALISM_ProcessController_Dump", active = true, groupName = "Processes", groupDisplayName = "#KERBALISM_Group_Processes")]//"Dump""Processes"
		public void DumpValve()
		{
			dumpValve.NextValve();

			// refresh VAB/SPH ui
			if (Lib.IsEditor()) GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
		}

		public void SetRunning(bool value)
		{
			if (broken)
				return;
			
			// switch status
			running = value;
			Lib.SetProcessEnabledDisabled(part, resource, running, capacity);

			// refresh VAB/SPH ui
			if (Lib.IsEditor()) GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
		}

		// action groups
		[KSPAction("_")] public void Action(KSPActionParam param) { Toggle(); }

		// part tooltip
		public override string GetInfo()
		{
			return isConfigurable ? string.Empty : Specs().Info(desc);
		}

		public bool IsRunning() {
			return running;
		}

		// specifics support
		public Specifics Specs()
		{
			Specifics specs = new Specifics();
			Process process = Profile.processes.Find(k => k.modifiers.Contains(resource));
			if (process != null)
			{
				foreach (KeyValuePair<string, double> pair in process.inputs)
				{
					if (!process.modifiers.Contains(pair.Key))
						specs.Add(Lib.GetResourceDisplayName(pair.Key), Lib.BuildString(" <color=#ffaa00>", Lib.HumanOrSIRate(pair.Value * capacity, pair.Key.GetHashCode()), "</color>"));
					else
						specs.Add(Local.ProcessController_info1, Lib.HumanReadableDuration(0.5 / pair.Value));//"Half-life"
				}
				foreach (KeyValuePair<string, double> pair in process.outputs)
				{
					specs.Add(Lib.GetResourceDisplayName(pair.Key), Lib.BuildString(" <color=#00ff00>", Lib.HumanOrSIRate(pair.Value * capacity, pair.Key.GetHashCode()), "</color>"));
				}
			}
			return specs;
		}

		// module info support
		public string GetModuleTitle() { return Lib.BuildString("<size=1><color=#00000000>01</color></size>", title); }  // Display after config widget
		public override string GetModuleDisplayName() { return Lib.BuildString("<size=1><color=#00000000>01</color></size>", title); }  // Display after config widget
		public string GetPrimaryField() { return string.Empty; }
		public Callback<Rect> GetDrawModulePanelCallback() { return null; }

		// animation group support
		public void EnableModule() { }
		public void DisableModule() { }
		public bool ModuleIsActive() { return broken ? false : running; }
		public bool IsSituationValid() { return true; }

	}


} // KERBALISM

