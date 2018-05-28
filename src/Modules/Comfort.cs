using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{


	public sealed class Comfort : PartModule, ISpecifics
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
	}


	public sealed class Comforts
	{
		public Comforts(Vessel v, bool env_firm_ground, bool env_not_alone, bool env_call_home)
		{
			// environment factors
			firm_ground = env_firm_ground;
			not_alone = env_not_alone;
			call_home = env_call_home;

			// if loaded
			if (v.loaded)
			{
				// scan parts for comfort
				foreach (Comfort c in Lib.FindModules<Comfort>(v))
				{
					switch (c.bonus)
					{
						case "firm-ground": firm_ground = true; break;
						case "not-alone": not_alone = true; break;
						case "call-home": call_home = true; break;
						case "exercise": exercise = true; break;
						case "panorama": panorama = true; break;
					}
				}

				// scan parts for gravity ring
				if (ResourceCache.Info(v, "ElectricCharge").amount >= 0.01)
				{
					firm_ground |= Lib.HasModule<GravityRing>(v, k => k.deployed);
				}
			}
			// if not loaded
			else
			{
				// scan parts for comfort
				foreach (ProtoPartModuleSnapshot m in Lib.FindModules(v.protoVessel, "Comfort"))
				{
					switch (Lib.Proto.GetString(m, "bonus"))
					{
						case "firm-ground": firm_ground = true; break;
						case "not-alone": not_alone = true; break;
						case "call-home": call_home = true; break;
						case "exercise": exercise = true; break;
						case "panorama": panorama = true; break;
					}
				}

				// scan parts for gravity ring
				if (ResourceCache.Info(v, "ElectricCharge").amount >= 0.01)
				{
					firm_ground |= Lib.HasModule(v.protoVessel, "GravityRing", k => Lib.Proto.GetBool(k, "deployed"));
				}
			}

			// calculate factor
			factor = 0.1;
			if (firm_ground) factor += Settings.ComfortFirmGround;
			if (not_alone) factor += Settings.ComfortNotAlone;
			if (call_home) factor += Settings.ComfortCallHome;
			if (exercise) factor += Settings.ComfortExercise;
			if (panorama) factor += Settings.ComfortPanorama;
			factor = Lib.Clamp(factor, 0.1, 1.0);
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
					if (m.moduleName == "Comfort")
					{
						Comfort c = m as Comfort;
						switch (c.bonus)
						{
							case "firm-ground": firm_ground = true; break;
							case "not-alone": not_alone = true; break;
							case "call-home": call_home = true; break;
							case "exercise": exercise = true; break;
							case "panorama": panorama = true; break;
						}
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
			factor = 0.1;
			if (firm_ground) factor += Settings.ComfortFirmGround;
			if (not_alone) factor += Settings.ComfortNotAlone;
			if (call_home) factor += Settings.ComfortCallHome;
			if (exercise) factor += Settings.ComfortExercise;
			if (panorama) factor += Settings.ComfortPanorama;
			factor = Lib.Clamp(factor, 0.1, 1.0);
		}



		public string Tooltip()
		{
			string yes = Lib.BuildString("<b><color=#00ff00>", Localizer.Format("#KERBALISM_Generic_YES"), " </color></b>");
			string no = Lib.BuildString("<b><color=#ff0000>", Localizer.Format("#KERBALISM_Generic_NO"), " </color></b>");
			return Lib.BuildString
			(
				"<align=left />",
				String.Format("{0,-14}\t{1}\n", Localizer.Format("#KERBALISM_Comfort_firmground"), firm_ground ? yes : no),
				String.Format("{0,-14}\t{1}\n", Localizer.Format("#KERBALISM_Comfort_exercise"), exercise ? yes : no),
				String.Format("{0,-14}\t{1}\n", Localizer.Format("#KERBALISM_Comfort_notalone"), not_alone ? yes : no),
				String.Format("{0,-14}\t{1}\n", Localizer.Format("#KERBALISM_Comfort_callhome"), call_home ? yes : no),
				String.Format("{0,-14}\t{1}\n", Localizer.Format("#KERBALISM_Comfort_panorama"), panorama ? yes : no),
				String.Format("<i>{0,-14}</i>\t{1}", Localizer.Format("#KERBALISM_Comfort_factor"), Lib.HumanReadablePerc(factor))
			);
		}

		public string Summary()
		{
			if (factor >= 0.99) return "ideal";
			else if (factor >= 0.66) return "good";
			else if (factor >= 0.33) return "modest";
			else if (factor > 0.1) return "poor";
			else return "none";
		}

		public bool firm_ground;
		public bool exercise;
		public bool not_alone;
		public bool call_home;
		public bool panorama;
		public double factor;
	}


} // KERBALISM
