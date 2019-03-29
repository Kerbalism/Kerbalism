using System;
using System.Collections.Generic;
using UnityEngine;



namespace KERBALISM
{

	public sealed class HardDrive : PartModule, IScienceDataContainer, ISpecifics, IModuleInfo, IPartMassModifier
	{
		[KSPField] public double dataCapacity = 102400.0;       // drive capacity, in Mb
		[KSPField] public double sampleCapacity = 102400.0;     // drive capacity, in Mb

		[KSPField(guiActive = true, guiName = "Capacity", guiActiveEditor = true)] public string Capacity;

		private Drive drive;
		private double totalSampleMass;

		public override void OnStart(StartState state)
		{
			// don't break tutorial scenarios
			if (Lib.DisableScenario(this)) return;

			if(drive == null)
			{
				if (Lib.IsEditor())
					drive = new Drive(part.name, dataCapacity, Lib.SampleSizeToSlots(sampleCapacity));
				else
					drive = DB.Vessel(vessel).DriveForPart(part, dataCapacity, Lib.SampleSizeToSlots(sampleCapacity));
			}

			UpdateCapacity();
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);

			if(Lib.IsEditor())
				drive = new Drive();
			else
				drive = DB.Vessel(vessel).DriveForPart(part, dataCapacity, Lib.SampleSizeToSlots(sampleCapacity));

			UpdateCapacity();
		}

		public void Update()
		{
			UpdateCapacity();

			if (Lib.IsFlight())
			{
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

		private void UpdateCapacity()
		{
			totalSampleMass = 0;
			foreach (var sample in drive.samples.Values) totalSampleMass += sample.mass;

			double availableDataCapacity = dataCapacity;
			int availableSlots = Lib.SampleSizeToSlots(sampleCapacity);
			if (Lib.IsFlight())
			{
				availableDataCapacity = drive.FileCapacityAvailable();
				availableSlots = Lib.SampleSizeToSlots(drive.SampleCapacityAvailable());
			}

			Capacity = string.Empty;
			if(availableDataCapacity > double.Epsilon)
				Capacity = Lib.HumanReadableDataSize(availableDataCapacity);
			if(availableSlots > 0)
			{
				if (Capacity.Length > 0) Capacity += " ";
				Capacity += Lib.HumanReadableSampleSize(availableSlots);
			}

			if(Lib.IsFlight() && totalSampleMass > double.Epsilon)
			{
				Capacity += " " + Lib.HumanReadableMass(totalSampleMass);
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
			return Specs().Info();
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
				var experimentInfo = Science.Experiment(data.subjectID);
				var sampleMass = Science.GetSampleMass(data.subjectID);
				var mass = sampleMass / experimentInfo.max_amount * data.dataAmount;

				result = drive.Record_sample(data.subjectID, data.dataAmount, mass);
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

		// module mass support
		public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) { return (float)totalSampleMass; }
		public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
	}


} // KERBALISM


