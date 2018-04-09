// ====================================================================================================================
// Functions that other mods can call to interact with Kerbalism. If you don't build against it, call these using
// reflection. All functions work transparently with loaded and unloaded vessels, unless otherwise specified.
// ====================================================================================================================


using System;


namespace KERBALISM
{


	public static class API
	{
		// show a message in the ui
		public static void Message(string msg)
		{
			KERBALISM.Message.Post(msg);
		}

		// kill a kerbal, even an EVA one
		public static void Kill(Vessel v, ProtoCrewMember c)
		{
			if (!Cache.VesselInfo(v).is_valid) return;
			if (!DB.vessels.ContainsKey(Lib.RootID(v))) return;
			if (!DB.kerbals.ContainsKey(c.name)) return;
			Misc.Kill(v, c);
		}

		// trigger an undesiderable event for the kerbal specified
		public static void Breakdown(Vessel v, ProtoCrewMember c)
		{
			if (!Cache.VesselInfo(v).is_valid) return;
			if (!DB.vessels.ContainsKey(Lib.RootID(v))) return;
			if (!DB.kerbals.ContainsKey(c.name)) return;
			Misc.Breakdown(v, c);
		}

		// disable or re-enable all rules for the specified kerbal
		public static void DisableKerbal(string k_name, bool disabled)
		{
			if (!DB.kerbals.ContainsKey(k_name)) return;
			DB.Kerbal(k_name).disabled = disabled;
		}

		// inject instant radiation dose to the specified kerbal (can use negative amounts)
		public static void InjectRadiation(string k_name, double amount)
		{
			if (!DB.kerbals.ContainsKey(k_name)) return;
			KerbalData kd = DB.Kerbal(k_name);
			foreach (Rule rule in Profile.rules)
			{
				if (rule.modifiers.Contains("radiation"))
				{
					RuleData rd = kd.rules[rule.name];
					rd.problem = Math.Max(rd.problem + amount, 0.0);
				}
			}
		}


		// --- ENVIRONMENT ----------------------------------------------------------

		// return true if the vessel specified is in sunlight
		public static bool InSunlight(Vessel v)
		{
			return Cache.VesselInfo(v).sunlight > double.Epsilon;
		}

		// return true if the vessel specified is inside a breathable atmosphere
		public static bool Breathable(Vessel v)
		{
			return Cache.VesselInfo(v).breathable;
		}


		// --- RADIATION ------------------------------------------------------------

		// return amount of environment radiation at the position of the specified vessel
		public static double Radiation(Vessel v)
		{
			if (!Features.Radiation) return 0.0;
			vessel_info vi = Cache.VesselInfo(v);
			return vi.radiation;
		}

		// return true if the vessel is inside the magnetopause of some body (except the sun)
		public static bool Magnetosphere(Vessel v)
		{
			if (!Features.Radiation) return false;
			return Cache.VesselInfo(v).magnetosphere;
		}

		// return true if the vessel is inside the radiation belt of some body
		public static bool InnerBelt(Vessel v)
		{
			if (!Features.Radiation) return false;
			return Cache.VesselInfo(v).inner_belt;
		}

		// return true if the vessel is inside the radiation belt of some body
		public static bool OuterBelt(Vessel v)
		{
			if (!Features.Radiation) return false;
			return Cache.VesselInfo(v).outer_belt;
		}


		// --- SPACE WEATHER --------------------------------------------------------

		// return true if a solar storm is incoming at the vessel position
		public static bool StormIncoming(Vessel v)
		{
			if (!Features.SpaceWeather) return false;
			return Cache.VesselInfo(v).is_valid && Storm.Incoming(v);
		}

		// return true if a solar storm is in progress at the vessel position
		public static bool StormInProgress(Vessel v)
		{
			if (!Features.SpaceWeather) return false;
			return Cache.VesselInfo(v).is_valid && Storm.InProgress(v);
		}

		// return true if the vessel is subject to a signal blackout
		public static bool Blackout(Vessel v)
		{
			if (!RemoteTech.Enabled()) return false;
			return Cache.VesselInfo(v).blackout;
		}

		// --- RELIABILITY ----------------------------------------------------------

		// return true if at least a component has malfunctioned, or had a critical failure
		public static bool Malfunction(Vessel v)
		{
			if (!Features.Reliability) return false;
			return Cache.VesselInfo(v).malfunction;
		}

		// return true if at least a componet had a critical failure
		public static bool Critical(Vessel v)
		{
			if (!Features.Reliability) return false;
			return Cache.VesselInfo(v).critical;
		}

		// return true if the part specified has a malfunction or critical failure
		public static bool Broken(Part part)
		{
			return part.FindModulesImplementing<Reliability>().FindAll(k => k.isEnabled && k.broken) != null;
		}

		// repair a specified part
		public static void Repair(Part part)
		{
			part.FindModulesImplementing<Reliability>().FindAll(k => k.isEnabled && k.broken).ForEach(k => k.Repair());
		}


		// --- HABITAT --------------------------------------------------------------

		// return volume of internal habitat in m^3
		public static double Volume(Vessel v)
		{
			if (!Features.Habitat) return 0.0;
			return Cache.VesselInfo(v).volume;
		}

		// return surface of internal habitat in m^2
		public static double Surface(Vessel v)
		{
			if (!Features.Habitat) return 0.0;
			return Cache.VesselInfo(v).surface;
		}

		// return normalized pressure of internal habitat
		public static double Pressure(Vessel v)
		{
			if (!Features.Pressure) return 0.0;
			return Cache.VesselInfo(v).pressure;
		}

		// return level of co2 of internal habitat
		public static double Poisoning(Vessel v)
		{
			if (!Features.Poisoning) return 0.0;
			return Cache.VesselInfo(v).poisoning;
		}

		// return proportion of radiation blocked by shielding
		public static double Shielding(Vessel v)
		{
			return Cache.VesselInfo(v).shielding;
		}

		// return living space factor
		public static double LivingSpace(Vessel v)
		{
			return Cache.VesselInfo(v).living_space;
		}

		// return comfort factor
		public static double Comfort(Vessel v)
		{
			return Cache.VesselInfo(v).comforts.factor;
		}


		// --- SCIENCE --------------------------------------------------------------

		// return size of a file in a vessel drive
		public static double FileSize(Vessel v, string subject_id)
		{
			if (!Cache.VesselInfo(v).is_valid) return 0.0;
			Drive drive = DB.Vessel(v).drive;
			File file;
			return drive.files.TryGetValue(subject_id, out file) ? file.size : 0.0;
		}

		// return size of a sample in a vessel drive
		public static double SampleSize(Vessel v, string subject_id)
		{
			if (!Cache.VesselInfo(v).is_valid) return 0.0;
			Drive drive = DB.Vessel(v).drive;
			Sample sample;
			return drive.samples.TryGetValue(subject_id, out sample) ? sample.size : 0.0;
		}

		// store a file on a vessel
		public static void StoreFile(Vessel v, string subject_id, double amount)
		{
			if (!Cache.VesselInfo(v).is_valid) return;
			Drive drive = DB.Vessel(v).drive;
			drive.record_file(subject_id, amount);
		}

		// store a sample on a vessel
		public static void StoreSample(Vessel v, string subject_id, double amount)
		{
			if (!Cache.VesselInfo(v).is_valid) return;
			Drive drive = DB.Vessel(v).drive;
			drive.record_sample(subject_id, amount);
		}

		// remove a file from a vessel
		public static void RemoveFile(Vessel v, string subject_id, double amount)
		{
			if (!Cache.VesselInfo(v).is_valid) return;
			Drive drive = DB.Vessel(v).drive;
			drive.delete_file(subject_id, amount);
		}

		// remove a sample from a vessel
		public static void RemoveSample(Vessel v, string subject_id, double amount)
		{
			if (!Cache.VesselInfo(v).is_valid) return;
			Drive drive = DB.Vessel(v).drive;
			drive.delete_sample(subject_id, amount);
		}
	}


} // KERBALISM



