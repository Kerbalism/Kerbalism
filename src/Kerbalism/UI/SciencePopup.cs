using KSP.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using static KERBALISM.ExperimentRequirements;
using static KERBALISM.Experiment;
using KERBALISM.KsmGui;

namespace KERBALISM
{
	public class SciencePopup
	{
		private class SubjectInfo
		{
			public string body;
			public string situation;
			public string biome;
			public float totalValue;
			public float retrieved;
			public double collected;
			public double percentRetrieved;
		}

		// args
		Vessel v;
		Experiment moduleOrPrefab;
		ProtoPartModuleSnapshot protoModule;

		// state vars
		bool isProto;
		string subject_id;
		ExperimentInfo expInfo;
		ExpStatus status;
		RunningState expState;
		bool isSample;
		double remainingSampleMass;
		string issue;

		// utils
		StringBuilder sb = new StringBuilder();

		// UI references
		KsmGuiWindow window;

		KsmGuiIconButton leftPanelVisibilityButton;

		KsmGuiVerticalLayout leftPanel;

		KsmGuiHeader rndArchiveHeader;
		KsmGuiVerticalScrollView rndArchiveView;

		KsmGuiTextBox statusBox;

		KsmGuiHeader requirementsHeader;
		KsmGuiTextBox requirementsBox;

		KsmGuiIconButton expInfoVisibilityButton;
		KsmGuiTextBox expInfoBox;

		KsmGuiButton forcedRunButton;
		KsmGuiButton startStopButton;

		public SciencePopup(Vessel v, Experiment moduleOrPrefab, ProtoPartModuleSnapshot protoModule = null)
		{
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
			isSample = moduleOrPrefab.sample_mass > 0f;

			// parse the module / protomodule data so we can use it right now
			GetData();

			// create the window
			window = new KsmGuiWindow(KsmGuiWindow.TopLayoutType.Vertical, 0.8f, true);
			window.SetLayoutElement(false, false, -1, -1, -1, 150);
			window.SetUpdateAction(GetData);

			// top header
			KsmGuiHeader topHeader = new KsmGuiHeader(expInfo.Name);
			leftPanelVisibilityButton = new KsmGuiIconButton(Textures.KsmGuiTexHeaderArrowsLeft, ToggleLeftPanel, "show info & science archive");
			topHeader.AddFirst(leftPanelVisibilityButton);
			topHeader.Add(new KsmGuiIconButton(Textures.KsmGuiTexHeaderClose, () => window.Close(), "close"));
			window.Add(topHeader);

			// 2 columns
			KsmGuiHorizontalLayout panels = new KsmGuiHorizontalLayout(5, 0, 0, 5, 0);
			window.Add(panels);

			// left panel
			leftPanel = new KsmGuiVerticalLayout(5, 0, 0, 5, 0);
			leftPanel.SetLayoutElement(false, true, -1, -1, 200);
			leftPanel.Enabled = false;
			panels.Add(leftPanel);

			// right panel : experiment info
			KsmGuiHeader experimentInfoHeader = new KsmGuiHeader("EXPERIMENT INFO");
			leftPanel.Add(experimentInfoHeader);
			expInfoVisibilityButton = new KsmGuiIconButton(Textures.KsmGuiTexHeaderArrowsDown, ToggleExpInfo, "show experiment info");
			experimentInfoHeader.AddFirst(expInfoVisibilityButton);
			expInfoBox = new KsmGuiTextBox(moduleOrPrefab.SpecsWithoutRequires().Info());
			expInfoBox.Enabled = false;
			leftPanel.Add(expInfoBox);

			// left panel : RnD archive
			rndArchiveHeader = new KsmGuiHeader("SCIENCE ARCHIVE");
			leftPanel.Add(rndArchiveHeader);
			rndArchiveView = new KsmGuiVerticalScrollView();
			rndArchiveView.SetLayoutElement(true, true, -1, -1, -1, 200);
			rndArchiveView.SetUpdateAction(RnDUpdate, 250);
			leftPanel.Add(rndArchiveView);
			//KsmGuiText tempRnD = new KsmGuiText("body\nsituation\nbiome\nbiome\nbiome\nbiome\nbody\nsituation\nbiome\nbiome\nbiome\nbiome");
			//rndArchiveView.Add(tempRnD);
			
			// right panel
			KsmGuiVerticalLayout rightPanel = new KsmGuiVerticalLayout(5, 0, 0, 5, 0);
			rightPanel.SetLayoutElement(false, true, -1, -1, 230);
			panels.Add(rightPanel);



			// right panel : experiment status
			rightPanel.Add(new KsmGuiHeader("STATUS"));
			statusBox = new KsmGuiTextBox("");
			statusBox.SetUpdateAction(StatusUpdate);
			rightPanel.Add(statusBox);

			// right panel : experiment requirements
			if (moduleOrPrefab.Requirements.Requires.Length > 0)
			{
				requirementsHeader = new KsmGuiHeader("REQUIREMENTS");
				rightPanel.Add(requirementsHeader);
				requirementsBox = new KsmGuiTextBox("_");
				requirementsBox.SetUpdateAction(RequirementsUpdate);
				rightPanel.Add(requirementsBox);
			}

			// right panel : buttons
			KsmGuiHorizontalLayout buttons = new KsmGuiHorizontalLayout(5);
			rightPanel.Add(buttons);

			forcedRunButton = new KsmGuiButton("forced run", ToggleForcedRun, "force experiment to run even\nif there is no science value left");
			forcedRunButton.SetUpdateAction(UpdateForcedRunButton);
			buttons.Add(forcedRunButton);

			startStopButton = new KsmGuiButton("_", Toggle, "_");
			startStopButton.SetUpdateAction(UpdateStartStopButton);
			buttons.Add(startStopButton);
		}

		private void GetData()
		{
			if (isProto)
			{
				status = Lib.Proto.GetEnum(protoModule, "status", ExpStatus.Stopped);
				expState = Lib.Proto.GetEnum(protoModule, "expState", RunningState.Stopped);
				subject_id = Lib.Proto.GetString(protoModule, "last_subject_id");
				expInfo = Science.Experiment(subject_id);
				issue = Lib.Proto.GetString(protoModule, "issue");
				if (isSample) remainingSampleMass = Lib.Proto.GetDouble(protoModule, "remainingSampleMass", 0.0);
			}
			else
			{
				status = moduleOrPrefab.Status;
				expState = moduleOrPrefab.State;
				subject_id = moduleOrPrefab.last_subject_id;
				expInfo = moduleOrPrefab.ExpInfo;
				issue = moduleOrPrefab.issue;
				if (isSample) remainingSampleMass = moduleOrPrefab.remainingSampleMass;
			}
		}

		private void StatusUpdate()
		{
			/*
			state :<pos=20em><b><color=#FFD200>started</color></b>
			status :<pos=20em><b><color=#FFD200>running</color></b>
			samples :<pos=20em><b><color=#FFD200>1.5</color></b> (<b><color=#FFD200>20 kg</color></b>)
			situation :<pos=20em><color=#FFD200><b>Duna Space High</b></color>
			retrieved :<pos=20em><color=#FFD200>never</color> (<color=#FFD200><b>0 %</b></color>)
			collected :<pos=20em><color=#6DCFF6><b>4.3</b></color> in RnD (<color=#6DCFF6><b>+2.1</b></color> in flight)
			value :<pos=20em><color=#6DCFF6><b>50.0</b></color>
			*/

			sb.Length = 0;

			sb.Append("state :<pos=20em>");
			sb.Append(Lib.Bold(RunningStateInfo(expState)));
			sb.Append("\nstatus :<pos=20em>");
			sb.Append(Lib.Bold(Experiment.StatusInfo(status, issue)));

			if (isSample)
			{
				sb.Append("\nsamples :<pos=20em>");
				sb.Append(Lib.Color((remainingSampleMass / moduleOrPrefab.sample_mass).ToString("F1"), Lib.KColor.Yellow, true));
				sb.Append(" (");
				sb.Append(Lib.Color(Lib.HumanReadableMass(remainingSampleMass), Lib.KColor.Yellow, true));
				sb.Append(")");
			}

			sb.Append("\nsituation :<pos=20em>");
			sb.Append(Lib.Color(expInfo.SubjectSituation, Lib.KColor.Yellow, true));

			if (!expInfo.SubjectExistsInRnD)
			{
				sb.Append("\nretrieved :<pos=20em>");
				sb.Append(Lib.Color("never", Lib.KColor.Yellow, true));

				sb.Append("\ncollected :<pos=20em>");
				sb.Append(Lib.Color("0.0", Lib.KColor.Science, true));
				sb.Append(Lib.InlineSpriteScience);

				sb.Append("\nvalue :<pos=20em>");
				sb.Append(Lib.Color("unknown", Lib.KColor.Science, true));
			}
			else
			{
				sb.Append("\nretrieved :<pos=20em>");
				if (expInfo.SubjectTimesCompleted > 0)
					sb.Append(Lib.Color(Lib.BuildString(expInfo.SubjectTimesCompleted.ToString(), expInfo.SubjectTimesCompleted > 1 ? " times" : " time"), Lib.KColor.Yellow));
				else
					sb.Append(Lib.Color("never", Lib.KColor.Yellow));

				if (expInfo.SubjectPercentRetrieved > 0.0)
				{
					sb.Append(" (");
					sb.Append(Lib.Color(expInfo.SubjectPercentRetrieved.ToString("P0"), Lib.KColor.Yellow, true));
					sb.Append(")");
				}

				sb.Append("\ncollected :<pos=20em>");
				sb.Append(Lib.Color(expInfo.SubjectScienceRetrievedInKSC.ToString("F1"), Lib.KColor.Science, true));
				sb.Append(Lib.InlineSpriteScience);
				sb.Append(" in RnD");
				if (expInfo.SubjectScienceCollectedInFlight > 0.05)
				{
					sb.Append(" (");
					sb.Append(Lib.Color(Lib.BuildString("+", expInfo.SubjectScienceCollectedInFlight.ToString("F1")), Lib.KColor.Science, true));
					sb.Append(Lib.InlineSpriteScience);
					sb.Append(" in flight)");
				}

				sb.Append("\nvalue :<pos=20em>");
				sb.Append(Lib.Color(expInfo.SubjectScienceMaxValue.ToString("F1"), Lib.KColor.Science, true));
				sb.Append(Lib.InlineSpriteScience);
			}
			statusBox.SetText(sb.ToString());
		}

		private void RequirementsUpdate()
		{
			sb.Length = 0;

			RequireResult[] reqs;
			moduleOrPrefab.Requirements.TestRequirements(v, out reqs, true);

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
					sb.Append(Lib.Color(ReqValueFormat(req.requireDef.require, req.requireDef.value), Lib.KColor.Yellow, true));
					sb.Append("\n<indent=5em>current : "); // match the checkbox indentation
					sb.Append(Lib.Color(req.isValid, ReqValueFormat(req.requireDef.require, req.value), Lib.KColor.Green, Lib.KColor.Orange, true));
					sb.Append("</indent>");
				}
			}

			requirementsBox.SetText(sb.ToString());
		}

		// TODO : clean this mess
		private void RnDUpdate()
		{
			rndArchiveView.Content.ClearChildren();

			Dictionary<string, SubjectInfo> subjectInfos = new Dictionary<string, SubjectInfo>();
			float scienceGainMultiplier = HighLogic.CurrentGame.Parameters.Career.ScienceGainMultiplier;
			foreach (ScienceSubject subject in ResearchAndDevelopment.GetSubjects())
			{
				if (subject.id.StartsWith(moduleOrPrefab.experiment_id))
				{
					SubjectInfo subjectInfo = new SubjectInfo();
					ScienceUtil.GetExperimentFieldsFromScienceID(subject.id, out subjectInfo.body, out subjectInfo.situation, out subjectInfo.biome);
					subjectInfo.situation = ExperimentInfo.ParseSituationSubstring(subjectInfo.situation);
					subjectInfo.totalValue = subject.scienceCap * scienceGainMultiplier;
					subjectInfo.retrieved = subject.science * scienceGainMultiplier;
					ExperimentInfo expInfo = Science.Experiment(subject.id);
					subjectInfo.collected = expInfo.SubjectScienceCollectedInFlight;
					subjectInfo.percentRetrieved = expInfo.SubjectPercentRetrieved;
					subjectInfos.Add(subject.id, subjectInfo);
				}
			}

			foreach (ExperimentInfo expInfo in Science.GetExperimentInfos())
			{
				if (expInfo.IsSubject && expInfo.ExperimentId == moduleOrPrefab.experiment_id && expInfo.SubjectScienceCollectedInFlight > 0.0 && !subjectInfos.ContainsKey(expInfo.SubjectId))
				{
					SubjectInfo subjectInfo = new SubjectInfo();
					ScienceUtil.GetExperimentFieldsFromScienceID(expInfo.SubjectId, out subjectInfo.body, out subjectInfo.situation, out subjectInfo.biome);
					subjectInfo.situation = ExperimentInfo.ParseSituationSubstring(subjectInfo.situation);
					subjectInfo.totalValue = -1f;
					subjectInfo.retrieved = 0f;
					subjectInfo.collected = expInfo.SubjectScienceCollectedInFlight;
					subjectInfo.percentRetrieved = 0.0;
					subjectInfos.Add(expInfo.SubjectId, subjectInfo);
				}
			}

			List<DialogGUIBase> subjectGuiList = new List<DialogGUIBase>();

			if (subjectInfos.Count == 0)
			{
				rndArchiveView.Add(new KsmGuiText(Lib.Color("Nothing retrieved yet", Lib.KColor.Yellow, true)));
			}
			else
			{
				string currentBody = string.Empty;
				string currentSituation = string.Empty;
				bool hasBiome = false;

				StringBuilder sb = new StringBuilder();

				int i = subjectInfos.Count;
				foreach (SubjectInfo si in subjectInfos.Values.OrderBy(p => p.body).ThenBy(p => p.situation))
				{
					i--;
					if (currentBody != si.body)
					{
						currentBody = si.body;
						currentSituation = string.Empty;
						rndArchiveView.Add(new KsmGuiText(Lib.Color(currentBody, Lib.KColor.Orange, true)));
					}

					if (currentSituation != si.situation)
					{
						if (hasBiome)
						{
							rndArchiveView.Add(new KsmGuiText(sb.ToString()));
							sb.Length = 0;
						}

						currentSituation = si.situation;
						hasBiome = !string.IsNullOrEmpty(si.biome);

						if (hasBiome)
							rndArchiveView.Add(new KsmGuiText(Lib.Color(currentSituation, Lib.KColor.Yellow, true)));
					}

					sb.Append(hasBiome ? si.biome : Lib.Color(currentSituation, Lib.KColor.Yellow));
					sb.Append(" : ");
					sb.Append(Lib.Color(si.retrieved.ToString("F1"), Lib.KColor.Science, true));
					if (si.collected > 0.05)
					{
						sb.Append(" (");
						sb.Append(Lib.Color(Lib.BuildString("+", si.collected.ToString("F1")), Lib.KColor.Science, true));
						sb.Append(")");
					}
					sb.Append(" / ");
					sb.Append(Lib.Color(si.totalValue >= 0.0 ? si.totalValue.ToString("F1") : "?", Lib.KColor.Science, true));
					sb.Append(" (");
					sb.Append(si.percentRetrieved.ToString("P0"));
					sb.Append(")");
					if (!hasBiome || i == 0)
					{
						rndArchiveView.Add(new KsmGuiText(sb.ToString()));
						sb.Length = 0;
					}
					else
					{
						sb.Append("\n");
					}
				}
			}
		}

		private void UpdateStartStopButton()
		{
			if (IsRunning(expState))
			{
				startStopButton.SetText("stop");
				startStopButton.SetTooltipText("stop experiment");
			}
			else
			{
				startStopButton.SetText("start");
				startStopButton.SetTooltipText("start experiment");
			}

			startStopButton.SetInteractable(!IsBroken(expState));
		}

		private void UpdateForcedRunButton()
		{
			forcedRunButton.SetInteractable(expState == RunningState.Stopped || expState == RunningState.Running);
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


		private void ToggleLeftPanel()
		{
			if (leftPanel.Enabled)
			{
				leftPanel.Enabled = false;
				leftPanelVisibilityButton.SetIconTexture(Textures.KsmGuiTexHeaderArrowsLeft);
				leftPanelVisibilityButton.SetTooltipText("show info & science archive");
			}
			else
			{
				leftPanel.Enabled = true;
				leftPanelVisibilityButton.SetIconTexture(Textures.KsmGuiTexHeaderArrowsRight);
				leftPanelVisibilityButton.SetTooltipText("hide info & science archive");
			}
		}

		private void ToggleExpInfo()
		{
			if (expInfoBox.Enabled)
			{
				expInfoBox.Enabled = false;
				expInfoVisibilityButton.SetIconTexture(Textures.KsmGuiTexHeaderArrowsDown);
				expInfoVisibilityButton.SetTooltipText("show experiment info");
			}
			else
			{
				expInfoBox.Enabled = true;
				expInfoVisibilityButton.SetIconTexture(Textures.KsmGuiTexHeaderArrowsUp);
				expInfoVisibilityButton.SetTooltipText("hide experiment info");
			}
		}
	}
}
