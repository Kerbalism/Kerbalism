using System;
using System.Collections.Generic;
using UnityEngine;



namespace KERBALISM
{

	public sealed class HardDrive : PartModule, IScienceDataContainer, ISpecifics, IModuleInfo, IPartMassModifier
	{
		[KSPField] public double dataCapacity = -1;             // drive capacity, in Mb. -1 = unlimited
		[KSPField] public int sampleCapacity = -1;              // drive capacity, in slots. -1 = unlimited
		[KSPField] public string title = "Kerbodyne ZeroBit";   // drive name to be displayed in file manager
		[KSPField] public string experiment_id = string.Empty;  // if set, restricts write access to the experiment on the same part, with the given experiment_id.

		[KSPField(isPersistant = true)] public uint hdId = 0;

		[KSPField(guiActive = true, guiName = "Capacity", guiActiveEditor = true)] public string Capacity;

		private Drive drive;
		private double totalSampleMass;

		public override void OnStart(StartState state)
		{
			// don't break tutorial scenarios
			if (Lib.DisableScenario(this)) return;

			if (hdId == 0) hdId = part.flightID;

			if(drive == null)
			{
				if (!Lib.IsFlight())
					drive = new Drive(title, dataCapacity, sampleCapacity);
				else
					drive = DB.Vessel(vessel).DriveForPart(title, hdId, dataCapacity, sampleCapacity);
			}

			drive.is_private |= experiment_id.Length > 0;
			UpdateCapacity();
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);

			if (HighLogic.LoadedScene == GameScenes.LOADING)
			{
				drive = new Drive();
				return;
			}

			if (!Lib.IsFlight())
				drive = new Drive();
			else
				drive = DB.Vessel(vessel).DriveForPart(title, hdId, dataCapacity, sampleCapacity);

			drive.is_private |= experiment_id.Length > 0;
			UpdateCapacity();
		}

		public void SetDrive(Drive drive)
		{
			this.drive = drive;
			drive.is_private |= experiment_id.Length > 0;
			UpdateCapacity();
		}

		public void FixedUpdate()
		{
			UpdateCapacity();
		}

		public void Update()
		{
			if (Lib.IsFlight())
			{
				// show DATA UI button, with size info
				Events["ToggleUI"].guiName = Lib.StatusToggle("Data", drive.Empty() ? "empty" : drive.Size());
				Events["ToggleUI"].active = !IsPrivate();

				// show TakeData eva action button, if there is something to take
				Events["TakeData"].active = !drive.Empty();

				// show StoreData eva action button, if active vessel is an eva kerbal and there is something to store from it
				Vessel v = FlightGlobals.ActiveVessel;
				Events["StoreData"].active = !IsPrivate() && v != null && v.isEVA && !EVA.IsDead(v);

				// hide TransferLocation button
				var transferVisible = !IsPrivate();
				if(transferVisible)
				{
					int count = 0;
					foreach (var d in DB.Vessel(vessel).drives.Values){
						if (!d.is_private) count++;
						if (count > 1) break;
					}
					transferVisible = count > 1;
				}
				Events["TransferData"].active = transferVisible;
				Events["TransferData"].guiActive = transferVisible;
			}
		}

		public bool IsPrivate()
		{
			return drive.is_private;
		}

		private void UpdateCapacity()
		{
			double mass = 0;
			foreach (var sample in drive.samples.Values) mass += sample.mass;
			totalSampleMass = mass;

			if (dataCapacity < 0 || sampleCapacity < 0 || IsPrivate())
			{
				Fields["Capacity"].guiActive = false;
				Fields["Capacity"].guiActiveEditor = false;
				return;
			}

			double availableDataCapacity = dataCapacity;
			int availableSlots = sampleCapacity;

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


		[KSPEvent(guiName = "#KERBALISM_HardDrive_TransferData", active = false)]
		public void TransferData()
		{
			var hardDrives = vessel.FindPartModulesImplementing<HardDrive>();
			foreach(var hardDrive in hardDrives)
			{
				if (hardDrive == this) continue;
				hardDrive.drive.Move(drive, PreferencesScience.Instance.sampleTransfer);
			}
		}


		[KSPEvent(guiActive = false, guiActiveUnfocused = true, guiActiveUncommand = true, guiName = "#KERBALISM_HardDrive_TakeData", active = true)]
		public void TakeData()
		{
			// disable for dead eva kerbals
			Vessel v = FlightGlobals.ActiveVessel;
			if (v == null || EVA.IsDead(v)) return;

			// transfer data
			Drive.Transfer(vessel, v, v.isEVA || PreferencesScience.Instance.sampleTransfer);
		}


		[KSPEvent(guiActive = false, guiActiveUnfocused = true, guiActiveUncommand = true, guiName = "#KERBALISM_HardDrive_TransferData", active = true)]
		public void StoreData()
		{
			// disable for dead eva kerbals
			Vessel v = FlightGlobals.ActiveVessel;
			if (v == null || EVA.IsDead(v)) return;

			// transfer data
			Drive.Transfer(v, vessel, v.isEVA || PreferencesScience.Instance.sampleTransfer);
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
				var exp = Science.Experiment(pair.Key);
				data.Add(new ScienceData((float)file.size, 1.0f, 1.0f, pair.Key, exp.FullName(pair.Key)));
			}
			foreach (var pair in drive.samples)
			{
				Sample sample = pair.Value;
				var exp = Science.Experiment(pair.Key);
				data.Add(new ScienceData((float)sample.size, 0.0f, 0.0f, pair.Key, exp.FullName(pair.Key)));
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
			specs.Add("File capacity", dataCapacity >= 0 ? Lib.HumanReadableDataSize(dataCapacity) : "unlimited");
			specs.Add("Sample capacity", sampleCapacity >= 0 ? Lib.HumanReadableSampleSize(sampleCapacity) : "unlimited");
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


