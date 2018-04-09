using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{


	public static class FileManager
	{
		public static void fileman(this Panel p, Vessel v)
		{
			// avoid corner-case when this is called in a lambda after scene changes
			v = FlightGlobals.FindVessel(v.id);

			// if vessel doesn't exist anymore, leave the panel empty
			if (v == null) return;

			// get info from the cache
			vessel_info vi = Cache.VesselInfo(v);

			// if not a valid vessel, leave the panel empty
			if (!vi.is_valid) return;

			// set metadata
			p.title(Lib.BuildString(Lib.Ellipsis(v.vesselName, 20), " <color=#cccccc>FILE MANAGER</color>"));
			p.width(320.0f);

			// time-out simulation
			if (p.timeout(vi)) return;

			// get vessel drive
			Drive drive = DB.Vessel(v).drive;

			// draw data section
			p.section("DATA");
			foreach (var pair in drive.files)
			{
				string filename = pair.Key;
				File file = pair.Value;
				render_file(p, filename, file, drive);
			}
			if (drive.files.Count == 0) p.content("<i>no files</i>", string.Empty);

			// draw samples section
			p.section("SAMPLES");
			foreach (var pair in drive.samples)
			{
				string filename = pair.Key;
				Sample sample = pair.Value;
				render_sample(p, filename, sample, drive);
			}
			if (drive.samples.Count == 0) p.content("<i>no samples</i>", string.Empty);
		}

		static void render_file(Panel p, string filename, File file, Drive drive)
		{
			// get experiment info
			ExperimentInfo exp = Science.experiment(filename);

			// render experiment name
			string exp_label = Lib.BuildString
			(
			  "<b>",
			  Lib.Ellipsis(exp.name, 24),
			  "</b> <size=10>",
			  Lib.Ellipsis(exp.situation, 32u - (uint)Math.Min(24, exp.name.Length)),
			  "</size>"
			);
			string exp_tooltip = Lib.BuildString
			(
			  exp.name, "\n",
			  "<color=#aaaaaa>", exp.situation, "</color>"
			);
			double exp_value = Science.value(filename, file.size);
			if (exp_value > double.Epsilon) exp_tooltip = Lib.BuildString(exp_tooltip, "\n<b>", Lib.HumanReadableScience(exp_value), "</b>");

			p.content(exp_label, Lib.HumanReadableDataSize(file.size), exp_tooltip);
			p.icon(file.send ? Icons.send_cyan : Icons.send_black, "Flag the file for transmission to <b>DSN</b>", () => { file.send = !file.send; });
			p.icon(Icons.toggle_red, "Delete the file", () => Lib.Popup
			(
			  "Warning!",
			  Lib.BuildString("Do you really want to delete ", exp.fullname, "?"),
			  new DialogGUIButton("Delete it", () => drive.files.Remove(filename)),
			  new DialogGUIButton("Keep it", () => { })
			));
		}


		static void render_sample(Panel p, string filename, Sample sample, Drive drive)
		{
			// get experiment info
			ExperimentInfo exp = Science.experiment(filename);

			// render experiment name
			string exp_label = Lib.BuildString
			(
			  "<b>",
			  Lib.Ellipsis(exp.name, 24),
			  "</b> <size=10>",
			  Lib.Ellipsis(exp.situation, 32u - (uint)Math.Min(24, exp.name.Length)),
			  "</size>"
			);
			string exp_tooltip = Lib.BuildString
			(
			  exp.name, "\n",
			  "<color=#aaaaaa>", exp.situation, "</color>"
			);
			double exp_value = Science.value(filename, sample.size);
			if (exp_value > double.Epsilon) exp_tooltip = Lib.BuildString(exp_tooltip, "\n<b>", Lib.HumanReadableScience(exp_value), "</b>");

			p.content(exp_label, Lib.HumanReadableDataSize(sample.size), exp_tooltip);
			p.icon(sample.analyze ? Icons.lab_cyan : Icons.lab_black, "Flag the file for analysis in a <b>laboratory</b>", () => { sample.analyze = !sample.analyze; });
			p.icon(Icons.toggle_red, "Dump the sample", () => Lib.Popup
			(
			  "Warning!",
			   Lib.BuildString("Do you really want to dump ", exp.fullname, "?"),
			   new DialogGUIButton("Dump it", () => drive.samples.Remove(filename)),
			   new DialogGUIButton("Keep it", () => { })
			));
		}
	}


} // KERBALISM