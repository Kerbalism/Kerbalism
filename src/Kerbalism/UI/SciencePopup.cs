using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using static KERBALISM.ExperimentRequirements;

namespace KERBALISM
{
	public class SciencePopup
	{
		// args
		Vessel v;
		Experiment moduleOrPrefab;
		ProtoPartModuleSnapshot protoModule;

		// main panel
		DialogGUIToggle forcedToggle;
		DialogGUIBox stateInfoBox;
		float stateInfoHeight = 100f;

		// left panel
		DialogGUIVerticalLayout leftPanel;
		DialogGUILabel experimentSpecs;
		DialogGUIScrollList scienceArchive;
		enum LeftPanelState { Hidden, ScienceArchive, ExpSpecs }
		LeftPanelState leftPanelState = LeftPanelState.Hidden;

		// top objects refs
		MultiOptionDialog multiOptionDialog;
		PopupDialog popupDialog;

		// state vars
		bool isProto;
		string subject_id;
		ExperimentInfo expInfo;
		Experiment.ExpStatus status;
		Experiment.RunningState expState;
		bool forcedRun;
		bool running;
		string issue;

		// utils
		StringBuilder sb = new StringBuilder();

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

		public SciencePopup(Vessel v, Experiment moduleOrPrefab, ProtoPartModuleSnapshot protoModule = null)
		{


			UIStyle whiteText = new UIStyle(HighLogic.UISkin.label);
			UIStyleState whiteTextState = new UIStyleState();
			whiteTextState.textColor = Color.white;
			whiteText.normal = whiteTextState;

			UIStyle whiteTextBox = new UIStyle(HighLogic.UISkin.box);
			UIStyleState whiteTextBoxState = new UIStyleState();
			whiteTextBoxState.textColor = Color.white;
			whiteTextBoxState.background = HighLogic.UISkin.box.normal.background;
			whiteTextBox.normal = whiteTextBoxState;
			

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

			OnUpdate();

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
				subjectGuiList.Add(new DialogGUILabel(Lib.Color("Nothing retrieved yet", Lib.KColor.Yellow, true), whiteText, true));
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
						subjectGuiList.Add(new DialogGUILabel(Lib.Color(currentBody, Lib.KColor.Orange, true), whiteText, true));
					}

					if (currentSituation != si.situation)
					{
						if (hasBiome)
						{
							subjectGuiList.Add(new DialogGUILabel(sb.ToString(), whiteText, true));
							sb.Length = 0;
						}

						currentSituation = si.situation;
						hasBiome = !string.IsNullOrEmpty(si.biome);

						if (hasBiome)
							subjectGuiList.Add(new DialogGUILabel(Lib.Color(currentSituation, Lib.KColor.Yellow, true), whiteText, true));
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
						subjectGuiList.Add(new DialogGUILabel(sb.ToString(), whiteText, true));
						sb.Length = 0;
					}
					else
					{
						sb.Append("\n");
					}
				}
			}

			scienceArchive = new DialogGUIScrollList
			(
				new Vector2(215f, 100f), new Vector2(215f, 50f * subjectInfos.Count), false, true,
				new DialogGUIVerticalLayout
				(
					false, false, 0f, new RectOffset(3, 3, 3, 3), TextAnchor.UpperLeft, subjectGuiList.ToArray()
				)
			);
			//scienceArchive.OptionEnabledCondition = () => leftPanelState == LeftPanelState.ScienceArchive;

			experimentSpecs = new DialogGUILabel(moduleOrPrefab.SpecsWithoutRequires().Info(), whiteText, true);
			//experimentSpecs.OptionEnabledCondition = () => leftPanelState == LeftPanelState.ExpSpecs;

			leftPanel = new DialogGUIVerticalLayout
			(
				230f, 250f,
				//new DialogGUILabel(RightPanelTitle, true),
				experimentSpecs,
				scienceArchive
			);
			//leftPanel.OptionEnabledCondition = () => leftPanelState != LeftPanelState.Hidden;

			forcedToggle = new DialogGUIToggle(forcedRun, "force collecting", ToggleForcedRun);
			forcedToggle.OptionInteractableCondition = () => running;

			stateInfoBox = new DialogGUIBox
			(
				"", 230f, 400f, null,
				new DialogGUIVerticalLayout
				(
					false, true, 0f, new RectOffset(3, 3, 3, 3), TextAnchor.UpperLeft,
					new DialogGUILabel(GetExpState, whiteText, true)
				)
			);
			stateInfoBox.flexibleHeight = true;

			//stateInfoBox.OnUpdate = () =>
			//{
			//	if (stateInfoBox.size.y != stateInfoHeight)
			//	{
			//		stateInfoBox.size.y = stateInfoHeight;
			//		stateInfoBox.height = stateInfoHeight;
			//		multiOptionDialog.Dirty = true;
			//	}
			//};

			DialogGUIHorizontalLayout content = new DialogGUIHorizontalLayout
			(
				true, true,
				leftPanel,
				new DialogGUIVerticalLayout // width 230
				(
					stateInfoBox,
					forcedToggle,
					new DialogGUIHorizontalLayout
					(
						220f, 20f,
						//new DialogGUIButton("< info", RightPanelToggle, 72f, 28f, false, HighLogic.UISkin.button),
						new DialogGUIButton(() => running ? "stop" : "start", Toggle, null, 100f, 28f, false, HighLogic.UISkin.button),
						new DialogGUIButton("close", null, 100f, 28f, true, HighLogic.UISkin.button)
					)
				)
			);

			multiOptionDialog = new MultiOptionDialog(moduleOrPrefab.experiment_id, "THIS UI IS UGLY, BUGGY AND NOT FINAL", Science.Experiment(moduleOrPrefab.experiment_id).Name, HighLogic.UISkin, 460f, content);
			multiOptionDialog.OnUpdate = OnUpdate;


			popupDialog = PopupDialog.SpawnPopupDialog(multiOptionDialog, false, HighLogic.UISkin, false);


		}

		private void OnUpdate()
		{
			if (isProto)
			{
				status = Lib.Proto.GetEnum(protoModule, "status", Experiment.ExpStatus.Stopped);
				expState = Lib.Proto.GetEnum(protoModule, "expState", Experiment.RunningState.Stopped);
				subject_id = Lib.Proto.GetString(protoModule, "last_subject_id");
				expInfo = Science.Experiment(subject_id);
				running = Experiment.IsRunning(expState);
				forcedRun = expState == Experiment.RunningState.Forced;
				issue = Lib.Proto.GetString(protoModule, "issue");
			}
			else
			{
				status = moduleOrPrefab.Status;
				expState = moduleOrPrefab.State;
				subject_id = moduleOrPrefab.last_subject_id;
				expInfo = moduleOrPrefab.ExpInfo;
				running = moduleOrPrefab.Running;
				forcedRun = moduleOrPrefab.State == Experiment.RunningState.Forced;
				issue = moduleOrPrefab.issue;
			}
		}

		private string RightPanelTitle()
		{
			// return "test";
			switch (leftPanelState)
			{
				case LeftPanelState.ScienceArchive: return "<size=14><b>Science Archive</b></size>";
				case LeftPanelState.ExpSpecs: return "<size=14><b>Experiment specs</b></size>";
				default: return "title";
			}
		}

		private void RightPanelToggle()
		{
			switch (leftPanelState)
			{
				case LeftPanelState.Hidden:
					leftPanelState = LeftPanelState.ScienceArchive;
					multiOptionDialog.dialogRect.x = 450f;
					break;
				case LeftPanelState.ScienceArchive:
					leftPanelState = LeftPanelState.ExpSpecs;
					multiOptionDialog.dialogRect.x = 450f;
					break;
				case LeftPanelState.ExpSpecs:
					leftPanelState = LeftPanelState.Hidden;
					multiOptionDialog.dialogRect.x = 240f;
					break;
			}
			multiOptionDialog.Resize();
		}

		private string GetExpState()
		{
			int lines = 1;
			sb.Length = 0;

			sb.Append("status : ");
			sb.Append(Lib.Bold(Experiment.StatusInfo(status)));

			if (status == Experiment.ExpStatus.Issue)
			{
				lines++;
				sb.Append("\nissue : ");
				sb.Append(Lib.Color(issue, Lib.KColor.Orange));
			}

			lines++;
			sb.Append("\nsituation : ");
			sb.Append(Lib.Color(expInfo.SubjectSituation, Lib.KColor.Yellow, true));

			if (status == Experiment.ExpStatus.Running)
			{
				sb.Append("\ncompletion : ");
				sb.Append(Lib.Color(expInfo.SubjectPercentCollectedTotal.ToString("P0"), Lib.KColor.Yellow, true));
				sb.Append(" - ");
				sb.Append(Lib.Color(Experiment.RunningCountdown(expInfo, moduleOrPrefab.data_rate, true), Lib.KColor.Yellow));
				lines++;
			}

			
			if (!expInfo.SubjectExistsInRnD)
			{
				lines++;
				sb.Append("\nscience value : ");
				sb.Append(Lib.Color("unknown", Lib.KColor.Science, true));
			}
			else
			{
				lines++;
				sb.Append("\nretrieved in RnD : ");
				if (expInfo.SubjectTimesCompleted > 0)
				{
					sb.Append(Lib.Color(Lib.BuildString(expInfo.SubjectTimesCompleted.ToString(), expInfo.SubjectTimesCompleted > 1 ? " times" : " time"), Lib.KColor.Yellow));
				}
				else
				{
					sb.Append(Lib.Color(" never", Lib.KColor.Yellow));
				}
				if (expInfo.SubjectPercentRetrieved > 0.0)
				{
					sb.Append(" (");
					sb.Append(Lib.Color(expInfo.SubjectPercentRetrieved.ToString("P0"), Lib.KColor.Yellow, true));
					sb.Append(")");
				}
				lines++;
				sb.Append("\nscience value :\n");
				sb.Append(Lib.Color(Lib.BuildString(expInfo.SubjectScienceRetrievedInKSC.ToString("F1"), "•"), Lib.KColor.Science, true));
				sb.Append(" in RnD");
				if (expInfo.SubjectScienceCollectedInFlight > 0.05)
				{
					sb.Append(" (");
					sb.Append(Lib.Color(Lib.BuildString("+", expInfo.SubjectScienceCollectedInFlight.ToString("F1"), "•"), Lib.KColor.Science, true));
					sb.Append(" in flight)");
				}
				sb.Append(" / ");
				sb.Append(Lib.Color(Lib.BuildString(expInfo.SubjectScienceMaxValue.ToString("F1"), "•"), Lib.KColor.Science, true));

			}

			if (moduleOrPrefab.Requirements.Requires.Length > 0)
			{
				RequireResult[] reqs;
				moduleOrPrefab.Requirements.TestRequirements(v, out reqs, true);

				lines += 3;
				sb.Append(Lib.Bold("\n\nRequirements :\n"));

				foreach (RequireResult req in reqs)
				{
					lines++;
					sb.Append("\n");
					sb.Append(Lib.Checkbox(req.isValid));
					sb.Append(" ");
					sb.Append(Lib.Bold(ReqName(req.requireDef.require)));
					if (req.value != null)
					{
						lines++;
						sb.Append(" : ");
						sb.Append(Lib.Color(ReqValueFormat(req.requireDef.require, req.requireDef.value), Lib.KColor.Yellow, true));
						sb.Append("\n\tcurrent : ");
						sb.Append(Lib.Color(req.isValid, ReqValueFormat(req.requireDef.require, req.value), Lib.KColor.Green, Lib.KColor.Orange, true));
					}
				}
			}



			stateInfoHeight = Math.Max(stateInfoHeight, 18f * lines);


			return sb.ToString();
		}

		private void IsRunning()
		{

		}


		private void Toggle()
		{
			if (isProto)
				Experiment.ProtoToggle(v, moduleOrPrefab, protoModule);
			else
				moduleOrPrefab.Toggle();
		}


		private void ToggleForcedRun(bool selected)
		{
			if (!running) return;

			if (isProto)
				Experiment.ProtoToggle(v, moduleOrPrefab, protoModule, true);
			else
				moduleOrPrefab.Toggle(true);
		}
	}
}
