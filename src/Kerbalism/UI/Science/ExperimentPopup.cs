using KSP.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using static KERBALISM.ExperimentRequirements;
using static KERBALISM.ModuleKsmExperiment;
using KERBALISM.KsmGui;
using static KERBALISM.ScienceDB;
using KSP.Localization;

namespace KERBALISM
{

	public class ExperimentPopup
	{
		// args
		Vessel v;
		ModuleKsmExperiment moduleOrPrefab;
		ProtoPartModuleSnapshot protoModule;

		VesselData vd;

		// state vars
		bool canInteract;
		bool isProto;
		ExperimentModuleDefinition moduleDefinition;
		SubjectData subjectData;
		ExpStatus status;
		RunningState expState;
		bool isSample;
		double remainingSampleMass;
		string issue;

		// utils
		StringBuilder sb = new StringBuilder();

		// UI references
		KsmGuiWindow window;

		KsmGuiIconButton rndVisibilityButton;
		KsmGuiIconButton expInfoVisibilityButton;

		KsmGuiVerticalLayout leftPanel;
		KsmGuiTextBox expInfoBox;

		KsmGuiTextBox statusBox;

		KsmGuiButton forcedRunButton;
		KsmGuiButton startStopButton;

		KsmGuiTextBox requirementsBox;

		KsmGuiHeader expInfoHeader;

		KsmGuiHeader rndArchiveHeader;
		ExperimentSubjectList rndArchiveView;

		private static List<long> activePopups = new List<long>();
		private long popupId;

		public ExperimentPopup(Vessel v, ModuleKsmExperiment moduleOrPrefab, uint partId, string partName, ProtoPartModuleSnapshot protoModule = null)
		{
			popupId = partId + moduleOrPrefab.ExperimentID.GetHashCode();

			if (activePopups.Contains(popupId))
				return;

			activePopups.Add(popupId);

			if (protoModule == null)
			{
				isProto = false;
			}
			else
			{
				isProto = true;
				this.protoModule = protoModule;
			}

			this.moduleOrPrefab = moduleOrPrefab;
			this.v = v;
			vd = v.KerbalismData();
			moduleDefinition = moduleOrPrefab.ModuleDefinition;
			isSample = moduleDefinition.Info.IsSample;

			// parse the module / protomodule data so we can use it right now
			GetData();

			// create the window
			window = new KsmGuiWindow(KsmGuiWindow.LayoutGroupType.Vertical, true, KsmGuiStyle.defaultWindowOpacity, true, 0, TextAnchor.UpperLeft, 5f);
			window.OnClose = () => activePopups.Remove(popupId);
			window.SetLayoutElement(false, false, -1, -1, -1, 150);
			window.SetUpdateAction(GetData);

			// top header
			KsmGuiHeader topHeader = new KsmGuiHeader(window, moduleDefinition.Info.Title, default, 120);
			topHeader.TextObject.SetTooltipText(Lib.BuildString(Local.SCIENCEARCHIVE_onvessel, " ", Lib.Bold(v.vesselName), "\n", Local.SCIENCEARCHIVE_onpart, " ", Lib.Bold(partName)));//"on vessel :"on part :
			rndVisibilityButton = new KsmGuiIconButton(topHeader, Textures.KsmGuiTexHeaderRnD, ToggleArchivePanel, Local.SCIENCEARCHIVE_showarchive);//"show science archive"
			rndVisibilityButton.MoveAsFirstChild();
			expInfoVisibilityButton = new KsmGuiIconButton(topHeader, Textures.KsmGuiTexHeaderInfo, ToggleExpInfo, Local.SCIENCEARCHIVE_showexperimentinfo);//"show experiment info"
			expInfoVisibilityButton.MoveAsFirstChild();
			new KsmGuiIconButton(topHeader, Textures.KsmGuiTexHeaderClose, () => window.Close(), Local.SCIENCEARCHIVE_closebutton);//"close"

			// 2 columns
			KsmGuiHorizontalLayout panels = new KsmGuiHorizontalLayout(window, 5, 0, 0, 0, 0);

			// left panel
			leftPanel = new KsmGuiVerticalLayout(panels, 5);
			leftPanel.SetLayoutElement(false, true, -1, -1, 160);
			leftPanel.Enabled = false;

			// right panel : experiment info
			expInfoHeader = new KsmGuiHeader(leftPanel, Local.SCIENCEARCHIVE_EXPERIMENTINFO);//"EXPERIMENT INFO"
			expInfoBox = new KsmGuiTextBox(leftPanel, Specs(moduleOrPrefab.ModuleDefinition).Info());
			expInfoBox.SetLayoutElement(false, true, 160);

			// right panel
			KsmGuiVerticalLayout rightPanel = new KsmGuiVerticalLayout(panels, 5);
			rightPanel.SetLayoutElement(false, true, -1, -1, 230);

			// right panel : experiment status
			new KsmGuiHeader(rightPanel, Local.SCIENCEARCHIVE_STATUS);//"STATUS"
			statusBox = new KsmGuiTextBox(rightPanel, "_");
			statusBox.TextObject.TextComponent.enableWordWrapping = false;
			statusBox.TextObject.TextComponent.overflowMode = TMPro.TextOverflowModes.Truncate;
			statusBox.SetLayoutElement(true, true, 230);
			statusBox.SetUpdateAction(StatusUpdate);

			// right panel : buttons
			KsmGuiHorizontalLayout buttons = new KsmGuiHorizontalLayout(rightPanel, 5);

			forcedRunButton = new KsmGuiButton(buttons, Local.SCIENCEARCHIVE_forcedrun, ToggleForcedRun, Local.SCIENCEARCHIVE_forcedrun_desc);//"forced run""force experiment to run even\nif there is no science value left"
			forcedRunButton.SetUpdateAction(UpdateForcedRunButton);

			startStopButton = new KsmGuiButton(buttons, "_", Toggle);
			startStopButton.SetUpdateAction(UpdateStartStopButton);

			// right panel : experiment requirements
			if (moduleDefinition.Requirements.Requires.Length > 0)
			{
				new KsmGuiHeader(rightPanel, Local.SCIENCEARCHIVE_REQUIREMENTS);//"REQUIREMENTS"
				requirementsBox = new KsmGuiTextBox(rightPanel, "_");
				requirementsBox.SetLayoutElement(false, false, 230);
				requirementsBox.SetUpdateAction(RequirementsUpdate);
			}

			window.RebuildLayout();
		}

		private void GetData()
		{
			canInteract = vd.Connection.linked || vd.CrewCount > 0;
			moduleDefinition = moduleOrPrefab.ModuleDefinition;

			if (isProto)
			{
				status = Lib.Proto.GetEnum(protoModule, "status", ExpStatus.Stopped);
				expState = Lib.Proto.GetEnum(protoModule, "expState", RunningState.Stopped);
				int situationId = Lib.Proto.GetInt(protoModule, "situationId", 0);
				subjectData = ScienceDB.GetSubjectData(moduleDefinition.Info, situationId);
				issue = Lib.Proto.GetString(protoModule, "issue");
				if (isSample) remainingSampleMass = Lib.Proto.GetDouble(protoModule, "remainingSampleMass", 0.0);
			}
			else
			{
				status = moduleOrPrefab.Status;
				expState = moduleOrPrefab.State;
				subjectData = moduleOrPrefab.Subject;
				issue = moduleOrPrefab.issue;
				if (isSample) remainingSampleMass = moduleOrPrefab.remainingSampleMass;
			}
		}

		private void StatusUpdate()
		{
			sb.Length = 0;

			sb.Append(Local.SCIENCEARCHIVE_state);//state
			sb.Append(" :<pos=20em>");
			sb.Append(Lib.Bold(RunningStateInfo(expState)));
			sb.Append("\n");
			sb.Append(Local.SCIENCEARCHIVE_status);//status
			sb.Append(" :<pos=20em>");
			sb.Append(Lib.Bold(StatusInfo(status, issue)));

			if (status == ExpStatus.Running)
			{
				sb.Append(", ");
				sb.Append(RunningCountdown(moduleDefinition.Info, subjectData, moduleDefinition.DataRate, true));
			}
			else if (status == ExpStatus.Forced && subjectData != null)
			{
				sb.Append(", ");
				sb.Append(Lib.Color(subjectData.PercentCollectedTotal.ToString("P1"), Lib.Kolor.Yellow, true));
				sb.Append(" ");
				sb.Append(Local.SCIENCEARCHIVE_collected);//collected
			}

			if (isSample && !moduleDefinition.SampleCollecting)
			{
				sb.Append("\n");
				sb.Append(Local.SCIENCEARCHIVE_samples);//samples
				sb.Append(" :<pos=20em>");
				sb.Append(Lib.Color((remainingSampleMass / moduleDefinition.Info.SampleMass).ToString("F1"), Lib.Kolor.Yellow, true));
				sb.Append(" (");
				sb.Append(Lib.Color(Lib.HumanReadableMass(remainingSampleMass), Lib.Kolor.Yellow, true));
				sb.Append(")");
			}

			sb.Append("\n");
			sb.Append(Local.SCIENCEARCHIVE_situation);//situation
			sb.Append(" :<pos=20em>");
			sb.Append(Lib.Color(vd.VesselSituations.GetExperimentSituation(moduleDefinition.Info).GetTitleForExperiment(moduleDefinition.Info), Lib.Kolor.Yellow, true));

			if (subjectData == null)
			{
				sb.Append("\n");
				sb.Append(Local.SCIENCEARCHIVE_retrieved);//retrieved
				sb.Append(" :<pos=20em>");
				sb.Append(Lib.Color(Local.SCIENCEARCHIVE_invalidsituation, Lib.Kolor.Yellow, true));//"invalid situation"

				sb.Append("\n");
				sb.Append(Local.SCIENCEARCHIVE_collected);//collected
				sb.Append(" :<pos=20em>");
				sb.Append(Lib.Color(Local.SCIENCEARCHIVE_invalidsituation, Lib.Kolor.Yellow, true));//"invalid situation"

				sb.Append("\n");
				sb.Append(Local.SCIENCEARCHIVE_value);//value
				sb.Append(" :<pos=20em>");
				sb.Append(Lib.Color(Local.SCIENCEARCHIVE_invalidsituation, Lib.Kolor.Yellow, true));//"invalid situation"
			}
			else
			{
				sb.Append("\n");
				sb.Append(Local.SCIENCEARCHIVE_retrieved);//retrieved
				sb.Append(" :<pos=20em>");
				if (subjectData.TimesCompleted > 0)
					sb.Append(Lib.Color(Lib.BuildString(subjectData.TimesCompleted.ToString(), subjectData.TimesCompleted > 1 ? " times" : " time"), Lib.Kolor.Yellow));
				else
					sb.Append(Lib.Color(Local.SCIENCEARCHIVE_never, Lib.Kolor.Yellow));//"never"

				if (subjectData.PercentRetrieved > 0.0)
				{
					sb.Append(" (");
					sb.Append(Lib.Color(subjectData.PercentRetrieved.ToString("P0"), Lib.Kolor.Yellow, true));
					sb.Append(")");
				}

				sb.Append("\n");
				sb.Append(Local.SCIENCEARCHIVE_collected);//collected
				sb.Append(" :<pos=20em>");
				sb.Append(Lib.Color(subjectData.ScienceRetrievedInKSC.ToString("F1"), Lib.Kolor.Science, true));
				sb.Append(Lib.InlineSpriteScience);
				sb.Append(" ");
				sb.Append(Local.SCIENCEARCHIVE_inRnD);//in RnD
				if (subjectData.ScienceCollectedInFlight > 0.05)
				{
					sb.Append(" (");
					sb.Append(Lib.Color(Lib.BuildString("+", subjectData.ScienceCollectedInFlight.ToString("F1")), Lib.Kolor.Science, true));
					sb.Append(Lib.InlineSpriteScience);
					sb.Append(" ");
					sb.Append(Local.SCIENCEARCHIVE_inflight);//in flight)
				}

				sb.Append("\n");
				sb.Append(Local.SCIENCEARCHIVE_value);//value
				sb.Append(" :<pos=20em>");
				sb.Append(Lib.Color(subjectData.ScienceMaxValue.ToString("F1"), Lib.Kolor.Science, true));
				sb.Append(Lib.InlineSpriteScience);
			}

			statusBox.Text = sb.ToString();
		}

		private void RequirementsUpdate()
		{
			sb.Length = 0;

			RequireResult[] reqs;
			moduleDefinition.Requirements.TestRequirements(v, out reqs, true);

			bool first = true;
			foreach (RequireResult req in reqs)
			{
				if (!first)
					sb.Append("\n");
				first = false;
				sb.Append(Lib.Checkbox(req.isValid));
				//sb.Append(" ");
				sb.Append(Lib.Bold(ReqName(req.requireDef.require)));
				if (req.value != null)
				{
					sb.Append(" : ");
					sb.Append(Lib.Color(ReqValueFormat(req.requireDef.require, req.requireDef.value), Lib.Kolor.Yellow, true));
					sb.Append("\n<indent=5em>"); // match the checkbox indentation
					sb.Append(Local.SCIENCEARCHIVE_current);//"current"
					sb.Append(" : ");
					sb.Append(Lib.Color(req.isValid, ReqValueFormat(req.requireDef.require, req.value), Lib.Kolor.Green, Lib.Kolor.Orange, true));
					sb.Append("</indent>");
				}
			}

			requirementsBox.Text = sb.ToString();
		}

		private void UpdateStartStopButton()
		{
			if (IsRunning(expState))
			{
				startStopButton.Text = Local.SCIENCEARCHIVE_stop;//"stop"
			}
			else
			{
				startStopButton.Text = Local.SCIENCEARCHIVE_start;//"start"
			}

			startStopButton.Interactable = canInteract && !IsBroken(expState);
		}

		private void UpdateForcedRunButton()
		{
			forcedRunButton.Interactable = canInteract && (expState == RunningState.Stopped || expState == RunningState.Running);
		}

		private void Toggle()
		{
			if (isProto)
				ProtoToggle(v, moduleOrPrefab, protoModule);
			else
				moduleOrPrefab.Toggle();
		}


		private void ToggleForcedRun()
		{
			if (isProto)
				ProtoToggle(v, moduleOrPrefab, protoModule, true);
			else
				moduleOrPrefab.Toggle(true);
		}


		private void ToggleArchivePanel()
		{
			if (rndArchiveHeader == null || !rndArchiveHeader.Enabled)
			{
				// create the RnD archive on demand, as this is is a bit laggy and takes quite a lot of memory
				if (rndArchiveHeader == null)
				{
					rndArchiveHeader = new KsmGuiHeader(window, Local.SCIENCEARCHIVE_title);//"SCIENCE ARCHIVE"
					rndArchiveView = new ExperimentSubjectList(window, moduleDefinition.Info);
					rndArchiveView.SetLayoutElement(true, false, 320, -1, -1, 250);
				}
				rndArchiveHeader.Enabled = true;
				rndArchiveView.Enabled = true;
				rndVisibilityButton.SetIconColor(Lib.Kolor.Yellow);
				rndVisibilityButton.SetTooltipText(Local.SCIENCEARCHIVE_hidearchive);//"hide science archive"
			}
			else
			{
				rndArchiveHeader.Enabled = false;
				rndArchiveView.Enabled = false;
				rndVisibilityButton.SetIconColor(Color.white);
				rndVisibilityButton.SetTooltipText(Local.SCIENCEARCHIVE_showarchive);//"show science archive"
			}
			window.RebuildLayout();
		}

		private void ToggleExpInfo()
		{
			if (leftPanel.Enabled)
			{
				leftPanel.Enabled = false;
				expInfoVisibilityButton.SetIconColor(Color.white);
				expInfoVisibilityButton.SetTooltipText(Local.SCIENCEARCHIVE_showexperimentinfo);//"show experiment info"
			}
			else
			{
				leftPanel.Enabled = true;
				expInfoVisibilityButton.SetIconColor(Lib.Kolor.Yellow);
				expInfoVisibilityButton.SetTooltipText(Local.SCIENCEARCHIVE_hideexperimentinfo);//"hide experiment info"
				expInfoHeader.TextObject.TextComponent.alignment = TMPro.TextAlignmentOptions.Center; // strange bug
			}
			window.RebuildLayout();
		}
	}
}
