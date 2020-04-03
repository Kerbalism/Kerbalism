using KERBALISM.KsmGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using KSP.Localization;

namespace KERBALISM
{
	public class ScienceArchiveWindow
	{
		public static ScienceArchiveWindow Instance { get; private set; }

		public ScienceArchiveWindow()
		{
			Init();
		}

		public static void Toggle()
		{
			if (window.Enabled)
			{
				Close();
			}
			else
			{
				UpdateResearchedFilter();
				vesselFilter.SetOnState(false, false);
				researchFilter.SetOnState(true, false);
				if (ROCFilter != null) ROCFilter.SetOnState(true, false);
				window.Enabled = true;
				UpdateVisibleExperiments();
			}
		}

		public static void Close()
		{
			foreach (KsmGuiToggleListElement<ExpInfoAndSubjects> exp in experimentsToggleList.ChildToggles)
			{
				foreach (ExperimentSubjectList.BodyContainer body in exp.ToggleId.experimentSubjectList.BodyContainers)
				{
					body.SubjectsContainer.DestroyUIObjects();
				}
			}

			window.Close();
		}

		static KsmGuiWindow window;
		static KsmGuiToggleList<ExpInfoAndSubjects> experimentsToggleList;
		static ExpInfoAndSubjects currentExperiment;
		static KsmGuiText expInfoText;
		static KsmGuiToggle vesselFilter;
		static KsmGuiToggle researchFilter;
		static KsmGuiToggle ROCFilter;

		static HashSet<ExperimentInfo> researchedExpInfos = new HashSet<ExperimentInfo>();
		static HashSet<ExperimentInfo> vesselExpInfos = new HashSet<ExperimentInfo>();
		static HashSet<ExperimentInfo> ROCExpInfos = new HashSet<ExperimentInfo>();

		static List<Part> vesselParts = null;
		static int lastPartCount;

		private void Init()
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
				TextAnchor.UpperLeft,
				TextAnchor.UpperLeft,
				280, -100);

			KsmGuiHeader mainHeader = new KsmGuiHeader(window, Local.SCIENCEARCHIVE_title);//"SCIENCE ARCHIVE"
			new KsmGuiIconButton(mainHeader, Textures.KsmGuiTexHeaderClose, () => Close());

			KsmGuiHorizontalLayout columns = new KsmGuiHorizontalLayout(window, 5, 0, 0, 5, 0);

			KsmGuiVerticalLayout experimentColumn = new KsmGuiVerticalLayout(columns, 5);
			experimentColumn.SetLayoutElement(false, true, 160);
			new KsmGuiHeader(experimentColumn, Local.SCIENCEARCHIVE_EXPERIMENTS);//"EXPERIMENTS"

			researchFilter = new KsmGuiToggle(experimentColumn, Local.SCIENCEARCHIVE_filter1, true, OnToggleResearchedFilter);//"filter by researched"
			if (Kerbalism.SerenityEnabled)
				ROCFilter = new KsmGuiToggle(experimentColumn, Local.SCIENCEARCHIVE_filter2, true, OnToggleROCFilter);//"filter ROCs"
			vesselFilter = new KsmGuiToggle(experimentColumn, Local.SCIENCEARCHIVE_filter3, false, OnToggleVesselFilter);//"filter by current vessel"

			KsmGuiVerticalScrollView experimentsScrollView = new KsmGuiVerticalScrollView(experimentColumn, 0, 0, 0, 0, 0);
			experimentsScrollView.SetLayoutElement(true, true, 160);
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
			new KsmGuiHeader(expInfoColumn, Local.SCIENCEARCHIVE_EXPERIMENTINFO);//"EXPERIMENT INFO"
			KsmGuiVerticalScrollView expInfoScrollView = new KsmGuiVerticalScrollView(expInfoColumn);
			expInfoScrollView.SetLayoutElement(false, true, 200);
			expInfoText = new KsmGuiText(expInfoScrollView, currentExperiment.expInfo.ModuleInfo);
			expInfoText.SetLayoutElement(true, true);

			foreach (ExperimentInfo experimentInfo in ScienceDB.ExperimentInfos)
				if (experimentInfo.IsROC)
					ROCExpInfos.Add(experimentInfo);

			window.SetUpdateAction(Update, 20);

			Callbacks.onConfigure.Add(OnConfigure);

			//window.RebuildLayout();
			window.Close();
			Lib.Log("Science Archive init done");
		}

		private static void Update()
		{
			if (Lib.IsEditor)
				vesselParts = EditorLogic.fetch.ship.Parts;
			else if (Lib.IsFlight)
				vesselParts = FlightGlobals.ActiveVessel?.Parts;
			else
				vesselParts = null;

			if (vesselParts == null || vesselParts.Count == 0)
			{
				if (vesselFilter.Enabled)
				{
					if (vesselFilter.IsOn)
					{
						vesselFilter.SetOnState(false, true);
					}
					vesselFilter.Enabled = false;
				}
			}
			else
			{
				if (!vesselFilter.Enabled)
				{
					vesselFilter.Enabled = true;
				}

				if (vesselFilter.IsOn && lastPartCount != vesselParts.Count)
				{
					UpdateVesselFilter();
					UpdateVisibleExperiments();
				}
			}

			lastPartCount = vesselParts != null ? vesselParts.Count : 0;
		}

		private static void OnToggleExperiment(ExpInfoAndSubjects expInfoAndSubjects)
		{
			currentExperiment.experimentSubjectList.Enabled = false;
			expInfoAndSubjects.experimentSubjectList.KnownSubjectsToggle.SetOnState(currentExperiment.experimentSubjectList.KnownSubjectsToggle.IsOn, true);
			currentExperiment = expInfoAndSubjects;
			expInfoAndSubjects.experimentSubjectList.Enabled = true;
			expInfoText.Text = expInfoAndSubjects.expInfo.ModuleInfo;
			window.RebuildLayout();
		}

		private static void OnToggleResearchedFilter(bool isOn)
		{
			if (isOn) UpdateResearchedFilter();
			UpdateVisibleExperiments();
		}

		private static void OnToggleVesselFilter(bool isOn)
		{
			if (isOn) UpdateVesselFilter();
			UpdateVisibleExperiments();
		}

		private static void OnToggleROCFilter(bool isOn)
		{
			UpdateVisibleExperiments();
		}

		private static void UpdateVisibleExperiments()
		{
			bool needRebuild = false;
			foreach (KsmGuiToggleListElement<ExpInfoAndSubjects> exp in experimentsToggleList.ChildToggles)
			{
				bool visible = true;
				if (researchFilter.IsOn && !researchedExpInfos.Contains(exp.ToggleId.expInfo))
					visible = false;
				else if (vesselFilter.Enabled && vesselFilter.IsOn && !vesselExpInfos.Contains(exp.ToggleId.expInfo))
					visible = false;

				if (ROCFilter != null && ROCFilter.IsOn && ROCExpInfos.Contains(exp.ToggleId.expInfo))
					visible = false;

				if (exp.Enabled != visible)
				{
					exp.Enabled = visible;
					needRebuild |= true;
				}
			}

			if (needRebuild)
			{
				window.RebuildLayout();
			}
		}


		private void OnConfigure(Part part, Configure configureModule)
		{
			if (window.Enabled && vesselFilter.Enabled && vesselFilter.IsOn)
			{
				UpdateVesselFilter();
				UpdateVisibleExperiments();
			}
		}

		private static void UpdateVesselFilter()
		{
			vesselExpInfos.Clear();
			bool hasROCScience = false;

			foreach (Part part in vesselParts)
			{
				foreach (PartModule partModule in part.Modules)
				{
					if (!partModule.enabled || !partModule.isEnabled)
						continue;

					if (partModule is ModuleKsmExperiment experiment)
					{
						vesselExpInfos.Add(experiment.moduleData.ModuleDefinition.Info);
					}
					else if (partModule is ModuleScienceExperiment stockExperiment)
					{
						if (stockExperiment.experimentID == "ROCScience")
						{
							hasROCScience = true;
						}
						else
						{
							ExperimentInfo expInfo = ScienceDB.GetExperimentInfo(stockExperiment.experimentID);
							if (expInfo != null)
								vesselExpInfos.Add(expInfo);
						}
					}
					else if (partModule is ModuleInventoryPart inventory)
					{
						foreach (string inventoryPartName in inventory.InventoryPartsList)
						{
							Part groundPart = PartLoader.getPartInfoByName(inventoryPartName)?.partPrefab;
							if (groundPart == null)
								continue;

							foreach (PartModule groundmodule in groundPart.Modules)
							{
								if (groundmodule is ModuleGroundExperiment groundExp)
								{
									ExperimentInfo expInfo = ScienceDB.GetExperimentInfo(groundExp.experimentId);
									if (expInfo != null)
										vesselExpInfos.Add(expInfo);
								}
							}
						}
					}
				}
			}

			if (hasROCScience)
			{
				foreach (ExperimentInfo experimentInfo in ScienceDB.ExperimentInfos)
				{
					if (experimentInfo.IsROC)
					{
						vesselExpInfos.Add(experimentInfo);
					}
				}
			}
		}

		private static void UpdateResearchedFilter()
		{
			researchedExpInfos.Clear();

			ExperimentInfo asteroidSample = ScienceDB.GetExperimentInfo("asteroidSample");
			if (asteroidSample != null)
				researchedExpInfos.Add(asteroidSample);

			foreach (AvailablePart availablePart in PartLoader.LoadedPartsList)
			{
				// EVA kerbals have no tech required
				if (!string.IsNullOrEmpty(availablePart.TechRequired) && !ResearchAndDevelopment.PartModelPurchased(availablePart))
					continue;

				foreach (PartModule partModule in availablePart.partPrefab.Modules)
				{
					if (partModule is ModuleKsmExperiment experiment)
					{
						// TODO : this probably doesn't work anymore
						if (experiment.moduleData.ModuleDefinition == null || researchedExpInfos.Contains(experiment.moduleData.ModuleDefinition.Info))
							continue;

						if (experiment.moduleData.ModuleDefinition.Requirements.TestProgressionRequirements())
							researchedExpInfos.Add(experiment.moduleData.ModuleDefinition.Info);
					}
					else if (partModule is ModuleScienceExperiment stockExperiment)
					{
						ExperimentInfo expInfo = ScienceDB.GetExperimentInfo(stockExperiment.experimentID);
						if (expInfo != null)
							researchedExpInfos.Add(expInfo);
					}
					else if (partModule is ModuleGroundExperiment groundExp)
					{
						ExperimentInfo expInfo = ScienceDB.GetExperimentInfo(groundExp.experimentId);
						if (expInfo != null)
							researchedExpInfos.Add(expInfo);
					}
				}
			}

			// ROCs are always researched
			foreach (ExperimentInfo experimentInfo in ScienceDB.ExperimentInfos)
			{
				if (experimentInfo.IsROC)
				{
					researchedExpInfos.Add(experimentInfo);
				}
			}
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
