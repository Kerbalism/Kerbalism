using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


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

		[KSPField] public bool disableInEditor = false;   // phasing-out old configs: disable this module when it is loaded in the editor

		// persistence/config
		// note: the running state doesn't need to be serialized, as it can be deduced from resource flow
		// but we find useful to set running to true in the cfg node for some processes, and not others
		[KSPField(isPersistant = true)] public bool running;

		// index of currently active dump valve
		[KSPField(isPersistant = true)] public int valve_i = 0;

		// caching of GetInfo() for automation tooltip
		public string ModuleInfo { get; private set; }

		private DumpSpecs dump_specs;
		private bool broken = false;
		private bool isConfigurable = false;

		public override void OnLoad(ConfigNode node)
		{
			ModuleInfo = GetInfo();
		}

		public void Start()
		{
			// don't break tutorial scenarios
			if (Lib.DisableScenario(this))
				return;

			if (disableInEditor && Lib.IsEditor())
			{
				enabled = false;
				isEnabled = false;
				return;
			}

			// configure on start, must be executed with enabled true on parts first load.
			Configure(true);

			// get dump specs for associated process
			dump_specs = Profile.processes.Find(x => x.modifiers.Contains(resource)).dump;

			// set dump valve ui button
			Events["DumpValve"].active = dump_specs.AnyValves;

			// set active dump valve
			dump_specs.ValveIndex = valve_i;
			valve_i = dump_specs.ValveIndex;

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
			if (disableInEditor && Lib.IsEditor())
			{
				enabled = false;
				isEnabled = false;
				return;
			}

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
			// update rmb ui
			Events["Toggle"].guiName = Lib.StatusToggle(title, broken ? Local.ProcessController_broken : running ? Local.ProcessController_running : Local.ProcessController_stopped);//"broken""running""stopped"
			Events["DumpValve"].guiName = Lib.StatusToggle(Local.ProcessController_Dump, dump_specs.valves[valve_i]);//"Dump"
		}

#if KSP15_16
		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = true)]
#else
		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = true, groupName = "Processes", groupDisplayName = "#KERBALISM_Group_Processes")]//Processes
#endif
		public void Toggle()
		{
			SetRunning(!running);
		}

#if KSP15_16
		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Dump", active = true)]
#else
		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "#KERBALISM_ProcessController_Dump", active = true, groupName = "Processes", groupDisplayName = "#KERBALISM_Group_Processes")]//"Dump""Processes"
#endif
		public void DumpValve()
		{
			valve_i = dump_specs.NextValve;

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

		// specifics support
		public Specifics Specs()
		{
			Process process = Profile.processes.Find(k => k.modifiers.Contains(resource));
			return ModuleKsmProcessController.Specifics(process, capacity);
		}

		// part tooltip
		public override string GetInfo()
		{
			if (disableInEditor)
				return string.Empty;

			if (isConfigurable)
				return string.Empty;

			return Specs().Info(desc);
		}

		public bool IsRunning() {
			return running;
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

