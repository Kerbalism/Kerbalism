using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{


	public static class FileManager
	{

		/// <summary>
		/// If short_strings parameter is true then the strings used for display of the data will be shorter when inflight.
		/// </summary>
		public static void Fileman(this Panel p, Vessel v, bool short_strings = false)
		{
			// avoid corner-case when this is called in a lambda after scene changes
			v = FlightGlobals.FindVessel(v.id);

			// if vessel doesn't exist anymore, leave the panel empty
			if (v == null) return;

			// get info from the cache
			Vessel_info vi = Cache.VesselInfo(v);

			// if not a valid vessel, leave the panel empty
			if (!vi.is_valid) return;

			// set metadata
			p.Title(Lib.BuildString(Lib.Ellipsis(v.vesselName, Styles.ScaleStringLength(40)), " <color=#cccccc>FILE MANAGER</color>"));
			p.Width(Styles.ScaleWidthFloat(465.0f));
			p.paneltype = Panel.PanelType.data;

 			// time-out simulation
			if (p.Timeout(vi)) return;

			var drives = DB.Vessel(v).drives;
			foreach (var idDrivePair in drives)
			{
				var drive = idDrivePair.Value;

				var skipDrive = drive.files.Count == 0 && drives.Count > 1;
				var driveHasCapacity = drive.dataCapacity > double.Epsilon || drive.dataCapacity < 0;

				if(!skipDrive && driveHasCapacity)
				{
					// draw data section
					var title = "DATA " + drive.name;
					title += ", " + Lib.HumanReadableDataSize(drive.FilesSize());
					if (drive.dataCapacity > 0)
						title += Lib.BuildString(" (", Lib.HumanReadablePerc(drive.FilesSize() / drive.dataCapacity), ")");
					p.AddSection(title);

					foreach (var pair in drive.files)
					{
						string filename = pair.Key;
						File file = pair.Value;
						Render_file(p, filename, file, drive, short_strings && Lib.IsFlight(), Cache.VesselInfo(v).connection.rate);
					}
					if (drive.files.Count == 0) p.AddContent("<i>no files</i>", string.Empty);
				}

				skipDrive = drive.samples.Count == 0 && drives.Count > 1;
				driveHasCapacity = drive.sampleCapacity != 0;

				if (!skipDrive && driveHasCapacity)
				{
					double mass = 0;
					foreach (var sample in drive.samples.Values) mass += sample.mass;

					var title = "SAMPLES " + drive.name;
					title += ", " + Lib.HumanReadableSampleSize(mass);
					if (drive.sampleCapacity > 0)
						title += Lib.BuildString(" (", Lib.HumanReadablePerc((double)(drive.SamplesSize()) / (double)(drive.sampleCapacity)), ") ");
					p.AddSection(title);

					foreach (var pair in drive.samples)
					{
						string filename = pair.Key;
						Sample sample = pair.Value;
						Render_sample(p, filename, sample, drive, short_strings && Lib.IsFlight());
					}
					if (drive.samples.Count == 0) p.AddContent("<i>no samples</i>", string.Empty);
				}
			}
		}

		static void Render_file(Panel p, string filename, File file, Drive drive, bool short_strings, double rate)
		{
			// get experiment info
			ExperimentInfo exp = Science.Experiment(filename);

			// render experiment name
			string exp_label = Lib.BuildString
			(
			  "<b>",
			  Lib.Ellipsis(exp.name, Styles.ScaleStringLength(short_strings ? 24 : 38)),
			  "</b> <size=", Styles.ScaleInteger(10).ToString(), ">",
			  Lib.Ellipsis(ExperimentInfo.Situation(filename), Styles.ScaleStringLength((short_strings ? 32 : 62) - Lib.Ellipsis(exp.name, Styles.ScaleStringLength(short_strings ? 24 : 38)).Length)),
			  "</size>"
			);
			string exp_tooltip = Lib.BuildString
			(
			  exp.name, "\n",
			  "<color=#aaaaaa>", ExperimentInfo.Situation(filename), "</color>"
			);
			double exp_value = Science.Value(filename, file.size);
			if (exp_value > double.Epsilon) exp_tooltip = Lib.BuildString(exp_tooltip, "\n<b>", Lib.HumanReadableScience(exp_value), "</b>");
			if (rate > 0) exp_tooltip = Lib.BuildString(exp_tooltip, "\n<i>" + Lib.HumanReadableDuration(file.size / rate) + "</i>");
			p.AddContent(exp_label, Lib.HumanReadableDataSize(file.size), exp_tooltip);

			bool send = drive.GetFileSend(filename);
			p.AddIcon(send ? Icons.send_cyan : Icons.send_black, "Flag the file for transmission to <b>DSN</b>", () => { drive.Send(filename, !send); });
			p.AddIcon(Icons.toggle_red, "Delete the file", () => Lib.Popup
			(
			  "Warning!",
			  Lib.BuildString("Do you really want to delete ", exp.FullName(filename), "?"),
			  new DialogGUIButton("Delete it", () => drive.files.Remove(filename)),
			  new DialogGUIButton("Keep it", () => { })
			));
		}

		static void Render_sample(Panel p, string filename, Sample sample, Drive drive, bool short_strings)
		{
			// get experiment info
			ExperimentInfo exp = Science.Experiment(filename);

			// render experiment name
			string exp_label = Lib.BuildString
			(
			  "<b>",
			  Lib.Ellipsis(exp.name, Styles.ScaleStringLength(short_strings ? 24 : 38)),
			  "</b> <size=", Styles.ScaleInteger(10).ToString(), ">",
			  Lib.Ellipsis(ExperimentInfo.Situation(filename), Styles.ScaleStringLength((short_strings ? 32 : 62) - Lib.Ellipsis(exp.name, Styles.ScaleStringLength(short_strings ? 24 : 38)).Length)),
			  "</size>"
			);
			string exp_tooltip = Lib.BuildString
			(
			  exp.name, "\n",
			  "<color=#aaaaaa>", ExperimentInfo.Situation(filename), "</color>"
			);
			double exp_value = Science.Value(filename, sample.size);
			if (exp_value > double.Epsilon) exp_tooltip = Lib.BuildString(exp_tooltip, "\n<b>", Lib.HumanReadableScience(exp_value), "</b>");
			if (sample.mass > Double.Epsilon) exp_tooltip = Lib.BuildString(exp_tooltip, "\n<b>", Lib.HumanReadableMass(sample.mass), "</b>");

			p.AddContent(exp_label, Lib.HumanReadableSampleSize(sample.size), exp_tooltip);
			p.AddIcon(sample.analyze ? Icons.lab_cyan : Icons.lab_black, "Flag the file for analysis in a <b>laboratory</b>", () => { sample.analyze = !sample.analyze; });
			p.AddIcon(Icons.toggle_red, "Dump the sample", () => Lib.Popup
			(
			  "Warning!",
			   Lib.BuildString("Do you really want to dump ", exp.FullName(filename), "?"),
			   new DialogGUIButton("Dump it", () => drive.samples.Remove(filename)),
			   new DialogGUIButton("Keep it", () => { })
			));
		}
	}


} // KERBALISM
