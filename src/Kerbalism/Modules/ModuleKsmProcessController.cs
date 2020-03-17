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
		public PartProcessData ProcessData => partData; PartProcessData partData;
		private bool broken = false;

		private BaseField runningField;

		// parsing configs at prefab compilation
		public override void OnLoad(ConfigNode node)
		{
			if (HighLogic.LoadedScene == GameScenes.LOADING)
			{
				// needed for part module info
				//partData = new PartProcessData(processName, capacity, id, running, broken);
				return;
			}

			if (Lib.IsFlight() && string.IsNullOrEmpty(processName))
			{
				Lib.LogDebug($"Loaded in flight without process, disabling");
				enabled = false;
				isEnabled = false;
			}

			// data will be restored from OnLoad only in the following cases:
			// - Part created in the editor from a saved ship (not a freshly instantiated part from the part list)
			// - Part created in flight from a just launched vessel
			ConfigNode editorDataNode = node.GetNode("EditorProcessData");
			if (editorDataNode != null)
				partData = new PartProcessData(editorDataNode);

			// we might be restarted after a configuration change
			if (Lib.IsEditor())
				StartInternal();
		}

		// this is only for editor <--> editor and editor -> flight persistence
		public override void OnSave(ConfigNode node)
		{
			if (Lib.IsEditor() && partData != null)
			{
				ConfigNode processDataNode = node.AddNode("EditorProcessData");
				partData.Save(processDataNode);
			}
		}

		public override void OnStart(StartState state)
		{
			if (string.IsNullOrEmpty(id))
			{
				// auto-assign ID
				id = part.name + "." + part.Modules.IndexOf(this);
				Lib.Log($"ProcessController `{processName}` on {part.name} without id. Auto-assigning `{id}`, but you really should set a unique id in your configuration", Lib.LogLevel.Warning);
			}

			StartInternal();
		}

		/// <summary>  start the module. must be idempotent: expect to be called several times </summary>
		private void StartInternal()
		{
			Lib.LogDebug($"ProcessController {id} on {part.name} starting with process '{processName}'");

			if (string.IsNullOrEmpty(processName))
			{
				Lib.LogDebug($"No process, disabling module");
				isEnabled = false;
				enabled = false;
				return;
			}

			bool isFlight = Lib.IsFlight();

			// make sure part data is valid (if we have it), we might be restarting with a different configuration
			if(partData != null && partData.processName != processName)
			{
				Lib.LogDebug($"Restarting with different process '{processName}' (was '{partData.processName}'), discarding old part data");
				partData = null;
			}

			// get persistent data
			if (partData == null)
			{
				// in flight, we should have the data stored in VesselData > PartData, unless the part was created in flight (rescue, KIS...)
				if (isFlight)
					partData = PartProcessData.GetFlightReferenceFromPart(part, id);

				// if all other cases, this is a newly instantiated part from prefab. Create the data object and set default values.
				if (partData == null)
				{
					Lib.LogDebug($"Instantiating new part data with processName '{processName}'");
					partData = new PartProcessData(processName, capacity, id, running, broken);

					if(!string.IsNullOrEmpty(processName) && partData.process == null)
					{
						Lib.Log($"Invalid process '{processName}' in ModuleKsmProcessController id {id} for part {part.partName}", Lib.LogLevel.Error);
						isEnabled = false;
						enabled = false;
						return;
					}

					// part was created in flight (rescue, KIS...)
					if (isFlight)
					{
						// set the VesselData / PartData reference
						PartProcessData.SetFlightReferenceFromPart(part, partData);
					}
				}
			}
			else if (isFlight)
			{
				PartProcessData.SetFlightReferenceFromPart(part, partData);
			}

			partData.module = this;

			// PAW setup
			running = partData.process != null && partData.isRunning;

			runningField = Fields["running"];
			runningField.OnValueModified += (field) => Toggle(partData, true);
			runningField.guiActive = runningField.guiActiveEditor = partData.process != null && partData.process.canToggle;
			runningField.guiName = partData.process?.title;

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
			if (partData != null)
				partData.module = null;
		}

		public static void Toggle(PartProcessData partData, bool isLoaded)
		{
			if (partData.isBroken)
				return;

			partData.isRunning = !partData.isRunning;
	
			if (isLoaded)
				partData.module.running = partData.isRunning;

			// refresh VAB/SPH ui
			if (Lib.IsEditor()) GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
		}

		// part tooltip
		public override string GetInfo()
		{
			if (string.IsNullOrEmpty(processName) || partData == null || partData.process == null)
				return string.Empty;
			return Specs().Info(partData.process.desc);
		}

		public bool IsRunning() {
			return running && !string.IsNullOrEmpty(processName);
		}

		// specifics support
		public Specifics Specs()
		{
			Process process = Profile.processes.Find(k => k.name == processName);
			if (process == null)
				return new Specifics();
			return process.Specifics(capacity);
		}

		// module info support
		public string GetModuleTitle() { return partData?.process?.title; }
		public override string GetModuleDisplayName() { return partData?.process?.title; }
		public string GetPrimaryField() { return string.Empty; }
		public Callback<Rect> GetDrawModulePanelCallback() { return null; }

		// animation group support
		public void EnableModule() { }
		public void DisableModule() { }
		public bool ModuleIsActive() { return broken ? false : running; }
		public bool IsSituationValid() { return true; }
	}

} // KERBALISM

