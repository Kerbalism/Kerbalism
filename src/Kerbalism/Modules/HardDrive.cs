using System;
using System.Collections.Generic;
using UnityEngine;



namespace KERBALISM
{

	public sealed class HardDrive : PartModule, IScienceDataContainer, ISpecifics, IModuleInfo
	{
		[KSPField] public double dataCapacity = 2000.0;       // drive capacity, in Mb
		[KSPField] public int sampleCapacity = 10;            // drive capacity, in slots

		// show abundance level
		[KSPField(guiActive = false, guiName = "_")] public string Capacity;

		private Drive drive;

		public override void OnStart(StartState state)
		{
			// don't break tutorial scenarios
			if (Lib.DisableScenario(this)) return;

			if(drive == null)
			{
				drive = new Drive();
				drive.sampleCapacity = sampleCapacity;
				drive.dataCapacity = dataCapacity;
			}
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
			drive = new Drive(node.GetNode("drive"));
		}

		public override void OnSave(ConfigNode node)
		{
			base.OnSave(node);
			drive.Save(node.AddNode("drive"));
		}

		public void Update()
		{
			if (Lib.IsFlight())
			{
				drive.dataCapacity = dataCapacity;
				drive.sampleCapacity = sampleCapacity;

				Fields["Capacity"].guiName = "Storage";
				Fields["Capacity"].guiActive = true;
				Capacity = Lib.BuildString("Data: ", Lib.HumanReadableDataSize(drive.FileCapacityAvailable()),
				                           " Slots: ", Lib.HumanReadableDataSize(drive.SampleCapacityAvailable()));

				// show DATA UI button, with size info
				Events["ToggleUI"].guiName = Lib.StatusToggle("Data", drive.Empty() ? "empty" : drive.Size());
				Events["ToggleUI"].active = true;

				// show TakeData eva action button, if there is something to take
				Events["TakeData"].active = !drive.Empty();

				// show StoreData eva action button, if active vessel is an eva kerbal and there is something to store from it
				Vessel v = FlightGlobals.ActiveVessel;
				Events["StoreData"].active = v != null && v.isEVA && !EVA.IsDead(v) && drive.FilesSize() > double.Epsilon;

				// hide TransferLocation button
				Events["TransferData"].active = true;
			}
		}

		public Drive GetDrive()
		{
			return drive;
		}

		[KSPEvent(guiActive = true, guiName = "_", active = true)]
		public void ToggleUI()
		{
			UI.Open((Panel p) => p.Fileman(vessel));
		}


		[KSPEvent(guiActive = true, guiName = "#KERBALISM_HardDrive_TransferData", active = false)]
		public void TransferData()
		{
			var hardDrives = vessel.FindPartModulesImplementing<HardDrive>();
			foreach(var hardDrive in hardDrives)
			{
				if (hardDrive == this) continue;
				hardDrive.drive.Move(drive);
			}
		}


		[KSPEvent(guiActive = false, guiActiveUnfocused = true, guiActiveUncommand = true, guiName = "#KERBALISM_HardDrive_TakeData", active = true)]
		public void TakeData()
		{
			// disable for dead eva kerbals
			Vessel v = FlightGlobals.ActiveVessel;
			if (v == null || EVA.IsDead(v)) return;

			// transfer data
			Drive.Transfer(vessel, v, v.isEVA);
		}


		[KSPEvent(guiActive = false, guiActiveUnfocused = true, guiActiveUncommand = true, guiName = "#KERBALISM_HardDrive_TransferData", active = true)]
		public void StoreData()
		{
			// disable for dead eva kerbals
			Vessel v = FlightGlobals.ActiveVessel;
			if (v == null || EVA.IsDead(v)) return;

			// transfer data
			Drive.Transfer(v, vessel, v.isEVA);
		}


		// part tooltip
		public override string GetInfo()
		{
			return "Solid-state hard drive";
		}


		// science container implementation
		public ScienceData[] GetData()
		{
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

		// TODO do something about limited capacity...
		// EVAs returning should get a warning if needed
		public void ReturnData(ScienceData data)
		{
			// store the data
			bool result = false;
			if (data.baseTransmitValue > float.Epsilon || data.transmitBonus > double.Epsilon)
			{
				result = drive.Record_file(data.subjectID, data.dataAmount);
			}
			else
			{
				result = drive.Record_sample(data.subjectID, data.dataAmount);
			}
		}

		public void DumpData(ScienceData data)
		{
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

		//public override string GetModuleDisplayName() { return "Hard Drive"; }

		// specifics support
		public Specifics Specs()
		{
			Specifics specs = new Specifics();
			specs.Add("File capacity", Lib.HumanReadableDataSize(dataCapacity));
			specs.Add("Sample capacity", Lib.HumanReadableDataSize(sampleCapacity));
			return specs;
		}

		// module info support
		public string GetModuleTitle() { return "Hard Drive"; }
		public override string GetModuleDisplayName() { return "Hard Drive"; }
		public string GetPrimaryField() { return string.Empty; }
		public Callback<Rect> GetDrawModulePanelCallback() { return null; }

	}


} // KERBALISM


