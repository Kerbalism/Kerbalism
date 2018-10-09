using System;
using System.Collections.Generic;
using UnityEngine;



namespace KERBALISM
{


	public sealed class HardDrive : PartModule, IScienceDataContainer
	{
		public override void OnStart(StartState state)
		{
			// don't break tutorial scenarios
			if (Lib.DisableScenario(this)) return;
		}

		public void Update()
		{
			if (Lib.IsFlight())
			{
				Drive drive = DB.Vessel(vessel).drive;

				// if no location was ever specified, set it here
				if (drive.location == 0) drive.location = part.flightID;

				// if this is the location the data is stored
				if (drive.location == part.flightID)
				{
					// get data size
					double size = drive.Size();

					// show DATA UI button, with size info
					Events["ToggleUI"].guiName = Lib.StatusToggle("Data", size > double.Epsilon ? Lib.HumanReadableDataSize(size) : "empty");
					Events["ToggleUI"].active = true;

					// show TakeData eva action button, if there is something to take
					Events["TakeData"].active = size > double.Epsilon;

					// show StoreData eva action button, if active vessel is an eva kerbal and there is something to store from it
					Vessel v = FlightGlobals.ActiveVessel;
					Events["StoreData"].active = v != null && v.isEVA && !EVA.IsDead(v) && DB.Vessel(v).drive.Size() > double.Epsilon;

					// hide TransferLocation button
					Events["TransferData"].active = false;
				}
				// if this is not the location the data is stored
				else
				{
					// hide DATA UI button
					Events["ToggleUI"].active = false;

					// hide EVA actions
					Events["TakeData"].active = false;
					Events["StoreData"].active = false;

					// show TransferData button
					Events["TransferData"].active = true;
				}
			}
		}


		[KSPEvent(guiActive = true, guiName = "_", active = true)]
		public void ToggleUI()
		{
			UI.Open((Panel p) => p.Fileman(vessel));
		}


		[KSPEvent(guiActive = true, guiName = "#KERBALISM_HardDrive_TransferData", active = false)]
		public void TransferData()
		{
			DB.Vessel(vessel).drive.location = part.flightID;
		}


		[KSPEvent(guiActive = false, guiActiveUnfocused = true, guiActiveUncommand = true, guiName = "#KERBALISM_HardDrive_TakeData", active = true)]
		public void TakeData()
		{
			// disable for dead eva kerbals
			Vessel v = FlightGlobals.ActiveVessel;
			if (v == null || EVA.IsDead(v)) return;

			// transfer data
			Drive.Transfer(vessel, v);
		}


		[KSPEvent(guiActive = false, guiActiveUnfocused = true, guiActiveUncommand = true, guiName = "#KERBALISM_HardDrive_TransferData", active = true)]
		public void StoreData()
		{
			// disable for dead eva kerbals
			Vessel v = FlightGlobals.ActiveVessel;
			if (v == null || EVA.IsDead(v)) return;

			// transfer data
			Drive.Transfer(v, vessel);
		}


		// part tooltip
		public override string GetInfo()
		{
			return "Solid-state hard drive";
		}


		// science container implementation
		public ScienceData[] GetData()
		{
			// get drive
			Drive drive = DB.Vessel(vessel).drive;

			// if not the preferred drive
			if (drive.location != part.flightID) return new ScienceData[0];

			// generate and return stock science data
			List<ScienceData> data = new List<ScienceData>();
			foreach (var pair in drive.files)
			{
				File file = pair.Value;
				data.Add(new ScienceData((float)file.size, 1.0f, 1.0f, pair.Key, Science.Experiment(pair.Key).fullname));
			}
			foreach (var pair in drive.samples)
			{
				Sample sample = pair.Value;
				data.Add(new ScienceData((float)sample.size, 0.0f, 0.0f, pair.Key, Science.Experiment(pair.Key).fullname));
			}
			return data.ToArray();
		}

		public void ReturnData(ScienceData data)
		{
			// get drive
			Drive drive = DB.Vessel(vessel).drive;

			// if not the preferred drive
			if (drive.location != part.flightID) return;

			// store the data
			if (data.baseTransmitValue > float.Epsilon || data.transmitBonus > double.Epsilon)
			{
				drive.Record_file(data.subjectID, data.dataAmount);
			}
			else
			{
				drive.Record_sample(data.subjectID, data.dataAmount);
			}
		}

		public void DumpData(ScienceData data)
		{
			// get drive
			Drive drive = DB.Vessel(vessel).drive;

			// if not the preferred drive
			if (drive.location != part.flightID) return;

			// remove the data
			if (data.baseTransmitValue > float.Epsilon || data.transmitBonus > double.Epsilon)
			{
				drive.Delete_file(data.subjectID, data.dataAmount);
			}
			else
			{
				drive.Delete_sample(data.subjectID, data.dataAmount);
			}
		}

		public void ReviewData()
		{
			UI.Open((p) => p.Fileman(vessel));
		}

		public void ReviewDataItem(ScienceData data)
		{
			ReviewData();
		}

		public int GetScienceCount()
		{
			// We are forced to return zero, or else EVA kerbals re-entering a pod
			// will complain about being unable to store the data (but they shouldn't)
			return 0;

			/*Drive drive = DB.Vessel(vessel).drive;

			// if not the preferred drive
			if (drive.location != part.flightID) return 0;

			// return number of entries
			return drive.files.Count + drive.samples.Count;*/
		}

		public bool IsRerunnable()
		{
			// don't care
			return false;
		}

		public override string GetModuleDisplayName() { return "Hard Drive"; }
	}


} // KERBALISM


