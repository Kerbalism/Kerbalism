using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;
using KERBALISM.Planner;

namespace KERBALISM
{
	public class Emitter : PartModule, ISpecifics, IBackgroundModule, IPlannerModule
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
		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "_", groupName = "Radiation", groupDisplayName = "#KERBALISM_Group_Radiation")]//Radiation
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
				title = radiation >= 0.0 ? "Radiation" : "Active shield";

			Fields["Status"].guiName = title;
			Events["Toggle"].active = toggle;
			Actions["Action"].active = toggle;

			// deal with non-toggable emitters
			if (!toggle) running = true;

			// create animator
			active_anim = new Animator(part, active);

			// set animation initial state
			active_anim.Still(running ? 0f : 1f);
		}

		public class HabitatInfo
		{
			public ModuleKsmHabitat habitat;
			public float distance;

			public HabitatInfo(ModuleKsmHabitat habitat, float distance)
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

			List<ModuleKsmHabitat> habitats;

			if (Lib.IsEditor())
			{
				habitats = new List<ModuleKsmHabitat>();

				foreach (var p in EditorLogic.fetch.ship.parts)
				{
					var habitat = p.FindModuleImplementing<ModuleKsmHabitat>();
					if (habitat != null) habitats.Add(habitat);
				}
			}
			else
			{
				habitats = vessel.FindPartModulesImplementing<ModuleKsmHabitat>();
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
			Status = running ? Lib.HumanReadableRadiation(Math.Abs(radiation)) : Local.Emitter_none;//"none"
			Events["Toggle"].guiName = Lib.StatusToggle(part.partInfo.title, running ? Local.Generic_ACTIVE : Local.Generic_DISABLED);
		}

		public void FixedUpdate()
		{
			if (!radiation_impact_calculated)
				radiation_impact_calculated = CalculateRadiationImpact();

			if (ec_rate > 0.0 && running)
				vessel.KerbalismData().ResHandler.ElectricCharge.Consume(ec_rate * Kerbalism.elapsed_s, ResourceBroker.GetOrCreate(title));
		}

		public void BackgroundUpdate(VesselData vd, ProtoPartSnapshot protoPart, ProtoPartModuleSnapshot protoModule, double elapsed_s)
		{
			if (ec_rate > 0.0 && Lib.Proto.GetBool(protoModule, "running"))
				vd.ResHandler.ElectricCharge.Consume(ec_rate * elapsed_s, ResourceBroker.GetOrCreate(title));
		}

		public void PlannerUpdate(VesselResHandler resHandler, EnvironmentAnalyzer environment, VesselAnalyzer vessel)
		{
			if (ec_rate > 0.0 && running)
				resHandler.ElectricCharge.Consume(ec_rate, ResourceBroker.GetOrCreate(title));
		}

#if KSP15_16
		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = true)]
#else
	[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = true, groupName = "Radiation", groupDisplayName = "#KERBALISM_Group_Radiation")]//Radiation
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
			  ? Local.Emitter_EmitIonizing
			  : Local.Emitter_ReduceIncoming;

			return Specs().Info(desc);
		}

		// specifics support
		public Specifics Specs()
		{
			Specifics specs = new Specifics();
			specs.Add(radiation >= 0.0 ? Local.Emitter_Emitted : Local.Emitter_ActiveShielding, Lib.HumanReadableRadiation(Math.Abs(radiation)));
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
			VesselResource ec = v.KerbalismData().ResHandler.ElectricCharge;

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

