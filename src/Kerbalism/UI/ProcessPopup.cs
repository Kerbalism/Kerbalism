﻿using KERBALISM.KsmGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using KSP.Localization;

namespace KERBALISM
{
	public class ProcessPopup
	{
		VesselData vd;
		VesselProcessData processData;

		// UI references
		KsmGuiWindow window;
		KsmGuiTextBox statusBox;
		KsmGuiSlider capacitySlider;
		KsmGuiButton enableButton;

		List<ResourceEntry> resources = new List<ResourceEntry>();
		List<PartEntry> parts = new List<PartEntry>();

		private static List<string> activePopups = new List<string>();
		private string popupId;

		public ProcessPopup(VesselProcessData processData, VesselData vd)
		{
			popupId = processData.processName;

			if (activePopups.Contains(popupId))
				return;

			activePopups.Add(popupId);

			this.processData = processData;
			this.vd = vd;

			// create the window
			window = new KsmGuiWindow(KsmGuiWindow.LayoutGroupType.Vertical, true, KsmGuiStyle.defaultWindowOpacity, true, 0, TextAnchor.UpperLeft, 5f);
			window.OnClose = () => activePopups.Remove(popupId);
			window.SetLayoutElement(false, false, 300);
			//window.SetUpdateAction(GetData);

			// top header
			KsmGuiHeader topHeader = new KsmGuiHeader(window, processData.process.title, default, 120);
			new KsmGuiIconButton(topHeader, Textures.KsmGuiTexHeaderClose, () => window.Close(), Local.SCIENCEARCHIVE_closebutton);//"close"

			// content panel
			KsmGuiVerticalLayout content = new KsmGuiVerticalLayout(window, 5);
			content.SetLayoutElement(false, true, -1, -1);

			statusBox = new KsmGuiTextBox(content, "_");
			statusBox.TextObject.TextComponent.enableWordWrapping = false;
			statusBox.TextObject.TextComponent.overflowMode = TextOverflowModes.Ellipsis;

			KsmGuiHorizontalLayout buttons = new KsmGuiHorizontalLayout(content, 5);
			capacitySlider = new KsmGuiSlider(buttons, 0f, 1f, false, OnCapacityTweak, null, 200);
			capacitySlider.Value = (float)processData.maxSetting;
			enableButton = new KsmGuiButton(buttons, Local.Generic_ENABLED, OnToggle);

			new KsmGuiHeader(content, Local.ProcessPopup_TITLE);

			KsmGuiVerticalLayout resourceList = new KsmGuiVerticalLayout(content);

			KsmGuiBase resListHeader = new KsmGuiBase(resourceList);
			resListHeader.SetLayoutElement(true, false, -1, 16);
			resListHeader.AddImageComponentWithColor(KsmGuiStyle.boxColor);

			KsmGuiText resHeaderText = new KsmGuiText(resListHeader,
				Local.ProcessPopup_NameTitle,
				Local.ProcessPopup_NameTooltip,
				TextAlignmentOptions.Left);
			resHeaderText.TextComponent.fontStyle = FontStyles.Bold;
			resHeaderText.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 5, 0);
			resHeaderText.TopTransform.SetSizeDelta(95, 16);

			KsmGuiText nominalHeaderText = new KsmGuiText(resListHeader,
				Local.ProcessPopup_MaxRateTitle,
				Local.ProcessPopup_MaxRateTooltip,
				TextAlignmentOptions.Right);
			nominalHeaderText.TextComponent.fontStyle = FontStyles.Bold;
			nominalHeaderText.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 105, 0);
			nominalHeaderText.TopTransform.SetSizeDelta(65, 16);

			KsmGuiText statusHeaderText = new KsmGuiText(resListHeader,
				Local.ProcessPopup_StatusTitle,
				Local.ProcessPopup_StatusTooltip,
				TextAlignmentOptions.Right);
			statusHeaderText.TextComponent.fontStyle = FontStyles.Bold;
			statusHeaderText.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 175, 0);
			statusHeaderText.TopTransform.SetSizeDelta(80, 16);

			KsmGuiText dumpHeaderText = new KsmGuiText(resListHeader,
				Local.ProcessPopup_DumpTitle,
				Local.ProcessPopup_DumpTooltip,
				TextAlignmentOptions.Right);
			dumpHeaderText.TextComponent.color = Lib.KolorToColor(Lib.Kolor.Yellow);
			dumpHeaderText.TextComponent.fontStyle = FontStyles.Bold;
			dumpHeaderText.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 250, 0);
			dumpHeaderText.TopTransform.SetSizeDelta(40, 16);

			foreach (KeyValuePair<string, double> output in processData.process.outputs)
			{
				VesselResource resource = vd.ResHandler.GetResource(output.Key);
				if (!resource.Visible) // don't display invisible resources
					continue;
				resources.Add(new ResourceEntry(resourceList, this, output, false));
			}

			foreach (KeyValuePair<string, double> input in processData.process.inputs)
			{
				VesselResource resource = vd.ResHandler.GetResource(input.Key);
				if (!resource.Visible) // don't display invisible resources
					continue;
				resources.Add(new ResourceEntry(resourceList, this, input, true));
			}

			new KsmGuiHeader(content, Local.ProcessPopup_PARTS); // "PARTS"

			KsmGuiVerticalLayout partList = new KsmGuiVerticalLayout(content);

			KsmGuiBase partListHeader = new KsmGuiBase(partList);
			partListHeader.SetLayoutElement(true, false, -1, 16);
			partListHeader.AddImageComponentWithColor(KsmGuiStyle.boxColor);

			KsmGuiText partListPartName = new KsmGuiText(partListHeader, Local.ProcessPopup_NameTitle, null, TextAlignmentOptions.Left, false, TextOverflowModes.Ellipsis);
			partListPartName.TextComponent.fontStyle = FontStyles.Bold;
			partListPartName.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 5, 0);
			partListPartName.TopTransform.SetSizeDelta(150, 16);

			KsmGuiText partListCapacity = new KsmGuiText(partListHeader, Local.ProcessPopup_Capacity, null, TextAlignmentOptions.Center);
			partListCapacity.TextComponent.fontStyle = FontStyles.Bold;
			partListCapacity.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 160, 0);
			partListCapacity.TopTransform.SetSizeDelta(55, 16);

			KsmGuiText partListEnabled = new KsmGuiText(partListHeader, Lib.UppercaseFirst(Local.Generic_ENABLED), null, TextAlignmentOptions.Center);
			partListEnabled.TextComponent.color = Lib.KolorToColor(Lib.Kolor.Yellow);
			partListEnabled.TextComponent.fontStyle = FontStyles.Bold;
			partListEnabled.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 230, 0);
			partListEnabled.TopTransform.SetSizeDelta(60, 16);

			foreach (PartData pd in vd.Parts)
			{
				if (pd.Processes == null)
					continue;

				foreach (PartProcessData ppd in pd.Processes)
				{
					if (ppd.processName == processData.processName)
					{
						parts.Add(new PartEntry(partList, pd.PartInfo.title, ppd, vd));
					}
				}
			}

			window.SetUpdateAction(Update);
			window.RebuildLayout();
		}

		private void Update()
		{
			StringBuilder sb = new StringBuilder();
			sb.Append(Local.ProcessPopup_VesselCapacity); // "Vessel capacity"
			sb.Append(" : ");
			sb.Append(processData.totalCapacity.ToString("F1"));
			sb.Append("\n");
			sb.Append(Local.ProcessPopup_EnabledCapacity); // "Enabled capacity"
			sb.Append(" : ");
			sb.Append((processData.totalCapacity * processData.maxSetting).ToString("F1"));
			sb.Append("\n");
			sb.Append(Local.ProcessPopup_CapacityUsed); // "Capacity used"
			sb.Append(" : ");
			sb.Append(resources[0].usage.ToString("P1"));
			statusBox.Text = sb.ToString();

			enableButton.Text = processData.enabled ? Local.Generic_ENABLED : Local.Generic_DISABLED;
		}

		private void OnToggle()
		{
			processData.SetEnabled(!processData.enabled, null);
		}

		private void OnCapacityTweak(float capacity)
		{
			processData.SetMaxSetting(capacity, null);
		}

		public class ResourceEntry : KsmGuiBase
		{
			ProcessPopup window;
			VesselResource resource;
			KsmGuiText resNameText;
			KsmGuiText resRateText;
			KsmGuiText resStatusText;
			KsmGuiTextButton resDumpText;
			string resName;
			double baseResRate;
			bool isInput;
			bool dump;
			bool dumpable;
			public double usage;

			public ResourceEntry(KsmGuiBase parent, ProcessPopup window, KeyValuePair<string, double> inputOrOutput, bool isInput) : base(parent)
			{
				this.isInput = isInput;
				this.window = window;
				resName = inputOrOutput.Key;
				baseResRate = isInput ? -inputOrOutput.Value : inputOrOutput.Value;
				resource = window.vd.ResHandler.GetResource(resName);
				dump = window.processData.dumpedOutputs.Contains(resName);
				dumpable = !isInput && window.processData.process.dumpable.Contains(resName);

				SetLayoutElement(true, false, -1, 16);
				this.AddImageComponentWithColor(KsmGuiStyle.boxColor);
				SetUpdateAction(Update);

				resNameText = new KsmGuiText(this, resource.Title, null, TextAlignmentOptions.Left, false, TextOverflowModes.Ellipsis);
				resNameText.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 5, 0);
				resNameText.TopTransform.SetSizeDelta(95, 16);

				resRateText = new KsmGuiText(this, "", null, TextAlignmentOptions.Right);
				resRateText.TextComponent.color = Lib.KolorToColor(isInput ? Lib.Kolor.NegRate : Lib.Kolor.PosRate);
				resRateText.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 105, 0);
				resRateText.TopTransform.SetSizeDelta(65, 16);

				resStatusText = new KsmGuiText(this, "", null, TextAlignmentOptions.Right);
				resStatusText.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 175, 0);
				resStatusText.TopTransform.SetSizeDelta(80, 16);

				if (dumpable)
				{
					resDumpText = new KsmGuiTextButton(this, "", null, null, TextAlignmentOptions.Right);
					resDumpText.TextComponent.color = Lib.KolorToColor(Lib.Kolor.Yellow);
					resDumpText.TextComponent.fontStyle = FontStyles.Bold;
					resDumpText.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 250, 0);
					resDumpText.TopTransform.SetSizeDelta(40, 16);
					resDumpText.SetButtonOnClick(OnToggleDump);
				}
			}

			private void Update()
			{
				resNameText.SetTooltipText(resource.BrokerListTooltipTMP());
				usage = baseResRate * window.processData.maxSetting * window.processData.totalCapacity;
				resRateText.Text = Lib.HumanReadableRate(usage, "F3", "", true);

				if (isInput && resource.AvailabilityFactor == 0.0)
				{
					resStatusText.Text = Local.Generic_notAvailable; //  "n/a";
					resStatusText.TextComponent.color = Lib.KolorToColor(Lib.Kolor.Red);
					usage = 0.0;
				}
				else if (!isInput && !dump && resource.ProduceRequests == 0.0 && (resource.Capacity == 0.0 || resource.Level == 1.0))
				{
					resStatusText.Text = Local.ProcessPopup_NoStorage; // "no storage";
					if(dumpable)
						resStatusText.TextComponent.color = Lib.KolorToColor(Lib.Kolor.Red);
					else
						resStatusText.TextComponent.color = Lib.KolorToColor(Lib.Kolor.Yellow);
					usage = 0.0;
				}
				else
				{
					bool found = false;
					foreach (ResourceBrokerRate brokerRate in resource.ResourceBrokers)
					{
						if (brokerRate.broker == window.processData.process.broker)
						{
							resStatusText.Text = Lib.HumanReadableRate(brokerRate.rate, "F3", "", true);
							usage = brokerRate.rate / usage;
							found = true;
							break;
						}
					}

					if (!found)
					{
						resStatusText.Text = "0.0";
						usage = 0.0;
					}

					if (isInput)
						resStatusText.TextComponent.color = Lib.KolorToColor(Lib.Kolor.NegRate);
					else
						resStatusText.TextComponent.color = Lib.KolorToColor(Lib.Kolor.PosRate);
				}

				if (dumpable)
				{
					dump = window.processData.dumpedOutputs.Contains(resName);
					resDumpText.Text = dump ? Local.Generic_YES : Local.Generic_NO;
				}

			}

			private void OnToggleDump()
			{
				bool removed = false;
				for (int i = window.processData.dumpedOutputs.Count - 1; i >= 0; i--)
				{
					if (window.processData.dumpedOutputs[i] == resName)
					{
						removed = true;
						window.processData.dumpedOutputs.RemoveAt(i);
					}
				}

				if (!removed)
					window.processData.dumpedOutputs.Add(resName);
			}
		}

		public class PartEntry : KsmGuiBase
		{
			KsmGuiText resCapText;
			KsmGuiTextButton resToggleText;
			PartProcessData data;
			VesselData vd;

			public PartEntry(KsmGuiBase parent, string partTitle, PartProcessData data, VesselData vd) : base(parent)
			{
				this.data = data;
				this.vd = vd;

				SetLayoutElement(true, false, -1, 16);
				this.AddImageComponentWithColor(KsmGuiStyle.boxColor);
				SetUpdateAction(Update);

				KsmGuiText resNameText = new KsmGuiText(this, partTitle, null, TextAlignmentOptions.Left, false, TextOverflowModes.Ellipsis);
				resNameText.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 5, 0);
				resNameText.TopTransform.SetSizeDelta(150, 16);

				resCapText = new KsmGuiText(this, data.processCapacity.ToString("F1"), null, TextAlignmentOptions.Center);
				resCapText.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 160, 0);
				resCapText.TopTransform.SetSizeDelta(55, 16);

				resToggleText = new KsmGuiTextButton(this, "", null, null, TextAlignmentOptions.Center);
				resToggleText.TextComponent.color = Lib.KolorToColor(Lib.Kolor.Yellow);
				resToggleText.TextComponent.fontStyle = FontStyles.Bold;
				resToggleText.TopTransform.SetAnchorsAndPosition(TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 230, 0);
				resToggleText.TopTransform.SetSizeDelta(60, 16);
				resToggleText.SetButtonOnClick(OnTogglePart);
			}

			private void Update()
			{
				if (data.isBroken)
				{
					resCapText.Text = Local.Generic_BROKEN;
					resCapText.TextComponent.color = Lib.KolorToColor(Lib.Kolor.Red);
					resToggleText.Enabled = false;
				}
				else
				{
					resCapText.Text = data.processCapacity.ToString("F1");
					resCapText.TextComponent.color = Color.white;
					resToggleText.Enabled = true;
					resToggleText.Text = data.isRunning ? Local.Generic_YES : Local.Generic_NO;
				}
			}

			private void OnTogglePart()
			{
				ModuleKsmProcessController.Toggle(data, vd.Vessel.loaded);
			}
		}
	}
}
