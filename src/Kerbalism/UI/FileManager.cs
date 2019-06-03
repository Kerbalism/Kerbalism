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
			if (!Lib.IsControlUnit(v) && p.Timeout(vi)) return;

			var drives = Drive.GetDriveParts(v);

			int filesCount = 0;
			double usedDataCapacity = 0;
			double totalDataCapacity = 0;

			int samplesCount = 0;
			int usedSlots = 0;
			int totalSlots = 0;
			double totalMass = 0;
			bool unlimitedData = false;
			bool unlimitedSamples = false;

			foreach (var idDrivePair in drives)
			{
				var drive = idDrivePair.Value;

				if(!drive.is_private)
				{
					usedDataCapacity += drive.FilesSize();
					totalDataCapacity += drive.dataCapacity;

					unlimitedData |= drive.dataCapacity < 0;
					unlimitedSamples |= drive.sampleCapacity < 0;

					usedSlots += drive.SamplesSize();
					totalSlots += drive.sampleCapacity;
				}

				filesCount += drive.files.Count;
				samplesCount += drive.samples.Count;
				foreach (var sample in drive.samples.Values) totalMass += sample.mass;
			}

			if(filesCount > 0 || totalDataCapacity > 0)
			{
				var title = "DATA " + Lib.HumanReadableDataSize(usedDataCapacity);
				if(!unlimitedData) title += Lib.BuildString(" (", Lib.HumanReadablePerc((totalDataCapacity - usedDataCapacity) / totalDataCapacity), " available)");
				p.AddSection(title);

				foreach (var idDrivePair in drives)
				{
					uint partId = idDrivePair.Key;
					var drive = idDrivePair.Value;
					foreach (var pair in drive.files)
					{
						string filename = pair.Key;
						File file = pair.Value;
						Render_file(p, partId, filename, file, drive, short_strings && Lib.IsFlight(), v);
					}
				}

				if(filesCount == 0) p.AddContent("<i>no files</i>", string.Empty);
			}

			if(samplesCount > 0 || totalSlots > 0)
			{
				var title = "SAMPLES " + Lib.HumanReadableMass(totalMass) + " " + Lib.HumanReadableSampleSize(usedSlots);
				if (totalSlots > 0 && !unlimitedSamples) title += ", " + Lib.HumanReadableSampleSize(totalSlots) + " available";
				p.AddSection(title);

				foreach (var idDrivePair in drives)
				{
					uint partId = idDrivePair.Key;
					var drive = idDrivePair.Value;
					foreach (var pair in drive.samples)
					{
						string samplename = pair.Key;
						Sample sample = pair.Value;
						Render_sample(p, partId, samplename, sample, drive, short_strings && Lib.IsFlight());
					}
				}

				if (samplesCount == 0) p.AddContent("<i>no samples</i>", string.Empty);
			}
		}

		static void Render_file(Panel p, uint partId, string filename, File file, Drive drive, bool short_strings, Vessel v)
		{
			// get experiment info
			ExperimentInfo exp = Science.Experiment(filename);
			double rate = Cache.VesselInfo(v).connection.rate;

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
			if (exp_value >= 0.1) exp_tooltip = Lib.BuildString(exp_tooltip, "\n<b>", Lib.HumanReadableScience(exp_value), "</b>");
			if (rate > 0) exp_tooltip = Lib.BuildString(exp_tooltip, "\n<i>" + Lib.HumanReadableDuration(file.size / rate) + "</i>");
			p.AddContent(exp_label, Lib.HumanReadableDataSize(file.size), exp_tooltip, (Action)null, () => Highlighter.Set(partId, Color.cyan));

			bool send = drive.GetFileSend(filename);
			p.AddIcon(send ? Icons.send_cyan : Icons.send_black, "Flag the file for transmission to <b>DSN</b>", () => { drive.Send(filename, !send); });
			p.AddIcon(Icons.toggle_red, "Delete the file", () =>
				{
					Lib.Popup("Warning!",
						Lib.BuildString("Do you really want to delete ", exp.FullName(filename), "?"),
				        new DialogGUIButton("Delete it", () => drive.Delete_file(filename, double.MaxValue, v.protoVessel)),
						new DialogGUIButton("Keep it", () => { }));
				}
			);
		}

		static void Render_sample(Panel p, uint partId, string filename, Sample sample, Drive drive, bool short_strings)
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
			if (exp_value >= 0.1) exp_tooltip = Lib.BuildString(exp_tooltip, "\n<b>", Lib.HumanReadableScience(exp_value), "</b>");
			if (sample.mass > Double.Epsilon) exp_tooltip = Lib.BuildString(exp_tooltip, "\n<b>", Lib.HumanReadableMass(sample.mass), "</b>");

			p.AddContent(exp_label, Lib.HumanReadableSampleSize(sample.size), exp_tooltip, (Action)null, () => Highlighter.Set(partId, Color.cyan));
			p.AddIcon(sample.analyze ? Icons.lab_cyan : Icons.lab_black, "Flag the file for analysis in a <b>laboratory</b>", () => { sample.analyze = !sample.analyze; });
			p.AddIcon(Icons.toggle_red, "Dump the sample", () =>
				{
					Lib.Popup("Warning!",
						Lib.BuildString("Do you really want to dump ", exp.FullName(filename), "?"),
						new DialogGUIButton("Dump it", () => drive.samples.Remove(filename)),
							  new DialogGUIButton("Keep it", () => { }));
				}
			);
		}
	}


} // KERBALISM
