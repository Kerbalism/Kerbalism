using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{


	public sealed class Sickbay: PartModule, IModuleInfo, ISpecifics
	{
		// config
		[KSPField] public string resource = string.Empty; // pseudo-resource to control
		[KSPField] public string rule = string.Empty;     // which rule to affect
		[KSPField] public double rate = 0.0;              // healing rate
		[KSPField] public string title = string.Empty;    // name to show on ui
		[KSPField] public string desc = string.Empty;     // description to show on tooltip
		[KSPField] public int capacity = 1;               // how many kerbals can be healed at once

		[KSPField(isPersistant = true)] public bool running;
		[KSPField(isPersistant = true)] public string kerbals;

		public void Start()
		{
			// don't break tutorial scenarios
			if (Lib.DisableScenario(this))
				return;

			// configure on start
			Configure(false);

			// set action group ui
			Actions["Action"].guiName = Lib.BuildString("Start/Stop ", title);

			UpdateActions();
		}

		public void Configure(bool enable)
		{
			// do something useful here (look at ProcessController)
		}

		public void Update()
		{
			// update flow mode of resource
			// note: this has to be done constantly to prevent the user from changing it
			Lib.SetResourceFlow(part, resource, running);

			// update rmb ui
			Events["Toggle"].guiName = Lib.StatusToggle(title, running ? "healing" : "idle");

			foreach(ProtoCrewMember crew in part.protoModuleCrew) {
			//	crew.get
			}

			UpdateActions();
		}

		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = true)]
		public void Toggle()
		{
			// switch status
			running = !running;
		}

		// action groups
		[KSPAction("_")] public void Action(KSPActionParam param) { Toggle(); }
	
		private void UpdateActions()
		{
			Events["Toggle"].active = part.protoModuleCrew.Count > 0;
			Actions["Action"].active = part.protoModuleCrew.Count > 0;

			//if (cleaner && (!researcher_cs || researcher_cs.Check(part.protoModuleCrew))) Events["CleanExperiments"].active = true;
			//else Events["CleanExperiments"].active = false;
		}

		// part tooltip
		public override string GetInfo()
		{
			return Specs().Info(desc);
		}

		// specifics support
		public Specifics Specs()
		{
			Specifics specs = new Specifics();
			specs.Add("Capacity", capacity + " Kerbals");
			return specs;
		}

		// module info support
		public string GetModuleTitle() { return Lib.BuildString("<size=1><color=#00000000>01</color></size>", title); }  // Display after config widget
		public override string GetModuleDisplayName() { return Lib.BuildString("<size=1><color=#00000000>01</color></size>", title); }  // Display after config widget
		public string GetPrimaryField() { return string.Empty; }
		public Callback<Rect> GetDrawModulePanelCallback() { return null; }
	}


} // KERBALISM

