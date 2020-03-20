using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;



namespace KERBALISM
{
	public class ModuleKsmDrive : KsmPartModule<ModuleKsmDrive, DriveData>, IScienceDataContainer, ISpecifics, IModuleInfo, IPartMassModifier, IPartCostModifier
	{
		public const int CAPACITY_UNLIMITED = -1;
		public const int CAPACITY_AUTO = -2;

		[KSPField] public double dataCapacity = CAPACITY_AUTO;  // base drive capacity, in Mb. -1 = unlimited, -2 = auto
		[KSPField] public int sampleCapacity = CAPACITY_AUTO;   // base drive capacity, in slots. -1 = unlimited, -2 = auto
		[KSPField] public string experiment_id = string.Empty;  // if set, restricts write access to the experiment on the same part, with the given experiment_id.

		[KSPField] public int maxDataCapacityFactor = 4;        // how much additional data capacity to allow in editor
		[KSPField] public int maxSampleCapacityFactor = 4;      // how much additional data capacity to allow in editor

		[KSPField] public float dataCapacityCost = 400;         // added part cost per data capacity
		[KSPField] public float dataCapacityMass = 0.005f;      // added part mass per data capacity
		[KSPField] public float sampleCapacityCost = 300;       // added part cost per sample capacity
		[KSPField] public float sampleCapacityMass = 0.008f;    // added part mass per sample capacity

		[KSPField(isPersistant = true)] public double effectiveDataCapacity = -1.0;    // effective drive capacity, in Mb. -1 = unlimited
		[KSPField(isPersistant = true)] public int effectiveSampleCapacity = -1;     // effective drive capacity, in slots. -1 = unlimited


		[KSPField(isPersistant = false, guiName = "#KERBALISM_HardDrive_DataCapacity", guiActive = false, guiActiveEditor = false, groupName = "Science", groupDisplayName = "#KERBALISM_Group_Science"), UI_ChooseOption(scene = UI_Scene.Editor)]//Data Capacity--Science
		public string dataCapacityUI = "0";
		[KSPField(isPersistant = false, guiName = "#KERBALISM_HardDrive_SampleCapacity", guiActive = false, guiActiveEditor = false, groupName = "Science", groupDisplayName = "#KERBALISM_Group_Science"), UI_ChooseOption(scene = UI_Scene.Editor)]//Sample Capacity--Science
		public string sampleCapacityUI = "0";
		[KSPField(guiActive = true, guiName = "#KERBALISM_HardDrive_Capacity", guiActiveEditor = true, groupName = "Science", groupDisplayName = "#KERBALISM_Group_Science")]//Capacity--Science
		public string Capacity;

		private double totalSampleMass;

		List<KeyValuePair<string, double>> dataCapacities = null;
		List<KeyValuePair<string, int>> sampleCapacities = null;

		public override void OnLoad(ConfigNode node)
		{
			if (HighLogic.LoadedScene != GameScenes.LOADING)
				return;

			if (effectiveDataCapacity < 0)
				effectiveDataCapacity = dataCapacity;

			if (dataCapacity > 0.0 && maxDataCapacityFactor > 0)
			{
				Fields["dataCapacityUI"].guiActiveEditor = true;
				var o = (UI_ChooseOption)Fields["dataCapacityUI"].uiControlEditor;

				dataCapacities = GetDataCapacitySizes();
				int currentCapacityIndex = dataCapacities.FindIndex(p => p.Value == effectiveDataCapacity);
				if (currentCapacityIndex >= 0)
				{
					dataCapacityUI = dataCapacities[currentCapacityIndex].Key;
				}
				else
				{
					effectiveDataCapacity = dataCapacities[0].Value;
					dataCapacityUI = dataCapacities[0].Key;
				}

				string[] dataOptions = new string[dataCapacities.Count];
				for (int i = 0; i < dataCapacities.Count; i++)
					dataOptions[i] = Lib.HumanReadableDataSize(dataCapacities[i].Value);
				o.options = dataOptions;
			}

			if (effectiveSampleCapacity < 0)
				effectiveSampleCapacity = sampleCapacity;

			if (sampleCapacity > 0 && maxSampleCapacityFactor > 0)
			{
				Fields["sampleCapacityUI"].guiActiveEditor = true;
				var o = (UI_ChooseOption)Fields["sampleCapacityUI"].uiControlEditor;

				sampleCapacities = GetSampleCapacitySizes();
				int currentCapacityIndex = sampleCapacities.FindIndex(p => p.Value == effectiveSampleCapacity);
				if (currentCapacityIndex >= 0)
				{
					sampleCapacityUI = sampleCapacities[currentCapacityIndex].Key;
				}
				else
				{
					effectiveSampleCapacity = sampleCapacities[0].Value;
					sampleCapacityUI = sampleCapacities[0].Key;
				}

				string[] sampleOptions = new string[sampleCapacities.Count];
				for (int i = 0; i < sampleCapacities.Count; i++)
					sampleOptions[i] = Lib.HumanReadableSampleSize(sampleCapacities[i].Value);
				o.options = sampleOptions;
			}

			moduleData = new DriveData();
			moduleData.dataCapacity = effectiveDataCapacity;
			moduleData.sampleCapacity = effectiveSampleCapacity;
			moduleData.isPrivate = !string.IsNullOrEmpty(experiment_id);
		}

		public override void OnStart(StartState state)
		{
			// don't break tutorial scenarios
			if (Lib.DisableScenario(this)) return;


			if (Lib.IsEditor)
			{
				if (effectiveDataCapacity < 0)
					effectiveDataCapacity = dataCapacity;

				if (dataCapacity > 0.0 && maxDataCapacityFactor > 0)
				{
					Fields["dataCapacityUI"].guiActiveEditor = true;
					var o =(UI_ChooseOption)Fields["dataCapacityUI"].uiControlEditor;

					dataCapacities = GetDataCapacitySizes();
					int currentCapacityIndex = dataCapacities.FindIndex(p => p.Value == effectiveDataCapacity);
					if (currentCapacityIndex >= 0)
					{
						dataCapacityUI = dataCapacities[currentCapacityIndex].Key;
					}
					else
					{
						effectiveDataCapacity = dataCapacities[0].Value;
						dataCapacityUI = dataCapacities[0].Key;
					}

					string[] dataOptions = new string[dataCapacities.Count];
					for(int i = 0; i < dataCapacities.Count; i++)
						dataOptions[i] = Lib.HumanReadableDataSize(dataCapacities[i].Value);
					o.options = dataOptions;
				}

				if (effectiveSampleCapacity < 0)
					effectiveSampleCapacity = sampleCapacity;

				if (sampleCapacity > 0 && maxSampleCapacityFactor > 0)
				{
					Fields["sampleCapacityUI"].guiActiveEditor = true;
					var o = (UI_ChooseOption)Fields["sampleCapacityUI"].uiControlEditor;

					sampleCapacities = GetSampleCapacitySizes();
					int currentCapacityIndex = sampleCapacities.FindIndex(p => p.Value == effectiveSampleCapacity);
					if (currentCapacityIndex >= 0)
					{
						sampleCapacityUI = sampleCapacities[currentCapacityIndex].Key;
					}
					else
					{
						effectiveSampleCapacity = sampleCapacities[0].Value;
						sampleCapacityUI = sampleCapacities[0].Key;
					}

					string[] sampleOptions = new string[sampleCapacities.Count];
					for (int i = 0; i < sampleCapacities.Count; i++)
						sampleOptions[i] = Lib.HumanReadableSampleSize(sampleCapacities[i].Value);
					o.options = sampleOptions;
				}
			}

			moduleData.dataCapacity = effectiveDataCapacity;
			moduleData.sampleCapacity = effectiveSampleCapacity;
			moduleData.isPrivate = !string.IsNullOrEmpty(experiment_id);

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

		public void FixedUpdate()
		{
			UpdateCapacity();
		}

		public void Update()
		{
			if (moduleData == null)
				return;

			if (Lib.IsEditor)
			{
				bool update = false;
				if(dataCapacities != null)
				{
					foreach (var c in dataCapacities)
						if (c.Key == dataCapacityUI)
						{
							update |= effectiveDataCapacity != c.Value;
							effectiveDataCapacity = c.Value;
						}
				}

				if (sampleCapacities != null)
				{
					foreach (var c in sampleCapacities)
						if (c.Key == sampleCapacityUI)
						{
							update |= effectiveSampleCapacity != c.Value;
							effectiveSampleCapacity = c.Value;
						}
				}

				moduleData.dataCapacity = effectiveDataCapacity;
				moduleData.sampleCapacity = effectiveSampleCapacity;

				Fields["sampleCapacityUI"].guiActiveEditor = sampleCapacity > 0 && !IsPrivate();
				Fields["dataCapacityUI"].guiActiveEditor = dataCapacity > 0 && !IsPrivate();

				if (update)
				{
					UpdateCapacity();
					GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
				}
			}

			if (Lib.IsFlight)
			{
				// show DATA UI button, with size info
				Events["ToggleUI"].guiName = Lib.StatusToggle(Local.HardDrive_Data, moduleData.Empty() ? Local.HardDrive_Dataempty : moduleData.Size());//"Data""empty"
				Events["ToggleUI"].active = !IsPrivate();

				// show TakeData eva action button, if there is something to take
				Events["TakeData"].active = !moduleData.Empty();

				// show StoreData eva action button, if active vessel is an eva kerbal and there is something to store from it
				Vessel v = FlightGlobals.ActiveVessel;
				Events["StoreData"].active = !IsPrivate() && v != null && v.isEVA && !EVA.IsDead(v);

				// hide TransferLocation button
				var transferVisible = !IsPrivate();
				if(transferVisible)
				{
					transferVisible = DriveData.GetDrives(vessel, true).Count() > 1;
				}
				Events["TransferData"].active = transferVisible;
				Events["TransferData"].guiActive = transferVisible;
			}
		}

		public bool IsPrivate()
		{
			return moduleData.isPrivate;
		}

		private void UpdateCapacity()
		{
			if (moduleData == null)
				return;
			
			double mass = 0;
			foreach (var sample in moduleData.samples.Values) mass += sample.mass;
			totalSampleMass = mass;

			if (effectiveDataCapacity < 0 || effectiveSampleCapacity < 0 || IsPrivate())
			{
				Fields["Capacity"].guiActive = false;
				Fields["Capacity"].guiActiveEditor = false;
				return;
			}

			double availableDataCapacity = effectiveDataCapacity;
			int availableSlots = effectiveSampleCapacity;

			if (Lib.IsFlight)
			{
				availableDataCapacity = moduleData.FileCapacityAvailable();
				availableSlots = Lib.SampleSizeToSlots(moduleData.SampleCapacityAvailable());
			}

			Capacity = string.Empty;
			if(availableDataCapacity > double.Epsilon)
				Capacity = Lib.HumanReadableDataSize(availableDataCapacity);
			if(availableSlots > 0)
			{
				if (Capacity.Length > 0) Capacity += " ";
				Capacity += Lib.HumanReadableSampleSize(availableSlots);
			}

			if(Lib.IsFlight && totalSampleMass > double.Epsilon)
			{
				Capacity += " " + Lib.HumanReadableMass(totalSampleMass);
			}
		}

		public DriveData GetDrive()
		{
			return moduleData;
		}

		[KSPEvent(guiActive = true, guiName = "_", active = true, groupName = "Science", groupDisplayName = "#KERBALISM_Group_Science")]//Science
		public void ToggleUI()
		{
			UI.Open((Panel p) => p.Fileman(vessel));
		}

		[KSPEvent(guiName = "#KERBALISM_HardDrive_TransferData", active = false, groupName = "Science", groupDisplayName = "#KERBALISM_Group_Science")]//Science
		public void TransferData()
		{
			var hardDrives = vessel.FindPartModulesImplementing<ModuleKsmDrive>();
			foreach(var hardDrive in hardDrives)
			{
				if (hardDrive == this) continue;
				hardDrive.moduleData.Move(moduleData, PreferencesScience.Instance.sampleTransfer || Lib.CrewCount(vessel) > 0);
			}
		}

		[KSPEvent(guiActive = false, guiActiveUnfocused = true, guiActiveUncommand = true, guiName = "#KERBALISM_HardDrive_TakeData", active = true, groupName = "Science", groupDisplayName = "#KERBALISM_Group_Science")]//Science
		public void TakeData()
		{
			// disable for dead eva kerbals
			Vessel v = FlightGlobals.ActiveVessel;
			if (v == null || EVA.IsDead(v)) return;

			// transfer data
			if(!DriveData.Transfer(moduleData, v, PreferencesScience.Instance.sampleTransfer || Lib.CrewCount(v) > 0))
			{
				Message.Post
				(
					Lib.Color(Lib.BuildString(Local.HardDrive_WARNING_title), Lib.Kolor.Red, true),//"WARNING: not evering copied"
					Lib.BuildString(Local.HardDrive_WARNING)//"Storage is at capacity"
				);
			}
		}

		[KSPEvent(guiActive = false, guiActiveUnfocused = true, guiActiveUncommand = true, guiName = "#KERBALISM_HardDrive_TransferData", active = true, groupName = "Science", groupDisplayName = "#KERBALISM_Group_Science")]//Science
		public void StoreData()
		{
			// disable for dead eva kerbals
			Vessel v = FlightGlobals.ActiveVessel;
			if (v == null || EVA.IsDead(v)) return;

			// transfer data
			if(!DriveData.Transfer(v, moduleData, PreferencesScience.Instance.sampleTransfer || Lib.CrewCount(v) > 0))
			{
				Message.Post
				(
					Lib.Color(Lib.BuildString(Local.HardDrive_WARNING_title), Lib.Kolor.Red, true),//"WARNING: not evering copied"
					Lib.BuildString(Local.HardDrive_WARNING)//"Storage is at capacity"
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
			// generate and return stock science data
			List<ScienceData> data = new List<ScienceData>();

			// this might be called before we had the chance to execute our OnStart() method
			// (looking at you, RasterPropMonitor)
			if(moduleData != null)
			{
				foreach (File file in moduleData.files.Values)
					data.Add(file.ConvertToStockData());

				foreach (Sample sample in moduleData.samples.Values)
					data.Add(sample.ConvertToStockData());
			}

			return data.ToArray();
		}

		// TODO do something about limited capacity...
		// EVAs returning should get a warning if needed
		// TODO : this should not be used for EVA boarding, too much information is lost in the conversion
		public void ReturnData(ScienceData data)
		{
			SubjectData subjectData = ScienceDB.GetSubjectDataFromStockId(data.subjectID);
			if (subjectData == null)
				return;

			if (data.baseTransmitValue > Science.maxXmitDataScalarForSample || data.transmitBonus > Science.maxXmitDataScalarForSample)
			{
				moduleData.RecordFile(subjectData, data.dataAmount);
			}
			else
			{
				moduleData.RecordSample(subjectData, data.dataAmount, subjectData.ExpInfo.MassPerMB * data.dataAmount);
			}
		}

		public void DumpData(ScienceData data)
		{
			SubjectData subjectData = ScienceDB.GetSubjectDataFromStockId(data.subjectID);
			// remove the data
			if (data.baseTransmitValue > float.Epsilon || data.transmitBonus > float.Epsilon)
			{
				moduleData.DeleteFile(subjectData, data.dataAmount);
			}
			else
			{
				moduleData.DeleteSample(subjectData, data.dataAmount);
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
			specs.Add(Local.HardDrive_info1, dataCapacity >= 0 ? Lib.HumanReadableDataSize(dataCapacity) : Local.HardDrive_Capacityunlimited);//"File capacity""unlimited"
			specs.Add(Local.HardDrive_info2, sampleCapacity >= 0 ? Lib.HumanReadableSampleSize(sampleCapacity) : Local.HardDrive_Capacityunlimited);//"Sample capacity""unlimited"
			return specs;
		}

		// module info support
		public string GetModuleTitle() { return "Hard Drive"; }
		public override string GetModuleDisplayName() { return Local.HardDrive; }//"Hard Drive"
		public string GetPrimaryField() { return string.Empty; }
		public Callback<Rect> GetDrawModulePanelCallback() { return null; }

		// module mass support
		public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) {
			double result = totalSampleMass;

			if (effectiveSampleCapacity > sampleCapacity && sampleCapacity > 0)
			{
				var sampleMultiplier = (effectiveSampleCapacity / sampleCapacity) - 1;
				result += sampleMultiplier * sampleCapacityMass;
			}

			if (effectiveDataCapacity > dataCapacity && dataCapacity > 0)
			{
				var dataMultiplier = (effectiveDataCapacity / dataCapacity) - 1.0;
				result += dataMultiplier * dataCapacityMass;
			}

			if(Double.IsNaN(result))
			{
				Lib.Log("Drive mass is NaN: esc " + effectiveSampleCapacity + " scm " + sampleCapacityMass + " dedcm " + effectiveDataCapacity + " dcm " + dataCapacityMass + " tsm " + totalSampleMass);
				return 0;
			}

			return (float)result;
		}
		public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

		// module cost support
		public float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
		{
			double result = 0;

			if(effectiveSampleCapacity > sampleCapacity && sampleCapacity > 0)
			{
				var sampleMultiplier = (effectiveSampleCapacity / sampleCapacity) - 1;
				result += sampleMultiplier * sampleCapacityCost;
			}

			if (effectiveDataCapacity > dataCapacity && dataCapacity > 0)
			{
				var dataMultiplier = (effectiveDataCapacity / dataCapacity) - 1.0;
				result += dataMultiplier * dataCapacityCost;
			}

			return (float)result;
		}
		public ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
	}


} // KERBALISM


