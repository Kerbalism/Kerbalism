using System;
using System.Reflection;
using System.Collections.Generic;
using Harmony;
using Harmony.ILCopying;
using UnityEngine;
using KSP.Localization;

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
	[HarmonyPatch(typeof(VesselRecovery))]
	[HarmonyPatch("OnVesselRecovered")]
	class VesselRecovery_OnVesselRecovered {
		static bool Prefix(ref ProtoVessel pv, ref bool quick) {

			if (pv == null) return true;

			// get a hard drive. any hard drive will do, no need to find a specific one.
			ProtoPartModuleSnapshot protoHardDrive = null;
			foreach (var p in pv.protoPartSnapshots)
			{
				foreach (var pm in Lib.FindModules(p, "HardDrive"))
				{
					protoHardDrive = pm;
					break;
				}
				if (protoHardDrive != null)
					break;
			}

			if (protoHardDrive == null)
				return true; // no drive on the vessel - nothing to do.

			double scienceToCredit = 0.0;

			List<DialogGUIBase> labels = new List<DialogGUIBase>();

			foreach (Drive drive in Drive.GetDrives(pv, true))
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
						scienceToCredit += file.subjectData.RetrieveScience(subjectValue, false, pv);

						labels.Add(new DialogGUILabel(Lib.BuildString(
							Lib.Color("+ " + subjectValue.ToString("F1"), Lib.Kolor.Science),
							" (",
							Lib.Color(file.subjectData.ScienceRetrievedInKSC.ToString("F1"), Lib.Kolor.Science, true),
							" / ",
							Lib.Color(file.subjectData.ScienceMaxValue.ToString("F1"), Lib.Kolor.Science, true),
							") : ",
							file.subjectData.FullTitle
							)));
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
						scienceToCredit += sample.subjectData.RetrieveScience(subjectValue, false, pv);

						labels.Add(new DialogGUILabel(Lib.BuildString(
							Lib.Color("+ " + subjectValue.ToString("F1"), Lib.Kolor.Science),
							" (",
							Lib.Color(sample.subjectData.ScienceRetrievedInKSC.ToString("F1"), Lib.Kolor.Science, true),
							" / ",
							Lib.Color(sample.subjectData.ScienceMaxValue.ToString("F1"), Lib.Kolor.Science, true),
							") : ",
							sample.subjectData.FullTitle
							)));
					}
				}
			}

			protoHardDrive.moduleName = "ModuleScienceContainer";

			if (scienceToCredit > 0.0)
			{
				// ideally we should hack the stock dialog to add the little science widgets to it but I'm lazy
				// plus it looks like crap anyway
				PopupDialog.SpawnPopupDialog
				(
					new MultiOptionDialog
					(
						"scienceResults", "", pv.vesselName + " "+Local.VesselRecovery_title, HighLogic.UISkin, new Rect(0.3f, 0.5f, 350f, 100f),//" recovery"
						new DialogGUIVerticalLayout
						(
							new DialogGUIBox(Local.VesselRecovery_info + " : " + Lib.Color(scienceToCredit.ToString("F1") + " " + Local.VesselRecovery_CREDITS, Lib.Kolor.Science, true), 340f, 30f),//"SCIENCE RECOVERED"" CREDITS"
							new DialogGUIScrollList
							(
								new Vector2(340f, 250f), false, true,
								new DialogGUIVerticalLayout(labels.ToArray())
							),
							new DialogGUIButton(Local.VesselRecovery_OKbutton, null, 340f, 30f, true, HighLogic.UISkin.button)//"OK"
						)
					),
					false, HighLogic.UISkin
				);
			}

			return true; // continue to the original code
		}
	}
}
