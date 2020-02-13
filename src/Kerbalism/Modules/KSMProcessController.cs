using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{
	/// <summary>
	/// Replacement for ProcessController
	/// </summary>
	public class KSMProcessController: PartModule, IModuleInfo, IAnimatedModule, ISpecifics
	{
		// config
		[KSPField] public string resource = string.Empty; // pseudo-resource to control
		[KSPField] public string title = string.Empty;    // name to show on ui
		[KSPField] public string desc = string.Empty;     // description to show on tooltip
		[KSPField] public double capacity = 1.0;          // amount of associated pseudo-resource
		[KSPField] public bool toggle = true;             // show the enable/disable toggle button
		[KSPField] public string id = string.Empty;       // needed for B9PS

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

		// When switching a process controller via B9PS, it will call OnLoad
		// with the new prefab data. Remember which resource we've added,
		// so we can remove the old resource before we add a new one after being
		// switched to a different type.
		[KSPField(isPersistant = true)] string createdResource = string.Empty;

		public override void OnLoad(ConfigNode node)
		{
			ModuleInfo = GetInfo();

			if (Lib.IsEditor())
			{
				if (string.IsNullOrEmpty(resource))
				{
					Lib.Log("ProcessController " + id + ": deactivating");
					Deactivate();
					return;
				}

				InitProcess();
			}
		}

		protected void Deactivate()
		{
			Lib.Log("ProcessController " + id + ": shutting down, bye");
			RemoveResource();
			resource = string.Empty;
			isEnabled = false;
			enabled = false;
			Events["DumpValve"].active = false;
			Events["Toggle"].active = false;
			Actions["Action"].active = false;
		}

		protected void InitProcess()
		{
			Lib.Log("ProcessController " + id + ": init process");
			isEnabled = true;
			enabled = true;

			if (!string.IsNullOrEmpty(createdResource) && createdResource != resource)
			{
				RemoveResource();
			}
			CreateResource();

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
			Lib.AddResource(part, resource, running ? capacity : 0.0, 0.0, true);
		}

		public void Start()
		{
			// don't break tutorial scenarios
			if (Lib.DisableScenario(this))
				return;

			if (string.IsNullOrEmpty(resource))
			{
				Deactivate();
				return;
			}

			InitProcess();
		}

		protected void CreateResource()
		{
			// if never set
			// - this is the case in the editor, the first time, or in flight
			//   in the case the module was added post-launch, or EVA kerbals
			// - resource will be != createdResource when controller was switched
			//   to a different type in the editor
			if (createdResource != resource)
			{
				RemoveResource();

				Lib.Log("ProcessController " + id + ": new resource, creating " + resource);

				// add the resource
				// - always add the specified amount, even in flight
				double amount = (!broken && running) ? capacity : 0.0;
				Lib.AddResource(part, resource, 0.0, capacity, true);
				createdResource = resource;
			}
			else
			{
				Lib.Log("ProcessController " + id + ": already created resource, not creating again " + resource);
			}
		}

		protected void RemoveResource()
		{
			if (!string.IsNullOrEmpty(createdResource))
			{
				Lib.Log("ProcessController " + id + ": removing previously created resource " + createdResource);
				Lib.RemoveResource(part, createdResource, 0.0, capacity);
				createdResource = string.Empty;
			}
		}

		///<summary> Call this when process controller breaks down or is repaired </summary>
		public void ReliablityEvent(bool breakdown)
		{
			double oldCapacity = !broken && running ? capacity : 0.0;
			broken = breakdown;
			double newCapacity = !broken && running ? capacity : 0.0;
			double capacityChange = newCapacity - oldCapacity;
			Lib.AddResource(part, resource, capacityChange, 0.0, true);
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

			double oldCapacity = running ? capacity : 0.0;
			running = value;
			double newCapacity = running ? capacity : 0.0;
			Lib.AddResource(part, resource, newCapacity - oldCapacity, 0.0, true);

			// refresh VAB/SPH ui
			if (Lib.IsEditor()) GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
		}

		// action groups
		[KSPAction("_")] public void Action(KSPActionParam param) { Toggle(); }

		// part tooltip
		public override string GetInfo()
		{
			if (string.IsNullOrEmpty(desc))
				return string.Empty;
			return Specs().Info(desc);
		}

		public bool IsRunning() {
			return running;
		}

		// specifics support
		public Specifics Specs()
		{
			Process process = Profile.processes.Find(k => k.modifiers.Contains(resource));
			return Specifics(process, capacity);
		}

		public static Specifics Specifics(Process process, double capacity)
		{
			Specifics specs = new Specifics();
			if (process != null)
			{
				foreach (KeyValuePair<string, double> pair in process.inputs)
				{
					if (!process.modifiers.Contains(pair.Key))
						specs.Add(pair.Key, Lib.BuildString("<color=#ffaa00>", Lib.HumanReadableRate(pair.Value * capacity), "</color>"));
					else
						specs.Add(Local.ProcessController_info1, Lib.HumanReadableDuration(0.5 / pair.Value));//"Half-life"
				}
				foreach (KeyValuePair<string, double> pair in process.outputs)
				{
					specs.Add(pair.Key, Lib.BuildString("<color=#00ff00>", Lib.HumanReadableRate(pair.Value * capacity), "</color>"));
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

