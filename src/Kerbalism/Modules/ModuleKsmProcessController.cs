using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;


namespace KERBALISM
{
	/// <summary>
	/// Replacement for ProcessController
	/// </summary>
	public class ModuleKsmProcessController: PartModule, IModuleInfo, IAnimatedModule, ISpecifics
	{
		// configuration values that are set by module configuration in the editor must be persitant

		[KSPField(isPersistant = true)] public string processName = string.Empty;
		[KSPField(isPersistant = true)] public double capacity = 1.0;
		[KSPField(isPersistant = true)] public string id = string.Empty;

		[KSPField] public string uiGroup = null;          // display name of the UI group

		[KSPField(isPersistant = true)]
		[UI_Toggle(scene = UI_Scene.All, affectSymCounterparts = UI_Scene.None)]
		public bool running;

		// internal state
		private PartProcessData data;
		public PartProcessData ProcessData => data;
		private bool broken = false;

		// caching frequently used things
		private VesselData vd;
		private VesselResHandler vesselResHandler;

		private BaseField runningField;

		// parsing configs at prefab compilation
		public override void OnLoad(ConfigNode node)
		{
			if (HighLogic.LoadedScene == GameScenes.LOADING)
			{
				data = new PartProcessData(processName, capacity, id)
				{
					isRunning = running,
					isBroken = broken
				};
				return;
			}

			data = null;

			if (Lib.IsEditor())
			{
				ConfigNode editorDataNode = node.GetNode("EditorProcessData");
				if (editorDataNode != null)
					data = new PartProcessData(editorDataNode);
			}
		}

		// this is only for editor <--> editor and editor -> flight persistence
		public override void OnSave(ConfigNode node)
		{
			if (Lib.IsEditor() && data != null)
			{
				ConfigNode processDataNode = node.AddNode("EditorProcessData");
				data.Save(processDataNode);
			}
		}

		public override void OnStart(StartState state)
		{
			bool isFlight = Lib.IsFlight();

			// auto-assign ID
			if(string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(processName))
				id = processName + "." + part.Modules.IndexOf(this);

			if (isFlight)
			{
				vd = vessel.KerbalismData();
				vesselResHandler = vd.ResHandler;
			}
			else
			{
				vesselResHandler = EditorResourceHandler.Handler;
			}

			// get persistent data
			// data will be restored from OnLoad (and therefore not null) only in the following cases :
			// - Part created in the editor from a saved ship (not a freshly instantiated part from the part list)
			// - Part created in flight from a just launched vessel
			if (data == null)
			{
				// in flight, we should have the data stored in VesselData > PartData, unless the part was created in flight (rescue, KIS...)
				if (isFlight)
					data = PartProcessData.GetFlightReferenceFromPart(part, id);

				// if all other cases, this is newly instantiated part from prefab. Create the data object and set default values.
				if (data == null)
				{
					data = new PartProcessData(processName, capacity, id)
					{
						isRunning = running,
						isBroken = broken
					};

					if(data.process == null)
					{
						Lib.Log($"Invalid process `{processName}` in ModuleKsmProcessController id {id} for part {part.partName}", Lib.LogLevel.Error);
						isEnabled = false;
						enabled = false;
						return;
					}

					// part was created in flight (rescue, KIS...)
					if (isFlight)
					{
						// set the VesselData / PartData reference
						PartProcessData.SetFlightReferenceFromPart(part, data);
					}
				}
			}
			else if (isFlight)
			{
				PartProcessData.SetFlightReferenceFromPart(part, data);
			}

			data.module = this;

			// PAW setup
			running = data.process != null && data.isRunning;

			runningField = Fields["running"];
			runningField.OnValueModified += (field) => SetRunning(data, true);
			runningField.guiActive = runningField.guiActiveEditor = data.process != null && data.process.canToggle;
			runningField.guiName = data.process.title;

			((UI_Toggle)runningField.uiControlFlight).enabledText = Lib.Color(Local.Generic_ENABLED.ToLower(), Lib.Kolor.Green);
			((UI_Toggle)runningField.uiControlFlight).disabledText = Lib.Color(Local.Generic_DISABLED.ToLower(), Lib.Kolor.Yellow);
			((UI_Toggle)runningField.uiControlEditor).enabledText = Lib.Color(Local.Generic_ENABLED.ToLower(), Lib.Kolor.Green);
			((UI_Toggle)runningField.uiControlEditor).disabledText = Lib.Color(Local.Generic_DISABLED.ToLower(), Lib.Kolor.Yellow);

			if (uiGroup != null)
				runningField.group = new BasePAWGroup(uiGroup, uiGroup, false);
		}

		public void OnDestroy()
		{
			// clear loaded module reference to avoid memory leaks
			if (data != null)
				data.module = null;
		}

		public static void SetRunning(PartProcessData data, bool isLoaded)
		{
			if (data.isBroken)
				return;

			data.isRunning = !data.isRunning;

			if (isLoaded)
				data.module.running = data.isRunning;

			// refresh VAB/SPH ui
			if (Lib.IsEditor()) GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
		}

		// part tooltip
		public override string GetInfo()
		{
			if (string.IsNullOrEmpty(processName) || data == null || data.process == null)
				return string.Empty;
			return Specs().Info(data.process.desc);
		}

		public bool IsRunning() {
			return running && !string.IsNullOrEmpty(processName);
		}

		// specifics support
		public Specifics Specs()
		{
			Process process = Profile.processes.Find(k => k.modifiers.Contains(processName));
			if (process == null)
				return new Specifics();
			return process.Specifics(capacity);
		}

		// module info support
		public string GetModuleTitle() { return data?.process?.title; }
		public override string GetModuleDisplayName() { return data?.process?.title; }
		public string GetPrimaryField() { return string.Empty; }
		public Callback<Rect> GetDrawModulePanelCallback() { return null; }

		// animation group support
		public void EnableModule() { }
		public void DisableModule() { }
		public bool ModuleIsActive() { return broken ? false : running; }
		public bool IsSituationValid() { return true; }
	}

} // KERBALISM

