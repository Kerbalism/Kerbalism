using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;

namespace KERBALISM
{
	public class Sandbags : PartModule, IPartMassModifier
	{
		// config
		[KSPField] public string title = "Sandbags";              // GUI name of the status action in the PAW
		[KSPField] public string enableTitle = "Fill Sandbags";
		[KSPField] public string disableTitle = "Empty Sandbags";
		[KSPField] public double radiation;                       // radiation effect in rad/s
		[KSPField] public double ec_rate = 0;                     // EC consumption rate per-second (optional)
		[KSPField] public bool toggle;                            // true if the effect can be toggled on/off
		[KSPField] public string animation;                       // name of animation to play when enabling/disabling
		[KSPField] public float added_mass = 1;                   // mass added when full, in tons


		// persistent
		[KSPField(isPersistant = true)] public bool deployed = false; // currently deployed

		// rmb status
		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "_")] public string Status;  // rate of radiation emitted/shielded

		// animations
		Animator active_anim;

		// pseudo-ctor
		public override void OnStart(StartState state)
		{
			// don't break tutorial scenarios
			if (Lib.DisableScenario(this)) return;

			// update RMB ui
			Fields["Status"].guiName = title;
			Events["Toggle"].active = toggle;
			Actions["Action"].active = toggle;

			// deal with non-toggable
			if (!toggle)
			{
				deployed = true;
			}

			// create animator
			active_anim = new Animator(part, animation);

			// set animation initial state
			active_anim.Still(deployed ? 1.0 : 0.0);
		}

		public void Update()
		{
			// update ui
			Status = deployed ? Lib.HumanReadableRadiation(Math.Abs(radiation)) : "none";
			Events["Toggle"].guiName = Lib.StatusToggle(title, deployed ? enableTitle : disableTitle);
		}

		public void FixedUpdate()
		{
			// do nothing else in the editor
			if (Lib.IsEditor()) return;

			// allow sandbag filling only when landed
			Events["Toggle"].active = toggle && (vessel.Landed || deployed);

			// if there is ec consumption
			if (deployed && ec_rate != 0)
			{
				// get resource cache
				ResourceCache.GetResource(vessel, "ElectricCharge").Consume(ec_rate * Kerbalism.elapsed_s, title);
			}
		}

		public static void BackgroundUpdate(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, PartModule pm, Part part, double elapsed_s)
		{
			Sandbags sandbags = pm as Sandbags;
			if (sandbags == null) return;
			if (sandbags.ec_rate == 0) return;

			bool deployed = Lib.Proto.GetBool(m, "deployed");
			if (!deployed) return;

			ResourceCache.GetResource(v, "ElectricCharge").Consume(sandbags.ec_rate * elapsed_s, sandbags.title);
		}

		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = true)]
		public void Toggle()
		{
			// switch status
			deployed = !deployed;

			// play animation
			active_anim.Play(deployed, false);

			// refresh VAB/SPH ui
			if (Lib.IsEditor()) GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
		}

		// action groups
		[KSPAction("Toggle")] public void Action(KSPActionParam param) { Toggle(); }

		// mass change support
		public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) { return deployed ? added_mass : 0.0f; }
		public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
	}

} // KERBALISM

