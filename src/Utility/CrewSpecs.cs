using System;
using System.Collections.Generic;


namespace KERBALISM
{


	public sealed class CrewSpecs
	{
		public CrewSpecs(string value)
		{
			// if empty or false: not enabled
			if (value.Length == 0 || value.ToLower() == "false")
			{
				trait = string.Empty;
				level = 0;
				enabled = false;
			}
			// if true: enabled, any trait
			else if (value.ToLower() == "true")
			{
				trait = string.Empty;
				level = 0;
				enabled = true;
			}
			// all other cases: enabled, specified trait and experience
			else
			{
				var tokens = Lib.Tokenize(value, '@');
				trait = tokens.Count > 0 ? tokens[0] : string.Empty;
				level = tokens.Count > 1 ? Lib.Parse.ToUInt(tokens[1]) : 0;
				enabled = true;
			}
		}

		// return true if the crew of active vessel satisfy the specs
		public bool Check()
		{
			Vessel v = FlightGlobals.ActiveVessel;
			return v != null && Check(v);
		}

		// return true if the crew of specified vessel satisfy the specs
		public bool Check(Vessel v)
		{
			return Check(Lib.CrewList(v));
		}

		// return true if the specified crew satisfy the specs
		public bool Check(List<ProtoCrewMember> crew)
		{
			for (int i = 0; i < crew.Count; ++i)
			{
				if (Check(crew[i])) return true;
			}
			return false;
		}

		// return true if the specified crew member satisfy the specs
		public bool Check(ProtoCrewMember c)
		{
			return trait.Length == 0 || (c.trait == trait && c.experienceLevel >= level);
		}

		// return sum of al bonuses for crew on vessel
		public int Bonus(Vessel v, int requiredLevel = Int16.MinValue)
		{
			return Bonus(Lib.CrewList(v), requiredLevel);
		}

		// return sum of al bonuses for crew
		public int Bonus(List<ProtoCrewMember> crew, int requiredLevel = Int16.MinValue)
		{
			int result = 0;
			for (int i = 0; i < crew.Count; ++i)
			{
				int bonus = Bonus(crew[i], requiredLevel);
				if (bonus > 0) result += bonus;
			}
			return result;
		}

		// return how much above the required level a crew member is
		public int Bonus(ProtoCrewMember c, int requiredLevel = Int16.MinValue)
		{
			if(requiredLevel == Int16.MinValue) {
				requiredLevel = (int)level;
			}
			if (trait.Length == 0 || c.trait != trait) return 0;
			return (int)(c.experienceLevel - requiredLevel);
		}

		// generate a string for use in warning messages
		public string Warning()
		{
			return Lib.BuildString
			(
			  "<b>",
			  (trait.Length == 0 ? "Crew" : trait),
			  "</b> ",
			  (level == 0 ? string.Empty : "of level <b>" + level + "</b> "),
			  "is required"
			);
		}

		// generate a string for use in part tooltip
		public string Info()
		{
			if (!enabled) return "no";
			else if (trait.Length == 0) return "anyone";
			else return Lib.BuildString(trait, (level == 0 ? string.Empty : " (level: " + level + ")"));
		}

		// can check if enabled by bool comparison
		public static implicit operator bool(CrewSpecs ct)
		{
			return ct.enabled;
		}

		public string trait;    // trait specified, or empty for any trait
		public uint level;    // experience level specified
		public bool enabled;  // can also specify 'disabled' state
	}


} // KERBALISM

