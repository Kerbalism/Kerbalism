using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;

namespace KERBALISM
{
	public class Emitter : PartModule, ISpecifics
	{
		// config
		[KSPField] public string active;                          // name of animation to play when enabling/disabling

		[KSPField(isPersistant = true)] public string title = string.Empty;     // GUI name of the status action in the PAW
		[KSPField(isPersistant = true)] public bool toggle;						// true if the effect can be toggled on/off
		[KSPField(isPersistant = true)] public double radiation;				// radiation in rad/s
		[KSPField(isPersistant = true)] public double ec_rate;					// EC consumption rate per-second (optional)
		[KSPField(isPersistant = true)] public bool running;
		[KSPField(isPersistant = true)] public double radiation_impact = 1.0;	// calculated based on vessel design

#if KSP15_16
		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "_")]
#else
		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "_", groupName = "Radiation", groupDisplayName = "Radiation")]
#endif
		// rmb status
		public string Status;  // rate of radiation emitted/shielded

		// animations
		Animator active_anim;
		bool radiation_impact_calculated = false;

		// pseudo-ctor
		public override void OnStart(StartState state)
		{
			// don't break tutorial scenarios
			if (Lib.DisableScenario(this)) return;

			// update RMB ui
			if (string.IsNullOrEmpty(title))
				Fields["Status"].guiName = radiation >= 0.0 ? "Radiation" : "Active shield";
			else
				Fields["Status"].guiName = title;

			Events["Toggle"].active = toggle;
			Actions["Action"].active = toggle;

			// deal with non-toggable emitters
			if (!toggle) running = true;

			// create animator
			active_anim = new Animator(part, active);

			// set animation initial state
			active_anim.Still(running ? 0.0 : 1.0);
		}

		public class HabitatInfo
		{
			public Habitat habitat;
			public float distance;

			public HabitatInfo(Habitat habitat, float distance)
			{
				this.habitat = habitat;
				this.distance = distance;
			}
		}
		List<HabitatInfo> habitatInfos = null;

		public void Recalculate()
		{
			habitatInfos = null;
			CalculateRadiationImpact();
		}

		public void BuildHabitatInfos()
		{
			if (habitatInfos != null) return;
			if (part.transform == null) return;
			var emitterPosition = part.transform.position;

			List<Habitat> habitats;

			if (Lib.IsEditor())
			{
				habitats = new List<Habitat>();

				List<Part> parts = Lib.GetPartsRecursively(EditorLogic.RootPart);
				foreach (var p in parts)
				{
					var habitat = p.FindModuleImplementing<Habitat>();
					if (habitat != null) habitats.Add(habitat);
				}
			}
			else
			{
				habitats = vessel.FindPartModulesImplementing<Habitat>();
			}

			habitatInfos = new List<HabitatInfo>();

			foreach (var habitat in habitats)
			{
				var habitatPosition = habitat.part.transform.position;
				var vector = habitatPosition - emitterPosition;

				HabitatInfo spi = new HabitatInfo(habitat, vector.magnitude);
				habitatInfos.Add(spi);
			}
		}

		/// <summary>Calculate the average radiation effect to all habitats. returns true if successful.</summary>
		public bool CalculateRadiationImpact()
		{
			if (radiation < 0)
			{
				radiation_impact = 1.0;
				return true;
			}

			if (habitatInfos == null) BuildHabitatInfos();
			if (habitatInfos == null) return false;

			radiation_impact = 0.0;
			int habitatCount = 0;

			foreach (var hi in habitatInfos)
			{
				radiation_impact += Radiation.DistanceRadiation(1.0, hi.distance);
				habitatCount++;
			}

			if (habitatCount > 1)
				radiation_impact /= habitatCount;

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
			if (!radiation_impact_calculated)
				radiation_impact_calculated = CalculateRadiationImpact();
		}

		/// <summary>
		/// We're always going to call you for resource handling.  You tell us what to produce or consume.  Here's how it'll look when your vessel is NOT loaded
		/// </summary>
		/// <param name="v">the vessel (unloaded)</param>
		/// <param name="part_snapshot">proto part snapshot (contains all non-persistant KSPFields)</param>
		/// <param name="module_snapshot">proto part module snapshot (contains all non-persistant KSPFields)</param>
		/// <param name="proto_part_module">proto part module snapshot (contains all non-persistant KSPFields)</param>
		/// <param name="proto_part">proto part snapshot (contains all non-persistant KSPFields)</param>
		/// <param name="availableResources">key-value pair containing all available resources and their currently available amount on the vessel. if the resource is not in there, it's not available</param>
		/// <param name="resourceChangeRequest">key-value pair that contains the resource names and the units per second that you want to produce/consume (produce: positive, consume: negative)</param>
		/// <param name="elapsed_s">how much time elapsed since the last time. note this can be very long, minutes and hours depending on warp speed</param>
		/// <returns>the title to be displayed in the resource tooltip</returns>
		public static string BackgroundUpdate(Vessel v,
			ProtoPartSnapshot part_snapshot, ProtoPartModuleSnapshot module_snapshot,
			PartModule proto_part_module, Part proto_part,
			Dictionary<string, double> availableResources, List<KeyValuePair<string, double>> resourceChangeRequest,
			double elapsed_s)
		{
			Emitter emitter = proto_part_module as Emitter;
			if (emitter == null) return string.Empty;

			if (Lib.Proto.GetBool(module_snapshot, "running") && emitter.ec_rate > 0)
			{
				resourceChangeRequest.Add(new KeyValuePair<string, double>("ElectricCharge", -emitter.ec_rate));
			}

			return "active shield";
		}


		/// <summary>
		/// We're also always going to call you when you're loaded.  Since you're loaded, this will be your PartModule, just like you'd expect in KSP. Will only be called while in flight, not in the editor
		/// </summary>
		/// <param name="availableResources">key-value pair containing all available resources and their currently available amount on the vessel. if the resource is not in there, it's not available</param>
		/// <param name="resourceChangeRequest">key-value pair that contains the resource names and the units per second that you want to produce/consume (produce: positive, consume: negative)</param>
		/// <returns></returns>
		public virtual string ResourceUpdate(Dictionary<string, double> availableResources, List<KeyValuePair<string, double>> resourceChangeRequest)
		{
			// if enabled, and there is ec consumption
			if (running && ec_rate > 0)
			{
				resourceChangeRequest.Add(new KeyValuePair<string, double>("ElectricCharge", -ec_rate));
			}

			return "active shield";
		}


#if KSP15_16
		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = true)]
#else
		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = true, groupName = "Radiation", groupDisplayName = "Radiation")]
#endif
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
				if (!vd.IsSimulated) continue;

				foreach (var emitter in Lib.FindModules<Emitter>(n))
				{
					if (emitter.part == null || emitter.part.transform == null) continue;
					if (emitter.radiation <= 0) continue; // ignore shielding effects here
					if (!emitter.running) continue;

					var emitterPosition = emitter.part.transform.position;
					var vector = evaPosition - emitterPosition;
					var distance = vector.magnitude;

					result += Radiation.DistanceRadiation(emitter.radiation, distance);
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
						if (emitter.running)
						{
							if (emitter.radiation > 0) tot += emitter.radiation * emitter.radiation_impact;
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
						if (Lib.Proto.GetBool(m, "running"))
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

