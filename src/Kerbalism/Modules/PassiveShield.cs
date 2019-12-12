using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;

namespace KERBALISM
{
	public class PassiveShield: PartModule, IPartMassModifier
	{
		// config
		[KSPField] public string title = "Sandbags";              // GUI name of the status action in the PAW
		[KSPField] public string engageActionTitle = "fill";      // what the deploy action should be called
		[KSPField] public string disengageActionTitle = "empty";  // what the empty action should be called
		[KSPField] public string disabledTitle = "stowed";        // what to display in the status text while not deployed

		[KSPField] public bool toggle;                            // true if the effect can be toggled on/off
		[KSPField] public string animation;                       // name of animation to play when enabling/disabling
		[KSPField] public float added_mass = 1.5f;                // mass added when deployed, in tons
		[KSPField] public bool require_eva = true;                // true if only accessible by EVA
		[KSPField] public string crew_operate = "true";           // operator crew requirement. true means anyone

		// persisted for simplicity, so that the values are available in Total() below
		[KSPField(isPersistant = true)] public double radiation;                       // radiation effect in rad/s
		[KSPField(isPersistant = true)] public double ec_rate = 0;                     // EC consumption rate per-second (optional)

		// persistent
		[KSPField(isPersistant = true)] public bool deployed = false; // currently deployed

		// rmb status
		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "_")] public string Status;  // rate of radiation emitted/shielded

		Animator deploy_anim;
		CrewSpecs deploy_cs;

		// pseudo-ctor
		public override void OnStart(StartState state)
		{
			// don't break tutorial scenarios
			if (Lib.DisableScenario(this)) return;

			// update RMB ui
			Fields["Status"].guiName = title;
			Actions["Action"].active = toggle;

			if(toggle)
			{
				Events["Toggle"].guiActiveUnfocused = require_eva;
				Events["Toggle"].guiActive = !require_eva;
			}

			// deal with non-toggable
			if (!toggle)
			{
				deployed = true;
			}

			// create animator
			deploy_anim = new Animator(part, animation);

			// set animation initial state
			deploy_anim.Still(deployed ? 1.0 : 0.0);

			deploy_cs = new CrewSpecs(crew_operate);
		}

		public void Update()
		{
			// update ui
			Status = deployed ? Lib.BuildString("absorbing ", Lib.HumanReadableRadiation(Math.Abs(radiation))) : disabledTitle;
			Events["Toggle"].guiName = Lib.StatusToggle(title, deployed ? disengageActionTitle : engageActionTitle);
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
			PassiveShield sandbags = pm as PassiveShield;
			if (sandbags == null) return;
			if (sandbags.ec_rate == 0) return;

			bool deployed = Lib.Proto.GetBool(m, "deployed");
			if (!deployed) return;

			ResourceCache.GetResource(v, "ElectricCharge").Consume(sandbags.ec_rate * elapsed_s, sandbags.title);
		}

		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = true)]
		public void Toggle()
		{
			// disable for dead eva kerbals
			Vessel v = FlightGlobals.ActiveVessel;
			if (v == null || EVA.IsDead(v)) return;

			if(!deploy_cs.Check(v))
			{
				Message.Post
				(
				  Lib.TextVariant
				  (
					"I don't know how this works!"
				  ),
				  deploy_cs.Warning()
				);
				return;
			}

			// switch status
			deployed = !deployed;

			// play animation
			deploy_anim.Play(!deployed, false);

			// refresh VAB/SPH ui
			if (Lib.IsEditor()) GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
		}

		// action groups
		[KSPAction("Toggle")] public void Action(KSPActionParam param) { Toggle(); }


		// return total radiation emitted in a vessel
		public static double Total(Vessel v)
		{
			// get resource cache
			ResourceInfo ec = ResourceCache.GetResource(v, "ElectricCharge");

			double total = 0.0;
			if (v.loaded)
			{
				foreach (var shield in Lib.FindModules<PassiveShield>(v))
				{
					if (ec.Amount > 0 || shield.ec_rate <= 0)
					{
						if (shield.deployed)
							total += shield.radiation;
					}
				}
			}
			else
			{
				foreach (ProtoPartModuleSnapshot m in Lib.FindModules(v.protoVessel, "PassiveShield"))
				{
					if (ec.Amount > 0 || Lib.Proto.GetDouble(m, "ec_rate") <= 0)
					{
						if (Lib.Proto.GetBool(m, "deployed"))
						{
							var rad = Lib.Proto.GetDouble(m, "radiation");
							total += rad;
						}
					}
				}
			}
			return total;
		}

		// mass change support
		public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) { return deployed ? added_mass : 0.0f; }
		public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
	}

} // KERBALISM

