using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KSP.Localization;

namespace KERBALISM
{
	// regexp for creating a field in this file from a loc config line
	// search : \# KERBALISM_(.*?) = (.*)\r\n
	// replace : public static string $1 = GetLoc("$1"); // "$2"\r\n

	// Localization cache
	public static class Local
	{
		private const string prefix = "#KERBALISM_";
		private static string GetLoc(string template) => Localizer.Format(prefix + template);


		////////////////////////////////////////////////////////////////////
		// Generic strings
		public static string Generic_ON = GetLoc("Generic_ON"); // "on"
		public static string Generic_OFF = GetLoc("Generic_OFF"); // "off"
		public static string Generic_ENABLED = GetLoc("Generic_ENABLED"); // "enabled"
		public static string Generic_DISABLED = GetLoc("Generic_DISABLED"); // "disabled"
		public static string Generic_ACTIVE = GetLoc("Generic_ACTIVE"); // "active"
		public static string Generic_INACTIVE = GetLoc("Generic_INACTIVE"); // "inactive"
		public static string Generic_ALWAYSON = GetLoc("Generic_ALWAYSON"); // "always on"
		public static string Generic_RECORDING = GetLoc("Generic_RECORDING"); // "recording"
		public static string Generic_STOPPED = GetLoc("Generic_STOPPED"); // "stopped"
		public static string Generic_RUNNING = GetLoc("Generic_RUNNING"); // "running"
		public static string Generic_EXTENDED = GetLoc("Generic_EXTENDED"); // "extended"
		public static string Generic_RETRACTED = GetLoc("Generic_RETRACTED"); // "retracted"
		public static string Generic_DEPLOYED = GetLoc("Generic_DEPLOYED"); // "extended"
		public static string Generic_BROKEN = GetLoc("Generic_BROKEN"); // "broken"
		public static string Generic_EXTENDING = GetLoc("Generic_EXTENDING"); // "extending"
		public static string Generic_RETRACTING = GetLoc("Generic_RETRACTING"); // "retracting"
		public static string Generic_YES = GetLoc("Generic_YES"); // "yes"
		public static string Generic_NO = GetLoc("Generic_NO"); // "no"
		public static string Generic_RETRACT = GetLoc("Generic_RETRACT"); // "retract"
		public static string Generic_DEPLOY = GetLoc("Generic_DEPLOY"); // "deploy"
		public static string Generic_FROM = GetLoc("Generic_FROM"); // "from"
		public static string Generic_TO = GetLoc("Generic_TO"); // "to"
		public static string Generic_NONE = GetLoc("Generic_NONE"); // "none"
		public static string Generic_NOTHING = GetLoc("Generic_NOTHING"); // "nothing"
		public static string Generic_SLOTS = GetLoc("Generic_SLOTS"); // "slots"
		public static string Generic_SLOT = GetLoc("Generic_SLOT"); // "slot"
		public static string Generic_AVERAGE = GetLoc("Generic_AVERAGE"); // "average"
	}
}
