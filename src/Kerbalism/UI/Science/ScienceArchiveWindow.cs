using KERBALISM.KsmGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace KERBALISM
{
	public static class ScienceArchiveWindow
	{
		public static void Open()
		{
			if (window.Enabled)
			{
				window.Close();
				return;
			}
			else
			{
				window.RebuildLayout();
				window.Enabled = true;
			}
		}

		static KsmGuiWindow window;
		static KsmGuiToggleList<ExpInfoAndSubjects> experimentsToggleList;
		static ExpInfoAndSubjects currentExperiment;
		static KsmGuiText expInfoText;

		public static void Init()
		{
			Lib.Log("Science Archive init started");

			window = new KsmGuiWindow(
				KsmGuiWindow.LayoutGroupType.Vertical,
				false,
				1f,
				true,
				0,
				TextAnchor.UpperLeft,
				5f,
				TextAnchor.UpperRight,
				TextAnchor.UpperRight,
				-100, -100);

			KsmGuiHeader mainHeader = new KsmGuiHeader(window, "SCIENCE ARCHIVE");
			new KsmGuiIconButton(mainHeader, Textures.KsmGuiTexHeaderClose, () => window.Close());

			KsmGuiHorizontalLayout columns = new KsmGuiHorizontalLayout(window, 5, 0, 0, 5, 0);

			KsmGuiVerticalLayout experimentColumn = new KsmGuiVerticalLayout(columns, 5);
			experimentColumn.SetLayoutElement(false, true, 150);
			new KsmGuiHeader(experimentColumn, "EXPERIMENTS");

			KsmGuiVerticalScrollView experimentsScrollView = new KsmGuiVerticalScrollView(experimentColumn, 0, 0, 0, 0, 0);
			experimentsScrollView.SetLayoutElement(true, true, 150);
			experimentsToggleList = new KsmGuiToggleList<ExpInfoAndSubjects>(experimentsScrollView, OnToggleExperiment);

			foreach (ExperimentInfo expInfo in ScienceDB.ExperimentInfos.OrderBy(p => p.Title))
			{
				ExperimentSubjectList experimentSubjectList = new ExperimentSubjectList(columns, expInfo);
				experimentSubjectList.Enabled = false;
				ExpInfoAndSubjects expInfoPlus = new ExpInfoAndSubjects(expInfo, experimentSubjectList);
				new KsmGuiToggleListElement<ExpInfoAndSubjects>(experimentsToggleList, expInfoPlus, expInfo.Title);
			}

			Toggle.ToggleEvent temp = experimentsToggleList.ChildToggles[0].ToggleComponent.onValueChanged;
			experimentsToggleList.ChildToggles[0].ToggleComponent.onValueChanged = new Toggle.ToggleEvent();
			experimentsToggleList.ChildToggles[0].ToggleComponent.isOn = true;
			experimentsToggleList.ChildToggles[0].ToggleComponent.onValueChanged = temp;

			currentExperiment = experimentsToggleList.ChildToggles[0].ToggleId;
			currentExperiment.experimentSubjectList.Enabled = true;

			KsmGuiVerticalLayout expInfoColumn = new KsmGuiVerticalLayout(columns, 5);
			new KsmGuiHeader(expInfoColumn, "EXPERIMENT INFO");
			KsmGuiVerticalScrollView expInfoScrollView = new KsmGuiVerticalScrollView(expInfoColumn);
			expInfoScrollView.SetLayoutElement(false, true, 200);
			expInfoText = new KsmGuiText(expInfoScrollView, currentExperiment.expInfo.ModuleInfo);
			expInfoText.SetLayoutElement(true, true);

			
			window.RebuildLayout();
			window.Close();
			Lib.Log("Science Archive init done");
		}

		private static void OnToggleExperiment(ExpInfoAndSubjects expInfoAndSubjects)
		{
			currentExperiment.experimentSubjectList.Enabled = false;
			currentExperiment = expInfoAndSubjects;
			expInfoAndSubjects.experimentSubjectList.KnownSubjectsToggle.SetOnState(true, true);
			expInfoAndSubjects.experimentSubjectList.Enabled = true;
			expInfoText.SetText(expInfoAndSubjects.expInfo.ModuleInfo);
			window.RebuildLayout();
		}

		private class ExpInfoAndSubjects
		{
			public ExperimentInfo expInfo;
			public ExperimentSubjectList experimentSubjectList;

			public ExpInfoAndSubjects(ExperimentInfo expInfo, ExperimentSubjectList experimentSubjectList)
			{
				this.expInfo = expInfo;
				this.experimentSubjectList = experimentSubjectList;
			}
		}
	}
}
