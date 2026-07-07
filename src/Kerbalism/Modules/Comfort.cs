using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{


	public class Comfort : PartModule, ISpecifics
	{
		// config+persistence
		[KSPField(isPersistant = true)] public string bonus = string.Empty; // the comfort bonus provided

		// config
		[KSPField] public string desc = string.Empty;                       // short description shown in part tooltip


		public override void OnStart(StartState state)
		{
			// don't break tutorial scenarios
			if (Lib.DisableScenario(this)) return;
		}


		public override string GetInfo()
		{
			return Specs().Info(desc);
		}

		// specifics support
		public Specifics Specs()
		{
			Specifics specs = new Specifics();
			specs.Add("bonus", bonus);
			return specs;
		}

		public override string GetModuleDisplayName() { return Local.Module_Comfort; }//"Comfort"
	}


	public class Comforts
	{
		public Comforts(Vessel v, bool env_firm_ground, bool env_not_alone, bool env_call_home)
		{
			// environment factors
			firm_ground = env_firm_ground;
			call_home = env_call_home;

			if (v.loaded)
			{
				not_alone = env_not_alone;
				bool multiCrew = Lib.CrewCount(v) > 1;

				// scan parts for comfort
				foreach (Comfort c in PartModuleCache.GetModules<Comfort>(v))
				{
					if (c.isEnabled)
						ApplyBonus(c.bonus, multiCrew);
				}

				// scan parts for gravity ring
				if (Lib.IsPowered(v))
				{
					firm_ground |= Lib.HasModule<GravityRing>(v, k => k.deployed);
				}
			}
			else
			{
				bool multiCrew;
				try
				{
					multiCrew = Lib.CrewCount(v.protoVessel) > 1;
				}
				catch
				{
					multiCrew = env_not_alone;
				}
				not_alone = multiCrew;

				// scan parts for comfort
				foreach (ProtoPartModuleSnapshot m in ProtoPartModuleCache.GetModules(v.protoVessel, "Comfort"))
				{
					ApplyBonus(Lib.Proto.GetString(m, "bonus"), multiCrew);
				}

				// scan parts for gravity ring
				if (Lib.IsPowered(v))
				{
					firm_ground |= Lib.HasModule(v.protoVessel, "GravityRing", k => Lib.Proto.GetBool(k, "deployed"));
				}
			}

			CalculateFactor();
		}


		public Comforts(List<Part> parts, bool env_firm_ground, bool env_not_alone, bool env_call_home)
		{
			// environment factors
			firm_ground = env_firm_ground;
			not_alone = env_not_alone;
			call_home = env_call_home;

			// for each parts
			foreach (Part p in parts)
			{
				// for each modules in part
				foreach (PartModule m in p.Modules)
				{
					// skip disabled modules
					if (!m.isEnabled) continue;

					// comfort
					// note: this runs in the editor where modules have no vessel, env_not_alone already is the crew count check
					if (m.moduleName == "Comfort")
					{
						ApplyBonus((m as Comfort).bonus, env_not_alone);
					}
					// gravity ring
					// - ignoring if ec is present or not here
					else if (m.moduleName == "GravityRing")
					{
						GravityRing ring = m as GravityRing;
						firm_ground |= ring.deployed;
					}
				}
			}

			// calculate factor
			CalculateFactor();
		}

		///<summary>register the bonus provided by a comfort provider</summary>
		private void ApplyBonus(string bonus, bool multiCrew)
		{
			switch (bonus)
			{
				case "firm-ground": firm_ground = true; break;
				case "not-alone": not_alone = multiCrew; break;
				case "call-home": call_home = true; break;
				case "exercise": exercise = true; break;
				case "panorama": panorama = true; break;
				case "plants": plants = true; break;
			}
		}

		///<summary>compute the comfort factor from the individual bonuses</summary>
		private void CalculateFactor()
		{
			factor = 0.1;
			if (firm_ground) factor += PreferencesComfort.Instance.firmGround;
			if (not_alone) factor += PreferencesComfort.Instance.notAlone;
			if (call_home) factor += PreferencesComfort.Instance.callHome;
			if (exercise) factor += PreferencesComfort.Instance.exercise;
			if (panorama) factor += PreferencesComfort.Instance.panorama;
			if (plants) factor += PreferencesComfort.Instance.plants;
			factor = Lib.Clamp(factor, 0.1, 1.0);
		}

		public string Tooltip()
		{
			string yes = Lib.BuildString("<b><color=#00ff00>", Local.Generic_YES, " </color></b>");
			string no = Lib.BuildString("<b><color=#ffaa00>", Local.Generic_NO, " </color></b>");
			return Lib.BuildString
			(
				"<align=left />",
				String.Format("{0,-14}\t{1}\n", Local.Comfort_firmground, firm_ground ? yes : no),
				String.Format("{0,-14}\t{1}\n", Local.Comfort_exercise, exercise ? yes : no),
				String.Format("{0,-14}\t{1}\n", Local.Comfort_notalone, not_alone ? yes : no),
				String.Format("{0,-14}\t{1}\n", Local.Comfort_callhome, call_home ? yes : no),
				String.Format("{0,-14}\t{1}\n", Local.Comfort_panorama, panorama ? yes : no),
				String.Format("{0,-14}\t{1}\n", Local.Comfort_plants, plants ? yes : no),
				String.Format("<i>{0,-14}</i>\t{1}", Local.Comfort_factor, Lib.HumanReadablePerc(factor))
			);
		}

		public string Summary()
		{
			if (factor >= 0.99) return Local.Module_Comfort_Summary1;//"ideal"
			else if (factor >= 0.66) return Local.Module_Comfort_Summary2;//"good"
			else if (factor >= 0.33) return Local.Module_Comfort_Summary3;//"modest"
			else if (factor > 0.1) return Local.Module_Comfort_Summary4;//"poor"
			else return Local.Module_Comfort_Summary5;//"none"
		}

		public bool firm_ground;
		public bool exercise;
		public bool not_alone;
		public bool call_home;
		public bool panorama;
		public bool plants;
		public double factor;
	}


} // KERBALISM
