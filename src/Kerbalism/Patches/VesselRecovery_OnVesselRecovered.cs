using System;
using System.Reflection;
using System.Collections.Generic;
using Harmony;
using Harmony.ILCopying;
using UnityEngine;
using KSP.Localization;
using KSP.UI.Screens.SpaceCenter.MissionSummaryDialog;
using KSP.UI.Screens;
using TMPro;

namespace KERBALISM
{
	/* With ROC (Serenity), the contracts to gather some stone from an other body are broken
	 * because Kerbalism converts that into it's own science data.
	 *
	 * No matter from what module (ours/ stock / mods) the data is generated, it's always
	 * stored in our drives as we remove all ModuleScienceContainer modules.
	 * Unless the mod that search for the data is doing it while the vessel is loaded
	 * AND it's using IScienceDataContainer (we implement it in harddrive), it won't find anaything.
	 *
	 * Since vessel recovery has to be done on the protovessel, the only way for stock/mods is
	 * by searching for the ModuleScienceContainer protovalues.
	 *
	 * This works because any module can store data, the recovery code seems to be searching in
	 * all protomodules for "ScienceData" nodes. We can expect other mods or stock features to
	 * act the same way.
	 *
	 * This will let the stock science data recovery happen, and anything that expect it to be
	 * here in onVesselRecoveryProcessing should regain compatibility.
	 * 
	 * There are other part of stock / other mods that might be expecting to find the stock science
	 * data on vessel recovery, so we implement this global fix.
	 *
	 * onVesselRecoveryProcessing seems to be fired from only in VesselRecovery.OnVesselRecovered(),
	 * which is a callback added to onVesselRecovered. So we use a harmony Prefix method attached
	 * to VesselRecovery.OnVesselRecovered() that restores our proprietary data to what stock KSP
	 * and mods expect to see at this point.
	 *
	 * See https://github.com/Kerbalism/Kerbalism/issues/476
	 * See https://github.com/Kerbalism/Kerbalism/issues/249
	 *
	 * Further note :
	 * We don't want our experiments data to be credited through the stock system, because it has this
	 * formula that degrade the value when a subject is partially completed.
	 * So keep track of by what the data was created in the `bool useStockCrediting` of files/samples
	 */

	public struct RecoveryWidgetData
	{
		public ScienceSubject subject;
		public float scienceAdded;
		public string dataText;
		public string valueText;
		public string scienceText;

		public RecoveryWidgetData(ScienceSubject subject, float scienceAdded, string dataText, string valueText, string scienceText)
		{
			this.subject = subject;
			this.scienceAdded = scienceAdded;
			this.dataText = dataText;
			this.valueText = valueText;
			this.scienceText = scienceText;
		}
	}

	[HarmonyPatch(typeof(VesselRecovery))]
	[HarmonyPatch("OnVesselRecovered")]
	class VesselRecovery_OnVesselRecovered
	{
		private static List<RecoveryWidgetData> recoveryScienceWidgets = new List<RecoveryWidgetData>();

		static void Prefix(ProtoVessel pv, bool quick)
		{
			recoveryScienceWidgets.Clear();

			if (pv == null)
				return;

			// get a hard drive. any hard drive will do, no need to find a specific one.
			ProtoPartModuleSnapshot protoHardDrive = null;
			foreach (var p in pv.protoPartSnapshots)
			{
				foreach (var pm in Lib.FindModules(p, nameof(ModuleKsmDrive)))
				{
					protoHardDrive = pm;
					break;
				}
				if (protoHardDrive != null)
					break;
			}

			if (protoHardDrive == null)
				return;

			// trick the stock science crediting system in thinking this is a stock science container partmodule,
			// even tough this is our ModuleKsmDrive
			protoHardDrive.moduleName = "ModuleScienceContainer";

			double scienceToCredit = 0.0;

			foreach (DriveData drive in DriveData.GetDrives(pv, true))
			{
				foreach (File file in drive.files.Values)
				{
					double subjectValue = file.subjectData.ScienceValue(file.size);
					file.subjectData.RemoveScienceCollectedInFlight(subjectValue);

					if (file.useStockCrediting)
					{
						file.ConvertToStockData().Save(protoHardDrive.moduleValues.AddNode("ScienceData"));
						file.subjectData.SetAsPersistent();
					}
					else
					{
						double scienceCredited = file.subjectData.RetrieveScience(subjectValue, false, pv);
						scienceToCredit += scienceCredited;

						// stock recovery dialog is shown only if quick is false
						if (!quick)
						{
							RecoveryWidgetData entry = new RecoveryWidgetData(
								file.subjectData.RnDSubject,
								(float)scienceCredited,
								Lib.BuildString(file.subjectData.ScienceMaxValue.ToString("F1"), " ", "subject value"),
								Lib.BuildString(file.subjectData.ScienceRetrievedInKSC.ToString("F1"), " ", "in RnD"),
								Lib.BuildString(scienceCredited.ToString("+0.0"), " ", "earned"));

							recoveryScienceWidgets.Add(entry);
						}
					}
				}

				foreach (Sample sample in drive.samples.Values)
				{
					double subjectValue = sample.subjectData.ScienceValue(sample.size);
					sample.subjectData.RemoveScienceCollectedInFlight(subjectValue);

					if (sample.useStockCrediting)
					{
						sample.ConvertToStockData().Save(protoHardDrive.moduleValues.AddNode("ScienceData"));
						sample.subjectData.SetAsPersistent();
					}
					else
					{
						double scienceCredited = sample.subjectData.RetrieveScience(subjectValue, false, pv);
						scienceToCredit += scienceCredited;

						// stock recovery dialog is shown only if quick is false
						if (!quick)
						{
							RecoveryWidgetData entry = new RecoveryWidgetData(
								sample.subjectData.RnDSubject,
								(float)scienceCredited,
								Lib.BuildString(sample.subjectData.ScienceMaxValue.ToString("F1"), " ", "subject value"),
								Lib.BuildString(sample.subjectData.ScienceRetrievedInKSC.ToString("F1"), " ", "in RnD"),
								Lib.BuildString(scienceCredited.ToString("+0.0"), " ", "earned"));

							recoveryScienceWidgets.Add(entry);
						}
					}
				}
			}
		}

		// this is called by GameEvents.onVesselRecoveryProcessingComplete from callbacks,
		// near the end of VesselRecovery.OnVesselRecovered(). We don't use a postfix because
		// the MissionRecoveryDialog object is local to VesselRecovery.OnVesselRecovered() and
		// there is no easy accessible reference other than searching the gameobject hierarchy.
		public static void OnVesselRecoveryProcessingComplete(MissionRecoveryDialog dialog)
		{
			MissionRecoveryDialog_updateScienceWindowContent.widgetData.Clear();

			if (dialog == null)
			{
				recoveryScienceWidgets.Clear();
				return;
			}

			float kerbalismScienceEarned = 0f;
			foreach (RecoveryWidgetData entry in recoveryScienceWidgets)
			{
				ScienceSubjectWidget widget = ScienceSubjectWidget.Create(entry.subject, 1f, entry.scienceAdded, dialog);
				dialog.AddDataWidget(widget);

				MissionRecoveryDialog_updateScienceWindowContent.widgetData.Add(widget, entry);

				widget.scienceWidgetDataContent.sprite = widget.scienceWidgetScienceContent.sprite;
				TextMeshProUGUI dataTextComponent = Lib.ReflectionValue<TextMeshProUGUI>(widget.scienceWidgetDataContent, "textComponent");
				dataTextComponent.color = Lib.KolorToColor(Lib.Kolor.Science);

				widget.scienceWidgetValueContent.sprite = widget.scienceWidgetScienceContent.sprite;
				TextMeshProUGUI valueTextComponent = Lib.ReflectionValue<TextMeshProUGUI>(widget.scienceWidgetValueContent, "textComponent");
				valueTextComponent.color = Lib.KolorToColor(Lib.Kolor.Science);


				dialog.scienceEarned += entry.scienceAdded;
				kerbalismScienceEarned += entry.scienceAdded;
			}

			dialog.totalScience += kerbalismScienceEarned;

			recoveryScienceWidgets.Clear();
		}
	}

	// we can't set the widget text in the GameEvents.onVesselRecoveryProcessingComplete callback
	// because it is set later in MissionRecoveryDialog.updateScienceWindowContent().
	[HarmonyPatch(typeof(MissionRecoveryDialog))]
	[HarmonyPatch("updateScienceWindowContent")]
	class MissionRecoveryDialog_updateScienceWindowContent
	{
		public static Dictionary<ScienceSubjectWidget, RecoveryWidgetData> widgetData = new Dictionary<ScienceSubjectWidget, RecoveryWidgetData>();

		static void Postfix(List<ScienceSubjectWidget> ___scienceWidgets)
		{
			foreach (ScienceSubjectWidget widget in ___scienceWidgets)
			{
				if (widgetData.TryGetValue(widget, out RecoveryWidgetData data))
				{
					widget.scienceWidgetDataContent.text = data.dataText;
					widget.scienceWidgetValueContent.text = data.valueText;
					widget.scienceWidgetScienceContent.text = data.scienceText;
				}
			}
			widgetData.Clear();
		}
	}
}
