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
		[KSPField(isPersistant = true)] public double radiation_factor = 1.0; // calculated based on vessel design

		// rmb status
		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "_")] public string Status;  // rate of radiation emitted/shielded

		// animations
		Animator active_anim;
		bool radiation_calculated = false;

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

		public class ShieldingPartInfo
		{
			public Habitat habitat;
			// public List<Part> collidingParts;
			public float distance;

			public ShieldingPartInfo(Habitat habitat, float distance)
			{
				this.habitat = habitat;
				this.distance = distance;
				// collidingParts = new List<Part>();
			}
		}
		List<ShieldingPartInfo> shieldingPartInfos = null;

		public void Recalculate()
		{
			shieldingPartInfos = null;
			CalculateRadiationImpact();
		}

		public void RaycastParts()
		{
			if (shieldingPartInfos != null) return;

			// find all parts that intersect with the line from the emitter to the habitat
			if (part.transform == null) return;
			var emitterPosition = part.transform.position;

			List<Habitat> habitats;

			if(Lib.IsEditor())
			{
				habitats = new List<Habitat>();

				List<Part> parts = Lib.GetPartsRecursively(EditorLogic.RootPart);
				foreach(var p in parts)
				{
					var habitat = p.FindModuleImplementing<Habitat>();
					if (habitat != null) habitats.Add(habitat);
				}
			}
			else
			{
				habitats = vessel.FindPartModulesImplementing<Habitat>();
			}

			shieldingPartInfos = new List<ShieldingPartInfo>();

			foreach (var habitat in habitats)
			{
				var habitatPosition = habitat.part.transform.position;
				var vector = habitatPosition - emitterPosition;

				ShieldingPartInfo spi = new ShieldingPartInfo(habitat, vector.magnitude);
				shieldingPartInfos.Add(spi);

				/*
				Ray r = new Ray(emitterPosition, vector);
				var hits = Physics.RaycastAll(r, vector.magnitude);
				foreach (var hit in hits)
				{
					if(hit.collider != null && hit.collider.gameObject != null)
					{
						Part a = Part.GetComponentUpwards<Part>(hit.collider.gameObject);
						if (a == habitat.part) continue;
						if(a != null) spi.collidingParts.Add(a);
					}
				}
				*/
			}
		}

		/// <summary>Calculate the average radiation effect to all habitats. returns true if successful.</summary>
		public bool CalculateRadiationImpact()
		{
			if(radiation < 0)
			{
				radiation_factor = 1.0;
				return true;
			}

			if (shieldingPartInfos == null) RaycastParts();
			if (shieldingPartInfos == null) return false;

			radiation_factor = 0.0;
			int habitatCount = 0;

			foreach(var spi in shieldingPartInfos)
			{
				// radiation decreases with 1/4 * r^2
				var factor = 1.0 / Math.Max(1, spi.distance * spi.distance / 4.0);

				/*
				foreach (var p in spi.collidingParts)
				{
					double mass = p.mass + p.GetResourceMass();
					mass *= 1000.0; // KSP masses are in tons

					// the following is guesswork:

					// radiation has to pass through that part mass in a straight line.
					// use the cubic root of the part mass as radiation damping factor, since the ray
					// doesn't have to go through the total mass, just through that one line

					// the shielding effect is the inverse of the cubic root of the part mass,
					// multiplied by a mass shielding effect coefficient
					var massShielding = 0.6 * Math.Pow(mass, 1.0/3.0);
					var massFactor = 1.0 / Math.Max(1, massShielding);

					factor *= massFactor;
				}
				*/

				radiation_factor += factor;
				habitatCount++;
			}

			if (habitatCount > 1)
				radiation_factor /= habitatCount;

			return true;
		}

		public void Update()
		{
			// update ui
			Status = running ? Lib.HumanReadableRadiation(Math.Abs(radiation)) : "none";
			Events["Toggle"].guiName = Lib.StatusToggle(Localizer.Format("#kerbalism-activeshield_Part_title").Replace("Shield", "shield"), running ? Localizer.Format("#KERBALISM_Generic_ACTIVE") : Localizer.Format("#KERBALISM_Generic_DISABLED")); //i'm lazy lol
		}

		public void FixedUpdate()
		{
			if (!radiation_calculated)
				radiation_calculated = CalculateRadiationImpact();

			// do nothing else in the editor
			if (Lib.IsEditor()) return;

			// if enabled, and there is ec consumption
			if (running && ec_rate > double.Epsilon)
			{
				// get resource cache
				ResourceInfo ec = ResourceCache.GetResource(vessel, "ElectricCharge");

				// consume EC
				ec.Consume(ec_rate * Kerbalism.elapsed_s, "emitter");
			}
		}


		public static void BackgroundUpdate(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, Emitter emitter, ResourceInfo ec, double elapsed_s)
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

		/// <summary>
		/// get the total radiation emitted by nearby emitters (used for EVAs). only works for loaded vessels.
		/// </summary>
		public static double Nearby(Vessel v)
		{
			if (!v.loaded || !v.isEVA) return 0.0;
			var evaPosition = v.rootPart.transform.position;

			double result = 0.0;

			foreach (Vessel n in FlightGlobals.VesselsLoaded)
			{
				var vd = n.KerbalismData();
				if (!vd.IsValid) continue;

				foreach (var emitter in vd.Emitters())
				{
					if (emitter.part == null || emitter.part.transform == null) continue;
					if (emitter.radiation <= 0) continue; // ignore shielding effects here
					if (!emitter.running) continue;

					var emitterPosition = emitter.part.transform.position;
					var vector = evaPosition - emitterPosition;
					var distance = vector.magnitude;

					// radiation decreases with 1/4 * r^2
					var factor = 1.0 / Math.Max(1, distance * distance / 4.0);
					result += factor * emitter.radiation;
				}
			}

			return result;
		}

		// return total radiation emitted in a vessel
		public static double Total(Vessel v)
		{
			// get resource cache
			ResourceInfo ec = ResourceCache.GetResource(v, "ElectricCharge");

			double tot = 0.0;
			if (v.loaded)
			{
				foreach (var emitter in Lib.FindModules<Emitter>(v))
				{
					if (ec.Amount > double.Epsilon || emitter.ec_rate <= double.Epsilon)
					{
						if(emitter.running)
						{
							if (emitter.radiation > 0) tot += emitter.radiation * emitter.radiation_factor;
							else tot += emitter.radiation; // always account for full shielding effect
						}
					}
				}
			}
			else
			{
				foreach (ProtoPartModuleSnapshot m in Lib.FindModules(v.protoVessel, "Emitter"))
				{
					if (ec.Amount > double.Epsilon || Lib.Proto.GetDouble(m, "ec_rate") <= double.Epsilon)
					{
						if(Lib.Proto.GetBool(m, "running"))
						{
							var rad = Lib.Proto.GetDouble(m, "radiation");
							if (rad < 0) tot += rad;
							else
							{
								tot += rad * Lib.Proto.GetDouble(m, "radiation_factor");
							}
						}
					}
				}
			}
			return tot;
		}
	}


} // KERBALISM

