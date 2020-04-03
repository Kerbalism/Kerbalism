using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


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
			v.TryGetVesselData(out VesselData vd);

			// if not a valid vessel, leave the panel empty
			if (!vd.IsSimulated) return;

			// set metadata
			p.Title(Lib.BuildString(Lib.Ellipsis(v.vesselName, Styles.ScaleStringLength(40)), " ", Lib.Color(Local.FILEMANAGER_title, Lib.Kolor.LightGrey)));//"FILE MANAGER"
			p.Width(Styles.ScaleWidthFloat(465.0f));
			p.paneltype = Panel.PanelType.data;

 			// time-out simulation
			if (!Lib.IsControlUnit(v) && p.Timeout(vd)) return;

			int filesCount = 0;
			double usedDataCapacity = 0;
			double totalDataCapacity = 0;

			int samplesCount = 0;
			int usedSlots = 0;
			int totalSlots = 0;
			double totalMass = 0;
			bool unlimitedData = false;
			bool unlimitedSamples = false;

			foreach (DriveData drive in vd.Parts.AllModulesOfType<DriveData>())
			{
				if (!drive.isPrivate)
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
				var title = Local.FILEMANAGER_DataCapacity + " " + Lib.HumanReadableDataSize(usedDataCapacity);//"DATA " 
				if (!unlimitedData) title += Local.FILEMANAGER_DataAvailable.Format(Lib.HumanReadablePerc((totalDataCapacity - usedDataCapacity) / totalDataCapacity));//Lib.BuildString(" (", Lib.HumanReadablePerc((totalDataCapacity - usedDataCapacity) / totalDataCapacity), " available)");
				p.AddSection(title);

				foreach (var drive in vd.Parts.AllModulesOfType<DriveData>())
				{
					foreach (File file in drive.files.Values)
					{
						Render_file(vd, p, drive.partData.flightId, file, drive, short_strings && Lib.IsFlight, v);
					}
				}

				if(filesCount == 0) p.AddContent("<i>"+Local.FILEMANAGER_nofiles +"</i>", string.Empty);//no files
			}

			if(samplesCount > 0 || totalSlots > 0)
			{
				var title = Local.FILEMANAGER_SAMPLESMass.Format(Lib.HumanReadableMass(totalMass)) + " " + Lib.HumanReadableSampleSize(usedSlots);//"SAMPLES " + 
				if (totalSlots > 0 && !unlimitedSamples) title += ", " + Lib.HumanReadableSampleSize(totalSlots) + " "+ Local.FILEMANAGER_SAMPLESAvailable;//available
				p.AddSection(title);

				foreach (var drive in vd.Parts.AllModulesOfType<DriveData>())
				{
					foreach (Sample sample in drive.samples.Values)
					{
						Render_sample(p, drive.partData.flightId, sample, drive, short_strings && Lib.IsFlight);
					}
				}

				if (samplesCount == 0) p.AddContent("<i>"+Local.FILEMANAGER_nosamples+"</i>", string.Empty);//no samples
			}
		}

		static void Render_file(VesselData vd, Panel p, uint partId, File file, DriveData drive, bool short_strings, Vessel v)
		{
			// render experiment name
			string exp_label = Lib.BuildString
			(
			  "<b>",
			  Lib.Ellipsis(file.subjectData.ExperimentTitle, Styles.ScaleStringLength(short_strings ? 24 : 38)),
			  "</b> <size=", Styles.ScaleInteger(10).ToString(), ">",
			  Lib.Ellipsis(file.subjectData.SituationTitle, Styles.ScaleStringLength((short_strings ? 32 : 62) - Lib.Ellipsis(file.subjectData.ExperimentTitle, Styles.ScaleStringLength(short_strings ? 24 : 38)).Length)),
			  "</size>"
			);
			string exp_tooltip = Lib.BuildString
			(
			  file.subjectData.ExperimentTitle, "\n",
			  Lib.Color(file.subjectData.SituationTitle, Lib.Kolor.LightGrey)
			);

			double exp_value = file.size * file.subjectData.SciencePerMB;
			if (file.subjectData.ScienceRemainingToRetrieve > 0f && file.size > 0.0)
				exp_tooltip = Lib.BuildString(exp_tooltip, "\n<b>", Lib.HumanReadableScience(exp_value, false), "</b>");
			if (file.transmitRate > 0.0)
			{
				if (file.size > 0.0)
					exp_tooltip = Lib.Color(Lib.BuildString(exp_tooltip, "\n", Local.FILEMANAGER_TransmittingRate.Format(Lib.HumanReadableDataRate(file.transmitRate)), " : <i>", Lib.HumanReadableCountdown(file.size / file.transmitRate), "</i>"), Lib.Kolor.Cyan);//Transmitting at <<1>>
				else
					exp_tooltip = Lib.Color(Lib.BuildString(exp_tooltip, "\n", Local.FILEMANAGER_TransmittingRate.Format(Lib.HumanReadableDataRate(file.transmitRate))), Lib.Kolor.Cyan);//Transmitting at <<1>>
			}
			else if (vd.Connection.rate > 0.0)
				exp_tooltip = Lib.BuildString(exp_tooltip, "\n", Local.FILEMANAGER_Transmitduration, "<i>", Lib.HumanReadableDuration(file.size / vd.Connection.rate), "</i>");//Transmit duration : 
			if (!string.IsNullOrEmpty(file.resultText))
				exp_tooltip = Lib.BuildString(exp_tooltip, "\n", Lib.WordWrapAtLength(file.resultText, 50));

			string size;
			if (file.transmitRate > 0.0 )
			{
				if (file.size == 0.0)
					size = Lib.Color(Lib.BuildString("↑ ", Lib.HumanReadableDataRate(file.transmitRate)), Lib.Kolor.Cyan);
				else
					size = Lib.Color(Lib.BuildString("↑ ", Lib.HumanReadableDataSize(file.size)), Lib.Kolor.Cyan);
			}
			else
			{
				size = Lib.HumanReadableDataSize(file.size);
			}

			p.AddContent(exp_label, size, exp_tooltip, (Action)null, () => Highlighter.Set(partId, Color.cyan));

			bool send = drive.GetFileSend(file.subjectData.Id);
			p.AddRightIcon(send ? Textures.send_cyan : Textures.send_black, Local.FILEMANAGER_send, () => { drive.Send(file.subjectData.Id, !send); });//"Flag the file for transmission to <b>DSN</b>"
			p.AddRightIcon(Textures.toggle_red, Local.FILEMANAGER_Delete, () =>//"Delete the file"
				{
					Lib.Popup(Local.FILEMANAGER_Warning_title,//"Warning!"
						Local.FILEMANAGER_DeleteConfirm.Format(file.subjectData.FullTitle),//Lib.BuildString(, "?"),//"Do you really want to delete <<1>>", 
				        new DialogGUIButton(Local.FILEMANAGER_DeleteConfirm_button1, () => drive.DeleteFile(file.subjectData)),//"Delete it"
						new DialogGUIButton(Local.FILEMANAGER_DeleteConfirm_button2, () => { }));//"Keep it"
				}
			);
		}

		static void Render_sample(Panel p, uint partId, Sample sample, DriveData drive, bool short_strings)
		{
			// render experiment name
			string exp_label = Lib.BuildString
			(
			  "<b>",
			  Lib.Ellipsis(sample.subjectData.ExperimentTitle, Styles.ScaleStringLength(short_strings ? 24 : 38)),
			  "</b> <size=", Styles.ScaleInteger(10).ToString(), ">",
			  Lib.Ellipsis(sample.subjectData.SituationTitle, Styles.ScaleStringLength((short_strings ? 32 : 62) - Lib.Ellipsis(sample.subjectData.ExperimentTitle, Styles.ScaleStringLength(short_strings ? 24 : 38)).Length)),
			  "</size>"
			);
			string exp_tooltip = Lib.BuildString
			(
			  sample.subjectData.ExperimentTitle, "\n",
			  Lib.Color(sample.subjectData.SituationTitle, Lib.Kolor.LightGrey)
			);

			double exp_value = sample.size * sample.subjectData.SciencePerMB;
			if (exp_value >= 0.1) exp_tooltip = Lib.BuildString(exp_tooltip, "\n<b>", Lib.HumanReadableScience(exp_value, false), "</b>");
			if (sample.mass > Double.Epsilon) exp_tooltip = Lib.BuildString(exp_tooltip, "\n<b>", Lib.HumanReadableMass(sample.mass), "</b>");
			if (!string.IsNullOrEmpty(sample.resultText)) exp_tooltip = Lib.BuildString(exp_tooltip, "\n", Lib.WordWrapAtLength(sample.resultText, 50));

			p.AddContent(exp_label, Lib.HumanReadableSampleSize(sample.size), exp_tooltip, (Action)null, () => Highlighter.Set(partId, Color.cyan));
			p.AddRightIcon(sample.analyze ? Textures.lab_cyan : Textures.lab_black, Local.FILEMANAGER_analysis, () => { sample.analyze = !sample.analyze; });//"Flag the file for analysis in a <b>laboratory</b>"
			p.AddRightIcon(Textures.toggle_red, Local.FILEMANAGER_Dumpsample, () =>//"Dump the sample"
				{
					Lib.Popup(Local.FILEMANAGER_Warning_title,//"Warning!"
						Local.FILEMANAGER_DumpConfirm.Format(sample.subjectData.FullTitle),//"Do you really want to dump <<1>>?", 
						new DialogGUIButton(Local.FILEMANAGER_DumpConfirm_button1, () => drive.DeleteSample(sample.subjectData)),//"Dump it"
							  new DialogGUIButton(Local.FILEMANAGER_DumpConfirm_button2, () => { }));//"Keep it"
				}
			);
		}
	}


} // KERBALISM
