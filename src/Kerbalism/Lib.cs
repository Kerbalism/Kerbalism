using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using CommNet;
using KSP.Localization;

namespace KERBALISM
{


	public static class Lib
	{
		// --- UTILS ----------------------------------------------------------------

		// write a message to the log
		public static void Log(string msg, params object[] param)
		{
			StackTrace stackTrace = new StackTrace();
			UnityEngine.Debug.Log(string.Format("{0} -> verbose: {1}.{2} - {3}", "[Kerbalism] ", stackTrace.GetFrame(1).GetMethod().ReflectedType.Name,
				stackTrace.GetFrame(1).GetMethod().Name, string.Format(msg, param)));
		}

		[Conditional("DEBUG")]
		public static void DebugLog(string message, params object[] param)
		{
			StackTrace stackTrace = new StackTrace();
			UnityEngine.Debug.Log(string.Format("{0} -> debug: {1}.{2} - {3}", "[Kerbalism] ", stackTrace.GetFrame(1).GetMethod().ReflectedType.Name,
				stackTrace.GetFrame(1).GetMethod().Name, string.Format(message, param)));
		}

		// return version as a string
		static string _version;
		public static string Version()
		{
			if (_version == null) _version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
			return _version;
		}

		// returns current version
		public static string VersionString
		{
			get
			{
				return Versioning.version_major.ToString() + Versioning.version_minor.ToString();
			}
		}

		// return true if an assembly with specified name is loaded
		public static bool HasAssembly(string name)
		{
			foreach (var a in AssemblyLoader.loadedAssemblies)
			{
				if (a.name == name) return true;
			}
			return false;
		}

		// swap two variables
		public static void Swap<T>(ref T a, ref T b)
		{
			T tmp = b;
			b = a;
			a = tmp;
		}

		// find a directory in the GameData directory
		public static bool GameDirectoryExist(string findpath)
		{
			try
			{
				string gamedir = Path.Combine(Path.GetFullPath(KSPUtil.ApplicationRootPath), "GameData/" + findpath);
				findpath = Path.GetFileName(gamedir);
				gamedir = Path.GetDirectoryName(gamedir);
				string[] paths = System.IO.Directory.GetDirectories(gamedir, findpath, SearchOption.AllDirectories);
				if (paths.Length > 0)
					return true;
				else
					return false;
			}
			catch (Exception e)
			{
				Log("error while looking for directory '" + findpath + "' in 'GameData' directory. (" + e.Message + ")");
				return false;
			}
		}


		// --- MATH -----------------------------------------------------------------

		// clamp a value
		public static int Clamp(int value, int min, int max)
		{
			return Math.Max(min, Math.Min(value, max));
		}

		// clamp a value
		public static float Clamp(float value, float min, float max)
		{
			return Math.Max(min, Math.Min(value, max));
		}

		// clamp a value
		public static double Clamp(double value, double min, double max)
		{
			return Math.Max(min, Math.Min(value, max));
		}

		// blend between two values
		public static float Mix(float a, float b, float k)
		{
			return a * (1.0f - k) + b * k;
		}

		// blend between two values
		public static double Mix(double a, double b, double k)
		{
			return a * (1.0 - k) + b * k;
		}


		// --- RANDOM ---------------------------------------------------------------

		// store the random number generator
		static System.Random rng = new System.Random();

		// return random integer
		public static int RandomInt(int max_value)
		{
			return rng.Next(max_value);
		}

		// return random float [0..1]
		public static float RandomFloat()
		{
			return (float)rng.NextDouble();
		}

		// return random double [0..1]
		public static double RandomDouble()
		{
			return rng.NextDouble();
		}

		// return random float in [-1,+1] range
		// - it is less random than the c# RNG, but is way faster
		// - the seed is meant to overflow! (turn off arithmetic overflow/underflow exceptions)
		static int fast_float_seed = 1;
		public static float FastRandomFloat()
		{
			fast_float_seed *= 16807;
			return fast_float_seed * 4.6566129e-010f;
		}


		// --- HASH -----------------------------------------------------------------

		// combine two guid, irregardless of their order (eg: Combine(a,b) == Combine(b,a))
		public static Guid CombineGuid(Guid a, Guid b)
		{
			byte[] a_buf = a.ToByteArray();
			byte[] b_buf = b.ToByteArray();
			byte[] c_buf = new byte[16];
			for (int i = 0; i < 16; ++i) c_buf[i] = (byte)(a_buf[i] ^ b_buf[i]);
			return new Guid(c_buf);
		}

		// combine two guid, in a non-commutative way
		public static Guid OrderedCombineGuid(Guid a, Guid b)
		{
			byte[] a_buf = a.ToByteArray();
			byte[] b_buf = b.ToByteArray();
			byte[] c_buf = new byte[16];
			for (int i = 0; i < 16; ++i) c_buf[i] = (byte)(a_buf[i] & ~b_buf[i]);
			return new Guid(c_buf);
		}

		// get 32bit FNV-1a hash of a string
		public static UInt32 Hash32(string s)
		{
			// offset basis
			UInt32 h = 2166136261u;

			// for each byte of the buffer
			for (int i = 0; i < s.Length; ++i)
			{
				// xor the bottom with the current octet
				h ^= s[i];

				// equivalent to h *= 16777619 (FNV magic prime mod 2^32)
				h += (h << 1) + (h << 4) + (h << 7) + (h << 8) + (h << 24);
			}

			//return the hash
			return h;
		}


		// --- TIME -----------------------------------------------------------------

		// return hours in a day
		public static double HoursInDay()
		{
			if(FlightGlobals.ready || IsEditor())
			{
				var homeBody = FlightGlobals.GetHomeBody();
				return Math.Round(homeBody.rotationPeriod / 3600, 0);
			}
			return GameSettings.KERBIN_TIME ? 6.0 : 24.0;
		}

		// return year length
		public static double DaysInYear()
		{
			if (FlightGlobals.ready || IsEditor())
			{
				var homeBody = FlightGlobals.GetHomeBody();
				return Math.Floor(homeBody.orbit.period / (HoursInDay() * 60.0 * 60.0));
			}
			return 426.0;
		}

		// stop time warping
		public static void StopWarp(int rate = 0)
		{
			TimeWarp.fetch.CancelAutoWarp();
			TimeWarp.SetRate(rate, true, false);
		}

		// disable time warping above a specified level
		public static void DisableWarp(uint max_level)
		{
			for (uint i = max_level + 1u; i < 8; ++i)
			{
				TimeWarp.fetch.warpRates[i] = TimeWarp.fetch.warpRates[max_level];
			}
		}

		// get current time
		public static UInt64 Clocks()
		{
			return (UInt64)Stopwatch.GetTimestamp();
		}

		// convert from clocks to microseconds
		public static double Microseconds(UInt64 clocks)
		{
			return clocks * 1000000.0 / Stopwatch.Frequency;
		}


		public static double Milliseconds(UInt64 clocks)
		{
			return clocks * 1000.0 / Stopwatch.Frequency;
		}


		public static double Seconds(UInt64 clocks)
		{
			return clocks / (double)Stopwatch.Frequency;
		}

		// return human-readable timestamp of planetarium time
		public static string PlanetariumTimestamp()
		{
			double t = Planetarium.GetUniversalTime();
			const double len_min = 60.0;
			const double len_hour = len_min * 60.0;
			double len_day = len_hour * Lib.HoursInDay();
			double len_year = len_day * Lib.DaysInYear();

			double year = Math.Floor(t / len_year);
			t -= year * len_year;
			double day = Math.Floor(t / len_day);
			t -= day * len_day;
			double hour = Math.Floor(t / len_hour);
			t -= hour * len_hour;
			double min = Math.Floor(t / len_min);

			return BuildString
			(
			  "[",
			  ((uint)year + 1).ToString("D4"),
			  "/",
			  ((uint)day + 1).ToString("D2"),
			  " ",
			  ((uint)hour).ToString("D2"),
			  ":",
			  ((uint)min).ToString("D2"),
			  "]"
			);
		}

		// return true half the time
		public static int Alternate(int seconds, int elements)
		{
			return ((int)Time.realtimeSinceStartup / seconds) % elements;
		}


		// --- REFLECTION -----------------------------------------------------------

		private static readonly BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

		// return a value from a module using reflection
		// note: useful when the module is from another assembly, unknown at build time
		// note: useful when the value isn't persistent
		// note: this function break hard when external API change, by design
		public static T ReflectionValue<T>(PartModule m, string value_name)
		{
			return (T)m.GetType().GetField(value_name, flags).GetValue(m);
		}

		public static T? SafeReflectionValue<T>(PartModule m, string value_name) where T : struct
		{
			FieldInfo fi = m.GetType().GetField(value_name, flags);
			if (fi == null)
				return null;
			return (T)fi.GetValue(m);
		}

		// set a value from a module using reflection
		// note: useful when the module is from another assembly, unknown at build time
		// note: useful when the value isn't persistent
		// note: this function break hard when external API change, by design
		public static void ReflectionValue<T>(PartModule m, string value_name, T value)
		{
			m.GetType().GetField(value_name, flags).SetValue(m, value);
		}

		///<summary> Sets the value of a private field via reflection </summary>
		public static void ReflectionValue<T>(object instance, string value_name, T value)
		{
			instance.GetType().GetField(value_name, flags).SetValue(instance, value);
		}

		///<summary> Returns the value of a private field via reflection </summary>
		public static T ReflectionValue<T>(object instance, string field_name)
		{
			return (T)instance.GetType().GetField(field_name, flags).GetValue(instance);
		}

		public static void ReflectionCall(object m, string call_name)
		{
			m.GetType().GetMethod(call_name, flags).Invoke(m, null);
		}

		public static T ReflectionCall<T>(object m, string call_name)
		{
			return (T)(m.GetType().GetMethod(call_name, flags).Invoke(m, null));
		}

		public static T ReflectionCall<T>(object m, string call_name, Type[] types, object[] parameters)
		{
			return (T)(m.GetType().GetMethod(call_name, flags, null, types, null).Invoke(m, parameters));
		}


		// --- STRING ---------------------------------------------------------------

		// return string limited to len, with ... at the end
		public static string Ellipsis(string s, uint len)
		{
			len = Math.Max(len, 3u);
			return s.Length <= len ? s : Lib.BuildString(s.Substring(0, (int)len - 3), "...");
		}

		// tokenize a string
		public static List<string> Tokenize(string txt, char separator)
		{
			List<string> ret = new List<string>();
			string[] strings = txt.Split(separator);
			foreach (string s in strings)
			{
				string trimmed = s.Trim();
				if (trimmed.Length > 0) ret.Add(trimmed);
			}
			return ret;
		}

		// return message with the macro expanded
		// - variant: tokenize the string by '|' and select one
		public static string ExpandMsg(string txt, Vessel v = null, ProtoCrewMember c = null, uint variant = 0)
		{
			// get variant
			var variants = txt.Split('|');
			if (variants.Length > variant) txt = variants[variant];

			// macro expansion
			string v_name = v != null ? (v.isEVA ? "EVA" : v.vesselName) : "";
			string c_name = c != null ? c.name : "";
			return txt
			  .Replace("@", "\n")
			  .Replace("$VESSEL", BuildString("<b>", v_name, "</b>"))
			  .Replace("$KERBAL", "<b>" + c_name + "</b>")
			  .Replace("$ON_VESSEL", v != null && v.isActiveVessel ? "" : BuildString("On <b>", v_name, "</b>, "))
			  .Replace("$HIS_HER", c != null && c.gender == ProtoCrewMember.Gender.Male ? "his" : "her");
		}

		// make the first letter uppercase
		public static string UppercaseFirst(string s)
		{
			return s.Length > 0 ? char.ToUpper(s[0]) + s.Substring(1) : string.Empty;
		}


		// return string with specified color if condition evaluate to true
		public static string Color(string s, bool cond, string clr)
		{
			return !cond ? s : BuildString("<color=", clr, ">", s, "</color>");
		}


		// return string with specified color and bold if stated
		public static string Color(string color, string s, bool bold = false)
		{
			if (string.IsNullOrEmpty(color))
				return !bold ? s : ("<b>" + s + "</b>");
			return !bold ? ("<color=" + color + ">" + s + "</color>") : ("<color=" + color + "><b>" + s + "</b></color>");
		}


		// return string in bold
		public static string Bold(string s)
		{
			return ("<b>" + s + "</b>");
		}


		// return string in italic
		public static string Italic(string s)
		{
			return ("<i>" + s + "</i>");
		}


		// add spaces on caps
		public static string SpacesOnCaps(string s)
		{
			return System.Text.RegularExpressions.Regex.Replace(s, "[A-Z]", " $0").TrimStart();
		}


		// convert to smart_case
		public static string SmartCase(string s)
		{
			return SpacesOnCaps(s).ToLower().Replace(' ', '_');
		}


		// select a string at random
		public static string TextVariant(params string[] list)
		{
			return list.Length == 0 ? string.Empty : list[RandomInt(list.Length)];
		}


		// --- BUILD STRING ---------------------------------------------------------

		// compose a set of strings together, without creating temporary objects
		// note: the objective here is to minimize number of temporary variables for GC
		// note: okay to call recursively, as long as all individual concatenation is atomic
		static StringBuilder sb = new StringBuilder(256);
		public static string BuildString(string a, string b)
		{
			sb.Length = 0;
			sb.Append(a);
			sb.Append(b);
			return sb.ToString();
		}
		public static string BuildString(string a, string b, string c)
		{
			sb.Length = 0;
			sb.Append(a);
			sb.Append(b);
			sb.Append(c);
			return sb.ToString();
		}
		public static string BuildString(string a, string b, string c, string d)
		{
			sb.Length = 0;
			sb.Append(a);
			sb.Append(b);
			sb.Append(c);
			sb.Append(d);
			return sb.ToString();
		}
		public static string BuildString(string a, string b, string c, string d, string e)
		{
			sb.Length = 0;
			sb.Append(a);
			sb.Append(b);
			sb.Append(c);
			sb.Append(d);
			sb.Append(e);
			return sb.ToString();
		}
		public static string BuildString(string a, string b, string c, string d, string e, string f)
		{
			sb.Length = 0;
			sb.Append(a);
			sb.Append(b);
			sb.Append(c);
			sb.Append(d);
			sb.Append(e);
			sb.Append(f);
			return sb.ToString();
		}
		public static string BuildString(string a, string b, string c, string d, string e, string f, string g)
		{
			sb.Length = 0;
			sb.Append(a);
			sb.Append(b);
			sb.Append(c);
			sb.Append(d);
			sb.Append(e);
			sb.Append(f);
			sb.Append(g);
			return sb.ToString();
		}
		public static string BuildString(string a, string b, string c, string d, string e, string f, string g, string h)
		{
			sb.Length = 0;
			sb.Append(a);
			sb.Append(b);
			sb.Append(c);
			sb.Append(d);
			sb.Append(e);
			sb.Append(f);
			sb.Append(g);
			sb.Append(h);
			return sb.ToString();
		}
		public static string BuildString(params string[] args)
		{
			sb.Length = 0;
			foreach (string s in args) sb.Append(s);
			return sb.ToString();
		}


		// --- HUMAN READABLE -------------------------------------------------------

		///<summary> Pretty-print a resource rate (rate is per second, must be positive) </summary>
		public static string HumanReadableRate(double rate, string precision = "F3")
		{
			if (rate <= double.Epsilon) return "none";
			if (rate >= 0.01) return BuildString(rate.ToString(precision), "/s");
			rate *= 60.0; // per-minute
			if (rate >= 0.01) return BuildString(rate.ToString(precision), "/m");
			rate *= 60.0; // per-hour
			if (rate >= 0.01) return BuildString(rate.ToString(precision), "/h");
			rate *= HoursInDay();  // per-day
			if (rate >= 0.01) return BuildString(rate.ToString(precision), "/d");
			return BuildString((rate * DaysInYear()).ToString(precision), "/y");
		}

		///<summary> Pretty-print a duration (duration is in seconds, must be positive) </summary>
		public static string HumanReadableDuration(double duration)
		{
			if (duration <= double.Epsilon) return "none";
			if (double.IsInfinity(duration) || double.IsNaN(duration)) return "perpetual";

			double hours_in_day = HoursInDay();
			double days_in_year = DaysInYear();

			// seconds
			if (duration < 60.0) return BuildString(duration.ToString("F0"), "s");

			// minutes + seconds
			double duration_min = Math.Floor(duration / 60.0);
			duration -= duration_min * 60.0;
			if (duration_min < 60.0) return BuildString(duration_min.ToString("F0"), "m", (duration < 1.0 ? "" : BuildString(" ", duration.ToString("F0"), "s")));

			// hours + minutes
			double duration_h = Math.Floor(duration_min / 60.0);
			duration_min -= duration_h * 60.0;
			if (duration_h < hours_in_day) return BuildString(duration_h.ToString("F0"), "h", (duration_min < 1.0 ? "" : BuildString(" ", duration_min.ToString("F0"), "m")));

			// days + hours
			double duration_d = Math.Floor(duration_h / hours_in_day);
			duration_h -= duration_d * hours_in_day;
			if (duration_d < days_in_year) return BuildString(duration_d.ToString("F0"), "d", (duration_h < 1.0 ? "" : BuildString(" ", duration_h.ToString("F0"), "h")));

			// years + days
			double duration_y = Math.Floor(duration_d / days_in_year);
			duration_d -= duration_y * days_in_year;
			return BuildString(duration_y.ToString("F0"), "y", (duration_d < 1.0 ? "" : BuildString(" ", duration_d.ToString("F0"), "d")));
		}

		public static string HumanReadableCountdown(double d)
		{
			if (d <= double.Epsilon) return string.Empty;
			if (double.IsInfinity(d) || double.IsNaN(d)) return "never";

			double hours_in_day = HoursInDay();
			double days_in_year = DaysInYear();

			int duration = (int)d;
			int seconds = duration % 60;
			duration /= 60;
			int minutes = duration % 60;
			duration /= 60;
			int hours = duration % (int)hours_in_day;
			duration /= (int)hours_in_day;
			int days = duration % (int)days_in_year;
			int years = duration / (int)days_in_year;

			string result = "T-";
			if (years > 0) result += years + "y ";
			if (years > 0 || days > 0) result += days + "d ";
			if (years > 0 || days > 0 || hours > 0) result += hours.ToString("D2") + ":";
			if (years > 0 || days > 0 || hours > 0 || minutes > 0) result += minutes.ToString("D2") + ":";
			result += seconds.ToString("D2");

			return result;
		}

		///<summary> Pretty-print a range (range is in meters, must be positive) </summary>
		public static string HumanReadableRange(double range)
		{
			if (range <= double.Epsilon) return "none";
			if (range < 1000.0) return BuildString(range.ToString("F1"), " m");
			range /= 1000.0;
			if (range < 1000.0) return BuildString(range.ToString("F1"), " Km");
			range /= 1000.0;
			if (range < 1000.0) return BuildString(range.ToString("F1"), " Mm");
			range /= 1000.0;
			if (range < 1000.0) return BuildString(range.ToString("F1"), " Gm");
			range /= 1000.0;
			if (range < 1000.0) return BuildString(range.ToString("F1"), " Tm");
			range /= 1000.0;
			if (range < 1000.0) return BuildString(range.ToString("F1"), " Pm");
			range /= 1000.0;
			return BuildString(range.ToString("F1"), " Em");
		}

		///<summary> Pretty-print a speed (in meters/sec, must be positive) </summary>
		public static string HumanReadableSpeed(double speed)
		{
			return Lib.BuildString(HumanReadableRange(speed), "/s");
		}

		///<summary> Pretty-print temperature </summary>
		public static string HumanReadableTemp(double temp)
		{
			return BuildString(temp.ToString("F1"), " K");
		}

		///<summary> Pretty-print angle </summary>
		public static string HumanReadableAngle(double angle)
		{
			return BuildString(angle >= 0.0001 ? angle.ToString("F1") : "0", " °");
		}

		///<summary> Pretty-print flux </summary>
		public static string HumanReadableFlux(double flux)
		{
			return BuildString(flux >= 0.0001 ? flux.ToString("F1") : flux.ToString(), " W/m²");
		}

		///<summary> Pretty-print magnetic strength </summary>
		public static string HumanReadableField(double strength)
		{
			return BuildString(strength.ToString("F1"), " uT"); //< micro-tesla
		}

		///<summary> Pretty-print radiation rate </summary>
		public static string HumanReadableRadiation(double rad)
		{
			if (rad <= double.Epsilon) return "none";
			else if (rad <= 0.0000002777) return "nominal";
			return BuildString((rad * 3600.0).ToString("F3"), " rad/h");
		}

		///<summary> Pretty-print percentage </summary>
		public static string HumanReadablePerc(double v, string format = "F0")
		{
			return BuildString((v * 100.0).ToString(format), "%");
		}

		///<summary> Pretty-print pressure (value is in kPa) </summary>
		public static string HumanReadablePressure(double v)
		{
			return Lib.BuildString(v.ToString("F1"), " kPa");
		}

		///<summary> Pretty-print volume (value is in m^3) </summary>
		public static string HumanReadableVolume(double v)
		{
			return Lib.BuildString(v.ToString("F2"), " m³");
		}

		///<summary> Pretty-print surface (value is in m^2) </summary>
		public static string HumanReadableSurface(double v)
		{
			return Lib.BuildString(v.ToString("F2"), " m²");
		}

		///<summary> Pretty-print mass </summary>
		public static string HumanReadableMass(double v)
		{
			if (v <= double.Epsilon) return "0 kg";
			if (v > 1) return Lib.BuildString(v.ToString("F3"), " t");
			v *= 1000;
			if (v > 1) return Lib.BuildString(v.ToString("F2"), " kg");
			v *= 1000;
			return Lib.BuildString(v.ToString("F2"), " g");
		}

		///<summary> Pretty-print cost </summary>
		public static string HumanReadableCost(double v)
		{
			return Lib.BuildString(v.ToString("F0"), " $");
		}

		///<summary> Format a value to 2 decimal places, or return 'none' </summary>
		public static string HumanReadableAmount(double value, string append = "")
		{
			return (Math.Abs(value) <= double.Epsilon ? "none" : BuildString(value.ToString("F2"), append));
		}

		///<summary> Format an integer value, or return 'none' </summary>
		public static string HumanReadableInteger(uint value, string append = "")
		{
			return (Math.Abs(value) <= 0 ? "none" : BuildString(value.ToString("F0"), append));
		}

		///<summary> Format data size, the size parameter is in MB (megabytes) </summary>
		public static string HumanReadableDataSize(double size)
		{
			size *= 1024.0 * 1024.0 * 8.0; //< bits
			if (size < 0.01) return "none";
			if (size <= 32.0) return BuildString(size.ToString("F0"), " b");
			size /= 8; //< to bytes
			if (size < 1024.0) return BuildString(size.ToString("F0"), " B");
			size /= 1024.0;
			if (size < 1024.0) return BuildString(size.ToString("F2"), " kB");
			size /= 1024.0;
			if (size < 1024.0) return BuildString(size.ToString("F2"), " MB");
			size /= 1024.0;
			if (size < 1024.0) return BuildString(size.ToString("F2"), " GB");
			size /= 1024.0;
			return BuildString(size.ToString("F2"), " TB");
		}

		///<summary> Format data rate, the rate parameter is in Mb/s </summary>
		public static string HumanReadableDataRate(double rate)
		{
			return rate < double.Epsilon ? "none" : Lib.BuildString(HumanReadableDataSize(rate), "/s");
		}

		public static string HumanReadableSampleSize(double size)
		{
			return HumanReadableSampleSize(SampleSizeToSlots(size));
		}

		public static string HumanReadableSampleSize(int slots)
		{
			if (slots <= 0) return Lib.BuildString("no ", Localizer.Format("#KERBALISM_Generic_SLOT"));

			return Lib.BuildString(slots.ToString(), " ", slots > 1 ? Localizer.Format("#KERBALISM_Generic_SLOTS") : Localizer.Format("#KERBALISM_Generic_SLOT"));
		}

		public static int SampleSizeToSlots(double size)
		{
			int result = (int)(size / 1024);
			if (result * 1024 < size) ++result;
			return result;
		}

		public static double SlotsToSampleSize(int slots)
		{
			return slots * 1024;
		}

		///<summary> Format science credits </summary>
		public static string HumanReadableScience(double value)
		{
			return Lib.BuildString("<color=cyan>", value.ToString("F1"), " CREDITS</color>");
		}


		// --- GAME LOGIC -----------------------------------------------------------

		// return true if the current scene is flight
		public static bool IsFlight()
		{
			return HighLogic.LoadedSceneIsFlight;
		}

		// return true if the current scene is editor
		public static bool IsEditor()
		{
			return HighLogic.LoadedSceneIsEditor;
		}

		// return true if the current scene is not the main menu
		public static bool IsGame()
		{
			return HighLogic.LoadedSceneIsGame;
		}

		// return true if game is paused
		public static bool IsPaused()
		{
			return FlightDriver.Pause || Planetarium.Pause;
		}

		// return true if a tutorial scenario or making history mission is active
		public static bool IsScenario()
		{
			return HighLogic.CurrentGame.Mode == Game.Modes.SCENARIO
				|| HighLogic.CurrentGame.Mode == Game.Modes.SCENARIO_NON_RESUMABLE
				|| HighLogic.CurrentGame.Mode == Game.Modes.MISSION_BUILDER
				|| HighLogic.CurrentGame.Mode == Game.Modes.MISSION;
		}

		// disable the module and return true if a tutorial scenario is active
		public static bool DisableScenario(PartModule m)
		{
			if (IsScenario())
			{
				m.enabled = false;
				m.isEnabled = false;
				return true;
			}
			return false;
		}


		// --- BODY -----------------------------------------------------------------

		// return reference body of the planetary system that contain the specified body
		public static CelestialBody PlanetarySystem(CelestialBody body)
		{
			if (Lib.IsSun(body)) return body;
			while (!Lib.IsSun(body)) body = body.referenceBody;
			return body;
		}

		// return selected body in tracking-view/map-view
		// if a vessel is selected, return its main body
		public static CelestialBody SelectedBody()
		{
			var target = PlanetariumCamera.fetch.target;
			return
				target == null ? null : target.celestialBody ?? target.vessel?.mainBody;
		}

		// return terrain height at point specified
		// - body terrain must be loaded for this to work: use it only for loaded vessels
		public static double TerrainHeight(CelestialBody body, Vector3d pos)
		{
			PQS pqs = body.pqsController;
			if (pqs == null) return 0.0;
			Vector2d latlong = body.GetLatitudeAndLongitude(pos);
			Vector3d radial = QuaternionD.AngleAxis(latlong.y, Vector3d.down) * QuaternionD.AngleAxis(latlong.x, Vector3d.forward) * Vector3d.right;
			return (pos - body.position).magnitude - pqs.GetSurfaceHeight(radial);
		}

		public static double SunBodyAngle(Vessel v)
		{
			// orbit around sun?
			if (IsSun(v.mainBody))
			{
				return 0;
			}

			var body_vessel = v.mainBody.position - Lib.VesselPosition(v);
			var body_sun = v.mainBody.position - GetSun(v.mainBody).position;

			return Vector3d.Angle(body_vessel, body_sun);
		}

		private static readonly Dictionary<int, bool> _IsSun = new Dictionary<int, bool>();
		public static bool IsSun(CelestialBody body)
		{
			if(_IsSun.ContainsKey(body.flightGlobalsIndex))
			{
				return _IsSun[body.flightGlobalsIndex];
			}

			// Kopernicus stores solar luminosity in its own component
			foreach (var c in body.GetComponentsInChildren<MonoBehaviour>(true))
			{
				if (c.GetType().ToString() == "LightShifter")
				{
					_IsSun[body.flightGlobalsIndex] = true;
					return true;
				}
			}

			var result = body.flightGlobalsIndex == 0 || body.referenceBody == null;
			_IsSun[body.flightGlobalsIndex] = result;
			return result;
		}

		public static CelestialBody GetSun(CelestialBody body)
		{
			if (IsSun(body))
			{
				return body;
			}

			var b = body.referenceBody;
			do
			{
				if (IsSun(b))
				{
					return b;
				}
				b = b.referenceBody;
			} while (b != null);

			return FlightGlobals.Bodies[0];
		}

		// --- VESSEL ---------------------------------------------------------------

		// return true if landed somewhere
		public static bool Landed(Vessel v)
		{
			if (v.loaded) return v.Landed || v.Splashed;
			else return v.protoVessel.landed || v.protoVessel.splashed;
		}

		// return vessel position
		public static Vector3d VesselPosition(Vessel v)
		{
			// the issue
			//   - GetWorldPos3D() return mainBody position for a few ticks after scene changes
			//   - we can detect that, and fall back to evaluating position from the orbit
			//   - orbit is not valid if the vessel is landed, and for a tick on prelaunch/staging/decoupling
			//   - evaluating position from latitude/longitude work in all cases, but is probably the slowest method

			// get vessel position
			Vector3d pos = v.GetWorldPos3D();

			// during scene changes, it will return mainBody position
			if (Vector3d.SqrMagnitude(pos - v.mainBody.position) < 1.0)
			{
				// try to get it from orbit
				pos = v.orbit.getPositionAtUT(Planetarium.GetUniversalTime());

				// if the orbit is invalid (landed, or 1 tick after prelaunch/staging/decoupling)
				if (double.IsNaN(pos.x))
				{
					// get it from lat/long (work even if it isn't landed)
					pos = v.mainBody.GetWorldSurfacePosition(v.latitude, v.longitude, v.altitude);
				}
			}

			// victory
			return pos;
		}


		// return set of crew on a vessel
		public static List<ProtoCrewMember> CrewList(Vessel v)
		{
			return v.loaded ? v.GetVesselCrew() : v.protoVessel.GetVesselCrew();
		}


		// return crew count of a vessel
		public static int CrewCount(Vessel v)
		{
			return v.isEVA ? 1 : CrewList(v).Count;
		}

		// return crew capacity of a vessel
		public static int CrewCapacity(Vessel v)
		{
			if (v.isEVA) return 1;
			if (v.loaded)
			{
				return v.GetCrewCapacity();
			}
			else
			{
				int capacity = 0;
				foreach (ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
				{
					capacity += p.partInfo.partPrefab.CrewCapacity;
				}
				return capacity;
			}
		}


		// return true if this is a 'vessel'
		public static bool IsVessel(Vessel v)
		{
			// something weird is going on
			if (v == null) return false;

			// if the vessel is in DEAD status, we consider it invalid
			if (v.state == Vessel.State.DEAD) return false;

			// if the vessel is a debris, a flag or an asteroid, ignore it
			// - the user can change vessel type, in that case he is actually disabling this mod for the vessel
			//   the alternative is to scan the vessel for ModuleCommand, but that is slower, and rescue vessels have no module command
			// - flags have type set to 'station' for a single update, can still be detected as they have vesselID == 0
			switch(v.vesselType)
			{
				case VesselType.Debris:
				case VesselType.Flag:
				case VesselType.SpaceObject:
				case VesselType.Unknown:
#if !KSP170 && !KSP16 && !KSP15 && !KSP14
				case VesselType.DeployedSciencePart:
#endif
					return false;
			}

			// [disabled] when going to eva (and possibly other occasions), for a single update the vessel is not properly set
			// this can be detected by vessel.distanceToSun being 0 (an impossibility otherwise)
			// in this case, just wait a tick for the data being set by the game engine
			//if (v.loaded && v.distanceToSun <= double.Epsilon) return false;

			// the vessel is valid
			return true;
		}


#if !KSP170 && !KSP16 && !KSP15 && !KSP14
		public static bool IsControlUnit(Vessel v)
		{
			return Serenity.GetScienceCluster(v) != null;
		}
#else
		public static bool IsControlUnit(Vessel v) {
			return false;
		}
#endif

		public static bool IsPowered(Vessel v)
		{
#if !KSP170 && !KSP16 && !KSP15 && !KSP14
			var cluster = Serenity.GetScienceCluster(v);
			if (cluster != null)
				return cluster.IsPowered;
#endif
			return ResourceCache.Info(v, "ElectricCharge").amount > double.Epsilon;
		}

		public static Guid VesselID(Vessel v)
		{
			// Lesson learned: v.persistendId is not unique. Far from it, in fact.

			// neither is this ----vvv (see https://github.com/steamp0rt/Kerbalism/issues/370)
			//byte[] b = v.id.ToByteArray();
			//UInt64 result = BitConverter.ToUInt64(b, 0);
			//result ^= BitConverter.ToUInt64(b, 8);
			//return result;
			// --------------------^^^

			// maybe this?
			// return RootID(v); // <-- nope. not unique.
			return v.id;
		}

		public static Guid VesselID(ProtoVessel pv)
		{
			// nope
			//byte[] b = pv.vesselID.ToByteArray();
			//UInt64 result = BitConverter.ToUInt64(b, 0);
			//result ^= BitConverter.ToUInt64(b, 8);
			//return result;
			//return pv.protoPartSnapshots[pv.rootIndex].flightID;
			return pv.vesselID;
		}

		public static Vessel CommNodeToVessel(CommNode node)
		{
			// Is is home return null
			if (node.isHome) return null;

			foreach (Vessel v in FlightGlobals.Vessels)
			{
				if (!IsVessel(v)) continue;

				if (AreSame(node, v.connection.Comm))
				{
					return v;
				}
			}

			Log("The node " + node.name + " is not valid.");
			return null;
		}

		public static bool AreSame(CommNode a, CommNode b)
		{
			if (a == null || b == null)
			{
				return false;
			}

			return a.precisePosition == b.precisePosition;
		}

		// --- PART -----------------------------------------------------------------

		// get list of parts recursively, useful from the editors
		public static List<Part> GetPartsRecursively(Part root)
		{
			List<Part> ret = new List<Part>
			{
				root
			};
			foreach (Part p in root.children)
			{
				ret.AddRange(GetPartsRecursively(p));
			}
			return ret;
		}

		// return the name of a part
		public static string PartName(Part p)
		{
			return p.partInfo.name;
		}

		// return the volume of a part, in m^3
		// note: this can only be called when part has not been rotated
		//       we could use the partPrefab bounding box, but then it isn't available in GetInfo()
		public static double PartVolume(Part p)
		{
			return PartVolume(p.GetPartRendererBound());
		}

		public static double PartVolume(Bounds bb)
		{
			return bb.size.x * bb.size.y * bb.size.z * 0.785398;
		}

		// return the surface of a part, in m^2
		// note: this can only be called when part has not been rotated
		//       we could use the partPrefab bounding box, but then it isn't available in GetInfo()
		public static double PartSurface(Part p)
		{
			return PartSurface(p.GetPartRendererBound());
		}

		public static double PartSurface(Bounds bb)
		{
			double a = bb.extents.x;
			double b = bb.extents.y;
			double c = bb.extents.z;
			return 2.0 * (a * b + a * c + b * c) * 0.95493;
		}

		public static int CrewCount(Part part)
		{
			// outside of the editors, it is easy
			if (!Lib.IsEditor())
			{
				return part.protoModuleCrew.Count;
			}

			// in the editor we need something more involved
			Int64 part_id = 4294967296L + part.GetInstanceID();
			var manifest = KSP.UI.CrewAssignmentDialog.Instance.GetManifest();
			var part_manifest = manifest.GetCrewableParts().Find(k => k.PartID == part_id);
			if (part_manifest != null)
			{
				int result = 0;
				foreach (var s in part_manifest.partCrew)
				{
					if (!string.IsNullOrEmpty(s)) result++;
				}
				return result;
			}

			return 0;
		}

		// return true if a part is manned, even in the editor
		public static bool IsCrewed(Part p)
		{
			return CrewCount(p) > 0;
		}


		// --- MODULE ---------------------------------------------------------------

		// return all modules implementing a specific type in a vessel
		// note: disabled modules are not returned
		public static List<T> FindModules<T>(Vessel v) where T : class
		{
			List<T> ret = new List<T>();
			for (int i = 0; i < v.parts.Count; ++i)
			{
				Part p = v.parts[i];
				for (int j = 0; j < p.Modules.Count; ++j)
				{
					PartModule m = p.Modules[j];
					if (m.isEnabled)
					{
						if (m is T t)
							ret.Add(t);
					}
				}
			}
			return ret;
		}

		public static bool HasPart(Vessel v, string part_name)
		{
			if (Cache.HasVesselObjectsCache(v, "has_part:" + part_name))
				return Cache.VesselObjectsCache<bool>(v, "has_part:" + part_name);

			bool ret = false;
			foreach(string name in Tokenize(part_name, ','))
			{
				if (v.loaded)
					ret = v.parts.Find(k => k.name.StartsWith(part_name, StringComparison.Ordinal)) != null;
				else
					ret = v.protoVessel.protoPartSnapshots.Find(k => k.partName.StartsWith(part_name, StringComparison.Ordinal)) != null;
				if (ret) break;
			}

			Cache.SetVesselObjectsCache(v, "has_part:" + part_name, ret);
			return ret;
		}

		/// <summary>
		/// return all proto modules with a specified name in a vessel.
		/// note: disabled modules are not returned
		/// </summary>
		public static List<ProtoPartModuleSnapshot> FindModules(ProtoVessel v, string module_name)
		{
			var ret = Cache.VesselObjectsCache<List<ProtoPartModuleSnapshot>>(v, "mod:" + module_name);
			if (ret != null)
				return ret;

			ret = new List<ProtoPartModuleSnapshot>(8);
			for (int i = 0; i < v.protoPartSnapshots.Count; ++i)
			{
				ProtoPartSnapshot p = v.protoPartSnapshots[i];
				ret.AddRange(FindModules(p, module_name));
			}

			Cache.SetVesselObjectsCache(v, "mod:" + module_name, ret);
			return ret;
		}

		// return all proto modules with a specified name in a part
		// note: disabled modules are not returned
		public static List<ProtoPartModuleSnapshot> FindModules(ProtoPartSnapshot p, string module_name)
		{
			List<ProtoPartModuleSnapshot> ret = new List<ProtoPartModuleSnapshot>(8);
			for (int j = 0; j < p.modules.Count; ++j)
			{
				ProtoPartModuleSnapshot m = p.modules[j];
				if (m.moduleName == module_name && Proto.GetBool(m, "isEnabled"))
				{
					ret.Add(m);
				}
			}
			return ret;
		}

		// return true if a module implementing a specific type and satisfying the predicate specified exist in a vessel
		// note: disabled modules are ignored
		public static bool HasModule<T>(Vessel v, Predicate<T> filter) where T : class
		{
			for (int i = 0; i < v.parts.Count; ++i)
			{
				Part p = v.parts[i];
				for (int j = 0; j < p.Modules.Count; ++j)
				{
					PartModule m = p.Modules[j];
					if (m.isEnabled)
					{
						if (m is T t && filter(t))
							return true;
					}
				}
			}
			return false;
		}

		// return true if a proto module with the specified name and satisfying the predicate specified exist in a vessel
		// note: disabled modules are not returned
		public static bool HasModule(ProtoVessel v, string module_name, Predicate<ProtoPartModuleSnapshot> filter)
		{
			for (int i = 0; i < v.protoPartSnapshots.Count; ++i)
			{
				ProtoPartSnapshot p = v.protoPartSnapshots[i];
				for (int j = 0; j < p.modules.Count; ++j)
				{
					ProtoPartModuleSnapshot m = p.modules[j];
					if (m.moduleName == module_name && Proto.GetBool(m, "isEnabled") && filter(m))
					{
						return true;
					}
				}
			}
			return false;
		}

		// used by ModulePrefab function, to support multiple modules of the same type in a part
		public sealed class Module_prefab_data
		{
			public int index;                         // index of current module of this type
			public List<PartModule> prefabs;          // set of module prefabs of this type
		}

		// get module prefab
		//  This function is used to solve the problem of obtaining a specific module prefab,
		//  and support the case where there are multiple modules of the same type in the part.
		public static PartModule ModulePrefab(List<PartModule> module_prefabs, string module_name, Dictionary<string, Module_prefab_data> PD)
		{
			// get data related to this module type, or create it
			Module_prefab_data data;
			if (!PD.TryGetValue(module_name, out data))
			{
				data = new Module_prefab_data
				{
					prefabs = module_prefabs.FindAll(k => k.moduleName == module_name)
				};
				PD.Add(module_name, data);
			}

			// return the module prefab, and increment module-specific index
			// note: if something messed up the prefab, or module were added dynamically,
			// then we have no chances of finding the module prefab so we return null
			return data.index < data.prefabs.Count ? data.prefabs[data.index++] : null;
		}

		// --- RESOURCE -------------------------------------------------------------

		/// <summary> Returns the amount of a resource in a part </summary>
		public static double Amount(Part part, string resource_name, bool ignore_flow = false)
		{
			foreach (PartResource res in part.Resources)
			{
				if ((res.flowState || ignore_flow) && res.resourceName == resource_name) return res.amount;
			}
			return 0.0;
		}

		/// <summary> Returns the capacity of a resource in a part </summary>
		public static double Capacity(Part part, string resource_name, bool ignore_flow = false)
		{
			foreach (PartResource res in part.Resources)
			{
				if ((res.flowState || ignore_flow) && res.resourceName == resource_name) return res.maxAmount;
			}
			return 0.0;
		}

		/// <summary> Returns the level of a resource in a part </summary>
		public static double Level(Part part, string resource_name, bool ignore_flow = false)
		{
			foreach (PartResource res in part.Resources)
			{
				if ((res.flowState || ignore_flow) && res.resourceName == resource_name)
				{
					return res.maxAmount > double.Epsilon ? res.amount / res.maxAmount : 0.0;
				}
			}
			return 0.0;
		}

		/// <summary> Adds the specified resource amount and capacity to a part,
		/// the resource is created if it doesn't already exist </summary>
		// poached from https://github.com/blowfishpro/B9PartSwitch/blob/master/B9PartSwitch/Extensions/PartExtensions.cs
		public static void AddResource(Part p, string res_name, double amount, double capacity)
		{
#if !KSP14
			var reslib = PartResourceLibrary.Instance.resourceDefinitions;
			// if the resource is not known, log a warning and do nothing
			if (!reslib.Contains(res_name))
			{
				Lib.Log(Lib.BuildString("error while adding ", res_name, ": the resource doesn't exist"));
				return;
			}
			var resourceDefinition = reslib[res_name];

			amount = Math.Min(amount, capacity);
			amount = Math.Max(amount, 0);
			PartResource resource = p.Resources[resourceDefinition.name];

			if (resource == null)
			{
				resource = new PartResource(p);
				resource.SetInfo(resourceDefinition);
				resource.maxAmount = capacity;
				resource.amount = amount;
				resource.flowState = true;
				resource.isTweakable = resourceDefinition.isTweakable;
				resource.isVisible = resourceDefinition.isVisible;
				resource.hideFlow = false;
				resource.flowMode = PartResource.FlowMode.Both;
				p.Resources.dict.Add(resourceDefinition.name.GetHashCode(), resource);

				PartResource simulationResource = new PartResource(resource);
				simulationResource.simulationResource = true;
				p.SimulationResources?.dict.Add(resourceDefinition.name.GetHashCode(), simulationResource);

				GameEvents.onPartResourceListChange.Fire(p);
			}
			else
			{
				resource.maxAmount = capacity;

				PartResource simulationResource = p.SimulationResources?[resourceDefinition.name];
				if (simulationResource != null) simulationResource.maxAmount = capacity;

				resource.amount = amount;
			}
#else
			// if the resource is already in the part
			if (p.Resources.Contains(res_name))
			{
				// add amount and capacity
				var res = p.Resources[res_name];
				res.amount += amount;
				res.maxAmount += capacity;
			}
			// if the resource is not already in the part
			else
			{
				// shortcut to resource library
				var reslib = PartResourceLibrary.Instance.resourceDefinitions;

				// if the resource is not known, log a warning and do nothing
				if (!reslib.Contains(res_name))
				{
					Lib.Log(Lib.BuildString("error while adding ", res_name, ": the resource doesn't exist"));
					return;
				}

				// get resource definition
				var def = reslib[res_name];

				// create the resource
				ConfigNode res = new ConfigNode("RESOURCE");
				res.AddValue("name", res_name);
				res.AddValue("amount", amount);
				res.AddValue("maxAmount", capacity);

				// add it to the part
				p.Resources.Add(res);
			}
#endif
		}

		/// <summary> Removes the specified resource amount and capacity from a part,
		/// the resource is removed completely if the capacity reaches zero </summary>
		public static void RemoveResource(Part p, string res_name, double amount, double capacity)
		{
#if !KSP14
			// if the resource is not in the part, do nothing
			if (!p.Resources.Contains(res_name))
				return;

			// get the resource
			var res = p.Resources[res_name];

			// reduce amount and capacity
			res.amount -= amount;
			res.maxAmount -= capacity;

			// clamp amount to capacity just in case
			res.amount = Math.Min(res.amount, res.maxAmount);

			// if the resource is empty
			if (res.maxAmount <= 0.005) //< deal with precision issues
			{
				var reslib = PartResourceLibrary.Instance.resourceDefinitions;
				var resourceDefinition = reslib[res_name];

				p.Resources.dict.Remove(resourceDefinition.name.GetHashCode());
				p.SimulationResources?.dict.Remove(resourceDefinition.name.GetHashCode());

				GameEvents.onPartResourceListChange.Fire(p);
			}
#else
			// if the resource is not already in the part, do nothing
			if (p.Resources.Contains(res_name))
			{
				// get the resource
				var res = p.Resources[res_name];

				// reduce amount and capacity
				res.amount -= amount;
				res.maxAmount -= capacity;

				// clamp amount to capacity just in case
				res.amount = Math.Min(res.amount, res.maxAmount);

				// if the resource is empty
				if (res.maxAmount <= 0.005) //< deal with precision issues
				{
					// remove it
					p.Resources.Remove(res);
				}
			}
#endif
		}

		// note: the resource must exist
		public static void SetResourceCapacity( Part p, string res_name, double capacity )
		{
			// if the resource is not in the part, log a warning and do nothing
			if (!p.Resources.Contains( res_name ))
			{
				Lib.Log( Lib.BuildString( "error while setting capacity for ", res_name, ": the resource is not in the part" ) );
				return;
			}

			// set capacity and clamp amount
			var res = p.Resources[res_name];
			res.maxAmount = capacity;
			res.amount = Math.Min( res.amount, capacity );
		}

		// note: the resource must exist
		public static void SetResource( Part p, string res_name, double amount, double capacity )
		{
			// if the resource is not in the part, log a warning and do nothing
			if (!p.Resources.Contains( res_name ))
			{
				Lib.Log( Lib.BuildString( "error while setting capacity for ", res_name, ": the resource is not in the part" ) );
				return;
			}

			// set capacity and clamp amount
			var res = p.Resources[res_name];
			res.maxAmount = capacity;
			res.amount = Math.Min( amount, capacity );
		}

		/// <summary> Set flow of a resource in the specified part. Does nothing if the resource does not exist in the part </summary>
		public static void SetResourceFlow(Part p, string res_name, bool enable)
		{
			// if the resource is not in the part, do nothing
			if (p.Resources.Contains( res_name ))
			{
				// set flow state
				var res = p.Resources[res_name];
				res.flowState = enable;
			} else {
				Lib.DebugLog("Resource " + res_name + " not in part " + p.name);
			}
		}

		/// <summary> Fills a resource in the specified part to its capacity </summary>
		public static void FillResource(Part p, string res_name)
		{
			// if the resource is not in the part, do nothing
			if (p.Resources.Contains(res_name))
			{
				PartResource res = p.Resources[res_name];
				res.amount = res.maxAmount;
			}
			else {
				Lib.DebugLog("Resource " + res_name + " not in part " + p.name); }
		}

		/// <summary> Sets the amount of a resource in the specified part to zero </summary>
		public static void EmptyResource(Part p, string res_name)
		{
			// if the resource is not in the part, do nothing
			if (p.Resources.Contains(res_name))
				p.Resources[res_name].amount = 0.0;
			else {
				Lib.DebugLog("Resource " + res_name + " not in part " + p.name); }
		}

		/// <summary> Set the enabled/disabled state of a process
		/// <para> Use the process_capacity parameter to set the pseudo resource amount for the process,
		/// an amount of 0.0 disables the process, any non-zero value is a multiplier of the process.
		/// </para> </summary>
		public static void SetProcessEnabledDisabled(Part p, string res_name, bool enable, double process_capacity)
		{
			if (!p.Resources.Contains(res_name))
			{
				Lib.AddResource(p, res_name, 0.0, process_capacity);
			}

			if (enable)
			{
				SetResource(p, res_name, process_capacity, process_capacity);
			}
			else
			{
				// Never remove the resource capacity, otherwise checks against
				// the pseudo resource might fail
				SetResource(p, res_name, 0.0, process_capacity);
			}
		}

		/// <summary> Returns the definition of a resource, or null if it doesn't exist </summary>
		public static PartResourceDefinition GetDefinition( string name )
		{
			// shortcut to the resource library
			var reslib = PartResourceLibrary.Instance.resourceDefinitions;

			// return the resource definition, or null if it doesn't exist
			return reslib.Contains( name ) ? reslib[name] : null;
		}

		/// <summary> Returns name of propellant used on eva </summary>
		public static string EvaPropellantName()
		{
			// first, get the kerbal eva part prefab
			Part p = PartLoader.getPartInfoByName( "kerbalEVA" ).partPrefab;

			// then get the KerbalEVA module prefab
			KerbalEVA m = p.FindModuleImplementing<KerbalEVA>();

			// finally, return the propellant name
			return m.propellantResourceName;
		}


		/// <summary> Returns capacity of propellant on eva </summary>
		public static double EvaPropellantCapacity()
		{
			// first, get the kerbal eva part prefab
			Part p = PartLoader.getPartInfoByName( "kerbalEVA" ).partPrefab;

			// then get the first resource and return capacity
			return p.Resources.Count == 0 ? 0.0 : p.Resources[0].maxAmount;
		}


		// --- SCIENCE DATA ---------------------------------------------------------

		// return true if there is experiment data on the vessel
		public static bool HasData( Vessel v )
		{
			// stock science system
			if (!Features.Science)
			{
				// if vessel is loaded
				if (v.loaded)
				{
					// iterate over all science containers/experiments and return true if there is data
					return Lib.HasModule<IScienceDataContainer>( v, k => k.GetData().Length > 0 );
				}
				// if not loaded
				else
				{
					// iterate over all science containers/experiments proto modules and return true if there is data
					return Lib.HasModule( v.protoVessel, "ModuleScienceContainer", k => k.moduleValues.GetNodes( "ScienceData" ).Length > 0 )
						|| Lib.HasModule( v.protoVessel, "ModuleScienceExperiment", k => k.moduleValues.GetNodes( "ScienceData" ).Length > 0 );
				}
			}
			// our own science system
			else
			{
				foreach (var drive in Drive.GetDrives(v, true))
					if (drive.files.Count > 0) return true;
				return false;
			}
		}

		// remove one experiment at random from the vessel
		public static void RemoveData( Vessel v )
		{
			// stock science system
			if (!Features.Science)
			{
				// if vessel is loaded
				if (v.loaded)
				{
					// get all science containers/experiments with data
					List<IScienceDataContainer> modules = Lib.FindModules<IScienceDataContainer>( v ).FindAll( k => k.GetData().Length > 0 );

					// remove a data sample at random
					if (modules.Count > 0)
					{
						IScienceDataContainer container = modules[Lib.RandomInt( modules.Count )];
						ScienceData[] data = container.GetData();
						container.DumpData( data[Lib.RandomInt( data.Length )] );
					}
				}
				// if not loaded
				else
				{
					// get all science containers/experiments with data
					var modules = new List<ProtoPartModuleSnapshot>();
					modules.AddRange( Lib.FindModules( v.protoVessel, "ModuleScienceContainer" ).FindAll( k => k.moduleValues.GetNodes( "ScienceData" ).Length > 0 ) );
					modules.AddRange( Lib.FindModules( v.protoVessel, "ModuleScienceExperiment" ).FindAll( k => k.moduleValues.GetNodes( "ScienceData" ).Length > 0 ) );

					// remove a data sample at random
					if (modules.Count > 0)
					{
						ProtoPartModuleSnapshot container = modules[Lib.RandomInt( modules.Count )];
						ConfigNode[] data = container.moduleValues.GetNodes( "ScienceData" );
						container.moduleValues.RemoveNode( data[Lib.RandomInt( data.Length )] );
					}
				}
			}
			// our own science system
			else
			{
				// select a file at random and remove it
				foreach (var drive in Drive.GetDrives(v, true))
				{
					if (drive.files.Count > 0) //< it should always be the case
					{
						string filename = string.Empty;
						int i = Lib.RandomInt(drive.files.Count);
						foreach (var pair in drive.files)
						{
							if (i-- == 0)
							{
								filename = pair.Key;
								break;
							}
						}
						drive.files.Remove(filename);
						break;
					}
				}
			}
		}


		// -- TECH ------------------------------------------------------------------

		// return true if the tech has been researched
		public static bool HasTech( string tech_id )
		{
			// if science is disabled, all technologies are considered available
			if (HighLogic.CurrentGame.Mode == Game.Modes.SANDBOX) return true;

			// if RnD is not initialized
			if (ResearchAndDevelopment.Instance == null)
			{
				// this should not happen, throw exception
				throw new Exception( "querying tech '" + tech_id + "' while TechTree is not ready" );
			}

			// get the tech
			return ResearchAndDevelopment.GetTechnologyState( tech_id ) == RDTech.State.Available;
		}

		// return number of techs researched among the list specified
		public static int CountTech( string[] techs )
		{
			int n = 0;
			foreach (string tech_id in techs) n += HasTech( tech_id ) ? 1 : 0;
			return n;
		}


		// --- ASSETS ---------------------------------------------------------------

		///<summary> Returns the path of the directory containing the DLL </summary>
		public static string Directory()
		{
			string dll_path = Assembly.GetExecutingAssembly().Location;
			return dll_path.Substring( 0, dll_path.LastIndexOf( Path.DirectorySeparatorChar ) );
		}

		///<summary> Loads a .png texture from the folder defined in <see cref="Icons.TexturePath"/> </summary>
		public static Texture2D GetTexture( string name, int width = 16, int height = 16 )
		{
			Texture2D texture = new Texture2D( width, height, TextureFormat.ARGB32, false );
			ImageConversion.LoadImage(texture, System.IO.File.ReadAllBytes(Icons.TexturePath + name + ".png"));
			return texture;
		}

		///<summary> Returns a scaled copy of the source texture </summary>
		public static Texture2D ScaledTexture( Texture2D src, int width, int height, FilterMode mode = FilterMode.Trilinear )
		{
			ScaleWithGPU( src, width, height, mode );

			Texture2D texture = new Texture2D( width, height, TextureFormat.ARGB32, false );
			texture.Resize( width, height );
			texture.ReadPixels( new Rect( 0, 0, width, height ), 0, 0, true );
			return texture;
		}

		///<summary> Scales the texture data of the source texture </summary>
		public static void ScaleTexture( Texture2D texture, int width, int height, FilterMode mode = FilterMode.Trilinear )
		{
			ScaleWithGPU( texture, width, height, mode );

			texture.Resize( width, height );
			texture.ReadPixels( new Rect( 0, 0, width, height ), 0, 0, true );
			texture.Apply( true );
		}

		///<summary>Renders the source texture into the RTT - used by the scaling methods ScaledTexture() and ScaleTexture() </summary>
		private static void ScaleWithGPU( Texture2D src, int width, int height, FilterMode fmode )
		{
			src.filterMode = fmode;
			src.Apply( true );

			RenderTexture rtt = new RenderTexture( width, height, 32 );
			Graphics.SetRenderTarget( rtt );
			GL.LoadPixelMatrix( 0, 1, 1, 0 );
			GL.Clear( true, true, new Color( 0, 0, 0, 0 ) );
			Graphics.DrawTexture( new Rect( 0, 0, 1, 1 ), src );
		}

		public static Dictionary<string, Material> shaders;
		///<summary> Returns a material from the specified shader </summary>
		public static Material GetShader( string name )
		{
			if (shaders == null)
			{
				shaders = new Dictionary<string, Material>();
				string platform = "windows";
				if (Application.platform == RuntimePlatform.LinuxPlayer) platform = "linux";
				else if (Application.platform == RuntimePlatform.OSXPlayer) platform = "osx";
#pragma warning disable CS0618 // WWW is obsolete
				using (WWW www = new WWW("file://" + KSPUtil.ApplicationRootPath + "GameData/Kerbalism/Shaders/" + VersionString + "/" + "_" + platform))
#pragma warning restore CS0618
				{
					AssetBundle bundle = www.assetBundle;
					Shader[] pre_shaders = bundle.LoadAllAssets<Shader>();
					foreach (Shader shader in pre_shaders)
					{
						string key = shader.name.Replace("Custom/", string.Empty);
						if (shaders.ContainsKey(key))
							shaders.Remove(key);
						shaders.Add(key, new Material(shader));
					}
					bundle.Unload(false);
					www.Dispose();
				}
			}

			Material mat;
			if (!shaders.TryGetValue( name, out mat ))
			{
				throw new Exception( "shader " + name + " not found" );
			}
			return mat;
		}



		// --- CONFIG ---------------------------------------------------------------

		// get a config node from the config system
		public static ConfigNode ParseConfig( string path )
		{
			return GameDatabase.Instance.GetConfigNode( path ) ?? new ConfigNode();
		}

		// get a set of config nodes from the config system
		public static ConfigNode[] ParseConfigs( string path )
		{
			return GameDatabase.Instance.GetConfigNodes( path );
		}

		// get a value from config
		public static T ConfigValue<T>( ConfigNode cfg, string key, T def_value )
		{
			try
			{
				return cfg.HasValue( key ) ? (T) Convert.ChangeType( cfg.GetValue( key ), typeof( T ) ) : def_value;
			}
			catch (Exception e)
			{
				Lib.Log( "error while trying to parse '" + key + "' from " + cfg.name + " (" + e.Message + ")" );
				return def_value;
			}
		}

		// get an enum from config
		public static T ConfigEnum<T>( ConfigNode cfg, string key, T def_value )
		{
			try
			{
				return cfg.HasValue( key ) ? (T) Enum.Parse( typeof( T ), cfg.GetValue( key ) ) : def_value;
			}
			catch (Exception e)
			{
				Lib.Log( "invalid enum in '" + key + "' from " + cfg.name + " (" + e.Message + ")" );
				return def_value;
			}
		}


		// --- UI -------------------------------------------------------------------

		/// <summary>Trigger a planner update</summary>
		public static void RefreshPlanner()
		{
			Planner.Planner.RefreshPlanner();
		}

		// return true if last GUILayout element was clicked
		public static bool IsClicked( int button = 0 )
		{
			return Event.current.type == EventType.MouseDown
				&& Event.current.button == button
				&& GUILayoutUtility.GetLastRect().Contains( Event.current.mousePosition );
		}

		// return true if the mouse is inside the last GUILayout element
		public static bool IsHover()
		{
			return GUILayoutUtility.GetLastRect().Contains( Event.current.mousePosition );
		}

		// render a text field with placeholder
		// - id: an unique name for the text field
		// - text: the previous text field content
		// - placeholder: the text to show if the content is empty
		// - style: GUIStyle to use for the text field
		public static string TextFieldPlaceholder( string id, string text, string placeholder, GUIStyle style )
		{
			GUI.SetNextControlName( id );
			text = GUILayout.TextField( text, style );

			if (Event.current.type == EventType.Repaint)
			{
				if (GUI.GetNameOfFocusedControl() == id)
				{
					if (text == placeholder) text = "";
				}
				else
				{
					if (text.Length == 0) text = placeholder;
				}
			}
			return text;
		}

		// used to make rmb ui status toggles look all the same
		public static string StatusToggle( string title, string status )
		{
			return Lib.BuildString( "<b>", title, "</b>: ", status );
		}


		// show a modal popup window where the user can choose among two options
		public static PopupDialog Popup( string title, string msg, params DialogGUIBase[] buttons)
		{
			return PopupDialog.SpawnPopupDialog
			(
				new Vector2( 0.5f, 0.5f ),
				new Vector2( 0.5f, 0.5f ),
				new MultiOptionDialog( title, msg, title, HighLogic.UISkin, buttons),
				false,
				HighLogic.UISkin,
				true,
				string.Empty
			);
		}

		public static string Greek() {
			string[] letters = {
				"Alpha",
				"Beta",
				"Gamma",
				"Delta",
				"Epsilon",
				"Zeta",
				"Eta",
				"Theta",
				"Iota",
				"Kappa",
				"Lambda",
				"Mu",
				"Nu",
				"Xi",
				"Omicron",
				"Pi",
				"Sigma",
				"Tau",
				"Upsilon",
				"Phi",
				"Chi",
				"Psi",
				"Omega"
			};
			System.Random rand = new System.Random();
			int index = rand.Next(letters.Length);
			return (string)letters[index];
		}

		// --- PROTO ----------------------------------------------------------------

		public static class Proto
		{
			public static bool GetBool( ProtoPartModuleSnapshot m, string name, bool def_value = false )
			{
				bool v;
				string s = m.moduleValues.GetValue( name );
				return s != null && bool.TryParse( s, out v ) ? v : def_value;
			}

			public static uint GetUInt( ProtoPartModuleSnapshot m, string name, uint def_value = 0 )
			{
				uint v;
				string s = m.moduleValues.GetValue( name );
				return s != null && uint.TryParse( s, out v ) ? v : def_value;
			}

			public static float GetFloat( ProtoPartModuleSnapshot m, string name, float def_value = 0.0f )
			{
				// note: we set NaN and infinity values to zero, to cover some weird inter-mod interactions
				float v;
				string s = m.moduleValues.GetValue( name );
				return s != null && float.TryParse( s, out v ) && !float.IsNaN( v ) && !float.IsInfinity( v ) ? v : def_value;
			}

			public static double GetDouble( ProtoPartModuleSnapshot m, string name, double def_value = 0.0 )
			{
				// note: we set NaN and infinity values to zero, to cover some weird inter-mod interactions
				double v;
				string s = m.moduleValues.GetValue( name );
				return s != null && double.TryParse( s, out v ) && !double.IsNaN( v ) && !double.IsInfinity( v ) ? v : def_value;
			}

			public static string GetString( ProtoPartModuleSnapshot m, string name, string def_value = "" )
			{
				string s = m.moduleValues.GetValue( name );
				return s ?? def_value;
			}

			// set a value in a proto module
			public static void Set<T>( ProtoPartModuleSnapshot module, string value_name, T value )
			{
				module.moduleValues.SetValue( value_name, value.ToString(), true );
			}
		}


		public static class Parse
		{
			public static bool ToBool( string s, bool def_value = false )
			{
				bool v;
				return s != null && bool.TryParse( s, out v ) ? v : def_value;
			}

			public static uint ToUInt( string s, uint def_value = 0 )
			{
				uint v;
				return s != null && uint.TryParse( s, out v ) ? v : def_value;
			}

			public static Guid ToGuid (string s)
			{
				return new Guid(s);
			}

			public static float ToFloat( string s, float def_value = 0.0f )
			{
				float v;
				return s != null && float.TryParse( s, out v ) ? v : def_value;
			}

			public static double ToDouble( string s, double def_value = 0.0 )
			{
				double v;
				return s != null && double.TryParse( s, out v ) ? v : def_value;
			}

			private static bool TryParseColor( string s, out UnityEngine.Color c )
			{
				string[] split = s.Replace( " ", String.Empty ).Split( ',' );
				if (split.Length < 3)
				{
					c = new UnityEngine.Color( 0, 0, 0 );
					return false;
				}
				if (split.Length == 4)
				{
					c = new UnityEngine.Color( ToFloat( split[0], 0f ), ToFloat( split[1], 0f ), ToFloat( split[2], 0f ), ToFloat( split[3], 1f ) );
					return true;
				}
				c = new UnityEngine.Color( ToFloat( split[0], 0f ), ToFloat( split[1], 0f ), ToFloat( split[2], 0f ) );
				return true;
			}

			public static UnityEngine.Color ToColor( string s, UnityEngine.Color def_value )
			{
				UnityEngine.Color v;
				return s != null && TryParseColor( s, out v ) ? v : def_value;
			}
		}

		/// <summary>
		/// Checks whether the location is behind the body
		/// Original code by regex from https://github.com/NathanKell/RealSolarSystem/blob/master/Source/KSCSwitcher.cs
		/// </summary>
		public static bool IsOccluded( Vector3d loc, CelestialBody body )
		{
			Vector3d camPos = ScaledSpace.ScaledToLocalSpace( PlanetariumCamera.Camera.transform.position );

			if (Vector3d.Angle( camPos - loc, body.position - loc ) > 90) { return false; }
			return true;
		}

		public static String FormatSI( double value, String unit )
		{
			string[] DistanceUnits = { "", "k", "M", "G", "T" };
			var i = (int) Clamp( Math.Floor( Math.Log10( value ) ) / 3,
				0, DistanceUnits.Length - 1 );
			value /= Math.Pow( 1000, i );
			return value.ToString( "F2" ) + DistanceUnits[i] + unit;
		}
	}


} // KERBALISM
