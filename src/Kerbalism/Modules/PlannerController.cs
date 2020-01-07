using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{

	public class PlannerController : PartModule
	{
		// config
		[KSPField] public bool toggle = true;                       // true to show the toggle button in editor
		[KSPField] public string title = string.Empty;              // name to show on the button

		// persistence
		[KSPField(isPersistant = true)] public bool considered;     // true to consider the part modules in planner


		public override void OnStart(StartState state)
		{
			// don't break tutorial scenarios
			if (Lib.DisableScenario(this)) return;

			if (Lib.IsEditor())
			{
				Events["Toggle"].active = toggle;
			}
		}

		void Update()
		{
			Events["Toggle"].guiName = Lib.StatusToggle
			(
			  String.Format("Simulate {0} in planner", title),//
			  considered ? "<b><color=#00ff00>"+Localizer.Format("#KERBALISM_PlannerController_yes") +"</color></b>" : "<b><color=#ffff00>"+Localizer.Format("#KERBALISM_PlannerController_no") +"</color></b>"//yes  no
			);
		}

		[KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "_", active = true)]
		public void Toggle()
		{
			considered = !considered;

			// refresh VAB/SPH ui
			if (Lib.IsEditor()) GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
		}
	}


} // KERBALISM

