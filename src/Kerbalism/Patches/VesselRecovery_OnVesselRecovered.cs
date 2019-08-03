using System;
using System.Reflection;
using System.Collections.Generic;
using Harmony;
using Harmony.ILCopying;

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

			foreach (Drive drive in Drive.GetDrives(pv))
			{
				ScienceData[] sd = HardDrive.GetData(drive);
				foreach(ScienceData d in sd)
				{
					d.Save(protoHardDrive.moduleValues.AddNode("ScienceData"));
				}
			}

			return true; // continue to the original code
		}
	}
}
