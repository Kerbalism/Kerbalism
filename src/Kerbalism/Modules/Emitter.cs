using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;

namespace KERBALISM
{
	public class Emitter : PartModule, ISpecifics
	{
		// config
		[KSPField(isPersistant = true)] public double radiation;  // radiation in rad/s
		[KSPField(isPersistant = true)] public double ec_rate;    // EC consumption rate per-second (optional)
		[KSPField] public bool toggle;                            // true if the effect can be toggled on/off
		[KSPField] public string active;                          // name of animation to play when enabling/disabling

		// persistent
		[KSPField(isPersistant = true)] public bool running;

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
			Fields["Status"].guiName = radiation >= 0.0 ? "Radiation" : "Active shielding";
			Events["Toggle"].active = toggle;
			Actions["Action"].active = toggle;

			// deal with non-toggable emitters
			if (!toggle) running = true;

			// create animator
			active_anim = new Animator(part, active);

			// set animation initial state
			active_anim.Still(running ? 0.0 : 1.0);
		}


		public void Update()
		{
			// update ui
			Status = running ? Lib.HumanReadableRadiation(Math.Abs(radiation)) : "none";
			Events["Toggle"].guiName = Lib.StatusToggle(Localizer.Format("#kerbalism-activeshield_Part_title").Replace("Shield", "shield"), running ? Localizer.Format("#KERBALISM_Generic_ACTIVE") : Localizer.Format("#KERBALISM_Generic_DISABLED")); //i'm lazy lol
		}



		public void FixedUpdate()
		{
			// do nothing in the editor
			if (Lib.IsEditor()) return;

			// if enabled, and there is ec consumption
			if (running && ec_rate > double.Epsilon)
			{
				// get resource cache
				Resource_info ec = ResourceCache.Info(vessel, "ElectricCharge");

				// consume EC
				ec.Consume(ec_rate * Kerbalism.elapsed_s, "emitter");
			}
		}


		public static void BackgroundUpdate(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, Emitter emitter, Resource_info ec, double elapsed_s)
		{
			// if enabled, and EC is required
			if (Lib.Proto.GetBool(m, "running") && emitter.ec_rate > double.Epsilon)
			{
				// consume EC
				ec.Consume(emitter.ec_rate * elapsed_s, "emitter");
			}
		}


		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = true)]
		public void Toggle()
		{
			// switch status
			running = !running;

			// play animation
			active_anim.Play(running, false);

			// refresh VAB/SPH ui
			if (Lib.IsEditor()) GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
		}


		// action groups
		[KSPAction("#KERBALISM_Emitter_Action")] public void Action(KSPActionParam param) { Toggle(); }



		// part tooltip
		public override string GetInfo()
		{
			string desc = radiation > double.Epsilon
			  ? Localizer.Format("#KERBALISM_Emitter_EmitIonizing")
			  : Localizer.Format("#KERBALISM_Emitter_ReduceIncoming");

			return Specs().Info(desc);
		}

		// specifics support
		public Specifics Specs()
		{
			Specifics specs = new Specifics();
			specs.Add(radiation >= 0.0 ? Localizer.Format("#KERBALISM_Emitter_Emitted") : Localizer.Format("#KERBALISM_Emitter_ActiveShielding"), Lib.HumanReadableRadiation(Math.Abs(radiation)));
			if (ec_rate > double.Epsilon) specs.Add("EC/s", Lib.HumanReadableRate(ec_rate));
			return specs;
		}


		// return total radiation emitted in a vessel
		public static double Total(Vessel v)
		{
			// get resource cache
			Resource_info ec = ResourceCache.Info(v, "ElectricCharge");

			double tot = 0.0;
			if (v.loaded)
			{
				foreach (var emitter in Lib.FindModules<Emitter>(v))
				{
					if (ec.Amount > double.Epsilon || emitter.ec_rate <= double.Epsilon)
					{
						tot += emitter.running ? emitter.radiation : 0.0;
					}
				}
			}
			else
			{
				foreach (ProtoPartModuleSnapshot m in Lib.FindModules(v.protoVessel, "Emitter"))
				{
					if (ec.Amount > double.Epsilon || Lib.Proto.GetDouble(m, "ec_rate") <= double.Epsilon)
					{
						tot += Lib.Proto.GetBool(m, "running") ? Lib.Proto.GetDouble(m, "radiation") : 0.0;
					}
				}
			}
			return tot;
		}
	}


} // KERBALISM

