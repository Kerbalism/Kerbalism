using KERBALISM.KsmGui;
using System.Collections.Generic;
using UnityEngine;

namespace KERBALISM
{
	/// <summary>
	/// Science popup for KerbalismScansat — mirrors ExperimentPopup enough for #609
	/// (status, coverage, science value, start/stop, archive subject list).
	/// </summary>
	public class ScanExperimentPopup
	{
		private readonly Vessel vessel;
		private readonly KerbalismScansat moduleOrPrefab;
		private readonly ProtoPartModuleSnapshot protoModule;
		private readonly ProtoPartSnapshot protoPart;
		private readonly bool isProto;
		private readonly VesselData vd;
		private readonly long popupId;

		private ExperimentInfo expInfo;
		private SubjectData subjectData;
		private bool isScanning;
		private string issue;
		private double bodyCoverage;
		private double fileSizeOnVessel;
		private bool canInteract;

		private readonly System.Text.StringBuilder sb = new System.Text.StringBuilder();

		private KsmGuiWindow window;
		private KsmGuiIconButton rndVisibilityButton;
		private KsmGuiIconButton expInfoVisibilityButton;
		private KsmGuiVerticalLayout leftPanel;
		private KsmGuiTextBox expInfoBox;
		private KsmGuiTextBox statusBox;
		private KsmGuiButton startStopButton;
		private KsmGuiHeader expInfoHeader;
		private KsmGuiHeader rndArchiveHeader;
		private ExperimentSubjectList rndArchiveView;

		private static readonly Dictionary<long, KsmGuiWindow> activePopups = new Dictionary<long, KsmGuiWindow>();

		public ScanExperimentPopup(Vessel v, KerbalismScansat moduleOrPrefab, uint partId, string partName,
			ProtoPartModuleSnapshot protoModule = null)
		{
			this.vessel = v;
			this.moduleOrPrefab = moduleOrPrefab;
			this.isProto = protoModule != null;
			this.protoModule = protoModule;
			this.protoPart = null;

			if (isProto && v.protoVessel != null)
			{
				List<ProtoPartSnapshot> parts = v.protoVessel.protoPartSnapshots;
				for (int i = 0; i < parts.Count; i++)
				{
					if (parts[i].flightID == partId)
					{
						this.protoPart = parts[i];
						break;
					}
				}
			}

			int moduleIndex = isProto
				? this.protoPart?.modules.IndexOf(protoModule) ?? -1
				: moduleOrPrefab.part?.Modules.IndexOf(moduleOrPrefab) ?? -1;
			popupId = ((long)partId << 32) | (uint)(moduleIndex + 1);

			if (activePopups.TryGetValue(popupId, out KsmGuiWindow existingWindow))
			{
				existingWindow.Close();
				return;
			}

			vd = v.KerbalismData();
			expInfo = ScienceDB.GetExperimentInfo(moduleOrPrefab.experimentType);
			GetData();

			window = new KsmGuiWindow(KsmGuiWindow.LayoutGroupType.Vertical, true, KsmGuiStyle.defaultWindowOpacity, true, 0, TextAnchor.UpperLeft, 5f);
			activePopups.Add(popupId, window);
			window.OnClose = () => activePopups.Remove(popupId);
			window.SetLayoutElement(false, false, -1, -1, -1, 150);
			window.SetUpdateAction(GetData);

			string title = expInfo != null ? expInfo.Title : moduleOrPrefab.experimentType;
			KsmGuiHeader topHeader = new KsmGuiHeader(window, title, default, 120);
			topHeader.TextObject.SetTooltipText(Lib.BuildString(
				Local.SCIENCEARCHIVE_onvessel, " ", Lib.Bold(v.vesselName), "\n",
				Local.SCIENCEARCHIVE_onpart, " ", Lib.Bold(partName)));

			rndVisibilityButton = new KsmGuiIconButton(topHeader, Textures.KsmGuiTexHeaderRnD, ToggleArchivePanel, Local.SCIENCEARCHIVE_showarchive);
			rndVisibilityButton.MoveAsFirstChild();
			expInfoVisibilityButton = new KsmGuiIconButton(topHeader, Textures.KsmGuiTexHeaderInfo, ToggleExpInfo, Local.SCIENCEARCHIVE_showexperimentinfo);
			expInfoVisibilityButton.MoveAsFirstChild();
			new KsmGuiIconButton(topHeader, Textures.KsmGuiTexHeaderClose, () => window.Close(), Local.SCIENCEARCHIVE_closebutton);

			KsmGuiHorizontalLayout panels = new KsmGuiHorizontalLayout(window, 5, 0, 0, 0, 0);

			leftPanel = new KsmGuiVerticalLayout(panels, 5);
			leftPanel.SetLayoutElement(false, true, -1, -1, 160);
			leftPanel.Enabled = false;

			expInfoHeader = new KsmGuiHeader(leftPanel, Local.SCIENCEARCHIVE_EXPERIMENTINFO);
			expInfoBox = new KsmGuiTextBox(leftPanel, BuildExpInfoText());
			expInfoBox.SetLayoutElement(false, true, 160);

			KsmGuiVerticalLayout rightPanel = new KsmGuiVerticalLayout(panels, 5);
			rightPanel.SetLayoutElement(false, true, -1, -1, 230);

			new KsmGuiHeader(rightPanel, Local.SCIENCEARCHIVE_STATUS);
			statusBox = new KsmGuiTextBox(rightPanel, "_");
			statusBox.TextObject.TextComponent.enableWordWrapping = false;
			statusBox.TextObject.TextComponent.overflowMode = TMPro.TextOverflowModes.Truncate;
			statusBox.SetLayoutElement(true, true, 230);
			statusBox.SetUpdateAction(StatusUpdate);

			KsmGuiHorizontalLayout buttons = new KsmGuiHorizontalLayout(rightPanel, 5);
			startStopButton = new KsmGuiButton(buttons, "_", Toggle);
			startStopButton.SetUpdateAction(UpdateStartStopButton);

			window.RebuildLayout();
		}

		private string BuildExpInfoText()
		{
			var specs = new Specifics();
			if (expInfo == null)
			{
				specs.Add(Local.ExperimentInfo_Unknown);
				return specs.Info();
			}

			specs.Add(Local.Module_Experiment_Specifics_info1, Lib.HumanReadableDataSize(expInfo.DataSize));
			specs.Add(Local.Module_Experiment_Specifics_info9, Lib.HumanOrSIRate(moduleOrPrefab.ec_rate, Lib.ECResID));

			List<string> situations = expInfo.AvailableSituations();
			if (situations.Count > 0)
			{
				specs.Add(string.Empty);
				specs.Add(Lib.Color(Local.Module_Experiment_Specifics_Situations, Lib.Kolor.Cyan, true));
				foreach (string s in situations)
					specs.Add(Lib.BuildString("• <b>", s, "</b>"));
			}

			return specs.Info();
		}

		private void GetData()
		{
			canInteract = vd.Connection.linked || vd.CrewCount > 0;
			expInfo = ScienceDB.GetExperimentInfo(moduleOrPrefab.experimentType);

			if (isProto)
			{
				isScanning = false;
				issue = Lib.Proto.GetBool(protoModule, "power_disabled")
					? Local.Module_Experiment_issue4
					: string.Empty;
				ProtoPartModuleSnapshot scanner = null;
				if (protoPart != null)
				{
					scanner = GetProtoScanner();
					isScanning = scanner != null && Lib.Proto.GetBool(scanner, "scanning");
				}

				int sensorType = SCANsat.ScienceSensorType(moduleOrPrefab.experimentType);
				if (sensorType == 0)
					sensorType = scanner != null
						? (int)Lib.Proto.GetUInt(scanner, "sensorType")
						: (int)Lib.Proto.GetUInt(protoModule, "sensorType");
				if (sensorType == 0 && moduleOrPrefab.sensorType != 0)
					sensorType = moduleOrPrefab.sensorType;

				bodyCoverage = 0.0;
				if (vessel.mainBody != null && sensorType != 0)
					bodyCoverage = SCANsat.Coverage(sensorType, vessel.mainBody);
			}
			else
			{
				isScanning = moduleOrPrefab.IsScanning;
				issue = moduleOrPrefab.Issue ?? string.Empty;
				bodyCoverage = moduleOrPrefab.BodyCoveragePercent;
				if (moduleOrPrefab.CurrentSubject != null)
					subjectData = moduleOrPrefab.CurrentSubject;
			}

			if (expInfo != null && vessel.mainBody != null)
			{
				subjectData = ScienceDB.GetSubjectData(
					expInfo,
					new Situation(vessel.mainBody.flightGlobalsIndex, ScienceSituation.InSpaceHigh));
			}

			fileSizeOnVessel = 0.0;
			if (subjectData != null)
			{
				if (vd.TransmitBufferDrive != null
					&& vd.TransmitBufferDrive.files.TryGetValue(subjectData, out File bufferedFile))
				{
					fileSizeOnVessel += bufferedFile.size;
				}

				foreach (Drive drive in Drive.GetDrives(vd, true))
				{
					if (drive.files.TryGetValue(subjectData, out File file))
						fileSizeOnVessel += file.size;
				}
			}
		}

		private void StatusUpdate()
		{
			sb.Length = 0;

			sb.Append(Local.SCIENCEARCHIVE_status);
			sb.Append(" :<pos=20em>");
			if (!string.IsNullOrEmpty(issue))
				sb.Append(Lib.Bold(Lib.Color(issue, Lib.Kolor.Orange)));
			else
				sb.Append(Lib.Bold(Lib.Color(isScanning, Local.Generic_ENABLED, Lib.Kolor.Green, Local.Generic_DISABLED, Lib.Kolor.Yellow)));

			sb.Append("\n");
			sb.Append(Local.SCIENCEARCHIVE_situation);
			sb.Append(" :<pos=20em>");
			if (subjectData != null)
				sb.Append(Lib.Color(subjectData.FullTitle, Lib.Kolor.Yellow, true));
			else
				sb.Append(Lib.Color(Local.SCIENCEARCHIVE_invalidsituation, Lib.Kolor.Yellow, true));

			sb.Append("\n");
			sb.Append(Local.SCIENCEARCHIVE_bodycoverage);
			sb.Append(" :<pos=20em>");
			sb.Append(Lib.Color(bodyCoverage.ToString("F1") + " %", Lib.Kolor.Yellow, true));

			sb.Append("\n");
			sb.Append(Local.SCIENCEARCHIVE_stored);
			sb.Append(" :<pos=20em>");
			sb.Append(Lib.Color(Lib.HumanReadableDataSize(fileSizeOnVessel), Lib.Kolor.Yellow, true));

			if (subjectData == null)
			{
				sb.Append("\n");
				sb.Append(Local.SCIENCEARCHIVE_value);
				sb.Append(" :<pos=20em>");
				sb.Append(Lib.Color(Local.SCIENCEARCHIVE_invalidsituation, Lib.Kolor.Yellow, true));
			}
			else
			{
				sb.Append("\n");
				sb.Append(Local.SCIENCEARCHIVE_retrieved);
				sb.Append(" :<pos=20em>");
				if (subjectData.TimesCompleted > 0)
					sb.Append(Lib.Color(subjectData.TimesCompleted.ToString(), Lib.Kolor.Yellow));
				else
					sb.Append(Lib.Color(Local.SCIENCEARCHIVE_never, Lib.Kolor.Yellow));

				if (subjectData.PercentRetrieved > 0.0)
				{
					sb.Append(" (");
					sb.Append(Lib.Color(subjectData.PercentRetrieved.ToString("P0"), Lib.Kolor.Yellow, true));
					sb.Append(")");
				}

				sb.Append("\n");
				sb.Append(Local.SCIENCEARCHIVE_collected);
				sb.Append(" :<pos=20em>");
				sb.Append(Lib.Color(subjectData.ScienceRetrievedInKSC.ToString("F1"), Lib.Kolor.Science, true));
				sb.Append(Lib.InlineSpriteScience);
				sb.Append(" ");
				sb.Append(Local.SCIENCEARCHIVE_inRnD);
				if (subjectData.ScienceCollectedInFlightValue > 0.05)
				{
					sb.Append(" (");
					sb.Append(Lib.Color(Lib.BuildString("+", subjectData.ScienceCollectedInFlightValue.ToString("F1")), Lib.Kolor.Science, true));
					sb.Append(Lib.InlineSpriteScience);
					sb.Append(" ");
					sb.Append(Local.SCIENCEARCHIVE_inflight);
				}

				sb.Append("\n");
				sb.Append(Local.SCIENCEARCHIVE_value);
				sb.Append(" :<pos=20em>");
				sb.Append(Lib.Color(Experiment.ScienceValue(subjectData), Lib.Kolor.Science, true));
			}

			statusBox.Text = sb.ToString();
		}

		private void UpdateStartStopButton()
		{
			startStopButton.Text = isScanning ? Local.SCIENCEARCHIVE_stop : Local.SCIENCEARCHIVE_start;
			startStopButton.Interactable = canInteract;
		}

		private void Toggle()
		{
			if (isProto)
			{
				if (protoPart == null)
					return;

				ProtoPartModuleSnapshot scanner = GetProtoScanner();
				if (scanner == null)
					return;

				Lib.Proto.Set(protoModule, "power_disabled", false);
				vd.scansat_id.Remove(protoPart.flightID);
				if (isScanning)
					SCANsat.StopScanner(vessel, scanner, moduleOrPrefab.part);
				else
					SCANsat.ResumeScanner(vessel, scanner, moduleOrPrefab.part);
			}
			else
			{
				if (moduleOrPrefab.IsScanning)
					moduleOrPrefab.StopScan();
				else
					moduleOrPrefab.StartScan();
			}
		}

		private ProtoPartModuleSnapshot GetProtoScanner()
		{
			if (protoPart == null)
				return null;
			int sensorType = (int)Lib.Proto.GetUInt(protoModule, "sensorType");
			if (sensorType == 0)
				sensorType = moduleOrPrefab.sensorType;
			return SCANsat.FindScanner(protoPart, moduleOrPrefab.experimentType, sensorType);
		}

		private void ToggleArchivePanel()
		{
			if (expInfo == null)
				return;

			if (rndArchiveHeader == null || !rndArchiveHeader.Enabled)
			{
				if (rndArchiveHeader == null)
				{
					rndArchiveHeader = new KsmGuiHeader(window, Local.SCIENCEARCHIVE_title);
					rndArchiveView = new ExperimentSubjectList(window, expInfo);
					rndArchiveView.SetLayoutElement(true, false, 320, -1, -1, 250);
				}
				rndArchiveHeader.Enabled = true;
				rndArchiveView.Enabled = true;
				rndVisibilityButton.SetIconColor(Lib.Kolor.Yellow);
				rndVisibilityButton.SetTooltipText(Local.SCIENCEARCHIVE_hidearchive);
			}
			else
			{
				rndArchiveHeader.Enabled = false;
				rndArchiveView.Enabled = false;
				rndVisibilityButton.SetIconColor(Color.white);
				rndVisibilityButton.SetTooltipText(Local.SCIENCEARCHIVE_showarchive);
			}
			window.RebuildLayout();
		}

		private void ToggleExpInfo()
		{
			if (leftPanel.Enabled)
			{
				leftPanel.Enabled = false;
				expInfoVisibilityButton.SetIconColor(Color.white);
				expInfoVisibilityButton.SetTooltipText(Local.SCIENCEARCHIVE_showexperimentinfo);
			}
			else
			{
				leftPanel.Enabled = true;
				expInfoVisibilityButton.SetIconColor(Lib.Kolor.Yellow);
				expInfoVisibilityButton.SetTooltipText(Local.SCIENCEARCHIVE_hideexperimentinfo);
				expInfoHeader.TextObject.TextComponent.alignment = TMPro.TextAlignmentOptions.Center;
			}
			window.RebuildLayout();
		}
	}
}
