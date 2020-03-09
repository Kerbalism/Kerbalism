using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;
using KERBALISM.Planner;

namespace KERBALISM
{
	public class PassiveShield: PartModule, IPartMassModifier, IBackgroundModule, IPlannerModule
	{
		// config
		[KSPField] public string title = Local.PassiveShield_Sandbags;//"Sandbags"              // GUI name of the status action in the PAW
		[KSPField] public string engageActionTitle = Local.PassiveShield_fill;//"fill"      // what the deploy action should be called
		[KSPField] public string disengageActionTitle = Local.PassiveShield_empty;//"empty"  // what the empty action should be called
		[KSPField] public string disabledTitle = Local.PassiveShield_stowed;// "stowed"       // what to display in the status text while not deployed

		[KSPField] public bool toggle = true;                     // true if the effect can be toggled on/off
		[KSPField] public string animation;                       // name of animation to play when enabling/disabling
		[KSPField] public float added_mass = 1.5f;                // mass added when deployed, in tons
		[KSPField] public bool require_eva = true;                // true if only accessible by EVA
		[KSPField] public bool require_landed = true;             // true if only deployable when landed
		[KSPField] public string crew_operate = "true";           // operator crew requirement. true means anyone

		// persisted for simplicity, so that the values are available in Total() below
		[KSPField(isPersistant = true)] public double radiation;                       // radiation effect in rad/s
		[KSPField(isPersistant = true)] public double ec_rate = 0;                     // EC consumption rate per-second (optional)

		// persistent
		[KSPField(isPersistant = true)] public bool deployed = false; // currently deployed


		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "_", groupName = "Radiation", groupDisplayName = "#KERBALISM_Group_Radiation")]//Radiation
		// rmb status
		public string Status;  // rate of radiation emitted/shielded

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
				Events["Toggle"].guiActive = !require_eva || Lib.IsEditor();
			}

			// deal with non-toggable
			if (!toggle)
			{
				deployed = true;
			}

			// create animator
			deploy_anim = new Animator(part, animation);

			// set animation initial state
			deploy_anim.Still(deployed ? 1f : 0f);

			deploy_cs = new CrewSpecs(crew_operate);
		}

		public void Update()
		{
			// update ui
			Status = deployed ? Lib.BuildString(Local.PassiveShield_absorbing ," ", Lib.HumanReadableRadiation(Math.Abs(radiation))) : disabledTitle;//"absorbing
			Events["Toggle"].guiName = Lib.StatusToggle(title, deployed ? disengageActionTitle : engageActionTitle);
		}

		public void FixedUpdate()
		{
			// do nothing else in the editor
			if (Lib.IsEditor()) return;

			// allow sandbag filling only when landed
			bool allowDeploy = vessel.Landed || !require_landed;
			Events["Toggle"].active = toggle && (allowDeploy || deployed);

			if (deployed && ec_rate > 0)
				vessel.KerbalismData().ResHandler.ElectricCharge.Consume(ec_rate * Kerbalism.elapsed_s, ResourceBroker.PassiveShield);
		}

		public void BackgroundUpdate(VesselData vd, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule, double elapsed_s)
		{
			if (ec_rate == 0.0 || !Lib.Proto.GetBool(protoModule, "deployed"))
				return;

			vd.ResHandler.ElectricCharge.Consume(ec_rate * elapsed_s, ResourceBroker.GetOrCreate(title));
		}

		public void PlannerUpdate(VesselResHandler resHandler, EnvironmentAnalyzer environment, VesselAnalyzer vessel)
		{
			if (deployed && ec_rate > 0)
				resHandler.ElectricCharge.Consume(ec_rate, ResourceBroker.GetOrCreate(title));
		}

		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = true, groupName = "Radiation", groupDisplayName = "#KERBALISM_Group_Radiation")]//Radiation
		public void Toggle()
		{
			if (Lib.IsFlight())
			{

				// disable for dead eva kerbals
				Vessel v = FlightGlobals.ActiveVessel;
				if (v == null || EVA.IsDead(v)) return;
				if (!deploy_cs.Check(v))
				{
					Message.Post
					(
					  Lib.TextVariant
					  (
						Local.PassiveShield_MessagePost//"I don't know how this works!"
					  ),
					  deploy_cs.Warning()
					);
					return;
				}
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
			VesselResource ec = v.KerbalismData().ResHandler.GetResource("ElectricCharge");

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

