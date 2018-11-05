using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{


	public sealed class ProcessController: PartModule, IModuleInfo, IAnimatedModule, ISpecifics, IConfigurable
	{
		// config
		[KSPField] public string resource = string.Empty; // pseudo-resource to control
		[KSPField] public string title = string.Empty;    // name to show on ui
		[KSPField] public string desc = string.Empty;     // description to show on tooltip
		[KSPField] public double capacity = 1.0;          // amount of associated pseudo-resource
		[KSPField] public bool toggle = true;             // show the enable/disable toggle

		// persistence/config
		// note: the running state doesn't need to be serialized, as it can be deduced from resource flow
		// but we find useful to set running to true in the cfg node for some processes, and not others
		[KSPField(isPersistant = true)] public bool running;

		// amount of times to multiply capacity, used so configure can have the same process in more than one slot
		[KSPField(isPersistant = true)] public int multiple = 1;

		// index of currently active dump valve
		[KSPField(isPersistant = true)] public int valve_i = 0;

		private DumpSpecs dump_specs;

		public void Start()
		{
			// don't break tutorial scenarios
			if (Lib.DisableScenario(this))
				return;

			// get dump specs for associated process
			dump_specs = Profile.processes.Find(x => x.modifiers.Contains(resource)).dump;

			// set dump valve ui button
			Events["DumpValve"].active = dump_specs.AnyValves;

			// set active dump valve
			dump_specs.ValveIndex = valve_i;
			valve_i = dump_specs.ValveIndex;

			// set action group ui
			Actions["Action"].guiName = Lib.BuildString("Start/Stop ", title);

			// hide toggle if specified
			Events["Toggle"].active = toggle;
			Actions["Action"].active = toggle;

			// deal with non-togglable processes
			if (!toggle)
				running = true;

			// configure on start
			Configure(running, multiple);
		}

		public void Configure(bool enable, int multiple = 1)
		{
			// make sure multiple is not zero
			multiple = multiple == 0 ? 1 : multiple;
			Lib.SetProcessEnabledDisabled(part, resource, enable, multiple);
		}


		public void Update()
		{
			// update rmb ui
			Events["Toggle"].guiName = Lib.StatusToggle(title, running ? "running" : "stopped");
			Events["DumpValve"].guiName = Lib.StatusToggle("Dump", dump_specs.valves[valve_i]);
		}


		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = true)]
		public void Toggle()
		{
			// switch status
			running = !running;
			Configure(running, multiple);
		}

		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Dump", active = true)]
		public void DumpValve() { valve_i = dump_specs.NextValve; }

		// action groups
		[KSPAction("_")] public void Action(KSPActionParam param) { Toggle(); }


		// part tooltip
		public override string GetInfo()
		{
			return Specs().Info(desc);
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
						specs.Add(pair.Key, Lib.BuildString("<color=#ffaa00>", Lib.HumanReadableRate(pair.Value * capacity), "</color>"));
					else
						specs.Add("Half-life", Lib.HumanReadableDuration(0.5 / pair.Value));
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
		public bool ModuleIsActive() { return running; }
		public bool IsSituationValid() { return true; }
	}


} // KERBALISM

