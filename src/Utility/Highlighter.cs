using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{


	public static class Highlighter
	{
		public static void Init()
		{
			// initialize data
			parts = new Dictionary<uint, Color>();
			prev_parts = new Dictionary<uint, Color>();
		}


		public static void Update()
		{
			// for each part from previous frame
			foreach (var prev_p in prev_parts)
			{
				// get the part
				Part prev_part = FlightGlobals.FindPartByID(prev_p.Key);

				// if it still exist
				if (prev_part != null)
				{
					// note: when solar panels break (the stock mechanic), this throw exceptions inside KSP
					try
					{
						// reset highlight color
						prev_part.SetHighlightDefault();

						// the new color change module overwrite our highlights and was disabled, re-enable it
						prev_part.FindModulesImplementing<ModuleColorChanger>().ForEach(k => k.enabled = true);
					}
					catch { }
				}
			}

			// for each part in this farme
			foreach (var p in parts)
			{
				// get the part
				Part part = FlightGlobals.FindPartByID(p.Key);

				// if it still exist
				if (part != null)
				{
					// note: when solar panels break (the stock mechanic), this throw exceptions inside KSP
					try
					{
						// set highlight color
						part.SetHighlightDefault();
						part.SetHighlightType(Part.HighlightType.AlwaysOn);
						part.SetHighlightColor((Color)p.Value);
						part.SetHighlight(true, false);

						// the new color change module seem to overwrite our highlights, disable it
						part.FindModulesImplementing<ModuleColorChanger>().ForEach(k => k.enabled = false);
					}
					catch { }
				}
			}

			// clear previous parts, and remember current parts as previous
			Lib.Swap(ref prev_parts, ref parts);
			parts.Clear();
		}


		public static void Set(uint flight_id, Color clr)
		{
			if (parts.ContainsKey(flight_id))
			{
				parts.Remove(flight_id);
			}
			parts.Add(flight_id, clr);
		}


		// set of parts to highlight
		static Dictionary<uint, Color> parts;

		// set of parts highlighted in previous frame
		static Dictionary<uint, Color> prev_parts;
	}


} // KERBALISM

