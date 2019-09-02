using System;
using System.Collections.Generic;
using UnityEngine;



namespace KERBALISM
{

	public class HardDrive : PartModule, IScienceDataContainer, ISpecifics, IModuleInfo, IPartMassModifier, IPartCostModifier
	{
		[KSPField] public double dataCapacity = -1;             // base drive capacity, in Mb. -1 = unlimited
		[KSPField] public double effectiveDataCapacity = -1;    // effective drive capacity, in Mb. -1 = unlimited
		[KSPField] public int sampleCapacity = -1;              // base drive capacity, in slots. -1 = unlimited
		[KSPField] public int effectiveSampleCapacity = -1;     // effective drive capacity, in slots. -1 = unlimited
		[KSPField] public string title = "Kerbodyne ZeroBit";   // drive name to be displayed in file manager
		[KSPField] public string experiment_id = string.Empty;  // if set, restricts write access to the experiment on the same part, with the given experiment_id.

		[KSPField] public int maxDataCapacityFactor = 4;        // how much additional data capacity to allow in editor
		[KSPField] public int maxSampleCapacityFactor = 4;      // how much additional data capacity to allow in editor

		[KSPField] public float dataCapacityCost = 0;           // added part cost per data capacity
		[KSPField] public float dataCapacityMass = 0;           // added part mass per data capacity
		[KSPField] public float sampleCapacityCost = 0;         // added part cost per sample capacity
		[KSPField] public float sampleCapacityMass = 0;         // added part mass per sample capacity

		[KSPField(isPersistant = true)] public uint hdId = 0;


		[KSPField(isPersistant = false, guiName = "Data Capacity", guiActive = false, guiActiveEditor = false), UI_ChooseOption(scene = UI_Scene.Editor)]
		public string dataCapacityUI = "0";
		[KSPField(isPersistant = false, guiName = "Sample Capacity", guiActive = false, guiActiveEditor = false), UI_ChooseOption(scene = UI_Scene.Editor)]
		public string sampleCapacityUI = "0";

		[KSPField(guiActive = true, guiName = "Capacity", guiActiveEditor = true)] public string Capacity;

		private Drive drive;
		private double totalSampleMass;

		List<KeyValuePair<string, double>> dataCapacities = null;
		List<KeyValuePair<string, int>> sampleCapacities = null;

		public override void OnStart(StartState state)
		{
			// don't break tutorial scenarios
			if (Lib.DisableScenario(this)) return;

			if (Lib.IsEditor())
			{
				effectiveDataCapacity = dataCapacity;
				effectiveSampleCapacity = sampleCapacity;

				if(dataCapacity > 0 && maxDataCapacityFactor > 0)
				{
					Fields["dataCapacityUI"].guiActiveEditor = true;
					var o =(UI_ChooseOption)Fields["dataCapacityUI"].uiControlEditor;

					dataCapacities = GetDataCapacitySizes();
					dataCapacityUI = dataCapacities[0].Key;
					effectiveDataCapacity = dataCapacities[0].Value;

					string[] dataOptions = new string[dataCapacities.Count];
					for(int i = 0; i < dataCapacities.Count; i++)
						dataOptions[i] = Lib.HumanReadableDataSize(dataCapacities[i].Value);
					o.options = dataOptions;
				}

				if (sampleCapacity > 0 && maxSampleCapacityFactor > 0)
				{
					Fields["sampleCapacityUI"].guiActiveEditor = true;
					var o = (UI_ChooseOption)Fields["sampleCapacityUI"].uiControlEditor;

					sampleCapacities = GetSampleCapacitySizes();
					sampleCapacityUI = sampleCapacities[0].Key;
					effectiveSampleCapacity = sampleCapacities[0].Value;

					string[] sampleOptions = new string[sampleCapacities.Count];
					for (int i = 0; i < sampleCapacities.Count; i++)
						sampleOptions[i] = Lib.HumanReadableSampleSize(sampleCapacities[i].Value);
					o.options = sampleOptions;
				}
			}

			if (Lib.IsFlight() && hdId == 0) hdId = part.flightID;
			if (drive == null)
			{
				if (!Lib.IsFlight())
					drive = new Drive(title, effectiveDataCapacity, effectiveSampleCapacity);
				else
					drive = DB.Drive(hdId, title, effectiveDataCapacity, effectiveSampleCapacity);
			}


			if (vessel != null) Cache.RemoveVesselObjectsCache(vessel, "drives");
			drive.is_private |= experiment_id.Length > 0;

			UpdateCapacity();
		}

		protected List<KeyValuePair<string, double>> GetDataCapacitySizes()
		{
			List<KeyValuePair<string, double>> result = new List<KeyValuePair<string, double>>();
			for(var i = 1; i <= maxDataCapacityFactor; i++)
				result.Add(new KeyValuePair<string, double>(Lib.HumanReadableDataSize(dataCapacity * i), dataCapacity * i));
			return result;
		}

		protected List<KeyValuePair<string, int>> GetSampleCapacitySizes()
		{
			List<KeyValuePair<string, int>> result = new List<KeyValuePair<string, int>>();
			for (var i = 1; i <= maxSampleCapacityFactor; i++)
				result.Add(new KeyValuePair<string, int>(Lib.HumanReadableSampleSize(sampleCapacity * i), sampleCapacity * i));
			return result;
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);

			if (HighLogic.LoadedScene == GameScenes.LOADING)
			{
				drive = new Drive();
				return;
			}
		}

		/// <summary>Called by Callbacks just after rollout to launch pad</summary>
		public void OnRollout()
		{
			if (Lib.DisableScenario(this)) return;

			// register the drive in the kerbalism DB
			// this needs to be done only once just after launch
			hdId = part.flightID;
			drive = DB.Drive(hdId, title, effectiveDataCapacity, effectiveSampleCapacity);
			drive.is_private = experiment_id.Length > 0;

			UpdateCapacity();

			if (vessel != null) Cache.RemoveVesselObjectsCache(vessel, "drives");
		}

		public void FixedUpdate()
		{
			UpdateCapacity();
		}

		public void Update()
		{
			if (drive == null)
				return;

			if (Lib.IsEditor())
			{
				if(dataCapacities != null)
				{
					foreach(var c in dataCapacities)
						if (c.Key == dataCapacityUI) effectiveDataCapacity = c.Value;
				}

				if (sampleCapacities != null)
				{
					foreach (var c in sampleCapacities)
						if (c.Key == sampleCapacityUI) effectiveSampleCapacity = c.Value;
				}

				drive.dataCapacity = effectiveDataCapacity;
				drive.sampleCapacity = effectiveSampleCapacity;
				UpdateCapacity();
			}

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
					transferVisible = Drive.GetDrives(vessel, true).Count > 1;
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
			if (drive == null)
				return;
			
			double mass = 0;
			foreach (var sample in drive.samples.Values) mass += sample.mass;
			totalSampleMass = mass;

			if (effectiveDataCapacity < 0 || effectiveSampleCapacity < 0 || IsPrivate())
			{
				Fields["Capacity"].guiActive = false;
				Fields["Capacity"].guiActiveEditor = false;
				return;
			}

			double availableDataCapacity = effectiveDataCapacity;
			int availableSlots = effectiveSampleCapacity;

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
				hardDrive.drive.Move(drive, PreferencesScience.Instance.sampleTransfer || Lib.CrewCount(vessel) > 0);
			}
		}


		[KSPEvent(guiActive = false, guiActiveUnfocused = true, guiActiveUncommand = true, guiName = "#KERBALISM_HardDrive_TakeData", active = true)]
		public void TakeData()
		{
			// disable for dead eva kerbals
			Vessel v = FlightGlobals.ActiveVessel;
			if (v == null || EVA.IsDead(v)) return;

			// transfer data
			if(!Drive.Transfer(drive, v, PreferencesScience.Instance.sampleTransfer || Lib.CrewCount(v) > 0))
			{
				Message.Post
				(
					Lib.Color("red", Lib.BuildString("WARNING: not evering copied"), true),
					Lib.BuildString("Storage is at capacity")
				);
			}
		}


		[KSPEvent(guiActive = false, guiActiveUnfocused = true, guiActiveUncommand = true, guiName = "#KERBALISM_HardDrive_TransferData", active = true)]
		public void StoreData()
		{
			// disable for dead eva kerbals
			Vessel v = FlightGlobals.ActiveVessel;
			if (v == null || EVA.IsDead(v)) return;

			// transfer data
			if(!Drive.Transfer(v, drive, PreferencesScience.Instance.sampleTransfer || Lib.CrewCount(v) > 0))
			{
				Message.Post
				(
					Lib.Color("red", Lib.BuildString("WARNING: not evering copied"), true),
					Lib.BuildString("Storage is at capacity")
				);
			}
		}


		// part tooltip
		public override string GetInfo()
		{
			return Specs().Info();
		}


		// science container implementation
		public ScienceData[] GetData()
		{
			return GetData(drive);
		}

		// science container implementation
		public static ScienceData[] GetData(Drive drive)
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
				drive.Delete_file(data.subjectID, data.dataAmount, null);
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
		public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) {
			double result = totalSampleMass;
			if (effectiveSampleCapacity > 0)
				result += effectiveSampleCapacity * sampleCapacityMass;
			if (effectiveDataCapacity > 0)
				result += effectiveDataCapacity * dataCapacityMass;
			return (float)result;
		}
		public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

		public float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
		{
			double result = 0;
			if (effectiveSampleCapacity > 0)
				result += effectiveSampleCapacity * sampleCapacityCost;
			if (effectiveDataCapacity > 0)
				result += effectiveDataCapacity * dataCapacityCost;
			return (float)result;
		}
		public ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
	}


} // KERBALISM


